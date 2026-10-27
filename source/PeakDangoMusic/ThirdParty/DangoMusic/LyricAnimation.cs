using System.Globalization;

namespace DangoMusic.Core;

public sealed record LyricGrapheme(string Text, int SourceStart, int SourceLength);
/// <summary>Alpha multiplier, scale, font-pixel offsets and rotation in radians.</summary>
public readonly record struct LyricGlyphFrame(double Alpha, double Scale, double XOffset, double YOffset, double Rotation);

/// <summary>Pure per-grapheme reveal. Song position drives singing; monotonic time drives eight-Hz wiggle.</summary>
public sealed class LyricAnimation
{
    private readonly long lineStartMs;
    private readonly double revealSpanMs;
    private readonly double[]? wordStarts;
    private readonly bool[] visible;
    private readonly int seed;
    public IReadOnlyList<LyricGrapheme> Graphemes { get; }

    public LyricAnimation(LyricsLine line, long nextLineStartMs = 0)
    {
        if(line==null)throw new ArgumentNullException(nameof(line));
        string text = line.Text ?? "";
        lineStartMs = line.StartMs;
        var starts = StringInfo.ParseCombiningCharacters(text);
        var glyphs = new LyricGrapheme[starts.Length];
        visible = new bool[starts.Length];
        for (int i = 0; i < starts.Length; i++)
        {
            int end = i + 1 < starts.Length ? starts[i + 1] : text.Length;
            string grapheme = text[starts[i]..end];
            glyphs[i] = new(grapheme, starts[i], end - starts[i]);
            visible[i] = !grapheme.All(char.IsWhiteSpace);
        }
        Graphemes = Array.AsReadOnly(glyphs);
        double duration = nextLineStartMs > line.StartMs ? nextLineStartMs - (double)line.StartMs
            : line.DurationMs > 0 ? line.DurationMs : 5000;
        revealSpanMs = Math.Min(Math.Max(glyphs.Length * 55d, 250), Math.Max(duration, 400) * .6);
        wordStarts = AlignWordStarts(text, glyphs, line) ?? EstimateWordStarts(glyphs,line.StartMs,duration);
        seed = StableSeed(text);
    }

    public LyricGlyphFrame Sample(int graphemeIndex, long songPositionMs, double animationSeconds, bool wordPop = true, bool wiggle = true,
        double wiggleFrequencyHz = 8, double wiggleAmount = .8)
    {
        if ((uint)graphemeIndex >= (uint)Graphemes.Count || !visible[graphemeIndex]) return new(0, 1, 0, 0, 0);
        double alpha, scale = 1, x = 0, y = 0, rotation = 0;
        if (wordPop && wordStarts != null)
        {
            double ageMs = songPositionMs - wordStarts[graphemeIndex];
            alpha = Math.Clamp(ageMs * 3 / 160, 0, 1);
            double back = Math.Clamp(1 - ageMs / 160, 0, 1);
            scale = 1 + .55 * back * back;
        }
        else
        {
            double appearAt = graphemeIndex / (double)Math.Max(Graphemes.Count, 1) * revealSpanMs;
            double t = Math.Clamp((songPositionMs - (double)lineStartMs - appearAt) / 180, 0, 1);
            alpha = t * t * (3 - 2 * t);
            y = (1 - alpha) * 5;
        }
        if (wiggle)
        {
            double frequency = (!double.IsNaN(wiggleFrequencyHz) && !double.IsInfinity(wiggleFrequencyHz)) ? Math.Clamp(wiggleFrequencyHz, 4, 24) : 8;
            double amount = (!double.IsNaN(wiggleAmount) && !double.IsInfinity(wiggleAmount)) ? Math.Clamp(wiggleAmount, 0, 2) : .8;
            double clock = (!double.IsNaN(animationSeconds) && !double.IsInfinity(animationSeconds)) ? Math.Clamp(animationSeconds, 0, long.MaxValue / 48d) : 0;
            long bucket = (long)Math.Floor(clock * frequency);
            rotation = Noise(graphemeIndex, bucket, 1, seed) * 9 * amount * Math.PI / 180;
            x = Noise(graphemeIndex, bucket, 2, seed) * .8 * amount;
            y += Noise(graphemeIndex, bucket, 3, seed) * 1.6 * amount;
            scale *= 1 + Noise(graphemeIndex, bucket, 4, seed) * .12 * amount;
        }
        return new(alpha, scale, x, y, rotation);
    }

    private static double[] EstimateWordStarts(LyricGrapheme[] glyphs,long start,double duration)
    {
        // Sorihiko fallback: Latin words or pairs of CJK glyphs, over min(75% line, 330ms per word).
        var groups=new int[glyphs.Length];int group=0,cjk=0;bool pending=false;
        for(int i=0;i<glyphs.Length;i++)
        {
            string value=glyphs[i].Text;
            if(value.All(char.IsWhiteSpace)){if(pending){group++;pending=false;cjk=0;}groups[i]=-1;continue;}
            bool asian=value[0]>=0x2e80;
            if(pending && (asian && cjk==0 || !asian && cjk>0)){group++;pending=false;cjk=0;}
            groups[i]=group;pending=true;
            if(asian && ++cjk>=2){group++;pending=false;cjk=0;}
        }
        int count=Math.Max(1,group+(pending?1:0));double span=Math.Min(Math.Max(300,duration)*.75,count*330d);
        return groups.Select(index=>start+span*Math.Max(0,index)/count).ToArray();
    }
    private static double[]? AlignWordStarts(string text, LyricGrapheme[] glyphs, LyricsLine line)
    {
        if (line.Words == null || line.Words.Count == 0) return null;
        var byUtf16 = new double[text.Length];
        Array.Fill(byUtf16, double.NaN);
        int cursor = 0;
        foreach (var word in line.Words)
        foreach (char c in word.Text ?? "")
        {
            if (char.IsWhiteSpace(c)) continue;
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor])) cursor++;
            if (cursor >= text.Length || text[cursor] != c) return null;
            byUtf16[cursor++] = Math.Max(line.StartMs, word.StartMs);
        }
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor])) cursor++;
        if (cursor != text.Length) return null;
        var times = new double[glyphs.Length];
        for (int i = 0; i < glyphs.Length; i++)
        {
            double at = line.StartMs;
            for (int j = glyphs[i].SourceStart; j < glyphs[i].SourceStart + glyphs[i].SourceLength; j++)
                if (!double.IsNaN(byUtf16[j])) at = Math.Max(at, byUtf16[j]);
            times[i] = at;
        }
        return times;
    }

    private static int StableSeed(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text) hash = unchecked((hash ^ c) * 16777619);
        return unchecked((int)hash);
    }

    private static double Noise(int index, long frame, int salt, int textSeed)
    {
        uint hash = unchecked((uint)(index * 73856093) ^ (uint)(frame * 19349663L) ^ (uint)(salt * 83492791) ^ (uint)textSeed);
        hash ^= hash >> 13;
        hash = unchecked(hash * 1540483477);
        hash ^= hash >> 15;
        return (hash & 0xFFFF) / 32767.5 - 1;
    }
}
