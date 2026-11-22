using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DangoMusic.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed class GdMusicApi : IDisposable
{
	internal sealed record Track(string Id, string Title, string Artist, string Album, string LyricId, string Source, string Cover = "");

	internal sealed record Resolved(Track Track, string Url, LyricsTimeline Lyrics, string Note, string Format);

	internal static readonly string[] Keys = new string[10] { "netease", "joox", "bilibili", "kuwo", "tencent", "tidal", "qobuz", "apple", "ytmusic", "spotify" };

	internal static readonly string[] Names = new string[10] { "网易云音乐", "JOOX", "哔哩哔哩", "酷我音乐", "QQ音乐", "Tidal", "Qobuz", "Apple Music", "YouTube Music", "Spotify" };

	private readonly HttpClient http;

	private readonly Queue<long> requests = new Queue<long>();

	private readonly Dictionary<string, long> cooldown = new Dictionary<string, long>();

	internal const string Endpoint = "https://music-api.gdstudio.xyz/api.php";

	internal GdMusicApi(HttpMessageHandler? handler = null)
	{
		http = new HttpClient(handler ?? new HttpClientHandler
		{
			UseCookies = false,
			UseDefaultCredentials = false
		})
		{
			Timeout = TimeSpan.FromSeconds(12.0)
		};
		http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
	}

	private async Task<JToken> Get(string source, string query, CancellationToken ct)
	{
		if (!Enumerable.Contains(Keys, source))
		{
			throw new ArgumentException("未知聚合平台");
		}
		lock (requests)
		{
			long now = MusicClock.Now;
			while (requests.Count > 0 && now - requests.Peek() >= 300000)
			{
				requests.Dequeue();
			}
			if (requests.Count >= 50)
			{
				throw new InvalidOperationException("GD接口请求已达50次/5分钟，请稍后再试");
			}
			if (cooldown.TryGetValue(source, out var value) && value > now)
			{
				throw new InvalidOperationException("该平台暂时故障，冷却后可重试");
			}
			requests.Enqueue(now);
		}
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[1] { ct });
		timeout.CancelAfter(12000);
		ct = timeout.Token;
		try
		{
			using HttpResponseMessage response = await http.GetAsync("https://music-api.gdstudio.xyz/api.php?source=" + Uri.EscapeDataString(source) + "&" + query, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(continueOnCapturedContext: false);
			response.EnsureSuccessStatusCode();
			using Stream input = await response.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false);
			using MemoryStream data = new MemoryStream();
			byte[] buffer = new byte[8192];
			int num;
			while ((num = await input.ReadAsync(buffer, 0, buffer.Length, ct).ConfigureAwait(continueOnCapturedContext: false)) > 0)
			{
				if (data.Length + num > 2097152)
				{
					throw new InvalidDataException("GD响应超过2MiB");
				}
				data.Write(buffer, 0, num);
			}
			JsonTextReader val = new JsonTextReader((TextReader)new StringReader(Encoding.UTF8.GetString(data.ToArray())))
			{
				MaxDepth = 20,
				DateParseHandling = (DateParseHandling)0
			};
			try
			{
				JToken val2 = JToken.ReadFrom((JsonReader)(object)val);
				JObject val3 = (JObject)(object)((val2 is JObject) ? val2 : null);
				if (val3 != null && (val3["detail"] != null || val3["error"] != null))
				{
					throw new InvalidDataException(((string)(val3["detail"] ?? val3["error"])) ?? "平台未开放");
				}
				return val2;
			}
			finally
			{
				((IDisposable)val)?.Dispose();
			}
		}
		catch (OperationCanceledException)
		{
			throw;
		}
		catch
		{
			lock (requests)
			{
				cooldown[source] = MusicClock.Now + 30000;
			}
			throw;
		}
	}

	internal async Task<Track[]> Search(string source, string keyword, int page, CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(keyword))
		{
			return Array.Empty<Track>();
		}
		JToken val = await Get(source, "types=search&name=" + Uri.EscapeDataString(keyword.Trim()) + "&count=20&pages=" + Math.Max(1, page), ct).ConfigureAwait(continueOnCapturedContext: false);
		return (from o in ((IEnumerable)(((val is JArray) ? val : null) ?? throw new InvalidDataException("GD搜索响应格式错误"))).OfType<JObject>()
			where !string.IsNullOrEmpty((string)o["id"])
			select o).Take(20).Select((JObject o) =>
		{
			string id = (string)o["id"];
			string title = ((string)o["name"]) ?? "";
			JToken val2 = o["artist"];
			JArray val3 = (JArray)(object)((val2 is JArray) ? val2 : null);
			return new Track(id, title, (val3 != null) ? string.Join(" / ", ((JContainer)val3).Values<string>()) : (((string)o["artist"]) ?? ""), ((string)o["album"]) ?? "", ((string)o["lyric_id"]) ?? "", Enumerable.Contains(Keys, (string)o["source"]) ? ((string)o["source"]) : source, ((string)(o["pic"] ?? o["cover"])) ?? "");
		}).ToArray();
	}

    internal static string AudioFormat(string format, Uri uri) =>
        (string.IsNullOrWhiteSpace(format) ? Path.GetExtension(uri.AbsolutePath) : format).Trim().TrimStart('.').ToLowerInvariant();

	internal async Task<Resolved> Resolve(Track track, int bitrate, CancellationToken ct)
	{
		string url = "";
		string format = "";
		foreach (int item in new int[3] { bitrate, 320, 128 }.Distinct())
		{
			JToken val = await Get(track.Source, "types=url&id=" + Uri.EscapeDataString(track.Id) + "&br=" + item, ct).ConfigureAwait(continueOnCapturedContext: false);
			url = ((string)val[(object)"url"]) ?? "";
			format = ((string)(val[(object)"type"] ?? val[(object)"format"])) ?? "";
			if (!Uri.TryCreate(url, UriKind.Absolute, out Uri result) ||
                (result.Scheme != "https" && result.Scheme != "http") ||
                AudioFormat(format, result) is "flac" or "m4a" or "aac")
			{
				url = "";
				continue;
			}
			break;
		}
		if (url.Length == 0)
		{
			throw new InvalidOperationException("平台未返回播放地址，请换一首歌曲或音源");
		}
		LyricsTimeline lyrics = LyricsTimeline.Empty;
		string note = "";
		if (track.LyricId.Length > 0)
		{
			try
			{
				JToken val2 = await Get(track.Source, "types=lyric&id=" + Uri.EscapeDataString(track.LyricId), ct).ConfigureAwait(continueOnCapturedContext: false);
				lyrics = LyricsTimeline.Parse((string)val2[(object)"lyric"], null, (string)val2[(object)"tlyric"]);
			}
			catch (Exception ex) when (!(ex is OperationCanceledException))
			{
				note = DiagnosticLog.Report("GD聚合音乐", "读取同步歌词", ex, "GdMusicApi.cs", 78);
			}
		}
		return new Resolved(track, url, lyrics, note, format);
	}

	public void Dispose()
	{
		http.Dispose();
	}
}
