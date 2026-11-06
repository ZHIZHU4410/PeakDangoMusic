using System.Globalization;
using System.Text;

namespace DangoMusic.Core;

public sealed record LyricToken(string Text, int SourceStart, int SourceLength, long StartMs, long EndMs);

public sealed class LyricTokens
{
    public IReadOnlyList<LyricToken> Tokens { get; }
    public long EndMs { get; }
    private readonly int seed;

    /// <param name="chineseWordSegments">Optional Chinese word boundaries in the NFC display text,
    /// measured in UTF-16 code units. Only complete, consecutive Han graphemes are grouped.</param>
    public LyricTokens(LyricsLine line, long nextLineStartMs = 0, bool singleCharacter = false,
        Func<string, IReadOnlyList<(int Start, int Length)>>? chineseWordSegments = null)
    {
        line = LyricUnicode.Normalize(line);
        var graphemes = Graphemes(line.Text);
        var ranges = Split(line.Text, graphemes, singleCharacter, chineseWordSegments);
        long durationEnd = Add(line.StartMs, line.DurationMs > 0 ? line.DurationMs : 5000);
        EndMs = nextLineStartMs > line.StartMs ? Math.Min(durationEnd, nextLineStartMs) : durationEnd;
        // Untimed LRC lines use the next timestamp, even when it is more than five seconds away.
        if (line.DurationMs <= 0 && nextLineStartMs > line.StartMs) EndMs = nextLineStartMs;
        var times = Align(graphemes, ranges, line, EndMs);
        long totalWeight = ranges.Sum(range => (long)range.Weight), weight = 0;
        var result = new LyricToken[ranges.Count];
        for (int index = 0; index < ranges.Count; index++)
        {
            var range = ranges[index];
            long start = times?[index].Start ?? Interpolate(line.StartMs, EndMs, weight, totalWeight);
            weight += range.Weight;
            long end = times?[index].End ?? Interpolate(line.StartMs, EndMs, weight, totalWeight);
            result[index] = new(line.Text[range.Start..range.End], range.Start, range.End - range.Start, start, end);
        }
        Tokens = Array.AsReadOnly(result);
        uint hash = 2166136261;
        foreach (char character in line.Text) hash = unchecked((hash ^ character) * 16777619);
        seed = unchecked((int)hash);
    }

    /// <summary>Hold each token until the next starts; never advance using wall-clock time.</summary>
    public int FindIndex(long songPositionMs)
    {
        if (songPositionMs >= EndMs) return -1;
        int low = 0, high = Tokens.Count - 1, found = -1;
        while (low <= high)
        {
            int middle = low + (high - low) / 2;
            if (Tokens[middle].StartMs <= songPositionMs) { found = middle; low = middle + 1; }
            else high = middle - 1;
        }
        return found;
    }

    /// <summary>Animate one complete token, so a word never accumulates other visible words.</summary>
    public LyricGlyphFrame Sample(int tokenIndex, long songPositionMs, double animationSeconds, bool wordPop = true,
        bool wiggle = true, double wiggleFrequencyHz = 8, double wiggleAmount = .8)
    {
        if ((uint)tokenIndex >= (uint)Tokens.Count || FindIndex(songPositionMs) != tokenIndex) return new(0, 1, 0, 0, 0);
        double age = songPositionMs - (double)Tokens[tokenIndex].StartMs;
        double alpha = wordPop ? Math.Clamp(age * 3 / 160, 0, 1) : 1;
        double back = wordPop ? Math.Clamp(1 - age / 160, 0, 1) : 0;
        double scale = 1 + .55 * back * back, x = 0, y = 0, rotation = 0;
        if (wiggle)
        {
            double frequency = (!double.IsNaN(wiggleFrequencyHz) && !double.IsInfinity(wiggleFrequencyHz)) ? Math.Clamp(wiggleFrequencyHz, 4, 24) : 8;
            double amount = (!double.IsNaN(wiggleAmount) && !double.IsInfinity(wiggleAmount)) ? Math.Clamp(wiggleAmount, 0, 2) : .8;
            double clock = (!double.IsNaN(animationSeconds) && !double.IsInfinity(animationSeconds)) ? Math.Clamp(animationSeconds, 0, long.MaxValue / 48d) : 0;
            long bucket = (long)Math.Floor(clock * frequency);
            rotation = Noise(tokenIndex, bucket, 1) * 9 * amount * Math.PI / 180;
            x = Noise(tokenIndex, bucket, 2) * .8 * amount;
            y = Noise(tokenIndex, bucket, 3) * 1.6 * amount;
            scale *= 1 + Noise(tokenIndex, bucket, 4) * .12 * amount;
        }
        return new(alpha, scale, x, y, rotation);
    }

