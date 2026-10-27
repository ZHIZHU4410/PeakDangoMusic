using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DangoMusic.Core;

namespace DangoMusic.Desktop;

/// <summary>
/// Parses the lyric text served by the 洛雪 / BLF Music open API (<c>/lyric-all</c>) without running
/// the player. Plain LRC supplies line timing; the LXLRC text produced by the client for NetEase,
/// QQ, KuGou, Kuwo and Migu sources adds per-syllable timing as <c>&lt;start,duration&gt;</c>
/// markers whose start is relative to the line, in milliseconds. Fractions are read as literal
/// milliseconds because that is how the client writes and reads them (for example
/// <c>[00:12.5]</c> is 12.005 s, not 12.500 s).
/// </summary>
public static class LxLyricParser
{
    private static readonly Regex LineTags = new(@"^(?:\[[\d:.]+\])+", RegexOptions.CultureInvariant);
    private static readonly Regex TagValue = new(@"\[([\d:.]+)\]", RegexOptions.CultureInvariant);
    private static readonly Regex OffsetTag = new(@"\[offset:([+-]?\d{1,10})\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WordMarker = new(@"<(-?\d{1,9}),(-?\d{1,9})>", RegexOptions.CultureInvariant);
    private static readonly Regex Markers = new(@"<-?\d{1,9},-?\d{1,9}>", RegexOptions.CultureInvariant);

    /// <summary>
    /// Parses one track. <paramref name="lxlyric"/> wins when it yields timed lines; otherwise
    /// <paramref name="lyric"/> is used. Translation lines are matched by timestamp, exactly like the client.
    /// </summary>
    public static LyricsTimeline Parse(string? lyric, string? lxlyric = null, string? translation = null)
    {
        bool useLx = !string.IsNullOrWhiteSpace(lxlyric);
        string? primary = useLx ? lxlyric : lyric;
        long offset = GetOffset(primary);
        var lines = ParseTimed(primary, offset);
        if (lines.Count == 0 && useLx && !string.IsNullOrWhiteSpace(lyric))
        {
            offset = GetOffset(lyric);
            lines = ParseTimed(lyric, offset);
        }
        if (lines.Count == 0) return LyricsTimeline.Empty;
        var translated = ParseTranslation(translation, offset);
        return LyricsTimeline.FromLines(lines, translated.Count == 0 ? null : ToLrc(translated));
    }

    /// <summary>Reads the last <c>[offset:±ms]</c> tag; the client applies it to every line.</summary>
    internal static long GetOffset(string? text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        long offset = 0;
        foreach (Match match in OffsetTag.Matches(text))
            if (long.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value)) offset = value;
        return offset;
    }

