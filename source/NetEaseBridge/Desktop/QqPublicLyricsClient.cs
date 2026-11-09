using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DangoMusic.Core;
namespace DangoMusic.Desktop;

/// <summary>Public QQ catalog and line-timed lyrics. No accounts, cookies or audio URLs.</summary>
public sealed class QqPublicLyricsClient:IDisposable
{
    readonly HttpClient http;
    public sealed record Track(MusicTrack Metadata,string Mid);
    public QqPublicLyricsClient(HttpMessageHandler? handler=null)
    {
        handler??=new HttpClientHandler{UseCookies=false,AllowAutoRedirect=false,UseDefaultCredentials=false,AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate};
        http=new HttpClient(handler){Timeout=TimeSpan.FromSeconds(12)};
    }
    async Task<JsonDocument> Get(string path,CancellationToken ct)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct);timeout.CancelAfter(15000);ct=timeout.Token;
        var uri=new Uri("https://c.y.qq.com"+path);
        if(uri.Host!="c.y.qq.com"||uri.Scheme!="https"||!uri.IsDefaultPort)throw new InvalidOperationException();
        using var request=new HttpRequestMessage(HttpMethod.Get,uri);request.Headers.Referrer=new Uri("https://y.qq.com/");request.Headers.UserAgent.ParseAdd("Mozilla/5.0 DangoMusic/2.1");
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);response.EnsureSuccessStatusCode();
        if(response.Content.Headers.ContentLength>4*1024*1024)throw new InvalidDataException();
        using var input=await response.Content.ReadAsStreamAsync(ct);using var output=new MemoryStream();byte[] buffer=new byte[8192];int n;
        while((n=await input.ReadAsync(buffer,ct))>0){if(output.Length+n>4*1024*1024)throw new InvalidDataException();output.Write(buffer,0,n);}
        return JsonDocument.Parse(output.ToArray(),new JsonDocumentOptions{MaxDepth=32});
    }
    static JsonElement Child(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)?v:default;
    static string Text(JsonElement e,string key)=>Child(e,key) is var v&&v.ValueKind==JsonValueKind.String?WebUtility.HtmlDecode(v.GetString()??""):"";
    static long Number(JsonElement e,string key)=>Child(e,key) is var v&&v.ValueKind==JsonValueKind.Number&&v.TryGetInt64(out var n)?n:0;
    public static IReadOnlyList<Track> ParseSearch(JsonElement root)
    {
        if(Number(root,"code")!=0)throw new InvalidDataException("QQ曲库搜索暂不可用");
        var list=Child(Child(Child(root,"data"),"song"),"list");var result=new List<Track>();if(list.ValueKind!=JsonValueKind.Array)return result;
        foreach(var item in list.EnumerateArray().Take(100))
        {
            string mid=Text(item,"mid");if(mid.Length==0)mid=Text(item,"songmid");if(!Regex.IsMatch(mid,@"^[A-Za-z0-9]{1,64}$"))continue;
            string title=Text(item,"title");if(title.Length==0)title=Text(item,"songname");var singers=Child(item,"singer");
            string artist=singers.ValueKind==JsonValueKind.Array?string.Join(" / ",singers.EnumerateArray().Select(s=>Text(s,"name"))):"";
            if(result.Any(t=>t.Mid==mid))continue;
            result.Add(new Track(new MusicTrack(result.Count+1,title,artist,Number(item,"interval")*1000),mid));
        }
        return result;
    }
    public static LyricsTimeline ParseLyrics(JsonElement root)
    {
        if(Number(root,"retcode")!=0||Number(root,"code")!=0)throw new InvalidDataException("QQ歌词接口暂不可用");
        string Decode(string key)
        {
            string value=Text(root,key);if(string.IsNullOrWhiteSpace(value)||value.TrimStart().StartsWith("["))return value;
            try{return WebUtility.HtmlDecode(Encoding.UTF8.GetString(Convert.FromBase64String(value)));}catch(FormatException){return "";}
        }
        return LyricsTimeline.Parse(Decode("lyric"),translation:Decode("trans"));
    }
    public async Task<LyricsTimeline> FindAsync(DesktopPlayback track,CancellationToken ct)
    {
        using var search=await Get("/soso/fcgi-bin/client_search_cp?format=json&new_json=1&p=1&n=40&w="+Uri.EscapeDataString(track.Title+" "+track.Artist),ct);
        var candidates=ParseSearch(search.RootElement);var match=SongMatcher.FindBest(track.Title,track.Artist,track.DurationMs,candidates.Select(t=>t.Metadata).ToArray());
        if(match==null)return LyricsTimeline.Empty;
        string mid=candidates.Single(t=>t.Metadata.Id==match.Id).Mid;
        using var lyric=await Get("/lyric/fcgi-bin/fcg_query_lyric_new.fcg?format=json&nobase64=1&g_tk=5381&songmid="+mid,ct);
        return ParseLyrics(lyric.RootElement);
    }
    public void Dispose()=>http.Dispose();
}
