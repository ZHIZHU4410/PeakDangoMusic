using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DangoMusic.Core;
using DangoMusic.Desktop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed class LxClient : IDisposable
{
	private sealed class LyricResult
	{
		internal readonly string Key;

		internal readonly int Epoch;

		internal readonly LyricsTimeline Timeline;

		internal LyricResult(string key, int epoch, LyricsTimeline timeline)
		{
			Key = key;
			Epoch = epoch;
			Timeline = timeline;
		}
	}

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private readonly HttpClient http;

	private readonly Task worker;

	private volatile MusicState state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L);

	private int port = 23330;

	private int enabled = 1;

	private int epoch;

	internal MusicState Snapshot => state;

	internal LxClient(int initialPort, bool active)
	{
		port = Math.Clamp(initialPort, 1, 65535);
		enabled = (active ? 1 : 0);
		HttpClientHandler handler = new HttpClientHandler
		{
			UseProxy = false,
			AllowAutoRedirect = false,
			UseCookies = false,
			UseDefaultCredentials = false,
			Credentials = null,
			AutomaticDecompression = DecompressionMethods.None
		};
		http = new HttpClient(handler)
		{
			Timeout = Timeout.InfiniteTimeSpan
		};
		worker = Task.Run((Func<Task?>)RunAsync);
	}

	internal void Configure(int value, bool active)
	{
		value = Math.Clamp(value, 1, 65535);
		bool flag = Interlocked.Exchange(ref port, value) != value;
		int num = (active ? 1 : 0);
		if (flag | (Interlocked.Exchange(ref enabled, num) != num))
		{
			state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, active ? "正在连接 BLF / LX" : "漂浮歌词已关闭");
			Interlocked.Increment(ref epoch);
		}
	}

	internal void Reconnect()
	{
		state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, "正在重新连接 BLF / LX");
		Interlocked.Increment(ref epoch);
	}

	private async Task RunAsync()
	{
		string key = "";
		int lastEpoch = -1;
		int generation = 0;
		int attempts = 0;
		long retryAt = 0L;
		long lastSuccess = 0L;
		LyricsTimeline timeline = LyricsTimeline.Empty;
		Task<LyricResult> job = null;
		try
		{
			_ = 4;
			try
			{
				while (!lifetime.IsCancellationRequested)
				{
					int currentEpoch = Volatile.Read(ref epoch);
					int currentPort = Volatile.Read(ref port);
					if (currentEpoch != lastEpoch)
					{
						lastEpoch = currentEpoch;
						key = "";
						timeline = LyricsTimeline.Empty;
						retryAt = 0L;
						attempts = 0;
						generation++;
					}
					if (Volatile.Read(ref enabled) == 0)
					{
						state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, "漂浮歌词已关闭");
						await Task.Delay(600, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
						continue;
					}
					try
					{
						JObject payload = await ReadJsonAsync(currentPort, "/status?filter=status,name,singer,duration,progress,playbackRate", 65536, 1500, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
						if (currentEpoch != Volatile.Read(ref epoch))
						{
							continue;
						}
						long tick = MusicClock.Now;
						lastSuccess = tick;
						string status = Text(payload, "status", 32);
						string title = Text(payload, "name", 512);
						string artist = Text(payload, "singer", 512);
						if ((status != "playing" && status != "paused") || title.Length == 0 || !Number(payload, "duration", out var duration) || !Number(payload, "progress", out var progress) || duration <= 0.0 || duration > 86400.0 || progress < 0.0 || progress > duration + 0.1)
						{
							key = "";
							timeline = LyricsTimeline.Empty;
							state = new MusicState(connected: true, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, "已连接，请在 BLF / LX 中播放歌曲");
							await Task.Delay(600, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
							continue;
						}
						string text = title + "\u001f" + artist + "\u001f" + Math.Round(duration * 1000.0);
						if (text != key)
						{
							key = text;
							timeline = LyricsTimeline.Empty;
							retryAt = tick + 900;
							attempts = 0;
							generation++;
						}
						if (job != null && job.IsCompleted)
						{
							LyricResult lyricResult = await job.ConfigureAwait(continueOnCapturedContext: false);
							job = null;
							if (lyricResult.Key == key && lyricResult.Epoch == currentEpoch)
							{
								timeline = lyricResult.Timeline;
								generation++;
								attempts++;
								retryAt = tick + ((attempts < 4) ? 2000 : 15000);
							}
						}
						if (timeline.Lines.Count == 0 && job == null && tick >= retryAt)
						{
							job = ReadLyricsAsync(currentPort, key, currentEpoch, lifetime.Token);
						}
						double rate = ((Number(payload, "playbackRate", out var number) && number > 0.0) ? Math.Clamp(number, 0.1, 8.0) : 1.0);
						string message;
						if (timeline.Lines.Count > 0)
						{
							message = "已连接 · 使用播放器自带歌词";
						}
						else
						{
							message = ((job != null) ? "已连接 · 正在读取歌词" : "已连接 · 当前曲目暂无同步歌词");
						}
						state = new MusicState(connected: true, key, title, artist, (long)Math.Round(progress * 1000.0), (long)Math.Round(duration * 1000.0), status == "playing", tick, rate, timeline, generation, message);
						goto IL_07db;
					}
					catch (OperationCanceledException error) when (!lifetime.IsCancellationRequested)
					{
						Disconnected(lastSuccess, error);
						goto IL_07db;
					}
					catch (Exception ex) when (ex is InvalidDataException || ex is HttpRequestException || ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is ObjectDisposedException)
					{
						Disconnected(lastSuccess, ex);
						goto IL_07db;
					}
					IL_07db:
					await Task.Delay(state.Connected ? 600 : 1500, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (ObjectDisposedException)
			{
			}
		}
		finally
		{
			if (job != null)
			{
				try
				{
					await job.ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (OperationCanceledException)
				{
				}
			}
		}
	}

	private void Disconnected(long lastSuccess, Exception error)
	{
		if (!lifetime.IsCancellationRequested)
		{
			string text = DiagnosticLog.Report("BLF / LX", "读取 /status（端口 " + Volatile.Read(ref port) + "）", error, "LxClient.cs", 116);
			if (MusicClock.Now - lastSuccess > 3000)
			{
				state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, text + "\n请检查播放器本机API设置");
			}
		}
	}

	private async Task<LyricResult> ReadLyricsAsync(int currentPort, string key, int currentEpoch, CancellationToken token)
	{
		try
		{
			JObject data = await ReadJsonAsync(currentPort, "/lyric-all", 2097152, 6000, token).ConfigureAwait(continueOnCapturedContext: false);
			return new LyricResult(key, currentEpoch, LxLyricParser.Parse(TextField("lyric"), TextField("lxlyric"), TextField("tlyric")));
			string TextField(string name)
			{
				JToken val = data[name];
				if (val != null && (int)val.Type != 10)
				{
					if ((int)val.Type != 8)
					{
						throw new InvalidOperationException();
					}
					string text = (string)val;
					if (text.Length > 524288)
					{
						throw new InvalidOperationException();
					}
					return text;
				}
				return "";
			}
		}
		catch (Exception ex) when (ex is InvalidDataException || ex is HttpRequestException || ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is OperationCanceledException || ex is ObjectDisposedException || ex is FormatException || ex is OverflowException)
		{
			if (!token.IsCancellationRequested && currentEpoch == Volatile.Read(ref epoch))
			{
				DiagnosticLog.Report("BLF / LX", "读取或解析 /lyric-all（端口 " + currentPort + "）", ex, "LxClient.cs", 129);
			}
			return new LyricResult(key, currentEpoch, LyricsTimeline.Empty);
		}
	}

	private async Task<JObject> ReadJsonAsync(int currentPort, string path, int limit, int timeoutMs, CancellationToken token)
	{
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[1] { token });
		timeout.CancelAfter(timeoutMs);
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://127.0.0.1:" + currentPort + path);
		request.Headers.ConnectionClose = true;
		request.Headers.Accept.ParseAdd("application/json");
		using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(continueOnCapturedContext: false);
		if (response.StatusCode != HttpStatusCode.OK)
		{
			throw new HttpRequestException("HTTP " + (int)response.StatusCode + " · " + path.Split('?')[0]);
		}
		if (response.Content.Headers.ContentLength > limit)
		{
			throw new InvalidDataException("API响应超过 " + limit + " 字节 · " + path.Split('?')[0]);
		}
		using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(continueOnCapturedContext: false);
		using MemoryStream bytes = new MemoryStream();
		byte[] buffer = new byte[8192];
		int num;
		while ((num = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(continueOnCapturedContext: false)) > 0)
		{
			if (bytes.Length + num > limit)
			{
				throw new IOException("API response too large");
			}
			bytes.Write(buffer, 0, num);
		}
		using StringReader stringReader = new StringReader(Encoding.UTF8.GetString(bytes.ToArray()));
		JsonTextReader val = new JsonTextReader((TextReader)stringReader)
		{
			MaxDepth = 8,
			DateParseHandling = (DateParseHandling)0
		};
		try
		{
			return JObject.Load((JsonReader)(object)val);
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static string Text(JObject value, string key, int max)
	{
		JToken val = value[key];
		if (val == null || (int)val.Type != 8)
		{
			return "";
		}
		string text = (((string)val) ?? "").Trim();
		if (text.Length <= max && !text.Any(char.IsControl))
		{
			return text;
		}
		return "";
	}

	private static bool Number(JObject value, string key, out double number)
	{
		number = 0.0;
		JToken val = value[key];
		if (val == null || ((int)val.Type != 6 && (int)val.Type != 7))
		{
			return false;
		}
		number = (double)val;
		if (!double.IsNaN(number))
		{
			return !double.IsInfinity(number);
		}
		return false;
	}

	public void Dispose()
	{
		lifetime.Cancel();
		http.Dispose();
	}
}
