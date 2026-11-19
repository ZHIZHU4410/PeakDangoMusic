using System;

namespace PeakDangoMusic;

internal static class LyricContrast
{
	internal static bool IsDay(float hour, float start, float end)
	{
		hour = (hour % 24f + 24f) % 24f;
		start = (start % 24f + 24f) % 24f;
		end = (end % 24f + 24f) % 24f;
		if (!(start <= end))
		{
			if (!(hour >= start))
			{
				return hour < end;
			}
			return true;
		}
		if (hour >= start)
		{
			return hour < end;
		}
		return false;
	}

	internal static bool Resolve(bool automatic, bool manualDark, bool daylight)
	{
		if (!automatic)
		{
			return manualDark;
		}
		return daylight;
	}

	internal static float Linear(float channel)
	{
		if (!(channel <= 0.04045f))
		{
			return (float)Math.Pow((channel + 0.055f) / 1.055f, 2.4);
		}
		return channel / 12.92f;
	}

	internal static float Luminance(float r, float g, float b)
	{
		return 0.2126f * Linear(r) + 0.7152f * Linear(g) + 0.0722f * Linear(b);
	}

	internal static bool UseDark(float luminance, bool wasDark)
	{
		if (!wasDark)
		{
			return luminance > 0.205f;
		}
		return luminance > 0.165f;
	}
}
