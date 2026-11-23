using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed partial class MuoniumHub
{
    private const int OrderedMode = 0;
    private const int RepeatOneMode = 1;
    private const int ShuffleMode = 2;
    private const int HeartMode = 3;
    private static readonly string[] PlaybackModeNames = { "顺序播放", "单曲循环", "随机播放", "心动模式" };
    private static readonly string[] PlaybackModeButtons = { "顺序", "单曲循环", "随机", "心动模式" };

    private Task<JObject>? heartTask;
    private int previousMode;
    private string heartSeed = "";
    private bool openingHeart;
    private bool endedWhileLoading;
    private bool tryingHeartBatch;

    private bool HeartAvailable => platform == 0 || current?.Source == "ncm";

    private void ChangePlaybackMode(int mode)
    {
        if (playMode == mode) return;
        if (mode == HeartMode && (!HeartAvailable || plugin.Aggregate.PlaybackPending))
        {
            message = "请切到网易云音乐，并等待正在加载的歌曲完成";
            return;
        }

        CancelAutoAdvance();
        DropHeartRequest();
        tryingHeartBatch = false;
        if (mode == HeartMode)
        {
            previousMode = playMode;
            heartSeed = "";
            Activate();
            CancelAutoAdvance();
        }
        playMode = mode;
        upcomingVersion = -1;
        if (mode == HeartMode) RequestHeart(initial: true);
    }

    private void RequestHeart(bool initial)
    {
        if (heartTask != null) return;
        openingHeart = initial;
        endedWhileLoading = false;
        string preferred = initial && current?.Source == "ncm" ? current.Id : "";
        heartTask = client.Call("heart", "ncm", new JObject
        {
            ["songId"] = preferred,
            ["previousSeed"] = heartSeed
        });
        message = initial ? "正在读取网易云心动推荐…" : "正在补充心动推荐…";
    }

    private void DropHeartRequest()
    {
        if (heartTask != null)
            _ = heartTask.ContinueWith(task => _ = task.Exception, TaskContinuationOptions.OnlyOnFaulted);
        heartTask = null;
        endedWhileLoading = false;
    }

    private void CollectHeart()
    {
        var task = heartTask;
        if (task == null || !task.IsCompleted || plugin.Aggregate.PlaybackPending) return;
        heartTask = null;
        bool initial = openingHeart;
        bool ended = endedWhileLoading;
        endedWhileLoading = false;

        GdMusicApi.Track[] result;
        string note;
        try
        {
            JObject data = task.GetAwaiter().GetResult();
            note = (string?)data["message"] ?? "";
            heartSeed = (string?)data["seed"] ?? heartSeed;
            result = data["tracks"] is JArray array ? ParseTracks(array) : Array.Empty<GdMusicApi.Track>();
        }
        catch (Exception error)
        {
            FinishUnavailableHeart(initial, ended,
                DiagnosticLog.Report("网易云音乐", "获取心动推荐", error, "MuoniumHub.Heart.cs", 1));
            return;
        }

        bool keepPlaying = initial && !ended && current?.Source == "ncm" && plugin.Aggregate.Snapshot.Playing;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        if (!initial)
        {
            foreach (GdMusicApi.Track track in library.Queue) seen.Add(PlayerLibrary.Key(track));
            foreach (GdMusicApi.Track track in library.History.Take(35)) seen.Add(PlayerLibrary.Key(track));
        }
        if (keepPlaying) seen.Add(PlayerLibrary.Key(current!));
        var suggestions = result.Where(track => track.Source == "ncm" && seen.Add(PlayerLibrary.Key(track))).Take(50).ToArray();
        if (suggestions.Length == 0)
        {
            FinishUnavailableHeart(initial, ended, note.Length > 0 ? note : "网易云暂未返回新的心动歌曲");
            return;
        }

        if (initial)
        {
            library.Queue.Clear();
            if (keepPlaying) library.Queue.Add(current!);
        }
        else if (library.Queue.Count + suggestions.Length > 200)
        {
            library.Queue.RemoveRange(0, library.Queue.Count + suggestions.Length - 200);
        }
        library.Queue.AddRange(suggestions);
        library.Touch();
        upcomingVersion = -1;
        queueIndex = keepPlaying ? 0 : library.Queue.FindIndex(track => current != null && PlayerLibrary.Key(track) == PlayerLibrary.Key(current));
        message = $"心动模式 · 已加入 {suggestions.Length} 首推荐";
        if (!keepPlaying)
        {
            tryingHeartBatch = true;
            autoQueue = new Queue<GdMusicApi.Track>(suggestions);
            advancePending = true;
        }
    }

    private void FinishUnavailableHeart(bool initial, bool ended, string note)
    {
        if (initial)
        {
            playMode = previousMode;
            upcomingVersion = -1;
            if (ended) Ended();
        }
        message = note;
    }
}
