using UnityEngine;

namespace PeakDangoMusic;

internal static class LyricMaterial
{
    internal static void Apply(Material material,MusicSettings settings,AdaptiveLyrics.Palette? palette=null)
    {
        // Keep HDR intensity in the material: TMP vertex colors use Color32 and clamp at 1.
        float brightness=palette?.IsDark==true?1:settings.Brightness.Value;
        bool glow=settings.Glow.Value;
        var white=new Color(brightness,brightness,brightness,1);
        if(material.HasProperty("_FaceColor"))material.SetColor("_FaceColor",palette==null?white:new Color(palette.Face.r*brightness,palette.Face.g*brightness,palette.Face.b*brightness,1));
        if(material.HasProperty("_Color"))material.SetColor("_Color",white);
        if(material.HasProperty("_FaceDilate"))material.SetFloat("_FaceDilate",.08f);
        if(material.HasProperty("_GlowColor"))
        {
            var tint=settings.GlowColor.Value switch{1=>new Color(1,.38f,.62f,1),2=>new Color(.32f,.66f,1,1),3=>new Color(1,.74f,.30f,1),_=>palette?.IsDark==true?new Color(.55f,.78f,1,1):LyricPlacement.TextColor};
            float gain=brightness*settings.GlowStrength.Value*4;
            material.SetColor("_GlowColor",new Color(tint.r*gain,tint.g*gain,tint.b*gain,1));
            if(material.HasProperty("_GlowInner"))material.SetFloat("_GlowInner",0);
            if(material.HasProperty("_GlowOffset"))material.SetFloat("_GlowOffset",0);
            if(material.HasProperty("_GlowPower"))material.SetFloat("_GlowPower",glow?.75f:0);
            if(material.HasProperty("_GlowOuter"))material.SetFloat("_GlowOuter",.08f+.42f*settings.GlowRadius.Value);
            if(glow)material.EnableKeyword("GLOW_ON");else material.DisableKeyword("GLOW_ON");
        }
        if(material.HasProperty("_OutlineWidth")){material.SetFloat("_OutlineWidth",palette!=null?.14f:glow?.04f:0);material.EnableKeyword("OUTLINE_ON");}
        if(material.HasProperty("_OutlineColor"))material.SetColor("_OutlineColor",palette?.Outline??white);
    }
}