    /// <summary>
    /// The client format allows seconds, minutes:seconds or hours:minutes:seconds before an optional
    /// fraction of at most three digits. Minutes and seconds are not required to be two digits wide.
    /// </summary>
    internal static bool TryParseTime(string value, out long milliseconds)
    {
        milliseconds = 0;
        value = value.Trim();
        if (value.Length == 0) return false;
        int dot = value.IndexOf('.');
        string clock = dot >= 0 ? value[..dot] : value;
        string fraction = dot >= 0 ? value[(dot + 1)..] : "";
        if (fraction.Length > 3 || !AllDigits(fraction)) return false;
        string[] parts = clock.Split(':');
        if (parts.Length is < 1 or > 3) return false;
        long[] numbers = new long[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length is < 1 or > 3 || !AllDigits(parts[i])) return false;
            numbers[i] = long.Parse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture);
        }
        long seconds = numbers[^1];
        long minutes = parts.Length >= 2 ? numbers[^2] : 0;
        long hours = parts.Length == 3 ? numbers[0] : 0;
        long fractionMs = fraction.Length == 0 ? 0 : long.Parse(fraction, NumberStyles.None, CultureInfo.InvariantCulture);
        milliseconds = hours * 3_600_000 + minutes * 60_000 + seconds * 1_000 + fractionMs;
        return true;
    }

    private static List<LyricsLine> ParseTimed(string? text, long offset)
    {
        var result = new List<LyricsLine>();
        if (string.IsNullOrWhiteSpace(text)) return result;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            Match tags = LineTags.Match(line);
            if (!tags.Success) continue;
            var times = new List<long>(2);
            foreach (Match value in TagValue.Matches(tags.Value))
                if (TryParseTime(value.Groups[1].Value, out long time)) times.Add(time);
            if (times.Count == 0) continue;
            string body = line[tags.Length..].Trim();
            if (body.Length == 0 || body == "//") continue;
            var words = ParseWords(body);
            string visible = words == null ? Markers.Replace(body, "").Trim() : string.Concat(words.Select(word => word.Text));
            if (visible.Length == 0) continue;
            foreach (long time in times)
            {
                long start = Math.Max(0, time + offset);
                if (words == null)
                {
                    result.Add(new(start, 0, visible));
                    continue;
                }
                var timed = words.Select(word => new LyricsWord(Math.Max(0, start + word.StartMs), word.DurationMs, word.Text)).ToArray();
                result.Add(new(start, 0, visible, timed));
            }
        }
        return result;
    }

    /// <summary>Word markers are optional; text before the first marker keeps line timing only.</summary>
    private static LyricsWord[]? ParseWords(string body)
    {
        var markers = WordMarker.Matches(body);
        if (markers.Count == 0) return null;
        if (!string.IsNullOrWhiteSpace(Markers.Replace(body[..markers[0].Index], ""))) return null;
        var words = new List<LyricsWord>(markers.Count);
        long previous = long.MinValue;
        for (int i = 0; i < markers.Count; i++)
        {
            Match marker = markers[i];
            if (!long.TryParse(marker.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long start) ||
                !long.TryParse(marker.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out long duration)) return null;
            // The client plays markers in file order; out-of-order or negative durations would misplace text.
            if (start < previous || duration < 0) return null;
            previous = start;
            int textStart = marker.Index + marker.Length;
            int textEnd = i + 1 < markers.Count ? markers[i + 1].Index : body.Length;
            words.Add(new(start, duration, body[textStart..textEnd]));
        }
        return words.ToArray();
    }

    /// <summary>Translation and romanization share the lyric clock, including its offset tag.</summary>
    private static Dictionary<long, string> ParseTranslation(string? text, long offset)
    {
        var map = new Dictionary<long, string>();
        if (string.IsNullOrWhiteSpace(text)) return map;
        foreach (string raw in text.Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0) continue;
            Match tags = LineTags.Match(line);
            if (!tags.Success) continue;
            var times = new List<long>(2);
            foreach (Match value in TagValue.Matches(tags.Value))
                if (TryParseTime(value.Groups[1].Value, out long time)) times.Add(time);
            if (times.Count == 0) continue;
            string body = Markers.Replace(line[tags.Length..], "").Trim();
            if (body.Length == 0 || body == "//") continue;
            foreach (long time in times)
            {
                long key = Math.Max(0, time + offset);
                map[key] = map.TryGetValue(key, out string? existing) ? existing + " / " + body : body;
            }
        }
        return map;
    }

    /// <summary>Canonical three-digit LRC so the shared timeline builder sees exact millisecond keys.</summary>
    private static string ToLrc(Dictionary<long, string> translated)
    {
        var builder = new StringBuilder();
        foreach (var (start, text) in translated.OrderBy(pair => pair.Key))
            builder.Append('[').Append((start / 60000).ToString("00", CultureInfo.InvariantCulture)).Append(':')
                .Append((start / 1000 % 60).ToString("00", CultureInfo.InvariantCulture)).Append('.')
                .Append((start % 1000).ToString("000", CultureInfo.InvariantCulture)).Append(']')
                .Append(text).Append('\n');
        return builder.ToString();
    }

    private static bool AllDigits(string value)
    {
        foreach (char c in value) if (c is < '0' or > '9') return false;
        return value.Length > 0;
    }
}
