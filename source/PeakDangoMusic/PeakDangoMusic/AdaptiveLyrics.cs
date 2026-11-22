using System;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>
/// 歌词字色：要么跟昼夜自动深浅，要么用手动深/浅，要么用调色盘里选的色。
/// </summary>
internal sealed class AdaptiveLyrics : IDisposable
{
	internal sealed class Palette
	{
		internal bool IsDark { get; private set; }

		internal Color Face
		{
			get
			{
				Color custom;
				if (LyricTint(out custom))
				{
					return custom;
				}
				return IsDark ? Dark : Light;
			}
		}

		/// <summary>光晕颜色，跟字色同色；太暗就先提亮，免得深字配深光晕看不见。</summary>
		internal Color Glow
		{
			get
			{
				Color face = Face;
				float luminance = 0.2126f * face.r + 0.7152f * face.g + 0.0722f * face.b;
				if (luminance >= 0.45f)
				{
					return face;
				}
				float boost = 0.45f / Mathf.Max(0.001f, luminance);
				return new Color(Mathf.Min(1f, face.r * boost), Mathf.Min(1f, face.g * boost), Mathf.Min(1f, face.b * boost), 1f);
			}
		}

		internal void Update(Vector2 anchor)
		{
			MusicSettings? settings = Plugin.Instance?.Settings;
			IsDark = settings != null && LyricContrast.Resolve(settings.AutoDayNight.Value, settings.ManualDark.Value, Current?.Daylight ?? false);
		}
	}

	internal static AdaptiveLyrics? Current;

	internal static readonly Color Light = new Color(1f, 0.96f, 0.91f, 1f);

	internal static readonly Color Dark = new Color(0.055f, 0.07f, 0.095f, 1f);

	/// <summary>调色盘选的自定义字色；没选或写坏了就返回 false，照旧走深浅两套。</summary>
	internal static bool LyricTint(out Color color)
	{
		color = Light;
		MusicSettings? settings = Plugin.Instance?.Settings;
		if (settings == null || settings.LyricColor.Value.Length == 0)
		{
			return false;
		}
		Color parsed;
		if (!UiKit.TryParseHex(settings.LyricColor.Value, out parsed))
		{
			return false;
		}
		color = new Color(parsed.r, parsed.g, parsed.b, 1f);
		return true;
	}

	internal bool Daylight { get; private set; }

	internal void Tick()
	{
		DayNightManager instance = DayNightManager.instance;
		if (instance != null)
		{
			Daylight = LyricContrast.IsDay(instance.timeOfDay, instance.dayStart, instance.dayEnd);
		}
	}

	public void Dispose()
	{
		if (Current == this)
		{
			Current = null;
		}
	}
}
