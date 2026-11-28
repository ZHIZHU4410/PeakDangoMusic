using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using Newtonsoft.Json;

namespace PeakDangoMusic;

// Only immutable clock readings cross back to the game thread. The pipe and process
// have their own lifetime and never need Unity's SynchronizationContext.
internal sealed class IndependentAudio : IDisposable
{
    internal sealed class Update
    {
        public string Type = "";
        public int Id;
        public int Token;
        public int Revision;
        public bool Playing;
        public long Position;
        public long Duration;
        public long Bytes;
        public double Rate = 1;
        public string Error = "";
        internal long ReceivedAt;
    }

    private readonly ConcurrentQueue<string> commands = new();
    private readonly SemaphoreSlim available = new(0);
    private readonly CancellationTokenSource stop = new();
    private readonly Task worker;
    private int disposed;
    internal readonly ConcurrentQueue<Update> Events = new();
    internal volatile Update? Sample;
    internal volatile string? Failure;

    internal IndependentAudio()
    {
        worker = Task.Run(Run);
    }

    internal void Send(object command)
    {
        if (Volatile.Read(ref disposed) != 0 || Failure != null) return;
        commands.Enqueue(JsonConvert.SerializeObject(command));
        available.Release();
    }

    private async Task Run()
    {
        var token = stop.Token;
        string root = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location)!, "PeakDangoMusic", "Folia");
        string runtime = Path.Combine(root, "Runtime", "PeakFoliaRenderer.exe");
        Process? child = null;
        try
        {
            if (!File.Exists(runtime))
                throw new FileNotFoundException("缺少独立音频运行时，请安装完整安装包。");
            string application = Path.Combine(Paths.CachePath, "PeakDangoMusic", "AudioWorker");
            Directory.CreateDirectory(application);
            foreach (string script in new[] { "main.cjs", "preload.cjs" })
            {
                using var resource = typeof(Plugin).Assembly.GetManifestResourceStream("PeakDangoMusic.Audio." + script)
                    ?? throw new IOException("独立音频脚本缺失。");
                using var file = File.Create(Path.Combine(application, script));
                resource.CopyTo(file);
            }
            string profile = Path.Combine(application, "Profile");
            Directory.CreateDirectory(profile);
            string name = "PeakAudio-" + Guid.NewGuid().ToString("N");
            using var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
            using var close = token.Register(() => pipe.Dispose());
            var options = new ProcessStartInfo(runtime,
                $"\"{Path.Combine(application, "main.cjs")}\" --pipe={name} --profile=\"{profile}\" --cache=\"{OwnedAudioCache.Root}\"")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                WorkingDirectory = application,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            options.EnvironmentVariables.Remove("ELECTRON_RUN_AS_NODE");
            token.ThrowIfCancellationRequested();
            child = Process.Start(options) ?? throw new IOException("无法启动独立音频进程。");
            child.BeginOutputReadLine();
            child.BeginErrorReadLine();
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
            startup.CancelAfter(TimeSpan.FromSeconds(12));
            await pipe.WaitForConnectionAsync(startup.Token).ConfigureAwait(false);
            using var reader = new StreamReader(pipe, new UTF8Encoding(false), false, 4096, leaveOpen: true);
            using var writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true);
            var ready = reader.ReadLineAsync();
            if (await Task.WhenAny(ready, Task.Delay(12000, token)).ConfigureAwait(false) != ready ||
                await ready.ConfigureAwait(false) != "{\"type\":\"ready\"}")
                throw new IOException("独立音频进程初始化超时。");
            using var connected = CancellationTokenSource.CreateLinkedTokenSource(token);
            var read = Read(reader);
            var write = Write(writer, connected.Token);
            var first = await Task.WhenAny(read, write).ConfigureAwait(false);
            connected.Cancel();
            pipe.Dispose();
            try { await Task.WhenAll(read, write).ConfigureAwait(false); }
            catch when (!first.IsFaulted) { }
            await first.ConfigureAwait(false);
            if (!token.IsCancellationRequested) throw new IOException("独立音频进程已断开。");
        }
        catch (Exception error) when (!token.IsCancellationRequested)
        {
            Failure = error is OperationCanceledException ? "启动独立音频进程超时。" : error.Message;
        }
        catch (Exception) when (token.IsCancellationRequested) { }
        finally
        {
            if (child != null)
            {
                try { if (!child.WaitForExit(300)) child.Kill(); }
                catch (InvalidOperationException) { }
                finally { child.Dispose(); }
            }
        }
    }

    private async Task Read(StreamReader reader)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            if (line.Length > 8192) throw new IOException("独立音频状态超出格式限制。");
            var update = JsonConvert.DeserializeObject<Update>(line);
            if (update == null) continue;
            update.ReceivedAt = MusicClock.Now;
            if (update.Type is "sample" or "started" or "ended") Sample = update;
            if (update.Type != "sample") Events.Enqueue(update);
        }
    }

    private async Task Write(StreamWriter writer, CancellationToken token)
    {
        while (true)
        {
            await available.WaitAsync(token).ConfigureAwait(false);
            if (commands.TryDequeue(out string line))
            {
                await writer.WriteLineAsync(line).ConfigureAwait(false);
                await writer.FlushAsync().ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        stop.Cancel();
        _ = worker.ContinueWith(task =>
        {
            _ = task.Exception;
            available.Dispose();
            stop.Dispose();
        }, TaskScheduler.Default);
    }
}
