using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace DangoMusic.Desktop;

public sealed class CdpProgressReader : IDisposable
{
    private const int MaximumBytes = 256 * 1024, MaximumFrameBytes = 64 * 1024;
    private const long MaximumTrackMs = 86_400_000;
    private readonly int port;
    private readonly HttpClient http;
    private readonly HttpMessageInvoker websocketTransport;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim readGate = new(1, 1);
    private int disposed;
    public string Status { get; private set; } = "等待网易云本机进度接口";

    // Fixed playback reads only. The once-per-page webpack capture obtains an existing module cache;
    // it never dispatches actions, subscribes to native events, controls media or reads credentials/storage.
    // Timer fallback reads only visible elapsed/total text and an explicit MediaSession state.
    // Ambiguous/missing observations return null.
    internal const string Expression = """
        (() => {
          if (location.protocol !== 'orpheus:') return null;
          // Read initialized exports only. Module numbers/export letters change between builds.
          // Only invoke the known store getter and syntactically pure cached-value getters.
          try {
            if (typeof window.__dangoReadOnlyRequire !== 'function' && Array.isArray(window.webpackJsonp)) {
              window.webpackJsonp.push([[987661],{987661:function(module,exports,require){window.__dangoReadOnlyRequire=require;}},[[987661]]]);
            }
            const r = window.__dangoReadOnlyRequire;
            const cache = r && r.c;
            const pure = f => typeof f === 'function' && /^\(\)\s*=>\s*[a-zA-Z_$][\w$]*$/.test(Function.prototype.toString.call(f));
            const stateEnum = s => s && s.Pause === 1 && s.Playing === 2 && s.Stop === 0;
            const read = (store, progress, states) => {
              if (!store || typeof store.getStore !== 'function' || !pure(progress) || !stateEnum(states)) return null;
              const root = store.getStore(), p = root && root.playing, q = progress();
              const track = p && p.curPlaying && p.curPlaying.track;
              const songId = Number(p && p.curPlaying && p.curPlaying.resourceId);
              const idMatches = p && q && (p.playId ? String(q.playId) === String(p.playId) :
                p.playingState === states.Pause && String(q.playId) === String(songId));
              if (track && idMatches && Number.isSafeInteger(songId) && songId > 0 &&
                  Number.isFinite(q.current) && q.current >= 0 && Number.isFinite(p.resourceDuration) &&
                  p.resourceDuration > 0 && p.resourceDuration <= 86400 && q.current <= p.resourceDuration &&
                  (p.playingState === states.Playing || p.playingState === states.Pause)) {
                const title = typeof track.name === 'string' ? track.name.slice(0,512) : '';
                const artist = Array.isArray(track.artists) ? track.artists.map(a => typeof a.name === 'string' ? a.name : '').filter(Boolean).join(' / ').slice(0,512) : '';
                if (!title.trim()) return null;
                return {clock:q,frame:{title,artist,songId,source:'native-cache',positionMs:Math.round(q.current*1000),
                  durationMs:Math.round(p.resourceDuration*1000),playing:p.playingState === states.Playing}};
              }
              return null;
            };
            if (cache) {
              let legacy = null;
              try {
                const exp = id => cache[id] && cache[id].exports || {};
                legacy = read(exp(8).a,exp(148).b,exp(7).k);
              } catch (_) { /* The known 3.1.42 slots may now contain unrelated exports. */ }
              if (legacy) return legacy.frame;
              let candidates = window.__dangoPlaybackCandidates;
              if (!candidates || candidates.cache !== cache || Date.now()-candidates.scannedAt > 10000) {
                candidates = {cache,scannedAt:Date.now(),stores:[],progress:[],states:[]};
                const add = (list,value,limit) => { if(list.length<limit && !list.includes(value))list.push(value); };
                for (const id of Object.keys(cache).slice(0,4096)) {
                  const exports = cache[id] && cache[id].exports;
                  if (!exports) continue;
                  for (const key of Object.keys(exports).slice(0,64)) {
                    try {
                      const value = exports[key];
                      if (value && typeof value.getStore === 'function') add(candidates.stores,value,8);
                      if (stateEnum(value)) add(candidates.states,value,8);
                      if (pure(value)) add(candidates.progress,value,96);
                    } catch (_) { /* Ignore unavailable cached exports. */ }
                  }
                }
                window.__dangoPlaybackCandidates = candidates;
              }
              const matches = [];
              for (const store of candidates.stores) for (const states of candidates.states) for (const progress of candidates.progress) {
                try {
                  const match = read(store,progress,states);
                  if (!match) continue;
                  // Multiple export aliases of the same cached clock are equivalent.
                  if (!matches.some(m=>m.clock===match.clock && m.frame.songId===match.frame.songId)) matches.push(match);
                } catch (_) { /* Not the active playback state/clock pair. */ }
              }
              if (matches.length === 1) return matches[0].frame;
              if (matches.length > 1) return null;
            }
          } catch (_) { /* Unsupported client shape: only standards-based fallbacks remain. */ }
          const session = navigator.mediaSession;
          const meta = session && session.metadata;
          const text = value => typeof value === 'string' ? value.slice(0, 512).trim() : '';
          const identity = {title:text(meta && meta.title), artist:text(meta && meta.artist), documentTitle:text(document.title)};
          const audio = Array.from(document.querySelectorAll('audio')).slice(0,32).filter(a =>
            a.readyState >= 1 && a.currentSrc && Number.isFinite(a.duration) && a.duration > 0 && a.duration <= 86400 &&
            Number.isFinite(a.currentTime) && a.currentTime >= 0 && a.currentTime <= a.duration);
          if (audio.length === 1) {
            const a = audio[0];
            return {...identity, source:'audio', positionMs:Math.round(a.currentTime*1000),
              durationMs:Math.round(a.duration*1000), playing:!a.paused && !a.ended};
          }
          if (audio.length > 1 || !session || !['playing','paused'].includes(session.playbackState)) return null;
          if (!identity.title && !identity.documentTitle.includes(' - ')) return null;
          const clocks = [], separate = [];
          const clockText = '[0-9]{1,3}:[0-5][0-9](?::[0-5][0-9])?';
          const pattern = new RegExp('^(' + clockText + ')\\s*\\/\\s*(' + clockText + ')$');
          const single = new RegExp('^' + clockText + '$');
          const seconds = value => value.split(':').reduce((n,v) => n*60 + Number(v), 0);
          for (const node of Array.from(document.querySelectorAll('span,time')).slice(0,2000)) {
            if (node.childElementCount || !node.getClientRects().length) continue;
            const content = node.textContent || '';
            if (content.length > 40) continue;
            const value = content.trim(), match = value.match(pattern);
            if (match) {
              const current = seconds(match[1]), total = seconds(match[2]);
              if (total <= 0 || total > 86400 || current > total) continue;
              clocks.push({positionMs:current*1000, durationMs:total*1000});
            } else if (single.test(value)) {
              separate.push({seconds:seconds(value), parent:node.parentElement});
            }
            if (clocks.length > 1 || separate.length > 2) return null;
          }
          const related = separate.length === 2 && separate[0].parent && separate[1].parent &&
            (separate[0].parent === separate[1].parent ||
             separate[0].parent.parentElement && separate[0].parent.parentElement === separate[1].parent.parentElement);
          if (!clocks.length && related && separate[1].seconds > 0 &&
              separate[1].seconds <= 86400 && separate[0].seconds <= separate[1].seconds)
            clocks.push({positionMs:separate[0].seconds*1000, durationMs:separate[1].seconds*1000});
          return clocks.length === 1 ? {...identity, ...clocks[0], source:'timer', playing:session.playbackState === 'playing'} : null;
        })()
        """;

