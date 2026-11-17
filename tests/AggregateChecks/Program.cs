using System.Net;
using PeakDangoMusic;

int checks=0;
void Check(bool valid,string label){if(!valid)throw new Exception(label);checks++;}
using(var api=new GdMusicApi(new Fake((u)=>u.Query.Contains("types=search")?
    "[{\"id\":\"123\",\"name\":\"测试歌\",\"artist\":[\"甲\",\"乙\"],\"lyric_id\":\"lyric123\",\"source\":\"joox\"}]":
    u.Query.Contains("types=url")?"{\"url\":\"https://example.org/audio.mp3\"}":"{\"lyric\":\"[00:01.00]原文\",\"tlyric\":\"[00:01.00]翻译\"}")))
{
    var tracks=await api.Search("netease","测试 & 歌",2,CancellationToken.None);
    Check(tracks.Length==1&&tracks[0].Source=="joox"&&tracks[0].Artist=="甲 / 乙","Preserve returned source/artist identity");
    var result=await api.Resolve(tracks[0],999,CancellationToken.None);
    Check(result.Lyrics.Lines.Count==1&&result.Lyrics.Lines[0].Translation=="翻译","Parse original/translated timeline");
    Check(result.Url=="https://example.org/audio.mp3","Retain audio URL");
}
using(var api=new GdMusicApi(new Fake(_=>"{\"url\":\"file:///private\"}")))
{
    try{await api.Resolve(new("x","","","","","netease"),320,CancellationToken.None);throw new Exception("Accepted local playback URL");}
    catch(InvalidOperationException){checks++;}
}
using(var api=new GdMusicApi(new Fake(_=>"{\"detail\":\"unsupported\"}")))
{
    try{await api.Search("spotify","x",1,CancellationToken.None);throw new Exception("Accepted unsupported response");}catch(InvalidDataException){checks++;}
    try{await api.Search("spotify","x",1,CancellationToken.None);throw new Exception("Ignored platform cooldown");}catch(InvalidOperationException){checks++;}
}
using(var api=new GdMusicApi(new Fake(_=>"[]")))
{
    for(int i=0;i<50;i++)await api.Search("netease","x",1,CancellationToken.None);
    try{await api.Search("netease","x",1,CancellationToken.None);throw new Exception("Ignored shared request budget");}catch(InvalidOperationException){checks++;}
}
using(var api=new GdMusicApi(new Fake(_=>new string('x',2*1024*1024+1))))
{
    try{await api.Search("netease","x",1,CancellationToken.None);throw new Exception("Accepted oversized response");}catch(InvalidDataException){checks++;}
}
Check(GdMusicApi.Keys.Length==10&&GdMusicApi.Keys.Distinct().Count()==10,"Complete GD platform catalogue");
Console.WriteLine($"Aggregate checks passed: {checks}");
if(args.Contains("--live"))
{
    using var api=new GdMusicApi();
    foreach(string platform in GdMusicApi.Keys)
    {
        try
        {
            var results=await api.Search(platform,"Faded",1,CancellationToken.None);
            Console.WriteLine(platform+": search="+results.Length);
            if(platform=="netease"&&results.Length>0)
            {
                var result=await api.Resolve(results[0],128,CancellationToken.None);
                Console.WriteLine("netease: audioUrl="+Uri.IsWellFormedUriString(result.Url,UriKind.Absolute)+", lyricLines="+result.Lyrics.Lines.Count);
            }
        }
        catch(Exception ex){Console.WriteLine(platform+": "+ex.GetType().Name+" / "+ex.Message+" / "+ex.InnerException?.Message);}
    }
}
sealed class Fake(Func<Uri,string> response):HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {ct.ThrowIfCancellationRequested();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(response(request.RequestUri!))});}
}
