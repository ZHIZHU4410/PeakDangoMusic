using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

/// <summary>Serial background RPC to the original Muonium account/catalogue implementation.</summary>
internal sealed class MuoniumClient : IDisposable
{
    readonly SemaphoreSlim gate=new(1,1);
    readonly CancellationTokenSource lifetime=new();
    Process? child;
    int serial;
    bool restored;
    internal Task<JObject> Call(string operation,string source,JObject? fields=null)=>Task.Run(async()=>
    {
        await gate.WaitAsync(lifetime.Token).ConfigureAwait(false);
        try
        {
            EnsureStarted();
            if(!restored)
            {
                JObject credentials;
                try{credentials=AccountVault.Load();}catch(Exception ex){credentials=new JObject();DiagnosticLog.Report("音乐账号","读取Windows加密登录态",ex);}
                await Exchange(new JObject{["op"]="restore",["credentials"]=credentials}).ConfigureAwait(false);restored=true;
            }
            var request=fields??new JObject();request["op"]=operation;request["source"]=source;
            return await Exchange(request).ConfigureAwait(false);
        }
        catch{Stop();throw;}
        finally{gate.Release();}
    });
    void EnsureStarted()
    {
        if(child!=null&&!child.HasExited)return;
        string root=Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!,"PeakDangoMusic","Muonium");
        string java=Path.Combine(root,"Runtime","bin","java.exe");
        if(!File.Exists(java)||!File.Exists(Path.Combine(root,"PeakMuoniumBridge.jar")))throw new FileNotFoundException("缺少Muonium登录/曲库组件，请完整安装新版");
        child=new Process{StartInfo=new ProcessStartInfo(java,"-Djava.awt.headless=true -Xmx128m -cp \""+root+"\\*\" PeakMuoniumBridge "+Process.GetCurrentProcess().Id)
        {WorkingDirectory=root,UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=new UTF8Encoding(false),StandardErrorEncoding=new UTF8Encoding(false)}};
        child.Start();var errors=child.StandardError;
        _=Task.Run(async()=>{try{while(await errors.ReadLineAsync().ConfigureAwait(false)!=null){}}catch(IOException){}catch(ObjectDisposedException){}});
        restored=false;
    }
    async Task<JObject> Exchange(JObject request)
    {
        int id=++serial;request["id"]=id;
        await child!.StandardInput.WriteLineAsync(request.ToString(Formatting.None)).ConfigureAwait(false);
        await child.StandardInput.FlushAsync().ConfigureAwait(false);
        string line=await ReadBounded(child.StandardOutput,lifetime.Token).ConfigureAwait(false);
        using var reader=new JsonTextReader(new StringReader(line)){MaxDepth=32,DateParseHandling=DateParseHandling.None};
        var response=JObject.Load(reader);
        if((int?)response["id"]!=id)throw new InvalidDataException("Muonium组件响应序号错误");
        if((bool?)response["ok"]!=true)throw new InvalidOperationException((string?)response["error"]??"平台操作失败");
        var data=response["data"] as JObject??new JObject();
        if(data["credentials"] is JObject credentials)
        {
            try{AccountVault.Save(credentials);}catch(Exception ex){DiagnosticLog.Report("音乐账号","保存Windows加密登录态",ex);data["vaultWarning"]="登录已生效，但保存失败；重启后需要重新登录";}
            data.Remove("credentials");
        }
        return data;
    }
    static async Task<string> ReadBounded(StreamReader reader,CancellationToken ct)
    {
        var read=Task.Run(async()=>
        {
            var buffer=new char[4096];var text=new StringBuilder();
            // Read one character for protocol framing; StreamReader retains its own byte buffer.
            while(true){int count=await reader.ReadAsync(buffer,0,1).ConfigureAwait(false);if(count==0)throw new IOException("Muonium组件已退出");if(buffer[0]=='\n')return text.ToString();if(text.Length>=4*1024*1024)throw new InvalidDataException("Muonium响应过大");text.Append(buffer[0]);}
        });
        if(await Task.WhenAny(read,Task.Delay(75000,ct)).ConfigureAwait(false)!=read){ct.ThrowIfCancellationRequested();throw new TimeoutException("Muonium平台请求超时，请重试");}
        return await read.ConfigureAwait(false);
    }
    void Stop(){var process=child;child=null;restored=false;if(process!=null){try{if(!process.HasExited)process.Kill();}catch(InvalidOperationException){}finally{process.Dispose();}}}
    public void Dispose(){lifetime.Cancel();Stop();}
}
