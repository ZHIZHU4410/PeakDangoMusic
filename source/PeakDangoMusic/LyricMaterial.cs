using UnityEngine;

namespace PeakDangoMusic;

internal static class LyricMaterial
{
    internal static void Apply(Material material,MusicSettings settings)
    {
        // Keep HDR intensity in the material: TMP vertex colors use Color32 and clamp at 1.
        float brightness=settings.Brightness.Value;
        var white=new Color(brightness,brightness,brightness,1);
        if(material.HasProperty("_FaceColor"))material.SetColor("_FaceColor",white);
        if(material.HasProperty("_Color"))material.SetColor("_Color",white);
        if(material.HasProperty("_FaceDilate"))material.SetFloat("_FaceDilate",.08f);
        if(material.HasProperty("_GlowColor"))
        {
            var tint=settings.GlowColor.Value switch{1=>new Color(1,.38f,.62f,1),2=>new Color(.32f,.66f,1,1),3=>new Color(1,.74f,.30f,1),_=>LyricPlacement.TextColor};
            float gain=brightness*settings.GlowStrength.Value*4;
            material.SetColor("_GlowColor",new Color(tint.r*gain,tint.g*gain,tint.b*gain,1));
            if(material.HasProperty("_GlowPower"))material.SetFloat("_GlowPower",settings.Glow.Value?.75f:0);
            if(material.HasProperty("_GlowOuter"))material.SetFloat("_GlowOuter",.08f+.42f*settings.GlowRadius.Value);
            if(settings.Glow.Value)material.EnableKeyword("GLOW_ON");else material.DisableKeyword("GLOW_ON");
        }
        if(material.HasProperty("_OutlineWidth"))material.SetFloat("_OutlineWidth",settings.Glow.Value?.04f:0);
        if(material.HasProperty("_OutlineColor"))material.SetColor("_OutlineColor",white);
    }
}