    private static List<Range> Split(string text, List<Grapheme> graphemes, bool characters,
        Func<string, IReadOnlyList<(int Start, int Length)>>? chineseWordSegments)
    {
        var result = new List<Range>();
        var chineseGroups = !characters && chineseWordSegments != null
            ? ChineseGroups(text, graphemes, chineseWordSegments) : null;
        int start = -1, end = 0, weight = 0;
        int previousChineseGroup = -1;
        bool atomic = false;
        foreach (var grapheme in graphemes)
        {
            if (grapheme.WhiteSpace) { if (!characters) Flush(); continue; }
            if (characters)
            {
                if (Punctuation(grapheme))
                {
                    if (result.Count > 0) result[^1] = result[^1] with { End = grapheme.End };
                    else if (start < 0) start = grapheme.Start;
                }
                else
                {
                    result.Add(new(start >= 0 ? start : grapheme.Start, grapheme.End, 1));
                    start = -1;
                }
                continue;
            }
            var category = CharUnicodeInfo.GetUnicodeCategory(text, grapheme.Start);
            bool punctuation = category is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation
                or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation or UnicodeCategory.InitialQuotePunctuation
                or UnicodeCategory.FinalQuotePunctuation or UnicodeCategory.OtherPunctuation;
            if (punctuation)
            {
                if (weight > 0 && category is UnicodeCategory.OpenPunctuation or UnicodeCategory.InitialQuotePunctuation) Flush();
                if (start < 0) start = grapheme.Start;
                end = grapheme.End;
                continue;
            }
            int scalar = PeakDangoMusic.UnicodeScalar.TryGetRuneAt(text, grapheme.Start, out var rune) ? rune.Value : text[grapheme.Start];
            bool nextAtomic = IsCjk(scalar) || category is UnicodeCategory.MathSymbol or UnicodeCategory.CurrencySymbol
                or UnicodeCategory.ModifierSymbol or UnicodeCategory.OtherSymbol;
            int chineseGroup = chineseGroups != null && chineseGroups.TryGetValue(grapheme.Start, out int group) ? group : -1;
            bool sameChineseWord = chineseGroup >= 0 && chineseGroup == previousChineseGroup;
            if (weight > 0 && (atomic || nextAtomic) && !sameChineseWord) Flush();
            if (start < 0) start = grapheme.Start;
            end = grapheme.End; weight++; atomic = nextAtomic;
            previousChineseGroup = chineseGroup;
        }
        Flush();
        return result;

        void Flush()
        {
            if (start < 0) return;
            var category = CharUnicodeInfo.GetUnicodeCategory(text, start);
            if (weight == 0 && result.Count > 0 && category is not UnicodeCategory.OpenPunctuation and not UnicodeCategory.InitialQuotePunctuation)
                result[^1] = result[^1] with { End = end };
            else result.Add(new(start, end, Math.Max(1, weight)));
            start = -1; weight = 0; atomic = false; previousChineseGroup = -1;
        }
    }

