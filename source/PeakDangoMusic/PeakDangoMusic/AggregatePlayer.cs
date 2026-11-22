using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

	private readonly AudioLowPassFilter? lowPass;

	private readonly AudioEchoFilter? echo;

	private CancellationTokenSource cancel = new CancellationTokenSource();

	private Task<GdMusicApi.Track[]>? search;

    private sealed class AudioLoad
    {
        internal readonly string Key;
        internal readonly GdMusicApi.Track Track;
        internal readonly Task<GdMusicApi.Resolved> Resolve;
        internal UnityWebRequest? Request;
        internal Coroutine? Download;
        internal AudioLoad(string key, GdMusicApi.Track track, Task<GdMusicApi.Resolved> resolve)
        {
            Key = key;
            Track = track;
            Resolve = resolve;
        }
    }

    private sealed class CachedAudio
    {
        internal readonly AudioClip Clip;
        internal readonly GdMusicApi.Resolved Result;
        internal readonly long BudgetBytes;
        internal float LastUse;
        internal CachedAudio(AudioClip clip, GdMusicApi.Resolved result, ulong downloadedBytes)
        {
            Clip = clip;
            Result = result;
            // MP3/OGG are loaded compressed. WAV/AIFF can occupy the decoded PCM size.
            long pcmBytes = (long)clip.samples * clip.channels * sizeof(float);
            BudgetBytes = clip.loadType == AudioClipLoadType.CompressedInMemory && downloadedBytes > 0
                ? (long)downloadedBytes + 2 * 1024 * 1024
                : pcmBytes;
            LastUse = Time.unscaledTime;
        }
    }

    private const int MaxCachedSongs = 12;
    private const long MaxCachedBytes = 160L * 1024 * 1024;
    private readonly Dictionary<string, CachedAudio> cached = new();
    private readonly Dictionary<string, float> retryAfter = new();
    private readonly List<AudioLoad> loads = new();
    private readonly List<string> upcomingKeys = new();
    private AudioLoad? foreground;
    private float nextPrune;

	private GdMusicApi.Track? current;

	private LyricsTimeline timeline = LyricsTimeline.Empty;

	private int generation;

	private bool active;

	private bool paused;

	private bool endReported;

	internal GdMusicApi.Track[] Results { get; private set; } = Array.Empty<GdMusicApi.Track>();

	internal string Status { get; private set; } = "输入歌名或歌手搜索";

    internal bool Busy => search != null || foreground != null;
    internal bool PlaybackPending => foreground != null;
    internal int CachedSongCount => cached.Count;
    internal void ClearSearchResults() => Results = Array.Empty<GdMusicApi.Track>();

	internal MusicState Snapshot => new MusicState(active, (current == null) ? "" : ("gd/" + current.Source + "/" + current.Id), current?.Title ?? "", current?.Artist ?? "", ((Object)(object)audio.clip != (Object)null) ? ((long)(audio.time * 1000f)) : 0, ((Object)(object)audio.clip != (Object)null) ? ((long)(audio.clip.length * 1000f)) : 0, audio.isPlaying, MusicClock.Now, audio.pitch, timeline, generation, Status);

	internal event Action<GdMusicApi.Track>? TrackStarted;

	internal event Action? TrackEnded;

    internal event Action? TrackLoadFailed;

    internal GdMusicApi.Track? PlayingTrack => audio.isPlaying ? current : null;

	private static AudioEchoFilter? AddEcho(Component host)
	{
		try
		{
			AudioEchoFilter? filter = host.gameObject.AddComponent<AudioEchoFilter>();
			if ((UnityEngine.Object)(object)filter == (UnityEngine.Object)null)
			{
				return null;
			}
			filter.delay = 0.1f;
			filter.decayRatio = 0.42f;
			filter.dryMix = 1f;
			filter.wetMix = 0f;
			return filter;
		}
		catch
		{
			return null;
		}
	}

	internal AggregatePlayer(Plugin plugin)
	{
		owner = plugin;
		audio = ((Component)plugin).gameObject.AddComponent<AudioSource>();
		audio.playOnAwake = false;
		audio.spatialBlend = 0f;
		audio.ignoreListenerPause = true;
		lowPass = ((Component)plugin).gameObject.AddComponent<AudioLowPassFilter>();
		if ((UnityEngine.Object)(object)lowPass != (UnityEngine.Object)null)
		{
			lowPass.cutoffFrequency = 22000f;
		}
		echo = AddEcho((Component)plugin);
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
		AmbienceMix.Tick(owner.Settings, Time.unscaledDeltaTime);
		audio.volume = Mathf.Clamp01(volume * AmbienceMix.MusicScale);
		audio.panStereo = AmbienceMix.Pan;
		audio.pitch = AmbienceMix.Pitch;
		if ((UnityEngine.Object)(object)lowPass != (UnityEngine.Object)null)
		{
			// 正常状态下直接关掉滤波器，别让 22000Hz 的空转把高频蹭掉一点。
			lowPass.cutoffFrequency = AmbienceMix.Cutoff;
			lowPass.enabled = AmbienceMix.Cutoff < 20000f;
		}
		if ((UnityEngine.Object)(object)echo != (UnityEngine.Object)null)
		{
			echo.wetMix = Mathf.Clamp01(AmbienceMix.EchoWet);
			echo.enabled = AmbienceMix.EchoWet > 0.001f;
		}
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
        if (Time.unscaledTime >= nextPrune)
        {
            nextPrune = Time.unscaledTime + 5f;
            PruneCache();
            foreach (var retry in retryAfter.Where(entry => entry.Value <= Time.unscaledTime).ToArray())
                retryAfter.Remove(retry.Key);
        }
        for (int i = loads.Count - 1; i >= 0; i--)
        {
            var load = loads[i];
            if (load.Download != null || !load.Resolve.IsCompleted) continue;
            try
            {
                load.Download = owner.StartCoroutine(Download(load, load.Resolve.GetAwaiter().GetResult()));
            }
            catch (Exception error)
            {
                FinishLoad(load, null, error);
            }
        }
		if (current != null && (Object)(object)audio.clip != (Object)null && !audio.isPlaying && !paused && !PlaybackPending && !endReported)
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

    private static string AudioKey(GdMusicApi.Track track, int bitrate) =>
        PlayerLibrary.Key(track) + "|" + (MuoniumHub.IsNative(track.Source) ? 0 : bitrate);

    private Task<GdMusicApi.Resolved> ResolveAudio(GdMusicApi.Track track, int bitrate)
    {
        if (MuoniumHub.IsNative(track.Source)) return owner.Hub.ResolveNativeAudio(track);
        CancellationToken token = cancel.Token;
        return Task.Run(() => api.Resolve(track, bitrate, token), token);
    }

    internal void Play(GdMusicApi.Track track, int bitrate)
    {
        if (!active || PlaybackPending) return;

        string key = AudioKey(track, bitrate);
        PruneCache();
        if (cached.TryGetValue(key, out var ready))
        {
            StartPlayback(ready);
            return;
        }

        foreground = loads.Find(load => load.Key == key);
        if (foreground == null)
        {
            foreground = new AudioLoad(key, track, ResolveAudio(track, bitrate));
            loads.Add(foreground);
        }
        Status = "正在缓存整首歌曲…";
    }

    internal void Prefetch(IReadOnlyList<GdMusicApi.Track> upcoming, int bitrate)
    {
        // One background request at a time so it cannot crowd out manual playback.
        upcomingKeys.Clear();
        foreach (var track in upcoming) upcomingKeys.Add(AudioKey(track, bitrate));
        if (!active || current == null || Busy || loads.Count != 0) return;
        foreach (var track in upcoming)
        {
            string key = AudioKey(track, bitrate);
            if (cached.ContainsKey(key) || retryAfter.ContainsKey(key)) continue;
            loads.Add(new AudioLoad(key, track, ResolveAudio(track, bitrate)));
            break;
        }
    }

    private static UnityWebRequest OpenAudioRequest(GdMusicApi.Resolved result)
    {
        var uri = new Uri(result.Url);
        string format = GdMusicApi.AudioFormat(result.Format, uri);
        AudioType type = format switch
        {
            "ogg" => AudioType.OGGVORBIS,
            "wav" => AudioType.WAV,
            "aiff" or "aif" => AudioType.AIFF,
            "flac" or "m4a" or "aac" => throw new NotSupportedException("Unity 不支持此音频格式，请选择 128 或 320 kbps"),
            _ => AudioType.MPEG
        };
        if (uri.Scheme == "http")
            uri = new UriBuilder(uri) { Scheme = "https", Port = uri.IsDefaultPort ? -1 : uri.Port }.Uri;
        if (uri.Scheme != "https" && !OwnedAudioCache.Allows(uri))
            throw new InvalidDataException("平台没有返回有效播放地址");
        var request = UnityWebRequestMultimedia.GetAudioClip(uri.AbsoluteUri, type);
        request.timeout = 45;
        var handler = (DownloadHandlerAudioClip)request.downloadHandler;
        handler.streamAudio = false;
        handler.compressed = true;
        return request;
    }

    private IEnumerator Download(AudioLoad load, GdMusicApi.Resolved result)
    {
        yield return null;
        UnityWebRequestAsyncOperation operation;
        try
        {
            load.Request = OpenAudioRequest(result);
            operation = load.Request.SendWebRequest();
        }
        catch (Exception error)
        {
            FinishLoad(load, null, error);
            yield break;
        }
        while (!operation.isDone)
        {
            if (load.Request!.downloadedBytes > 64UL * 1024 * 1024)
            {
                load.Request.Abort();
                FinishLoad(load, null, new IOException("单首音频超过 64 MiB 缓存上限"));
                yield break;
            }
            yield return null;
        }
        CachedAudio ready;
        try
        {
            if (load.Request!.result != UnityWebRequest.Result.Success)
                throw new IOException(load.Request.error);
            if (load.Request.downloadedBytes > 64UL * 1024 * 1024)
                throw new IOException("单首音频超过 64 MiB 缓存上限");
            var clip = DownloadHandlerAudioClip.GetContent(load.Request);
            if (clip == null) throw new IOException("音频解码未返回有效曲目");
            ready = new CachedAudio(clip, result, load.Request.downloadedBytes);
        }
        catch (Exception error)
        {
            FinishLoad(load, null, error);
            yield break;
        }
        FinishLoad(load, ready, null);
    }

    private void FinishLoad(AudioLoad load, CachedAudio? ready, Exception? error)
    {
        load.Request?.Dispose();
        load.Request = null;
        loads.Remove(load);
        bool play = foreground == load;
        if (play) foreground = null;
        if (ready == null)
        {
            retryAfter[load.Key] = Time.unscaledTime + 45f;
            if (play)
            {
                Status = DiagnosticLog.Report("Muonium音乐", "缓存音频", error!, "AggregatePlayer.cs", 1);
                TrackLoadFailed?.Invoke();
            }
            return;
        }

        retryAfter.Remove(load.Key);
        cached[load.Key] = ready;
        if (play) StartPlayback(ready);
        PruneCache();
        if (!play && !cached.ContainsKey(load.Key))
            retryAfter[load.Key] = Time.unscaledTime + 45f;
    }

    private void StartPlayback(CachedAudio ready)
    {
        ReleaseClip();
        ready.LastUse = Time.unscaledTime;
        audio.clip = ready.Clip;
        current = ready.Result.Track;
        timeline = ready.Result.Lyrics;
        generation++;
        paused = false;
        endReported = false;
        audio.Play();
        Status = timeline.Lines.Count > 0 ? "正在播放 · 同步歌词" : "正在播放 · 该歌曲未提供同步歌词";
        if (ready.Result.Note.Length > 0) Status += "\n" + ready.Result.Note;
        TrackStarted?.Invoke(current);
    }

    private void PruneCache()
    {
        float now = Time.unscaledTime;
        float expiry = now - owner.Settings.CacheMinutes.Value * 60f;
        foreach (var entry in cached.Where(entry => entry.Value.LastUse <= expiry && entry.Value.Clip != audio.clip).ToArray())
            RemoveCached(entry.Key);
        long bytes = cached.Values.Sum(value => value.BudgetBytes);
        if (bytes <= MaxCachedBytes && cached.Count <= MaxCachedSongs) return;

        // Discard old tracks first. If the budget still cannot fit the queue, keep the closest songs.
        foreach (var entry in cached.OrderByDescending(entry =>
            {
                int index = upcomingKeys.IndexOf(entry.Key);
                return index < 0 ? int.MaxValue : index;
            }).ThenBy(entry => entry.Value.LastUse).ToArray())
        {
            if (entry.Value.Clip == audio.clip) continue;
            RemoveCached(entry.Key);
            bytes -= entry.Value.BudgetBytes;
            if (bytes <= MaxCachedBytes && cached.Count <= MaxCachedSongs) break;
        }
    }

    private void RemoveCached(string key)
    {
        var value = cached[key];
        cached.Remove(key);
        if (value.Clip != audio.clip) Object.Destroy(value.Clip);
    }

    internal void InvalidateSource(string source)
    {
        foreach (var key in cached.Keys.Where(key => key.StartsWith(source + "/", StringComparison.Ordinal)).ToArray())
            RemoveCached(key);
        foreach (var key in retryAfter.Keys.Where(key => key.StartsWith(source + "/", StringComparison.Ordinal)).ToArray())
            retryAfter.Remove(key);
        foreach (var load in loads.Where(load => load.Track.Source == source).ToArray())
            CancelLoad(load);
    }

    private void CancelLoad(AudioLoad load)
    {
        if (load.Download != null) owner.StopCoroutine(load.Download);
        if (load.Request != null)
        {
            try { load.Request.Abort(); }
            catch (ObjectDisposedException) { }
            finally
            {
                load.Request.Dispose();
                load.Request = null;
            }
        }
        _ = load.Resolve.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        if (foreground == load) foreground = null;
        loads.Remove(load);
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
        if (clip == null) return;
        var cachedClip = cached.Values.FirstOrDefault(value => value.Clip == clip);
        if (cachedClip != null)
            cachedClip.LastUse = Time.unscaledTime;
        else
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
        search = null;
        foreach (var load in loads.ToArray()) CancelLoad(load);
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
        foreach (var key in cached.Keys.ToArray()) RemoveCached(key);
		Object.Destroy((Object)(object)audio);
	}
}
