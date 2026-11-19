using System;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class AdaptiveLyrics : IDisposable
{
	internal sealed class Palette
	{
		internal bool IsDark { get; private set; }

		internal Color Face
		{
			get
			{
				if (!IsDark)
				{
					return Light;
				}
				return Dark;
			}
		}

		internal void Update(Vector2 unused)
		{
			MusicSettings musicSettings = Plugin.Instance?.Settings;
			IsDark = musicSettings != null && LyricContrast.Resolve(musicSettings.AutoDayNight.Value, musicSettings.ManualDark.Value, Current?.Daylight ?? false);
		}
	}

	internal static AdaptiveLyrics? Current;

	internal static readonly Color Light = new Color(1f, 0.96f, 0.91f, 1f);

	internal static readonly Color Dark = new Color(0.055f, 0.07f, 0.095f, 1f);

	internal bool Daylight { get; private set; }

	internal void Tick()
	{
		DayNightManager instance = DayNightManager.instance;
		if ((Object)(object)instance != (Object)null)
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

	static AdaptiveLyrics()
	{
	}
}
