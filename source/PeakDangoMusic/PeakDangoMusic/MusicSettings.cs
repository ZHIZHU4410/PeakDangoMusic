using System;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace PeakDangoMusic;

internal sealed class MusicSettings
{
	internal readonly ConfigEntry<bool> AutoDayNight;

	internal readonly ConfigEntry<bool> ManualDark;

	internal readonly ConfigEntry<bool> Enabled;

	internal readonly ConfigEntry<bool> PlayerEnabled;

	internal readonly ConfigEntry<bool> Translation;

	internal readonly ConfigEntry<bool> Glow;

	internal readonly ConfigEntry<bool> Pop;

	internal readonly ConfigEntry<bool> Wiggle;

	internal readonly ConfigEntry<bool> AllowWalls;

	internal readonly ConfigEntry<bool> BehindPlayer;

	internal readonly ConfigEntry<bool> Smart;

	internal readonly ConfigEntry<bool> Follow;

	internal readonly ConfigEntry<bool> FacePlayer;

	internal readonly ConfigEntry<bool> KeepAway;

	internal readonly ConfigEntry<bool> AboveGround;

	internal readonly ConfigEntry<bool> SplitLines;

	internal readonly ConfigEntry<int> Port;

	internal readonly ConfigEntry<int> Mode;

	internal readonly ConfigEntry<int> Frequency;

	internal readonly ConfigEntry<int> Offset;

	internal readonly ConfigEntry<int> Entrance;

	internal readonly ConfigEntry<int> Exit;

	internal readonly ConfigEntry<int> Source;

	internal readonly ConfigEntry<int> NetEasePort;

	internal readonly ConfigEntry<float> FontSize;

	internal readonly ConfigEntry<float> Distance;

	internal readonly ConfigEntry<float> Amount;

	internal readonly ConfigEntry<float> Brightness;

	internal readonly ConfigEntry<float> Spread;

	internal readonly ConfigEntry<float> GlowStrength;

	internal readonly ConfigEntry<float> GlowRadius;

	internal readonly ConfigEntry<string> PlayerPath;

	internal readonly ConfigEntry<string> FontFolder;
    internal readonly ConfigEntry<string> FfmpegPath;
    internal readonly ConfigEntry<string> YouTubeProxy;
    internal readonly ConfigEntry<string> LxScriptPath;
    internal readonly ConfigEntry<string> LxChannel;

	internal readonly ConfigEntry<string> FontPath;

	internal readonly ConfigEntry<string> KugouLyricsFolder;

	internal readonly ConfigEntry<string> Style;

	internal readonly ConfigEntry<int> FoliaPlacement;

	internal readonly ConfigEntry<int> FoliaQuality;

	internal readonly ConfigEntry<int> FoliaFps;

	internal readonly ConfigEntry<int> FoliaIntensity;

	internal readonly ConfigEntry<int> AggregatePlatform;

	internal readonly ConfigEntry<int> AggregateBitrate;

    internal readonly ConfigEntry<int> CacheMinutes;

    internal readonly ConfigEntry<int> PrefetchSongs;

	internal readonly ConfigEntry<float> FoliaSize;

	internal readonly ConfigEntry<float> FoliaOpacity;

	internal readonly ConfigEntry<float> FoliaFontScale;

	internal readonly ConfigEntry<float> AggregateVolume;

	internal readonly ConfigEntry<bool> FoliaBackground;

	internal readonly ConfigEntry<bool> FoliaSoftware;

	internal readonly ConfigEntry<string> ThemeColor;

	internal readonly ConfigEntry<string> LyricColor;

	internal readonly ConfigEntry<float> MiniX;

	internal readonly ConfigEntry<float> MiniY;

	internal readonly ConfigEntry<Key> PerspectiveKey;

	internal readonly ConfigEntry<bool> AmbienceMix;

	internal readonly ConfigEntry<bool> AmbienceLowPass;

	internal readonly ConfigEntry<float> AmbienceFloor;

	internal float WorldFontSize => 2.8f * FontSize.Value * (Distance.Value / 10f);

	internal int EffectiveSource => Math.Clamp(Source.Value, 0, 5);