    private static Dictionary<int, int>? ChineseGroups(string text, List<Grapheme> graphemes,
        Func<string, IReadOnlyList<(int Start, int Length)>> segment)
    {
        var han = new Dictionary<int, int>();
        foreach (var grapheme in graphemes)
            if (PeakDangoMusic.UnicodeScalar.TryGetRuneAt(text, grapheme.Start, out var rune) && IsHan(rune.Value))
                han.Add(grapheme.Start, grapheme.End);
        if (han.Count == 0) return null;

        var result = new Dictionary<int, int>();
        int previousEnd = 0;
        foreach (var word in segment(text).OrderBy(word => word.Start))
        {
            long end = (long)word.Start + word.Length;
            if (word.Start < previousEnd || word.Length <= 0 || end > text.Length) continue;
            int cursor = word.Start;
            while (cursor < end && han.TryGetValue(cursor, out int next)) cursor = next;
            // Ignore malformed, partial-grapheme, mixed-script and whitespace-spanning ranges.
            // The normal splitter still handles every character outside accepted Chinese words.
            if (cursor != end) continue;
            for (cursor = word.Start; cursor < end; cursor = han[cursor]) result.Add(cursor, word.Start);
            previousEnd = (int)end;
        }
        return result;
    }

    // One mapping is shared by token selection and whole-line pop animation.
    // Punctuation is decoration, not an extra sung syllable; co-timed fragments
    // share their own interval rather than invalidating every timestamp in the line.
    internal static double[]? TimedGraphemeStarts(LyricsLine line, long lineEnd)
    {
        var times = AlignGraphemes(Graphemes(line.Text), line, lineEnd);
        return times?.Select(time => (double)time.Start).ToArray();
    }

    private static bool Punctuation(Grapheme grapheme) => CharUnicodeInfo.GetUnicodeCategory(grapheme.Text, 0)
        is UnicodeCategory.ConnectorPunctuation or UnicodeCategory.DashPunctuation
            or UnicodeCategory.OpenPunctuation or UnicodeCategory.ClosePunctuation
            or UnicodeCategory.InitialQuotePunctuation or UnicodeCategory.FinalQuotePunctuation
            or UnicodeCategory.OtherPunctuation;

    private static (long Start, long End)[]? AlignGraphemes(List<Grapheme> graphemes, LyricsLine line, long lineEnd)
    {
        if (line.Words == null || line.Words.Count == 0 || lineEnd <= line.StartMs) return null;
        var groups = new List<(long Start, long End, List<int> Indices)>();
        int cursor = 0;
        long previousStart = long.MinValue;
        foreach (var fragment in line.Words)
        {
            var sung = Graphemes(fragment.Text ?? "").Where(g => !g.WhiteSpace && !Punctuation(g)).ToArray();
            if (sung.Length == 0) continue;
            if (fragment.StartMs < previousStart || fragment.StartMs >= lineEnd || fragment.DurationMs < 0) return null;
            previousStart = fragment.StartMs;
            long start = Math.Clamp(fragment.StartMs, line.StartMs, lineEnd);
            long end = Math.Clamp(Add(fragment.StartMs, fragment.DurationMs), start, lineEnd);
            var indices = new List<int>();
            foreach (var glyph in sung)
            {
                while (cursor < graphemes.Count && (graphemes[cursor].WhiteSpace || Punctuation(graphemes[cursor]))) cursor++;
                // Never put another letter's timing on mismatched display text.
                if (cursor >= graphemes.Count || graphemes[cursor].Text != glyph.Text) return null;
                indices.Add(cursor++);
            }
            if (groups.Count > 0 && groups[^1].Start == start)
            {
                var previous = groups[^1];
                previous.Indices.AddRange(indices);
                groups[^1] = (start, Math.Max(previous.End, end), previous.Indices);
            }
            else groups.Add((start, end, indices));
        }
        while (cursor < graphemes.Count && (graphemes[cursor].WhiteSpace || Punctuation(graphemes[cursor]))) cursor++;
        if (cursor != graphemes.Count || groups.Count == 0) return null;
        var result = new (long Start, long End)[graphemes.Count];
        var assigned = new bool[graphemes.Count];
        for (int i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            long boundary = i + 1 < groups.Count ? groups[i + 1].Start : lineEnd;
            long end = group.End > group.Start ? Math.Min(group.End, boundary) : boundary;
            for (int j = 0; j < group.Indices.Count; j++)
            {
                int index = group.Indices[j];
                result[index] = (Interpolate(group.Start, end, j, group.Indices.Count),
                    Interpolate(group.Start, end, j + 1, group.Indices.Count));
                assigned[index] = true;
            }
        }
        var nearby = result[groups[0].Indices[0]];
        for (int i = 0; i < result.Length; i++)
        {
            if (assigned[i]) nearby = result[i];
            else result[i] = nearby;
        }
        return result;
    }

