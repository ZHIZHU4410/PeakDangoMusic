using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using DangoMusic.Core;
using DangoMusic.Desktop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal static class MusicClock
{
    internal static long Now => (long)(Stopwatch.GetTimestamp() * (1000d / Stopwatch.Frequency));
}

internal sealed class MusicState
{
    internal readonly bool Connected,Playing;
    internal readonly string Key,Title,Artist,Message;
    internal readonly long PositionMs,DurationMs,SampleTick;
    internal readonly double Rate;
    internal readonly LyricsTimeline Timeline;
    internal readonly int LyricGeneration;
    internal MusicState(bool connected=false,string key="",string title="",string artist="",long position=0,long duration=0,bool playing=false,long tick=0,double rate=1,LyricsTimeline? timeline=null,int generation=0,string message="等待连接 BLF / LX")
    {Connected=connected;Key=key;Title=title;Artist=artist;PositionMs=position;DurationMs=duration;Playing=playing;SampleTick=tick;Rate=rate;Timeline=timeline??LyricsTimeline.Empty;LyricGeneration=generation;Message=message;}
    internal long? PositionAt(long now)
    {
        long age=now-SampleTick;if(!Connected || Key.Length==0 || age<0 || age>3000)return null;
        return Math.Clamp(PositionMs+(Playing?(long)(Math.Min(age,1500)*Rate):0),0,DurationMs);
    }
}

