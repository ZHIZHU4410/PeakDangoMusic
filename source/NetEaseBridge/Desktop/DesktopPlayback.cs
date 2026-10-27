namespace DangoMusic.Desktop;

public sealed record DesktopPlayback(string Title, string Artist, long SongId,
    long? PositionMs, long DurationMs, bool Playing, long SampleTick, string Source)
{
    public string Key => Title + '\u001f' + Artist + '\u001f' + SongId;
    public long? PositionAt(long tick)
    {
        if (PositionMs is not { } position || tick - SampleTick > 3000) return null;
        long value = position + (Playing ? Math.Clamp(tick - SampleTick, 0, 1500) : 0);
        return Math.Clamp(value, 0, DurationMs > 0 ? DurationMs : long.MaxValue);
    }
}
