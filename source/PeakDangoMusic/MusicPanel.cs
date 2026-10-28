using System.Diagnostics;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>Settings shared by the standalone window and the optional PEAK menu tab.</summary>
public static class MusicPanel
{
    static string? portText,pathText;
    static GUIStyle? hint,title,toggleStyle;
    static GUISkin? styleSkin;
    static Vector2 connectionScroll,settingsScroll,narrowScroll;
    static int settingsTab;
    static readonly string[] Modes={"整句","单词","逐字"};
    static readonly string[] Tabs={"歌词显示与位置","动画与效果","Folia歌词样式"};
    static readonly string[] Sources={"BLF / LX","原版网易云"};
    static readonly string[] Entrance={"淡入","上浮","弹入","直接"},Exit={"淡出","上浮","缩小","直接"};
    static void Styles()
    {
        if(ReferenceEquals(styleSkin,GUI.skin))return;
        styleSkin=GUI.skin;
        hint=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=14};
        title=new GUIStyle(GUI.skin.label){wordWrap=true,fontStyle=FontStyle.Bold,fontSize=20};
        toggleStyle=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,wordWrap=true};
    }
    public static void Draw()
    {
        var plugin=Plugin.Instance;if(plugin==null){GUILayout.Label("漂浮歌词模块未加载，请重新打开 PEAK。");return;}
        Styles();
        GUILayout.Label("DangoMusic · 地图漂浮歌词",title);
        GUILayout.Label("F9 打开独立设置并暂停本机游戏；关闭后恢复。联机队友继续运行。",hint);
        Connection(plugin);GUILayout.Space(24);FoliaStyle(plugin);GUILayout.Space(24);Display(plugin);GUILayout.Space(24);Effects(plugin);
    }
    internal static void DrawStandalone(float availableWidth)
    {
        var plugin=Plugin.Instance;if(plugin==null)return;
        Styles();
        if(availableWidth<880)
        {
            narrowScroll=GUILayout.BeginScrollView(narrowScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(180,availableWidth-28)));
            Connection(plugin);GUILayout.Space(24);FoliaStyle(plugin);GUILayout.Space(24);Display(plugin);GUILayout.Space(24);Effects(plugin);
            GUILayout.EndVertical();GUILayout.EndScrollView();return;
        }
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width(availableWidth*.36f),GUILayout.ExpandHeight(true));
        connectionScroll=GUILayout.BeginScrollView(connectionScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
        GUILayout.BeginVertical(GUILayout.Width(availableWidth*.36f-58));
        Connection(plugin);
        GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndVertical();
        GUILayout.Space(22);
        GUILayout.BeginVertical(GUI.skin.box,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
        int tab=Selection(settingsTab,Tabs);
        if(tab!=settingsTab){settingsTab=tab;settingsScroll=Vector2.zero;}
        GUILayout.Space(16);
        settingsScroll=GUILayout.BeginScrollView(settingsScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
        GUILayout.BeginVertical(GUILayout.Width(availableWidth*.64f-80));
        if(settingsTab==0)Display(plugin);else if(settingsTab==1)Effects(plugin);else FoliaStyle(plugin);
        GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();
    }
    static void Connection(Plugin plugin)
    {
        var s=plugin.Settings;
        GUILayout.Label("播放器来源",title);
        s.Source.Value=Selection(s.Source.Value,Sources);GUILayout.Space(18);
        GUILayout.Label("播放状态",title);
        Toggle("启用漂浮歌词（F8）",s.Enabled);
        var state=plugin.Client.Snapshot;
        GUILayout.Label(state.Message,hint);
        GUILayout.Label("同步歌词："+state.Timeline.Lines.Count+" 行",hint);
        GUILayout.Label(plugin.LyricDisplayStatus,hint);
        GUILayout.Label(plugin.TextRenderStatus,hint);
        if(state.Title.Length>0)
        {
            GUILayout.Label(state.Title+" — "+state.Artist);
            long position=state.PositionAt(MusicClock.Now)??state.PositionMs;
            GUILayout.Label((state.Playing?"播放中  ":"已暂停  ")+TimeText(position)+" / "+TimeText(state.DurationMs),hint);
        }
        GUILayout.Space(24);GUILayout.Label("播放器连接",title);
        if(s.Source.Value==1)
        {
            GUILayout.Label("读取原版网易云的 Windows 媒体信息与本机播放进度，歌词由网易云公开接口获取。",hint);
            GUILayout.Space(10);
            if(Button("重新连接网易云"))plugin.Client.Reconnect();
            GUILayout.Space(12);
            GUILayout.Label("没有进度时，先退出网易云，再运行安装包内的“启动网易云歌词连接.cmd”。不需要登录账号或填写 Cookie。",hint);
            GUILayout.Label("此新增接口按要求未做实机测试。",hint);
            return;
        }
        GUILayout.Label("在 BLF / LX 的设置中开启本机开放 API，端口与下方保持一致。",hint);
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        GUILayout.Label("端口",GUILayout.Width(48));GUILayout.Space(8);
        portText=GUILayout.TextField(portText??s.Port.Value.ToString(),6,GUILayout.MinWidth(0),GUILayout.MinHeight(48));
        GUILayout.Space(10);
        if(Button("应用端口",110))
        {
            if(int.TryParse(portText,out int port) && port>=1 && port<=65535){s.Port.Value=port;plugin.Client.Reconnect();plugin.Notice="已更新本机连接端口。";}
            else plugin.Notice="端口应为1–65535。";
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(16);GUILayout.Label("播放器路径（可选）");
        GUILayout.Label("手动打开播放器时可留空；填写 exe 路径后可用下方按钮启动。",hint);
        pathText=GUILayout.TextField(pathText??s.PlayerPath.Value,GUILayout.MinWidth(0),GUILayout.MinHeight(48));
        GUILayout.Space(10);GUILayout.BeginHorizontal();
        if(Button("打开 BLF / LX"))
        {
            string path=(pathText??"").Trim();
            if(!File.Exists(path))plugin.Notice="未找到播放器，请检查上面的 exe 路径。";
            else try{s.PlayerPath.Value=path;Process.Start(new ProcessStartInfo(path){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Normal});plugin.Notice="已打开播放器。";}catch(Exception e){plugin.Notice="打开失败："+e.Message;}
        }
        GUILayout.Space(12);
        if(Button("重新连接"))plugin.Client.Reconnect();
        GUILayout.EndHorizontal();
        GUILayout.Space(14);
        if(Button("重新加载歌词字体"))plugin.Renderer.RetryFonts();
        if(plugin.Notice.Length>0){GUILayout.Space(12);GUILayout.Label(plugin.Notice,hint);}
        GUILayout.Space(24);GUILayout.Label("使用提示",title);
        GUILayout.Label("主菜单、机场、地图和设置界面均可接续显示歌词。音乐由播放器独立播放，不随窗口暂停。联机队友继续运行。",hint);
    }
    static void Display(Plugin plugin)
    {
        var s=plugin.Settings;
        GUILayout.Label("歌词显示",title);GUILayout.Space(8);
        s.Mode.Value=Selection(s.Mode.Value,Modes);
        if(s.Style.Value!="dango")GUILayout.Label("当前为Folia样式，自带逐词排版；上方三种播放方式用于Dango样式。",hint);
        GUILayout.Space(16);
        if(Button("恢复 Sorihiko 原项目默认")){s.ApplyOriginalDefaults();plugin.Notice="已恢复原项目歌词默认参数；播放器连接设置保留。";}
        GUILayout.Label("默认在左右两侧生成，固定世界位置；缩放1、距离10、散布30°。",hint);
        Toggle("显示播放器汉化歌词（扩展，默认关）",s.Translation);
        Toggle("自动拆分合并歌词",s.SplitLines);
        Toggle("智能防离屏（原项目默认关）",s.Smart);
        Toggle("固定跟随视角（扩展，默认关）",s.Follow);
        GUILayout.Space(12);
        Slider("文字亮度",s.Brightness,1,6,"F1");
        Slider("缩放倍率",s.FontSize,.5f,3,"F1");Slider("歌词距离",s.Distance,3,40,"F1");
        Slider("左右散布角",s.Spread,0,60,"F0");
        Toggle("始终朝向玩家",s.FacePlayer);Toggle("靠近时后退",s.KeepAway);Toggle("保持视线以上（默认关）",s.AboveGround);
        GUILayout.Space(16);GUILayout.Label("位置与遮挡",title);GUILayout.Space(8);
        Toggle("允许歌词出现在墙内",s.AllowWalls);Toggle("歌词位于人物后景",s.BehindPlayer);
        GUILayout.Label("后景模式使用场景深度；关闭穿墙时会避开实体岩壁，没有空间就跳过。",hint);
        GUILayout.Space(16);
        GUILayout.Label("时间偏移  "+s.Offset.Value+" ms");
        s.Offset.Value=(int)(Mathf.Round(GUILayout.HorizontalSlider(s.Offset.Value,-10000,10000,GUILayout.Height(26))/50)*50);
        GUILayout.Label("正值提前，负值延后。",hint);
    }
    static void Effects(Plugin plugin)
    {
        var s=plugin.Settings;
        GUILayout.Label("效果预设",title);GUILayout.Space(8);
        GUILayout.BeginHorizontal();Preset(plugin,"原项目默认",0);GUILayout.Space(12);Preset(plugin,"轻柔漂浮",1);GUILayout.EndHorizontal();
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();Preset(plugin,"活泼弹跳",2);GUILayout.Space(12);Preset(plugin,"静态清晰",3);GUILayout.EndHorizontal();
        GUILayout.Space(20);GUILayout.Label("文字动画",title);
        Toggle("白色光晕",s.Glow);Toggle("逐字弹出",s.Pop);Toggle("轻微抖动",s.Wiggle);
        GUILayout.Label("光晕颜色");s.GlowColor.Value=Selection(s.GlowColor.Value,new[]{"随字色","粉色","蓝色","金色"});
        Slider("光晕强度",s.GlowStrength,0,3,"F2");Slider("光晕半径",s.GlowRadius,0,1,"F2");
        GUILayout.Space(12);
        GUILayout.Label("抖动频率  "+s.Frequency.Value+" Hz");
        s.Frequency.Value=Mathf.RoundToInt(GUILayout.HorizontalSlider(s.Frequency.Value,4,24,GUILayout.Height(26)));
        GUILayout.Space(12);Slider("抖动幅度",s.Amount,0,2,"F2");
        GUILayout.Space(12);GUILayout.Label("入场动画");s.Entrance.Value=Selection(s.Entrance.Value,Entrance);
        GUILayout.Space(14);GUILayout.Label("退场动画");s.Exit.Value=Selection(s.Exit.Value,Exit);
        GUILayout.Space(22);
        if(Button("预览8秒并返回游戏"))plugin.Preview();
        GUILayout.Label("优先使用播放器逐字时间；普通 LRC 按行时长估算。歌词只在本机显示。",hint);
    }
    static void FoliaStyle(Plugin plugin)
    {
        var s=plugin.Settings;int selected=FoliaStyles.Index(s.Style.Value);
        GUILayout.Label("歌词显示样式",title);
        GUILayout.Label("保留Dango原样式，新增Folia全部15种内置样式。选择后自动保存。",hint);
        GUILayout.Space(12);
        for(int row=0;row<FoliaStyles.Ids.Length;row+=2)
        {
            GUILayout.BeginHorizontal();
            for(int i=row;i<Math.Min(row+2,FoliaStyles.Ids.Length);i++)
            {
                if(i>row)GUILayout.Space(12);
                if(GUILayout.Toggle(selected==i,FoliaStyles.Labels[i],toggleStyle!,GUILayout.MinWidth(0),GUILayout.ExpandWidth(true),GUILayout.MinHeight(48))&&selected!=i)
                {s.Style.Value=FoliaStyles.Ids[i];selected=i;}
            }
            GUILayout.EndHorizontal();GUILayout.Space(8);
        }
        GUILayout.Label(FoliaStyles.Descriptions[selected],hint);GUILayout.Space(16);
        if(selected==0){GUILayout.Label("Dango沿用当前左右位置、字号、亮度和动画设置。",hint);return;}
        GUILayout.Label(plugin.TextRenderStatus,hint);
        if(Button("重新准备Folia样式"))plugin.RestartFolia();
        GUILayout.Space(16);GUILayout.Label("舞台显示",title);
        s.FoliaPlacement.Value=Selection(s.FoliaPlacement.Value,new[]{"两侧悬浮","两侧屏幕","全屏舞台"});
        GUILayout.Label("两侧悬浮在菜单、加载和设置界面自动接续为屏幕舞台。",hint);
        Slider("舞台大小",s.FoliaSize,.5f,1.8f,"F2");
        Slider("整体透明度",s.FoliaOpacity,.2f,1,"F2");
        Slider("Folia字体倍率",s.FoliaFontScale,.5f,2,"F2");
        Toggle("舞台底色",s.FoliaBackground);
        GUILayout.Label("动效强度");s.FoliaIntensity.Value=Selection(s.FoliaIntensity.Value,new[]{"轻柔","标准","活泼"});
        GUILayout.Space(18);GUILayout.Label("画质与性能",title);
        s.FoliaQuality.Value=Selection(s.FoliaQuality.Value,new[]{"省资源 360p","均衡 540p","清晰 720p"});
        GUILayout.Label("舞台帧率");int[] fps={15,24,30,60};
        s.FoliaFps.Value=fps[Selection(Math.Max(0,Array.IndexOf(fps,s.FoliaFps.Value)),new[]{"15","24","30","60"})];
        Toggle("兼容渲染（显卡渲染失败时开启）",s.FoliaSoftware);
        GUILayout.Label("原样式使用现有曲目、时间轴和歌词。未提供的音频频谱／节拍／封面不会伪造；相关附加效果以实际输入为准。",hint);
        GUILayout.Space(16);
        if(Button("预览20秒并返回游戏"))plugin.Preview(20);
        GUILayout.Label("F8仍可显隐；切回Dango会停止Folia舞台以释放资源。",hint);
    }
    static bool Button(string label,float width=0)=>width>0?GUILayout.Button(label,GUILayout.Width(width),GUILayout.MinHeight(48)):GUILayout.Button(label,GUILayout.MinHeight(48));
    static int Selection(int selected,string[] labels)
    {
        GUILayout.BeginHorizontal();
        for(int i=0;i<labels.Length;i++)
        {
            if(i>0)GUILayout.Space(10);
            if(GUILayout.Toggle(selected==i,labels[i],GUI.skin.button,GUILayout.MinHeight(48)))selected=i;
        }
        GUILayout.EndHorizontal();return selected;
    }
    static void Toggle(string label,ConfigEntry<bool> entry)
    {
        bool value=GUILayout.Toggle(entry.Value,(entry.Value?"●  ":"○  ")+label,toggleStyle!,GUILayout.MinHeight(44));if(value!=entry.Value)entry.Value=value;
        GUILayout.Space(6);
    }
    static void Slider(string label,ConfigEntry<float> entry,float min,float max,string format)
    {
        GUILayout.Label(label+"  "+entry.Value.ToString(format));
        float value=GUILayout.HorizontalSlider(entry.Value,min,max,GUILayout.Height(26));
        if(Math.Abs(value-entry.Value)>.001f)entry.Value=value;
        GUILayout.Space(14);
    }
    static void Preset(Plugin plugin,string label,int id){if(Button(label))plugin.Settings.ApplyPreset(id);}
    static string TimeText(long ms)=>((int)(ms/60000)).ToString("00")+":"+((int)(ms/1000%60)).ToString("00");
}
