using UnityEngine;

namespace PeakDangoMusic;

internal static class LyricMaterial
{
	internal static void Apply(Material material, MusicSettings settings, AdaptiveLyrics.Palette? palette = null)
	{
		float num = ((palette != null && palette.IsDark) ? 1f : settings.Brightness.Value);
		bool value = settings.Glow.Value;
		Color val = new Color(num, num, num, 1f);
		if (material.HasProperty("_FaceColor"))
		{
			material.SetColor("_FaceColor", (Color)((palette == null) ? val : new Color(palette.Face.r * num, palette.Face.g * num, palette.Face.b * num, 1f)));
		}
		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", val);
		}
		if (material.HasProperty("_FaceDilate"))
		{
			material.SetFloat("_FaceDilate", 0.08f);
		}
		if (material.HasProperty("_GlowColor"))
		{
			Color val2 = settings.GlowColor.Value switch
			{
				1 => new Color(1f, 0.38f, 0.62f, 1f),
				2 => new Color(0.32f, 0.66f, 1f, 1f),
				3 => new Color(1f, 0.74f, 0.3f, 1f),
				_ => (Color)((palette != null && palette.IsDark) ? new Color(0.55f, 0.78f, 1f, 1f) : LyricPlacement.TextColor),
			};
			float num2 = num * settings.GlowStrength.Value * 4f;
			material.SetColor("_GlowColor", new Color(val2.r * num2, val2.g * num2, val2.b * num2, 1f));
			if (material.HasProperty("_GlowInner"))
			{
				material.SetFloat("_GlowInner", 0f);
			}
			if (material.HasProperty("_GlowOffset"))
			{
				material.SetFloat("_GlowOffset", 0f);
			}
			if (material.HasProperty("_GlowPower"))
			{
				material.SetFloat("_GlowPower", value ? 0.75f : 0f);
			}
			if (material.HasProperty("_GlowOuter"))
			{
				material.SetFloat("_GlowOuter", 0.08f + 0.42f * settings.GlowRadius.Value);
			}
			if (value)
			{
				material.EnableKeyword("GLOW_ON");
			}
			else
			{
				material.DisableKeyword("GLOW_ON");
			}
		}
		if (material.HasProperty("_OutlineWidth"))
		{
			material.SetFloat("_OutlineWidth", 0f);
		}
		if (material.HasProperty("_OutlineColor"))
		{
			material.SetColor("_OutlineColor", Color.clear);
		}
		material.DisableKeyword("OUTLINE_ON");
	}
}