/// <summary>Read-only loopback API. All HTTP, JSON and lyric parsing stays off Unity's thread.</summary>
internal sealed class LxClient : IDisposable
{
    readonly CancellationTokenSource lifetime=new();
    readonly HttpClient http;
    readonly Task worker;
    volatile MusicState state=new();
    int port=23330,enabled=1,epoch;
    internal MusicState Snapshot=>state;
    internal LxClient(int initialPort,bool active)
    {
        port=Math.Clamp(initialPort,1,65535);enabled=active?1:0;
        var handler=new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false,UseCookies=false,UseDefaultCredentials=false,Credentials=null,AutomaticDecompression=DecompressionMethods.None};
        http=new HttpClient(handler){Timeout=Timeout.InfiniteTimeSpan};
        worker=Task.Run(RunAsync);
    }
    internal void Configure(int value,bool active)
    {
        value=Math.Clamp(value,1,65535);
        bool changed=Interlocked.Exchange(ref port,value)!=value;
        int next=active?1:0;changed|=Interlocked.Exchange(ref enabled,next)!=next;
        if(changed){state=new MusicState(message:active?"正在连接 BLF / LX":"漂浮歌词已关闭");Interlocked.Increment(ref epoch);}
    }
    internal void Reconnect(){state=new MusicState(message:"正在重新连接 BLF / LX");Interlocked.Increment(ref epoch);}
    sealed class LyricResult
    {
        internal readonly string Key;internal readonly int Epoch;internal readonly LyricsTimeline Timeline;
        internal LyricResult(string key,int epoch,LyricsTimeline timeline){Key=key;Epoch=epoch;Timeline=timeline;}
    }
    async Task RunAsync()
    {
        string key="";int lastEpoch=-1,generation=0,attempts=0;long retryAt=0,lastSuccess=0;
        LyricsTimeline timeline=LyricsTimeline.Empty;Task<LyricResult>? job=null;
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                int currentEpoch=Volatile.Read(ref epoch),currentPort=Volatile.Read(ref port);
                if(currentEpoch!=lastEpoch){lastEpoch=currentEpoch;key="";timeline=LyricsTimeline.Empty;retryAt=0;attempts=0;generation++;}
                if(Volatile.Read(ref enabled)==0)
                {state=new MusicState(message:"漂浮歌词已关闭");await Task.Delay(600,lifetime.Token).ConfigureAwait(false);continue;}
                try
                {
                    var payload=await ReadJsonAsync(currentPort,"/status?filter=status,name,singer,duration,progress,playbackRate",64*1024,1500,lifetime.Token).ConfigureAwait(false);
                    if(currentEpoch!=Volatile.Read(ref epoch))continue;
                    long tick=MusicClock.Now;lastSuccess=tick;
                    string status=Text(payload,"status",32),title=Text(payload,"name",512),artist=Text(payload,"singer",512);
                    if((status!="playing" && status!="paused") || title.Length==0 || !Number(payload,"duration",out double duration) || !Number(payload,"progress",out double progress) || duration<=0 || duration>86400 || progress<0 || progress>duration+.1)
                    {
                        key="";timeline=LyricsTimeline.Empty;state=new MusicState(connected:true,message:"已连接，请在 BLF / LX 中播放歌曲");
                        await Task.Delay(600,lifetime.Token).ConfigureAwait(false);continue;
                    }
                    string nextKey=title+"\u001f"+artist+"\u001f"+Math.Round(duration*1000);
                    if(nextKey!=key){key=nextKey;timeline=LyricsTimeline.Empty;retryAt=tick+900;attempts=0;generation++;}
                    if(job!=null && job.IsCompleted)
                    {
                        var result=await job.ConfigureAwait(false);job=null;
                        if(result.Key==key && result.Epoch==currentEpoch)
                        {timeline=result.Timeline;generation++;attempts++;retryAt=tick+(attempts<4?2000:15000);}
                    }
                    if(timeline.Lines.Count==0 && job==null && tick>=retryAt)job=ReadLyricsAsync(currentPort,key,currentEpoch,lifetime.Token);
                    double rate=Number(payload,"playbackRate",out double speed)&&speed>0?Math.Clamp(speed,.1,8):1;
                    string message=timeline.Lines.Count>0?"已连接 · 使用播放器自带歌词":job!=null?"已连接 · 正在读取歌词":"已连接 · 当前曲目暂无同步歌词";
                    state=new MusicState(true,key,title,artist,(long)Math.Round(progress*1000),(long)Math.Round(duration*1000),status=="playing",tick,rate,timeline,generation,message);
                }
                catch(OperationCanceledException) when(!lifetime.IsCancellationRequested)
                {Disconnected(lastSuccess);}
                catch(Exception ex) when(ex is HttpRequestException || ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is ObjectDisposedException)
                {Disconnected(lastSuccess);}
                await Task.Delay(state.Connected?600:1500,lifetime.Token).ConfigureAwait(false);
            }
        }
        catch(OperationCanceledException) { }
        catch(ObjectDisposedException) { }
        finally
        {
            if(job!=null)try{await job.ConfigureAwait(false);}catch(OperationCanceledException) { }
        }
    }
    void Disconnected(long lastSuccess)
    {
        if(MusicClock.Now-lastSuccess>3000)state=new MusicState(message:"未连接：请开启 BLF / LX 设置中的本机开放 API");
    }
    async Task<LyricResult> ReadLyricsAsync(int currentPort,string key,int currentEpoch,CancellationToken token)
    {
        try
        {
            var data=await ReadJsonAsync(currentPort,"/lyric-all",2*1024*1024,6000,token).ConfigureAwait(false);
            string TextField(string name)
            {var v=data[name];if(v==null || v.Type==JTokenType.Null)return "";if(v.Type!=JTokenType.String)throw new InvalidOperationException();string text=(string)v!;if(text.Length>512*1024)throw new InvalidOperationException();return text;}
            return new LyricResult(key,currentEpoch,LxLyricParser.Parse(TextField("lyric"),TextField("lxlyric"),TextField("tlyric")));
        }
        catch(Exception ex) when(ex is HttpRequestException || ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is OperationCanceledException || ex is ObjectDisposedException || ex is FormatException || ex is OverflowException)
        {return new LyricResult(key,currentEpoch,LyricsTimeline.Empty);}
    }
    async Task<JObject> ReadJsonAsync(int currentPort,string path,int limit,int timeoutMs,CancellationToken token)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(timeoutMs);
        using var request=new HttpRequestMessage(HttpMethod.Get,"http://127.0.0.1:"+currentPort+path);
        request.Headers.ConnectionClose=true;request.Headers.Accept.ParseAdd("application/json");
        using var response=await http.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,timeout.Token).ConfigureAwait(false);
        if(response.StatusCode!=HttpStatusCode.OK || response.Content.Headers.ContentLength>limit)throw new InvalidOperationException("API response rejected");
        using var stream=await response.Content.ReadAsStreamAsync().ConfigureAwait(false);using var bytes=new MemoryStream();var buffer=new byte[8192];int count;
        while((count=await stream.ReadAsync(buffer,0,buffer.Length,timeout.Token).ConfigureAwait(false))>0)
        {if(bytes.Length+count>limit)throw new IOException("API response too large");bytes.Write(buffer,0,count);}
        using var text=new StringReader(Encoding.UTF8.GetString(bytes.ToArray()));using var reader=new JsonTextReader(text){MaxDepth=8,DateParseHandling=DateParseHandling.None};
        return JObject.Load(reader);
    }
    static string Text(JObject value,string key,int max)
    {var token=value[key];if(token?.Type!=JTokenType.String)return "";string text=((string?)token??"").Trim();return text.Length>max || text.Any(char.IsControl)?"":text;}
    static bool Number(JObject value,string key,out double number)
    {
        number=0;var token=value[key];if(token==null || token.Type!=JTokenType.Integer && token.Type!=JTokenType.Float)return false;
        number=(double)token;return !double.IsNaN(number) && !double.IsInfinity(number);
    }
    public void Dispose(){lifetime.Cancel();http.Dispose();}
}