    public CdpProgressReader(int port = 9223)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        this.port = port;
        http = new HttpClient(Transport()) { Timeout = Timeout.InfiniteTimeSpan };
        websocketTransport = new HttpMessageInvoker(Transport());
    }

    private static SocketsHttpHandler Transport() => new()
    {
        UseProxy = false, AllowAutoRedirect = false, UseCookies = false,
        Credentials = null, MaxResponseHeadersLength = 16, ConnectTimeout = TimeSpan.FromSeconds(1)
    };

    /// <summary>Returns null for an unavailable/ambiguous timeline; never substitutes a zero clock.</summary>
    public async Task<DesktopPlayback?> ReadAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (Volatile.Read(ref disposed) != 0) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var token = timeout.Token;
        bool entered = false;
        try
        {
            await readGate.WaitAsync(token).ConfigureAwait(false);
            entered = true;
            Status = "正在读取网易云本机页面";
            using var response = await http.GetAsync($"http://127.0.0.1:{port}/json", HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
            if (response.StatusCode != HttpStatusCode.OK || response.Content.Headers.ContentLength > MaximumBytes) { Status = "网易云本机页面响应异常"; return null; }
            await using var stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
            using var list = JsonDocument.Parse(await ReadBoundedAsync(stream, MaximumBytes, token).ConfigureAwait(false), new JsonDocumentOptions { MaxDepth = 12 });
            var target = SelectTarget(list.RootElement, port);
            if (target == null) { Status = "没有找到唯一的网易云播放页面"; return null; }
            using var socket = new ClientWebSocket();
            Status = "正在连接网易云页面 WebSocket";
            await socket.ConnectAsync(target, websocketTransport, token).ConfigureAwait(false);
            Status = "正在读取网易云页面曲目与进度";
            var request = JsonSerializer.SerializeToUtf8Bytes(new
            {
                id = 1, method = "Runtime.evaluate",
                @params = new { expression = Expression, returnByValue = true, awaitPromise = false, silent = true, timeout = 500 }
            });
            await socket.SendAsync(request.AsMemory(), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
            int total = 0;
            for (int messages = 0; messages < 16; messages++)
            {
                var bytes = await ReceiveBoundedAsync(socket, token).ConfigureAwait(false);
                if (bytes == null || (total += bytes.Length) > MaximumBytes) return null;
                using var reply = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 12 });
                var root = reply.RootElement;
                if (Child(root, "id").TryNumber(out var id) && id == 1)
                {
                    var playback = ParseReply(root, Environment.TickCount64);
                    Status = playback == null ? "页面已连接，但未识别有效曲目或进度" : "已读取网易云本机播放进度";
                    return playback;
                }
            }
            return null;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { Status = "网易云读取超时；" + Status; return null; }
        catch (Exception ex) when (ex is HttpRequestException or WebSocketException or IOException or JsonException or ObjectDisposedException)
        { Status = "网易云本机连接异常（" + ex.GetType().Name + "）；" + Status; return null; }
        finally { if (entered) readGate.Release(); }
    }

    internal static Uri? SelectTarget(JsonElement targets, int port)
    {
        if (targets.ValueKind != JsonValueKind.Array || targets.GetArrayLength() > 128) return null;
        Uri? result = null;
        foreach (var target in targets.EnumerateArray())
        {
            if (Text(target, "type") != "page" || !Text(target, "url").StartsWith("orpheus://", StringComparison.OrdinalIgnoreCase) ||
                !Uri.TryCreate(Text(target, "url"), UriKind.Absolute, out var page) || page.Scheme != "orpheus") continue;
            if (!Uri.TryCreate(Text(target, "webSocketDebuggerUrl"), UriKind.Absolute, out var endpoint) ||
                endpoint.Scheme != "ws" || endpoint.Port != port || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0 ||
                !IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var address) || !IPAddress.IsLoopback(address) ||
                !endpoint.AbsolutePath.StartsWith("/devtools/page/", StringComparison.Ordinal) || endpoint.AbsolutePath.Length <= 15) continue;
            if (result != null) return null;
            result = endpoint;
        }
        return result;
    }

    internal static DesktopPlayback? ParseReply(JsonElement reply, long sampleTick)
    {
        if (!Child(reply, "id").TryNumber(out var id) || id != 1 || Child(reply, "error").ValueKind != JsonValueKind.Undefined) return null;
        var envelope = Child(reply, "result");
        if (Child(envelope, "exceptionDetails").ValueKind != JsonValueKind.Undefined) return null;
        var value = Child(Child(envelope, "result"), "value");
        if (!Child(value, "positionMs").TryNumber(out var position) || !Child(value, "durationMs").TryNumber(out var duration) ||
            position < 0 || duration <= 0 || duration > MaximumTrackMs || position > duration ||
            Child(value, "playing").ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return null;
        string source = Text(value, "source"), title = Text(value, "title"), artist = Text(value, "artist");
        if (source is not ("audio" or "timer" or "native-cache")) return null;
        long songId = 0;
        if (source == "native-cache")
        {
            var song = Child(value, "songId");
            if (song.ValueKind != JsonValueKind.Number || !song.TryGetInt64(out songId) || songId <= 0 || songId > 9_007_199_254_740_991) return null;
        }
        if (title.Length == 0)
        {
            string documentTitle = Text(value, "documentTitle");
            int split = documentTitle.LastIndexOf(" - ", StringComparison.Ordinal);
            if (split > 0 && split < documentTitle.Length - 3)
            { title = documentTitle[..split].Trim(); artist = documentTitle[(split + 3)..].Trim(); }
        }
        // The caller must match title/artist to SMTC before merging this optional clock.
        return new(title, artist, songId, (long)Math.Round(position), (long)Math.Round(duration),
            Child(value, "playing").GetBoolean(), sampleTick, "CDP/" + source);
    }

    internal static async Task<byte[]> ReadBoundedAsync(Stream input, int limit, CancellationToken ct)
    {
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await input.ReadAsync(buffer.AsMemory(), ct).ConfigureAwait(false)) > 0)
        {
            if (data.Length + count > limit) throw new IOException("CDP response exceeds its size limit.");
            data.Write(buffer, 0, count);
        }
        return data.ToArray();
    }

    private static async Task<byte[]?> ReceiveBoundedAsync(ClientWebSocket socket, CancellationToken ct)
    {
        using var data = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var part = await socket.ReceiveAsync(buffer.AsMemory(), ct).ConfigureAwait(false);
            if (part.MessageType != WebSocketMessageType.Text || data.Length + part.Count > MaximumFrameBytes) return null;
            data.Write(buffer, 0, part.Count);
            if (part.EndOfMessage) return data.ToArray();
        }
    }

    private static JsonElement Child(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var child) ? child : default;
    private static string Text(JsonElement element, string name)
    {
        var child = Child(element, name);
        if (child.ValueKind != JsonValueKind.String) return "";
        string text = child.GetString() ?? "";
        return text.Length <= 2048 && !text.Any(char.IsControl) ? text.Trim() : "";
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel(); http.Dispose(); websocketTransport.Dispose();
        // Keep the token/gate usable while an in-flight read unwinds.
    }
}

internal static class CdpJsonNumber
{
    internal static bool TryNumber(this JsonElement element, out double value)
    {
        value = 0;
        return element.ValueKind == JsonValueKind.Number && element.TryGetDouble(out value) && double.IsFinite(value);
    }
}
