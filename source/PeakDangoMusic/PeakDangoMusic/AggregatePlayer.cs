using System;
using System.Collections;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DangoMusic.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace PeakDangoMusic;

internal sealed class AggregatePlayer : IDisposable
{
	private readonly Plugin owner;

	private readonly GdMusicApi api = new GdMusicApi();

	private readonly AudioSource audio;

	private CancellationTokenSource cancel = new CancellationTokenSource();

	private Task<GdMusicApi.Track[]>? search;

	private Task<GdMusicApi.Resolved>? resolve;

	private Coroutine? loading;

	private UnityWebRequest? request;

	private GdMusicApi.Track? current;

	private LyricsTimeline timeline = LyricsTimeline.Empty;

	private int generation;

	private bool active;

	private bool paused;

	private bool endReported;

	internal GdMusicApi.Track[] Results { get; private set; } = Array.Empty<GdMusicApi.Track>();

	internal string Status { get; private set; } = "输入歌名或歌手搜索";

	internal bool Busy
	{
		get
		{
			if (search == null && resolve == null)
			{
				return loading != null;
			}
			return true;
		}
	}

	internal MusicState Snapshot => new MusicState(active, (current == null) ? "" : ("gd/" + current.Source + "/" + current.Id), current?.Title ?? "", current?.Artist ?? "", ((Object)(object)audio.clip != (Object)null) ? ((long)(audio.time * 1000f)) : 0, ((Object)(object)audio.clip != (Object)null) ? ((long)(audio.clip.length * 1000f)) : 0, audio.isPlaying, MusicClock.Now, 1.0, timeline, generation, Status);

	internal event Action<GdMusicApi.Track>? TrackStarted;

	internal event Action? TrackEnded;

	internal void PlayResolved(GdMusicApi.Resolved value)
	{
		if (active && !Busy)
		{
			loading = ((MonoBehaviour)owner).StartCoroutine(Load(value));
		}
	}

	internal AggregatePlayer(Plugin plugin)
	{
		owner = plugin;
		audio = ((Component)plugin).gameObject.AddComponent<AudioSource>();
		audio.playOnAwake = false;
		audio.spatialBlend = 0f;
		audio.ignoreListenerPause = true;
	}

	internal void Tick(bool enabled, float volume)
	{
		if (active != enabled)
		{
			active = enabled;
			if (!enabled)
			{
				Stop();
			}
		}
		audio.volume = volume;
		Task<GdMusicApi.Track[]>? task = search;
		if (task != null && task.IsCompleted)
		{
			Task<GdMusicApi.Track[]> task2 = search;
			search = null;
			try
			{
				Results = task2.GetAwaiter().GetResult();
				Status = ((Results.Length == 0) ? "该平台没有搜索结果，可更换平台" : $"找到 {Results.Length} 首，点击播放");
			}
			catch (Exception error)
			{
				Status = DiagnosticLog.Report("GD聚合音乐", "搜索歌曲", error, "AggregatePlayer.cs", 50);
			}
		}
		Task<GdMusicApi.Resolved>? task3 = resolve;
		if (task3 != null && task3.IsCompleted)
		{
			Task<GdMusicApi.Resolved> task4 = resolve;
			resolve = null;
			try
			{
				loading = ((MonoBehaviour)owner).StartCoroutine(Load(task4.GetAwaiter().GetResult()));
			}
			catch (Exception error2)
			{
				Status = DiagnosticLog.Report("GD聚合音乐", "解析播放地址", error2, "AggregatePlayer.cs", 56);
			}
		}
		if (current != null && (Object)(object)audio.clip != (Object)null && !audio.isPlaying && !paused && !Busy && !endReported)
		{
			endReported = true;
			Status = "播放结束，可选择下一首";
			TrackEnded?.Invoke();
		}
	}

	internal void Search(string platform, string keyword, int page)
	{
		if (!Busy && active && !string.IsNullOrWhiteSpace(keyword))
		{
			Results = Array.Empty<GdMusicApi.Track>();
			Status = "正在搜索…";
			CancellationToken token = cancel.Token;
			search = Task.Run(() => api.Search(platform, keyword, page, token));
		}
	}

	internal void Play(GdMusicApi.Track track, int bitrate)
	{
		if (!Busy && active)
		{
			Status = "正在解析播放地址…";
			CancellationToken token = cancel.Token;
			resolve = Task.Run(() => api.Resolve(track, bitrate, token));
		}
	}

