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
    internal const string Version="1.1.1";
    internal static Plugin? Instance {get;private set;}
    internal MusicSettings Settings=null!;
    internal MusicClient Client=null!;
    internal WorldLyrics Renderer=null!;
    MusicWindow window=null!;
    Harmony? harmony;
    internal bool BlocksPlayerInput=>window?.BlocksPlayerInput ?? false;
    float saveAt=-1,previewUntil;
    long previewTick;
    MusicState? preview;
    internal string Notice="";
    void Awake()
    {
        Instance=this;Config.SaveOnConfigSet=false;Settings=new MusicSettings(Config);Client=new MusicClient(Settings);Renderer=new WorldLyrics(Settings,Logger);
        Config.SettingChanged+=Changed;
        window=new MusicWindow();
        harmony=new Harmony("local.peak.dangomusic");harmony.PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo("DangoMusic: F9 独立设置；F8 显隐。已有全能助手可用 F7→音乐。只读连接 BLF/LX 本机开放 API，默认23330。");
    }
    void Changed(object sender,SettingChangedEventArgs args){saveAt=Time.unscaledTime+1;Renderer?.Invalidate();}
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
        if(!Settings.Enabled.Value || !previewing && scene!="WilIsland" || Character.localCharacter==null || Camera.main==null)
        {Renderer.Hide();return;}
        long now=MusicClock.Now;
        MusicState state=previewing?new MusicState(true,"preview",preview!.Title,preview.Artist,Math.Clamp(now-previewTick,0,8000),8000,true,now,1,preview.Timeline,1,"预览中"):Client.Snapshot;
        Renderer.Tick(state,Camera.main,now);
    }
    void LateUpdate(){window?.KeepCursor();TickLyrics();}
    void OnDisable(){window?.Close();}
    void OnGUI(){window?.Draw();}
    internal void Preview()
    {
        Settings.Enabled.Value=true;
        previewTick=MusicClock.Now;previewUntil=Time.unscaledTime+8;
        var words=new[]{new LyricsWord(0,1400,"跟着"),new LyricsWord(1400,1400,"音乐，"),new LyricsWord(2800,1600,"继续"),new LyricsWord(4400,2600,"攀登")};
        var timeline=LyricsTimeline.FromLines(new[]{new LyricsLine(0,8000,"跟着音乐，继续攀登",words,"Climb with the rhythm · 함께 올라가요")});
        preview=new MusicState(true,"preview","歌词效果预览","DangoMusic",0,8000,true,previewTick,1,timeline,1,"预览中");
        Notice="已开启8秒预览；机场或地图内均可观察。";
        window.Close();
    }
    void OnDestroy()
    {
        Config.SettingChanged-=Changed;
        try{Config.Save();}finally{try{window?.Dispose();}finally{harmony?.UnpatchSelf();Client?.Dispose();Renderer?.Dispose();if(Instance==this)Instance=null;}}
    }
}