	internal MusicSettings(ConfigFile config)
	{
		PerspectiveKey = config.Bind("按键", "切换视角", Key.None, "切换角色视角的按键。None 为未设置，可在播放器设置中修改。");
		PlayerEnabled = config.Bind<bool>("播放器", "启用游戏内音乐", true, "独立于悬浮歌词开关；关闭时停止本机音乐。");
		Enabled = config.Bind<bool>("连接", "启用漂浮歌词", true, "独立控制歌词显示；关闭歌词不会停止游戏内音乐。");
		Source = config.Bind<int>("连接", "播放器来源", 0, new ConfigDescription("0=BLF/LX本机API；1=网易云；2=酷狗音乐；3=QQ音乐；4=GD聚合音乐；5=系统媒体会话。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 5), Array.Empty<object>()));
		AggregatePlatform = config.Bind<int>("聚合音乐", "平台", 0, new ConfigDescription("GD聚合平台", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 9), Array.Empty<object>()));
        CacheMinutes = config.Bind("缓存", "保留分钟", 10,
            new ConfigDescription("未使用的曲库与音频缓存保留时间。", new AcceptableValueRange<int>(1, 60)));
        PrefetchSongs = config.Bind("缓存", "预缓存后续歌曲", 3,
            new ConfigDescription("提前加载播放队列里的后续曲目，0 关闭预缓存。", new AcceptableValueRange<int>(0, 6)));
		AggregateBitrate = config.Bind<int>("聚合音乐", "码率", 320, new ConfigDescription("请求音质；平台可能返回较低音质", (AcceptableValueBase)(object)new AcceptableValueList<int>(new int[5] { 128, 192, 320, 740, 999 }), Array.Empty<object>()));
		AggregateVolume = config.Bind<float>("聚合音乐", "音量", 0.5f, new ConfigDescription("仅控制游戏内聚合音乐", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
		NetEasePort = config.Bind<int>("连接", "网易云本机端口", 9223, new ConfigDescription("原版网易云可选本机进度连接端口。未公开Windows播放进度时，使用包内脚本启动网易云。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 65535), Array.Empty<object>()));
		Port = config.Bind<int>("连接", "LX本机端口", 23330, new ConfigDescription("BLF / LX 设置→开放 API 的端口，仅连接127.0.0.1。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(1, 65535), Array.Empty<object>()));
		KugouLyricsFolder = config.Bind<string>("连接", "酷狗歌词缓存目录", "", "留空自动查找固定磁盘的KuGou/Lyric；也可填写酷狗设置中的歌词保存目录。只读，不复制歌词。");
		PlayerPath = config.Bind<string>("连接", "播放器路径", "", "可选：填写你自己的 BLF/LX exe 路径，仅用于打开播放器按钮；手动打开播放器无需设置。");
		LxScriptPath = config.Bind<string>("连接", "LX脚本路径", "", "由用户指定的本地JS脚本，不自动下载或打包；只在使用LX搜索/播放时加载。");
        LxChannel = config.Bind<string>("连接", "LX渠道", "wy", "当前支持wy网易云、txQQ、kg酷狗；脚本须声明musicUrl动作。");
        FfmpegPath = config.Bind<string>("连接", "FFmpeg路径", "", "可选：本机 ffmpeg.exe，用于哔哩哔哩/YouTube普通音频转码。留空时检测 universal-modder 安装目录。");
        YouTubeProxy = config.Bind<string>("连接", "YouTube代理", "", "可选：仅用于YouTube的HTTP代理，格式http://127.0.0.1:端口；留空直连。");
        FontFolder = config.Bind<string>("字体", "字体素材目录", "", "递归读取本地TTF、OTF、TTC字体，不安装或复制素材。");
		if (FontFolder.Value == @"D:\BaiduNetdiskDownload\字体搭配2.0 by阳春落羽") FontFolder.Value = "";
		FontPath = config.Bind<string>("字体", "歌词字体文件", "", "留空使用系统默认；悬浮歌词设置的字体页选择。缺失字形由系统字体补齐。");
		Style = config.Bind<string>("样式", "歌词显示样式", "dango", new ConfigDescription("dango保留原有显示；其余为Folia的15种内置样式；random为每首随机。", (AcceptableValueBase)(object)new AcceptableValueList<string>(FoliaStyles.Ids), Array.Empty<object>()));
		FoliaPlacement = config.Bind<int>("样式", "Folia舞台位置", 0, new ConfigDescription("0游戏内两侧悬浮舞台，界面中自动屏幕接续；1始终两侧屏幕；2全屏舞台。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 2), Array.Empty<object>()));
		FoliaQuality = config.Bind<int>("样式", "Folia画质", 1, new ConfigDescription("0=640x360，1=960x540，2=1280x720。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 2), Array.Empty<object>()));
		FoliaFps = config.Bind<int>("样式", "Folia帧率", 24, new ConfigDescription("舞台绘制帧率，降低可减少占用。", (AcceptableValueBase)(object)new AcceptableValueList<int>(new int[4] { 15, 24, 30, 60 }), Array.Empty<object>()));
		FoliaIntensity = config.Bind<int>("样式", "Folia动效强度", 1, new ConfigDescription("0轻柔、1标准、2活泼。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 2), Array.Empty<object>()));
		FoliaSize = config.Bind<float>("样式", "Folia舞台大小", 1f, new ConfigDescription("调整两侧舞台大小，不改变原歌词距离。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.5f, 1.8f), Array.Empty<object>()));
		FoliaOpacity = config.Bind<float>("样式", "Folia舞台透明度", 0.95f, new ConfigDescription("舞台整体透明度。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.2f, 1f), Array.Empty<object>()));
		FoliaFontScale = config.Bind<float>("样式", "Folia字体倍率", 1f, new ConfigDescription("传入原样式的歌词字体倍率；自适应排版样式按自身规则缩放。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.5f, 2f), Array.Empty<object>()));
		FoliaBackground = config.Bind<bool>("样式", "Folia舞台底色", false, "显示舞台底色；样式自身的色块、齿轮等装饰仍按原实现绘制。");
		FoliaSoftware = config.Bind<bool>("样式", "Folia兼容渲染", false, "仅在显卡渲染失败时开启；使用软件图形模式，可能增加CPU占用。");
		AutoDayNight = config.Bind<bool>("显示", "昼夜自动字色", true, "开启：游戏白天深色、夜晚浅色；不随视角、天气或发光切换。关闭后使用手动字色。");
		ManualDark = config.Bind<bool>("显示", "手动深色字", false, "关闭昼夜自动字色后生效：开启为深色，关闭为浅色；两种字色均保留光晕。");
		Translation = config.Bind<bool>("显示", "显示汉化歌词", false, "PEAK扩展选项；原项目只显示原文，默认关闭附加翻译。");
		Mode = config.Bind<int>("显示", "播放方式", 0, new ConfigDescription("0整句；1单词（中文无词库时按字）；2整句固定位置、逐字显现，下一句才换位置。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 2), Array.Empty<object>()));
		FontSize = config.Bind<float>("显示", "世界字号", 1f, new ConfigDescription("对应原项目Scale：0.5–3，默认1倍；Unity字号根据倍率和距离换算。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.5f, 3f), Array.Empty<object>()));
		Brightness = config.Bind<float>("显示", "文字亮度", 1f, new ConfigDescription("PEAK材质增益，默认1，不额外提高原项目暖白字色亮度。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(1f, 6f), Array.Empty<object>()));
		Distance = config.Bind<float>("显示", "歌词距离", 10f, new ConfigDescription("对应原项目Distance：3–40，默认10。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(3f, 40f), Array.Empty<object>()));
		Spread = config.Bind<float>("显示", "左右散布角", 30f, new ConfigDescription("对应原项目Spread：0–60度，默认30；在左右两侧交替生成。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 60f), Array.Empty<object>()));
		Follow = config.Bind<bool>("显示", "跟随视角", false, "PEAK扩展：固定跟随相机的两侧位置，默认关闭，避免替代原项目世界漂浮行为。");
		Smart = config.Bind<bool>("显示", "智能防离屏", false, "对应原项目Stay on screen，默认关闭；开启后提前接续到侧面，保留演唱进度。");
		FacePlayer = config.Bind<bool>("显示", "始终朝向玩家", true, "对应原项目Face player，默认开启。");
		KeepAway = config.Bind<bool>("显示", "靠近时后退", true, "对应原项目Keep away，默认开启，维持距离设置的60%为最小间距。");
		AboveGround = config.Bind<bool>("显示", "保持视线以上", false, "对应原项目Above ground，默认关闭；开启时最低为相机下方0.5单位。");
		SplitLines = config.Bind<bool>("显示", "自动拆分合并歌词", true, "对应原项目Split lines，默认开启；中日韩句子之间的空格用于拆句，保留逐字时间。");
		AllowWalls = config.Bind<bool>("显示", "允许出现在墙内", true, "关闭时通过场景射线和空间检测避开实体岩壁；没有空位就跳过。");
		BehindPlayer = config.Bind<bool>("显示", "位于人物后景", false, "开启后使用正常场景深度，让近处人物和岩壁遮挡歌词。");
		Offset = config.Bind<int>("显示", "时间偏移毫秒", 0, new ConfigDescription("正值提前显示，负值延后显示。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(-10000, 10000), Array.Empty<object>()));
		Glow = config.Bind<bool>("动画", "白色光晕", true, "使用文字材质可用的 SDF 外发光；不创建实时灯光。");
		GlowStrength = config.Bind<float>("动画", "光晕强度", 0.3f, new ConfigDescription("对应原项目Glow strength：0–3，默认0.3。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 3f), Array.Empty<object>()));
		GlowRadius = config.Bind<float>("动画", "光晕半径", 0.5f, new ConfigDescription("对应原项目Glow radius：0–1，默认0.5。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 1f), Array.Empty<object>()));
		Pop = config.Bind<bool>("动画", "逐字弹出", true, "优先按播放器逐字时间；只有行时间时估算显现。");
		Wiggle = config.Bind<bool>("动画", "轻微抖动", true, "动画只作用于文字，不改变角色或相机。");
		Frequency = config.Bind<int>("动画", "抖动频率", 8, new ConfigDescription("对应原项目Wiggle fps：4–24，默认8。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(4, 24), Array.Empty<object>()));
		Amount = config.Bind<float>("动画", "抖动幅度", 0.8f, new ConfigDescription("对应原项目Wiggle amount：0–2，默认0.8。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 2f), Array.Empty<object>()));
		Entrance = config.Bind<int>("动画", "入场", 3, new ConfigDescription("PEAK扩展：0淡入；1上浮；2弹入；3直接。默认3，仅保留原项目逐字显现。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 3), Array.Empty<object>()));
		ThemeColor = config.Bind<string>("外观", "界面颜色", "", "设置页调色盘选出的颜色，#RRGGBB；留空为默认玻璃白。");
		LyricColor = config.Bind<string>("外观", "歌词字色", "", "游戏内悬浮歌词的字色，#RRGGBB；留空跟随深/浅字色设置。");
		MiniX = config.Bind<float>("外观", "小窗水平位置", 50f, new ConfigDescription("小窗（顶部胶囊）水平位置：0=最左，50=居中，100=最右。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 100f), Array.Empty<object>()));
		MiniY = config.Bind<float>("外观", "小窗距顶部", 14f, new ConfigDescription("小窗离屏幕上边的距离（虚拟 1080 高度下的像素）。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0f, 400f), Array.Empty<object>()));
		AmbienceMix = config.Bind<bool>("环境音效", "融合环境音", true, "刮风、下雨、暴雪时压低游戏内音乐并做低通，环境过去自动恢复。");
		AmbienceLowPass = config.Bind<bool>("环境音效", "环境低通", true, "环境越强音乐越闷；关闭后只压低音量。");
		AmbienceFloor = config.Bind<float>("环境音效", "压制强度", 1f, new ConfigDescription("1.0 为默认强度；越大压得越深、滤波越狠、回声越明显。", (AcceptableValueBase)(object)new AcceptableValueRange<float>(0.3f, 1.6f), Array.Empty<object>()));
		Exit = config.Bind<int>("动画", "退场", 1, new ConfigDescription("0淡出；1上浮；2缩小；3直接消失。", (AcceptableValueBase)(object)new AcceptableValueRange<int>(0, 3), Array.Empty<object>()));
	}

	internal void ApplyOriginalDefaults()
	{
		Style.Value = "dango";
		AutoDayNight.Value = true;
		ManualDark.Value = false;
		Enabled.Value = true;
		Translation.Value = false;
		Mode.Value = 0;
		FontSize.Value = 1f;
		Brightness.Value = 1f;
		Distance.Value = 10f;
		Spread.Value = 30f;
		Follow.Value = false;
		Smart.Value = false;
		FacePlayer.Value = true;
		KeepAway.Value = true;
		AboveGround.Value = false;
		SplitLines.Value = true;
		AllowWalls.Value = true;
		BehindPlayer.Value = false;
		Offset.Value = 0;
		Glow.Value = true;
		GlowStrength.Value = 0.3f;
		GlowRadius.Value = 0.5f;
		Pop.Value = true;
		Wiggle.Value = true;
		Frequency.Value = 8;
		Amount.Value = 0.8f;
		Entrance.Value = 3;
		Exit.Value = 1;
	}

	internal void ApplyPreset(int id)
	{
		if (id == 0)
		{
			ApplyOriginalDefaults();
			return;
		}
		Glow.Value = id != 3;
		Pop.Value = id == 0 || id == 2;
		Wiggle.Value = id != 3;
		Frequency.Value = id switch
		{
			2 => 12,
			1 => 4,
			_ => 8,
		};
		Amount.Value = id switch
		{
			3 => 0f,
			2 => 1.1f,
			1 => 0.25f,
			_ => 0.8f,
		};
		Entrance.Value = id switch
		{
			3 => 3,
			2 => 2,
			1 => 1,
			_ => 0,
		};
		Exit.Value = id switch
		{
			3 => 3,
			2 => 2,
			_ => 1,
		};
	}
}
