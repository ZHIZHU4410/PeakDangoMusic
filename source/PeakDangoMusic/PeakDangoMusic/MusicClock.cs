using System.Diagnostics;

namespace PeakDangoMusic;

internal static class MusicClock
{
	internal static long Now => (long)((double)Stopwatch.GetTimestamp() * (1000.0 / (double)Stopwatch.Frequency));
}
