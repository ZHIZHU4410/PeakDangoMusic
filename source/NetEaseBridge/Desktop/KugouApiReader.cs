using System.Globalization;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DangoMusic.Desktop;

// One persistent local connection, one status request at a time, at most once per 4.4s.
// No UI Automation, window enumeration, player controls, cookies or account data.
public sealed class KugouApiReader : IDisposable
{
    const int MaximumBytes=256*1024;
    readonly int port;
    public KugouApiReader():this(9225){}
    internal KugouApiReader(int port){this.port=port;}
    readonly HttpClient http=new(new SocketsHttpHandler{UseProxy=false,AllowAutoRedirect=false,UseCookies=false});
    readonly SemaphoreSlim gate=new(1,1);
    readonly CancellationTokenSource lifetime=new();
    ClientWebSocket? socket;
    DesktopPlayback? cached;
    long nextRead;
    int sequence;
    readonly Queue<string> earlyEvents=new();
    public string Status{get;private set;}="请用包内脚本启动酷狗，打开推荐页并播放";

    // Defer the native call until Runtime.evaluate has returned. Expired queued calls do
    // not run. Only opcode 864 (read status) is used; controls from the reference are omitted.
    internal const string InstallExpression="""
        (()=>{
          if(window.__peakKugouBridge)return true;
          let pending=false,nextAt=0;
          window.__peakKugouBridge={begin(id,expiresAt){
            if(pending||Date.now()<nextAt)throw Error('Native bridge busy');
            pending=true;
            setTimeout(()=>{
              const report=value=>{try{window.__peakKugouResult(JSON.stringify({id,...value}));}catch(_){}};
              if(Date.now()>=expiresAt){pending=false;report({error:'Expired'});return;}
              const callback='__peakKugouSnapshot_'+id;let timer,done=false;
              const finish=value=>{if(done)return;done=true;clearTimeout(timer);delete window[callback];
                pending=false;nextAt=Date.now()+(value.error?30000:0);report(value);};
              try{
                timer=setTimeout(()=>finish({error:'Native timeout'}),8000);
                window[callback]=value=>{try{
                  const s=typeof value==='string'?JSON.parse(value):value;
                  finish({result:{available:String(s.status)==='1',title:s.filename||'',
                    playback:s.play_status||'stopped',duration_ms:Number(s.duration||0),progress:s.progress}});
                }catch(_){finish({error:'Invalid snapshot'});}};
                external.SuperCall(864,JSON.stringify({callback}));
              }catch(_){finish({error:'Native unavailable'});}
            },0);
            return {accepted:true};
          }};return true;
        })()
        """;

