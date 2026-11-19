using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DangoMusic.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed class NativeMusicClient : IDisposable
{
	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private readonly Task worker;

	private readonly object gate = new object();

	private Process? child;

	private volatile MusicState state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, "等待原版网易云音乐");

	private int restartEpoch = -1;

	private int restarts;

	private int enabled;

	private int port = 9223;

	private int epoch;

	private int source = 1;

	private string lyricsFolder = "";

	private string Player => Volatile.Read(ref source) switch
	{
		2 => "酷狗音乐", 
		3 => "QQ音乐", 
		5 => "系统媒体会话", 
		_ => "原版网易云音乐", 
	};

	internal MusicState Snapshot => state;

	internal NativeMusicClient()
	{
		worker = Task.Run((Func<Task?>)Run);
	}

	internal void Configure(int nextPort, bool active, int nextSource = 1, string nextFolder = "")
	{
		bool flag = lyricsFolder != nextFolder;
		lyricsFolder = nextFolder;
		if (flag | (Interlocked.Exchange(ref source, nextSource) != nextSource) | (Interlocked.Exchange(ref port, nextPort) != nextPort) | (Interlocked.Exchange(ref enabled, active ? 1 : 0) != (active ? 1 : 0)))
		{
			Reconnect();
		}
	}

	internal void Reconnect()
	{
		Interlocked.Increment(ref epoch);
		state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, "正在连接" + Player);
		StopChild();
	}

	private void StopChild()
	{
		lock (gate)
		{
			if (child == null)
			{
				return;
			}
			try
			{
				if (!child.HasExited)
				{
					child.Kill();
				}
			}
			catch (InvalidOperationException)
			{
			}
			catch (Win32Exception)
			{
			}
		}
	}

	private async Task Run()
	{
		_ = 4;
		try
		{
			while (!lifetime.IsCancellationRequested)
			{
				if (Volatile.Read(ref enabled) == 0)
				{
					await Task.Delay(400, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				int currentEpoch = Volatile.Read(ref epoch);
				if (restartEpoch != currentEpoch)
				{
					restartEpoch = currentEpoch;
					restarts = 0;
				}
				if (restarts >= 3)
				{
					await Task.Delay(400, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				string text = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "PeakDangoMusic", "Native", "PeakNeteaseBridge.exe");
				if (!File.Exists(text))
				{
					state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, DiagnosticLog.Report(Player, "连接组件 / 检查安装文件", new FileNotFoundException("缺少 PeakDangoMusic/Native/PeakNeteaseBridge.exe，请完整安装"), "NativeMusicClient.cs", 50));
					await Task.Delay(2500, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				Process process = null;
				string stage = "连接组件 / 启动进程";
				try
				{
					Process process2 = new Process();
					Process process3 = process2;
					string fileName = text;
					string text2 = Process.GetCurrentProcess().Id.ToString();
					string text3 = Volatile.Read(ref port).ToString();
					string text4 = Volatile.Read(ref source) switch
					{
						2 => "kugou", 
						3 => "qqmusic", 
						5 => "external", 
						_ => "netease", 
					};
					ProcessStartInfo processStartInfo = new ProcessStartInfo(fileName, text2 + " " + text3 + " " + text4 + " \"" + lyricsFolder.Replace("\"", "").TrimEnd('\\') + "\"");
					processStartInfo.UseShellExecute = false;
					processStartInfo.CreateNoWindow = true;
					processStartInfo.WindowStyle = ProcessWindowStyle.Hidden;
					processStartInfo.RedirectStandardOutput = true;
					processStartInfo.StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
					processStartInfo.WorkingDirectory = Path.GetDirectoryName(text);
					process3.StartInfo = processStartInfo;
					process = process2;
					lock (gate)
					{
						if (currentEpoch != Volatile.Read(ref epoch) || Volatile.Read(ref enabled) == 0 || lifetime.IsCancellationRequested)
						{
							continue;
						}
						process.Start();
						child = process;
						goto IL_0428;
					}
					IL_0428:
					long startedAt = MusicClock.Now;
					LyricsTimeline timeline = LyricsTimeline.Empty;
					string timelineKey = "";
					string reportedMessage = "";
					string reportedDiagnostic = "";
					long reportedStamp = 0L;
					while (!lifetime.IsCancellationRequested && currentEpoch == Volatile.Read(ref epoch) && Volatile.Read(ref enabled) != 0)
					{
						stage = "连接组件 / 等待心跳";
						string text5 = await ReadLineBounded(process.StandardOutput, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
						if (text5 == null)
						{
							throw new IOException("连接组件输出已关闭" + (process.HasExited ? ("，退出码 " + process.ExitCode) : ""));
						}
						if (MusicClock.Now - startedAt > 10000)
						{
							restarts = 0;
						}
						stage = "连接组件 / 解析JSON数据";
						JsonTextReader val = new JsonTextReader((TextReader)new StringReader(text5))
						{
							MaxDepth = 12,
							DateParseHandling = (DateParseHandling)0
						};
						try
						{
							JObject val2 = JObject.Load((JsonReader)(object)val);
							string text6 = Text((JToken)(object)val2, "key", 1600);
							if (text6 != timelineKey)
							{
								timelineKey = text6;
								timeline = LyricsTimeline.Empty;
							}
							JToken val3 = val2["lines"];
							JArray val4 = (JArray)(object)((val3 is JArray) ? val3 : null);
							if (val4 != null)
							{
								stage = "连接组件 / 解析歌词行与逐字时间";
								if (((JContainer)val4).Count > 12000)
								{
									throw new InvalidDataException();
								}
								List<LyricsLine> list = new List<LyricsLine>();
								foreach (JToken item in val4)
								{
									JToken val5 = item[(object)"words"];
									JToken obj = ((val5 is JArray) ? val5 : null);
									LyricsWord[] words = ((obj != null) ? (from w in ((IEnumerable<JToken>)obj).Take(1000)
										select new LyricsWord(((long?)w[(object)"startMs"]).GetValueOrDefault(), ((long?)w[(object)"durationMs"]).GetValueOrDefault(), Text(w, "text", 4096))).ToArray() : null);
									list.Add(new LyricsLine(((long?)item[(object)"startMs"]).GetValueOrDefault(), ((long?)item[(object)"durationMs"]).GetValueOrDefault(), Text(item, "text", 8192), words, Text(item, "translation", 8192)));
								}
								timeline = LyricsTimeline.FromLines(list);
							}
							stage = "连接组件 / 解析播放进度";
							long valueOrDefault = ((long?)val2["durationMs"]).GetValueOrDefault();
							long? num = (long?)val2["positionMs"];
							bool flag = num >= 0 && valueOrDefault > 0 && valueOrDefault <= 86400000 && num <= valueOrDefault;
							if (currentEpoch != Volatile.Read(ref epoch))
							{
								break;
							}
							string text7 = Text((JToken)(object)val2, "message", 2048);
							string text8 = Text((JToken)(object)val2, "diagnostic", 2048);
							bool flag2 = text7 != reportedMessage;
							if (flag2)
							{
								DiagnosticLog.Report(text7);
							}
							long valueOrDefault2 = ((long?)val2["diagnosticStamp"]).GetValueOrDefault();
							if ((text8 != reportedDiagnostic || valueOrDefault2 != reportedStamp) && (!flag2 || text8 != text7))
							{
								DiagnosticLog.Report(text8);
							}
							reportedMessage = text7;
							reportedDiagnostic = text8;
							reportedStamp = valueOrDefault2;
							state = new MusicState((bool?)val2["connected"] == true, flag ? text6 : "", Text((JToken)(object)val2, "title", 512), Text((JToken)(object)val2, "artist", 512), num.GetValueOrDefault(), valueOrDefault, (bool?)val2["playing"] == true, MusicClock.Now, 1.0, timeline, ((int?)val2["generation"]).GetValueOrDefault(), Text((JToken)(object)val2, "message", 1024));
							continue;
						}
						finally
						{
							((IDisposable)val)?.Dispose();
						}
					}
					goto IL_0ab0;
				}
				catch (Exception ex) when (ex is InvalidDataException || ex is TimeoutException || ex is IOException || ex is JsonException || ex is InvalidOperationException || ex is Win32Exception || ex is ArgumentException || ex is OverflowException)
				{
					if (currentEpoch == Volatile.Read(ref epoch) && Volatile.Read(ref enabled) != 0 && !lifetime.IsCancellationRequested)
					{
						state = new MusicState(connected: false, "", "", "", 0L, 0L, playing: false, 0L, 1.0, null, 0, DiagnosticLog.Report(Player, stage, ex, "NativeMusicClient.cs", 101) + ((++restarts >= 3) ? "\n连续失败3次，请点击重新连接" : "\n正在重试"));
					}
					goto IL_0ab0;
				}
				finally
				{
					lock (gate)
					{
						if (child == process)
						{
							StopChild();
							child = null;
						}
					}
					process?.Dispose();
				}
				IL_0ab0:
				if (!lifetime.IsCancellationRequested)
				{
					await Task.Delay(1800, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
		}
		catch (OperationCanceledException)
		{
		}
	}

	private static async Task<string?> ReadLineBounded(StreamReader reader, CancellationToken token)
	{
		token.ThrowIfCancellationRequested();
		Task<string> read = reader.ReadLineAsync();
		if (await Task.WhenAny(new Task[2]
		{
			read,
			Task.Delay(5000, token)
		}).ConfigureAwait(continueOnCapturedContext: false) != read)
		{
			token.ThrowIfCancellationRequested();
			throw new TimeoutException("播放器组件心跳超时");
		}
		string text = await read.ConfigureAwait(continueOnCapturedContext: false);
		if (text != null && text.Length > 4194304)
		{
			throw new InvalidDataException();
		}
		return text;
	}

	private static string Text(JToken token, string key, int max)
	{
		JToken val = token[(object)key];
		string text = ((val != null && (int)val.Type == 8) ? ((string)token[(object)key]) : "");
		if (text.Length > max)
		{
			return "";
		}
		return text;
	}

	public void Dispose()
	{
		lifetime.Cancel();
		StopChild();
	}
}