	private IEnumerator Load(GdMusicApi.Resolved result)
	{
		yield return null;
		Status = "正在加载音频…";
		string text = result.Format.ToLowerInvariant();
		if (text.Length == 0)
		{
			text = Path.GetExtension(new Uri(result.Url).AbsolutePath).TrimStart('.').ToLowerInvariant();
		}
		AudioType val;
		switch (text)
		{
		case "ogg":
			val = (AudioType)14;
			break;
		case "wav":
			val = (AudioType)20;
			break;
		case "aiff":
		case "aif":
			val = (AudioType)2;
			break;
		default:
			val = (AudioType)13;
			break;
		}
		AudioType val2 = val;
		bool flag;
		switch (text)
		{
		case "flac":
		case "m4a":
		case "aac":
			flag = true;
			break;
		default:
			flag = false;
			break;
		}
		if (flag)
		{
			Status = "当前播放地址为 " + text + "，Unity音频后端不支持；请选择128或320 kbps重试";
			loading = null;
			yield break;
		}
		Uri uri = new Uri(result.Url);
		string text2 = ((uri.Scheme == "http") ? new UriBuilder(uri)
		{
			Scheme = "https",
			Port = (uri.IsDefaultPort ? (-1) : uri.Port)
		}.Uri.AbsoluteUri : result.Url);
		UnityWebRequest download = UnityWebRequestMultimedia.GetAudioClip(text2, val2);
		try
		{
			request = download;
			download.timeout = 30;
			((DownloadHandlerAudioClip)download.downloadHandler).streamAudio = true;
			UnityWebRequestAsyncOperation val3 = null;
			try
			{
				val3 = download.SendWebRequest();
			}
			catch (Exception error)
			{
				Status = DiagnosticLog.Report("Muonium音乐", "发起音频请求", error, "AggregatePlayer.cs", 93);
				request = null;
				loading = null;
			}
			if (val3 == null)
			{
				yield break;
			}
			yield return val3;
			request = null;
			if ((int)download.result != 1)
			{
				Status = DiagnosticLog.Report("GD聚合音乐", "下载音频", new IOException(download.error), "AggregatePlayer.cs", 97);
			}
			else
			{
				AudioClip val4 = null;
				try
				{
					val4 = DownloadHandlerAudioClip.GetContent(download);
				}
				catch (Exception error2)
				{
					Status = DiagnosticLog.Report("GD聚合音乐", "解码音频", error2, "AggregatePlayer.cs", 102);
				}
				if ((Object)(object)val4 != (Object)null)
				{
					ReleaseClip();
					audio.clip = val4;
					current = result.Track;
					timeline = result.Lyrics;
					generation++;
					paused = false;
					endReported = false;
					audio.Play();
					TrackStarted?.Invoke(current);
					Status = ((timeline.Lines.Count > 0) ? "正在播放 · GD同步歌词" : "正在播放 · 该歌曲未提供同步歌词");
					if (result.Note.Length > 0)
					{
						Status = Status + "\n" + result.Note;
					}
				}
			}
		}
		finally
		{
			((IDisposable)download)?.Dispose();
		}
		loading = null;
	}

	internal void TogglePause()
	{
		if (!((Object)(object)audio.clip == (Object)null))
		{
			if (paused)
			{
				audio.UnPause();
				paused = false;
				Status = "正在播放";
			}
			else if (!audio.isPlaying)
			{
				audio.time = 0f;
				audio.Play();
				endReported = false;
				Status = "正在播放";
			}
			else
			{
				audio.Pause();
				paused = true;
				Status = "已暂停";
			}
		}
	}

	internal void Seek(float fraction)
	{
		if ((Object)(object)audio.clip != (Object)null)
		{
			audio.time = Mathf.Clamp(fraction, 0f, 0.999f) * audio.clip.length;
		}
	}

	private void ReleaseClip()
	{
		audio.Stop();
		AudioClip clip = audio.clip;
		audio.clip = null;
		if ((Object)(object)clip != (Object)null)
		{
			Object.Destroy((Object)(object)clip);
		}
	}

	internal void Stop()
	{
		cancel.Cancel();
		cancel.Dispose();
		cancel = new CancellationTokenSource();
		if (search != null)
		{
			search.ContinueWith((Task<GdMusicApi.Track[]> t) =>
			{
				_ = t.Exception;
			}, TaskContinuationOptions.OnlyOnFaulted);
		}
		if (resolve != null)
		{
			resolve.ContinueWith((Task<GdMusicApi.Resolved> t) =>
			{
				_ = t.Exception;
			}, TaskContinuationOptions.OnlyOnFaulted);
		}
		search = null;
		resolve = null;
		UnityWebRequest val = request;
		request = null;
		try
		{
			if (val != null)
			{
				val.Abort();
			}
		}
		catch (NullReferenceException)
		{
		}
		catch (ObjectDisposedException)
		{
		}
		if (loading != null)
		{
			((MonoBehaviour)owner).StopCoroutine(loading);
			loading = null;
		}
		ReleaseClip();
		current = null;
		timeline = LyricsTimeline.Empty;
		paused = false;
		generation++;
		Status = "已停止";
	}

	public void Dispose()
	{
		Stop();
		cancel.Dispose();
		api.Dispose();
		Object.Destroy((Object)(object)audio);
	}
}
