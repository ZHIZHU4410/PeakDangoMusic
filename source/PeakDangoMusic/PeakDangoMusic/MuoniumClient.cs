using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed class MuoniumClient : IDisposable
{
	private readonly SemaphoreSlim gate = new SemaphoreSlim(1, 1);

	private readonly CancellationTokenSource lifetime = new CancellationTokenSource();

	private Process? child;

	private int serial;

	private bool restored;

	internal Task<JObject> Call(string operation, string source, JObject? fields = null)
	{
		return Task.Run<JObject>(async () =>
		{
			await gate.WaitAsync(lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				EnsureStarted();
				if (!restored)
				{
					JObject val;
					try
					{
						val = AccountVault.Load();
					}
					catch (Exception error)
					{
						val = new JObject();
						DiagnosticLog.Report("音乐账号", "读取Windows加密登录态", error, "MuoniumClient.cs", 25);
					}
					await Exchange(new JObject
					{
						["op"] = (JToken)("restore"),
						["credentials"] = (JToken)(object)val
					}).ConfigureAwait(continueOnCapturedContext: false);
					restored = true;
				}
				JObject val2 = fields ?? new JObject();
				if(source == "lx-native"){
                    val2["scriptPath"] = Plugin.Instance.Settings.LxScriptPath.Value;
                    val2["channel"] = Plugin.Instance.Settings.LxChannel.Value;
                }
                val2["op"] = (JToken)(operation);
				val2["source"] = (JToken)(source);
				return await Exchange(val2).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch
			{
				Stop();
				throw;
			}
			finally
			{
				gate.Release();
			}
		});
	}

	private void EnsureStarted()
	{
		if (child != null && !child.HasExited)
		{
			return;
		}
		string text = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "PeakDangoMusic", "Muonium");
		string text2 = Path.Combine(text, "Runtime", "bin", "java.exe");
		if (!File.Exists(text2) || !File.Exists(Path.Combine(text, "PeakMuoniumBridge.jar")))
		{
			throw new FileNotFoundException("缺少Muonium登录/曲库组件，请完整安装新版");
		}
        // Host-owned config values: reject quotes/control characters before constructing JVM argv.
        string SafeOption(string value) => value.IndexOfAny(new[] { '\"', '\r', '\n', '\0' }) >= 0 ? "" : value;
        string ffmpeg = SafeOption(Plugin.Instance.Settings.FfmpegPath.Value);
        string proxy = SafeOption(Plugin.Instance.Settings.YouTubeProxy.Value);
        string arguments = "-Djava.awt.headless=true -Xms8m -Xmx64m -Xss512k -XX:+UseSerialGC -XX:MaxMetaspaceSize=96m -XX:ReservedCodeCacheSize=32m -XX:MaxDirectMemorySize=16m -XX:-UsePerfData -Dpeak.audioCache=\"" + OwnedAudioCache.Root + "\" -Dpeak.ffmpeg=\"" + ffmpeg + "\" -Dpeak.youtubeProxy=\"" + proxy + "\" -cp \"" + text + "\\*\" PeakMuoniumBridge " + Process.GetCurrentProcess().Id;
		child = new Process
		{
			StartInfo = new ProcessStartInfo(text2, arguments)
			{
				WorkingDirectory = text,
				UseShellExecute = false,
				CreateNoWindow = true,
				WindowStyle = ProcessWindowStyle.Hidden,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
				StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
			}
		};
		child.Start();
		StreamReader errors = child.StandardError;
		Task.Run(async () =>
		{
			try
			{
				while (await errors.ReadLineAsync().ConfigureAwait(continueOnCapturedContext: false) != null)
				{
				}
			}
			catch (IOException)
			{
			}
			catch (ObjectDisposedException)
			{
			}
		});
		restored = false;
	}

	private async Task<JObject> Exchange(JObject request)
	{
		int id = ++serial;
		request["id"] = (JToken)(id);
		await child.StandardInput.WriteLineAsync(((JToken)request).ToString((Formatting)0, Array.Empty<JsonConverter>())).ConfigureAwait(continueOnCapturedContext: false);
		await child.StandardInput.FlushAsync().ConfigureAwait(continueOnCapturedContext: false);
		string text = await ReadBounded(child.StandardOutput, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
		string noise = "";
		for (int guard = 0; guard < 32; guard++)
		{
			if (text.Length <= 0)
			{
				break;
			}
			if (text[0] == '{')
			{
				break;
			}
			noise = noise + text + " ";
			try
			{
				text = await ReadBounded(child.StandardOutput, lifetime.Token).ConfigureAwait(continueOnCapturedContext: false);
			}
			catch (IOException)
			{
				throw new InvalidOperationException(Describe(noise));
			}
		}
		if (text.Length == 0 || text[0] != '{')
		{
			throw new InvalidDataException(Describe(noise));
		}
		JsonTextReader val = new JsonTextReader((TextReader)new StringReader(text))
		{
			MaxDepth = 32,
			DateParseHandling = (DateParseHandling)0
		};
		try
		{
			JObject val2 = JObject.Load((JsonReader)(object)val);
			if ((int?)val2["id"] != id)
			{
				throw new InvalidDataException("Muonium组件响应序号错误");
			}
			if ((bool?)val2["ok"] != true)
			{
                string detail = (string?)val2["errorCode"] switch {
                    "FFmpeg required" => "缺少 FFmpeg，请在连接与诊断填写本机 ffmpeg.exe 路径",
                    "audio unavailable" => "平台未返回可用音频，请检查账号权限或更换曲目",
                    "audio decode timeout" => "音频转码超时，请检查网络后重试",
                    "audio decode failed" => "音频下载或转码失败，请检查网络与本机 FFmpeg",
                    _ => ((string)val2["error"]) ?? "平台操作失败"
                };
				throw new InvalidOperationException(detail);
			}
			JToken val3 = val2["data"];
			JObject val4 = (JObject)(((object)((val3 is JObject) ? val3 : null)) ?? ((object)new JObject()));
			JToken val5 = val4["credentials"];
			JObject val6 = (JObject)(object)((val5 is JObject) ? val5 : null);
			if (val6 != null)
			{
				try
				{
					AccountVault.Save(val6);
				}
				catch (Exception error)
				{
					DiagnosticLog.Report("音乐账号", "保存Windows加密登录态", error, "MuoniumClient.cs", 74);
					val4["vaultWarning"] = (JToken)("登录已生效，但保存失败；重启后需要重新登录");
				}
				val4.Remove("credentials");
			}
			return val4;
		}
		finally
		{
			((IDisposable)val)?.Dispose();
		}
	}

	private static async Task<string> ReadBounded(StreamReader reader, CancellationToken ct)
	{
		Task<string> read = Task.Run(async () =>
		{
			char[] buffer = new char[4096];
			StringBuilder text = new StringBuilder();
			while (true)
			{
				if (await reader.ReadAsync(buffer, 0, 1).ConfigureAwait(continueOnCapturedContext: false) == 0)
				{
					throw new IOException("Muonium组件已退出");
				}
				if (buffer[0] == '\n')
				{
					return text.ToString();
				}
				if (text.Length >= 4194304)
				{
					break;
				}
				text.Append(buffer[0]);
			}
			throw new InvalidDataException("Muonium响应过大");
		});
		if (await Task.WhenAny(new Task[2]
		{
			read,
			Task.Delay(120000, ct)
		}).ConfigureAwait(continueOnCapturedContext: false) != read)
		{
			ct.ThrowIfCancellationRequested();
			throw new TimeoutException("Muonium平台请求超时，请重试");
		}
		return await read.ConfigureAwait(continueOnCapturedContext: false);
	}

	private static string Describe(string noise)
	{
		string text = noise.Replace("\n", " ").Trim();
		if (text.Length > 180)
		{
			text = text.Substring(0, 180) + "…";
		}
		return (text.Contains("insufficient memory") ? "Muonium组件内存不足已退出：关闭其他占用内存的程序后重试，或重启游戏" : "Muonium组件异常退出") + ((text.Length > 0) ? ("（" + text + "）") : "");
	}

	private void Stop()
	{
		Process process = child;
		child = null;
		restored = false;
		if (process == null)
		{
			return;
		}
		try
		{
			if (!process.HasExited)
			{
				process.Kill();
			}
		}
		catch (InvalidOperationException)
		{
		}
		finally
		{
			process.Dispose();
		}
	}

	public void Dispose()
	{
		lifetime.Cancel();
		Stop();
	}
}
