using UnityEngine;

namespace PeakDangoMusic;

/// <summary>
/// 给歌词材质刷参数：正面字色、亮度、光晕。
/// 字色由自适应那套调色板决定，这里只管往材质里写。
/// </summary>
internal static class LyricMaterial
{
	internal readonly record struct Appearance(Color Face, Color Glow, float Brightness, bool GlowEnabled,
		float GlowStrength, float GlowRadius);

	internal static Appearance Read(MusicSettings settings, AdaptiveLyrics.Palette? palette = null)
	{
		// 深色字本身已经压得很暗，不再额外乘亮度
		float brightness = palette?.IsDark == true ? 1f : settings.Brightness.Value;
		return new Appearance(palette?.Face ?? AdaptiveLyrics.Light, palette?.Glow ?? AdaptiveLyrics.Light,
			brightness, settings.Glow.Value, settings.GlowStrength.Value, settings.GlowRadius.Value);
	}

	internal static void Apply(Material material, MusicSettings settings, AdaptiveLyrics.Palette? palette = null)
		=> Apply(material, Read(settings, palette));

	internal static void Apply(Material material, Appearance appearance)
	{
		float brightness = appearance.Brightness;
		Color grey = new Color(brightness, brightness, brightness, 1f);

		if (material.HasProperty("_FaceColor"))
		{
			Color face = appearance.Face;
			material.SetColor("_FaceColor", new Color(face.r * brightness, face.g * brightness, face.b * brightness, 1f));
		}
		if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", grey);
		}
		if (material.HasProperty("_FaceDilate"))
		{
			material.SetFloat("_FaceDilate", 0.08f);
		}

		if (material.HasProperty("_GlowColor"))
		{
			// 光晕跟字色同色，太暗时 palette.Glow 会自动往上提
			Color glow = appearance.Glow;
			float power = brightness * appearance.GlowStrength * 4f;
			material.SetColor("_GlowColor", new Color(glow.r * power, glow.g * power, glow.b * power, 1f));

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
				material.SetFloat("_GlowPower", appearance.GlowEnabled ? 0.75f : 0f);
			}
			if (material.HasProperty("_GlowOuter"))
			{
				material.SetFloat("_GlowOuter", 0.08f + 0.42f * appearance.GlowRadius);
			}

			if (appearance.GlowEnabled)
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
