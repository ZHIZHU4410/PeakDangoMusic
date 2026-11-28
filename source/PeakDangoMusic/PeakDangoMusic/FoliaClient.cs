using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using DangoMusic.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class FoliaClient : IDisposable
{
	internal sealed class Request
	{
		internal readonly MusicState State;

		internal readonly string Mode;

		internal readonly string Primary;

		internal readonly string Accent;

		internal readonly string Intensity;

		internal readonly string FontPath;

		internal readonly bool Enabled;

		internal readonly bool Translation;

		internal readonly bool Split;

		internal readonly bool Background;

		internal readonly bool Software;

		internal readonly int Offset;

		internal readonly int Quality;

		internal readonly int Fps;

		internal readonly float FontScale;

		internal int Revision;

		internal Request(MusicState state, MusicSettings settings, AdaptiveLyrics.Palette palette)
		{
			FontPath = settings.FontPath.Value;
			State = state;
			Mode = FoliaStyles.Resolve(settings.Style.Value, state.Key);
			Enabled = settings.Enabled.Value;
			Translation = settings.Translation.Value;
			Split = settings.SplitLines.Value;
			Background = settings.FoliaBackground.Value;
			Software = settings.FoliaSoftware.Value;
			Offset = settings.Offset.Value;
			Quality = settings.FoliaQuality.Value;
			Fps = settings.FoliaFps.Value;
			FontScale = settings.FoliaFontScale.Value;
			Intensity = settings.FoliaIntensity.Value switch
			{
				0 => "calm",
				2 => "chaotic",
				_ => "normal",
			};
			bool customColor = AdaptiveLyrics.LyricTint(out var face);
			if (!customColor)
				face = palette.Face;
			Primary = Background && !customColor ? "#F7E1D7" : "#" + ColorUtility.ToHtmlStringRGB(face);
			Color accentColor = new Color(face.r * 0.72f, face.g * 0.72f, face.b * 0.72f, 1f);
			Accent = "#" + ColorUtility.ToHtmlStringRGB(accentColor);
			if (!Background && !customColor && palette.IsDark)
			{
				Accent = "#234D59";
			}
		}
	}

	internal sealed class Frame
	{
		internal readonly int Width;

		internal readonly int Height;

		internal readonly byte[] Pixels;

		internal Frame(int width, int height, byte[] pixels)
		{
			Width = width;
			Height = height;
			Pixels = pixels;
		}
	}

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private readonly object gate = new object();

	private readonly ConcurrentQueue<byte[]> buffers = new ConcurrentQueue<byte[]>();

	private readonly Task worker;

	private volatile Request? latest;

	private Frame? frame;

	private volatile string status = "Folia 样式尚未启用";

	private volatile bool running;

	private volatile bool ready;

	private int epoch;

	private int requestedRevision;

	private volatile bool failed;

	private long lastFrameTick;

	private string lastDiagnostic = "";

	private Process? child;

	internal string Status => status;

	internal bool Running => running;

	internal bool Ready
	{
		get
		{
			if (running && ready && !failed)
			{
				return MusicClock.Now - Interlocked.Read(ref lastFrameTick) < 5000;
			}
			return false;
		}
	}

	internal Frame? TakeFrame()
	{
		return Interlocked.Exchange(ref frame, null);
	}

	internal FoliaClient()
	{
		worker = Task.Run((Func<Task?>)Run);
	}

	internal void Set(Request? request)
	{
		Request request2 = latest;
		if (request != null)
		{
			if (request2 == null || request2.Mode != request.Mode || request2.FontPath != request.FontPath || request2.State.Key != request.State.Key || request2.State.LyricGeneration != request.State.LyricGeneration || request2.Split != request.Split)
			{
				Interlocked.Increment(ref requestedRevision);
				ready = false;
				failed = false;
				status = "正在准备所选Folia样式…";
			}
			request.Revision = Volatile.Read(ref requestedRevision);
		}
		latest = request;
	}

	internal void Restart()
	{
		Interlocked.Increment(ref epoch);
		ready = false;
		failed = false;
		status = "正在重新准备Folia样式…";
	}

	internal void ReturnFrame(Frame value)
	{
		if (buffers.Count < 4)
		{
			buffers.Enqueue(value.Pixels);
		}
	}

	private byte[] Rent(int length)
	{
		byte[] result;
		while (buffers.TryDequeue(out result))
		{
			if (result.Length == length)
			{
				return result;
			}
		}
		return new byte[length];
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
		running = false;
	}

	private async Task Run()
	{
		_ = 8;
		try
		{
			while (!lifetime.IsCancellationRequested)
			{
				Request first = latest;
				if (first == null)
				{
					await Task.Delay(300, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				string text = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "PeakDangoMusic", "Folia");
				string text2 = Path.Combine(text, "Runtime", "PeakFoliaRenderer.exe");
				string text3 = Path.Combine(text, "App");
				if (!File.Exists(text2) || !File.Exists(Path.Combine(text3, "dist", "index.html")))
				{
					status = "缺少Folia样式组件，请安装完整的1.2.2或更新安装包；暂用Dango显示";
					await Task.Delay(2500, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
					continue;
				}
				Process process = null;
				string stage = "校验组件协议";
				try
				{
					if (((JToken)JObject.Parse(File.ReadAllText(Path.Combine(text3, "package.json")))).Value<int?>((object)"peakProtocol") != 3)
					{
						status = "Folia组件与DLL版本不匹配，请一起更新完整1.2.2安装包；暂用Dango显示";
						await Task.Delay(2500, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
						continue;
					}
					string text4 = "PeakFolia-" + Guid.NewGuid().ToString("N");
					stage = "创建命名管道";
					using NamedPipeServerStream pipe = new NamedPipeServerStream(text4, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
					string text5 = Path.Combine(Paths.CachePath, "PeakDangoMusicFolia", Process.GetCurrentProcess().Id.ToString());
					Directory.CreateDirectory(text5);
					process = new Process
					{
						StartInfo = new ProcessStartInfo(text2, "\"" + text3 + "\" --parent=" + Process.GetCurrentProcess().Id + " --profile=\"" + text5 + "\" --pipe=" + text4 + " --software=" + (first.Software ? "1" : "0"))
						{
							UseShellExecute = false,
							CreateNoWindow = true,
							WindowStyle = ProcessWindowStyle.Hidden,
							RedirectStandardOutput = true,
							RedirectStandardError = true,
							StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
							WorkingDirectory = text
						}
					};
					int sessionEpoch = Volatile.Read(ref epoch);
					process.StartInfo.EnvironmentVariables.Remove("ELECTRON_RUN_AS_NODE");
					stage = "启动渲染进程";
					lock (gate)
					{
						if (latest == null)
						{
							continue;
						}
						status = "正在准备Folia样式…";
						ready = false;
						failed = false;
						lastDiagnostic = "";
						process.Start();
						child = process;
						running = true;
						goto IL_0495;
					}
					IL_0495:
					using (CancellationTokenSource session = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[1] { lifetime.Token }))
					{
						Task diagnostics = Diagnostics(process.StandardError, session.Token);
						process.StandardOutput.ReadToEndAsync();
						using (CancellationTokenSource connection = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[1] { session.Token }))
						{
							connection.CancelAfter(10000);
							stage = "等待渲染进程连接管道";
							try
							{
								await pipe.WaitForConnectionAsync(connection.Token).ConfigureAwait(continueOnCapturedContext: false);
							}
							catch (OperationCanceledException) when (!lifetime.IsCancellationRequested)
							{
								throw new IOException("Folia专用管道连接超时");
							}
						}
						using StreamWriter writer = new StreamWriter(pipe, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), 8192, leaveOpen: true);
						Task send = Send(writer, first.Software, sessionEpoch, session.Token);
						Task watchdog = Watchdog(process, session.Token);
						stage = "接收与解析渲染画面";
						try
						{
							await Receive(pipe, session.Token).ConfigureAwait(continueOnCapturedContext: false);
						}
						finally
						{
							session.Cancel();
							StopChild();
							try
							{
								await send.ConfigureAwait(continueOnCapturedContext: false);
							}
							catch (OperationCanceledException)
							{
							}
							catch (IOException)
							{
							}
							catch (InvalidOperationException)
							{
							}
							try
							{
								await diagnostics.ConfigureAwait(continueOnCapturedContext: false);
							}
							catch (OperationCanceledException)
							{
							}
							try
							{
								await watchdog.ConfigureAwait(continueOnCapturedContext: false);
							}
							catch (OperationCanceledException)
							{
							}
						}
					}
					goto IL_0a65;
				}
				catch (Exception ex7) when ((ex7 is IOException || ex7 is InvalidOperationException || ex7 is Win32Exception || ex7 is ArgumentException || ex7 is NotSupportedException || ex7 is JsonException) ? true : false)
				{
					string text6 = DiagnosticLog.Report("Folia", stage, ex7, "FoliaClient.cs", 127);
					status = ((lastDiagnostic.Length > 0) ? lastDiagnostic : text6) + "\n已回退Dango，正在重试";
					goto IL_0a65;
				}
				finally
				{
					running = false;
					ready = false;
					lock (gate)
					{
						if (child == process)
						{
							StopChild();
							child = null;
						}
					}
					process?.Dispose();
					Frame frame = Interlocked.Exchange(ref this.frame, null);
					if (frame != null)
					{
						ReturnFrame(frame);
					}
				}
				IL_0a65:
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

	private async Task Send(StreamWriter writer, bool software, int sessionEpoch, CancellationToken token)
	{
		string previousKey = "";
		int previousGeneration = -1;
		bool previousSplit = false;
		LyricsTimeline previousTimeline = null;
		LyricLineView view = new LyricLineView();
		JsonSerializerSettings json = new JsonSerializerSettings
		{
			NullValueHandling = (NullValueHandling)1
		};
		try
		{
			while (!token.IsCancellationRequested)
			{
				Request request = latest;
				if (request == null || request.Software != software || sessionEpoch != Volatile.Read(ref epoch))
				{
					break;
				}
				MusicState state = request.State;
				long? num = state.PositionAt(MusicClock.Now);
				object[] lines = ((previousKey != state.Key || previousGeneration != state.LyricGeneration || previousSplit != request.Split || previousTimeline != state.Timeline) ? ToLines(view.Get(state.Timeline, request.Split), state.DurationMs) : null);
				var anon = new
				{
					fontPath = request.FontPath,
					revision = request.Revision,
					mode = request.Mode,
					key = state.Key,
					title = state.Title,
					artist = state.Artist,
					positionMs = Math.Clamp((num ?? state.PositionMs) + request.Offset, 0L, Math.Max(0L, state.DurationMs)),
					durationMs = state.DurationMs,
					playing = (num.HasValue && state.Playing),
					visible = (request.Enabled && num.HasValue && state.Timeline.Lines.Count > 0),
					generation = state.LyricGeneration,
					lines = lines,
					translation = request.Translation,
					fontScale = request.FontScale,
					intensity = request.Intensity,
					primary = request.Primary,
					accent = request.Accent,
					background = request.Background,
					quality = request.Quality,
					fps = request.Fps
				};
				await writer.WriteLineAsync(JsonConvert.SerializeObject((object)anon, json)).ConfigureAwait(continueOnCapturedContext: false);
				await writer.FlushAsync().ConfigureAwait(continueOnCapturedContext: false);
				previousKey = state.Key;
				previousGeneration = state.LyricGeneration;
				previousSplit = request.Split;
				previousTimeline = state.Timeline;
				await Task.Delay(100, token).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is InvalidOperationException || ex is JsonException || ex is ArgumentException) ? true : false)
		{
			lastDiagnostic = DiagnosticLog.Report("Folia", "发送歌词命名管道", ex, "FoliaClient.cs", 159);
			failed = true;
			ready = false;
			throw;
		}
		finally
		{
			StopChild();
		}
	}

	private static object[] ToLines(LyricsTimeline timeline, long duration)
	{
		return timeline.Lines.Take(12000).Select((Func<LyricsLine, int, object>)((LyricsLine line, int index) =>
		{
			long val = ((index + 1 < timeline.Lines.Count) ? timeline.Lines[index + 1].StartMs : Math.Max(line.StartMs + Math.Max(5000L, line.DurationMs), duration));
			val = Math.Max(line.StartMs + 1, val);
			IReadOnlyList<LyricsWord> words = line.Words;
			IReadOnlyList<LyricsWord> source = ((words != null && words.Count > 0) ? line.Words : EstimateWords(line, val));
			return new
			{
				fullText = line.Text,
				startTime = (double)line.StartMs / 1000.0,
				endTime = (double)val / 1000.0,
				translation = line.Translation,
				words = source.Select((LyricsWord word) => new
				{
					text = word.Text,
					startTime = (double)word.StartMs / 1000.0,
					endTime = (double)(word.StartMs + Math.Max(1L, word.DurationMs)) / 1000.0
				}).ToArray()
			};
		})).ToArray();
	}

	private static IReadOnlyList<LyricsWord> EstimateWords(LyricsLine line, long end)
	{
		List<string> list = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		TextElementEnumerator textElementEnumerator = StringInfo.GetTextElementEnumerator(line.Text);
		while (textElementEnumerator.MoveNext())
		{
			string text = (string)textElementEnumerator.Current;
			if (char.IsWhiteSpace(text, 0))
			{
				stringBuilder.Append(text);
				if (stringBuilder.Length > 0)
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Clear();
				}
			}
			else if (text[0] >= '⺀')
			{
				if (stringBuilder.Length > 0)
				{
					list.Add(stringBuilder.ToString());
					stringBuilder.Clear();
				}
				list.Add(text);
			}
			else
			{
				stringBuilder.Append(text);
			}
		}
		if (stringBuilder.Length > 0)
		{
			list.Add(stringBuilder.ToString());
		}
		int num = Math.Max(1, list.Sum((string part) => Math.Max(1, new StringInfo(part).LengthInTextElements)));
		int num2 = 0;
		List<LyricsWord> list2 = new List<LyricsWord>();
		long num3 = Math.Max(1L, end - line.StartMs);
		foreach (string item in list)
		{
			int num4 = Math.Max(1, new StringInfo(item).LengthInTextElements);
			long num5 = line.StartMs + num3 * num2 / num;
			num2 += num4;
			list2.Add(new LyricsWord(num5, Math.Max(1L, line.StartMs + num3 * num2 / num - num5), item));
		}
		return list2;
	}

	private async Task Receive(Stream stream, CancellationToken token)
	{
		byte[] magic = Encoding.ASCII.GetBytes("DANGOF02");
		byte[] header = new byte[24];
		byte[] row = Array.Empty<byte>();
		while (!token.IsCancellationRequested)
		{
			await ReadExactly(stream, header, 0, 8, token).ConfigureAwait(continueOnCapturedContext: false);
			int skipped = 0;
			byte[] one = new byte[1];
			while (!header.Take(8).SequenceEqual(magic))
			{
				int num = skipped + 1;
				skipped = num;
				if (num > 65536)
				{
					throw new InvalidDataException("Folia帧头无效");
				}
				await ReadExactly(stream, one, token).ConfigureAwait(continueOnCapturedContext: false);
				Buffer.BlockCopy(header, 1, header, 0, 7);
				header[7] = one[0];
			}
			await ReadExactly(stream, header, 8, 16, token).ConfigureAwait(continueOnCapturedContext: false);
			int width = BitConverter.ToInt32(header, 8);
			int height = BitConverter.ToInt32(header, 12);
			int length = BitConverter.ToInt32(header, 16);
			int revision = BitConverter.ToInt32(header, 20);
			if (width < 1 || width > 1920 || height < 1 || height > 1080 || length != width * height * 4)
			{
				throw new InvalidDataException("Folia帧尺寸无效");
			}
			byte[] pixels = Rent(length);
			bool published = false;
			try
			{
				await ReadExactly(stream, pixels, token).ConfigureAwait(continueOnCapturedContext: false);
				int num2 = width * 4;
				if (row.Length != num2)
				{
					row = new byte[num2];
				}
				for (int i = 0; i < height / 2; i++)
				{
					int num3 = i * num2;
					int num4 = (height - 1 - i) * num2;
					Buffer.BlockCopy(pixels, num3, row, 0, num2);
					Buffer.BlockCopy(pixels, num4, pixels, num3, num2);
					Buffer.BlockCopy(row, 0, pixels, num4, num2);
				}
				bool flag = false;
				for (int j = 0; j < length; j += 4)
				{
					int num5 = pixels[j + 3];
					flag |= num5 > 0;
					if ((num5 != 0 && num5 != 255) || 1 == 0)
					{
						pixels[j] = (byte)Math.Min(255, (pixels[j] * 255 + num5 / 2) / num5);
						pixels[j + 1] = (byte)Math.Min(255, (pixels[j + 1] * 255 + num5 / 2) / num5);
						pixels[j + 2] = (byte)Math.Min(255, (pixels[j + 2] * 255 + num5 / 2) / num5);
					}
				}
				Interlocked.Exchange(ref lastFrameTick, MusicClock.Now);
				if (revision == Volatile.Read(ref requestedRevision) && !failed)
				{
					Frame frame = Interlocked.Exchange(ref this.frame, new Frame(width, height, pixels));
					if (frame != null)
					{
						ReturnFrame(frame);
					}
					published = true;
					ready |= flag;
					if (ready)
					{
						status = "Folia原生样式已就绪";
					}
				}
			}
			finally
			{
				if (!published && buffers.Count < 4)
				{
					buffers.Enqueue(pixels);
				}
			}
		}
	}

	private static async Task ReadExactly(Stream stream, byte[] data, CancellationToken token)
	{
		int num;
		for (int offset = 0; offset < data.Length; offset += num)
		{
			num = await stream.ReadAsync(data, offset, data.Length - offset, token).ConfigureAwait(continueOnCapturedContext: false);
			if (num == 0)
			{
				throw new EndOfStreamException();
			}
		}
	}

	private static async Task ReadExactly(Stream stream, byte[] data, int offset, int length, CancellationToken token)
	{
		int end = offset + length;
		while (offset < end)
		{
			int num = await stream.ReadAsync(data, offset, end - offset, token).ConfigureAwait(continueOnCapturedContext: false);
			if (num == 0)
			{
				throw new EndOfStreamException();
			}
			offset += num;
		}
	}

	private async Task Diagnostics(StreamReader reader, CancellationToken token)
	{
		try
		{
			while (!token.IsCancellationRequested)
			{
				string text = await reader.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false);
				if (text == null)
				{
					break;
				}
				if (text.StartsWith("PEAK_FOLIA_ERROR ", StringComparison.Ordinal))
				{
					lastDiagnostic = DiagnosticLog.Report("Folia前端", "渲染进程报告", new InvalidOperationException(text.Substring(17, Math.Min(500, text.Length - 17))), "FoliaClient.cs", 229);
					failed = true;
					ready = false;
					status = "Folia失败，暂用Dango：" + lastDiagnostic;
				}
			}
		}
		catch (IOException)
		{
		}
		catch (ObjectDisposedException)
		{
		}
	}

	private async Task Watchdog(Process process, CancellationToken token)
	{
		long start = MusicClock.Now;
		while (!token.IsCancellationRequested)
		{
			await Task.Delay(1000, token).ConfigureAwait(continueOnCapturedContext: false);
			if (latest == null)
			{
				break;
			}
			if (MusicClock.Now - start > 30000 && !ready)
			{
				if (lastDiagnostic.Length == 0)
				{
					lastDiagnostic = DiagnosticLog.Report("Folia", "等待首个有效画面", new TimeoutException("30秒未收到有效画面；详见 BepInEx/PeakDangoMusic-Folia.log"), "FoliaClient.cs", 242);
				}
				StopChild();
				break;
			}
		}
	}

	public void Dispose()
	{
		latest = null;
		lifetime.Cancel();
		Task.Run((Action)StopChild);
	}
}
