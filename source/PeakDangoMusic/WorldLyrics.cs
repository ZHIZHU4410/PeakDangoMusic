using BepInEx.Logging;
using DangoMusic.Core;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TextCore.LowLevel;

namespace PeakDangoMusic;

/// <summary>World-space TMP meshes only: no colliders, audio, game patches or network objects.</summary>
internal sealed class WorldLyrics : IDisposable
{
    readonly MusicSettings settings;readonly ManualLogSource log;
    readonly List<Entry> entries=new();readonly List<UnityEngine.Object> resources=new();
    TMP_FontAsset? font;GameObject? root;Entry? current;
    string renderKey="",songKey="";long previousPosition=-1;float retryAt,retargetAt;int serial;
    bool fontFailed;
    readonly LegacyWorldLyrics compatible;
    readonly LyricLineView lineView=new();
    internal string RenderStatus=>fontFailed?compatible.Status:font!=null?"文字渲染：系统字体 SDF":"文字渲染：等待进入地图及歌曲／预览";
    string cachedKey="";int cachedGeneration=-1,cachedIndex=-1,cachedMode=-1;LyricsLine? cachedLine;LyricTokens? cachedTokens;long cachedEnd;
    sealed class Entry
    {
        internal GameObject Root=null!;internal TextMeshPro Main=null!,Translation=null!;
        internal LyricsLine Line=null!;internal LyricAnimation Animation=null!;internal TMP_MeshInfo[] Original=null!;
        internal Vector3 Anchor,Viewport;internal Quaternion Orientation;internal float Born,Retired=-1;internal int[] CharacterToGrapheme=Array.Empty<int>();
    }
    internal WorldLyrics(MusicSettings settings,ManualLogSource log){this.settings=settings;this.log=log;compatible=new LegacyWorldLyrics(settings,log);}
    bool EnsureFont()
    {
        if(font!=null)return true;if(fontFailed)return false;
        try
        {
            root=new GameObject("DangoMusic local world lyrics");UnityEngine.Object.DontDestroyOnLoad(root);
            FontEngine.InitializeFontEngine();
            foreach(string file in new[]{"msyh.ttc","msyh.ttf","simhei.ttf","simsun.ttc"})
            {
                var candidate=FileFont(file);
                if(candidate==null)continue;
                if(candidate.TryAddCharacters("歌词ABC",out string missing) && candidate.material!=null && candidate.material.shader.isSupported){font=candidate;break;}
                ReleaseAsset(candidate);
            }
            if(font==null)throw new InvalidOperationException("系统字体文件无法生成可用图集");resources.Add(font);resources.Add(font.material);
            var fallback=FileFont("malgun.ttf");
            if(fallback!=null){resources.Add(fallback);resources.Add(fallback.material);font.fallbackFontAssetTable??=new List<TMP_FontAsset>();font.fallbackFontAssetTable.Add(fallback);}
            log.LogInfo("漂浮歌词字体：从系统字体文件创建SDF，已加载中文字形。");
            return true;
        }
        catch(Exception e){fontFailed=true;font=null;log.LogWarning("SDF字体不可用，改用兼容文字渲染: "+e.Message);return false;}
    }
    static TMP_FontAsset? FileFont(string name)
    {
        string path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),name);
        if(!File.Exists(path))return null;
        return TMP_FontAsset.CreateFontAsset(path,0,64,8,GlyphRenderMode.SDFAA,1024,1024);
    }
    static void ReleaseAsset(TMP_FontAsset asset)
    {foreach(var texture in asset.atlasTextures)if(texture!=null)UnityEngine.Object.Destroy(texture);if(asset.material!=null)UnityEngine.Object.Destroy(asset.material);UnityEngine.Object.Destroy(asset);}
    internal void RetryFonts(){Dispose();fontFailed=false;renderKey="";cachedIndex=-1;}
    internal void Invalidate(){renderKey="";cachedIndex=-1;ClearEntries();compatible.Hide();}
    internal void Hide(){ClearEntries();compatible.Hide();renderKey="";previousPosition=-1;}
    internal void Tick(MusicState state,Camera camera,long now)
    {
        long? clock=state.PositionAt(now);
        if(clock==null){Hide();return;}
        if(!EnsureFont()){ClearEntries();compatible.Tick(state,camera,now);return;}
        compatible.Hide();
        long position=Math.Clamp(clock.Value+settings.Offset.Value,0,state.DurationMs);
        if(state.Key!=songKey || previousPosition>=0 && Math.Abs(position-previousPosition)>1800)
        {ClearEntries();renderKey="";songKey=state.Key;}
        previousPosition=position;
        var timeline=lineView.Get(state.Timeline,settings.SplitLines.Value);
        int index=timeline.FindIndex(position);
        if(index<0){Hide();return;}
        int mode=settings.Mode.Value;
        if(cachedKey!=state.Key || cachedGeneration!=state.LyricGeneration || cachedIndex!=index || cachedMode!=mode)
        {
            cachedKey=state.Key;cachedGeneration=state.LyricGeneration;cachedIndex=index;cachedMode=mode;
            cachedLine=LyricUnicode.Normalize(timeline.Lines[index]);cachedEnd=index+1<timeline.Lines.Count?timeline.Lines[index+1].StartMs:Math.Min(state.DurationMs,cachedLine.StartMs+Math.Max(5000,cachedLine.DurationMs));
            cachedTokens=mode==0?null:new LyricTokens(cachedLine,cachedEnd,mode==2);
        }
        LyricsLine line=cachedLine!;long next=cachedEnd;
        if(position>=next || string.IsNullOrWhiteSpace(line.Text)){Retire();Animate(camera,position);return;}
        int tokenIndex=-1;
        if(mode!=0)
        {
            var tokens=cachedTokens!;tokenIndex=tokens.FindIndex(position);
            if(tokenIndex<0){Retire();Animate(camera,position);return;}
            var token=tokens.Tokens[tokenIndex];next=token.EndMs;
            if(current!=null && current.Line.StartMs==token.StartMs && current.Line.Text==token.Text)line=current.Line;
            else line=new LyricsLine(token.StartMs,Math.Max(1,token.EndMs-token.StartMs),token.Text,new[]{new LyricsWord(token.StartMs,Math.Max(1,token.EndMs-token.StartMs),token.Text)},line.Translation);
        }
        string key=state.Key+"/"+state.LyricGeneration+"/"+index+"/"+mode+"/"+tokenIndex;
        if(key!=renderKey || current==null && Time.unscaledTime>=retryAt)
        {
            Retire();renderKey=key;retryAt=Time.unscaledTime+.8f;current=Spawn(line,next,camera);
        }
        else if(current!=null && !settings.Follow.Value && settings.Smart.Value && Time.unscaledTime>=retargetAt && NearEdge(current,camera))
        {
            Place(current,camera);retargetAt=Time.unscaledTime+.08f;
        }
        Animate(camera,position);
    }
    Entry? Spawn(LyricsLine raw,long next,Camera camera)
    {
        if(root==null || font==null)return null;
        string text=Limit(raw.Text,160),translation=settings.Translation.Value?Limit(raw.Translation,160):"";
        // Truncation changes timed text alignment: fall back to line timing for that rare long line.
        var line=text==raw.Text?raw:new LyricsLine(raw.StartMs,raw.DurationMs,text,null,translation);
        var e=new Entry{Root=new GameObject("DangoMusic lyric"),Line=line,Animation=new LyricAnimation(line,next),Born=Time.unscaledTime};
        e.Root.transform.SetParent(root.transform,false);e.Main=Text(e.Root,"Original",text,settings.WorldFontSize);e.Translation=Text(e.Root,"Translation",translation,settings.WorldFontSize*.72f);
        float width=Mathf.Min(settings.Distance.Value*.6f,6.5f);e.Main.rectTransform.sizeDelta=new Vector2(width,20);e.Translation.rectTransform.sizeDelta=new Vector2(width,20);
        e.Main.ForceMeshUpdate(true,true);e.Translation.ForceMeshUpdate(true,true);
        float height=Mathf.Max(.3f,e.Main.textBounds.size.y);e.Translation.transform.localPosition=new Vector3(0,-height*.6f-.2f,0);
        if(!Place(e,camera)){DestroyEntry(e);return null;}
        e.Original=e.Main.textInfo.CopyMeshInfoVertexData();e.CharacterToGrapheme=new int[e.Main.textInfo.characterCount];
        for(int i=0;i<e.CharacterToGrapheme.Length;i++)
        {
            int source=e.Main.textInfo.characterInfo[i].index,grapheme=0;
            while(grapheme+1<e.Animation.Graphemes.Count && e.Animation.Graphemes[grapheme+1].SourceStart<=source)grapheme++;
            e.CharacterToGrapheme[i]=grapheme;
        }
        entries.Add(e);while(entries.Count>4){var old=entries[0];entries.RemoveAt(0);DestroyEntry(old);}return e;
    }
    TextMeshPro Text(GameObject parent,string label,string value,float size)
    {
        var go=new GameObject(label);go.transform.SetParent(parent.transform,false);var text=go.AddComponent<TextMeshPro>();text.font=font;text.fontSize=size;
        text.text=value;text.richText=false;text.enableWordWrapping=true;text.maxVisibleLines=3;text.overflowMode=TextOverflowModes.Ellipsis;text.alignment=TextAlignmentOptions.Center;
        text.color=LyricPlacement.TextColor;text.raycastTarget=false;
        var material=new Material(text.fontSharedMaterial);text.fontSharedMaterial=material;
        LyricMaterial.Apply(material,settings);
        if(material.HasProperty("_ZTestMode"))material.SetFloat("_ZTestMode",settings.AllowWalls.Value && !settings.BehindPlayer.Value?(int)CompareFunction.Always:(int)CompareFunction.LessEqual);
        text.UpdateMeshPadding();
        text.renderer.shadowCastingMode=ShadowCastingMode.Off;text.renderer.receiveShadows=false;return text;
    }
    bool Place(Entry e,Camera camera)
    {
        var points=LyricPlacement.Candidates(camera,settings,serial++);
        float width=Mathf.Clamp(e.Main.textBounds.size.x,.4f,6.5f),height=Mathf.Clamp(e.Main.textBounds.size.y+e.Translation.textBounds.size.y+.4f,.4f,3.5f);
        int mask=LayerMask.GetMask("Map","Terrain");
        foreach(var point in points)
        {
            Vector3 p=camera.ViewportToWorldPoint(point);
            if(!settings.AllowWalls.Value && (Physics.Linecast(camera.transform.position,p,mask,QueryTriggerInteraction.Ignore) || Physics.CheckBox(p,new Vector3(width*.5f,height*.5f,.12f),camera.transform.rotation,mask,QueryTriggerInteraction.Ignore)))continue;
            e.Root.transform.position=p;e.Orientation=LyricPlacement.Face(p,camera);e.Root.transform.rotation=e.Orientation;e.Anchor=p;e.Viewport=point;return true;
        }
        return false;
    }
    static bool NearEdge(Entry e,Camera camera)
    {
        var p=camera.WorldToViewportPoint(e.Root.transform.position);
        if(p.z<1 || p.x<.28f || p.x>.72f || p.y<.32f || p.y>.82f)return true;
        var bounds=e.Main.renderer.bounds;
        foreach(var v in new[]{bounds.min,bounds.max}){var q=camera.WorldToViewportPoint(v);if(q.x<.025f || q.x>.975f || q.y<.1f || q.y>.97f)return true;}
        return false;
    }
    void Retire()
    {
        if(current==null)return;current.Retired=Time.unscaledTime;current.Translation.text="";current=null;
    }
    void Animate(Camera camera,long position)
    {
        for(int i=entries.Count-1;i>=0;i--)
        {
            var e=entries[i];float time=Time.unscaledTime,age=time-e.Born,leaving=e.Retired<0?0:(time-e.Retired)/.4f;
            if(e.Retired>=0 && (settings.Exit.Value==3 || leaving>=1)){entries.RemoveAt(i);DestroyEntry(e);continue;}
            float alpha=settings.Entrance.Value==3?1:Mathf.Clamp01(age/.25f),scale=1,y=0;
            if(settings.Entrance.Value==1)y=(1-alpha)*-.18f;
            if(settings.Entrance.Value==2)scale=1+.18f*(1-alpha)*(1-alpha);
            if(e.Retired>=0){alpha*=1-leaving;if(settings.Exit.Value==1)y+=leaving*.16f;if(settings.Exit.Value==2)scale*=1-leaving*.5f;}
            if(settings.Follow.Value)
            {
                e.Anchor=camera.ViewportToWorldPoint(e.Viewport);
                bool visible=settings.AllowWalls.Value || !Physics.Linecast(camera.transform.position,e.Anchor,LayerMask.GetMask("Map","Terrain"),QueryTriggerInteraction.Ignore);
                e.Root.SetActive(visible);
            }
            e.Anchor=LyricPlacement.KeepDistance(e.Anchor,camera,settings);
            e.Root.transform.rotation=settings.FacePlayer.Value?LyricPlacement.Face(e.Anchor,camera):e.Orientation;e.Root.transform.position=e.Anchor+camera.transform.up*y;e.Root.transform.localScale=Vector3.one*scale;
            e.Translation.alpha=alpha;
            var info=e.Main.textInfo;
            for(int m=0;m<info.meshInfo.Length && m<e.Original.Length;m++)Array.Copy(e.Original[m].vertices,info.meshInfo[m].vertices,Math.Min(e.Original[m].vertices.Length,info.meshInfo[m].vertices.Length));
            for(int c=0;c<info.characterCount;c++)
            {
                var ch=info.characterInfo[c];if(!ch.isVisible || c>=e.CharacterToGrapheme.Length)continue;
                int mesh=ch.materialReferenceIndex,vi=ch.vertexIndex;if(mesh>=e.Original.Length)continue;
                var frame=e.Animation.Sample(e.CharacterToGrapheme[c],position,time,settings.Pop.Value,settings.Wiggle.Value,settings.Frequency.Value,settings.Amount.Value);
                if(!settings.Pop.Value && !settings.Wiggle.Value)frame=new LyricGlyphFrame(1,1,0,0,0);
                float visible=e.Retired>=0?alpha:alpha*(float)frame.Alpha;
                var original=e.Original[mesh].vertices;var vertices=info.meshInfo[mesh].vertices;var colors=info.meshInfo[mesh].colors32;
                Vector3 centre=(original[vi]+original[vi+2])*.5f;var rotation=Quaternion.Euler(0,0,(float)(frame.Rotation*180/Math.PI));
                float pixel=.03f*settings.FontSize.Value*(settings.Distance.Value/6.5f);
                for(int k=0;k<4;k++)
                {
                    vertices[vi+k]=centre+rotation*((original[vi+k]-centre)*(float)frame.Scale)+new Vector3((float)frame.XOffset*pixel,(float)frame.YOffset*pixel,0);
                    colors[vi+k]=new Color32(247,225,215,(byte)Mathf.RoundToInt(255*Mathf.Clamp01(visible)));
                }
            }
            e.Main.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices|TMP_VertexDataUpdateFlags.Colors32);
        }
    }
    static string Limit(string text,int elements)
    {
        int[] starts=System.Globalization.StringInfo.ParseCombiningCharacters(text);return starts.Length<=elements?text:text.Substring(0,starts[elements])+"…";
    }
    void ClearEntries(){foreach(var e in entries)DestroyEntry(e);entries.Clear();current=null;}
    static void DestroyEntry(Entry e)
    {
        if(e.Main!=null && e.Main.fontSharedMaterial!=null)UnityEngine.Object.Destroy(e.Main.fontSharedMaterial);
        if(e.Translation!=null && e.Translation.fontSharedMaterial!=null)UnityEngine.Object.Destroy(e.Translation.fontSharedMaterial);
        if(e.Root!=null)UnityEngine.Object.Destroy(e.Root);
    }
    public void Dispose(){ClearEntries();compatible.Dispose();if(root!=null)UnityEngine.Object.Destroy(root);root=null;foreach(var r in resources)if(r!=null)UnityEngine.Object.Destroy(r);resources.Clear();font=null;}
}
