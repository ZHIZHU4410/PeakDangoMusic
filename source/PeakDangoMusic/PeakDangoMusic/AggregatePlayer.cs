using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DangoMusic.Core;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class AggregatePlayer : IDisposable
{
    private sealed class AudioLoad
    {
        internal readonly string Key;
        internal readonly GdMusicApi.Track Track;
        internal readonly Task<GdMusicApi.Resolved> Resolve;
        internal readonly int Id;
        internal GdMusicApi.Resolved? Result;

        internal AudioLoad(int id, string key, GdMusicApi.Track track, Task<GdMusicApi.Resolved> resolve)
        {
            Id = id;
            Key = key;
            Track = track;
            Resolve = resolve;
        }
    }

    private sealed class CachedAudio
    {
        internal readonly int Id;
        internal readonly GdMusicApi.Resolved Result;
        internal readonly long Bytes;
        internal float LastUse = Time.unscaledTime;

        internal CachedAudio(int id, GdMusicApi.Resolved result, long bytes)
        {
            Id = id;
            Result = result;
            Bytes = bytes;
        }
    }

    private const int MaxCachedSongs = 12;
    private const long MaxCachedBytes = 160L * 1024 * 1024;
    private readonly Plugin owner;
    private readonly GdMusicApi api = new();
    private readonly Dictionary<string, CachedAudio> cached = new();
    private readonly Dictionary<string, float> retryAfter = new();
    private readonly List<AudioLoad> loads = new();
    private readonly List<string> upcomingKeys = new();
    private CancellationTokenSource cancel = new();
    private IndependentAudio? transport;
    private AudioLoad? foreground;
    private CachedAudio? starting;
    private CachedAudio? current;
    private IndependentAudio.Update? clock;
    private Task<GdMusicApi.Track[]>? search;
    private int nextMediaId, token, generation;
    private bool active, repeat, endReported;
    private float nextPrune;
    private long nextMix;
    private int[]? continuation;
    private (float Gain, float Pan, float Cutoff, float Wet, float Rate, float Delay, float Feedback)? appliedMix;

    internal AggregatePlayer(Plugin plugin) => owner = plugin;

    internal GdMusicApi.Track[] Results { get; private set; } = Array.Empty<GdMusicApi.Track>();
    internal string Status { get; private set; } = "输入歌名或歌手搜索";
    internal bool Busy => SearchPending || PlaybackPending;
    internal bool SearchPending => search != null;
    internal bool PlaybackPending => foreground != null || starting != null;
    internal int CachedSongCount => cached.Count;
    internal GdMusicApi.Track? PlayingTrack => clock?.Playing == true ? current?.Result.Track : null;

    internal event Action<GdMusicApi.Track>? TrackStarted;
    internal event Action? TrackEnded;
    internal event Action? TrackLoadFailed;

    internal void ClearSearchResults() => Results = Array.Empty<GdMusicApi.Track>();

    internal MusicState Snapshot
    {
        get
        {
            var track = current?.Result.Track;
            var sample = clock;
            long now = MusicClock.Now;
            long age = sample != null ? now - sample.ReceivedAt : 0L;
            bool playing = sample?.Playing == true && age <= 1500;
            long duration = sample?.Duration ?? 0L;
            long position = Math.Clamp((sample?.Position ?? 0L) + (playing ? (long)(Math.Max(0, age) * sample!.Rate) : 0L), 0L, duration);
            return new MusicState(active, track == null ? "" : "gd/" + track.Source + "/" + track.Id,
                track?.Title ?? "", track?.Artist ?? "", position, duration, playing, now, sample?.Rate ?? 1,
                current?.Result.Lyrics, generation, Status);
        }
    }

    internal void Tick(bool enabled, float volume)
    {
        if (active != enabled)
        {
            active = enabled;
            if (!enabled) Stop();
        }

        AmbienceMix.Tick(owner.Settings, Time.unscaledDeltaTime);
        ReadAudioState();
        UpdateAudioMix(volume);
        if (search is { IsCompleted: true } finished)
        {
            search = null;
            try
            {
                Results = finished.GetAwaiter().GetResult();
                Status = Results.Length == 0 ? "该平台没有搜索结果，可更换平台" : $"找到 {Results.Length} 首，点击播放";
            }
            catch (Exception error)
            {
                Status = DiagnosticLog.Report("GD聚合音乐", "搜索歌曲", error);
            }
        }
        if (Time.unscaledTime >= nextPrune)
        {
            nextPrune = Time.unscaledTime + 5f;
            PruneCache();
            foreach (var key in retryAfter.Where(entry => entry.Value <= Time.unscaledTime).Select(entry => entry.Key).ToArray())
                retryAfter.Remove(key);
        }
        for (int i = loads.Count - 1; i >= 0; i--)
        {
            var load = loads[i];
            if (load.Result != null || !load.Resolve.IsCompleted) continue;
            try
            {
                var resolved = load.Resolve.GetAwaiter().GetResult();
                var uri = new Uri(resolved.Url);
                if (uri.Scheme == "http")
                    uri = new UriBuilder(uri) { Scheme = "https", Port = uri.IsDefaultPort ? -1 : uri.Port }.Uri;
                if (uri.Scheme != "https" && !OwnedAudioCache.Allows(uri))
                    throw new InvalidDataException("平台没有返回有效播放地址");
                load.Result = resolved;
                EnsureTransport().Send(new { type = "load", id = load.Id, url = uri.AbsoluteUri });
            }
            catch (Exception error)
            {
                FinishLoad(load, null, error);
            }
        }
    }

    private IndependentAudio EnsureTransport()
    {
        if (transport != null) return transport;
        transport = new IndependentAudio();
        appliedMix = null;
        nextMix = 0;
        UpdateAudioMix(owner.Settings.AggregateVolume.Value);
        return transport;
    }

    private void UpdateAudioMix(float volume)
    {
        long now = MusicClock.Now;
        if (transport == null || now < nextMix) return;
        nextMix = now + 50;
        var mix = (Gain: Mathf.Clamp01(volume * AmbienceMix.MusicScale), Pan: AmbienceMix.Pan,
            Cutoff: AmbienceMix.Cutoff, Wet: Mathf.Clamp01(AmbienceMix.EchoWet), Rate: AmbienceMix.Pitch,
            Delay: AmbienceMix.EchoDelay, Feedback: AmbienceMix.EchoFeedback);
        if (appliedMix == mix) return;
        appliedMix = mix;
        transport.Send(new { type = "mix", gain = mix.Gain, pan = mix.Pan,
            cutoff = mix.Cutoff, wet = mix.Wet, rate = mix.Rate, delay = mix.Delay, feedback = mix.Feedback });
    }

    private void ReadAudioState()
    {
        if (transport is not { } source) return;
        if (source.Failure is { } failure)
        {
            bool pending = PlaybackPending;
            Stop();
            source.Dispose();
            transport = null;
            cached.Clear();
            Status = failure;
            if (pending) TrackLoadFailed?.Invoke();
            return;
        }
        while (source.Events.TryDequeue(out var update))
        {
            if (update.Type is "loaded" or "loadError")
            {
                var load = loads.Find(item => item.Id == update.Id);
                if (load == null) continue;
                if (update.Type == "loaded" && load.Result != null && update.Bytes > 0)
                    FinishLoad(load, new CachedAudio(load.Id, load.Result, update.Bytes), null);
                else
                    FinishLoad(load, null, new IOException(update.Error));
                continue;
            }
            if (update.Token != token) continue;
            switch (update.Type)
            {
                case "started":
                    var ready = starting?.Id == update.Id ? starting :
                        cached.Values.FirstOrDefault(item => item.Id == update.Id);
                    if (ready != null) Started(ready, update);
                    break;
                case "ended":
                    if (current?.Id == update.Id && !endReported)
                    {
                        clock = update;
                        endReported = true;
                        if (!PlaybackPending)
                        {
                            Status = "播放结束，可选择下一首";
                            TrackEnded?.Invoke();
                        }
                    }
                    break;
                case "playError":
                    bool failedToStart = starting?.Id == update.Id;
                    if (!failedToStart && current?.Id != update.Id) break;
                    if (failedToStart) starting = null;
                    endReported = true;
                    Status = update.Error;
                    if (failedToStart) TrackLoadFailed?.Invoke();
                    break;
            }
        }
        if (source.Sample is { } sample && sample.Token == token && current?.Id == sample.Id)
        {
            if (sample.Revision != clock?.Revision)
                generation++;
            if (sample.Playing != clock?.Playing && !endReported && !PlaybackPending)
                Status = sample.Playing ? PlayingStatus(current) : "已暂停";
            if (sample.Playing) endReported = false;
            clock = sample;
        }
    }

    internal void Search(string platform, string keyword, int page)
    {
        if (Busy || !active || string.IsNullOrWhiteSpace(keyword)) return;
        Results = Array.Empty<GdMusicApi.Track>();
        Status = "正在搜索…";
        CancellationToken searchToken = cancel.Token;
        search = Task.Run(() => api.Search(platform, keyword, page, searchToken));
    }

    private static string AudioKey(GdMusicApi.Track track, int bitrate) =>
        PlayerLibrary.Key(track) + "|" + (MuoniumHub.IsNative(track.Source) ? 0 : bitrate);

    private AudioLoad Resolve(GdMusicApi.Track track, int bitrate, string key)
    {
        var requestToken = cancel.Token;
        var resolution = MuoniumHub.IsNative(track.Source) ? owner.Hub.ResolveNativeAudio(track) :
            Task.Run(() => api.Resolve(track, bitrate, requestToken), requestToken);
        var load = new AudioLoad(++nextMediaId, key, track, resolution);
        loads.Add(load);
        return load;
    }

    internal void Play(GdMusicApi.Track track, int bitrate, bool loop = false)
    {
        if (!active || PlaybackPending) return;
        SetContinuation(Array.Empty<int>());
        repeat = loop;
        string key = AudioKey(track, bitrate);
        PruneCache();
        if (cached.TryGetValue(key, out var ready))
        {
            StartPlayback(ready);
            return;
        }
        foreground = loads.Find(load => load.Key == key) ?? Resolve(track, bitrate, key);
        Status = "正在缓存整首歌曲…";
    }

    internal void SetRepeat(bool loop)
    {
        if (PlaybackPending || repeat == loop) return;
        repeat = loop;
        if (loop) SetContinuation(Array.Empty<int>());
        transport?.Send(new { type = "repeat", token, repeat });
    }

    internal void Prefetch(IReadOnlyList<GdMusicApi.Track> upcoming, int bitrate, bool mayLoad)
    {
        upcomingKeys.Clear();
        foreach (var track in upcoming) upcomingKeys.Add(AudioKey(track, bitrate));
        if (!active || current == null || PlaybackPending) return;

        var ready = new List<int>();
        if (!repeat)
        {
            foreach (var key in upcomingKeys)
            {
                if (!cached.TryGetValue(key, out var song)) break;
                ready.Add(song.Id);
            }
        }
        SetContinuation(ready.ToArray());

        if (!mayLoad || Busy || loads.Count != 0) return;
        foreach (var track in upcoming)
        {
            string key = AudioKey(track, bitrate);
            if (cached.ContainsKey(key) || retryAfter.ContainsKey(key)) continue;
            Resolve(track, bitrate, key);
            break;
        }
    }

    private void SetContinuation(int[] ids)
    {
        if (continuation != null && continuation.SequenceEqual(ids)) return;
        continuation = ids;
        transport?.Send(new { type = "next", token, current = current?.Id ?? 0, ids });
    }

    private void FinishLoad(AudioLoad load, CachedAudio? ready, Exception? error)
    {
        loads.Remove(load);
        bool play = foreground == load;
        if (play) foreground = null;
        if (ready == null)
        {
            retryAfter[load.Key] = Time.unscaledTime + 45f;
            if (play)
            {
                Status = DiagnosticLog.Report("Muonium音乐", "缓存音频", error!);
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
        starting = ready;
        ready.LastUse = Time.unscaledTime;
        continuation = Array.Empty<int>();
        Status = "正在启动独立播放…";
        EnsureTransport().Send(new { type = "play", id = ready.Id, token = ++token, repeat });
    }

    private void Started(CachedAudio ready, IndependentAudio.Update update)
    {
        if (current != null)
        {
            current.LastUse = Time.unscaledTime;
            if (!cached.ContainsValue(current))
                transport?.Send(new { type = "drop", id = current.Id });
        }
        ready.LastUse = Time.unscaledTime;
        current = ready;
        starting = null;
        clock = update;
        continuation = null;
        generation++;
        endReported = false;
        Status = PlayingStatus(ready);
        TrackStarted?.Invoke(ready.Result.Track);
    }

    private static string PlayingStatus(CachedAudio item) =>
        (item.Result.Lyrics.Lines.Count > 0 ? "正在播放 · 同步歌词" : "正在播放 · 该歌曲未提供同步歌词") +
        (item.Result.Note.Length > 0 ? "\n" + item.Result.Note : "");

    private void PruneCache()
    {
        float expiry = Time.unscaledTime - owner.Settings.CacheMinutes.Value * 60f;
        foreach (var entry in cached.Where(entry => !InUse(entry.Value) && entry.Value.LastUse <= expiry).ToArray())
            RemoveCached(entry.Key);
        long bytes = cached.Values.Sum(value => value.Bytes);
        if (bytes <= MaxCachedBytes && cached.Count <= MaxCachedSongs) return;
        foreach (var entry in cached.OrderByDescending(entry =>
            {
                int index = upcomingKeys.IndexOf(entry.Key);
                return index < 0 ? int.MaxValue : index;
            }).ThenBy(entry => entry.Value.LastUse).ToArray())
        {
            if (InUse(entry.Value)) continue;
            RemoveCached(entry.Key);
            bytes -= entry.Value.Bytes;
            if (bytes <= MaxCachedBytes && cached.Count <= MaxCachedSongs) break;
        }
    }

    private bool InUse(CachedAudio audio) => audio == current || audio == starting;

    private void RemoveCached(string key)
    {
        var ready = cached[key];
        cached.Remove(key);
        if (!InUse(ready))
            transport?.Send(new { type = "drop", id = ready.Id });
    }

    internal void InvalidateSource(string source)
    {
        SetContinuation(Array.Empty<int>());
        foreach (var key in cached.Keys.Where(key => key.StartsWith(source + "/", StringComparison.Ordinal)).ToArray())
            RemoveCached(key);
        foreach (var key in retryAfter.Keys.Where(key => key.StartsWith(source + "/", StringComparison.Ordinal)).ToArray())
            retryAfter.Remove(key);
        foreach (var load in loads.Where(load => load.Track.Source == source).ToArray())
            CancelLoad(load);
    }

    private void CancelLoad(AudioLoad load)
    {
        if (load.Result != null)
            transport?.Send(new { type = "drop", id = load.Id });
        ObserveFault(load.Resolve);
        if (foreground == load) foreground = null;
        loads.Remove(load);
    }

    private static void ObserveFault(Task task) =>
        _ = task.ContinueWith(completed => { _ = completed.Exception; }, TaskContinuationOptions.OnlyOnFaulted);

    internal void TogglePause()
    {
        if (current != null && starting == null)
            transport?.Send(new { type = "pause", token });
    }

    internal void Seek(float fraction)
    {
        if (current != null && starting == null && !float.IsNaN(fraction))
            transport?.Send(new { type = "seek", token, fraction = Mathf.Clamp(fraction, 0f, .999f) });
    }

    internal void Stop()
    {
        cancel.Cancel();
        cancel.Dispose();
        cancel = new CancellationTokenSource();
        if (search != null) ObserveFault(search);
        search = null;
        foreach (var load in loads.ToArray()) CancelLoad(load);
        transport?.Send(new { type = "stop", token = ++token });
        if (current != null && !cached.ContainsValue(current))
            transport?.Send(new { type = "drop", id = current.Id });
        current = starting = null;
        continuation = Array.Empty<int>();
        clock = null;
        generation++;
        Status = "已停止";
    }

    public void Dispose()
    {
        Stop();
        transport?.Dispose();
        cached.Clear();
        cancel.Dispose();
        api.Dispose();
    }
}
