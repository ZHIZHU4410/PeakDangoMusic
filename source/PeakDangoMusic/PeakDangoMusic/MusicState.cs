using System;
using DangoMusic.Core;

namespace PeakDangoMusic;

internal sealed class MusicState
{
	internal readonly bool Connected;

	internal readonly bool Playing;

	internal readonly string Key;

	internal readonly string Title;

	internal readonly string Artist;

	internal readonly string Message;

	internal readonly long PositionMs;

	internal readonly long DurationMs;

	internal readonly long SampleTick;

	internal readonly double Rate;

	internal readonly LyricsTimeline Timeline;

	internal readonly int LyricGeneration;

	internal MusicState(bool connected = false, string key = "", string title = "", string artist = "", long position = 0L, long duration = 0L, bool playing = false, long tick = 0L, double rate = 1.0, LyricsTimeline? timeline = null, int generation = 0, string message = "等待连接 BLF / LX")
	{
		Connected = connected;
		Key = key;
		Title = title;
		Artist = artist;
		PositionMs = position;
		DurationMs = duration;
		Playing = playing;
		SampleTick = tick;
		Rate = rate;
		Timeline = timeline ?? LyricsTimeline.Empty;
		LyricGeneration = generation;
		Message = message;
	}

	internal long? PositionAt(long now)
	{
		long num = now - SampleTick;
		if (!Connected || Key.Length == 0 || num < 0 || num > 3000)
		{
			return null;
		}
		return Math.Clamp(PositionMs + (Playing ? ((long)((double)Math.Min(num, 1500L) * Rate)) : 0), 0L, DurationMs);
	}
}
