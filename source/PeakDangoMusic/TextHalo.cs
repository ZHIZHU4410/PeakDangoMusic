using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace PeakDangoMusic;

// PEAK supplies Mobile/Distance Field, which has a soft outline but no GLOW_ON pass.
// Separate geometry keeps that halo independent from the dark glyph face and its vertex tint.
internal sealed class TextHalo : IDisposable
{
    sealed class Part
    {
        internal GameObject Root=null!;
        internal Mesh Mesh=null!;
        internal Material Material=null!;
        internal Color32[] Colors=Array.Empty<Color32>();
        internal CanvasRenderer? Canvas;
    }
    readonly List<Part> parts=new();
    internal void Update(TMP_Text text,MusicSettings settings,AdaptiveLyrics.Palette palette)
    {
        var info=text.textInfo;
        var materials=text.fontSharedMaterials;
        for(int i=0;i<info.materialCount&&i<materials.Length;i++)
        {
            var source=info.meshInfo[i];
            if(source.mesh==null)continue;
            while(parts.Count<=i)
            {
                var part=new Part();part.Root=new GameObject("Lyric soft halo",typeof(RectTransform));
                part.Root.layer=text.gameObject.layer;part.Root.transform.SetParent(text.transform,false);
                part.Material=new Material(materials[i]);part.Mesh=new Mesh();
                if(text is TextMeshProUGUI){part.Canvas=part.Root.AddComponent<CanvasRenderer>();part.Canvas.materialCount=1;part.Canvas.SetMaterial(part.Material,0);}
                else
                {
                    part.Root.AddComponent<MeshFilter>().sharedMesh=part.Mesh;
                    var renderer=part.Root.AddComponent<MeshRenderer>();renderer.sharedMaterial=part.Material;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                    renderer.sortingOrder=((TextMeshPro)text).renderer.sortingOrder-1;
                }
                parts.Add(part);
            }
            var p=parts[i];
            bool show=settings.Glow.Value&&settings.GlowStrength.Value>0&&!materials[i].HasProperty("_GlowColor");
            p.Root.SetActive(show);if(!show)continue;
            p.Root.layer=text.gameObject.layer;
            var tint=settings.GlowColor.Value switch{1=>new Color(1,.38f,.62f),2=>new Color(.32f,.66f,1),3=>new Color(1,.74f,.30f),_=>palette.IsDark?new Color(.55f,.78f,1):AdaptiveLyrics.Light};
            float gain=1+settings.GlowStrength.Value*2;
            tint=new Color(tint.r*gain,tint.g*gain,tint.b*gain,Mathf.Clamp01(settings.GlowStrength.Value)*.75f);
            p.Material.EnableKeyword("OUTLINE_ON");
            p.Material.SetColor("_FaceColor",Color.clear);
            p.Material.SetColor("_OutlineColor",tint);
            p.Material.SetFloat("_FaceDilate",0);
            p.Material.SetFloat("_OutlineWidth",.18f+.25f*settings.GlowRadius.Value);
            p.Material.SetFloat("_OutlineSoftness",.2f+.5f*settings.GlowRadius.Value);
            if(p.Material.HasProperty("_ZTestMode")&&materials[i].HasProperty("_ZTestMode"))p.Material.SetFloat("_ZTestMode",materials[i].GetFloat("_ZTestMode"));
            p.Mesh.Clear();p.Mesh.vertices=source.vertices;p.Mesh.SetUVs(0,source.uvs0);p.Mesh.SetUVs(1,source.uvs2);p.Mesh.triangles=source.triangles;
            if(p.Colors.Length!=source.colors32.Length)p.Colors=new Color32[source.colors32.Length];
            for(int k=0;k<p.Colors.Length;k++)p.Colors[k]=new Color32(255,255,255,source.colors32[k].a);
            p.Mesh.colors32=p.Colors;p.Mesh.RecalculateBounds();
            if(p.Canvas!=null){p.Canvas.SetMesh(p.Mesh);p.Canvas.SetTexture(p.Material.mainTexture);}
        }
        for(int i=info.materialCount;i<parts.Count;i++)parts[i].Root.SetActive(false);
    }
    public void Dispose(){foreach(var p in parts){UnityEngine.Object.Destroy(p.Root);UnityEngine.Object.Destroy(p.Mesh);UnityEngine.Object.Destroy(p.Material);}parts.Clear();}
}

