namespace DangoMusic.Desktop;

public sealed record DesktopPlayback(string Title, string Artist, long SongId,
    long? PositionMs, long DurationMs, bool Playing, long SampleTick, string Source)
{
    public string Key => Title + '\u001f' + Artist + '\u001f' + SongId;
    public long? PositionAt(long tick)
    {
        if (PositionMs is not { } position) return null;
        // A missed UI Automation/SMTC sample must not hide the lyrics. Keep advancing
        // from the last valid clock until the track ends; the next real sample corrects
        // any drift. Only an explicit track/status change invalidates the clock.
        long maximum = DurationMs > 0 ? DurationMs : long.MaxValue;
        long elapsed = Math.Max(0, tick - SampleTick);
        long value = position + (Playing ? Math.Min(elapsed, Math.Max(0, maximum - position)) : 0);
        return Math.Clamp(value, 0, maximum);
    }
}
