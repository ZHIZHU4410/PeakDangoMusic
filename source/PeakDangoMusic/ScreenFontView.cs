using DangoMusic.Core;
using TMPro;
using UnityEngine;

namespace PeakDangoMusic;

// File-backed TMP glyphs also render in menus, without installing fonts into Windows.
internal sealed class ScreenFontView : IDisposable
{
    readonly MusicSettings settings;
    readonly AdaptiveLyrics.Palette palette=new();
    readonly HashSet<Material> ownedMaterials=new();
    readonly TextHalo mainHalo=new(),translationHalo=new();
    FontLibrary.Asset? asset;
    GameObject? root;
    TextMeshProUGUI? text,translation;
    LyricAnimation? previous;
    TMP_MeshInfo[] original=Array.Empty<TMP_MeshInfo>();
    int[] mapping=Array.Empty<int>();
    bool failed;
    int width,height;
    float scale;
    internal ScreenFontView(MusicSettings settings){this.settings=settings;}
    internal void Hide(){if(root!=null)root.SetActive(false);}
    internal bool Draw(LyricAnimation animation,long position,int serial,string translated)
    {
        if(failed)return false;
        try
        {
            if(root==null)
            {
                asset=new FontLibrary.Asset(settings.FontPath.Value);
                root=new GameObject("DangoMusic custom-font screen",typeof(RectTransform),typeof(Canvas));UnityEngine.Object.DontDestroyOnLoad(root);
                var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32765;
                text=Create("Lyrics");translation=Create("Translation");
            }
            root.SetActive(true);
            bool changed=previous!=animation||width!=Screen.width||height!=Screen.height||scale!=settings.FontSize.Value;
            if(changed)
            {
                previous=animation;width=Screen.width;height=Screen.height;scale=settings.FontSize.Value;
                text!.fontSize=Mathf.Clamp(height/1080f*28*scale,16,84);
                text.text=string.Concat(animation.Graphemes.Select(g=>g.Text));
                text.rectTransform.sizeDelta=new Vector2(width*.32f,height*.3f);
                text.ForceMeshUpdate(true,true);original=text.textInfo.CopyMeshInfoVertexData();mapping=new int[text.textInfo.characterCount];
                for(int i=0;i<mapping.Length;i++){int index=text.textInfo.characterInfo[i].index,g=0;while(g+1<animation.Graphemes.Count&&animation.Graphemes[g+1].SourceStart<=index)g++;mapping[i]=g;}
            }
            var point=new Vector2(width*(serial%2==0?.22f:.78f),height*.39f);
            text!.rectTransform.position=point;
            palette.Update(new Vector2(point.x/width,(point.y-text.preferredHeight*.5f)/height));
            foreach(var m in text.fontMaterials){ownedMaterials.Add(m);LyricMaterial.Apply(m,settings,palette);}
            foreach(var m in translation!.fontMaterials){ownedMaterials.Add(m);LyricMaterial.Apply(m,settings,palette);}
            text.color=Color.white;
            translation!.fontSize=text.fontSize*.7f;translation.rectTransform.sizeDelta=new Vector2(width*.32f,height*.12f);
            translation.rectTransform.position=point-new Vector2(0,text.preferredHeight+8);
            translation.text=settings.Translation.Value?translated:"";translation.color=Color.white;
            var info=text.textInfo;
            for(int m=0;m<info.meshInfo.Length&&m<original.Length;m++)Array.Copy(original[m].vertices,info.meshInfo[m].vertices,Math.Min(original[m].vertices.Length,info.meshInfo[m].vertices.Length));
            Color baseColor=Color.white;baseColor.a=1;
            for(int c=0;c<info.characterCount&&c<mapping.Length;c++)
            {
                var ch=info.characterInfo[c];if(!ch.isVisible)continue;int m=ch.materialReferenceIndex,v=ch.vertexIndex;
                var frame=animation.Sample(mapping[c],position,Time.realtimeSinceStartup,settings.Pop.Value||settings.Mode.Value==2,settings.Wiggle.Value,settings.Frequency.Value,settings.Amount.Value);
                var center=(original[m].vertices[v]+original[m].vertices[v+2])*.5f;var rotation=Quaternion.Euler(0,0,(float)frame.Rotation*Mathf.Rad2Deg);
                var color=baseColor;color.a=(float)frame.Alpha;
                for(int k=0;k<4;k++){info.meshInfo[m].vertices[v+k]=center+rotation*((original[m].vertices[v+k]-center)*(float)frame.Scale)+new Vector3((float)frame.XOffset,-(float)frame.YOffset,0);info.meshInfo[m].colors32[v+k]=color;}
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices|TMP_VertexDataUpdateFlags.Colors32);
            mainHalo.Update(text,settings,palette);translationHalo.Update(translation,settings,palette);return true;
        }
        catch(Exception ex){failed=true;Hide();if(Plugin.Instance!=null)Plugin.Instance.Notice="所选屏幕字体不可用，使用系统字体："+ex.Message;return false;}
    }
    TextMeshProUGUI Create(string name)
    {
        var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(root!.transform,false);var label=go.AddComponent<TextMeshProUGUI>();
        label.extraPadding=true;label.font=asset!.Main;label.richText=false;label.raycastTarget=false;label.alignment=TextAlignmentOptions.Top;label.rectTransform.pivot=new Vector2(.5f,1);label.enableWordWrapping=true;label.maxVisibleLines=3;label.overflowMode=TextOverflowModes.Ellipsis;label.fontSharedMaterial=new Material(label.fontSharedMaterial);return label;
    }
    public void Dispose(){mainHalo.Dispose();translationHalo.Dispose();foreach(var m in ownedMaterials)if(m!=null)UnityEngine.Object.Destroy(m);ownedMaterials.Clear();if(root!=null)UnityEngine.Object.Destroy(root);asset?.Dispose();root=null;asset=null;}
}