    public async Task<DesktopPlayback?> ReadAsync(CancellationToken token)
    {
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if(Environment.TickCount64<nextRead)return cached;
            // Failure also observes a cooldown; a wedged native page cannot create retries.
            nextRead=Environment.TickCount64+30000;
            using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);
            deadline.CancelAfter(10000);var ct=deadline.Token;
            if(socket?.State!=WebSocketState.Open)await ConnectAsync(ct).ConfigureAwait(false);
            string request=Guid.NewGuid().ToString("N");
            await CommandAsync("Runtime.evaluate",new{expression="window.__peakKugouBridge.begin('"+request+"',Date.now()+8000)",awaitPromise=false,returnByValue=true},ct).ConfigureAwait(false);
            while(true)
            {
                using var message=earlyEvents.Count>0?JsonDocument.Parse(earlyEvents.Dequeue()):await ReceiveAsync(ct).ConfigureAwait(false);
                var root=message.RootElement;
                if(!root.TryGetProperty("method",out var method)||method.GetString()!="Runtime.bindingCalled")continue;
                var args=root.GetProperty("params");
                if(args.GetProperty("name").GetString()!="__peakKugouResult")continue;
                string payload=args.GetProperty("payload").GetString()??"";
                if(payload.Length>MaximumBytes)throw new InvalidDataException();
                using var result=JsonDocument.Parse(payload,new JsonDocumentOptions{MaxDepth=16});
                if(!result.RootElement.TryGetProperty("id",out var id)||id.GetString()!=request)continue;
                if(result.RootElement.TryGetProperty("error",out _))throw new InvalidDataException("Native status unavailable");
                cached=ParseSnapshot(result.RootElement.GetProperty("result"),Environment.TickCount64);
                Status=cached==null?"酷狗接口暂无有效播放信息":"已连接酷狗本机播放接口";
                nextRead=Environment.TickCount64+4400;
                return cached;
            }
        }
        catch(Exception ex)when(ex is HttpRequestException or IOException or InvalidDataException or WebSocketException or JsonException or InvalidOperationException or OperationCanceledException or KeyNotFoundException or FormatException)
        {
            token.ThrowIfCancellationRequested();lifetime.Token.ThrowIfCancellationRequested();
            socket?.Dispose();socket=null;cached=null;
            nextRead=Environment.TickCount64+30000;
            Status="酷狗接口未就绪；请用包内脚本启动并打开推荐页";
            return null;
        }
        finally{gate.Release();}
    }

    async Task ConnectAsync(CancellationToken ct)
    {
        socket?.Dispose();socket=null;earlyEvents.Clear();
        using var response=await http.GetAsync($"http://127.0.0.1:{port}/json/list",HttpCompletionOption.ResponseHeadersRead,ct).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        using var stream=await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        using var targets=await ReadJsonAsync(stream,ct).ConfigureAwait(false);
        Uri? address=SelectTarget(targets.RootElement,port);
        if(address==null)throw new InvalidDataException("Open Kugou recommendation page");
        socket=new ClientWebSocket();socket.Options.Proxy=null;
        await socket.ConnectAsync(address,ct).ConfigureAwait(false);
        await CommandAsync("Runtime.addBinding",new{name="__peakKugouResult"},ct).ConfigureAwait(false);
        await CommandAsync("Runtime.evaluate",new{expression=InstallExpression,awaitPromise=false,returnByValue=true},ct).ConfigureAwait(false);
    }

    internal static Uri? SelectTarget(JsonElement targets,int port=9225)
    {
        if(targets.ValueKind!=JsonValueKind.Array)return null;
        foreach(var target in targets.EnumerateArray().Take(128))
        {
            string Get(string name)=>target.TryGetProperty(name,out var p)&&p.ValueKind==JsonValueKind.String?p.GetString()??"":"";
            if(Get("type")!="page"||!Uri.TryCreate(Get("url"),UriKind.Absolute,out var page)||
                page.UserInfo.Length!=0||page.Host!="pc.service.kugou.com"||page.Scheme is not ("https" or "http")||
                !(page.AbsolutePath.StartsWith("/yueku/",StringComparison.Ordinal)||page.AbsolutePath.StartsWith("/search/2024/",StringComparison.Ordinal)))continue;
            if(Uri.TryCreate(Get("webSocketDebuggerUrl"),UriKind.Absolute,out var ws)&&ws.Scheme=="ws"&&
                ws.Host=="127.0.0.1"&&ws.Port==port&&ws.UserInfo.Length==0&&ws.Query.Length==0&&ws.Fragment.Length==0&&
                ws.AbsolutePath.StartsWith("/devtools/page/",StringComparison.Ordinal))return ws;
        }
        return null;
    }

    internal static DesktopPlayback? ParseSnapshot(JsonElement result,long tick)
    {
        string Text(string key)=>result.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString()??"":"";
        double Number(string key)=>result.TryGetProperty(key,out var v)&&double.TryParse(v.ToString(),NumberStyles.Float,CultureInfo.InvariantCulture,out var n)?n:double.NaN;
        if(!result.TryGetProperty("available",out var available)||available.ValueKind!=JsonValueKind.True)return null;
        string filename=Text("title").Trim(),status=Text("playback");
        double duration=Number("duration_ms"),position=Number("progress")/10000d;
        if(filename.Length is 0 or >1024||status is not ("playing" or "paused")||
            !double.IsFinite(duration)||duration<=0||duration>86400000||!double.IsFinite(position)||position<0||position>duration+1000)return null;
        int separator=filename.IndexOf(" - ",StringComparison.Ordinal);
        string artist=separator>0?filename[..separator].Trim():"";
        string title=separator>0?filename[(separator+3)..].Trim():filename;
        if(title.Length==0)return null;
        return new(title,artist,0,(long)Math.Round(Math.Min(position,duration)),(long)Math.Round(duration),status=="playing",tick,"酷狗本机API");
    }

    async Task CommandAsync(string method,object args,CancellationToken ct)
    {
        int id=++sequence;
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new{id,method,@params=args});
        await socket!.SendAsync(new ArraySegment<byte>(bytes),WebSocketMessageType.Text,true,ct).ConfigureAwait(false);
        while(true)
        {
            using var message=await ReceiveAsync(ct).ConfigureAwait(false);
            if(!message.RootElement.TryGetProperty("id",out var value)||value.GetInt32()!=id)
            {
                if(message.RootElement.TryGetProperty("method",out var methodName)&&methodName.GetString()=="Runtime.bindingCalled")
                {
                    if(earlyEvents.Count>=8)throw new InvalidDataException("Too many pending callbacks");
                    earlyEvents.Enqueue(message.RootElement.GetRawText());
                }
                continue;
            }
            if(message.RootElement.TryGetProperty("error",out _)||
                message.RootElement.TryGetProperty("result",out var result)&&result.TryGetProperty("exceptionDetails",out _))throw new InvalidDataException("CDP command failed");
            return;
        }
    }
    async Task<JsonDocument> ReceiveAsync(CancellationToken ct)
    {
        using var data=new MemoryStream();byte[] buffer=new byte[8192];
        WebSocketReceiveResult frame;
        do
        {
            frame=await socket!.ReceiveAsync(new ArraySegment<byte>(buffer),ct).ConfigureAwait(false);
            if(frame.MessageType!=WebSocketMessageType.Text||data.Length+frame.Count>MaximumBytes)throw new InvalidDataException("Invalid CDP frame");
            data.Write(buffer,0,frame.Count);
        }while(!frame.EndOfMessage);
        return JsonDocument.Parse(data.ToArray(),new JsonDocumentOptions{MaxDepth=32});
    }
    static async Task<JsonDocument> ReadJsonAsync(Stream input,CancellationToken ct)
    {
        using var data=new MemoryStream();byte[] buffer=new byte[8192];int size;
        while((size=await input.ReadAsync(buffer,ct).ConfigureAwait(false))>0)
        {
            if(data.Length+size>MaximumBytes)throw new InvalidDataException("Oversized target list");
            data.Write(buffer,0,size);
        }
        return JsonDocument.Parse(data.ToArray(),new JsonDocumentOptions{MaxDepth=32});
    }
    public void Dispose(){lifetime.Cancel();socket?.Dispose();http.Dispose();}
}
