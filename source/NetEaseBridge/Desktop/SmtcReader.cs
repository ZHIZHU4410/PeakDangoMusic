using Windows.Media.Control;

namespace DangoMusic.Desktop;

public sealed class SmtcReader
{
    private GlobalSystemMediaTransportControlsSessionManager? manager;

    public async Task<DesktopPlayback?> ReadAsync(CancellationToken ct = default, bool includeNetEase = true, bool includeLxMusic = false)
    {
        manager ??= await GlobalSystemMediaTransportControlsSessionManager.RequestAsync().AsTask(ct);
        var session = manager.GetSessions().Where(s => Matches(s.SourceAppUserModelId) && IsActiveStatus(s.GetPlaybackInfo().PlaybackStatus))
            .OrderByDescending(s => s.GetPlaybackInfo().PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
            .FirstOrDefault();
        bool Matches(string id) => includeNetEase && IsNetEase(id) || includeLxMusic && IsLxMusic(id);
        if (session == null) return null;
        var media = await session.TryGetMediaPropertiesAsync().AsTask(ct);
        if (string.IsNullOrWhiteSpace(media.Title)) return null;
        var state = session.GetPlaybackInfo();
        if (!IsActiveStatus(state.PlaybackStatus)) return null;
        var time = session.GetTimelineProperties();
        bool playing = state.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing;
        long duration = Math.Max(0, (long)(time.EndTime - time.StartTime).TotalMilliseconds);
        long? position = null;
        // The stock client reports all zeros and the 1601 epoch. This is unavailable, not track start.
        if (time.LastUpdatedTime.Year >= 2000 && duration > 0)
        {
            long advance = playing ? (long)Math.Clamp((DateTimeOffset.UtcNow - time.LastUpdatedTime).TotalMilliseconds, 0, duration) : 0;
            double rate = state.PlaybackRate is { } r && double.IsFinite(r) && r > 0 ? r : 1;
            position = Math.Clamp((long)(time.Position - time.StartTime).TotalMilliseconds + (long)(advance * rate), 0, duration);
        }
        long id = 0;
        foreach (string genre in media.Genres)
            if (genre.StartsWith("NCM-", StringComparison.OrdinalIgnoreCase) && long.TryParse(genre.AsSpan(4), out long parsed) && parsed > 0) { id = parsed; break; }
        return new(media.Title.Trim(), media.Artist.Trim(), id, position, duration, playing, Environment.TickCount64, "SMTC");
    }

    internal static bool IsActiveStatus(GlobalSystemMediaTransportControlsSessionPlaybackStatus status) =>
        status is GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing or GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused;

    internal static bool IsNetEase(string id) => id.Equals("cloudmusic.exe", StringComparison.OrdinalIgnoreCase)
        || id.Contains("NetEase.CloudMusic", StringComparison.OrdinalIgnoreCase)
        || id.StartsWith("CloudMusic", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// 洛雪 / BLF Music publishes its track through the Chromium media session. The observed BLF
    /// session id is <c>cn.toside.music.desktop</c> (the upstream LX Music application id); the
    /// executable name is kept too because forks may register their own id. This path has no lyrics.
    /// </summary>
    internal static bool IsLxMusic(string id) => id.Contains("cn.toside.music", StringComparison.OrdinalIgnoreCase)
        || id.Contains("blf", StringComparison.OrdinalIgnoreCase)
        || id.Contains("lx-music", StringComparison.OrdinalIgnoreCase)
        || id.Contains("lxmusic", StringComparison.OrdinalIgnoreCase)
        || id.Contains("lx music", StringComparison.OrdinalIgnoreCase);
}
