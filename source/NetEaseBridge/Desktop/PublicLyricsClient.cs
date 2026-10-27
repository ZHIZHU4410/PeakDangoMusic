using System.Globalization;
using System.Net;
using System.Text.Json;
using DangoMusic.Core;

namespace DangoMusic.Desktop;

/// <summary>Read-only, unauthenticated search and lyrics. Never accesses media or account APIs.</summary>
public sealed class PublicLyricsClient : IDisposable
{
    private const int MaximumBytes = 4 * 1024 * 1024;
    private static readonly Uri Origin = new("https://music.163.com/");
    private readonly HttpClient http;

    public PublicLyricsClient(HttpMessageHandler? handler = null)
    {
        handler ??= new HttpClientHandler { AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate };
        var transport = handler;
        while (transport is DelegatingHandler delegated && delegated.InnerHandler is not null) transport = delegated.InnerHandler;
        if (transport is HttpClientHandler standard)
        { standard.UseCookies = false; standard.AllowAutoRedirect = false; standard.UseDefaultCredentials = false; standard.Credentials = null; standard.MaxResponseHeadersLength = 64; }
        if (transport is SocketsHttpHandler sockets)
        { sockets.UseCookies = false; sockets.AllowAutoRedirect = false; sockets.Credentials = null; sockets.MaxResponseHeadersLength = 64; }
        http = new(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
    }

    public async Task<IReadOnlyList<MusicTrack>> SearchAsync(string query, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(query)) return Array.Empty<MusicTrack>();
        query = query.Trim();
        if (query.Length > 256) throw new ArgumentException("搜索关键词过长。", nameof(query));
        string parameters = "?s=" + Uri.EscapeDataString(query) + "&type=1&offset=0&limit=50";
        try
        {
            using var result = await GetAsync("/api/cloudsearch/pc" + parameters, ct).ConfigureAwait(false);
            return ParseTracks(result.RootElement);
        }
        catch (InvalidOperationException) { ct.ThrowIfCancellationRequested(); }
        using var fallback = await GetAsync("/api/search/get/web" + parameters, ct).ConfigureAwait(false);
        return ParseTracks(fallback.RootElement);
    }

    public async Task<LyricsTimeline> GetLyricsAsync(long id, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        using var result = await GetAsync("/api/song/lyric?id=" + id.ToString(CultureInfo.InvariantCulture) + "&lv=-1&kv=-1&tv=-1&yv=-1&ytv=-1", ct).ConfigureAwait(false);
        var root = result.RootElement;
        string yrc = Text(Child(root, "yrc"), "lyric");
        string translated = yrc.Length > 0 ? Text(Child(root, "ytlrc"), "lyric") : "";
        if (translated.Length == 0) translated = Text(Child(root, "tlyric"), "lyric");
        return LyricsTimeline.Parse(Text(Child(root, "lrc"), "lyric"), yrc, translated);
    }

    private async Task<JsonDocument> GetAsync(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var uri = new Uri(Origin, path);
        if (uri.Scheme != "https" || uri.Host != "music.163.com" || !uri.IsDefaultPort)
            throw new InvalidOperationException("拒绝访问非网易云 HTTPS 接口。");
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Referrer = Origin;
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 DangoDesktopLyrics/0.1");
        request.Headers.Accept.ParseAdd("application/json");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400) throw new InvalidOperationException("网易云歌词接口发生重定向，已停止请求。");
            if (!response.IsSuccessStatusCode) throw new InvalidOperationException($"网易云歌词接口暂不可用（HTTP {(int)response.StatusCode}）。");
            if (response.Content.Headers.ContentLength > MaximumBytes) throw new InvalidOperationException("网易云响应超过安全大小限制。");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var buffer = new MemoryStream();
            byte[] bytes = new byte[16384];
            while (true)
            {
                int read = await stream.ReadAsync(bytes, timeout.Token).ConfigureAwait(false);
                if (read == 0) break;
                if (buffer.Length + read > MaximumBytes) throw new InvalidOperationException("网易云响应超过安全大小限制。");
                buffer.Write(bytes, 0, read);
            }
            var json = JsonDocument.Parse(buffer.ToArray(), new JsonDocumentOptions { MaxDepth = 64 });
            var code = Child(json.RootElement, "code");
            if (code.ValueKind != JsonValueKind.Number || !code.TryGetInt32(out int number) || number != 200)
            { json.Dispose(); throw new InvalidOperationException("网易云公开歌词接口暂不可用，请稍后重试。"); }
            return json;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new InvalidOperationException("网易云歌词请求超时，请检查网络。"); }
        catch (HttpRequestException) { throw new InvalidOperationException("无法连接网易云公开歌词接口，请检查网络。"); }
        catch (JsonException) { throw new InvalidOperationException("网易云歌词接口返回了无法识别的数据。"); }
        catch (IOException) { throw new InvalidOperationException("读取网易云歌词响应失败，请稍后重试。"); }
    }

    private static IReadOnlyList<MusicTrack> ParseTracks(JsonElement root)
    {
        var result = Child(root, "result");
        if (result.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("网易云搜索接口暂不可用，请稍后重试。");
        return Elements(Child(result, "songs")).Where(s => Number(s, "id") > 0).Select(song =>
        {
            var artists = Child(song, "ar");
            if (artists.ValueKind != JsonValueKind.Array) artists = Child(song, "artists");
            string title = Text(song, "name");
            foreach (var alias in Elements(Child(song, "alia")).Where(a => a.ValueKind == JsonValueKind.String).Select(a => a.GetString()!).Where(a => !string.IsNullOrWhiteSpace(a)).Distinct().Take(3))
                if (!title.Contains(alias, StringComparison.OrdinalIgnoreCase)) title += " (" + alias + ")";
            long duration = Number(song, "dt");
            if (duration == 0) duration = Number(song, "duration");
            return new MusicTrack(Number(song, "id"), title, string.Join(" / ", Elements(artists).Select(a => Text(a, "name")).Where(a => a.Length > 0)), Math.Max(0, duration));
        }).ToArray();
    }

    private static JsonElement Child(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;
    private static string Text(JsonElement element, string name) => Child(element, name) is var value && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    private static long Number(JsonElement element, string name) => Child(element, name) is var value && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long number) ? number : 0;
    private static IEnumerable<JsonElement> Elements(JsonElement element) => element.ValueKind == JsonValueKind.Array ? element.EnumerateArray() : Enumerable.Empty<JsonElement>();
    public void Dispose() => http.Dispose();
}
