using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DangoMusic.Core;
using DangoMusic.Desktop;

// Windows APIs stay in this isolated helper; Unity Mono only receives JSON over its child stdout.
Console.OutputEncoding=new UTF8Encoding(false);
if(args.Length==1&&args[0]=="--check-qq-lyrics")
{
    using var api=new QqPublicLyricsClient();using var timeout=new CancellationTokenSource(30000);
    var t=await api.FindAsync(new DesktopPlayback("来不及爱你","h3R3刘清云",0,0,205287,false,Environment.TickCount64,"test"),timeout.Token);
    Console.WriteLine(JsonSerializer.Serialize(new{source="QQ",lines=t.Lines.Count}));return;
}
if(args.Length==1&&args[0]=="--check-kugou-cache")
{
    foreach(var pair in new[]{("人生短短数十年","欢欢"),("像雾像雨又像风 (深情版)","烬的歌"),("来不及爱你","h3R3刘清云"),("雨爱","杨丞琳")})
    {
        var track=new DesktopPlayback(pair.Item1,pair.Item2,0,0,pair.Item1=="雨爱"?260000:0,false,Environment.TickCount64,"test");
        var t=KugouLyricsCache.Read(track,KugouLyricsCache.Folders());
        Console.WriteLine(JsonSerializer.Serialize(new{title=pair.Item1,lines=t.Lines.Count,words=t.Lines.Sum(l=>l.Words?.Count??0)}));
    }
    return;
}
if(args.Length==2&&args[0]=="--probe-kugou")
{
    using var reader=new KugouApiReader();
    for(int i=0;i<6;i++)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();var t=await reader.ReadAsync(CancellationToken.None);
        await File.AppendAllTextAsync(args[1],JsonSerializer.Serialize(new{elapsedMs=watch.ElapsedMilliseconds,track=t,status=reader.Status})+"\n");
        await Task.Delay(4500);
    }
    return;
}
if(args.Length==1&&args[0]=="--sessions")
{
    var manager=await Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
    foreach(var session in manager.GetSessions()){var media=await session.TryGetMediaPropertiesAsync();var time=session.GetTimelineProperties();Console.WriteLine(JsonSerializer.Serialize(new{app=session.SourceAppUserModelId,title=media.Title,artist=media.Artist,position=time.Position.TotalMilliseconds,duration=time.EndTime.TotalMilliseconds,updated=time.LastUpdatedTime,status=session.GetPlaybackInfo().PlaybackStatus.ToString()}));}
    return;
}
if(args.Length is not (2 or 3 or 4) || !int.TryParse(args[0],out int parentId) || !int.TryParse(args[1],out int port) || port<1 || port>65535)return;
string source=args.Length>=3?args[2]:"netease";
if(source is not ("netease" or "kugou" or "qqmusic"))return;
string player=PlayerIdentity.Label(source);
using var parent=Process.GetProcessById(parentId);
using var cancel=new CancellationTokenSource();
using var cdp=new CdpProgressReader(port);
using var kugouApi=new KugouApiReader();
using var lyrics=new PublicLyricsClient();
using var qqLyrics=new QqPublicLyricsClient();
var smtc=new SmtcReader();
var jsonOptions=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
string key="",message="等待原版网易云音乐";
LyricsTimeline timeline=LyricsTimeline.Empty;
int generation=0,sentGeneration=-1;
long retryAt=0;
Task<(string Key,LyricsTimeline Timeline,string Message)>? lyricJob=null;
using var sampler=new PlaybackSampler(async ct=>
{
    // Kugou is read only through its native API. Never scan its controls or windows.
    if(source=="kugou")return await kugouApi.ReadAsync(ct);
    DesktopPlayback? playback=await smtc.ReadPlayerAsync(source,ct);
    if(source=="netease"&&playback?.PositionMs==null)
    {
        var native=await cdp.ReadAsync(ct);
        if(native!=null&&native.Title.Length>0&&(playback==null||SameTrack(playback,native)))playback=native;
    }
    return playback;
},cancel.Token);
try
{
    while(!parent.HasExited)
    {
        DesktopPlayback? playback=sampler.Snapshot;
        string next=playback==null?key:source+"/"+playback.Key;
        if(next!=key){key=next;timeline=LyricsTimeline.Empty;generation++;retryAt=0;message="正在匹配公开同步歌词";}
        if(lyricJob?.IsCompleted==true)
        {
            var result=await lyricJob;lyricJob=null;
            if(result.Key==key){timeline=result.Timeline;message=result.Message;generation++;retryAt=Environment.TickCount64+15000;}
        }
        if(playback?.PositionMs!=null && key.Length>0 && timeline.Lines.Count==0 && lyricJob==null && Environment.TickCount64>=retryAt)
            lyricJob=Task.Run(()=>Fetch(playback));
        string status=playback==null?(source=="kugou"?kugouApi.Status:"等待"+player+"曲目信息；请保持播放器正在播放，并启用系统媒体会话"):playback.PositionMs==null?
            player+"未提供有效播放进度；"+(source=="netease"?cdp.Status:"请启用系统媒体会话或显示完整播放界面"):message;
        var packet=new
        {
            connected=playback!=null,key,title=playback?.Title??"",artist=playback?.Artist??"",
            positionMs=playback?.PositionAt(Environment.TickCount64),durationMs=playback?.DurationMs??0,
            playing=playback?.Playing??false,generation,message=status,
            lines=sentGeneration==generation?null:timeline.Lines
        };
        Console.WriteLine(JsonSerializer.Serialize(packet,jsonOptions));Console.Out.Flush();sentGeneration=generation;
        await Task.Delay(200,cancel.Token);
    }
}
catch(Exception ex) when(ex is IOException or OperationCanceledException or InvalidOperationException){ }
finally{cancel.Cancel();}

