using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DangoMusic.Core;
using DangoMusic.Desktop;

// Windows APIs stay in this isolated helper; Unity Mono only receives JSON over its child stdout.
Console.OutputEncoding=new UTF8Encoding(false);
if(args.Length!=2 || !int.TryParse(args[0],out int parentId) || !int.TryParse(args[1],out int port) || port<1 || port>65535)return;
using var parent=Process.GetProcessById(parentId);
using var cancel=new CancellationTokenSource();
using var cdp=new CdpProgressReader(port);
using var lyrics=new PublicLyricsClient();
var smtc=new SmtcReader();
var jsonOptions=new JsonSerializerOptions{PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
string key="",message="等待原版网易云音乐";
LyricsTimeline timeline=LyricsTimeline.Empty;
int generation=0,sentGeneration=-1;
long retryAt=0;
Task<(string Key,LyricsTimeline Timeline,string Message)>? lyricJob=null;
try
{
    while(!parent.HasExited)
    {
        DesktopPlayback? playback=null;
        try{playback=await smtc.ReadAsync(cancel.Token);}catch(Exception ex) when(ex is not OutOfMemoryException){ }
        if(playback?.PositionMs==null)
        {
            var native=await cdp.ReadAsync(cancel.Token);
            if(native!=null && native.Title.Length>0 && (playback==null || SameTrack(playback,native)))playback=native;
        }
        string next=playback?.Key??"";
        if(next!=key){key=next;timeline=LyricsTimeline.Empty;generation++;retryAt=0;message="正在获取网易云歌词";}
        if(lyricJob?.IsCompleted==true)
        {
            var result=await lyricJob;lyricJob=null;
            if(result.Key==key){timeline=result.Timeline;message=result.Message;generation++;retryAt=Environment.TickCount64+15000;}
        }
        if(playback?.PositionMs!=null && key.Length>0 && timeline.Lines.Count==0 && lyricJob==null && Environment.TickCount64>=retryAt)
            lyricJob=Fetch(playback);
        string status=playback==null?"等待原版网易云音乐：请打开播放器并播放歌曲":playback.PositionMs==null?
            "网易云未提供播放进度；请退出播放器后用本包的网易云连接脚本启动":message;
        var packet=new
        {
            connected=playback!=null,key,title=playback?.Title??"",artist=playback?.Artist??"",
            positionMs=playback?.PositionAt(Environment.TickCount64),durationMs=playback?.DurationMs??0,
            playing=playback?.Playing??false,generation,message=status,
            lines=sentGeneration==generation?null:timeline.Lines
        };
        Console.WriteLine(JsonSerializer.Serialize(packet,jsonOptions));Console.Out.Flush();sentGeneration=generation;
        await Task.Delay(400,cancel.Token);
    }
}
catch(Exception ex) when(ex is IOException or OperationCanceledException or InvalidOperationException){ }
finally{cancel.Cancel();if(lyricJob!=null)try{await lyricJob;}catch(OperationCanceledException){ }}

async Task<(string,LyricsTimeline,string)> Fetch(DesktopPlayback track)
{
    try
    {
        long id=track.SongId;
        if(id<=0)
        {
            var candidates=await lyrics.SearchAsync(track.Title+" "+track.Artist,cancel.Token);
            var match=SongMatcher.FindBest(track.Title,track.Artist,track.DurationMs,candidates);
            if(match==null)return(track.Key,LyricsTimeline.Empty,"网易云歌曲匹配不唯一，暂不显示歌词");
            id=match.Id;
        }
        var result=await lyrics.GetLyricsAsync(id,cancel.Token);
        return(track.Key,result,result.Lines.Count>0?"已连接原版网易云 · 公开同步歌词":"已连接原版网易云 · 此曲暂无同步歌词");
    }
    catch(OperationCanceledException){return(track.Key,LyricsTimeline.Empty,"网易云歌词读取已取消");}
    catch(Exception ex) when(ex is not OutOfMemoryException){return(track.Key,LyricsTimeline.Empty,"网易云歌词接口暂不可用，稍后自动重试");}
}
static bool SameTrack(DesktopPlayback a,DesktopPlayback b)=>string.Equals(a.Title.Trim(),b.Title.Trim(),StringComparison.OrdinalIgnoreCase) &&
    (a.Artist.Length==0 || b.Artist.Length==0 || string.Equals(a.Artist.Trim(),b.Artist.Trim(),StringComparison.OrdinalIgnoreCase));
