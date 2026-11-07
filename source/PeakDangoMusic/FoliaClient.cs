using System.Diagnostics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.IO.Pipes;
using DangoMusic.Core;
using Newtonsoft.Json;

namespace PeakDangoMusic;

/// <summary>Bounded, asynchronous stdin/bitmap protocol; never waits on Chromium on Unity's thread.</summary>
internal sealed class FoliaClient : IDisposable
{
    internal sealed class Request
    {
        internal readonly MusicState State;
        internal readonly string Mode,Primary,Accent,Intensity,FontPath;
        internal readonly bool Enabled,Translation,Split,Background,Software;
        internal readonly int Offset,Quality,Fps;
        internal readonly float FontScale;
        internal int Revision;
        internal Request(MusicState state,MusicSettings settings,AdaptiveLyrics.Palette palette)
        {
            FontPath=settings.FontPath.Value;State=state;Mode=FoliaStyles.Resolve(settings.Style.Value,state.Key);Enabled=settings.Enabled.Value;
            Translation=settings.Translation.Value;Split=settings.SplitLines.Value;Background=settings.FoliaBackground.Value;Software=settings.FoliaSoftware.Value;
            Offset=settings.Offset.Value;Quality=settings.FoliaQuality.Value;Fps=settings.FoliaFps.Value;FontScale=settings.FoliaFontScale.Value;
            Intensity=settings.FoliaIntensity.Value switch{0=>"calm",2=>"chaotic",_=>"normal"};
            Primary=Background?"#F7E1D7":"#"+UnityEngine.ColorUtility.ToHtmlStringRGB(palette.Face);Accent=settings.GlowColor.Value switch{1=>"#FF82B5",2=>"#76BBFF",3=>"#F4C676",_=>"#69D7D1"};
            if(!Background&&palette.IsDark)Accent="#234D59";
        }
    }
    internal sealed class Frame
    {
        internal readonly int Width,Height;
        internal readonly byte[] Pixels;
        internal Frame(int width,int height,byte[] pixels){Width=width;Height=height;Pixels=pixels;}
    }
    readonly CancellationTokenSource lifetime=new();
    readonly object gate=new();
    readonly ConcurrentQueue<byte[]> buffers=new();
    readonly Task worker;
    volatile Request? latest;
    Frame? frame;
    volatile string status="Folia 样式尚未启用";
    volatile bool running;
    volatile bool ready;
    int epoch;
    int requestedRevision;
    volatile bool failed;
    long lastFrameTick;
    string lastDiagnostic="";
    Process? child;
    internal string Status=>status;
    internal bool Running=>running;
    internal bool Ready=>running&&ready&&!failed&&MusicClock.Now-Interlocked.Read(ref lastFrameTick)<5000;
    internal Frame? TakeFrame()=>Interlocked.Exchange(ref frame,null);
    internal FoliaClient(){worker=Task.Run(Run);}
    internal void Set(Request? request)
    {
        var previous=latest;
        if(request!=null)
        {
            if(previous==null||previous.Mode!=request.Mode||previous.FontPath!=request.FontPath||previous.State.Key!=request.State.Key||previous.State.LyricGeneration!=request.State.LyricGeneration||previous.Split!=request.Split)
            {Interlocked.Increment(ref requestedRevision);ready=false;failed=false;status="正在准备所选Folia样式…";}
            request.Revision=Volatile.Read(ref requestedRevision);
        }
        latest=request;
    }
    internal void Restart(){Interlocked.Increment(ref epoch);ready=false;failed=false;status="正在重新准备Folia样式…";}
    internal void ReturnFrame(Frame value){if(buffers.Count<4)buffers.Enqueue(value.Pixels);}
    byte[] Rent(int length){while(buffers.TryDequeue(out var value)){if(value.Length==length)return value;}return new byte[length];}
    void StopChild()
    {
        lock(gate)
        {
            if(child==null)return;
            try{if(!child.HasExited)child.Kill();}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
        }
        running=false;
    }
    async Task Run()
    {
        try
        {
            while(!lifetime.IsCancellationRequested)
            {
                var first=latest;if(first==null){await Task.Delay(300,lifetime.Token).ConfigureAwait(false);continue;}
                string folder=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"PeakDangoMusic","Folia");
                string executable=Path.Combine(folder,"Runtime","PeakFoliaRenderer.exe"),app=Path.Combine(folder,"App");
                if(!File.Exists(executable)||!File.Exists(Path.Combine(app,"dist","index.html")))
                {status="缺少Folia样式组件，请安装完整的1.2.2或更新安装包；暂用Dango显示";await Task.Delay(2500,lifetime.Token).ConfigureAwait(false);continue;}
                Process? process=null;
                try
                {
                    if(Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(app,"package.json"))).Value<int?>("peakProtocol")!=3)
                    {status="Folia组件与DLL版本不匹配，请一起更新完整1.2.2安装包；暂用Dango显示";await Task.Delay(2500,lifetime.Token).ConfigureAwait(false);continue;}
                    string pipeName="PeakFolia-"+Guid.NewGuid().ToString("N");
                    using var pipe=new NamedPipeServerStream(pipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous);
                    string profile=Path.Combine(BepInEx.Paths.CachePath,"PeakDangoMusicFolia",Process.GetCurrentProcess().Id.ToString());Directory.CreateDirectory(profile);
                    process=new Process{StartInfo=new ProcessStartInfo(executable,"\""+app+"\" --parent="+Process.GetCurrentProcess().Id+" --profile=\""+profile+"\" --pipe="+pipeName+" --software="+(first.Software?"1":"0"))
                    {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardErrorEncoding=new UTF8Encoding(false),WorkingDirectory=folder}};
                    int sessionEpoch=Volatile.Read(ref epoch);
                    // A game launched from an Electron host must still start this as Electron, not plain Node.
                    process.StartInfo.EnvironmentVariables.Remove("ELECTRON_RUN_AS_NODE");
                    lock(gate){if(latest==null)continue;status="正在准备Folia样式…";ready=false;failed=false;lastDiagnostic="";process.Start();child=process;running=true;}
                    using var session=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    var diagnostics=Diagnostics(process.StandardError,session.Token);
                    _ = process.StandardOutput.ReadToEndAsync(); // GUI-console startup noise is never bitmap data.
                    using(var connection=CancellationTokenSource.CreateLinkedTokenSource(session.Token))
                    {
                        connection.CancelAfter(10000);
                        try{await pipe.WaitForConnectionAsync(connection.Token).ConfigureAwait(false);}
                        catch(OperationCanceledException) when(!lifetime.IsCancellationRequested){throw new IOException("Folia专用管道连接超时");}
                    }
                    using var writer=new StreamWriter(pipe,new UTF8Encoding(false),8192,true);
                    var send=Send(writer,first.Software,sessionEpoch,session.Token);
                    var watchdog=Watchdog(process,session.Token);
                    try{await Receive(pipe,session.Token).ConfigureAwait(false);}
                    finally{session.Cancel();StopChild();try{await send.ConfigureAwait(false);}catch(OperationCanceledException){}catch(IOException){}catch(InvalidOperationException){}try{await diagnostics.ConfigureAwait(false);}catch(OperationCanceledException){}try{await watchdog.ConfigureAwait(false);}catch(OperationCanceledException){}}
                }
                catch(Exception ex) when(ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException or NotSupportedException or JsonException)
                {status="Folia组件暂不可用，正在重试；当前使用Dango显示："+(lastDiagnostic.Length>0?lastDiagnostic:ex.Message);}
                finally{running=false;ready=false;lock(gate){if(child==process){StopChild();child=null;}}process?.Dispose();var old=Interlocked.Exchange(ref frame,null);if(old!=null)ReturnFrame(old);}
                if(!lifetime.IsCancellationRequested)await Task.Delay(1800,lifetime.Token).ConfigureAwait(false);
            }
        }
        catch(OperationCanceledException){}
    }
    async Task Send(StreamWriter writer,bool software,int sessionEpoch,CancellationToken token)
    {
        string previousKey="";int previousGeneration=-1;bool previousSplit=false;
        LyricsTimeline? previousTimeline=null;var view=new LyricLineView();
        var json=new JsonSerializerSettings{NullValueHandling=NullValueHandling.Ignore};
        try
        {
            while(!token.IsCancellationRequested)
            {
                var request=latest;if(request==null||request.Software!=software||sessionEpoch!=Volatile.Read(ref epoch))return;
                var state=request.State;long? position=state.PositionAt(MusicClock.Now);
                bool changed=previousKey!=state.Key||previousGeneration!=state.LyricGeneration||previousSplit!=request.Split||!ReferenceEquals(previousTimeline,state.Timeline);
                object[]? lines=changed?ToLines(view.Get(state.Timeline,request.Split),state.DurationMs):null;
                var packet=new{fontPath=request.FontPath,revision=request.Revision,mode=request.Mode,key=state.Key,title=state.Title,artist=state.Artist,
                    positionMs=Math.Clamp((position??state.PositionMs)+request.Offset,0,Math.Max(0,state.DurationMs)),durationMs=state.DurationMs,
                    playing=position!=null&&state.Playing,visible=request.Enabled&&position!=null&&state.Timeline.Lines.Count>0,
                    generation=state.LyricGeneration,lines,translation=request.Translation,fontScale=request.FontScale,
                    intensity=request.Intensity,primary=request.Primary,accent=request.Accent,background=request.Background,quality=request.Quality,fps=request.Fps};
                await writer.WriteLineAsync(JsonConvert.SerializeObject(packet,json)).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
                previousKey=state.Key;previousGeneration=state.LyricGeneration;previousSplit=request.Split;previousTimeline=state.Timeline;
                await Task.Delay(100,token).ConfigureAwait(false);
            }
        }
        catch(Exception ex) when(ex is IOException or InvalidOperationException or JsonException or ArgumentException)
        {lastDiagnostic="歌词管道发送失败："+ex.Message;failed=true;ready=false;throw;}
        finally{StopChild();}
    }
    static object[] ToLines(LyricsTimeline timeline,long duration)
    {
        return timeline.Lines.Take(12000).Select((line,index)=>
        {
            long end=index+1<timeline.Lines.Count?timeline.Lines[index+1].StartMs:Math.Max(line.StartMs+Math.Max(5000,line.DurationMs),duration);
            end=Math.Max(line.StartMs+1,end);
            var words=line.Words is {Count:>0}?line.Words:EstimateWords(line,end);
            return (object)new{fullText=line.Text,startTime=line.StartMs/1000d,endTime=end/1000d,translation=line.Translation,
                words=words.Select(word=>new{text=word.Text,startTime=word.StartMs/1000d,endTime=(word.StartMs+Math.Max(1,word.DurationMs))/1000d}).ToArray()};
        }).ToArray();
    }
    static IReadOnlyList<LyricsWord> EstimateWords(LyricsLine line,long end)
    {
        var parts=new List<string>();var text=new StringBuilder();
        var graphemes=StringInfo.GetTextElementEnumerator(line.Text);
        while(graphemes.MoveNext())
        {
            string value=(string)graphemes.Current;
            if(char.IsWhiteSpace(value,0)){text.Append(value);if(text.Length>0){parts.Add(text.ToString());text.Clear();}continue;}
            if(value[0]>=0x2e80){if(text.Length>0){parts.Add(text.ToString());text.Clear();}parts.Add(value);}
            else text.Append(value);
        }
        if(text.Length>0)parts.Add(text.ToString());
        int count=Math.Max(1,parts.Sum(part=>Math.Max(1,new StringInfo(part).LengthInTextElements))),before=0;
        var words=new List<LyricsWord>();long duration=Math.Max(1,end-line.StartMs);
        foreach(string part in parts){int length=Math.Max(1,new StringInfo(part).LengthInTextElements);long start=line.StartMs+duration*before/count;before+=length;words.Add(new LyricsWord(start,Math.Max(1,line.StartMs+duration*before/count-start),part));}
        return words;
    }
    async Task Receive(Stream stream,CancellationToken token)
    {
        var magic=Encoding.ASCII.GetBytes("DANGOF02");var header=new byte[24];byte[] row=Array.Empty<byte>();
        while(!token.IsCancellationRequested)
        {
            await ReadExactly(stream,header,0,8,token).ConfigureAwait(false);
            int skipped=0;var one=new byte[1];
            while(!header.Take(8).SequenceEqual(magic))
            {
                if(++skipped>65536)throw new InvalidDataException("Folia帧头无效");
                await ReadExactly(stream,one,token).ConfigureAwait(false);Buffer.BlockCopy(header,1,header,0,7);header[7]=one[0];
            }
            await ReadExactly(stream,header,8,16,token).ConfigureAwait(false);
            int width=BitConverter.ToInt32(header,8),height=BitConverter.ToInt32(header,12),length=BitConverter.ToInt32(header,16);
            int revision=BitConverter.ToInt32(header,20);
            if(width<1||width>1920||height<1||height>1080||length!=width*height*4)throw new InvalidDataException("Folia帧尺寸无效");
            var pixels=Rent(length);bool published=false;
            try
            {
                await ReadExactly(stream,pixels,token).ConfigureAwait(false);int stride=width*4;if(row.Length!=stride)row=new byte[stride];
                // Reuse bounded buffers; avoid allocating multi-megabyte arrays on every frame.
                for(int y=0;y<height/2;y++){int top=y*stride,bottom=(height-1-y)*stride;Buffer.BlockCopy(pixels,top,row,0,stride);Buffer.BlockCopy(pixels,bottom,pixels,top,stride);Buffer.BlockCopy(row,0,pixels,bottom,stride);}
                bool nonEmpty=false;
                for(int i=0;i<length;i+=4){int alpha=pixels[i+3];nonEmpty|=alpha>0;if(alpha is 0 or 255)continue;pixels[i]=(byte)Math.Min(255,(pixels[i]*255+alpha/2)/alpha);pixels[i+1]=(byte)Math.Min(255,(pixels[i+1]*255+alpha/2)/alpha);pixels[i+2]=(byte)Math.Min(255,(pixels[i+2]*255+alpha/2)/alpha);}
                Interlocked.Exchange(ref lastFrameTick,MusicClock.Now);
                if(revision!=Volatile.Read(ref requestedRevision)||failed)continue;
                var old=Interlocked.Exchange(ref frame,new Frame(width,height,pixels));if(old!=null)ReturnFrame(old);published=true;ready|=nonEmpty;if(ready)status="Folia原生样式已就绪";
            }
            finally{if(!published&&buffers.Count<4)buffers.Enqueue(pixels);}
        }
    }
    static async Task ReadExactly(Stream stream,byte[] data,CancellationToken token)
    {int offset=0;while(offset<data.Length){int read=await stream.ReadAsync(data,offset,data.Length-offset,token).ConfigureAwait(false);if(read==0)throw new EndOfStreamException();offset+=read;}}
    static async Task ReadExactly(Stream stream,byte[] data,int offset,int length,CancellationToken token)
    {int end=offset+length;while(offset<end){int read=await stream.ReadAsync(data,offset,end-offset,token).ConfigureAwait(false);if(read==0)throw new EndOfStreamException();offset+=read;}}
    async Task Diagnostics(StreamReader reader,CancellationToken token)
    {
        try{while(!token.IsCancellationRequested){string? line=await reader.ReadLineAsync().ConfigureAwait(false);if(line==null)return;
            if(!line.StartsWith("PEAK_FOLIA_ERROR ",StringComparison.Ordinal))continue;
            lastDiagnostic=line.Substring(17,Math.Min(500,line.Length-17));failed=true;ready=false;
            status="Folia失败，暂用Dango："+lastDiagnostic;
        }}
        catch(IOException){}catch(ObjectDisposedException){}
    }
    async Task Watchdog(Process process,CancellationToken token)
    {
        long start=MusicClock.Now;
        while(!token.IsCancellationRequested)
        {
            await Task.Delay(1000,token).ConfigureAwait(false);
            if(latest==null)return;
            if(MusicClock.Now-start>30000&&!ready)
            {if(lastDiagnostic.Length==0)lastDiagnostic="样式启动超时；详细原因见 BepInEx/PeakDangoMusic-Folia.log";StopChild();return;}
        }
    }
    public void Dispose(){latest=null;lifetime.Cancel();_ = Task.Run(StopChild);}
}
