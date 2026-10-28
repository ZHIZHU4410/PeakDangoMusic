using BepInEx.Configuration;
using DangoMusic.Core;

namespace PeakDangoMusic;
internal sealed class MusicSettings
{
    internal readonly ConfigEntry<bool> Enabled,Translation,Glow,Pop,Wiggle,AllowWalls,BehindPlayer,Smart,Follow,FacePlayer,KeepAway,AboveGround,SplitLines;
    internal readonly ConfigEntry<int> Port,Mode,Frequency,Offset,Entrance,Exit,Source,NetEasePort,GlowColor;
    internal readonly ConfigEntry<float> FontSize,Distance,Amount,Brightness,Spread,GlowStrength,GlowRadius;
    internal readonly ConfigEntry<string> PlayerPath;
    internal readonly ConfigEntry<string> Style;
    internal readonly ConfigEntry<int> FoliaPlacement,FoliaQuality,FoliaFps,FoliaIntensity;
    internal readonly ConfigEntry<float> FoliaSize,FoliaOpacity,FoliaFontScale;
    internal readonly ConfigEntry<bool> FoliaBackground,FoliaSoftware;
    internal MusicSettings(ConfigFile config)
    {
        Enabled=config.Bind("连接","启用漂浮歌词",true,"F8 显示或隐藏歌词；播放器自行播放，模组只读同步。");
        Source=config.Bind("连接","播放器来源",0,new ConfigDescription("0=BLF/LX本机API；1=原版网易云（Windows媒体接口与本机进度接口）。",new AcceptableValueRange<int>(0,1)));
        NetEasePort=config.Bind("连接","网易云本机端口",9223,new ConfigDescription("原版网易云可选本机进度连接端口。未公开Windows播放进度时，使用包内脚本启动网易云。",new AcceptableValueRange<int>(1,65535)));
        Port=config.Bind("连接","LX本机端口",23330,new ConfigDescription("BLF / LX 设置→开放 API 的端口，仅连接127.0.0.1。",new AcceptableValueRange<int>(1,65535)));
        PlayerPath=config.Bind("连接","播放器路径","","可选：填写你自己的 BLF/LX exe 路径，仅用于打开播放器按钮；手动打开播放器无需设置。");
        Style=config.Bind("样式","歌词显示样式","dango",new ConfigDescription("dango保留原有显示；其余为Folia的15种内置样式；random为每首随机。",new AcceptableValueList<string>(FoliaStyles.Ids)));
        FoliaPlacement=config.Bind("样式","Folia舞台位置",0,new ConfigDescription("0游戏内两侧悬浮舞台，界面中自动屏幕接续；1始终两侧屏幕；2全屏舞台。",new AcceptableValueRange<int>(0,2)));
        FoliaQuality=config.Bind("样式","Folia画质",1,new ConfigDescription("0=640x360，1=960x540，2=1280x720。",new AcceptableValueRange<int>(0,2)));
        FoliaFps=config.Bind("样式","Folia帧率",24,new ConfigDescription("舞台绘制帧率，降低可减少占用。",new AcceptableValueList<int>(15,24,30,60)));
        FoliaIntensity=config.Bind("样式","Folia动效强度",1,new ConfigDescription("0轻柔、1标准、2活泼。",new AcceptableValueRange<int>(0,2)));
        FoliaSize=config.Bind("样式","Folia舞台大小",1f,new ConfigDescription("调整两侧舞台大小，不改变原歌词距离。",new AcceptableValueRange<float>(.5f,1.8f)));
        FoliaOpacity=config.Bind("样式","Folia舞台透明度",.95f,new ConfigDescription("舞台整体透明度。",new AcceptableValueRange<float>(.2f,1)));
        FoliaFontScale=config.Bind("样式","Folia字体倍率",1f,new ConfigDescription("传入原样式的歌词字体倍率；自适应排版样式按自身规则缩放。",new AcceptableValueRange<float>(.5f,2)));
        FoliaBackground=config.Bind("样式","Folia舞台底色",false,"显示舞台底色；样式自身的色块、齿轮等装饰仍按原实现绘制。");
        FoliaSoftware=config.Bind("样式","Folia兼容渲染",false,"仅在显卡渲染失败时开启；使用软件图形模式，可能增加CPU占用。");
        Translation=config.Bind("显示","显示汉化歌词",false,"PEAK扩展选项；原项目只显示原文，默认关闭附加翻译。");
        Mode=config.Bind("显示","播放方式",0,new ConfigDescription("0整句；1单词（中文无词库时按字）；2逐字。",new AcceptableValueRange<int>(0,2)));
        FontSize=config.Bind("显示","世界字号",1f,new ConfigDescription("对应原项目Scale：0.5–3，默认1倍；Unity字号根据倍率和距离换算。",new AcceptableValueRange<float>(.5f,3)));
        Brightness=config.Bind("显示","文字亮度",1f,new ConfigDescription("PEAK材质增益，默认1，不额外提高原项目暖白字色亮度。",new AcceptableValueRange<float>(1,6)));
        Distance=config.Bind("显示","歌词距离",10f,new ConfigDescription("对应原项目Distance：3–40，默认10。",new AcceptableValueRange<float>(3,40)));
        Spread=config.Bind("显示","左右散布角",30f,new ConfigDescription("对应原项目Spread：0–60度，默认30；在左右两侧交替生成。",new AcceptableValueRange<float>(0,60)));
        Follow=config.Bind("显示","跟随视角",false,"PEAK扩展：固定跟随相机的两侧位置，默认关闭，避免替代原项目世界漂浮行为。");
        Smart=config.Bind("显示","智能防离屏",false,"对应原项目Stay on screen，默认关闭；开启后提前接续到侧面，保留演唱进度。");
        FacePlayer=config.Bind("显示","始终朝向玩家",true,"对应原项目Face player，默认开启。");
        KeepAway=config.Bind("显示","靠近时后退",true,"对应原项目Keep away，默认开启，维持距离设置的60%为最小间距。");
        AboveGround=config.Bind("显示","保持视线以上",false,"对应原项目Above ground，默认关闭；开启时最低为相机下方0.5单位。");
        SplitLines=config.Bind("显示","自动拆分合并歌词",true,"对应原项目Split lines，默认开启；中日韩句子之间的空格用于拆句，保留逐字时间。");
        AllowWalls=config.Bind("显示","允许出现在墙内",true,"关闭时通过场景射线和空间检测避开实体岩壁；没有空位就跳过。");
        BehindPlayer=config.Bind("显示","位于人物后景",false,"开启后使用正常场景深度，让近处人物和岩壁遮挡歌词。");
        Offset=config.Bind("显示","时间偏移毫秒",0,new ConfigDescription("正值提前显示，负值延后显示。",new AcceptableValueRange<int>(-10000,10000)));
        Glow=config.Bind("动画","白色光晕",true,"使用文字材质可用的 SDF 外发光；不创建实时灯光。");
        GlowColor=config.Bind("动画","光晕颜色",0,new ConfigDescription("对应原项目Glow color：0随字色、1粉、2蓝、3金，默认0。",new AcceptableValueRange<int>(0,3)));
        GlowStrength=config.Bind("动画","光晕强度",.3f,new ConfigDescription("对应原项目Glow strength：0–3，默认0.3。",new AcceptableValueRange<float>(0,3)));
        GlowRadius=config.Bind("动画","光晕半径",.5f,new ConfigDescription("对应原项目Glow radius：0–1，默认0.5。",new AcceptableValueRange<float>(0,1)));
        Pop=config.Bind("动画","逐字弹出",true,"优先按播放器逐字时间；只有行时间时估算显现。");
        Wiggle=config.Bind("动画","轻微抖动",true,"动画只作用于文字，不改变角色或相机。");
        Frequency=config.Bind("动画","抖动频率",8,new ConfigDescription("对应原项目Wiggle fps：4–24，默认8。",new AcceptableValueRange<int>(4,24)));
        Amount=config.Bind("动画","抖动幅度",.8f,new ConfigDescription("对应原项目Wiggle amount：0–2，默认0.8。",new AcceptableValueRange<float>(0,2)));
        Entrance=config.Bind("动画","入场",3,new ConfigDescription("PEAK扩展：0淡入；1上浮；2弹入；3直接。默认3，仅保留原项目逐字显现。",new AcceptableValueRange<int>(0,3)));
        Exit=config.Bind("动画","退场",1,new ConfigDescription("0淡出；1上浮；2缩小；3直接消失。",new AcceptableValueRange<int>(0,3)));
    }
    internal float WorldFontSize=>2.8f*FontSize.Value*(Distance.Value/10f);
    internal void ApplyOriginalDefaults()
    {
        Style.Value="dango";
        Enabled.Value=true;Translation.Value=false;Mode.Value=0;FontSize.Value=1;Brightness.Value=1;Distance.Value=10;Spread.Value=30;
        Follow.Value=false;Smart.Value=false;FacePlayer.Value=true;KeepAway.Value=true;AboveGround.Value=false;SplitLines.Value=true;
        AllowWalls.Value=true;BehindPlayer.Value=false;Offset.Value=0;
        Glow.Value=true;GlowColor.Value=0;GlowStrength.Value=.3f;GlowRadius.Value=.5f;Pop.Value=true;Wiggle.Value=true;Frequency.Value=8;Amount.Value=.8f;Entrance.Value=3;Exit.Value=1;
    }
    internal void ApplyPreset(int id)
    {
        if(id==0){ApplyOriginalDefaults();return;}
        Glow.Value=id!=3;Pop.Value=id==0 || id==2;Wiggle.Value=id!=3;
        Frequency.Value=id==1?4:id==2?12:8;Amount.Value=id==1?.25f:id==2?1.1f:id==3?0:.8f;
        Entrance.Value=id==1?1:id==2?2:id==3?3:0;Exit.Value=id==2?2:id==3?3:1;
    }
}
