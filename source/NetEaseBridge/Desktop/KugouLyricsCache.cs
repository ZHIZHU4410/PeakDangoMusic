using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using DangoMusic.Core;
namespace DangoMusic.Desktop;

/// <summary>Read only the user's own cached lyrics; never copy cache files into a release.</summary>
public static class KugouLyricsCache
{
    const int Maximum=4*1024*1024;
    static readonly byte[] Key={0x40,0x47,0x61,0x77,0x5e,0x32,0x74,0x47,0x51,0x36,0x31,0x2d,0xce,0xd2,0x6e,0x69};
    static readonly Regex FileName=new(@"^(.*?) - (.*?)(?:-[a-fA-F0-9]{32}-\d+-[a-fA-F0-9]+)?\.(krc|lrc)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
    static readonly Regex Row=new(@"^\[(\d{1,9}),(\d{1,9})\](.*)$",RegexOptions.CultureInvariant);
    static readonly Regex Word=new(@"<(\d{1,9}),(\d{1,9}),\d+>([^<]*)",RegexOptions.CultureInvariant);
    public static LyricsTimeline Parse(byte[] data)
    {
        if(data.Length<5||data.Length>Maximum||Encoding.ASCII.GetString(data,0,4)!="krc1")throw new InvalidDataException("无效KRC文件");
        var compressed=new byte[data.Length-4];for(int i=0;i<compressed.Length;i++)compressed[i]=(byte)(data[i+4]^Key[i%16]);
        using var input=new MemoryStream(compressed);using var z=new ZLibStream(input,CompressionMode.Decompress);using var buffer=new MemoryStream();
        byte[] chunk=new byte[8192];int count;while((count=z.Read(chunk,0,chunk.Length))>0){if(buffer.Length+count>Maximum)throw new InvalidDataException("KRC内容过大");buffer.Write(chunk,0,count);}
        return ParseText(new UTF8Encoding(false,true).GetString(buffer.ToArray()));
    }
    public static LyricsTimeline ParseText(string text)
    {
        if(text.Length>Maximum)throw new InvalidDataException();
        var offsetTag=Regex.Match(text,@"\[offset:([+-]?\d{1,9})\]",RegexOptions.IgnoreCase);long offset=offsetTag.Success?long.Parse(offsetTag.Groups[1].Value):0;
        var lines=new List<LyricsLine>();
        foreach(var raw in text.Split('\n'))
        {
            var row=Row.Match(raw.TrimEnd('\r'));if(!row.Success)continue;
            long start=long.Parse(row.Groups[1].Value)+offset,duration=long.Parse(row.Groups[2].Value);
            if(start<0||start+duration>86400000)continue;
            var words=new List<LyricsWord>();
            foreach(Match word in Word.Matches(row.Groups[3].Value))
            {
                long at=start+long.Parse(word.Groups[1].Value),length=long.Parse(word.Groups[2].Value);string value=WebUtility.HtmlDecode(word.Groups[3].Value);
                if(at+length>86400000||words.Count>=2000)throw new InvalidDataException();
                if(value.Length>0)words.Add(new LyricsWord(at,length,value));
            }
            string lyric=words.Count>0?string.Concat(words.Select(w=>w.Text)):WebUtility.HtmlDecode(row.Groups[3].Value);
            if(!string.IsNullOrWhiteSpace(lyric))lines.Add(new LyricsLine(start,duration,lyric,words));
            if(lines.Count>12000)throw new InvalidDataException();
        }
        return LyricsTimeline.FromLines(lines);
    }
    public static LyricsTimeline Read(DesktopPlayback track,IEnumerable<string> folders)
    {
        var found=new List<LyricsTimeline>();
        foreach(var folder in folders.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                if(!Directory.Exists(folder))continue;
                foreach(var path in Directory.EnumerateFiles(folder,"*",SearchOption.TopDirectoryOnly).Take(5000))
                {
                    var name=FileName.Match(Path.GetFileName(path));if(!name.Success)continue;
                    string Normalize(string value)=>string.Concat(value.Normalize(NormalizationForm.FormKC).Where(char.IsLetterOrDigit)).ToLowerInvariant();
                    if(Normalize(track.Title)!=Normalize(name.Groups[2].Value)&&Regex.IsMatch((track.Title+" "+name.Groups[2].Value).Normalize(NormalizationForm.FormKC),@"\([^)]*(?:版|live|remix|cover|instrumental|伴奏|现场)[^)]*\)",RegexOptions.IgnoreCase))continue;
                    var candidate=new MusicTrack(1,name.Groups[2].Value,name.Groups[1].Value,track.DurationMs);
                    if(SongMatcher.FindBest(track.Title,track.Artist,track.DurationMs,new[]{candidate})==null)continue;
                    var file=new FileInfo(path);if(file.Length<=0||file.Length>Maximum)continue;
                    LyricsTimeline result;
                    try{var data=File.ReadAllBytes(path);result=name.Groups[3].Value.Equals("krc",StringComparison.OrdinalIgnoreCase)?Parse(data):LyricsTimeline.Parse(new UTF8Encoding(false,true).GetString(data));}
                    catch(Exception ex)when(ex is IOException or InvalidDataException or DecoderFallbackException or ArgumentException){continue;}
                    if(result.Lines.Count==0)continue;
                    result=FitToDuration(result,track.DurationMs);
                    if(result.Lines.Count==0)continue;
                    found.Add(result);
                }
            }
            catch(Exception ex)when(ex is IOException or UnauthorizedAccessException or ArgumentException){}
        }
        if(found.Count==0)return LyricsTimeline.Empty;
        // Multiple cached revisions are accepted only when their timed content agrees.
        string Signature(LyricsTimeline t)=>string.Join("\n",t.Lines.Select(l=>$"{l.StartMs}:{l.DurationMs}:{l.Text}:"+string.Join(";",(l.Words??Array.Empty<LyricsWord>()).Select(w=>$"{w.StartMs}:{w.DurationMs}:{w.Text}"))));
        var first=Signature(found[0]);return found.Skip(1).Any(t=>Signature(t)!=first)?LyricsTimeline.Empty:found[0];
    }
    internal static LyricsTimeline FitToDuration(LyricsTimeline timeline,long duration)
    {
        if(duration<=0)return timeline;
        var lines=timeline.Lines.ToArray();
        for(int i=0;i<lines.Length;i++)
        {
            var line=lines[i];
            if(line.StartMs+line.DurationMs<=duration+5000)continue;
            // Some original KRCs put the whole song length in the final row's duration.
            // Repair only that trailing hold when every real word fits the playing track.
            // Genuine late words, untimed rows and earlier corrupt rows remain rejected.
            if(i!=lines.Length-1||line.StartMs>=duration||line.Words is not{Count:>0}||
                line.Words.Any(w=>w.StartMs+w.DurationMs>duration))return LyricsTimeline.Empty;
            lines[i]=line with{DurationMs=duration-line.StartMs};
        }
        return LyricsTimeline.FromLines(lines);
    }
    public static IEnumerable<string> Folders(string? custom=null)
    {
        if(!string.IsNullOrWhiteSpace(custom))yield return custom;
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyMusic),"KuGou","Lyric");
        yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments),"KuGou","Lyric");
        foreach(var drive in DriveInfo.GetDrives())if(drive.DriveType==DriveType.Fixed){yield return Path.Combine(drive.RootDirectory.FullName,"KuGou","Lyric");yield return Path.Combine(drive.RootDirectory.FullName,"KGMusic","Lyric");}
    }
}
