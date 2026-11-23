using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DangoMusic.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakDangoMusic;

internal sealed partial class MuoniumHub : IDisposable
{
	private readonly struct PlaylistRow
	{
		internal readonly string Id;

		internal readonly string Name;

		internal readonly string Cover;

		internal readonly int Count;

		internal PlaylistRow(string id, string name, int count, string cover)
		{
			Id = id;
			Name = name;
			Count = count;
			Cover = cover;
		}
	}

	private const float SidebarWide = 212f;

	private const float SidebarNarrow = 158f;

	private const float NavHeight = 44f;

	private const float NavGap = 4f;

	private const float PlatformHeight = 34f;

	private const float TrackRowHeight = 70f;

	private const float TrackRowGap = 6f;

	private const float TrackActionWidth = 78f;

	private const float TrackAddWidth = 62f;

	private const float ColumnGap = 6f;

	private const float RowButtonHeight = 44f;

	private const float PlaylistHeight = 54f;

	private const int TrackPageSize = 60;

	private const int PlaylistPageSize = 60;

	private readonly Plugin plugin;

	private readonly MuoniumClient client = new MuoniumClient();

	private readonly PlayerLibrary library = new PlayerLibrary();

	private Task<JObject>? pending;

	private string pendingOp = "";

	private string pendingSource = "";

	private string query = "";

	private string playlistId = "";

	private string message = "选择平台，搜索歌曲或登录账号";

	private string accountName = "未登录";

	private string accountAvatar = "";

	private string accountId = "";

	private string native = "ncm";

	private bool loggedIn;

	private bool polling;

	private float pollAt;

	private int page;

	private int platform;

	private int searchPage = 1;

	private int queueIndex = -1;

	private int playMode;

	private readonly System.Random random = new System.Random();

	private Vector2 scroll;

	private Vector2 sidebarScroll;

	private Vector2 songScroll;

	private GdMusicApi.Track[] rows = Array.Empty<GdMusicApi.Track>();

	private GdMusicApi.Track? current;

    private Queue<GdMusicApi.Track>? autoQueue;
    private bool advancePending;

	private JObject[] playlists = Array.Empty<JObject>();

	private Texture2D? qr;

	private GUIStyle? heading;

	private GUIStyle? bigHeading;

	private GUIStyle? section;

	private GUIStyle? muted;

	private GUIStyle? authorNoteStyle;

	private GUIStyle? messageStyle;

	private GUIStyle? card;

	private GUIStyle? cardOn;

	private GUIStyle? nav;

	private GUIStyle? navSelected;

	private GUIStyle? small;

	private GUIStyle? center;

	private GUIStyle? titleSmall;

	private GUIStyle? rowTitle;

	private GUIStyle? rowMeta;

	private GUIStyle? badge;

	private GUIStyle? searchField;

	private Font? titleFont;

	private readonly List<string> trackLabels = new List<string>();

	private readonly List<PlaylistRow> playlistRows = new List<PlaylistRow>();

	private readonly List<PlaylistRow> libraryRows = new List<PlaylistRow>();

	private JObject[] libraryPlaylists = Array.Empty<JObject>();

	private int discoverPage;

	private int libraryPage;

	private float lastScrollY = -1f;

	private GdMusicApi.Track[] cachedTracks = Array.Empty<GdMusicApi.Track>();

	private int cachedCount = -1;

	private float cachedLabelWidth = -1f;

	private int cachedLibraryVersion = -1;

	private GdMusicApi.Track[] favoritesView = Array.Empty<GdMusicApi.Track>();

	private GdMusicApi.Track[] historyView = Array.Empty<GdMusicApi.Track>();

	private GdMusicApi.Track[] queueView = Array.Empty<GdMusicApi.Track>();

	private GdMusicApi.Track[] recentView = Array.Empty<GdMusicApi.Track>();

	private int viewVersion = -1;

	private long cachedPositionSecond = -1L;

	private long cachedDurationSecond = -1L;

	private string cachedPositionText = "00:00";

	private string cachedDurationText = "00:00";

	private Texture2D? avatarTexture;

	private string avatarRequested = "";

	private int avatarSize = 64;

	private static readonly string[] Pages = new string[10] { "首页", "发现歌单", "我的歌单", "本地收藏", "播放历史", "播放队列", "账号中心", "悬浮歌词", "设置", "开发作者" };

	private static readonly string[] AuthorNames = new string[4] { "布洛芬", "Dv", "羊默默", "银歪大" };

	private static readonly string[] AuthorRoles = new string[4] { "主要负责 · 音乐播放器框架 / 修 bug / 后期维护", "主要负责 · 页面设计 / 功能开发 / 修 bug", "主要负责 · 测试 / bug 反馈", "主要负责 · 宣传" };

	private static readonly string[] AuthorUrls = new string[4] { "https://v.douyin.com/nJqjiK2Wc40/", "https://v.douyin.com/qwSfvxDN9d8/", "https://v.douyin.com/M5TKeBTKMP4/", "https://v.douyin.com/rO9gKqJFpbk/" };

	private const int AuthorPage = 9;
	private const int SettingsPage = 8;
	internal bool ViewingSettings => page == SettingsPage;

	private const int SearchPage = 12;

	private const int ChartPage = 13;

	private const int PlaylistPage = 14;

	private static readonly string[] ChartNames = new string[4] { "热歌榜", "新歌榜", "飙升榜", "原创榜" };

	private static readonly string[] ChartHints = new string[4] { "全网最热单曲 · 每小时更新", "最新上架单曲 · 每日更新", "热度蹿升最快 · 每小时更新", "独立与原创 · 每周更新" };

	private static readonly string[] ChartIds = new string[4] { "3778678", "3779629", "19723756", "2884035" };

	private static readonly string[] ChartSubs = new string[4] { "每小时更新", "每日更新", "每小时更新", "每周更新" };

	private static readonly string[] QqChartNames = new string[4] { "热歌榜", "新歌榜", "飙升榜", "流行指数榜" };

	private static readonly string[] QqChartHints = new string[4] { "QQ音乐站内播放热度前300 · 每日更新", "QQ音乐最新上架单曲 · 每日更新", "QQ音乐热度蹿升最快 · 每小时更新", "QQ音乐流行指数 · 每日更新" };

	private static readonly string[] QqChartSubs = new string[4] { "每日更新", "每日更新", "每小时更新", "每日更新" };

	private static readonly int[] QqChartIds = new int[4] { 26, 27, 62, 4 };

	private int chartIndex = -1;

	private bool chartRetried;

	private string playlistTitle = "";

	private int playlistOrigin = 1;

	private bool playlistRetried;

	private string discoverKey = "";

	private int discoverTries;

	private float discoverAt;

	private bool bootProbed;

	private string recKey = "";

	private string libKey = "";

	private float contentWidth = 1000f;

	private Task<GdMusicApi.Track[]>? chartTask;

	private GdMusicApi.Track[] recRows = Array.Empty<GdMusicApi.Track>();

	private Task<GdMusicApi.Track[]>? recTask;

	private int recOffset;

	private string recNote = "";

	private const int AccountPage = 6;

	private const int PlatformPages = 7;

	private const int AggregateArrowPages = 5;

	private const string SearchControl = "muonium-search";

	private static readonly Color FieldFill = new Color(0.98f, 0.99f, 1f, 0.55f);

	private static GUIStyle? fitStyle;

	private GUIStyle? titleEntry;

	private bool Busy => pending != null;

	private string Source
	{
		get
		{
			if (platform == 6) return "lx-native";
            if (platform == 5) return "youtube-native";
			if (platform == 4) return "bilibili-native";
            if (platform == 3) return "kugou";
			if (platform != 0)
			{
				if (platform != 1)
				{
					return GdMusicApi.Keys[plugin.Settings.AggregatePlatform.Value];
				}
				return "qq";
			}
			return "ncm";
		}
	}

	private string PageTitle
	{
		get
		{
			if (page == ChartPage)
			{
				return ChartLabel(chartIndex);
			}
			if (page == PlaylistPage)
			{
				return (playlistTitle.Length == 0) ? "歌单" : playlistTitle;
			}
			if (page != SearchPage)
			{
				if (page < 0 || page >= Pages.Length)
				{
					return "播放器";
				}
				return Pages[page];
			}
			return "搜索结果";
		}
	}

	private string PlatformLabel
	{
		get
		{
			if (platform == 6) return "LX 自定义源";
            if (platform == 5) return "YouTube";
			if (platform == 4) return "哔哩哔哩";
            if (platform == 3) return "酷狗音乐";
			if (platform != 0)
			{
				if (platform != 1)
				{
					return GdMusicApi.Names[plugin.Settings.AggregatePlatform.Value];
				}
				return "QQ音乐";
			}
			return "网易云音乐";
		}
	}

