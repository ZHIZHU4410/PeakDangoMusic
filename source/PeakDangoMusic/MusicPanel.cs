using System.Diagnostics;
using System.Globalization;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>Settings shared by the standalone window and the optional PEAK menu tab.</summary>
public static class MusicPanel
{
    static string? portText,pathText;
    static GUIStyle? hint,title,rowLabel,valueStyle,buttonStyle,presetStyle,selectedStyle,selectedPresetStyle,sliderTrack;
    static Texture2D? pressedTexture,selectedTexture;
    static Font? numericFont;
    static GUISkin? styleSkin;
    static Vector2 connectionScroll,settingsScroll,narrowScroll;
    static int settingsTab,fontPage;
    static float entranceAt=-1;
    static float feedbackAt=-10;
    static string feedback="";
    static string fontSearch="",fontFolderEdit="";
    const float RowLabelWidth=144,ValueWidth=96,ControlGap=12;
    static readonly string[] Modes={"整句","单词","逐字（整句）"};
    static readonly string[] Tabs={"歌词显示与位置","动画与效果","Folia歌词样式","字体"};
    static readonly string[] Sources={"BLF / LX","原版网易云"};
    static readonly string[] EffectPresets={"原项目默认","轻柔漂浮","活泼弹跳","静态清晰"};
    static readonly string[] Entrance={"淡入","上浮","弹入","直接"},Exit={"淡出","上浮","缩小","直接"};
    internal static void ResetEntrance()=>entranceAt=-1;
    internal static string Feedback=>Time.unscaledTime-feedbackAt<2.2f?feedback:"";
    static void Styles()
    {
        if(ReferenceEquals(styleSkin,GUI.skin))return;
        styleSkin=GUI.skin;
        hint=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=14};
        title=new GUIStyle(GUI.skin.label){wordWrap=true,fontStyle=FontStyle.Bold,fontSize=20};
        rowLabel=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleLeft,wordWrap=true,fontSize=16,margin=new RectOffset()};
        valueStyle=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleRight,fontSize=15,margin=new RectOffset(),padding=new RectOffset()};
        sliderTrack=new GUIStyle(GUI.skin.horizontalSlider){fixedHeight=24,margin=new RectOffset(),padding=new RectOffset(),border=new RectOffset()};
        foreach(var state in new[]{sliderTrack.normal,sliderTrack.hover,sliderTrack.active,sliderTrack.focused})state.background=null;
        numericFont ??= Font.CreateDynamicFontFromOSFont(new[]{"Cascadia Mono","Consolas","Courier New"},16);
        valueStyle.font=numericFont;
        if(pressedTexture!=null)UnityEngine.Object.Destroy(pressedTexture);
        if(selectedTexture!=null)UnityEngine.Object.Destroy(selectedTexture);
        pressedTexture=ButtonSurface(new Color(.055f,.09f,.12f,1));
        selectedTexture=ButtonSurface(new Color(.10f,.31f,.35f,1));
        buttonStyle=new GUIStyle(GUI.skin.button){border=new RectOffset(),wordWrap=true};
        buttonStyle.active.background=pressedTexture;
        buttonStyle.onActive.background=pressedTexture;
        buttonStyle.active.textColor=Color.white;
        buttonStyle.onActive.textColor=Color.white;
        presetStyle=new GUIStyle(buttonStyle)
        {
            alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip,
            padding=new RectOffset(12,12,10,10),margin=new RectOffset(0,0,0,0),fixedHeight=48
        };
        selectedStyle=new GUIStyle(buttonStyle);
        selectedStyle.normal.background=GUI.skin.button.onNormal.background??selectedTexture;
        selectedStyle.hover.background=GUI.skin.button.onHover.background??selectedTexture;
        selectedStyle.normal.textColor=Color.white;
        selectedStyle.hover.textColor=Color.white;
        selectedPresetStyle=new GUIStyle(presetStyle);
        selectedPresetStyle.normal.background=GUI.skin.button.onNormal.background??selectedTexture;
        selectedPresetStyle.hover.background=GUI.skin.button.onHover.background??selectedTexture;
        selectedPresetStyle.normal.textColor=Color.white;
        selectedPresetStyle.hover.textColor=Color.white;
    }
    public static void Draw()
    {
        var plugin=Plugin.Instance;if(plugin==null){GUILayout.Label("漂浮歌词模块未加载，请重新打开 PEAK。");return;}
        Styles();
        GUILayout.Label("DangoMusic · 地图漂浮歌词",title);
        GUILayout.Label("游戏选项 → 悬浮歌词 → 打开设置；歌词显隐也在此菜单中调整。",hint);
        DrawGroup(()=>Connection(plugin),0);GUILayout.Space(24);DrawGroup(()=>Fonts(plugin),.07f);GUILayout.Space(24);DrawGroup(()=>FoliaStyle(plugin),.14f);GUILayout.Space(24);DrawGroup(()=>Display(plugin),.21f);GUILayout.Space(24);DrawGroup(()=>Effects(plugin),.28f);
    }
    internal static void DrawStandalone(float availableWidth)
    {
        var plugin=Plugin.Instance;if(plugin==null)return;
        Styles();
        float scrollInset=GUI.skin.verticalScrollbar.fixedWidth+GUI.skin.verticalScrollbar.margin.horizontal+GUI.skin.scrollView.padding.horizontal+12;
        if(availableWidth<880)
        {
            narrowScroll=GUILayout.BeginScrollView(narrowScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(180,availableWidth-scrollInset)));
            DrawGroup(()=>Connection(plugin),0);GUILayout.Space(24);DrawGroup(()=>Fonts(plugin),.07f);GUILayout.Space(24);DrawGroup(()=>FoliaStyle(plugin),.14f);GUILayout.Space(24);DrawGroup(()=>Display(plugin),.21f);GUILayout.Space(24);DrawGroup(()=>Effects(plugin),.28f);
            GUILayout.EndVertical();GUILayout.EndScrollView();return;
        }
        float leftWidth=availableWidth*.36f,rightWidth=availableWidth-leftWidth-22;
        float contentInset=GUI.skin.box.padding.horizontal+scrollInset;
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width(leftWidth),GUILayout.ExpandHeight(true));
        connectionScroll=GUILayout.BeginScrollView(connectionScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
        GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(180,leftWidth-contentInset)));
        DrawGroup(()=>Connection(plugin),0);
        GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndVertical();
        GUILayout.Space(22);
        GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width(rightWidth),GUILayout.ExpandHeight(true));
        int tab=Selection(settingsTab,Tabs);
        if(tab!=settingsTab){settingsTab=tab;settingsScroll=Vector2.zero;ResetEntrance();}
        GUILayout.Space(16);
        settingsScroll=GUILayout.BeginScrollView(settingsScroll,false,false,GUIStyle.none,GUI.skin.verticalScrollbar);
        GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(180,rightWidth-contentInset)));
        if(settingsTab==0)DrawGroup(()=>Display(plugin),.08f);else if(settingsTab==1)DrawGroup(()=>Effects(plugin),.08f);else if(settingsTab==2)DrawGroup(()=>FoliaStyle(plugin),.08f);else DrawGroup(()=>Fonts(plugin),.08f);
        GUILayout.EndVertical();GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();
    }
    static void Connection(Plugin plugin)
    {
        var s=plugin.Settings;
        SectionTitle("播放器连接");
        s.Source.Value=NamedSelection("播放器来源",s.Source.Value,Sources);GUILayout.Space(24);
        SectionTitle("播放状态");
        Toggle("启用漂浮歌词",s.Enabled);
        var state=plugin.Client.Snapshot;
        GUILayout.Label(state.Message,hint);
        GUILayout.Label("同步歌词："+state.Timeline.Lines.Count+" 行",hint);
        GUILayout.Label(plugin.LyricDisplayStatus,hint);
        GUILayout.Label(plugin.TextRenderStatus,hint);
        if(s.Style.Value=="dango" && state.Timeline.Lines.Count>0)
        {
            int lyricIndex=state.Timeline.FindIndex(state.PositionAt(MusicClock.Now)??state.PositionMs);
            if(lyricIndex>=0 && state.Timeline.Lines[lyricIndex].Words is not {Count:>0})
                GUILayout.Label("当前歌词只有行时间，逐字同步为估算。",hint);
        }
        if(state.Title.Length>0)
        {
            GUILayout.Label(BalancedTitle(state.Title+" — "+state.Artist));
            long position=state.PositionAt(MusicClock.Now)??state.PositionMs;
            GUILayout.Label((state.Playing?"播放中  ":"已暂停  ")+TimeText(position)+" / "+TimeText(state.DurationMs),valueStyle);
        }
        GUILayout.Space(24);SectionTitle("连接设置");
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
        GUILayout.Space(24);SectionTitle("使用提示");
        GUILayout.Label("主菜单、机场、地图和设置界面均可接续显示歌词。音乐由播放器独立播放，不随窗口暂停。联机队友继续运行。",hint);
    }
    static void Fonts(Plugin plugin)
    {
        var s=plugin.Settings;
        SectionTitle("歌词字体");
        GUILayout.Label("当前："+(s.FontPath.Value.Length==0?"系统默认":Path.GetFileNameWithoutExtension(s.FontPath.Value)),hint);
        if(Button("恢复系统字体"))s.FontPath.Value="";
        GUILayout.Space(10);
        if(fontFolderEdit.Length==0)fontFolderEdit=s.FontFolder.Value;
        GUILayout.Label("字体素材目录");
        fontFolderEdit=GUILayout.TextField(fontFolderEdit,GUILayout.MinWidth(0),GUILayout.MinHeight(44));
        if(Button("刷新字体列表")){s.FontFolder.Value=fontFolderEdit;plugin.Fonts.Refresh(fontFolderEdit);fontPage=0;}
        GUILayout.Label(plugin.Fonts.Status,hint);
        GUILayout.Label("搜索字体");string query=GUILayout.TextField(fontSearch,GUILayout.MinWidth(0),GUILayout.MinHeight(44));
        if(query!=fontSearch){fontSearch=query;fontPage=0;}
        var choices=plugin.Fonts.Choices.Where(f=>f.Path.Length>0&&(fontSearch.Length==0||f.Label.IndexOf(fontSearch,StringComparison.OrdinalIgnoreCase)>=0)).ToArray();
        int pages=Math.Max(1,(choices.Length+9)/10);fontPage=Math.Clamp(fontPage,0,pages-1);
        foreach(var choice in choices.Skip(fontPage*10).Take(10))
        {
            if(GUILayout.Button(new GUIContent(choice.Label,choice.Path),s.FontPath.Value==choice.Path?selectedStyle!:buttonStyle!,GUILayout.MinWidth(0),GUILayout.MinHeight(44)))
            {s.FontPath.Value=choice.Path;SetFeedback("已选择字体："+choice.Label);}
            GUILayout.Space(6);
        }
        GUILayout.BeginHorizontal();
        if(Button("上一页"))fontPage=Math.Max(0,fontPage-1);
        GUILayout.Label((fontPage+1)+" / "+pages,valueStyle,GUILayout.Width(70));
        if(Button("下一页"))fontPage=Math.Min(pages-1,fontPage+1);
        GUILayout.EndHorizontal();
        GUILayout.Label("字体只从本地读取；英文专用字体缺少中文时使用系统字体补齐。",hint);
        if(Button("预览20秒并返回游戏"))plugin.Preview(20);
    }
    static void Display(Plugin plugin)
    {
        var s=plugin.Settings;
        SectionTitle("歌词显示");
        s.Mode.Value=NamedSelection("播放方式",s.Mode.Value,Modes);
        if(s.Mode.Value==2)GUILayout.Label("整句保持同一位置，按演唱顺序逐字显现；下一句才换位置。",hint);
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
        GUILayout.Space(24);SectionTitle("位置与遮挡");
        Toggle("允许歌词出现在墙内",s.AllowWalls);Toggle("歌词位于人物后景",s.BehindPlayer);
        GUILayout.Label("后景模式使用场景深度；关闭穿墙时会避开实体岩壁，没有空间就跳过。",hint);
        GUILayout.Space(16);
        s.Offset.Value=(int)SliderRow("时间偏移",s.Offset.Value,-10000,10000,value=>value.ToString("F0",CultureInfo.InvariantCulture)+" ms",50);
        GUILayout.Label("正值提前，负值延后。",hint);
    }
    static void Effects(Plugin plugin)
    {
        var s=plugin.Settings;
        SectionTitle("效果预设");
        int preset=PresetGrid(-1,EffectPresets,true);
        if(preset>=0)s.ApplyPreset(preset);
        GUILayout.Space(24);SectionTitle("文字动画");
        Toggle("白色光晕",s.Glow);Toggle("逐字弹出",s.Pop);
        s.GlowColor.Value=NamedSelection("光晕颜色",s.GlowColor.Value,new[]{"随字色","粉色","蓝色","金色"});
        Slider("光晕强度",s.GlowStrength,0,3,"F2");Slider("光晕半径",s.GlowRadius,0,1,"F2");
        GUILayout.Space(24);SectionTitle("文字抖动");
        Toggle("轻微抖动",s.Wiggle);
        GUILayout.Label(s.Wiggle.Value?"频率控制每秒变化次数，幅度控制抖动强弱；幅度为0时没有抖动。":"抖动已关闭，下方参数暂不生效；重新开启后沿用原数值。",hint);
        bool wasEnabled=GUI.enabled;GUI.enabled=wasEnabled && s.Wiggle.Value;
        try
        {
            s.Frequency.Value=(int)SliderRow("抖动频率",s.Frequency.Value,4,24,value=>value.ToString("F0",CultureInfo.InvariantCulture)+" Hz",1);
            GUILayout.Space(12);Slider("抖动幅度",s.Amount,0,2,"F2");
        }
        finally{GUI.enabled=wasEnabled;}
        GUILayout.Space(12);s.Entrance.Value=NamedSelection("入场动画",s.Entrance.Value,Entrance);
        GUILayout.Space(14);s.Exit.Value=NamedSelection("退场动画",s.Exit.Value,Exit);
        GUILayout.Space(22);
        if(Button("预览8秒并返回游戏"))plugin.Preview();
        GUILayout.Label("优先使用播放器逐字时间；普通 LRC 按行时长估算。歌词只在本机显示。",hint);
    }
    static void FoliaStyle(Plugin plugin)
    {
        var s=plugin.Settings;int selected=FoliaStyles.Index(s.Style.Value);
        SectionTitle("歌词显示样式");
        GUILayout.Label("保留Dango原样式，新增Folia全部15种内置样式。选择后自动保存。",hint);
        GUILayout.Space(12);
        int choice=PresetGrid(selected,FoliaStyles.Labels);
        if(choice!=selected){s.Style.Value=FoliaStyles.Ids[choice];selected=choice;}
        GUILayout.Space(8);
        GUILayout.Label(FoliaStyles.Descriptions[selected],hint);GUILayout.Space(16);
        if(selected==0){GUILayout.Label("Dango沿用当前左右位置、字号、亮度和动画设置。",hint);return;}
        GUILayout.Label(plugin.TextRenderStatus,hint);
        if(Button("重新准备Folia样式"))plugin.RestartFolia();
        GUILayout.Space(24);SectionTitle("舞台显示");
        s.FoliaPlacement.Value=NamedSelection("舞台位置",s.FoliaPlacement.Value,new[]{"两侧悬浮","两侧屏幕","全屏舞台"});
        GUILayout.Label("两侧悬浮在菜单、加载和设置界面自动接续为屏幕舞台。",hint);
        Slider("舞台大小",s.FoliaSize,.5f,1.8f,"F2");
        Slider("整体透明度",s.FoliaOpacity,.2f,1,"F2");
        Slider("Folia字体倍率",s.FoliaFontScale,.5f,2,"F2");
        Toggle("舞台底色",s.FoliaBackground);
        s.FoliaIntensity.Value=NamedSelection("动效强度",s.FoliaIntensity.Value,new[]{"轻柔","标准","活泼"});
        GUILayout.Space(24);SectionTitle("画质与性能");
        s.FoliaQuality.Value=NamedSelection("画质",s.FoliaQuality.Value,new[]{"省资源 360p","均衡 540p","清晰 720p"});
        int[] fps={15,24,30,60};
        s.FoliaFps.Value=fps[NamedSelection("舞台帧率",Math.Max(0,Array.IndexOf(fps,s.FoliaFps.Value)),new[]{"15","24","30","60"})];
        Toggle("兼容渲染（显卡渲染失败时开启）",s.FoliaSoftware);
        GUILayout.Label("原样式使用现有曲目、时间轴和歌词。未提供的音频频谱／节拍／封面不会伪造；相关附加效果以实际输入为准。",hint);
        GUILayout.Space(16);
        if(Button("预览20秒并返回游戏"))plugin.Preview(20);
        GUILayout.Label("使用「启用漂浮歌词」开关显隐；切回Dango会停止Folia舞台以释放资源。",hint);
    }
    static bool Button(string label,float width=0)
    {
        bool clicked=width>0?GUILayout.Button(label,buttonStyle!,GUILayout.Width(width),GUILayout.MinHeight(48)):GUILayout.Button(label,buttonStyle!,GUILayout.MinHeight(48));
        if(clicked)SetFeedback("已执行："+label);
        return clicked;
    }
    static int Selection(int selected,string[] labels)
    {
        Rect area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.MinWidth(0),GUILayout.ExpandWidth(true),GUILayout.Height(48));
        float gap=10,width=Mathf.Max(0,(area.width-gap*(labels.Length-1))/labels.Length);
        for(int i=0;i<labels.Length;i++)
        {
            if(GUI.Button(new Rect(area.x+i*(width+gap),area.y,width,area.height),labels[i],selected==i?selectedStyle!:buttonStyle!))
            {selected=i;SetFeedback("已选择："+labels[i]);}
        }
        return selected;
    }
    static int NamedSelection(string label,int selected,string[] labels)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label,rowLabel,GUILayout.Width(RowLabelWidth),GUILayout.Height(48));
        GUILayout.Space(ControlGap);
        int result=Selection(selected,labels);
        GUILayout.EndHorizontal();
        return result;
    }
    static void Toggle(string label,ConfigEntry<bool> entry)
    {
        GUILayout.BeginHorizontal(GUILayout.MinHeight(48));
        GUILayout.Label(label,rowLabel,GUILayout.MinWidth(0),GUILayout.ExpandWidth(true));
        bool value=GUILayout.Toggle(entry.Value,entry.Value?"已开启":"已关闭",buttonStyle!,GUILayout.Width(ValueWidth),GUILayout.Height(48));
        GUILayout.EndHorizontal();if(value!=entry.Value){entry.Value=value;SetFeedback(label+(value?"：已开启":"：已关闭"));}
        GUILayout.Space(8);
    }
    static void Slider(string label,ConfigEntry<float> entry,float min,float max,string format)
    {
        float step=format=="F0"?1:format=="F1"?.1f:.01f;
        float value=SliderRow(label,entry.Value,min,max,value=>value.ToString(format,CultureInfo.InvariantCulture),step);
        if(GUI.enabled && Math.Abs(value-entry.Value)>.001f){entry.Value=value;SetFeedback(label+"："+value.ToString(format));}
        GUILayout.Space(16);
    }
    static float SliderRow(string label,float value,float min,float max,Func<float,string> display,float step)
    {
        Rect area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.MinWidth(0),GUILayout.ExpandWidth(true),GUILayout.Height(36));
        float labelWidth=Mathf.Min(RowLabelWidth,area.width*.38f);
        float valueWidth=Mathf.Min(ValueWidth,area.width*.25f);
        GUI.Label(new Rect(area.x,area.y,labelWidth,area.height),label,rowLabel!);
        var slider=new Rect(area.x+labelWidth+ControlGap,area.y+(area.height-24)*.5f,Mathf.Max(0,area.width-labelWidth-valueWidth-ControlGap*2),24);
        float thumbWidth=GUI.skin.horizontalSliderThumb.fixedWidth;
        GUI.Box(new Rect(slider.x+thumbWidth*.5f,area.center.y-4,Mathf.Max(0,slider.width-thumbWidth),8),GUIContent.none,GUI.skin.horizontalSlider);
        float next=GUI.HorizontalSlider(slider,Mathf.Clamp(value,min,max),min,max,sliderTrack!,GUI.skin.horizontalSliderThumb);
        next=Mathf.Clamp(Mathf.Round(next/step)*step,min,max);
        // Paint the value after handling the slider, using exactly the value we save.
        NumericReadout.Draw(new Rect(area.xMax-valueWidth,area.y,valueWidth,area.height),display(next));
        return next;
    }
    static void SectionTitle(string label)
    {
        GUILayout.Space(4);
        GUILayout.Label(label,title);
        GUILayout.Space(14);
    }
    static string BalancedTitle(string text)
    {
        if(text.Length<=24)return text;
        int middle=text.Length/2,best=-1,distance=int.MaxValue;
        for(int i=8;i<text.Length-8;i++)
            if((text[i]==' '||text[i]=='·'||text[i]=='—'||text[i]=='/'||text[i]=='、')&&Math.Abs(i-middle)<distance)
            {best=i;distance=Math.Abs(i-middle);}
        return best<0?text:text.Substring(0,best).TrimEnd()+"\n"+text.Substring(best+1).TrimStart();
    }
    static void DrawGroup(Action draw,float delay)
    {
        if(entranceAt<0)entranceAt=Time.unscaledTime;
        var oldColor=GUI.color;
        if(Event.current.type==EventType.Repaint)
        {
            float progress=Mathf.Clamp01((Time.unscaledTime-entranceAt-delay)/.32f);
            progress=progress*progress*(3-2*progress);
            GUI.color=new Color(oldColor.r,oldColor.g,oldColor.b,oldColor.a*progress);
        }
        try{draw();}finally{GUI.color=oldColor;}
    }
    static void SetFeedback(string message){feedback=message;feedbackAt=Time.unscaledTime;}
    static Texture2D ButtonSurface(Color fill)
    {
        var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};
        texture.SetPixel(0,0,fill);texture.Apply();return texture;
    }
    static int PresetGrid(int selected,string[] labels,bool momentary=false)
    {
        const int columns=2;
        const float columnGap=12,rowGap=8,rowHeight=48;
        int rows=(labels.Length+columns-1)/columns;
        // Reserve the whole grid once: every row shares the same column boundaries,
        // including the final unpaired item, in both standalone and embedded panels.
        Rect area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,
            GUILayout.MinWidth(0),GUILayout.ExpandWidth(true),GUILayout.Height(rows*rowHeight+Math.Max(0,rows-1)*rowGap));
        float width=Mathf.Max(0,(area.width-columnGap)/columns);
        int result=selected;
        for(int i=0;i<labels.Length;i++)
        {
            var cell=new Rect(area.x+(i%columns)*(width+columnGap),area.y+(i/columns)*(rowHeight+rowGap),width,rowHeight);
            if(GUI.Button(cell,labels[i],!momentary&&selected==i?selectedPresetStyle!:presetStyle!))
            {result=i;SetFeedback("已选择："+labels[i]);}
        }
        return result;
    }
    static string TimeText(long ms)=>((int)(ms/60000)).ToString("00")+":"+((int)(ms/1000%60)).ToString("00");
}
