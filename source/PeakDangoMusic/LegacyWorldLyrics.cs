using System.Globalization;
using System.Text;
using BepInEx.Logging;
using DangoMusic.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakDangoMusic;

/// <summary>Native Unity dynamic-font fallback when TextCore cannot build an SDF atlas.</summary>
internal sealed class LegacyWorldLyrics : IDisposable
{
    readonly MusicSettings settings;readonly ManualLogSource log;
    Font? font;GameObject? root;TextMesh? original,translation;
    Vector3 viewport;
    Quaternion orientation;
    readonly LyricLineView lineView=new();
    readonly List<Material> materials=new();
    string key="",cachedSong="",lastPaint="";int cachedIndex=-1,generation=-1,mode=-1,serial;float paintAt,repositionAt;
    LyricsLine? line;LyricTokens? tokens;LyricAnimation? animation;long end;
    internal string Status=>font!=null?"文字渲染：兼容模式（原生动态字体）":"文字渲染：准备兼容模式";
    internal LegacyWorldLyrics(MusicSettings settings,ManualLogSource log){this.settings=settings;this.log=log;}
    bool Ensure()
    {
        if(font!=null && root!=null)return true;
        try
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Malgun Gothic","Arial"},64);if(font==null)return false;
            root=new GameObject("DangoMusic compatible world lyrics");UnityEngine.Object.DontDestroyOnLoad(root);
            original=Text("Original");translation=Text("Translation");translation.transform.localPosition=new Vector3(0,-.55f,0);
            log.LogInfo("漂浮歌词已启用原生动态字体兼容渲染。");return true;
        }
        catch(Exception e){log.LogWarning("兼容文字字体加载失败: "+e.Message);return false;}
    }
    TextMesh Text(string label)
    {
        var go=new GameObject(label);go.transform.SetParent(root!.transform,false);var text=go.AddComponent<TextMesh>();text.font=font;text.fontSize=64;text.characterSize=.04f;
        text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.richText=true;text.color=LyricPlacement.TextColor;
        var material=new Material(font!.material);materials.Add(material);LyricMaterial.Apply(material,settings);
        var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return text;
    }
    internal void Tick(MusicState state,Camera camera,long now)
    {
        long? clock=state.PositionAt(now);if(clock==null || !state.Playing || !Ensure()){Hide();return;}
        var timeline=lineView.Get(state.Timeline,settings.SplitLines.Value);
        long position=Math.Clamp(clock.Value+settings.Offset.Value,0,state.DurationMs);int index=timeline.FindIndex(position);
        if(index<0){Hide();return;}
        if(cachedSong!=state.Key || generation!=state.LyricGeneration || cachedIndex!=index || mode!=settings.Mode.Value)
        {
            cachedSong=state.Key;generation=state.LyricGeneration;cachedIndex=index;mode=settings.Mode.Value;
            line=LyricUnicode.Normalize(timeline.Lines[index]);end=index+1<timeline.Lines.Count?timeline.Lines[index+1].StartMs:Math.Min(state.DurationMs,line.StartMs+Math.Max(5000,line.DurationMs));
            tokens=mode==0?null:new LyricTokens(line,end,mode==2);animation=new LyricAnimation(line,end);key="";
        }
        if(position>=end){Hide();return;}
        LyricsLine selected=line!;int token=-1;
        if(tokens!=null)
        {
            token=tokens.FindIndex(position);if(token<0){Hide();return;}var t=tokens.Tokens[token];
            selected=new LyricsLine(t.StartMs,Math.Max(1,t.EndMs-t.StartMs),t.Text,new[]{new LyricsWord(t.StartMs,t.EndMs-t.StartMs,t.Text)},line!.Translation);
        }
        string next=state.Key+"/"+generation+"/"+index+"/"+mode+"/"+token;
        if(key!=next){key=next;animation=new LyricAnimation(selected,tokens==null?end:selected.StartMs+selected.DurationMs);lastPaint="";paintAt=0;Place(camera);}
        if(!settings.Follow.Value && settings.Smart.Value && Time.unscaledTime>=repositionAt)
        {
            var p=camera.WorldToViewportPoint(root!.transform.position);
            if(p.z<1 || p.x<.28f || p.x>.72f || p.y<.32f || p.y>.82f)Place(camera);
        }
        if(root==null || original==null || translation==null)return;
        if(settings.Follow.Value)
        {
            root.transform.position=camera.ViewportToWorldPoint(viewport);
            root.SetActive(settings.AllowWalls.Value || !Physics.Linecast(camera.transform.position,root.transform.position,LayerMask.GetMask("Map","Terrain"),QueryTriggerInteraction.Ignore));
        }
        foreach(var material in materials)LyricMaterial.Apply(material,settings);
        root.transform.position=LyricPlacement.KeepDistance(root.transform.position,camera,settings);
        root.transform.rotation=settings.FacePlayer.Value?LyricPlacement.Face(root.transform.position,camera):orientation;
        float factor=settings.FontSize.Value*(settings.Distance.Value/10f);original.characterSize=.04f*factor;translation.characterSize=.029f*factor;translation.transform.localPosition=new Vector3(0,-.55f*factor,0);
        if(Time.unscaledTime>=paintAt)
        {
            paintAt=Time.unscaledTime+.05f;var text=new StringBuilder();int columns=0;
            for(int i=0;i<animation!.Graphemes.Count && i<160;i++)
            {
                var glyph=animation.Graphemes[i];var frame=animation.Sample(i,position,Time.unscaledTime,settings.Pop.Value,settings.Wiggle.Value,settings.Frequency.Value,settings.Amount.Value);
                float alpha=!settings.Pop.Value && !settings.Wiggle.Value?1:(float)frame.Alpha;int size=Math.Clamp((int)Math.Round(64*frame.Scale),48,110);
                if(columns>=22 && !string.IsNullOrWhiteSpace(glyph.Text)){text.Append('\n');columns=0;}
                text.Append("<color=#F7E1D7").Append(((int)(Math.Clamp(alpha,0,1)*255)).ToString("X2")).Append("><size=").Append(size).Append('>').Append(Escape(glyph.Text)).Append("</size></color>");
                columns+=glyph.Text.Any(c=>c>255)?2:1;
            }
            string painted=text.ToString();if(painted!=lastPaint){original.text=painted;lastPaint=painted;}
            translation.text=settings.Translation.Value?Wrap(selected.Translation,32):"";translation.color=LyricPlacement.TextColor;
        }
    }
    void Place(Camera camera)
    {
        if(root==null)return;int mask=LayerMask.GetMask("Map","Terrain");
        foreach(var p in LyricPlacement.Candidates(camera,settings,serial++))
        {
            Vector3 point=camera.ViewportToWorldPoint(p);
            if(!settings.AllowWalls.Value && Physics.Linecast(camera.transform.position,point,mask,QueryTriggerInteraction.Ignore))continue;
            root.transform.position=point;orientation=LyricPlacement.Face(point,camera);root.transform.rotation=orientation;root.SetActive(true);viewport=p;repositionAt=Time.unscaledTime+.08f;return;
        }
        root.SetActive(false);repositionAt=Time.unscaledTime+.8f;
    }
    static string Escape(string text)=>text.Replace('<','‹').Replace('>','›');
    static string Wrap(string text,int width)
    {
        var s=new StringBuilder();var elements=StringInfo.GetTextElementEnumerator(text);int count=0,total=0;
        while(elements.MoveNext() && total++<160){string g=(string)elements.Current;if(count>=width){s.Append('\n');count=0;}s.Append(Escape(g));count+=g.Any(c=>c>255)?2:1;}return s.ToString();
    }
    internal void Hide(){if(root!=null)root.SetActive(false);key="";}
    public void Dispose(){if(root!=null)UnityEngine.Object.Destroy(root);foreach(var material in materials)if(material!=null)UnityEngine.Object.Destroy(material);materials.Clear();if(font!=null)UnityEngine.Object.Destroy(font);root=null;font=null;key="";cachedIndex=-1;}
}
