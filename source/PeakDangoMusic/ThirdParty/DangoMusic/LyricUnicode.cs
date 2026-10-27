using System.Globalization;
using System.Text;

namespace DangoMusic.Core;

/// <summary>Canonical composition for display and timed words; compatibility characters stay intact.</summary>
public static class LyricUnicode
{
    public static LyricsLine Normalize(LyricsLine line)
    {
        if(line==null)throw new ArgumentNullException(nameof(line));
        string text = NormalizeText(line.Text), translation = NormalizeText(line.Translation);
        var words = NormalizeWords(line.Words);
        return text == line.Text && translation == line.Translation && ReferenceEquals(words, line.Words)
            ? line : line with { Text = text, Translation = translation, Words = words };
    }

    private static IReadOnlyList<LyricsWord>? NormalizeWords(IReadOnlyList<LyricsWord>? words)
    {
        if (words == null || words.Count == 0) return words;
        string source = string.Concat(words.Select(word => word.Text));
        if (NormalizeText(source) == source) return words;

        // A timed word can end inside a Hangul syllable or combining sequence. Normalize
        // complete graphemes, assigning that grapheme the union of its contributing intervals.
        // Unaffected prefixes/suffixes retain their original timing rather than merging words.
        var starts = new int[words.Count];
        var ends = new int[words.Count];
        int cursor = 0;
        for (int i = 0; i < words.Count; i++)
        { starts[i] = cursor; cursor += words[i].Text?.Length ?? 0; ends[i] = cursor; }
        int[] elements = StringInfo.ParseCombiningCharacters(source);
        var result = new List<LyricsWord>();
        var pendingText = new StringBuilder();
        int wordIndex = 0, pendingFirst = -1, pendingLast = -1;
        for (int i = 0; i < elements.Length; i++)
        {
            int start = elements[i], end = i + 1 < elements.Length ? elements[i + 1] : source.Length;
            while (wordIndex < words.Count && ends[wordIndex] <= start) wordIndex++;
            int last = wordIndex;
            while (last + 1 < words.Count && starts[last + 1] < end) last++;
            if (pendingFirst != wordIndex || pendingLast != last)
            {
                Flush(); pendingFirst = wordIndex; pendingLast = last;
            }
            pendingText.Append(NormalizeText(source[start..end]));
        }
        Flush();
        return result.AsReadOnly();

        void Flush()
        {
            if (pendingText.Length == 0) return;
            long start = words[pendingFirst].StartMs;
            long duration = words[pendingFirst].DurationMs;
            if (pendingFirst != pendingLast)
            {
                decimal end = decimal.MinValue;
                for (int i = pendingFirst; i <= pendingLast; i++)
                {
                    if (ends[i] == starts[i]) continue; // Empty timing markers contribute no character.
                    start = Math.Min(start, words[i].StartMs);
                    end = Math.Max(end, (decimal)words[i].StartMs + Math.Max(0, words[i].DurationMs));
                }
                duration = (long)Math.Clamp(end - start, 0, long.MaxValue);
            }
            result.Add(new(start, duration, pendingText.ToString())); pendingText.Clear();
        }
    }

    private static string NormalizeText(string? value)
    {
        if (string.IsNullOrEmpty(value)) return value ?? "";
        // .NET NFC rejects malformed UTF-16. Preserve each isolated surrogate exactly,
        // treating it as a barrier, while composing all surrounding valid Unicode runs.
        StringBuilder? output = null;
        int runStart = 0;
        for (int i = 0; i < value.Length; i++)
        {
            if (!char.IsSurrogate(value[i])) continue;
            if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            { i++; continue; }
            output ??= new StringBuilder(value.Length);
            output.Append(value[runStart..i].Normalize(NormalizationForm.FormC));
            output.Append(value[i]); runStart = i + 1;
        }
        if (output == null) return value.Normalize(NormalizationForm.FormC);
        output.Append(value[runStart..].Normalize(NormalizationForm.FormC));
        return output.ToString();
    }
}