    private static (long Start, long End)[]? Align(List<Grapheme> graphemes, List<Range> ranges, LyricsLine line, long lineEnd)
    {
        if (ranges.Count == 0) return null;
        var times = AlignGraphemes(graphemes, line, lineEnd);
        if (times == null) return null;
        var result = new (long Start, long End)[ranges.Count];
        int cursor = 0;
        for (int index = 0; index < ranges.Count; index++)
        {
            var range = ranges[index];
            long start = long.MaxValue, end = long.MinValue;
            while (cursor < graphemes.Count && graphemes[cursor].Start < range.End)
            {
                if (!graphemes[cursor].WhiteSpace && !Punctuation(graphemes[cursor]) && graphemes[cursor].Start >= range.Start)
                { start = Math.Min(start, times[cursor].Start); end = Math.Max(end, times[cursor].End); }
                cursor++;
            }
            if (start == long.MaxValue)
            {
                int at = graphemes.FindIndex(g => g.Start >= range.Start);
                (start, end) = at >= 0 ? times[at] : (line.StartMs, line.StartMs);
            }
            if (index > 0 && start < result[index - 1].Start) return null;
            result[index] = (start, end);
        }
        return result;
    }

    private static List<Grapheme> Graphemes(string text)
    {
        var starts = StringInfo.ParseCombiningCharacters(text);
        var result = new List<Grapheme>(starts.Length);
        for (int index = 0; index < starts.Length; index++)
        {
            int end = index + 1 < starts.Length ? starts[index + 1] : text.Length;
            string value = text[starts[index]..end];
            result.Add(new(value, starts[index], end, string.IsNullOrWhiteSpace(value)));
        }
        return result;
    }

    private static bool IsCjk(int scalar) => scalar is >= 0x2E80 and <= 0x2FFF or >= 0x3040 and <= 0x312F
        or >= 0x31A0 and <= 0x31FF or >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF
        or >= 0xF900 and <= 0xFAFF or >= 0xFF66 and <= 0xFF9F or >= 0x20000 and <= 0x323AF;

    private static bool IsHan(int scalar) => scalar is >= 0x3400 and <= 0x4DBF or >= 0x4E00 and <= 0x9FFF
        or >= 0xF900 and <= 0xFAFF or >= 0x20000 and <= 0x323AF;

    private static long Add(long start, long duration) => (long)Math.Clamp((decimal)start + duration, long.MinValue, long.MaxValue);
    private static long Interpolate(long start, long end, long numerator, long denominator) => denominator == 0 ? start
        : (long)((decimal)start + ((decimal)end - start) * numerator / denominator);

    private double Noise(int index, long frame, int salt)
    {
        uint hash = unchecked((uint)(index * 73856093) ^ (uint)(frame * 19349663L) ^ (uint)(salt * 83492791) ^ (uint)seed);
        hash ^= hash >> 13; hash = unchecked(hash * 1540483477); hash ^= hash >> 15;
        return (hash & 0xFFFF) / 32767.5 - 1;
    }

    private readonly record struct Range(int Start, int End, int Weight);
    private readonly record struct Grapheme(string Text, int Start, int End, bool WhiteSpace);
}
