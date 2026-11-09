using System.Diagnostics;
using System.Text;
using DangoMusic.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

/// <summary>Optional Windows helper, started only while the native NetEase source is selected.</summary>
internal sealed class NativeMusicClient : IDisposable
{
    readonly CancellationTokenSource lifetime=new();
    readonly Task worker;
    readonly object gate=new();
    Process? child;
    volatile MusicState state=new(message:"等待原版网易云音乐");
    int enabled,port=9223,epoch,source=1;
    string lyricsFolder="";
    string Player=>Volatile.Read(ref source) switch{2=>"酷狗音乐",3=>"QQ音乐",_=>"原版网易云音乐"};
    internal MusicState Snapshot=>state;
    internal NativeMusicClient(){worker=Task.Run(Run);}
    internal void Configure(int nextPort,bool active,int nextSource=1,string nextFolder="")
    {
        bool folderChanged=lyricsFolder!=nextFolder;lyricsFolder=nextFolder;
        bool changed=folderChanged;changed|=Interlocked.Exchange(ref source,nextSource)!=nextSource;
        changed|=Interlocked.Exchange(ref port,nextPort)!=nextPort;
        changed|=Interlocked.Exchange(ref enabled,active?1:0)!=(active?1:0);
        if(changed)Reconnect();
    }
    internal void Reconnect()
    {
        Interlocked.Increment(ref epoch);state=new MusicState(message:"正在连接"+Player);StopChild();
    }
    void StopChild()
    {
        lock(gate){if(child!=null)try{if(!child.HasExited)child.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){} }
    }
    async Task Run()
    {
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                if(Volatile.Read(ref enabled)==0){await Task.Delay(400,lifetime.Token).ConfigureAwait(false);continue;}
                int currentEpoch=Volatile.Read(ref epoch);
                string path=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"PeakDangoMusic","Native","PeakNeteaseBridge.exe");
                if(!File.Exists(path)){state=new MusicState(message:"缺少播放器连接组件，请完整安装2.0安装包");await Task.Delay(2500,lifetime.Token).ConfigureAwait(false);continue;}
                Process? process=null;
                try
                {
                    process=new Process{StartInfo=new ProcessStartInfo(path,Process.GetCurrentProcess().Id+" "+Volatile.Read(ref port)+" "+(Volatile.Read(ref source) switch{2=>"kugou",3=>"qqmusic",_=>"netease"})+" \""+lyricsFolder.Replace("\"","").TrimEnd('\\')+"\"")
                    {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,StandardOutputEncoding=new UTF8Encoding(false),WorkingDirectory=Path.GetDirectoryName(path)!}};
                    lock(gate)
                    {
                        if(currentEpoch!=Volatile.Read(ref epoch) || Volatile.Read(ref enabled)==0 || lifetime.IsCancellationRequested)continue;
                        process.Start();child=process;
                    }
                    LyricsTimeline timeline=LyricsTimeline.Empty;string timelineKey="";
                    while(!lifetime.IsCancellationRequested && currentEpoch==Volatile.Read(ref epoch) && Volatile.Read(ref enabled)!=0)
                    {
                        string? line=await ReadLineBounded(process.StandardOutput,lifetime.Token).ConfigureAwait(false);if(line==null)break;
                        using var reader=new JsonTextReader(new StringReader(line)){MaxDepth=12,DateParseHandling=DateParseHandling.None};
                        var data=JObject.Load(reader);string key=Text(data,"key",1600);
                        if(key!=timelineKey){timelineKey=key;timeline=LyricsTimeline.Empty;}
                        if(data["lines"] is JArray lines)
                        {
                            if(lines.Count>12000)throw new InvalidDataException();
                            var parsed=new List<LyricsLine>();
                            foreach(var token in lines)
                            {
                                var words=(token["words"] as JArray)?.Take(1000).Select(w=>new LyricsWord((long?)w["startMs"]??0,(long?)w["durationMs"]??0,Text(w,"text",4096))).ToArray();
                                parsed.Add(new LyricsLine((long?)token["startMs"]??0,(long?)token["durationMs"]??0,Text(token,"text",8192),words,Text(token,"translation",8192)));
                            }
                            timeline=LyricsTimeline.FromLines(parsed);
                        }
                        long duration=(long?)data["durationMs"]??0;long? position=(long?)data["positionMs"];
                        bool valid=position>=0 && duration>0 && duration<=86400000 && position<=duration;
                        if(currentEpoch!=Volatile.Read(ref epoch))break;
                        state=new MusicState((bool?)data["connected"]??false,valid?key:"",Text(data,"title",512),Text(data,"artist",512),position??0,duration,(bool?)data["playing"]??false,MusicClock.Now,1,timeline,(int?)data["generation"]??0,Text(data,"message",1024));
                    }
                }
                catch(Exception ex) when(ex is TimeoutException || ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is ArgumentException || ex is OverflowException)
                {if(currentEpoch==Volatile.Read(ref epoch))state=new MusicState(message:Player+"连接组件暂不可用，正在重试；可查看安装说明");}
                finally
                {
                    lock(gate){if(child==process){StopChild();child=null;}}
                    process?.Dispose();
                }
                if(!lifetime.IsCancellationRequested)await Task.Delay(1800,lifetime.Token).ConfigureAwait(false);
            }
        }
        catch(OperationCanceledException){}
    }
    static async Task<string?> ReadLineBounded(StreamReader reader,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var read=reader.ReadLineAsync();
        if(await Task.WhenAny(read,Task.Delay(5000,token)).ConfigureAwait(false)!=read){token.ThrowIfCancellationRequested();throw new TimeoutException("播放器组件心跳超时");}
        string? text=await read.ConfigureAwait(false);
        if(text?.Length>4*1024*1024)throw new InvalidDataException();
        return text;
    }
    static string Text(JToken token,string key,int max)
    {string value=token[key]?.Type==JTokenType.String?(string)token[key]!:"";return value.Length<=max?value:"";}
    public void Dispose(){lifetime.Cancel();StopChild();}
}
