using System.Globalization;
using System.Text.RegularExpressions;

namespace DangoMusic.Core;

public sealed record LyricsWord(long StartMs, long DurationMs, string Text);
public sealed record LyricsLine(long StartMs, long DurationMs, string Text,
    IReadOnlyList<LyricsWord>? Words = null, string Translation = "");

public sealed class LyricsTimeline
{
    public IReadOnlyList<LyricsLine> Lines { get; }
    public static LyricsTimeline Empty { get; } = new(Array.Empty<LyricsLine>());
    private LyricsTimeline(IReadOnlyList<LyricsLine> lines) => Lines = lines;
    private static readonly Regex Timestamp = new(@"\[(\d{1,9}):([0-5]\d)(?:[.:](\d{1,3}))?\]", RegexOptions.CultureInvariant);
    private static readonly Regex Offset = new(@"\[offset:([+-]?\d{1,10})\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex YrcLine = new(@"^\[(\d{1,12}),(\d{1,12})\]", RegexOptions.CultureInvariant);
    private static readonly Regex YrcWord = new(@"\((\d{1,12}),(\d{1,12}),\d+\)", RegexOptions.CultureInvariant);

    public static LyricsTimeline Parse(string? lrc, string? yrc = null, string? translation = null)
    {
        var lines = ParseYrc(yrc);
        if (lines.Count == 0) lines = ParseLrc(lrc);
        return FromLines(lines, translation);
    }

    /// <summary>
    /// Builds a timeline from already timed lines, sorted by start. Translating lines are matched by
    /// their own LRC timestamps; a zero line duration is filled from the next line start.
    /// </summary>
    public static LyricsTimeline FromLines(IEnumerable<LyricsLine> lines, string? translation = null)
    {
        var timed = lines.Where(line => line is not null).ToList();
        if (timed.Count == 0) return Empty;
        var translated = ParseLrc(translation).GroupBy(l => l.StartMs)
            .ToDictionary(g => g.Key, g => string.Join(" / ", g.Select(l => l.Text)));
        var sorted = timed.OrderBy(l => l.StartMs).ToArray();
        for (int i = 0; i < sorted.Length; i++)
        {
            var line = sorted[i];
            if (line.DurationMs == 0 && i + 1 < sorted.Length)
                line = line with { DurationMs = sorted[i + 1].StartMs - line.StartMs };
            if (translated.TryGetValue(line.StartMs, out var text)) line = line with { Translation = text };
            sorted[i] = line;
        }
        return new(Array.AsReadOnly(sorted));
    }

    public int FindIndex(long positionMs)
    {
        int low = 0, high = Lines.Count - 1, found = -1;
        while (low <= high)
        {
            int mid = low + (high - low) / 2;
            if (Lines[mid].StartMs <= positionMs) { found = mid; low = mid + 1; }
            else high = mid - 1;
        }
        return found;
    }

    private static long GetOffset(string text)
    {
        var matches = Offset.Matches(text);
        return matches.Count > 0 ? long.Parse(matches[^1].Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static List<LyricsLine> ParseLrc(string? text)
    {
        var result = new List<LyricsLine>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        long offset = GetOffset(text);
        foreach (var raw in text.Split('\n'))
        {
            var stamps = Timestamp.Matches(raw);
            if (stamps.Count == 0) continue;
            // Only leading timestamp tags are timing instructions; leave lyric text intact.
            int end = 0;
            foreach (Match stamp in stamps)
            {
                if (!string.IsNullOrWhiteSpace(raw[end..stamp.Index])) break;
                end = stamp.Index + stamp.Length;
            }
            if (end == 0) continue;
            string lyric = raw[end..].Trim();
            foreach (Match stamp in stamps)
            {
                if (stamp.Index >= end) break;
                long minutes = long.Parse(stamp.Groups[1].Value, CultureInfo.InvariantCulture);
                long seconds = long.Parse(stamp.Groups[2].Value, CultureInfo.InvariantCulture);
                string fraction = stamp.Groups[3].Value.PadRight(3, '0');
                long milliseconds = fraction.Length == 0 ? 0 : long.Parse(fraction, CultureInfo.InvariantCulture);
                result.Add(new(Math.Max(0, minutes * 60000 + seconds * 1000 + milliseconds + offset), 0, lyric));
            }
        }
        return result;
    }

    private static List<LyricsLine> ParseYrc(string? text)
    {
        var result = new List<LyricsLine>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        long offset = GetOffset(text);
        foreach (var raw in text.Split('\n'))
        {
            var head = YrcLine.Match(raw.Trim());
            if (!head.Success) continue;
            string body = raw.Trim()[head.Length..];
            var markers = YrcWord.Matches(body);
            if (markers.Count == 0) continue;
            var words = new List<LyricsWord>();
            for (int i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                int start = marker.Index + marker.Length;
                int end = i + 1 < markers.Count ? markers[i + 1].Index : body.Length;
                words.Add(new(Math.Max(0, long.Parse(marker.Groups[1].Value, CultureInfo.InvariantCulture) + offset),
                    long.Parse(marker.Groups[2].Value, CultureInfo.InvariantCulture), body[start..end].TrimEnd('\r')));
            }
            result.Add(new(Math.Max(0, long.Parse(head.Groups[1].Value, CultureInfo.InvariantCulture) + offset),
                long.Parse(head.Groups[2].Value, CultureInfo.InvariantCulture), string.Concat(words.Select(w => w.Text)), words.AsReadOnly()));
        }
        return result;
    }
}