	private static GUIStyle FitStyle
	{
		get
		{
			if (fitStyle == null)
			{
				fitStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 16,
					wordWrap = false
				};
				fitStyle.normal.textColor = Color.white;
			}
			return fitStyle;
		}
	}

	internal MuoniumHub(Plugin owner)
	{
		plugin = owner;
		plugin.Aggregate.TrackStarted += Started;
		plugin.Aggregate.TrackEnded += Ended;
        plugin.Aggregate.TrackLoadFailed += LoadFailed;
	}

	private void Styles()
	{
		if (heading == null)
		{
			heading = new GUIStyle(GUI.skin.label)
			{
				fontSize = 26,
				fontStyle = (FontStyle)1,
				wordWrap = false,
				clipping = (TextClipping)0
			};
			bigHeading = new GUIStyle(heading)
			{
				fontSize = 19
			};
			section = new GUIStyle(heading)
			{
				fontSize = 16
			};
			titleSmall = new GUIStyle(GUI.skin.label)
			{
				fontSize = 13,
				fontStyle = (FontStyle)1,
				wordWrap = false,
				clipping = (TextClipping)0
			};
			titleSmall.normal.textColor = UiKit.Faint;
			muted = new GUIStyle(GUI.skin.label)
			{
				fontSize = 13,
				wordWrap = true
			};
			muted.normal.textColor = UiKit.Muted;
			authorNoteStyle = new GUIStyle(muted) { fontSize = 17 };
			messageStyle = new GUIStyle(muted)
			{
				wordWrap = false,
				clipping = (TextClipping)0,
				fontSize = 12
			};
			messageStyle.normal.textColor = UiKit.Faint;
			small = new GUIStyle(muted)
			{
				fontSize = 12,
				wordWrap = false,
				clipping = (TextClipping)0
			};
			center = new GUIStyle(small)
			{
				alignment = (TextAnchor)4
			};
			center.normal.textColor = UiKit.Text;
			rowTitle = new GUIStyle(GUI.skin.label)
			{
				fontSize = 17,
				fontStyle = (FontStyle)1,
				wordWrap = false,
				clipping = (TextClipping)0
			};
			rowTitle.normal.textColor = UiKit.Text;
			rowMeta = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				wordWrap = false,
				clipping = (TextClipping)0
			};
			rowMeta.normal.textColor = UiKit.Muted;
			badge = new GUIStyle(small)
			{
				fontSize = 12,
				fontStyle = (FontStyle)1,
				alignment = (TextAnchor)4
			};
			card = new GUIStyle(GUI.skin.button)
			{
				alignment = (TextAnchor)3,
				wordWrap = false,
				clipping = (TextClipping)1,
				padding = new RectOffset(14, 10, 6, 6),
				fontSize = 15
			};
			card.normal.textColor = UiKit.Text;
			card.hover.textColor = UiKit.Text;
			card.active.textColor = UiKit.Text;
			cardOn = new GUIStyle(card);
			cardOn.normal.background = GUI.skin.button.onNormal.background;
			cardOn.hover.background = GUI.skin.button.onHover.background;
			cardOn.normal.textColor = UiKit.Ink;
			cardOn.hover.textColor = UiKit.Ink;
			cardOn.active.textColor = UiKit.Ink;
			nav = new GUIStyle(GUI.skin.button)
			{
				alignment = (TextAnchor)3,
				padding = new RectOffset(14, 10, 6, 6),
				fontSize = 15,
				wordWrap = false,
				clipping = (TextClipping)1
			};
			navSelected = new GUIStyle(nav);
		}
	}

	internal void Tick()
	{
        if (!plugin.Settings.PlayerEnabled.Value)
        {
            CancelAutoAdvance();
            if (playMode == HeartMode) ChangePlaybackMode(OrderedMode);
        }
        else
        {
            CollectHeart();
            if (advancePending) Advance();
        }
        library.Flush(Time.unscaledTime);
        if (Time.unscaledTime >= nextPrefetch)
        {
            nextPrefetch = Time.unscaledTime + 0.5f;
            CoverCache.CollectDownloads();
            UpdateUpcoming();
            if (!Busy && !polling && heartTask == null && plugin.Settings.PrefetchSongs.Value > 0)
                plugin.Aggregate.Prefetch(upcoming, plugin.Settings.AggregateBitrate.Value);
            if (closedAt >= 0f && Time.unscaledTime - closedAt >= CacheLifetime)
            {
                closedAt = -1f;
                CoverCache.Reset();
                ResetCatalogueView();
            }
        }
		RefreshAvatar();
		Task<GdMusicApi.Track[]>? chartLoad = chartTask;
		if (chartLoad != null && chartLoad.IsCompleted)
		{
			chartTask = null;
			try
			{
				rows = chartLoad.GetAwaiter().GetResult();
				InvalidateTrackCache();
				message = "QQ 音乐榜单已加载";
			}
			catch (Exception error)
			{
				message = DiagnosticLog.Report("Muonium音乐", "读取QQ榜单", error, "MuoniumHub.cs", 300);
			}
		}
		Task<GdMusicApi.Track[]>? recLoad = recTask;
		if (recLoad != null && recLoad.IsCompleted)
		{
			recTask = null;
			try
			{
				recRows = recLoad.GetAwaiter().GetResult();
				recOffset = 0;
				recNote = ((recRows.Length == 0) ? "这个账号今天暂时没有推荐" : "");
			}
			catch (Exception error)
			{
				string text = error.Message ?? "";
				if (text.Contains("IllegalStateException"))
				{
					recNote = "网易云还没登录：去「账号中心」扫码登录，再回首页会自动重新拉取";
				}
				else if (text.Contains("IllegalArgumentException"))
				{
					recNote = "当前平台没有每日推荐，切到「网易云」再试";
				}
				else
				{
					recNote = "推荐拉取失败：" + text;
				}
			}
		}
		Task<JObject>? task = pending;
		if (task != null && task.IsCompleted)
		{
			Task<JObject> task2 = pending;
			pending = null;
			try
			{
                string finishedOp = pendingOp;
                string finishedSource = pendingSource;
                string detailId = pendingPlaylistId;
                int requestPage = pendingPage;
				JObject result = task2.GetAwaiter().GetResult();
                if (!pendingFromCache && pendingCacheKey != null &&
                    (result["tracks"] is JArray || result["playlists"] is JArray))
                    StoreCatalogue(pendingCacheKey, result);
                if (finishedOp is "restore" or "logout" or "loginCookie" || (string?)result["state"] == "CONFIRMED")
                    InvalidateAccountCaches(finishedSource);
				if (finishedOp == "restore")
				{
					message = "登录态已导入，正在校验…";
					Request("account", pendingSource);
				}
				JToken val = result["tracks"];
				JArray val2 = (JArray)(object)((val is JArray) ? val : null);
				if (val2 != null && (finishedOp != "playlist" ||
                    (page == requestPage && (requestPage != PlaylistPage || playlistId == detailId))))
				{
					rows = ParseTracks(val2);
					InvalidateTrackCache();
				}
				JToken val3 = result["playlists"];
				JArray val4 = (JArray)(object)((val3 is JArray) ? val3 : null);
				if (val4 != null)
				{
					if (finishedOp == "library")
					{
						libraryPlaylists = ((IEnumerable)val4).OfType<JObject>().ToArray();
						RebuildLibraryRows();
					}
					else
					{
						playlists = ((IEnumerable)val4).OfType<JObject>().ToArray();
						RebuildPlaylistRows();
					}
				}
				if (result["image"] != null)
				{
					ClearQr();
					qr = new Texture2D(2, 2);
					ImageConversion.LoadImage(qr, Convert.FromBase64String((string)result["image"]));
					((Texture)qr).filterMode = (FilterMode)0;
					native = pendingSource;
					polling = true;
					pollAt = Time.unscaledTime + 2f;
					message = pendingSource=="qq"?(qqWechat?"请使用微信扫描并确认":"请使用QQ扫描并确认"):"请用对应平台的手机App扫描二维码并确认";
				}
				else if (result["state"] != null)
				{
					string text = (string)result["state"];
					message = text switch
					{
						"WAITING" => "等待扫码",
						"SCANNED" => "已扫码，请在手机上确认",
						"CONFIRMED" => "登录成功",
						"EXPIRED" => "二维码已过期，请重新生成",
						_ => "登录失败，请重新生成二维码",
					};
					bool flag = ((text == "WAITING" || text == "SCANNED") ? true : false);
					polling = flag;
					pollAt = Time.unscaledTime + 2f;
					if (text == "CONFIRMED")
					{
						ClearQr();
						Request("account", pendingSource);
					}
				}
				else if (result["loggedIn"] != null)
				{
					loggedIn = (bool)result["loggedIn"];
					accountName = (loggedIn ? (((string)result["name"]) ?? "已登录") : "未登录");
					accountAvatar = (loggedIn ? (((string)result["avatar"]) ?? "") : "");
					accountId = (loggedIn ? (((string)result["userId"]) ?? "") : "");
					message = (loggedIn ? "登录态由Windows当前用户加密保存" : "当前平台尚未登录");
				}
				else
				{
					message = ((string)result["message"]) ?? "已加载";
				}
				if (result["vaultWarning"] != null)
				{
					message = (string)result["vaultWarning"];
				}
			}
			catch (Exception error)
			{
				polling = false;
				message = DiagnosticLog.Report("Muonium音乐", pendingOp, error, "MuoniumHub.cs", 137);
			}
		}
		if (polling && !Busy && Time.unscaledTime >= pollAt)
		{
			pollAt = Time.unscaledTime + 3f;
			Request("poll", native);
		}
		if (!loggedIn && accountAvatar.Length == 0 && accountName == "未登录")
		{
			avatarRequested = "";
		}
	}

    internal static bool IsNative(string source) =>
        source is "ncm" or "qq" or "kugou" or "bilibili-native" or "youtube-native" or "lx-native";

    internal async Task<GdMusicApi.Resolved> ResolveNativeAudio(GdMusicApi.Track track)
    {
        var response = await client.Call("resolve", track.Source, new JObject
        {
            ["trackId"] = track.Id,
            ["track"] = new JObject
            {
                ["id"] = track.Id,
                ["title"] = track.Title,
                ["artist"] = track.Artist,
                ["album"] = track.Album
            }
        }).ConfigureAwait(false);
        var lyrics = LyricsTimeline.Parse((string?)response["lrc"], (string?)response["yrc"], (string?)response["tlyric"]);
        if (lyrics.Lines.Count == 0 && response["lines"] is JArray lines)
        {
            lyrics = LyricsTimeline.FromLines(lines.OfType<JObject>().Select(line => new LyricsLine(
                (long?)line["startMs"] ?? 0,
                (long?)line["durationMs"] ?? 0,
                (string?)line["text"] ?? "",
                (line["words"] as JArray)?.OfType<JObject>().Select(word => new LyricsWord(
                    (long?)word["startMs"] ?? 0, (long?)word["durationMs"] ?? 0, (string?)word["text"] ?? "")).ToArray(),
                (string?)line["translation"] ?? "")));
        }
        string url = (string?)response["url"] ?? "";
        string note = (bool?)response["trial"] == true ? "平台返回试听片段，需按账号权限播放" : "";
        return new GdMusicApi.Resolved(track, url, lyrics, note, (string?)response["format"] ?? "");
    }

    private float nextPrefetch;
    private int upcomingVersion = -1;
    private int upcomingMode = -1;
    private int upcomingCount;
    private int upcomingIndex;
    private readonly List<GdMusicApi.Track> upcoming = new();

    private void UpdateUpcoming()
    {
        int count = plugin.Settings.PrefetchSongs.Value;
        if (upcomingVersion == library.Version && upcomingMode == playMode && upcomingCount == count &&
            upcomingIndex == queueIndex) return;

        var shuffleOrder = playMode == ShuffleMode && upcomingMode == ShuffleMode
            ? upcoming.ToArray()
            : Array.Empty<GdMusicApi.Track>();
        upcomingVersion = library.Version;
        upcomingMode = playMode;
        upcomingCount = count;
        upcomingIndex = queueIndex;
        upcoming.Clear();
        int length = library.Queue.Count;
        if (current == null || playMode == RepeatOneMode || queueIndex < 0 || queueIndex >= length) return;

        var seen = new HashSet<string> { PlayerLibrary.Key(current) };
        IEnumerable<GdMusicApi.Track> choices = playMode == HeartMode
            ? library.Queue.Skip(queueIndex + 1)
            : Enumerable.Range(1, length - 1).Select(offset => library.Queue[(queueIndex + offset) % length]);
        if (playMode == ShuffleMode)
        {
            var available = new HashSet<string>(library.Queue.Select(PlayerLibrary.Key));
            choices = shuffleOrder.Where(track => available.Contains(PlayerLibrary.Key(track)))
                .Concat(choices.OrderBy(_ => random.Next()));
        }
        upcoming.AddRange(choices.Where(track => seen.Add(PlayerLibrary.Key(track))).Take(Math.Max(1, count)));
    }

	private void RefreshAvatar()
	{
		string text = accountAvatar;
		if (text.Length == 0)
		{
			if ((Object)(object)avatarTexture != (Object)null)
			{
				Object.Destroy((Object)(object)avatarTexture);
				avatarTexture = null;
			}
			avatarRequested = "";
		}
		else if (!(text == avatarRequested) || !((Object)(object)avatarTexture != (Object)null))
		{
			avatarTexture = AvatarCache.For(text, avatarSize) ?? avatarTexture;
			avatarRequested = text;
		}
	}

	private static GdMusicApi.Track[] ParseTracks(JArray data)
	{
		return (from t in ((IEnumerable)data).OfType<JObject>()
			select new GdMusicApi.Track(((string)t["id"]) ?? "", ((string)t["title"]) ?? "", ((string)t["artist"]) ?? "", ((string)t["album"]) ?? "", "", ((string)t["source"]) ?? "ncm", ((string)t["cover"]) ?? "") into t
			where t.Id.Length > 0
			select t).ToArray();
	}

    private readonly Dictionary<string, (JObject Data, float UsedAt)> catalogueCache = new();
    private string? pendingCacheKey;
    private bool pendingFromCache;
    private int pendingPage;
    private string pendingPlaylistId = "";
    private float closedAt = -1f;
    private float CacheLifetime => plugin.Settings.CacheMinutes.Value * 60f;

    internal void WindowOpened()
    {
        if (closedAt >= 0f && Time.unscaledTime - closedAt >= CacheLifetime)
        {
            CoverCache.Reset();
            ResetCatalogueView();
        }
        closedAt = -1f;
        if (!catalogueExpired) return;
        catalogueExpired = false;
        if (page == SearchPage && !string.IsNullOrWhiteSpace(query) && !Busy) DoSearch();
    }
    internal void WindowClosed() => closedAt = Time.unscaledTime;

    private bool catalogueExpired;

    private void ResetCatalogueView()
    {
        catalogueExpired = true;
        catalogueCache.Clear();
        libraryRows.Clear();
        playlistRows.Clear();
        libraryPlaylists = Array.Empty<JObject>();
        playlists = Array.Empty<JObject>();
        rows = Array.Empty<GdMusicApi.Track>();
        plugin.Aggregate.ClearSearchResults();
        InvalidateTrackCache();
        discoverKey = libKey = recKey = "";
        recRows = Array.Empty<GdMusicApi.Track>();
        chartRetried = playlistRetried = false;
    }

    private void StoreCatalogue(string key, JObject data)
    {
        float now = Time.unscaledTime;
        foreach (var entry in catalogueCache.Where(entry => now - entry.Value.UsedAt >= CacheLifetime).ToArray())
            catalogueCache.Remove(entry.Key);
        catalogueCache.Remove(key);
        int rowCount = CountCatalogueRows(data);
        int storedRows = catalogueCache.Values.Sum(entry => CountCatalogueRows(entry.Data));
        while (catalogueCache.Count >= 64 || (catalogueCache.Count > 0 && storedRows + rowCount > 12000))
        {
            var oldest = catalogueCache.OrderBy(entry => entry.Value.UsedAt).First();
            storedRows -= CountCatalogueRows(oldest.Value.Data);
            catalogueCache.Remove(oldest.Key);
        }
        catalogueCache[key] = (data, now);
    }

    private static int CountCatalogueRows(JObject data) =>
        (data["tracks"] as JArray)?.Count ?? (data["playlists"] as JArray)?.Count ?? 0;

    internal void InvalidateAccountCaches(string source)
    {
        if (source == "ncm" && playMode == HeartMode) ChangePlaybackMode(OrderedMode);
        foreach (string key in catalogueCache.Keys.Where(key => key.StartsWith(source + "|", StringComparison.Ordinal)).ToArray())
            catalogueCache.Remove(key);
        plugin.Aggregate.InvalidateSource(source);
        if (Source != source) return;
        libKey = recKey = "";
        libraryPlaylists = Array.Empty<JObject>();
        libraryRows.Clear();
        recRows = Array.Empty<GdMusicApi.Track>();
    }

    private void Request(string op, string source, JObject? fields = null, bool refresh = false)
    {
        if (Busy) return;

        pendingOp = op;
        pendingSource = source;
        pendingPage = page;
        pendingPlaylistId = (string?)fields?["playlistId"] ?? "";
        pendingCacheKey = op is "discover" or "library" or "playlist" or "search"
            ? source + "|" + op + "|" + (fields?.ToString(Newtonsoft.Json.Formatting.None) ?? "")
            : null;
        pendingFromCache = false;
        if (pendingCacheKey != null && !refresh &&
            catalogueCache.TryGetValue(pendingCacheKey, out var hit) &&
            Time.unscaledTime - hit.UsedAt < CacheLifetime)
        {
            pendingFromCache = true;
            catalogueCache[pendingCacheKey] = (hit.Data, Time.unscaledTime);
            pending = Task.FromResult(hit.Data);
            return;
        }
        message = "正在加载…";
        pending = client.Call(op, source, fields);
    }

	private void Activate()
	{
		plugin.Settings.Source.Value = 4;
		plugin.Settings.PlayerEnabled.Value = true;
		plugin.Aggregate.Tick(enabled: true, plugin.Settings.AggregateVolume.Value);
	}

	private void Play(GdMusicApi.Track track, bool replaceQueue = false, bool automatic = false)
	{
		if (plugin.Aggregate.PlaybackPending)
		{
			message = "正在加载音频，请稍候";
			return;
		}
        if (!automatic)
        {
            CancelAutoAdvance();
            Activate();
        }
		if (replaceQueue)
		{
			library.Queue.Clear();
			library.Queue.AddRange((rows.Length != 0) ? rows : new GdMusicApi.Track[1] { track });
			queueIndex = library.Queue.FindIndex((GdMusicApi.Track t) => PlayerLibrary.Key(t) == PlayerLibrary.Key(track));
			library.Touch();
		}
		else
		{
			int num = library.Queue.FindIndex((GdMusicApi.Track t) => PlayerLibrary.Key(t) == PlayerLibrary.Key(track));
			if (num >= 0)
			{
				queueIndex = num;
			}
			else if (library.Queue.Count < 200)
			{
				int num2 = ((queueIndex >= 0 && queueIndex < library.Queue.Count) ? (queueIndex + 1) : library.Queue.Count);
				library.Queue.Insert(num2, track);
				queueIndex = num2;
				library.Touch();
			}
			else
			{
				queueIndex = -1;
			}
		}
        plugin.Aggregate.Play(track, plugin.Settings.AggregateBitrate.Value);
    }

	private void Started(GdMusicApi.Track track)
	{
        CancelAutoAdvance();
        tryingHeartBatch = false;
        CancelSeek();
        upcomingVersion = -1;
		current = track;
        CoverCache.Preload(track.Cover);
		library.Played(track);
		queueIndex = library.Queue.FindIndex((GdMusicApi.Track t) => PlayerLibrary.Key(t) == PlayerLibrary.Key(track));
	}

	private void Ended()
	{
        if (current == null) return;
        string key = PlayerLibrary.Key(current);
        int index = library.Queue.FindIndex(track => PlayerLibrary.Key(track) == key);
        if (playMode == HeartMode)
        {
            if (heartTask != null)
            {
                endedWhileLoading = true;
                return;
            }
            autoQueue = new Queue<GdMusicApi.Track>(library.Queue.Skip(index + 1));
            advancePending = true;
            return;
        }
        if (playMode == RepeatOneMode || index < 0 || library.Queue.Count == 1)
        {
            autoQueue = new Queue<GdMusicApi.Track>(new[] { current });
        }
        else
        {
            var seen = new HashSet<string> { key };
            var choices = Enumerable.Range(1, library.Queue.Count - 1)
                .Select(offset => library.Queue[(index + offset) % library.Queue.Count]);
            if (playMode == ShuffleMode)
            {
                queueIndex = index;
                UpdateUpcoming();
                choices = upcoming.Concat(choices.OrderBy(_ => random.Next()));
            }
            autoQueue = new Queue<GdMusicApi.Track>(choices.Where(track => seen.Add(PlayerLibrary.Key(track))));
            if (autoQueue.Count == 0) autoQueue.Enqueue(current);
        }
        advancePending = true;
	}

    private void LoadFailed()
    {
        if (autoQueue != null) advancePending = true;
    }

    private void Advance()
    {
        advancePending = false;
        if (autoQueue == null) return;
        if (autoQueue.Count == 0)
        {
            CancelAutoAdvance();
            if (playMode == HeartMode && !tryingHeartBatch)
                RequestHeart(initial: false);
            else
            {
                tryingHeartBatch = false;
                message = "队列中暂时没有能续播的歌曲";
            }
            return;
        }
        Play(autoQueue.Dequeue(), automatic: true);
    }

    private void CancelAutoAdvance()
    {
        autoQueue = null;
        advancePending = false;
    }

	private void Next(int direction)
	{
        if (playMode == HeartMode)
        {
            if (heartTask != null || plugin.Aggregate.PlaybackPending) return;
            int next = queueIndex + direction;
            if (next >= 0 && next < library.Queue.Count) Play(library.Queue[next]);
            else if (direction > 0) RequestHeart(initial: false);
            return;
        }
		if (library.Queue.Count == 0)
		{
			return;
		}
		if (queueIndex < 0 || queueIndex >= library.Queue.Count)
		{
			if (current != null)
			{
				Play(current);
			}
			return;
		}
		int count = library.Queue.Count;
		int num;
        if (playMode == ShuffleMode)
        {
            UpdateUpcoming();
            if (upcoming.Count > 0)
            {
                Play(upcoming[0]);
                return;
            }
            num = queueIndex;
        }
		else
		{
			num = queueIndex + direction;
			if (num >= count)
			{
				num = 0;
			}
			if (num < 0)
			{
				num = count - 1;
			}
		}
		Play(library.Queue[num]);
	}

	private void RefreshViews()
	{
		if (viewVersion != library.Version)
		{
			viewVersion = library.Version;
			favoritesView = library.Favorites.ToArray();
			historyView = library.History.ToArray();
			queueView = library.Queue.ToArray();
			int num = Math.Min(5, historyView.Length);
			recentView = ((num == historyView.Length) ? historyView : historyView[0..num]);
		}
	}

	internal void Draw(float width, float height)
	{
		Styles();
		RefreshViews();
		if (!bootProbed)
		{
			bootProbed = true;
			if (platform != 2)
			{
				Request("account", Source);
			}
		}
		float num = ((width < 880f) ? 158f : 212f);
		float num2 = Mathf.Max(220f, height - 232f);
		float num3 = Mathf.Max(320f, width - num - 16f);
		contentWidth = num3;
		GUILayout.BeginHorizontal(new GUILayoutOption[1] { GUILayout.Height(num2) });
		Sidebar(num, num2);
		GUILayout.Space(16f);
		GUILayout.BeginVertical(new GUILayoutOption[1] { GUILayout.Width(num3) });
		Header(num3);
		scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUIStyle.none, new GUILayoutOption[2] { GUILayout.Width(num3), GUILayout.Height(Mathf.Max(120f, num2 - 96f)) });
		try
		{
			GUILayout.Space(14f);
			switch (page)
			{
			case 0:
				Home();
				break;
			case 1:
				Discover();
				break;
			case 2:
				Library();
				break;
			case 3:
				TrackRows(favoritesView, "本地收藏", "收藏的曲目会保存在本机，不受账号退出影响。");
				break;
			case 4:
				TrackRows(historyView, "播放历史", "最近播放的曲目，最多保留在本机曲库里。");
				break;
			case 5:
				Queue();
				break;
			case 6:
				Account();
				break;
			case 7:
				MusicPanel.DrawLyricsSettings();
				break;
			case SettingsPage:
				Diagnostics();
				break;
			case 9:
				Authors();
				break;
			case 12:
				Search();
				break;
			case 13:
				ChartView();
				break;
			case 14:
				PlaylistDetail();
				break;
			}
			GUILayout.Space(12f);
			GUILayout.Label(message, messageStyle, new GUILayoutOption[1] { GUILayout.Height(22f) });
		}
		catch (Exception error)
		{
			message = DiagnosticLog.Report("Muonium音乐", "绘制内容", error, "MuoniumHub.cs", 236);
		}
		GUILayout.EndScrollView();
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
		Transport(width);
	}

	private void Sidebar(float width, float height)
	{
		Rect rect = GUILayoutUtility.GetRect(width, height, new GUILayoutOption[2]
		{
			GUILayout.Width(width),
			GUILayout.Height(height)
		});
		UiKit.Round(rect, UiKit.Panel, 14);
		float num = rect.x + 16f;
		float num2 = rect.width - 32f;
		GUI.Label(new Rect(num, rect.y + 16f, num2, 38f), "MUONIUM", heading);
		GUIStyle val = new GUIStyle(small)
		{
			fontSize = 12
		};
		val.normal.textColor = UiKit.Faint;
		GUI.Label(new Rect(num, rect.y + 56f, num2, 18f), "PEAK 音乐播放器", val);
		UiKit.Fill(new Rect(num, rect.y + 82f, num2, 1f), UiKit.Divider);
		float num3 = 72f;
		float num4 = rect.y + 96f;
		float num5 = Mathf.Clamp((rect.yMax - num3 - 8f - num4 - 4f * (float)(Pages.Length - 1)) / (float)Pages.Length, 26f, 44f);
		float num6 = num4;
		for (int i = 0; i < Pages.Length; i++)
		{
			Rect rect2 = new Rect(rect.x + 8f, num6, rect.width - 16f, num5);
			if (num6 + num5 > rect.yMax - num3 - 4f)
			{
				break;
			}
			bool flag = UiKit.Hover(rect2);
			if (page == i)
			{
				UiKit.Chip(rect2, 0.62f, 9);
			}
			else if (flag)
			{
				UiKit.Chip(rect2, 0.20f, 9);
			}
			GUIStyle val2 = new GUIStyle(small)
			{
				fontSize = ((num5 >= 38f) ? 15 : ((num5 >= 32f) ? 14 : 12)),
				alignment = (TextAnchor)4,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0)
			};
			val2.normal.textColor = ((page == i) ? UiKit.Ink : (flag ? UiKit.Text : UiKit.Muted));
			// 导航文字水平居中：按文本实际尺寸算出居中矩形再画，不依赖 GUIStyle.padding 的隐式偏移
			Vector2 labelSize = val2.CalcSize(new GUIContent(Pages[i]));
			Rect labelRect = new Rect(rect2.x + (rect2.width - labelSize.x) * 0.5f, rect2.y + (rect2.height - labelSize.y) * 0.5f, labelSize.x, labelSize.y);
			GUI.Label(labelRect, Pages[i], val2);
			if (UiKit.Click(rect2))
			{
				page = i;
				scroll = Vector2.zero;
			}
			num6 += num5 + 4f;
		}
		Rect rect3 = new Rect(rect.x + 8f, rect.yMax - num3 + 4f, rect.width - 16f, 60f);
		UiKit.Round(rect3, UiKit.Hover(rect3) ? UiKit.RowHover : UiKit.Row, 12);
		Rect rect4 = new Rect(rect3.x + 10f, rect3.y + 12f, 34f, 34f);
		DrawAvatar(rect4, (accountAvatar.Length > 0) ? accountName : PlatformLabel);
		GUIStyle val3 = new GUIStyle(rowTitle)
		{
			fontSize = 14
		};
		val3.normal.textColor = UiKit.Text;
		GUI.Label(new Rect(rect3.x + 52f, rect3.y + 10f, rect3.width - 60f, 20f), Fit((accountName == "未登录") ? PlatformLabel : accountName, rect3.width - 64f), val3);
		GUIStyle val4 = new GUIStyle(small)
		{
			fontSize = 11
		};
		val4.normal.textColor = (loggedIn ? UiKit.Ok : UiKit.Faint);
		GUI.Label(new Rect(rect3.x + 52f, rect3.y + 30f, rect3.width - 60f, 18f), loggedIn ? (PlatformLabel + " · 已登录") : (PlatformLabel + " · 未登录"), val4);
		if (UiKit.Click(rect3))
		{
			page = 6;
			scroll = Vector2.zero;
			if (platform != 2)
			{
				Request("account", Source);
			}
		}
	}

	private void DrawAvatar(Rect rect, string fallback)
	{
		UiKit.Circle(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), new Color(1f, 1f, 1f, 0.16f));
		if ((Object)(object)avatarTexture != (Object)null)
		{
			Color color = GUI.color;
			GUI.color = Color.white;
			GUI.DrawTexture(rect, (Texture)(object)avatarTexture, (ScaleMode)2, true);
			GUI.color = color;
		}
		else
		{
			UiKit.Circle(rect, UiKit.Tint((fallback.Length > 0) ? fallback : "PEAK"));
			GUIStyle val = new GUIStyle(center)
			{
				fontSize = Mathf.RoundToInt(rect.height * 0.46f),
				fontStyle = (FontStyle)1
			};
			val.normal.textColor = new Color(1f, 1f, 1f, 0.92f);
			GUI.Label(rect, UiKit.Initial(fallback), val);
		}
	}

	private void Header(float width)
	{
		Rect rect = GUILayoutUtility.GetRect(width, 96f, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(96f)
		});
		GUI.Label(new Rect(rect.x, rect.y, Mathf.Max(80f, width * 0.32f - 12f), 36f), PageTitle, heading);
		if (page < 7 || page == ChartPage)
		{
			string[] array = new string[7] { "网易云", "QQ 音乐", "GD 聚合", "酷狗", "哔哩", "YouTube", "LX" };
			float num = Mathf.Min(76f, (width * 0.68f - 30f) / 7f);
			float num2 = num * 7f + 30f;
			float num3 = rect.xMax - num2;
			float num4 = rect.y + 2f;
			bool enabled = GUI.enabled;
			GUI.enabled = enabled && !Busy;
			for (int i = 0; i < 7; i++)
			{
				if (UiKit.Pill(new Rect(num3 + (float)i * (num + 5f), num4, num, 34f), style: center, label: array[i], active: platform == i, hoverable: enabled))
				{
					platform = i;
					chartTask = null;
					if (page == ChartPage)
					{
						if (i >= 2)
						{
							page = 0;
						}
						else
						{
							OpenChart(chartIndex);
						}
					}
					polling = false;
					ClearQr();
					rows = Array.Empty<GdMusicApi.Track>();
					InvalidateTrackCache();
					playlists = Array.Empty<JObject>();
					playlistRows.Clear();
					libraryPlaylists = Array.Empty<JObject>();
					libraryRows.Clear();
					libKey = "";
					recRows = Array.Empty<GdMusicApi.Track>();
					recTask = null;
					recKey = "";
					recNote = "";
					recOffset = 0;
					message = "已切换平台";
					loggedIn = false;
					accountName = "未登录";
					accountAvatar = "";
					avatarRequested = "";
					if (i != 2)
					{
						Request("account", Source);
					}
				}
			}
			GUI.enabled = enabled;
			if (platform == 2 && page < 5)
			{
				MusicSettings settings = plugin.Settings;
				GUIStyle val = new GUIStyle(small)
				{
					fontSize = 12,
					alignment = (TextAnchor)5
				};
				val.normal.textColor = UiKit.Faint;
				GUI.Label(new Rect(rect.x + width * 0.5f, rect.y + 8f, width * 0.5f - 58f, 20f), GdMusicApi.Names[settings.AggregatePlatform.Value] + "  ‹ ›", val);
				Rect rect2 = new Rect(rect.xMax - 52f, rect.y + 4f, 22f, 34f);
				Rect rect3 = new Rect(rect.xMax - 26f, rect.y + 4f, 22f, 34f);
				if (UiKit.Click(rect2))
				{
					settings.AggregatePlatform.Value = (settings.AggregatePlatform.Value + 9) % 10;
				}
				if (UiKit.Click(rect3))
				{
					settings.AggregatePlatform.Value = (settings.AggregatePlatform.Value + 1) % 10;
				}
			}
		}
		Rect rect4 = new Rect(rect.x, rect.y + 46f, Mathf.Max(160f, width - 104f), 38f);
		bool flag = UiKit.Hover(rect4);
		UiKit.Round(new Rect(rect4.x - 2f, rect4.y - 2f, rect4.width + 4f, rect4.height + 4f), UiKit.Glass(flag ? 0.22f : 0.14f), 13);
		UiKit.Round(rect4, FieldFill, 11);
		if (searchField == null)
		{
			searchField = new GUIStyle(GUI.skin.textField)
			{
				fontSize = 14,
				alignment = (TextAnchor)3,
				fixedHeight = 0f,
				fixedWidth = 0f,
				padding = new RectOffset(14, 12, 0, 0),
				clipping = (TextClipping)0
			};
			searchField.normal.background = null;
			searchField.hover.background = null;
			searchField.active.background = null;
			searchField.focused.background = null;
			searchField.normal.textColor = UiKit.Text;
			searchField.hover.textColor = UiKit.Text;
			searchField.focused.textColor = UiKit.Text;
			searchField.active.textColor = UiKit.Text;
		}
		GUI.SetNextControlName("muonium-search");
		string text = GUI.TextField(new Rect(rect4.x + 2f, rect4.y + 9f, rect4.width - 6f, 22f), query, 80, searchField);
		if (text != query)
		{
			query = text;
		}
		if (query.Length == 0)
		{
			GUIStyle val2 = new GUIStyle(small)
			{
				fontSize = 13
			};
			val2.normal.textColor = UiKit.Faint;
			GUI.Label(new Rect(rect4.x + 14f, rect4.y + 9f, rect4.width - 28f, 20f), "搜索歌名或歌手…", val2);
		}
		Rect searchRect = new Rect(rect4.xMax + 10f, rect.y + 46f, 94f, 38f);
		bool searchHover = UiKit.Hover(searchRect);
		UiKit.Chip(searchRect, searchHover ? 0.78f : 0.56f, 19);
		GUIStyle searchLabel = new GUIStyle(center)
		{
			fontSize = 13
		};
		searchLabel.normal.textColor = UiKit.Ink;
		GUI.Label(searchRect, "搜索", searchLabel);
		bool flag2 = UiKit.Click(searchRect);
		bool flag3 = (int)Event.current.type == 4 && ((int)Event.current.keyCode == 13 || (int)Event.current.keyCode == 271) && GUI.GetNameOfFocusedControl() == "muonium-search";
		if (flag2 | flag3)
		{
			searchPage = 1;
			DoSearch();
			if ((int)Event.current.type == 4)
			{
				Event.current.Use();
			}
		}
		UiKit.Fill(new Rect(rect.x, rect.yMax - 1f, width, 1f), UiKit.Divider);
	}

	private bool ActionButton(string label, float width = 0f)
	{
		bool enabled = GUI.enabled;
		GUI.enabled = enabled && !Busy;
		bool result = ((width > 0f) ? GUILayout.Button(label, card, new GUILayoutOption[2]
		{
			GUILayout.Width(width),
			GUILayout.Height(44f)
		}) : GUILayout.Button(label, card, new GUILayoutOption[1] { GUILayout.Height(44f) }));
		GUI.enabled = enabled;
		return result;
	}

	private void Home()
    {
        if(platform==6){
            SectionHeader("LX 本地脚本", "指定自己的 .js 文件，支持网易云、QQ、酷狗、酷我、咪咕曲库搜索及脚本取流。");
            GUILayout.Label("脚本路径",muted);
            plugin.Settings.LxScriptPath.Value=GUILayout.TextField(plugin.Settings.LxScriptPath.Value,2048);
            GUILayout.BeginHorizontal();
            string[] keys={"wy","tx","kg","kw","mg"};string[] names={"网易云","QQ 音乐","酷狗","酷我","咪咕"};
            for(int i=0;i<keys.Length;i++)if(ActionButton((plugin.Settings.LxChannel.Value==keys[i]?"✓ ":"")+names[i],90f))plugin.Settings.LxChannel.Value=keys[i];
            GUILayout.EndHorizontal();
            if(ActionButton("加载并检查脚本",180f))Request("lxStatus",Source);
            return;
        }
        if(platform==5){SectionHeader("YouTube 原生", "支持搜索和歌单ID；播放需本机 FFmpeg，可在设置里配置专用 HTTP 代理。");return;}
        if(platform==4){SectionHeader("哔哩哔哩原生", "在上方搜索视频音乐，支持BV/av地址；AAC音频由本机FFmpeg转换。歌单功能继续适配。");return;}
        if(platform == 3){SectionHeader("酷狗原生曲库", "Muonium原生搜索、推荐歌单、扫码登录和播放解析");if(ActionButton("加载酷狗推荐歌单", 200f))Request("discover", Source);PlaylistRows(playlistRows, ref discoverPage);return;}

		if (platform == 2)
		{
			SectionHeader("音乐榜单", "GD 聚合没有榜单，切到网易云或 QQ 音乐才有");
			EmptyState("这里没有榜单数据", "点右上角「网易云」或「QQ 音乐」再看榜单");
			return;
		}
		SectionHeader("音乐榜单", (platform == 1) ? "QQ音乐巅峰榜，点开就能直接播放" : "网易云官方四大榜单，点开就能直接播放");
		ChartGrid();
		if (platform == 0)
		{
			if (ActionButton(playMode == HeartMode ? "退出心动模式" : "开启心动模式", 150f))
				ChangePlaybackMode(playMode == HeartMode ? OrderedMode : HeartMode);
			string key = "rec:" + Source;
			if (recRows.Length == 0 && recTask == null && recKey != key)
			{
				recKey = key;
				LoadRecs();
			}
		}
		RecommendSection();
	}

	private string ChartLabel(int index)
	{
		if (index < 0 || index >= 4)
		{
			return "音乐榜单";
		}
		return (platform == 1) ? QqChartNames[index] : ChartNames[index];
	}

	private string ChartNote(int index)
	{
		if (index < 0 || index >= 4)
		{
			return "";
		}
		return (platform == 1) ? QqChartHints[index] : ChartHints[index];
	}

	private string ChartFreq(int index)
	{
		if (index < 0 || index >= 4)
		{
			return "";
		}
		return (platform == 1) ? QqChartSubs[index] : ChartSubs[index];
	}

	private int ChartPaint(int index)
	{
		return (platform == 1) ? (4 + index) : index;
	}

	private void ChartGrid()
	{
		float gap = 12f;
		float width = Mathf.Max(1f, (contentWidth - gap * 3f) / 4f);
		float height = Mathf.Min(width, 238f);
		GUILayout.Space(height);
		Rect rect = GUILayoutUtility.GetLastRect();
		if (rect.height < 8f)
		{
			rect = new Rect(rect.x, rect.y, contentWidth, height);
		}
		for (int i = 0; i < ChartNames.Length; i++)
		{
			ChartCard(new Rect(rect.x + (width + gap) * (float)i, rect.y, width, height), i);
		}
	}

	private void RecommendSection()
	{
		float num = contentWidth;
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
		{
			GUILayout.Width(num),
			GUILayout.Height(46f)
		});
		GUIStyle guiStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 16,
			fontStyle = FontStyle.Bold,
			alignment = (TextAnchor)3,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle.normal.textColor = UiKit.Text;
		GUI.Label(new Rect(rect.x, rect.y, num - 200f, 24f), "听歌兴趣推荐", guiStyle);
		GUIStyle guiStyle2 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 11,
			alignment = (TextAnchor)3,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle2.normal.textColor = UiKit.Muted;
		string subtitle = platform == 0 && recTask == null && recRows.Length > 0
			? "听歌推荐已更新，点卡片直接播放"
			: "读取你网易云账号的听歌习惯，点卡片直接播";
		GUI.Label(new Rect(rect.x, rect.y + 24f, num - 200f, 18f), subtitle, guiStyle2);
		if (recRows.Length > 12)
		{
			Rect rect3 = new Rect(rect.x + num - 30f, rect.y + 4f, 26f, 28f);
			bool flag2 = UiKit.Hover(rect3);
			UiKit.Chip(rect3, flag2 ? 0.68f : 0.4f, 14);
			GUIStyle guiStyle4 = new GUIStyle(center)
			{
				fontSize = 13
			};
			guiStyle4.normal.textColor = UiKit.Ink;
			GUI.Label(rect3, "›", guiStyle4);
			if (UiKit.Click(rect3))
			{
				recOffset += 12;
			}
		}
		GUIStyle guiStyle5 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 11,
			wordWrap = true,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle5.normal.textColor = UiKit.Faint;
		string text = null;
		if (platform != 0)
		{
			text = "听歌推荐读的是网易云账号，点右上角切到「网易云」再看";
		}
		else if (recTask != null)
		{
			text = "正在拉取你的听歌推荐…";
		}
		else if (recRows.Length == 0)
		{
			text = ((recNote.Length > 0) ? recNote : "正在按你的听歌记录拉取推荐…");
		}
		if (text != null)
		{
			Rect rect4 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
			{
				GUILayout.Width(num),
				GUILayout.Height(44f)
			});
			GUI.Label(new Rect(rect4.x, rect4.y + 6f, num, 36f), text, guiStyle5);
			return;
		}
		float num2 = 12f;
		float num3 = (num - num2 * 3f) / 4f;
		float num4 = 64f;
		float num5 = num4 * 3f + num2 * 2f;
		Rect rect5 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
		{
			GUILayout.Width(num),
			GUILayout.Height(num5)
		});
		int num6 = recRows.Length;
		int num7 = (((recOffset % num6) + num6) % num6);
		int num8 = Math.Min(12, num6);
		GUIStyle guiStyle6 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 12,
			alignment = (TextAnchor)3,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle6.normal.textColor = UiKit.Text;
		GUIStyle guiStyle7 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 10,
			alignment = (TextAnchor)3,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle7.normal.textColor = UiKit.Muted;
		for (int i = 0; i < num8; i++)
		{
			int num9 = i / 4;
			int num10 = i % 4;
			Rect rect6 = new Rect(rect5.x + (num3 + num2) * (float)num10, rect5.y + (num4 + num2) * (float)num9, num3, num4);
			GdMusicApi.Track track = recRows[(num7 + i) % num6];
			bool flag3 = UiKit.Hover(rect6);
			bool flag4 = current != null && PlayerLibrary.Key(current) == PlayerLibrary.Key(track);
			UiKit.Chip(rect6, flag4 ? 0.52f : (flag3 ? 0.42f : 0.26f), 18);
			Rect rect7 = new Rect(rect6.x + 6f, rect6.y + 6f, 52f, 52f);
			Texture2D texture2D = CoverCache.For(track.Cover, 52);
			if ((Object)(object)texture2D != (Object)null)
			{
				GUI.DrawTexture(rect7, (Texture)(object)texture2D, (ScaleMode)2, true);
			}
			else
			{
				UiKit.Circle(rect7, UiKit.Tint(track.Title));
				GUIStyle guiStyle8 = new GUIStyle(center)
				{
					fontSize = 18
				};
				guiStyle8.normal.textColor = UiKit.Ink;
				GUI.Label(rect7, "♪", guiStyle8);
			}
			GUI.Label(new Rect(rect7.xMax + 8f, rect6.y + 12f, num3 - 78f, 17f), track.Title, guiStyle6);
			GUI.Label(new Rect(rect7.xMax + 8f, rect6.y + 31f, num3 - 78f, 15f), track.Artist, guiStyle7);
			if (UiKit.Click(rect6))
			{
				PlayRecommend(track);
			}
		}
	}

	private void LoadRecs()
	{
		if (platform != 0)
		{
			recNote = "听歌推荐读的是网易云账号，先切到「网易云」";
			return;
		}
		recNote = "";
		recTask = DailyRecs.Load(client);
	}

	private void PlayFromList(GdMusicApi.Track[] list, int index)
	{
		if (list == null || list.Length == 0)
		{
			return;
		}
		int num = Mathf.Clamp(index, 0, list.Length - 1);
        if (playMode == HeartMode)
        {
            if (page == 5 && list == queueView)
            {
                Play(list[num]);
                return;
            }
            ChangePlaybackMode(OrderedMode);
        }
		library.Queue.Clear();
		library.Queue.AddRange((list.Length > 200) ? list[0..200] : list);
		library.Touch();
		queueIndex = Mathf.Min(num, library.Queue.Count - 1);
		rows = list;
		InvalidateTrackCache();
		Play(list[queueIndex]);
	}

	private void PlayRecommend(GdMusicApi.Track track)
	{
		int num = 0;
		for (int i = 0; i < recRows.Length; i++)
		{
			if (PlayerLibrary.Key(recRows[i]) == PlayerLibrary.Key(track))
			{
				num = i;
				break;
			}
		}
		PlayFromList(recRows, num);
	}

	private void ChartCard(Rect rect, int index)
	{
		bool hover = UiKit.Hover(rect);
		Texture2D art = ChartArt.For(ChartPaint(index), Mathf.RoundToInt(rect.width));
		Color tint = UiKit.Tint(ChartLabel(index));
		if ((Object)(object)art != (Object)null)
		{
			GUI.DrawTexture(rect, (Texture)(object)art, (ScaleMode)1, true);
		}
		else
		{
			UiKit.Round(rect, tint, 16);
			GUIStyle glyphStyle = new GUIStyle(center)
			{
				fontSize = Mathf.RoundToInt(rect.width * 0.34f)
			};
			glyphStyle.normal.textColor = new Color(1f, 1f, 1f, 0.35f);
			GUI.Label(rect, "♪", glyphStyle);
		}
		if (hover)
		{
			UiKit.Round(rect, UiKit.Glass(0.08f), 16);
		}
		Rect bar = new Rect(rect.x + 12f, rect.yMax - 40f, rect.width - 24f, 28f);
		UiKit.Round(bar, new Color(0.02f, 0.03f, 0.04f, hover ? 0.66f : 0.56f), 10);
		GUIStyle name = new GUIStyle(rowTitle)
		{
			fontSize = Mathf.RoundToInt(Mathf.Clamp(rect.width * 0.078f, 12f, 19f)),
			fontStyle = FontStyle.Bold,
			alignment = TextAnchor.MiddleCenter,
			clipping = (TextClipping)0
		};
		name.normal.textColor = new Color(1f, 1f, 1f, 0.97f);
		GUI.Label(bar, ChartLabel(index), name);
		GUIStyle sub = new GUIStyle(small)
		{
			fontSize = 10,
			alignment = TextAnchor.MiddleRight,
			clipping = (TextClipping)0
		};
		sub.normal.textColor = new Color(1f, 1f, 1f, hover ? 0.85f : 0.7f);
		GUI.Label(new Rect(rect.x, rect.yMax - 72f, rect.width - 14f, 18f), ChartFreq(index), sub);
		if (UiKit.Click(rect))
		{
			OpenChart(index);
		}
	}
	private void OpenChart(int index)
	{
		if (index < 0 || index >= ChartNames.Length)
		{
			return;
		}
		chartIndex = index;
		chartRetried = false;
		page = ChartPage;
		rows = Array.Empty<GdMusicApi.Track>();
		InvalidateTrackCache();
		chartTask = null;
		if (platform == 2)
		{
			message = "GD 聚合没有榜单，切到网易云或 QQ 音乐";
			return;
		}
		if (platform == 1)
		{
			chartTask = QqChart.Load(QqChartIds[index], 100);
			message = "正在拉取 QQ 音乐榜单…";
			return;
		}
		Request("playlist", "ncm", new JObject
		{
			["playlistId"] = (JToken)(ChartIds[index])
		});
	}

	private void ChartView()
	{
		if (chartIndex < 0 || chartIndex >= ChartNames.Length)
		{
			EmptyState("还没有选择榜单", "回到首页挑一个榜单打开");
			return;
		}
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		bool flag = ActionButton("‹ 返回上一页", 150f);
		GUILayout.FlexibleSpace();
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
		{
			GUILayout.Width(108f),
			GUILayout.Height(30f)
		});
		PlayAllChip(rect, rows);
		GUILayout.EndHorizontal();
		if (flag)
		{
			page = 0;
			return;
		}
		GUILayout.Space(8f);
		Hint(ChartNote(chartIndex));
		if (platform == 2)
		{
			EmptyState("GD 聚合没有榜单", "点右上角切到「网易云」或「QQ 音乐」再看榜单");
			return;
		}
		if (platform == 1)
		{
			if (rows.Length == 0 && chartTask == null && !chartRetried)
			{
				chartRetried = true;
				chartTask = QqChart.Load(QqChartIds[chartIndex], 100);
			}
		}
		else if (rows.Length == 0 && !Busy && !chartRetried)
		{
			chartRetried = true;
			Request("playlist", "ncm", new JObject
			{
				["playlistId"] = (JToken)(ChartIds[chartIndex])
			});
		}
		if (rows.Length == 0)
		{
			EmptyState("榜单正在加载", "打开即自动拉取，等几秒就出来；一直空着就检查网络或账号登录态");
			return;
		}
		TrackRows(rows, "", null);
	}
	private void Search()
	{
		if (query.Trim().Length == 0)
		{
			EmptyState("还没有搜索", "在上方搜索框输入歌名或歌手，回车或点搜索");
			return;
		}
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (ActionButton("上一页", 96f) && searchPage > 1)
		{
			searchPage--;
			DoSearch();
		}
		GUILayout.Space(8f);
		if (ActionButton("下一页", 96f))
		{
			searchPage++;
			DoSearch();
		}
		GUILayout.Space(12f);
		GUILayout.Label("第 " + searchPage + " 页", small, Array.Empty<GUILayoutOption>());
		GUILayout.FlexibleSpace();
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
		{
			GUILayout.Width(108f),
			GUILayout.Height(30f)
		});
		PlayAllChip(rect, (platform == 2) ? plugin.Aggregate.Results : rows);
		GUILayout.EndHorizontal();
		GUILayout.Space(16f);
		TrackRows((platform == 2) ? plugin.Aggregate.Results : rows, "", null);
	}

	private void DoSearch()
	{
		if (query.Trim().Length == 0)
		{
			message = "请输入歌名或歌手";
			return;
		}
		page = SearchPage;
		scroll = Vector2.zero;
		if (platform == 2)
		{
			Activate();
			plugin.Aggregate.Search(Source, query, searchPage);
		}
		else
		{
			Request("search", Source, new JObject
			{
				["query"] = (JToken)(query),
				["page"] = (JToken)(searchPage)
			});
		}
	}

	private void LoadDiscover()
	{
		if (platform == 2)
		{
			message = "GD聚合提供歌曲搜索；精选歌单请使用原生平台";
		}
		else
		{
			Request("discover", Source);
		}
	}

	private void Discover()
	{
        if(platform>=4){EmptyState("此平台暂无推荐歌单", "使用上方搜索；YouTube 可在我的歌单中输入歌单 ID");return;}
		if (platform == 2)
		{
			EmptyState("GD 聚合没有歌单", "点右上角切到「网易云」或「QQ 音乐」看推荐歌单");
			return;
		}
		string key = Source + ":" + platform;
		if (playlistRows.Count == 0 && !Busy && discoverKey != key)
		{
			discoverKey = key;
			discoverTries = 0;
			discoverAt = 0f;
		}
		if (playlistRows.Count == 0 && !Busy && discoverKey == key && discoverTries < 3 && Time.unscaledTime >= discoverAt)
		{
			discoverTries++;
			discoverAt = Time.unscaledTime + 12f;
			LoadDiscover();
		}
		if (playlistRows.Count == 0)
		{
			EmptyState("正在拉取推荐歌单", "打开这一页会自动加载，等几秒；一直空着就检查网络或账号登录态");
			return;
		}
		PlaylistRows(playlistRows, ref discoverPage);
	}

	private void Library()
	{
        if(platform>=4){
            SectionHeader("打开歌单", platform==5?"输入 YouTube 歌单 ID 或链接":"哔哩哔哩收藏夹尚未适配，请使用搜索");
            if(platform==5){manualPlaylist=GUILayout.TextField(manualPlaylist, 2048);if(ActionButton("打开歌单",140f)&&!string.IsNullOrWhiteSpace(manualPlaylist))Request("playlist",Source,new JObject{["playlistId"]=manualPlaylist});if(rows.Length>0)TrackRows(rows,"",null);}
            return;
        }
		if (platform == 2)
		{
			EmptyState("GD 聚合没有云端歌单", "点右上角切到「网易云」或「QQ 音乐」");
			return;
		}
		string key = "lib:" + Source;
		if (libraryRows.Count == 0 && !Busy && libKey != key)
		{
			libKey = key;
			libraryPlaylists = Array.Empty<JObject>();
			RebuildLibraryRows();
			Request("library", Source);
		}
		if (libraryRows.Count == 0)
		{
			EmptyState(Busy?"正在加载你的"+PlatformLabel+"歌单":"歌单未加载或列表为空",Busy?"请稍候":message);
            if (!Busy && ActionButton("重新加载我的歌单", 200f)) Request("library", Source, refresh: true);
			return;
		}
		PlaylistRows(libraryRows, ref libraryPage);
	}

	private void PlaylistDetail()
	{
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		bool flag = ActionButton("‹ 返回上一页", 150f);
		GUILayout.FlexibleSpace();
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
		{
			GUILayout.Width(108f),
			GUILayout.Height(30f)
		});
		PlayAllChip(rect, rows);
		GUILayout.EndHorizontal();
		if (flag)
		{
			page = playlistOrigin;
			return;
		}
		GUILayout.Space(8f);
		Hint((playlistTitle.Length == 0) ? "歌单打开后自动加载" : (playlistTitle + " · 打开后自动加载"));
		if (rows.Length == 0)
		{
			if (!Busy && !playlistRetried && playlistId.Length > 0)
			{
				playlistRetried = true;
				Request("playlist", Source, new JObject { ["playlistId"] = (JToken)(playlistId) });
			}
			EmptyState(Busy?"歌单正在加载":"歌单为空或加载失败",Busy?"正在向当前平台请求曲目":message);
            if(!Busy && ActionButton("重试加载歌单",180f))Request("playlist",Source,new JObject{["playlistId"]=playlistId}, refresh: true);
			return;
		}
		TrackRows(rows, "", null);
	}

	private void RebuildPlaylistRows()
	{
		playlistRows.Clear();
		foreach (JObject item in playlists.Take(60))
		{
			string id = ((string)item["id"]) ?? "";
			string name = ((string)item["name"]) ?? "歌单";
			int valueOrDefault = ((int?)item["trackCount"]).GetValueOrDefault();
			string cover = ((string)(item["picUrl"] ?? item["coverUrl"] ?? item["cover"])) ?? "";
			playlistRows.Add(new PlaylistRow(id, name, valueOrDefault, cover));
		}
	}

	private void RebuildLibraryRows()
	{
		libraryRows.Clear();
		foreach (JObject item in libraryPlaylists.Take(120))
		{
			string id = ((string)item["id"]) ?? "";
			string name = ((string)item["name"]) ?? "歌单";
			int valueOrDefault = ((int?)item["trackCount"]).GetValueOrDefault();
			string cover = ((string)(item["picUrl"] ?? item["coverUrl"] ?? item["cover"])) ?? "";
			libraryRows.Add(new PlaylistRow(id, name, valueOrDefault, cover));
		}
		libraryPage = 0;
	}

	private void PlaylistRows(List<PlaylistRow> list, ref int cursor)
	{
		if (list.Count == 0)
		{
			EmptyState("还没有歌单", "歌单会自动加载，等几秒；一直空着说明账号里没有歌单");
			return;
		}
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(1f)
		});
		// Layout and Repaint must reserve the same grid height; GetRect.width is a placeholder during Layout.
        float num = Mathf.Max(120f, (contentWidth - 20f - 36f) / 4f);
		float num2 = Mathf.Min(num, 200f);
		float num3 = num2 + 54f;
		int num5 = 50;
		int num6 = Mathf.Max(1, Mathf.CeilToInt((float)list.Count / (float)num5));
		cursor = Mathf.Clamp(cursor, 0, num6 - 1);
		int num7 = cursor * num5;
		int num8 = Mathf.Min(num5, list.Count - num7);
		int num9 = Mathf.CeilToInt((float)num8 / 4f);
		if (num6 > 1)
		{
			Rect rect4 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
			{
				GUILayout.Width(rect.width),
				GUILayout.Height(30f)
			});
			GUIStyle val6 = new GUIStyle(center)
			{
				fontSize = 13
			};
			val6.normal.textColor = UiKit.Ink;
			GUIStyle val7 = new GUIStyle(center)
			{
				fontSize = 11
			};
			val7.normal.textColor = UiKit.Muted;
			GUI.Label(new Rect(rect4.x, rect4.y + 6f, 260f, 18f), "使用上方箭头翻页 · 共 " + list.Count + " 个歌单", val7);
			Rect rect5 = new Rect(rect4.xMax - 74f, rect4.y + 1f, 28f, 28f);
			UiKit.Chip(rect5, (float)((cursor < num6 - 1) ? (UiKit.Hover(rect5) ? 0.66f : 0.44f) : 0.24f), 14);
			GUI.Label(rect5, "›", val6);
			GUI.Label(new Rect(rect5.x - 88f, rect4.y + 1f, 80f, 28f), (cursor + 1) + " / " + num6, val7);
			Rect rect6 = new Rect(rect5.x - 116f, rect4.y + 1f, 28f, 28f);
			UiKit.Chip(rect6, (float)((cursor > 0) ? (UiKit.Hover(rect6) ? 0.66f : 0.44f) : 0.24f), 14);
			GUI.Label(rect6, "‹", val6);
			if (cursor > 0 && UiKit.Click(rect6))
			{
				cursor--;
				scroll = Vector2.zero;
				lastScrollY = 0f;
			}
			if (cursor < num6 - 1 && UiKit.Click(rect5))
			{
				cursor++;
				scroll = Vector2.zero;
				lastScrollY = 0f;
			}
		}
		Rect rect2 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height((float)num9 * num3 + 12f * (float)(num9 - 1))
		});
		for (int i = 0; i < num8; i++)
		{
			PlaylistRow playlistRow = list[num7 + i];
			int num10 = i % 4;
			int num11 = i / 4;
			Rect rect3 = new Rect(rect2.x + (float)num10 * (num + 12f), rect2.y + (float)num11 * (num3 + 12f), num, num3);
			bool flag = UiKit.Hover(rect3);
			Rect val = new Rect(rect3.x + (num - num2) * 0.5f, rect3.y, num2, num2);
			Texture2D val2 = CoverCache.For(playlistRow.Cover, Mathf.RoundToInt(num2));
			if ((Object)(object)val2 != (Object)null)
			{
				GUI.DrawTexture(val, (Texture)(object)val2, (ScaleMode)1, true);
			}
			else
			{
				UiKit.Round(val, UiKit.Tint(playlistRow.Name), 10);
				GUIStyle val3 = new GUIStyle(center)
				{
					fontSize = Mathf.RoundToInt(num2 * 0.26f)
				};
				val3.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
				GUI.Label(val, "♪", val3);
			}
			if (flag)
			{
				UiKit.Round(new Rect(rect3.x, rect3.y, rect3.width, rect3.height), UiKit.Glass(0.045f), 10);
			}
			GUIStyle val4 = new GUIStyle(rowTitle)
			{
				fontSize = 14
			};
			val4.normal.textColor = (Color)(flag ? UiKit.Text : new Color(UiKit.Text.r, UiKit.Text.g, UiKit.Text.b, 0.92f));
			GUI.Label(new Rect(rect3.x + 2f, val.yMax + 8f, rect3.width - 4f, 20f), Fit(playlistRow.Name, rect3.width - 8f), val4);
			GUIStyle val5 = new GUIStyle(small)
			{
				fontSize = 11
			};
			val5.normal.textColor = UiKit.Faint;
			GUI.Label(new Rect(rect3.x + 2f, val.yMax + 28f, rect3.width - 4f, 16f), (playlistRow.Count > 0) ? (playlistRow.Count + " 首曲目") : "歌单", val5);
			if (UiKit.Click(rect3))
			{
				playlistId = playlistRow.Id;
				playlistTitle = playlistRow.Name;
				playlistOrigin = page;
				playlistRetried = false;
				page = PlaylistPage;
				scroll = Vector2.zero;
				rows = Array.Empty<GdMusicApi.Track>();
				InvalidateTrackCache();
				Request("playlist", Source, new JObject { ["playlistId"] = (JToken)(playlistRow.Id) });
			}
		}
		GUILayout.Space(120f);
		lastScrollY = scroll.y;
	}

	private void Queue()
	{
		Hint((queueView.Length != 0) ? (queueView.Length + " 首曲目 · 上限 200") : "队列为空，先搜索歌曲或打开歌单");
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (ActionButton("清空队列", 120f))
		{
            if (playMode == HeartMode) ChangePlaybackMode(OrderedMode);
			library.Queue.Clear();
			queueIndex = -1;
			library.Touch();
			RefreshViews();
		}
		GUILayout.FlexibleSpace();
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
		{
			GUILayout.Width(108f),
			GUILayout.Height(30f)
		});
		PlayAllChip(rect, queueView);
		GUILayout.EndHorizontal();
		GUILayout.Space(14f);
		TrackRows(queueView, "", null);
	}

	private float MiniSlider(string label, float value, float min, float max, float step)
	{
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(34f)
		});
		float labelW = Mathf.Min(120f, rect.width * 0.3f);
		float valueW = 56f;
		GUIStyle text = new GUIStyle(small ?? GUI.skin.label)
		{
			fontSize = 12,
			alignment = TextAnchor.MiddleLeft,
			clipping = TextClipping.Clip
		};
		text.normal.textColor = UiKit.Text;
		GUI.Label(new Rect(rect.x, rect.y, labelW, rect.height), label, text);
		Rect track = new Rect(rect.x + labelW + 10f, rect.y + (rect.height - 22f) * 0.5f, Mathf.Max(40f, rect.width - labelW - valueW - 20f), 22f);
		float knob = GUI.skin.horizontalSliderThumb.fixedWidth;
		UiKit.Round(new Rect(track.x + knob * 0.5f, track.center.y - 3f, Mathf.Max(0f, track.width - knob), 6f), UiKit.Divider, 3);
		GUIStyle bare = new GUIStyle(GUI.skin.horizontalSlider)
		{
			fixedHeight = 22f,
			margin = new RectOffset(),
			padding = new RectOffset(),
			border = new RectOffset()
		};
		bare.normal.background = null;
		bare.hover.background = null;
		bare.active.background = null;
		bare.focused.background = null;
		float dragged = GUI.HorizontalSlider(track, Mathf.Clamp(value, min, max), min, max, bare, GUI.skin.horizontalSliderThumb);
		float snapped = Mathf.Clamp(Mathf.Round(dragged / step) * step, min, max);
		GUIStyle readout = new GUIStyle(text)
		{
			alignment = TextAnchor.MiddleRight,
			fontSize = 13
		};
		GUI.Label(new Rect(rect.xMax - valueW, rect.y, valueW, rect.height), Mathf.RoundToInt(snapped).ToString(), readout);
		return snapped;
	}

	private void Diagnostics()
    {
        SectionHeader("角色视角", "进入地图时使用第一人称。第二人称从角色前方回看，第三人称从背后跟随。");
        GUILayout.BeginHorizontal();
        for (int mode = 0; mode < PlayerPerspective.Names.Length; mode++)
        {
            string label = PlayerPerspective.Names[mode];
            if (ActionButton((plugin.Perspective.Mode == mode ? "✓ " : "") + label, 116f))
                plugin.Perspective.Mode = mode;
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        string binding = plugin.CapturingPerspectiveKey ? "请按下新的按键" : "切换键 · " + plugin.PerspectiveKeyName;
        if (ActionButton(binding, 160f))
            plugin.CapturingPerspectiveKey = !plugin.CapturingPerspectiveKey;
        if (ActionButton("清空按键", 104f))
        {
            plugin.Settings.PerspectiveKey.Value = UnityEngine.InputSystem.Key.None;
            plugin.CapturingPerspectiveKey = false;
        }
        GUILayout.EndHorizontal();
        if (plugin.CapturingPerspectiveKey)
            Hint("Esc 取消录入。右 Shift 和反斜杠已用于播放器。");
        GUILayout.Space(12f);
        if (ActionButton(plugin.Settings.IdleCameraEnabled.Value ? "闲置运镜 · 已开启" : "闲置运镜 · 已关闭", 160f))
            plugin.Settings.IdleCameraEnabled.Value = !plugin.Settings.IdleCameraEnabled.Value;
        if (plugin.Settings.IdleCameraEnabled.Value)
        {
            plugin.Settings.IdleCameraSeconds.Value = (int)MiniSlider("等待秒数", plugin.Settings.IdleCameraSeconds.Value, 10, 180, 1);
            Hint("按设定时间连续无操作后开始运镜。移动鼠标、按键或操作手柄即可恢复当前视角。");
            Hint("打开设置前的运镜状态 · " + plugin.Perspective.IdleStatus);
        }
        GUILayout.Space(18f);
        SectionHeader("地图热门评论", "关闭时立即隐藏地图上的评论。");
        if (ActionButton(plugin.Settings.WorldCommentsEnabled.Value ? "热门评论 · 已开启" : "热门评论 · 已关闭", 160f))
            plugin.Settings.WorldCommentsEnabled.Value = !plugin.Settings.WorldCommentsEnabled.Value;
        GUILayout.Space(18f);
        SectionHeader("缓存", $"当前已缓存 {plugin.Aggregate.CachedSongCount} 首。队列会提前加载后续歌曲。");
        plugin.Settings.CacheMinutes.Value = (int)MiniSlider("保留分钟", plugin.Settings.CacheMinutes.Value, 1, 60, 1);
        plugin.Settings.PrefetchSongs.Value = (int)MiniSlider("预缓存首数", plugin.Settings.PrefetchSongs.Value, 0, 6, 1);
        GUILayout.Space(18f);
		SectionHeader("界面颜色", "调色盘选的颜色会应用到强调色、悬停高亮和玻璃卡片底色；留空是默认玻璃白。");
		ThemePicker.Draw(plugin, contentWidth);
		GUILayout.Space(18f);
		SectionHeader("小窗播放器位置", "水平百分比 0 最左、50 居中、100 最右；垂直位置从屏幕顶部计。");
		float posX = MiniSlider("水平 %", plugin.Settings.MiniX.Value, 0f, 100f, 1f);
		if (Math.Abs(posX - plugin.Settings.MiniX.Value) > 0.01f)
		{
			plugin.Settings.MiniX.Value = posX;
		}
		GUILayout.Space(10f);
		float posY = MiniSlider("距顶部", plugin.Settings.MiniY.Value, 0f, 400f, 1f);
		if (Math.Abs(posY - plugin.Settings.MiniY.Value) > 0.01f)
		{
			plugin.Settings.MiniY.Value = posY;
		}
		GUILayout.Space(12f);
		if (ActionButton("恢复默认位置", 132f))
		{
			plugin.Settings.MiniX.Value = 50f;
			plugin.Settings.MiniY.Value = 14f;
		}
		GUILayout.Space(18f);
		SectionHeader("环境音效融合", "刮风、下雨、暴雪起来时把音乐压低、变闷，环境过去自己淡回来。");
		Rect rectMix = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(44f)
		});
		bool mixOn = plugin.Settings.AmbienceMix.Value;
		if (UiKit.Pill(new Rect(rectMix.x, rectMix.y, 236f, 40f), mixOn ? "环境音融合 · 已开启" : "环境音融合 · 已关闭", new GUIStyle(center)
		{
			fontSize = 13
		}, mixOn))
		{
			plugin.Settings.AmbienceMix.Value = !mixOn;
		}
		GUILayout.Space(10f);
		Hint("检测状态：" + AmbienceMix.StatusText + " · 天气：" + AmbienceMix.KindText + " · 音乐保留 " + Mathf.RoundToInt(AmbienceMix.MusicScale * 100f) + "%" + (AmbienceMix.Indoor > 0.5f ? " · 已躲进室内/遮蔽" : ""));
		GUILayout.Space(8f);
		SectionHeader("播放器线路", "YouTube 与哔哩哔哩需要本机 FFmpeg 或专用代理。");
        GUILayout.Label("FFmpeg 路径（可留空自动检测）",muted);
        plugin.Settings.FfmpegPath.Value=GUILayout.TextField(plugin.Settings.FfmpegPath.Value,2048);
        GUILayout.Label("YouTube 专用 HTTP 代理（留空直连，修改后重启游戏生效）",muted);
        plugin.Settings.YouTubeProxy.Value=GUILayout.TextField(plugin.Settings.YouTubeProxy.Value,2048);
        GUILayout.Space(12f);
		Hint("音乐开关与歌词显示开关互相独立");
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(44f)
		});
		bool value = plugin.Settings.PlayerEnabled.Value;
		if (UiKit.Pill(new Rect(rect.x, rect.y, 220f, 40f), value ? "游戏内播放器 · 已开启" : "游戏内播放器 · 已关闭", new GUIStyle(center)
		{
			fontSize = 13
		}, value))
		{
			plugin.Settings.PlayerEnabled.Value = !value;
		}
		GUILayout.Space(10f);
		GUILayout.Label("当前只运行Muonium原生曲库与GD聚合，不启动旧桌面播放器接口。", muted, Array.Empty<GUILayoutOption>());
		GUILayout.Space(14f);
		string snapshot = DiagnosticLog.Snapshot;
		GUILayout.Label((snapshot.Length > 0) ? snapshot : "当前没有最近错误", muted, Array.Empty<GUILayoutOption>());
		GUILayout.Space(12f);
		if (ActionButton("复制错误详情", 150f))
		{
			GUIUtility.systemCopyBuffer = snapshot;
		}
	}

	private void Hint(string text)
	{
		GUILayout.Label(text, muted, Array.Empty<GUILayoutOption>());
		GUILayout.Space(10f);
	}

	private void PlayAllChip(Rect rect, GdMusicApi.Track[] list)
	{
		if (list == null || list.Length == 0)
		{
			return;
		}
		bool flag = UiKit.Hover(rect);
		UiKit.Chip(rect, (float)(flag ? 0.74f : 0.52f), 14);
		GUIStyle guiStyle = new GUIStyle(center)
		{
			fontSize = 12,
			fontStyle = FontStyle.Bold
		};
		guiStyle.normal.textColor = UiKit.Ink;
		GUI.Label(rect, "▶ 播放全部", guiStyle);
		if (UiKit.Click(rect))
		{
			PlayFromList(list, 0);
			message = "播放全部 · 共 " + list.Length + " 首";
		}
	}

	private void Authors()
	{
		Hint("PEAK DangoMusic · Muonium 音乐播放器");
		GUILayout.Space(16f);
		int count = AuthorNames.Length;
		float gap = 18f;
		float cardWidth = (contentWidth - gap) * 0.5f;
		for (int row = 0; row < (count + 1) / 2; row++)
		{
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			for (int col = 0; col < 2; col++)
			{
				int index = row * 2 + col;
				if (col > 0)
				{
					GUILayout.Space(gap);
				}
				if (index >= count)
				{
					GUILayout.Space(cardWidth);
					continue;
				}
				Rect rect = GUILayoutUtility.GetRect(cardWidth, 200f, new GUILayoutOption[2]
				{
					GUILayout.Width(cardWidth),
					GUILayout.Height(200f)
				});
				AuthorCard(rect, index);
			}
			GUILayout.EndHorizontal();
			GUILayout.Space(gap);
		}
		GUILayout.Label("作者的话：感谢三位老师的鼎力相助，同时也要感谢茉莉出击 - 锦鲤 - JY - 言非...等用户的大力支持，以及各位的使用和反馈，希望悬浮歌词能给大家带来不一样的爬山旅程。", authorNoteStyle,
			GUILayout.Width(contentWidth));
	}

	private void AuthorCard(Rect rect, int index)
	{
		bool flag = UiKit.Hover(rect);
		UiKit.Chip(rect, (float)(flag ? 0.62f : 0.46f), 18);
		Rect rect2 = new Rect(rect.x + 26f, rect.y + 30f, 96f, 96f);
		UiKit.Circle(new Rect(rect2.x - 3f, rect2.y - 3f, rect2.width + 6f, rect2.height + 6f), new Color(1f, 1f, 1f, 0.18f));
		Texture2D texture2D = AuthorArt.For(index, 96);
		if ((Object)(object)texture2D != (Object)null)
		{
			Color color = GUI.color;
			GUI.color = Color.white;
			GUI.DrawTexture(rect2, (Texture)(object)texture2D, (ScaleMode)2, true);
			GUI.color = color;
		}
		else
		{
			UiKit.Circle(rect2, UiKit.Tint(AuthorNames[index]));
			GUIStyle guiStyle = new GUIStyle(center)
			{
				fontSize = 30,
				fontStyle = FontStyle.Bold
			};
			guiStyle.normal.textColor = UiKit.Ink;
			GUI.Label(rect2, UiKit.Initial(AuthorNames[index]), guiStyle);
		}
		GUIStyle guiStyle2 = new GUIStyle(heading)
		{
			fontSize = 26,
			fontStyle = FontStyle.Bold
		};
		guiStyle2.normal.textColor = UiKit.Ink;
		GUI.Label(new Rect(rect.x + 144f, rect.y + 46f, rect.width - 168f, 34f), AuthorNames[index], guiStyle2);
		GUIStyle guiStyle3 = new GUIStyle(small)
		{
			fontSize = 12,
			wordWrap = true
		};
		guiStyle3.normal.textColor = UiKit.Faint;
		GUI.Label(new Rect(rect.x + 144f, rect.y + 84f, rect.width - 168f, 40f), AuthorRoles[index], guiStyle3);
		UiKit.Fill(new Rect(rect.x + 26f, rect.y + 148f, rect.width - 52f, 1f), UiKit.Divider);
		GUIStyle guiStyle4 = new GUIStyle(small)
		{
			fontSize = 12
		};
		guiStyle4.normal.textColor = UiKit.Muted;
		GUI.Label(new Rect(rect.x + 26f, rect.y + 160f, rect.width - 52f, 20f), "点击打开抖音主页", guiStyle4);
		if (UiKit.Click(rect))
		{
			Application.OpenURL(AuthorUrls[index]);
			message = "正在打开 " + AuthorNames[index] + " 的抖音主页";
		}
	}

	private void SectionHeader(string title, string? hint)
	{
		GUILayout.Label(title, bigHeading, Array.Empty<GUILayoutOption>());
		if (hint != null && hint.Length > 0)
		{
			GUILayout.Label(hint, muted, Array.Empty<GUILayoutOption>());
		}
		GUILayout.Space(10f);
	}

	private void EmptyState(string title, string hint)
	{
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(96f)
		});
		UiKit.Round(rect, UiKit.PanelRaised, 12);
		GUIStyle val = new GUIStyle(center)
		{
			fontSize = 14
		};
		val.normal.textColor = UiKit.Muted;
		GUI.Label(new Rect(rect.x, rect.y + 26f, rect.width, 22f), title, val);
		GUIStyle val2 = new GUIStyle(center)
		{
			fontSize = 12
		};
		val2.normal.textColor = UiKit.Faint;
		GUI.Label(new Rect(rect.x, rect.y + 52f, rect.width, 20f), hint, val2);
	}

	private void TrackRows(GdMusicApi.Track[] tracks, string title, string? hint)
	{
		if (title.Length > 0)
		{
			Rect rect0 = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
			{
				GUILayout.Width(contentWidth),
				GUILayout.Height(1f)
			});
			PlayAllChip(new Rect(rect0.xMax - 112f, rect0.y + 2f, 108f, 30f), tracks);
			SectionHeader(title, hint);
		}
		if (tracks.Length == 0)
		{
			EmptyState("还没有歌曲", "搜索歌曲、打开歌单，或把喜欢的曲目加入队列");
			return;
		}
		int num = Math.Min(tracks.Length, 60);
		float num2 = 76f;
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height((float)num * num2)
		});
		float num3 = Mathf.Max(120f, rect.width - 78f - 62f - 12f);
		RebuildTrackCache(tracks, num, num3);
		for (int i = 0; i < num; i++)
		{
			GdMusicApi.Track track = tracks[i];
			float num4 = rect.y + (float)i * num2;
			Rect rect2 = new Rect(rect.x, num4, rect.width, 70f);
			Rect rect3 = new Rect(rect.x, num4, num3, 70f);
			bool num5 = current != null && PlayerLibrary.Key(current) == PlayerLibrary.Key(track);
			UiKit.Round(color: UiKit.Hover(rect2) ? UiKit.RowHover : ((i % 2 == 0) ? UiKit.Row : UiKit.RowAlt), rect: rect2, radius: 10);
			if (num5)
			{
				UiKit.Fill(new Rect(rect2.x + 6f, rect2.y + 14f, 3f, rect2.height - 28f), UiKit.Accent);
			}
			Rect val = new Rect(rect2.x + 14f, num4 + 12f, 46f, 46f);
			Texture2D val2 = CoverCache.For(track.Cover, 46);
			if ((Object)(object)val2 != (Object)null)
			{
				GUI.DrawTexture(val, (Texture)(object)val2, (ScaleMode)1, true);
			}
			else
			{
				UiKit.Round(val, UiKit.Tint(track.Title));
				GUIStyle val3 = new GUIStyle(center)
				{
					fontSize = 15
				};
				val3.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
				GUI.Label(val, "♪", val3);
			}
			GUI.Label(new Rect(val.xMax + 12f, num4 + 12f, num3 - 84f, 24f), (trackLabels.Count > i) ? SplitTitle(trackLabels[i]) : Fit(track.Title, num3 - 84f), rowTitle);
			GUIStyle val4 = new GUIStyle(rowMeta)
			{
				fontSize = 12
			};
			GUI.Label(new Rect(val.xMax + 12f, num4 + 38f, num3 - 84f, 20f), (trackLabels.Count > i) ? SplitMeta(trackLabels[i]) : track.Artist, val4);
			bool flag = library.Favorite(track);
			Rect val5 = new Rect(rect.x + num3 + 6f, num4 + 14f, 78f, 42f);
			Rect val6 = new Rect(val5.xMax + 6f, num4 + 14f, 62f, 42f);
			UiKit.Chip(val5, flag ? 0.60f : 0.30f, 9);
			GUIStyle val7 = new GUIStyle(center)
			{
				fontSize = 12,
				fontStyle = (FontStyle)1
			};
			val7.normal.textColor = (flag ? UiKit.Ink : UiKit.Muted);
			GUI.Label(val5, flag ? "已收藏" : "收藏", val7);
			UiKit.Round(val6, UiKit.PanelRaised, 9);
			GUIStyle val8 = new GUIStyle(center)
			{
				fontSize = 12
			};
			val8.normal.textColor = UiKit.Muted;
			GUI.Label(val6, "加入队列", val8);
			if (UiKit.Click(val5))
			{
				library.Toggle(track);
				message = (flag ? "已取消收藏" : "已加入收藏");
			}
			else if (UiKit.Click(val6))
			{
				if (library.Queue.Count < 200)
				{
					library.Queue.Add(track);
					library.Touch();
					message = "已加入播放队列";
				}
				else
				{
					message = "播放队列已满（200 首）";
				}
			}
			else if (UiKit.Click(rect3))
			{
				PlayFromList(tracks, i);
			}
		}
		if (tracks.Length > 60)
		{
			GUILayout.Space(6f);
			GUILayout.Label("已显示前 " + 60 + " 首；用搜索分页查看其余结果", muted, Array.Empty<GUILayoutOption>());
		}
	}

	private static string SplitTitle(string label)
	{
		int num = label.IndexOf('\n');
		if (num >= 0)
		{
			return label.Substring(0, num);
		}
		return label;
	}

	private static string SplitMeta(string label)
	{
		int num = label.IndexOf('\n');
		if (num >= 0)
		{
			return label.Substring(num + 1);
		}
		return "";
	}

	private void RebuildTrackCache(GdMusicApi.Track[] tracks, int count, float width)
	{
		if (cachedTracks != tracks || cachedCount != count || !(Mathf.Abs(cachedLabelWidth - width) < 0.5f) || cachedLibraryVersion != library.Version)
		{
			cachedTracks = tracks;
			cachedCount = count;
			cachedLabelWidth = width;
			cachedLibraryVersion = library.Version;
			trackLabels.Clear();
			for (int i = 0; i < count; i++)
			{
				trackLabels.Add(TrackLabel(tracks[i], width));
			}
		}
	}

	private void InvalidateTrackCache()
	{
		cachedTracks = Array.Empty<GdMusicApi.Track>();
		cachedCount = -1;
		trackLabels.Clear();
	}

	private static string TrackLabel(GdMusicApi.Track track, float width)
	{
		float width2 = Mathf.Max(40f, width - 64f);
		string text;
		if (track.Artist.Length <= 0)
		{
			text = track.Album;
		}
		else
		{
			text = ((track.Album.Length > 0) ? (track.Artist + " · " + track.Album) : track.Artist);
		}
		return Fit(track.Title, width2) + "\n" + Fit(text, width2);
	}

	private static readonly Dictionary<string, string> fitCache = new Dictionary<string, string>();

	private static string Fit(string text, float width)
	{
		string key = Mathf.RoundToInt(width) + "|" + text;
		if (fitCache.TryGetValue(key, out string cached))
		{
			return cached;
		}
		string text2 = FitUncached(text, width);
		if (fitCache.Count > 1500)
		{
			fitCache.Clear();
		}
		fitCache[key] = text2;
		return text2;
	}

	private static string FitUncached(string text, float width)
	{
		GUIStyle val = FitStyle;
		if (text.Length == 0 || val.CalcSize(new GUIContent(text)).x <= width)
		{
			return text;
		}
		int num = 0;
		int num2 = text.Length;
		while (num < num2)
		{
			int num3 = (num + num2 + 1) / 2;
			if (val.CalcSize(new GUIContent(text.Substring(0, num3) + "…")).x <= width)
			{
				num = num3;
			}
			else
			{
				num2 = num3 - 1;
			}
		}
		if (num > 0)
		{
			return text.Substring(0, num).TrimEnd() + "…";
		}
		return "…";
	}

	private static string CookieValue(string cookie, string name)
	{
		foreach (string part in cookie.Split(new char[2] { ';', '\n' }))
		{
			string text = part.Trim();
			int index = text.IndexOf('=');
			if (index > 0 && string.Equals(text.Substring(0, index).Trim(), name, StringComparison.OrdinalIgnoreCase))
			{
				return text.Substring(index + 1).Trim();
			}
		}
		return "";
	}

	private string manualPlaylist = "";
    private string youtubeCookie = "";
    private string qqCookie = "";
    private bool qqWechat;
    internal Task<JObject> CommentsFor(GdMusicApi.Track track)=>client.Call("comments",track.Source,new JObject{["trackId"]=track.Id});

	private void Account()
	{
		if (platform == 6){EmptyState("LX 使用本地脚本", "脚本路径和渠道在首页配置；账号登录请使用对应原生平台");return;}
        if (platform == 2)
		{
			EmptyState("GD 聚合不需要登录", "网易云 / QQ 账号请在各原生平台标签下管理");
			return;
		}
		Hint("曲库与登录都由本地桥接组件处理，账号信息只留在本机的加密文件里，不会上传。");
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(132f)
		});
		UiKit.Round(rect, UiKit.PanelRaised, 14);
		Rect rect2 = new Rect(rect.x + 24f, rect.y + 26f, 80f, 80f);
		DrawAvatar(rect2, loggedIn ? accountName : PlatformLabel);
		GUIStyle val = new GUIStyle(heading)
		{
			fontSize = 22
		};
		val.normal.textColor = (loggedIn ? UiKit.Text : UiKit.Muted);
		GUI.Label(new Rect(rect.x + 124f, rect.y + 26f, rect.width - 160f, 34f), (accountName == "未登录") ? "未登录" : Fit(accountName, rect.width - 170f), val);
		Rect val2 = new Rect(rect.x + 124f, rect.y + 68f, 96f, 24f);
		UiKit.Chip(val2, 0.52f, 12);
		GUIStyle val3 = new GUIStyle(center)
		{
			fontSize = 11,
			fontStyle = (FontStyle)1
		};
		val3.normal.textColor = UiKit.Text;
		GUI.Label(val2, PlatformLabel, val3);
		Rect val4 = new Rect(val2.xMax + 8f, rect.y + 68f, 96f, 24f);
		UiKit.Round(val4, (Color)(loggedIn ? new Color(0.16f, 0.34f, 0.26f, 1f) : UiKit.Panel), 12);
		GUIStyle val5 = new GUIStyle(center)
		{
			fontSize = 11,
			fontStyle = (FontStyle)1
		};
		val5.normal.textColor = (loggedIn ? UiKit.Ok : UiKit.Faint);
		GUI.Label(val4, loggedIn ? "已登录" : "未登录", val5);
		GUIStyle val6 = new GUIStyle(small)
		{
			fontSize = 11
		};
		val6.normal.textColor = UiKit.Faint;
		GUI.Label(new Rect(rect.x + 124f, rect.y + 98f, rect.width - 160f, 18f), (loggedIn && accountId.Length > 0) ? ("UID " + accountId) : "扫码登录后显示头像、昵称与账号状态", val6);
		GUILayout.Space(16f);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
        if(platform==1){
            if(ActionButton(qqWechat?"登录方式：微信（点击切换QQ）":"登录方式：QQ（点击切换微信）",260f)){qqWechat=!qqWechat;polling=false;ClearQr();}
        }
		if (platform < 5 && ActionButton("生成登录二维码", 160f))
		{
			polling = false;
			ClearQr();
			Request("qr", Source,new JObject{["loginMethod"]=qqWechat?"wechat":"qq"});
		}
		GUILayout.Space(10f);
		if (ActionButton("刷新账号状态", 150f))
		{
			Request("account", Source);
		}
		GUILayout.Space(10f);
		if (ActionButton("退出此账号", 130f))
		{
			polling = false;
			ClearQr();
			Request("logout", Source);
			accountAvatar = "";
			avatarRequested = "";
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(14f);
		if(platform==5){
            GUILayout.Label("导入本人浏览器 Cookie（输入隐藏，仅保存在本机加密账户文件）",muted);
            youtubeCookie=GUILayout.PasswordField(youtubeCookie, '●', 65536);
            if(ActionButton("导入并验证 Cookie",180f)&&youtubeCookie.Length>0){Request("loginCookie",Source,new JObject{["cookie"]=youtubeCookie});youtubeCookie="";}
        }
        if(platform==1){
            GUILayout.Label("备用方式：可导入本人 QQ 音乐网页 Cookie，支持 QQ / 微信登录态；凭证仅保存在本机加密账户文件。",muted);
            qqCookie=GUILayout.PasswordField(qqCookie, '●', 65536);
            if(ActionButton("用 Cookie 登录 QQ 音乐",210f)&&qqCookie.Length>0){
                string uin=CookieValue(qqCookie,"uin");if(uin.Length==0)uin=CookieValue(qqCookie,"wxuin");
                if(uin.StartsWith("o",StringComparison.OrdinalIgnoreCase)&&uin.Length>1&&uin.Skip(1).All(char.IsDigit))uin=uin.Substring(1);
                string key=CookieValue(qqCookie,"qm_keyst");
                if(key.Length==0)key=CookieValue(qqCookie,"qqmusic_key");if(key.Length==0)key=CookieValue(qqCookie,"wxmusickey");
                if(uin.Length==0||key.Length==0)message="Cookie 里没有 uin 或 qm_keyst，重新复制一次";
                else{
                    Request("restore",Source,new JObject{["credentials"]=new JObject{["qqId"]=uin,["qqKey"]=key,["qqLoginType"]=(qqWechat||CookieValue(qqCookie,"wxmusickey").Length>0)?1:0}});
                    message="正在用 Cookie 登录 QQ 音乐…";
                }
                qqCookie="";
            }
            GUILayout.Space(10f);
        }
        GUILayout.Label("使用对应平台手机 App 扫码确认；退出登录会清除本模组保存的该平台登录态，Windows 加密文件随即更新。", muted, Array.Empty<GUILayoutOption>());
		if ((Object)(object)qr != (Object)null)
		{
			GUILayout.Space(16f);
			Rect rect3 = GUILayoutUtility.GetRect(300f, 300f, new GUILayoutOption[2]
			{
				GUILayout.Width(300f),
				GUILayout.Height(300f)
			});
			UiKit.Round(new Rect(rect3.x - 12f, rect3.y - 12f, rect3.width + 24f, rect3.height + 24f), UiKit.PanelRaised, 14);
			GUI.DrawTexture(rect3, (Texture)(object)qr, (ScaleMode)2);
			GUILayout.Space(12f);
			GUILayout.Label("二维码 3 分钟内有效；当前状态：" + message, small, Array.Empty<GUILayoutOption>());
		}
	}

    internal bool HasMiniTrack => plugin.Aggregate.Snapshot.DurationMs > 0;
    internal float MiniWidth(){
        Styles();
        string title=current?.Title??"未播放 · 右Shift 打开播放器";
        float w=rowTitle!=null?rowTitle.CalcSize(new GUIContent(title)).x:200f;
        return Mathf.Clamp(78f+w,244f,560f);
    }
    internal void DrawMini(Rect area){
        Styles();
        int radius=(int)Mathf.Min(area.height*0.5f,26f);
        UiKit.Shadow(new Rect(area.x,area.y,area.width,area.height),radius,1f);
        UiKit.Round(area,UiKit.Glass(0.92f),radius);
        Rect cover=new Rect(area.x+10,area.y+(area.height-42f)*0.5f,42,42);
        var texture=current!=null?CoverCache.For(current.Cover,42):null;
        UiKit.Round(cover,UiKit.GlassLift(0.55f),12);
        if(texture!=null)GUI.DrawTexture(cover,texture,ScaleMode.ScaleToFit,true);
        else GUI.Label(cover,"♪",center);
        GUIStyle name=new GUIStyle(rowTitle!=null?rowTitle:GUI.skin.label){
            alignment=TextAnchor.MiddleLeft, clipping=TextClipping.Clip
        };
        GUI.Label(new Rect(cover.xMax+10,area.y,area.xMax-(cover.xMax+10)-14,area.height),
            Fit(current?.Title??"未播放 · 右Shift 打开播放器",area.xMax-(cover.xMax+10)-14),name);
        if(Cursor.visible && UiKit.Click(area))plugin.QueueOpenPlayer();
    }

	private void Transport(float width)
	{
		GUILayout.Space(12f);
		float num = 124f;
		Rect rect = GUILayoutUtility.GetRect(width, num, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(num)
		});
		UiKit.Round(rect, UiKit.Panel, 14);
		MusicState snapshot = plugin.Aggregate.Snapshot;
		Rect val = new Rect(rect.x + 16f, rect.y + 16f, 64f, 64f);
		Texture2D val2 = ((current != null) ? CoverCache.For(current.Cover, 64) : null);
		if ((Object)(object)val2 != (Object)null)
		{
			GUI.DrawTexture(val, (Texture)(object)val2, (ScaleMode)1, true);
		}
		else
		{
			UiKit.Chip(val, 0.55f, 12);
			GUIStyle val3 = new GUIStyle(center)
			{
				fontSize = 22
			};
			val3.normal.textColor = UiKit.Ink;
			GUI.Label(val, "♪", val3);
		}
		GUIStyle val4 = new GUIStyle(rowTitle)
		{
			fontSize = 16
		};
		val4.normal.textColor = UiKit.Text;
		GUI.Label(new Rect(rect.x + 92f, rect.y + 22f, Mathf.Max(120f, width * 0.26f), 22f), (current != null) ? Fit(current.Title, Mathf.Max(120f, width * 0.26f)) : "选择一首音乐", val4);
		GUIStyle val5 = new GUIStyle(small)
		{
			fontSize = 12
		};
		val5.normal.textColor = UiKit.Muted;
		GUI.Label(new Rect(rect.x + 92f, rect.y + 46f, Mathf.Max(120f, width * 0.26f), 18f), (current != null) ? Fit(current.Artist, Mathf.Max(120f, width * 0.26f)) : "PEAK · Muonium", val5);
		GUIStyle val6 = new GUIStyle(small)
		{
			fontSize = 11
		};
		val6.normal.textColor = UiKit.Faint;
		GUI.Label(new Rect(rect.x + 92f, rect.y + 64f, Mathf.Max(120f, width * 0.26f), 16f), PlaybackModeNames[playMode] +
			(heartTask != null ? " · 加载推荐中" : plugin.Aggregate.PlaybackPending ? " · 缓存音频中" : ""), val6);
		float num2 = rect.x + Mathf.Max(230f, width * 0.3f);
		Rect rect2 = new Rect(num2, rect.y + 22f, 44f, 44f);
		Rect rect3 = new Rect(num2 + 52f, rect.y + 18f, 72f, 52f);
		Rect rect4 = new Rect(num2 + 132f, rect.y + 22f, 44f, 44f);
		DrawControl(rect2, "prev", primary: false);
		DrawControl(rect4, "next", primary: false);
		DrawControl(rect3, snapshot.Playing ? "pause" : "play", primary: true);
		if (UiKit.Click(rect2))
		{
			Next(-1);
		}
		if (UiKit.Click(rect4))
		{
			Next(1);
		}
		if (UiKit.Click(rect3))
		{
			plugin.Aggregate.TogglePause();
		}
		float num3 = rect.xMax - 16f;
		Rect rect5 = new Rect(num3 - 70f, rect.y + 24f, 64f, 36f);
		bool favOn = current != null && library.Favorite(current);
		DrawPill(rect5, favOn ? "已收藏" : "收藏", () =>
		{
			if (current != null)
			{
				library.Toggle(current);
			}
		}, favOn);
		Rect rect6 = new Rect(rect5.x - 98f, rect.y + 24f, 92f, 36f);
		DrawPill(rect6, PlaybackModeButtons[playMode], () =>
			ChangePlaybackMode((playMode + 1) % (HeartAvailable ? PlaybackModeButtons.Length : HeartMode)));
		Rect val7 = new Rect(rect6.x - 160f, rect.y + 28f, 150f, 28f);
		GUI.Label(new Rect(val7.x, val7.y + 4f, 40f, 20f), "音量", val6);
		plugin.Settings.AggregateVolume.Value = DrawSlider(new Rect(val7.x + 42f, val7.y + 2f, val7.width - 42f, 24f), plugin.Settings.AggregateVolume.Value, 5260366, null);
		if (snapshot.DurationMs > 0)
		{
			long num4 = snapshot.PositionMs / 1000;
			long num5 = snapshot.DurationMs / 1000;
			if (num4 != cachedPositionSecond)
			{
				cachedPositionSecond = num4;
				cachedPositionText = Clock(snapshot.PositionMs);
			}
			if (num5 != cachedDurationSecond)
			{
				cachedDurationSecond = num5;
				cachedDurationText = Clock(snapshot.DurationMs);
			}
			Rect val8 = new Rect(rect.x + 16f, rect.y + 94f, rect.width - 32f, 24f);
			GUIStyle val9 = new GUIStyle(small)
			{
				fontSize = 11
			};
			val9.normal.textColor = UiKit.Muted;
			GUI.Label(new Rect(val8.x, val8.y + 3f, 52f, 18f), cachedPositionText, val9);
			GUIStyle val10 = new GUIStyle(val9)
			{
				alignment = (TextAnchor)5
			};
			GUI.Label(new Rect(val8.xMax - 52f, val8.y + 3f, 52f, 18f), cachedDurationText, val10);
			float value = (float)snapshot.PositionMs / (float)snapshot.DurationMs;
			DrawSlider(new Rect(val8.x + 62f, val8.y, val8.width - 124f, 24f), value, 5260367, (float fraction) =>
			{
				plugin.Aggregate.Seek(fraction);
			});
		}
		else
		{
			GUIStyle val11 = new GUIStyle(small)
			{
				fontSize = 11
			};
			val11.normal.textColor = UiKit.Faint;
			GUI.Label(new Rect(rect.x + 16f, rect.y + 94f, rect.width - 32f, 18f), plugin.Aggregate.Status, val11);
		}
	}

	private float seekPreview;
    internal void CancelSeek(){if(GUIUtility.hotControl==5260367)GUIUtility.hotControl=0;}

	private float DrawSlider(Rect rect, float value, int controlId, Action<float>? onCommit)
	{
		bool flag = GUIUtility.hotControl == controlId;
		bool flag2 = rect.Contains(Event.current.mousePosition);
		float num = rect.y + rect.height * 0.5f - 4f;
		Rect rect2 = new Rect(rect.x, num, rect.width, 8f);
		UiKit.Round(rect2, new Color(0.1f, 0.13f, 0.17f, 0.26f), 4);
		float num2 = Mathf.Clamp01(flag && onCommit != null ? seekPreview : value);
		if (num2 > 0.001f)
		{
			Rect fillRect = new Rect(rect2.x, rect2.y, Mathf.Max(8f, rect2.width * num2), rect2.height);
			UiKit.Round(fillRect, new Color(1f, 1f, 1f, 0.96f), 4);
			if (fillRect.width > 10f)
			{
				UiKit.Round(new Rect(fillRect.x + 2f, fillRect.y + 1.5f, fillRect.width - 4f, 2f), new Color(1f, 1f, 1f, 0.9f), 1);
			}
		}
		float num3 = rect2.x + rect2.width * num2;
		Rect rect3 = new Rect(num3 - 9f, rect.y + rect.height * 0.5f - 9f, 18f, 18f);
		UiKit.Circle(new Rect(rect3.x - 3f, rect3.y - 3f, 24f, 24f), new Color(1f, 1f, 1f, (flag2 | flag) ? 0.36f : 0.18f));
		UiKit.Circle(rect3, new Color(1f, 1f, 1f, 0.98f));
		UiKit.Circle(new Rect(rect3.x + 4f, rect3.y + 4f, 10f, 10f), new Color(0.18f, 0.23f, 0.3f, 0.2f));
		Event val = Event.current;
		if ((int)val.type == 0 && val.button == 0 && rect.Contains(val.mousePosition))
		{
			GUIUtility.hotControl = controlId;
			val.Use();
			float num4 = Mathf.Clamp01((val.mousePosition.x - rect2.x) / rect2.width);
			if (onCommit != null)
			{
				seekPreview = num4;
				return num4;
			}
			return num4;
		}
		if (((int)val.type == 3) & flag)
		{
			val.Use();
			float preview = Mathf.Clamp01((val.mousePosition.x - rect2.x) / rect2.width);
            if(onCommit != null)seekPreview=preview;
            return preview;
		}
		if (((int)val.type == 1) & flag)
		{
			float num5 = Mathf.Clamp01((val.mousePosition.x - rect2.x) / rect2.width);
			GUIUtility.hotControl = 0;
			val.Use();
			if (onCommit != null)
			{
				onCommit(num5);
				return num5;
			}
		}
		return value;
	}

	private void DrawControl(Rect rect, string label, bool primary)
	{
		bool flag = UiKit.Hover(rect);
		int num = Mathf.RoundToInt(rect.height * 0.5f);
		UiKit.Chip(rect, primary ? (flag ? 0.86f : 0.68f) : (flag ? 0.4f : 0.22f), num);
		float num2 = rect.height * (primary ? 0.54f : 0.46f);
		Texture2D val = UiKit.Glyph(label, Mathf.RoundToInt(num2), primary ? UiKit.Text : (flag ? UiKit.Text : UiKit.Muted));
		if ((Object)(object)val != (Object)null)
		{
			GUI.DrawTexture(new Rect(rect.x + (rect.width - num2) * 0.5f, rect.y + (rect.height - num2) * 0.5f, num2, num2), (Texture)(object)val, (ScaleMode)2, true);
			return;
		}
		GUIStyle val2 = new GUIStyle(center)
		{
			fontSize = 16,
			fontStyle = (FontStyle)1
		};
		val2.normal.textColor = (primary ? UiKit.Text : UiKit.Muted);
		GUI.Label(rect, label, val2);
	}

	private void DrawPill(Rect rect, string label, Action action, bool active = false)
	{
		bool flag = UiKit.Hover(rect);
		UiKit.Chip(rect, active ? 0.62f : (flag ? 0.4f : 0.24f), 9);
		GUIStyle val = new GUIStyle(center)
		{
			fontSize = 12
		};
		val.normal.textColor = ((active || flag) ? UiKit.Text : UiKit.Muted);
		GUI.Label(rect, label, val);
		if (UiKit.Click(rect))
		{
			action();
		}
	}

	private static string Clock(long ms)
	{
		return TimeSpan.FromMilliseconds(ms).ToString("mm\\:ss", CultureInfo.InvariantCulture);
	}

	private void ClearQr()
	{
		if ((Object)(object)qr != (Object)null)
		{
			Object.Destroy((Object)(object)qr);
		}
		qr = null;
	}

	internal void DrawTitleEntry(bool open)
	{
		if (open)
		{
			return;
		}
		Scene activeScene = SceneManager.GetActiveScene();
		if (!(activeScene.name != "Title"))
		{
			if (titleEntry == null)
			{
				titleFont = Font.CreateDynamicFontFromOSFont(new string[2] { "Microsoft YaHei", "Arial" }, 19);
				titleEntry = new GUIStyle(GUI.skin.button)
				{
					font = titleFont,
					fontSize = 19
				};
			}
			if (GUI.Button(new Rect((float)(Screen.width - 260), (float)(Screen.height - 76), 230f, 48f), "♫ 音乐播放器", titleEntry))
			{
				plugin.OpenPlayer();
			}
		}
	}

	public void Dispose()
	{
        DropHeartRequest();
		client.Dispose();
		ClearQr();
		AvatarCache.Reset();
		if ((Object)(object)titleFont != (Object)null)
		{
			Object.Destroy((Object)(object)titleFont);
		}
		if ((Object)(object)avatarTexture != (Object)null)
		{
			Object.Destroy((Object)(object)avatarTexture);
		}
		plugin.Aggregate.TrackStarted -= Started;
		plugin.Aggregate.TrackEnded -= Ended;
        plugin.Aggregate.TrackLoadFailed -= LoadFailed;
	}

	static MuoniumHub()
	{
	}
}
