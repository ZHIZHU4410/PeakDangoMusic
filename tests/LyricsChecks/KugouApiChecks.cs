using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DangoMusic.Desktop;

static class KugouApiChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        using var sample=JsonDocument.Parse("""{"available":true,"title":"歌手 - 歌曲 - 后缀","duration_ms":200000,"progress":"123450000","playback":"playing"}""");
        var track=KugouApiReader.ParseSnapshot(sample.RootElement,123);
        check(track is{Title:"歌曲 - 后缀",Artist:"歌手",PositionMs:12345,SampleTick:123,Playing:true},"Kugou native clock uses 100ns ticks and retains full title");
        foreach(string bad in new[]{"\"NaN\"","-1","999999999999"})
        {
            using var invalid=JsonDocument.Parse(sample.RootElement.GetRawText().Replace("\"123450000\"",bad));
            check(KugouApiReader.ParseSnapshot(invalid.RootElement,0)==null,"Reject invalid Kugou native clock");
        }
        foreach(string ws in new[]{"ws://evil.example:9225/devtools/page/1","ws://127.0.0.1:9226/devtools/page/1","ws://user@127.0.0.1:9225/devtools/page/1"})
        {
            using var list=JsonDocument.Parse(JsonSerializer.Serialize(new[]{new{type="page",url="https://pc.service.kugou.com/yueku/v8/",webSocketDebuggerUrl=ws}}));
            check(KugouApiReader.SelectTarget(list.RootElement)==null,"Reject nonlocal or credentialed Kugou debugger");
        }
        using(var list=JsonDocument.Parse("""[{"type":"page","url":"https://pc.service.kugou.com.evil.example/yueku/","webSocketDebuggerUrl":"ws://127.0.0.1:9225/devtools/page/1"}]"""))
            check(KugouApiReader.SelectTarget(list.RootElement)==null,"Reject lookalike Kugou page host");

        // A callback may arrive before Runtime.evaluate's response. Test the real socket
        // reader and cache against that ordering, without a player or native bridge.
        var reserve=new TcpListener(IPAddress.Loopback,0);reserve.Start();int port=((IPEndPoint)reserve.LocalEndpoint).Port;reserve.Stop();
        using var listener=new HttpListener();listener.Prefixes.Add($"http://127.0.0.1:{port}/");listener.Start();
        using var timeout=new CancellationTokenSource(10000);
        int calls=0;
        var server=Task.Run(async()=>
        {
            var discovery=await listener.GetContextAsync().WaitAsync(timeout.Token);
            byte[] body=JsonSerializer.SerializeToUtf8Bytes(new[]{new{type="page",url="https://pc.service.kugou.com/yueku/v8/",webSocketDebuggerUrl=$"ws://127.0.0.1:{port}/devtools/page/test"}});
            discovery.Response.ContentLength64=body.Length;await discovery.Response.OutputStream.WriteAsync(body,timeout.Token);discovery.Response.Close();
            var connection=await listener.GetContextAsync().WaitAsync(timeout.Token);
            using var ws=(await connection.AcceptWebSocketAsync(null)).WebSocket;
            async Task Send(object value)=>await ws.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(value)),WebSocketMessageType.Text,true,timeout.Token);
            byte[] buffer=new byte[16384];
            for(int i=0;i<3;i++)
            {
                var frame=await ws.ReceiveAsync(new ArraySegment<byte>(buffer),timeout.Token);
                using var command=JsonDocument.Parse(buffer.AsMemory(0,frame.Count));var root=command.RootElement;
                if(i==2)
                {
                    calls++;string expression=root.GetProperty("params").GetProperty("expression").GetString()!;
                    string request=Regex.Match(expression,@"begin\('([a-f0-9]+)'",RegexOptions.CultureInvariant).Groups[1].Value;
                    await Send(new{method="Runtime.bindingCalled",@params=new{name="__peakKugouResult",payload=JsonSerializer.Serialize(new{id=request,result=sample.RootElement})}});
                }
                await Send(new{id=root.GetProperty("id").GetInt32(),result=new{}});
            }
        },timeout.Token);
        using var reader=new KugouApiReader(port);
        var first=await reader.ReadAsync(timeout.Token);await server;
        var again=await reader.ReadAsync(timeout.Token);
        check(first?.PositionMs==12345,"Kugou receives callback that precedes evaluate reply");
        check(ReferenceEquals(first,again)&&calls==1,"Kugou repeated reads reuse cache without native calls or timestamp refresh");
    }
}
