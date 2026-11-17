using System.Net.Http;
using System.Text;
using DangoMusic.Core;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

// Protocol/platform catalogue adapted from MuoniumPlayer, Apache-2.0.
// C# implementation for PEAK; upstream pinned in docs/MUONIUM.md.
internal sealed class GdMusicApi : IDisposable
{
    internal static readonly string[] Keys={"netease","joox","bilibili","kuwo","tencent","tidal","qobuz","apple","ytmusic","spotify"};
    internal static readonly string[] Names={"网易云音乐","JOOX","哔哩哔哩","酷我音乐","QQ音乐","Tidal","Qobuz","Apple Music","YouTube Music","Spotify"};
    internal sealed record Track(string Id,string Title,string Artist,string Album,string LyricId,string Source);
    internal sealed record Resolved(Track Track,string Url,LyricsTimeline Lyrics,string Note,string Format);
    readonly HttpClient http;
    readonly Queue<long> requests=new();
    readonly Dictionary<string,long> cooldown=new();
    internal const string Endpoint="https://music-api.gdstudio.xyz/api.php";
    internal GdMusicApi(HttpMessageHandler? handler=null)
    {
        http=new HttpClient(handler??new HttpClientHandler{UseCookies=false,UseDefaultCredentials=false}){Timeout=TimeSpan.FromSeconds(12)};
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
    }
    async Task<JToken> Get(string source,string query,CancellationToken ct)
    {
        if(!Keys.Contains(source))throw new ArgumentException("未知聚合平台");
        lock(requests)
        {
            long now=MusicClock.Now;
            while(requests.Count>0&&now-requests.Peek()>=300000)requests.Dequeue();
            if(requests.Count>=50)throw new InvalidOperationException("GD接口请求已达50次/5分钟，请稍后再试");
            if(cooldown.TryGetValue(source,out long until)&&until>now)throw new InvalidOperationException("该平台暂时故障，冷却后可重试");
            requests.Enqueue(now);
        }
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(12000);ct=timeout.Token;
        try
        {
            using var response=await http.GetAsync(Endpoint+"?source="+Uri.EscapeDataString(source)+"&"+query,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using var input=await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            using var data=new MemoryStream();var buffer=new byte[8192];int count;
            while((count=await input.ReadAsync(buffer,0,buffer.Length,ct).ConfigureAwait(false))>0)
            {if(data.Length+count>2*1024*1024)throw new InvalidDataException("GD响应超过2MiB");data.Write(buffer,0,count);}
            using var reader=new Newtonsoft.Json.JsonTextReader(new StringReader(Encoding.UTF8.GetString(data.ToArray()))){MaxDepth=20,DateParseHandling=Newtonsoft.Json.DateParseHandling.None};
            var root=JToken.ReadFrom(reader);
            if(root is JObject o&&(o["detail"]!=null||o["error"]!=null))throw new InvalidDataException((string?)(o["detail"]??o["error"])??"平台未开放");
            return root;
        }
        catch(OperationCanceledException){throw;}
        catch{lock(requests)cooldown[source]=MusicClock.Now+30000;throw;}
    }
    internal async Task<Track[]> Search(string source,string keyword,int page,CancellationToken ct)
    {
        if(string.IsNullOrWhiteSpace(keyword))return Array.Empty<Track>();
        var root=await Get(source,"types=search&name="+Uri.EscapeDataString(keyword.Trim())+"&count=20&pages="+Math.Max(1,page),ct).ConfigureAwait(false);
        if(root is not JArray rows)throw new InvalidDataException("GD搜索响应格式错误");
        return rows.OfType<JObject>().Where(o=>!string.IsNullOrEmpty((string?)o["id"])).Take(20).Select(o=>new Track(
            (string)o["id"]!, (string?)o["name"]??"",o["artist"] is JArray a?string.Join(" / ",a.Values<string>()):(string?)o["artist"]??"",
            (string?)o["album"]??"",(string?)o["lyric_id"]??"",Keys.Contains((string?)o["source"])?(string)o["source"]!:source)).ToArray();
    }
    internal async Task<Resolved> Resolve(Track track,int bitrate,CancellationToken ct)
    {
        string url="",format="";
        foreach(int rate in new[]{bitrate,320,128}.Distinct())
        {
            var root=await Get(track.Source,"types=url&id="+Uri.EscapeDataString(track.Id)+"&br="+rate,ct).ConfigureAwait(false);
            url=(string?)root["url"]??"";
            format=(string?)(root["type"]??root["format"])??"";
            if(Uri.TryCreate(url,UriKind.Absolute,out var uri)&&(uri.Scheme=="https"||uri.Scheme=="http"))break;
            url="";
        }
        if(url.Length==0)throw new InvalidOperationException("平台未返回播放地址，请换一首歌曲或音源");
        LyricsTimeline lyrics=LyricsTimeline.Empty;string note="";
        if(track.LyricId.Length>0)
        {
            try{var root=await Get(track.Source,"types=lyric&id="+Uri.EscapeDataString(track.LyricId),ct).ConfigureAwait(false);lyrics=LyricsTimeline.Parse((string?)root["lyric"],translation:(string?)root["tlyric"]);}
            catch(Exception ex) when(ex is not OperationCanceledException){note=DiagnosticLog.Report("GD聚合音乐","读取同步歌词",ex);}
        }
        return new(track,url,lyrics,note,format);
    }
    public void Dispose()=>http.Dispose();
}