async Task<(string,LyricsTimeline,string)> Fetch(DesktopPlayback track)
{
    try
    {
        if(source=="kugou")
        {
            var local=KugouLyricsCache.Read(track,KugouLyricsCache.Folders(args.Length==4?args[3]:null));
            if(local.Lines.Count>0)return(source+"/"+track.Key,local,"已连接酷狗音乐 · 本机原版歌词");
        }
        if(source=="qqmusic")
        {
            try{var native=await qqLyrics.FindAsync(track,cancel.Token);if(native.Lines.Count>0)return(source+"/"+track.Key,native,"已连接QQ音乐 · QQ本平台同步歌词");}
            catch(Exception ex)when(ex is HttpRequestException or InvalidDataException or JsonException or OperationCanceledException){cancel.Token.ThrowIfCancellationRequested();}
        }
        long id=source=="netease"?track.SongId:0;
        if(id<=0)
        {
            var candidates=await lyrics.SearchAsync(track.Title+" "+track.Artist,cancel.Token);
            var match=SongMatcher.FindBest(track.Title,track.Artist,track.DurationMs,candidates);
            if(match==null)return(source+"/"+track.Key,LyricsTimeline.Empty,source=="kugou"?"未找到匹配的本机歌词，公开曲库也无确定版本；请检查酷狗歌词缓存目录":"公开曲库未找到确定的歌曲版本");
            id=match.Id;
        }
        var result=await lyrics.GetLyricsAsync(id,cancel.Token);
        return(source+"/"+track.Key,result,result.Lines.Count>0?"已连接"+player+" · 公开同步歌词":"已连接"+player+" · 此曲暂无同步歌词");
    }
    catch(OperationCanceledException){return(source+"/"+track.Key,LyricsTimeline.Empty,"网易云歌词读取已取消");}
    catch(Exception ex) when(ex is not OutOfMemoryException){return(source+"/"+track.Key,LyricsTimeline.Empty,"网易云歌词接口暂不可用，稍后自动重试");}
}
static bool SameTrack(DesktopPlayback a,DesktopPlayback b)=>string.Equals(a.Title.Trim(),b.Title.Trim(),StringComparison.OrdinalIgnoreCase) &&
    (a.Artist.Length==0 || b.Artist.Length==0 || string.Equals(a.Artist.Trim(),b.Artist.Trim(),StringComparison.OrdinalIgnoreCase));
