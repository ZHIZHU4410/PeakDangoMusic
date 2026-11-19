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

internal sealed class MuoniumHub : IDisposable
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

	private Task<JObject>? playback;

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

	private int playToken;

	private int pendingToken;

	private readonly System.Random random = new System.Random();

	private Vector2 scroll;

	private Vector2 sidebarScroll;

	private Vector2 songScroll;

	private GdMusicApi.Track[] rows = Array.Empty<GdMusicApi.Track>();

	private GdMusicApi.Track? requested;

	private GdMusicApi.Track? current;

	private int commentSkip;

	private JObject[] playlists = Array.Empty<JObject>();

	private Texture2D? qr;

	private GUIStyle? heading;

	private GUIStyle? bigHeading;

	private GUIStyle? section;

	private GUIStyle? muted;

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

	private static readonly string[] Pages = new string[10] { "首页", "发现歌单", "我的歌单", "本地收藏", "播放历史", "播放队列", "账号中心", "悬浮歌词", "连接与诊断", "开发作者" };

	private static readonly string[] AuthorNames = new string[3] { "布洛芬", "Dv", "羊默默" };

	private static readonly string[] AuthorRoles = new string[3] { "PEAK 音乐播放器 · 主开发", "PEAK 音乐播放器 · 联合开发", "PEAK 音乐播放器 · 测试与反馈" };

	private static readonly string[] AuthorUrls = new string[3] { "https://v.douyin.com/nJqjiK2Wc40/", "https://v.douyin.com/qwSfvxDN9d8/", "https://v.douyin.com/M5TKeBTKMP4/" };

	private const int AuthorPage = 9;

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
		library.Flush(Time.unscaledTime);
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
				message = "听歌推荐已更新";
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
				message = recNote;
			}
		}
		Task<JObject>? task = pending;
		if (task != null && task.IsCompleted)
		{
			Task<JObject> task2 = pending;
			pending = null;
			try
			{
				JObject result = task2.GetAwaiter().GetResult();
				JToken val = result["tracks"];
				JArray val2 = (JArray)(object)((val is JArray) ? val : null);
				if (val2 != null)
				{
					rows = ParseTracks(val2);
					InvalidateTrackCache();
				}
				JToken val3 = result["playlists"];
				JArray val4 = (JArray)(object)((val3 is JArray) ? val3 : null);
				if (val4 != null)
				{
					if (pendingOp == "library")
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
					message = "请用对应平台的手机App扫描二维码并确认";
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
		Task<JObject>? task3 = playback;
		if (task3 != null && task3.IsCompleted)
		{
			Task<JObject> task4 = playback;
			playback = null;
			try
			{
				JObject result2 = task4.GetAwaiter().GetResult();
				GdMusicApi.Track track = requested;
				JToken val5 = result2["lines"];
				JToken obj = ((val5 is JArray) ? val5 : null);
				IEnumerable<LyricsLine> enumerable = ((obj != null) ? ((IEnumerable<JToken>)obj).Select((JToken l) =>
				{
					long valueOrDefault = ((long?)l[(object)"startMs"]).GetValueOrDefault();
					long valueOrDefault2 = ((long?)l[(object)"durationMs"]).GetValueOrDefault();
					string text3 = ((string)l[(object)"text"]) ?? "";
					JToken val6 = l[(object)"words"];
					JToken obj2 = ((val6 is JArray) ? val6 : null);
					return new LyricsLine(valueOrDefault, valueOrDefault2, text3, (obj2 != null) ? ((IEnumerable<JToken>)obj2).Select((JToken w) => new LyricsWord(((long?)w[(object)"startMs"]).GetValueOrDefault(), ((long?)w[(object)"durationMs"]).GetValueOrDefault(), ((string)w[(object)"text"]) ?? "")).ToArray() : null, ((string)l[(object)"translation"]) ?? "");
				}) : null);
				// Match 2.1.2: parse the original YRC rather than Java-normalized display lines.
                var timeline212=LyricsTimeline.Parse((string?)result2["lrc"],(string?)result2["yrc"],(string?)result2["tlyric"]);
                if(timeline212.Lines.Count==0)timeline212=LyricsTimeline.FromLines(enumerable??Enumerable.Empty<LyricsLine>());
                string text2 = ((string)result2["url"]) ?? "";
				bool flag = !Uri.TryCreate(text2, UriKind.Absolute, out Uri result3);
				if (!flag)
				{
					string scheme = result3.Scheme;
                    bool flag2 = scheme == "http" || scheme == "https" || OwnedAudioCache.Allows(result3);
					flag = !flag2;
				}
				if (flag)
				{
					throw new InvalidDataException("原生平台没有返回有效播放地址");
				}
				if (pendingToken == playToken)
				{
					Activate();
					plugin.Aggregate.PlayResolved(new GdMusicApi.Resolved(track, text2, timeline212, ((bool?)result2["trial"] == true) ? "平台返回试听片段，需按账号权限播放" : "", ((string)result2["format"]) ?? ""));
				}
			}
			catch (Exception error2)
			{
				message = DiagnosticLog.Report("Muonium音乐", "原生曲库播放解析", error2, "MuoniumHub.cs", 152);
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

	private void Request(string op, string source, JObject? fields = null)
	{
		if (!Busy)
		{
			pendingOp = op;
			pendingSource = source;
			message = "正在加载…";
			pending = client.Call(op, source, fields);
		}
	}

	private void Activate()
	{
		plugin.Settings.Source.Value = 4;
		plugin.Settings.PlayerEnabled.Value = true;
		plugin.Aggregate.Tick(enabled: true, plugin.Settings.AggregateVolume.Value);
	}

	private void Play(GdMusicApi.Track track, bool replaceQueue = false)
	{
		if (plugin.Aggregate.Busy)
		{
			message = "正在加载音频，请稍候";
			return;
		}
		Activate();
		playToken++;
		pendingToken = playToken;
		requested = track;
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
		string source = track.Source;
		if ((source == "ncm" || source == "qq" || source == "kugou" || source == "bilibili-native" || source == "youtube-native" || source == "lx-native") ? true : false)
		{
			playback = client.Call("resolve", track.Source, new JObject
			{
				["trackId"] = (JToken)(track.Id),
				["track"] = (JToken)new JObject
				{
					["id"] = (JToken)(track.Id),
					["title"] = (JToken)(track.Title),
					["artist"] = (JToken)(track.Artist),
					["album"] = (JToken)(track.Album)
				}
			});
		}
		else
		{
			plugin.Aggregate.Play(track, plugin.Settings.AggregateBitrate.Value);
		}
	}

	private void Started(GdMusicApi.Track track)
	{
        CancelSeek();
		current = track;
		library.Played(track);
		queueIndex = library.Queue.FindIndex((GdMusicApi.Track t) => PlayerLibrary.Key(t) == PlayerLibrary.Key(track));
	}

	private void Ended()
	{
		if (current == null)
		{
			return;
		}
		if (queueIndex < 0 || queueIndex >= library.Queue.Count)
		{
			Play(current);
			return;
		}
		if (playMode == 1)
		{
			Play(current);
			return;
		}
		Next(1);
	}

	private void Next(int direction)
	{
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
		if (playMode == 2)
		{
			if (count <= 1)
			{
				num = 0;
			}
			else
			{
				int num2 = random.Next(count - 1);
				num = ((num2 < queueIndex) ? num2 : (num2 + 1));
			}
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
		queueIndex = num;
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
		scroll = GUILayout.BeginScrollView(scroll, false, false, GUIStyle.none, GUI.skin.verticalScrollbar, new GUILayoutOption[2] { GUILayout.Width(num3), GUILayout.Height(Mathf.Max(120f, num2 - 96f)) });
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
			case 8:
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
		UiKit.Round(new Rect(rect4.x - 2f, rect4.y - 2f, rect4.width + 4f, rect4.height + 4f), new Color(1f, 1f, 1f, flag ? 0.22f : 0.14f), 13);
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
        if(platform==5){SectionHeader("YouTube 原生", "支持搜索和歌单ID；播放需本机 FFmpeg，可在连接与诊断配置专用 HTTP 代理。");return;}
        if(platform==4){SectionHeader("哔哩哔哩原生", "在上方搜索视频音乐，支持BV/av地址；AAC音频由本机FFmpeg转换。歌单功能继续适配。");return;}
        if(platform == 3){SectionHeader("酷狗原生曲库", "Muonium原生搜索、推荐歌单、扫码登录和播放解析");if(ActionButton("加载酷狗推荐歌单", 200f))Request("discover", Source);PlaylistRows(playlistRows, ref discoverPage);return;}

		if (platform == 2)
		{
			SectionHeader("音乐榜单", "GD 聚合没有榜单，切到网易云或 QQ 音乐才有");
			EmptyState("这里没有榜单数据", "点右上角「网易云」或「QQ 音乐」再看榜单");
			return;
		}
		SectionHeader("音乐榜单", (platform == 1) ? "QQ音乐巅峰榜，点开就能直接播放" : "网易云官方四大榜单，点开就能直接播放");
		CommentFeed.Poll(current);
		ChartGrid();
		if (platform == 0)
		{
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
		float panelWidth = Mathf.Clamp(contentWidth * 0.3f, 220f, 340f);
		float cardsWidth = Mathf.Max(340f, contentWidth - panelWidth - 16f);
		float width = Mathf.Max(86f, (cardsWidth - gap * 3f) / 4f);
		float height = width;
		float panelHeight = height;
		float rowHeight = Mathf.Max(height, panelHeight);
		GUILayout.Space(rowHeight);
		Rect rect = GUILayoutUtility.GetLastRect();
		if (rect.height < 8f)
		{
			rect = new Rect(rect.x, rect.y, cardsWidth, rowHeight);
		}
		for (int i = 0; i < ChartNames.Length; i++)
		{
			ChartCard(new Rect(rect.x + (width + gap) * (float)i, rect.y, width, height), i);
		}
		CommentsPanel(new Rect(rect.x + cardsWidth + 16f, rect.y, panelWidth, panelHeight));
	}

	private void CommentsPanel(Rect rect)
	{
		UiKit.Round(rect, new Color(1f, 1f, 1f, 0.2f), 16);
		GUIStyle guiStyle = new GUIStyle(GUI.skin.label)
		{
			fontSize = 13,
			fontStyle = FontStyle.Bold,
			alignment = (TextAnchor)3,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle.normal.textColor = UiKit.Text;
		GUI.Label(new Rect(rect.x + 14f, rect.y + 9f, rect.width - 28f, 20f), "正在播放 · 热评", guiStyle);
		GUIStyle guiStyle2 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 10,
			alignment = (TextAnchor)3,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle2.normal.textColor = UiKit.Faint;
		GUIStyle guiStyle3 = new GUIStyle(GUI.skin.label)
		{
			fontSize = 10,
			wordWrap = true,
			padding = new RectOffset(0, 0, 0, 0),
			margin = new RectOffset(0, 0, 0, 0),
			clipping = (TextClipping)2
		};
		guiStyle3.normal.textColor = UiKit.Faint;
		UiKit.Line(rect.x + 14f, rect.y + 48f, rect.width - 28f, UiKit.Divider);
		string text = ((current == null) ? "还没有在放的歌" : ("《" + current.Title + "》"));
		IReadOnlyList<MusicComment> readOnlyList = null;
		MusicComment musicComment = null;
		int num = 0;
		if (current != null && CommentFeed.Ready)
		{
			readOnlyList = CommentFeed.Pick(CommentFeed.Count, 0);
			if (readOnlyList.Count > 0)
			{
				num = Mathf.FloorToInt(Time.unscaledTime / 6f) + commentSkip;
				int index = ((num % readOnlyList.Count) + readOnlyList.Count) % readOnlyList.Count;
				musicComment = readOnlyList[index];
				text = text + " · 第 " + (index + 1) + " / " + readOnlyList.Count + " 条";
			}
		}
		GUI.Label(new Rect(rect.x + 14f, rect.y + 29f, rect.width - 28f, 17f), text, guiStyle2);
		if (current == null)
		{
			GUI.Label(new Rect(rect.x + 14f, rect.y + 56f, rect.width - 28f, 40f), "放一首歌，这里会随机显示它的评论", guiStyle3);
			return;
		}
		if (CommentFeed.Loading)
		{
			GUI.Label(new Rect(rect.x + 14f, rect.y + 56f, rect.width - 28f, 40f), "正在拉取评论…", guiStyle3);
			return;
		}
		if (musicComment == null)
		{
			GUI.Label(new Rect(rect.x + 14f, rect.y + 56f, rect.width - 28f, 40f), (CommentFeed.Note.Length > 0) ? CommentFeed.Note : "这首歌还没有评论", guiStyle3);
		}
		else
		{
			Rect rect2 = new Rect(rect.x + 14f, rect.y + 56f, 40f, 40f);
			Texture2D texture2D = CommentFeed.Avatar(musicComment.Avatar);
			if ((Object)(object)texture2D != (Object)null)
			{
				GUI.DrawTexture(rect2, (Texture)(object)texture2D, (ScaleMode)2, true);
			}
			else
			{
				UiKit.Circle(rect2, UiKit.Tint(musicComment.Nick));
				GUIStyle guiStyle4 = new GUIStyle(center)
				{
					fontSize = 15
				};
				guiStyle4.normal.textColor = UiKit.Ink;
				GUI.Label(rect2, UiKit.Initial(musicComment.Nick), guiStyle4);
			}
			GUIStyle guiStyle5 = new GUIStyle(GUI.skin.label)
			{
				fontSize = 11,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0),
				clipping = (TextClipping)2
			};
			guiStyle5.normal.textColor = UiKit.Muted;
			GUI.Label(new Rect(rect2.xMax + 10f, rect.y + 56f, rect.width - 78f, 16f), musicComment.Nick + ((musicComment.Likes > 0) ? ("  ·  " + musicComment.Likes + " 赞") : ""), guiStyle5);
			GUIStyle guiStyle6 = new GUIStyle(GUI.skin.label)
			{
				fontSize = 11,
				wordWrap = true,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0),
				clipping = (TextClipping)2
			};
			guiStyle6.normal.textColor = UiKit.Text;
			float num2 = Mathf.Max(24f, rect.yMax - 40f - (rect.y + 76f));
			GUI.Label(new Rect(rect2.xMax + 10f, rect.y + 76f, rect.width - 82f, num2), musicComment.Body, guiStyle6);
			GUIStyle guiStyle7 = new GUIStyle(GUI.skin.label)
			{
				fontSize = 11,
                wordWrap = false,
				alignment = TextAnchor.MiddleRight,
				padding = new RectOffset(0, 0, 0, 0),
				margin = new RectOffset(0, 0, 0, 0),
				clipping = (TextClipping)2
			};
			guiStyle7.normal.textColor = UiKit.Faint;
			GUI.Label(new Rect(rect.x + 14f, rect.yMax - 32f, rect.width - 28f, 26f), "点一下换一条", guiStyle7);
		}
		if (UiKit.Click(rect))
		{
			commentSkip++;
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
		GUI.Label(new Rect(rect.x, rect.y + 24f, num - 200f, 18f), "读取你网易云账号的听歌习惯，点卡片直接播", guiStyle2);
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
		message = "正在拉取听歌推荐…";
		recTask = DailyRecs.Load(client);
	}

	private void PlayFromList(GdMusicApi.Track[] list, int index)
	{
		if (list == null || list.Length == 0)
		{
			return;
		}
		int num = Mathf.Clamp(index, 0, list.Length - 1);
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
			UiKit.Round(rect, new Color(1f, 1f, 1f, 0.08f), 16);
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
			Request("library", Source);
		}
		if (libraryRows.Count == 0)
		{
			EmptyState("正在加载你的网易云歌单", "进这一页自动加载，等几秒；一直空着说明这个账号没有自建/收藏歌单");
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
			EmptyState("歌单正在加载", "打开后会自动拉取，等几秒；一直空着就检查网络或账号登录态");
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
				UiKit.Round(new Rect(rect3.x, rect3.y, rect3.width, rect3.height), new Color(1f, 1f, 1f, 0.045f), 10);
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

	private void Diagnostics()
    {
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
		Hint("PEAK DangoMusic · Muonium 音乐播放器 · 版本 2.3.0");
		GUILayout.Space(16f);
		int count = AuthorNames.Length;
		float num = (contentWidth - 18f * (float)(count - 1)) / (float)count;
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		for (int i = 0; i < count; i++)
		{
			if (i > 0)
			{
				GUILayout.Space(18f);
			}
			Rect rect = GUILayoutUtility.GetRect(num, 200f, new GUILayoutOption[2]
			{
				GUILayout.Width(num),
				GUILayout.Height(200f)
			});
			AuthorCard(rect, i);
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(18f);
		Hint("曲库与登录都由本地桥接组件处理，账号信息只留在本机的加密文件里，不会上传。");
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
			fontSize = 12
		};
		guiStyle3.normal.textColor = UiKit.Faint;
		GUI.Label(new Rect(rect.x + 144f, rect.y + 84f, rect.width - 168f, 20f), AuthorRoles[index], guiStyle3);
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

	private static string Fit(string text, float width)
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

	private string manualPlaylist = "";
    private string youtubeCookie = "";

	private void Account()
	{
		GUILayout.Label("凭证以 Windows 当前用户 DPAPI 加密保存在本机", muted, Array.Empty<GUILayoutOption>());
		GUILayout.Space(10f);
		if (platform == 6){EmptyState("LX 使用本地脚本", "脚本路径和渠道在首页配置；账号登录请使用对应原生平台");return;}
        if (platform == 2)
		{
			EmptyState("GD 聚合不需要登录", "网易云 / QQ 账号请在各原生平台标签下管理");
			return;
		}
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
		if (platform < 5 && ActionButton("生成登录二维码", 160f))
		{
			polling = false;
			ClearQr();
			Request("qr", Source);
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
    internal void DrawMini(Rect area){
        Styles();
        UiKit.Shadow(area,18,1f);
        UiKit.Round(area,new Color(.88f,.92f,.96f,.94f),16);
        Rect cover=new Rect(area.x+12,area.y+12,60,60);
        var texture=current!=null?CoverCache.For(current.Cover,60):null;
        if(texture!=null)GUI.DrawTexture(cover,texture,ScaleMode.ScaleToFit,true);
        else {UiKit.Round(cover,UiKit.PanelRaised,10);GUI.Label(cover,"♪",center);}
        float width=Mathf.Max(40,area.width-98);
        GUI.Label(new Rect(cover.xMax+12,area.y+12,width,24),Fit(current?.Title??"正在播放",width),rowTitle);
        GUI.Label(new Rect(cover.xMax+12,area.y+38,width,20),Fit(current?.Artist??"",width),small);
        var state=plugin.Aggregate.Snapshot;
        GUI.Label(new Rect(area.x+14,area.y+80,area.width-28,20),(state.Playing?"正在播放":"已暂停")+" · 右Shift 展开 · \\ 隐藏",small);
        UiKit.Round(new Rect(area.x+14,area.yMax-10,area.width-28,3),UiKit.Divider,1);
        float fraction=state.DurationMs>0?Mathf.Clamp01((float)state.PositionMs/state.DurationMs):0;
        if(fraction>0)UiKit.Round(new Rect(area.x+14,area.yMax-10,(area.width-28)*fraction,3),UiKit.Muted,1);
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
		GUI.Label(new Rect(rect.x + 92f, rect.y + 64f, Mathf.Max(120f, width * 0.26f), 16f), (new string[3] { "顺序播放", "单曲循环", "随机播放" })[playMode] + ((playback != null) ? " · 解析音频中" : ""), val6);
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
		Rect rect6 = new Rect(rect5.x - 86f, rect.y + 24f, 80f, 36f);
		DrawPill(rect6, (new string[3] { "顺序", "循环", "随机" })[playMode], () =>
		{
			playMode = (playMode + 1) % 3;
		});
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
	}

	static MuoniumHub()
	{
	}
}
