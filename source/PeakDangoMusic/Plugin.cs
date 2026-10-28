using BepInEx;
using BepInEx.Configuration;
using DangoMusic.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace PeakDangoMusic;

[BepInPlugin("local.peak.dangomusic","PEAK DangoMusic · BLF/LX 漂浮歌词",Plugin.Version)]
[DefaultExecutionOrder(1000)]
public sealed class Plugin : BaseUnityPlugin
{
    internal const string Version="1.2.0";
    internal static Plugin? Instance {get;private set;}
    internal MusicSettings Settings=null!;
    internal MusicClient Client=null!;
    internal WorldLyrics Renderer=null!;
    ScreenLyrics screen=null!;
    FoliaView folia=null!;
    bool screenMode;
    MusicWindow window=null!;
    Harmony? harmony;
    internal bool BlocksPlayerInput=>window?.BlocksPlayerInput ?? false;
    float saveAt=-1,previewUntil;
    long previewTick;
    long previewDuration=8000;
    MusicState? preview;
    internal string Notice="";
    internal string LyricDisplayStatus {get;private set;}="歌词显示：等待游戏场景";
    internal string TextRenderStatus=>Settings.Style.Value!="dango"?folia.Status:screenMode?"文字渲染：屏幕浮层（菜单／设置／加载）":Renderer.RenderStatus;
    internal void RestartFolia()=>folia.Restart();
    void Awake()
    {
        Instance=this;Config.SaveOnConfigSet=false;Settings=new MusicSettings(Config);Client=new MusicClient(Settings);Renderer=new WorldLyrics(Settings,Logger);screen=new ScreenLyrics(Settings);folia=new FoliaView(Settings);
        Config.SettingChanged+=Changed;
        window=new MusicWindow();
        harmony=new Harmony("local.peak.dangomusic");harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("DangoMusic: F9 独立设置；F8 显隐。已有全能助手可用 F7→音乐。只读连接 BLF/LX 本机开放 API，默认23330。");
    }
    void Changed(object sender,SettingChangedEventArgs args){saveAt=Time.unscaledTime+1;Renderer?.Invalidate();folia?.Invalidate();}
    void Update()
    {
        var keyboard=Keyboard.current;
        if(keyboard!=null && keyboard.f9Key.wasPressedThisFrame)window.Toggle();
        if(window.Open && keyboard!=null && keyboard.escapeKey.wasPressedThisFrame)window.Close();
        window.KeepCursor();
        if(keyboard!=null && keyboard.f8Key.wasPressedThisFrame && GUIUtility.keyboardControl==0)Settings.Enabled.Value=!Settings.Enabled.Value;
        Client.Configure(Settings);
        if(saveAt>=0 && Time.unscaledTime>=saveAt){saveAt=-1;Config.Save();}
    }
    void TickLyrics()
    {
        string scene=SceneManager.GetActiveScene().name;
        bool previewing=Time.unscaledTime<previewUntil && preview!=null;
        if(!Settings.Enabled.Value){LyricDisplayStatus="歌词显示：已关闭（F8）";Renderer.Hide();screen.Hide();folia.Suspend();return;}
        long now=MusicClock.Now;
        MusicState state=previewing?new MusicState(true,"preview",preview!.Title,preview.Artist,Math.Clamp(now-previewTick,0,previewDuration),previewDuration,true,now,1,preview.Timeline,1,"预览中"):Client.Snapshot;
        long? position=state.PositionAt(now);
        if(position==null)LyricDisplayStatus="歌词显示：等待有效播放进度";
        else if(!state.Playing)LyricDisplayStatus="歌词显示：播放器暂停，保留当前句";
        else if(state.Timeline.Lines.Count==0)LyricDisplayStatus="歌词显示：尚未收到同步歌词";
        else
        {
            int index=state.Timeline.FindIndex(Math.Clamp(position.Value+Settings.Offset.Value,0,state.DurationMs));
            LyricDisplayStatus=index<0?"歌词显示：等待第一句开始":
                "当前歌词："+state.Timeline.Lines[index].Text;
        }
        // The playback clock is independent of game time and scene transitions.
        // UI scenes may have no main camera or local character; use a transparent screen layer there.
        var camera=Camera.main;
        screenMode=window.Open || scene=="Title" || Cursor.visible || Cursor.lockState!=CursorLockMode.Locked || Character.localCharacter==null || camera==null;
        if(Settings.Style.Value!="dango")
        {
            if(folia.Tick(state,camera,screenMode,now)){Renderer.Hide();screen.Hide();return;}
        }
        else folia.Suspend();
        if(screenMode){Renderer.Hide();screen.Tick(state,now);}
        else{screen.Hide();Renderer.Tick(state,camera!,now);}
    }
    void LateUpdate(){window?.KeepCursor();TickLyrics();}
    void OnDisable(){window?.Close();folia?.Suspend();}
    void OnGUI(){window?.Draw();screen?.Draw();folia?.Draw();}
    internal void Preview(int seconds=8)
    {
        Settings.Enabled.Value=true;
        previewTick=MusicClock.Now;previewDuration=seconds*1000;previewUntil=Time.unscaledTime+seconds;
        var texts=new[]{"跟着音乐，继续攀登","山风穿过云海的边缘","每一句都落在此刻","让歌词陪你走向下一程"};
        var lines=texts.Select((text,index)=>new LyricsLine(index*previewDuration/4,previewDuration/4,text,null,"Climb with the rhythm"));
        var timeline=LyricsTimeline.FromLines(lines);
        preview=new MusicState(true,"preview","歌词效果预览","DangoMusic",0,previewDuration,true,previewTick,1,timeline,1,"预览中");
        Notice="已开启"+seconds+"秒预览；所有界面均可观察。";
        window.Close();
    }
    void OnDestroy()
    {
        Config.SettingChanged-=Changed;
        try{Config.Save();}finally{try{window?.Dispose();}finally{harmony?.UnpatchSelf();Client?.Dispose();Renderer?.Dispose();screen?.Dispose();folia?.Dispose();if(Instance==this)Instance=null;}}
    }
}
