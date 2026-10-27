// Adapted from Sorihiko/DangoMusic LyricSplit, commit da696d0871fa348323782d7d299ba10df46e07db.
// GPL-3.0. CJK spaces split combined lines; short parts are merged, preserving word timing.
namespace DangoMusic.Core;

internal sealed class LyricLineView
{
    LyricsTimeline? source,result;
    bool enabled;
    internal LyricsTimeline Get(LyricsTimeline timeline,bool split)
    {
        if(ReferenceEquals(source,timeline) && enabled==split && result!=null)return result;
        source=timeline;enabled=split;
        var raw=timeline.Lines.Where(line=>!Placeholder(line.Text)).ToArray();
        result=LyricsTimeline.FromLines(split?Split(raw):raw);return result;
    }
    static bool Placeholder(string text)
    {
        string value=string.Concat(text.Where(char.IsLetterOrDigit)).ToLowerInvariant();
        return value=="纯音乐" || value.StartsWith("纯音乐请欣赏",StringComparison.Ordinal) || value.Contains("没有填词的纯音乐") || value=="instrumental" || value=="纯音乐请您欣赏";
    }
    sealed class Segment
    {
        internal string Text="";
        internal long Start;
        internal List<LyricsWord>? Words;
        internal int Count=>Text.Count(c=>!char.IsWhiteSpace(c));
    }
    static List<int> Cuts(string text)
    {
        var starts=new List<int>{0};
        for(int i=0;i<text.Length;)
        {
            if(text[i]!=' ' && text[i]!='\u3000'){i++;continue;}
            int end=i;bool wide=false;
            while(end<text.Length && (text[end]==' ' || text[end]=='\u3000')){wide|=text[end]=='\u3000';end++;}
            int previous=i-1;while(previous>=0 && char.IsWhiteSpace(text[previous]))previous--;
            if(previous>=0 && end<text.Length && (wide || text[previous]>=0x2e80 && text[end]>=0x2e80))starts.Add(end);
            i=end;
        }
        return starts;
    }
    static List<Segment> Segments(LyricsLine line,long? next)
    {
        if(line.Words is { Count:>0 } words)
        {
            string text=string.Concat(words.Select(w=>w.Text));var cuts=Cuts(text);
            if(cuts.Count<2)return new();
            var segments=cuts.Select(_=>new Segment{Start=long.MaxValue,Words=new()}).ToArray();int position=0;
            foreach(var word in words)
            {
                int first=0;while(first<word.Text.Length && char.IsWhiteSpace(word.Text[first]))first++;
                int at=position+(first<word.Text.Length?first:0),index=cuts.FindLastIndex(c=>c<=at);
                var segment=segments[Math.Max(0,index)];segment.Words!.Add(word);
                if(first<word.Text.Length)segment.Start=Math.Min(segment.Start,word.StartMs);
                position+=word.Text.Length;
            }
            return segments.Where(s=>s.Words!.Any(w=>!string.IsNullOrWhiteSpace(w.Text))).Select(s=>{s.Text=string.Concat(s.Words!.Select(w=>w.Text)).Trim();return s;}).ToList();
        }
        var starts=Cuts(line.Text);if(starts.Count<2)return new();
        var parts=starts.Select((start,i)=>line.Text.Substring(start,(i+1<starts.Count?starts[i+1]:line.Text.Length)-start).Trim()).ToArray();
        int total=Math.Max(1,parts.Sum(p=>p.Count(c=>!char.IsWhiteSpace(c)))),before=0;
        long duration=Math.Max(0,(next??line.StartMs+total*300L)-line.StartMs);var output=new List<Segment>();
        foreach(var part in parts)
        {
            output.Add(new Segment{Text=part,Start=line.StartMs+(long)(duration*.85*before/total)});
            before+=part.Count(c=>!char.IsWhiteSpace(c));
        }
        return output;
    }
    static void Merge(Segment previous,Segment next)
    {previous.Text+=" "+next.Text;if(previous.Words!=null && next.Words!=null)previous.Words.AddRange(next.Words);}
    static IEnumerable<LyricsLine> Split(IReadOnlyList<LyricsLine> lines)
    {
        for(int i=0;i<lines.Count;i++)
        {
            var line=lines[i];long? next=i+1<lines.Count?lines[i+1].StartMs:null;
            var segments=Segments(line,next);if(segments.Count<2){yield return line;continue;}
            var merged=new List<Segment>();
            foreach(var segment in segments)
            {
                var previous=merged.LastOrDefault();
                if(previous!=null && (segment.Count<2 || previous.Count<2 || segment.Start-previous.Start<500))Merge(previous,segment);
                else merged.Add(segment);
            }
            if(merged.Count>1 && next.HasValue && next.Value-merged[merged.Count-1].Start<500)
            {var last=merged[merged.Count-1];merged.RemoveAt(merged.Count-1);Merge(merged[merged.Count-1],last);}
            if(merged.Count<2){yield return line;continue;}
            for(int n=0;n<merged.Count;n++)
            {
                var segment=merged[n];long end=n+1<merged.Count?merged[n+1].Start:next??Math.Max(line.StartMs+line.DurationMs,segment.Start+5000);
                yield return new LyricsLine(segment.Start,Math.Max(1,end-segment.Start),segment.Text,segment.Words,line.Translation);
            }
        }
    }
}
