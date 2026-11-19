using System;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using DangoMusic.Core;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;

namespace PeakDangoMusic;

[BepInPlugin("local.peak.dangomusic", "PEAK DangoMusic · Muonium 音乐播放器", "2.3.4")]
[DefaultExecutionOrder(1000)]
public sealed class Plugin : BaseUnityPlugin
{
	internal const string Version = "2.3.4";

	internal MusicSettings Settings;

	internal FontLibrary Fonts;

	internal MusicClient Client;

	internal AggregatePlayer Aggregate;

	internal MuoniumHub Hub;

	internal WorldLyrics Renderer;

	private ScreenLyrics screen;

	private FoliaView folia;

	private bool screenMode;

	private readonly AdaptiveLyrics contrast = new AdaptiveLyrics();

	private MusicWindow window;

	private Harmony? harmony;

	private float saveAt = -1f;

	private float previewUntil;

	private long previewTick;

	private long previewDuration = 8000L;

	private MusicState? preview;

	internal string Notice = "";

	internal static Plugin? Instance { get; private set; }

	internal bool BlocksPlayerInput => window?.BlocksPlayerInput ?? false;

	internal string LyricDisplayStatus { get; private set; } = "歌词显示：等待游戏场景";

	internal string TextRenderStatus
	{
		get
		{
			if (!(Settings.Style.Value != "dango"))
			{
				if (!screenMode)
				{
					return Renderer.RenderStatus;
				}
				return "文字渲染：屏幕浮层（菜单／设置／加载）";
			}
			return folia.Status;
		}
	}

	internal void Log(string message)
	{
		Logger.LogInfo((object)message);
	}

	internal void LogError(string message)
	{
		Logger.LogError((object)message);
	}

	internal void RestartFolia()
	{
		folia.Restart();
	}

	private void Awake()
	{
		DiagnosticLog.Clear();
		DiagnosticLog.Write = (string message) =>
		{
			Logger.LogWarning((object)message);
		};
		DiagnosticLog.Clear();
		DiagnosticLog.Write = (string message) =>
		{
			Logger.LogWarning((object)message);
		};
		AdaptiveLyrics.Current = contrast;
		Instance = this;
		((BaseUnityPlugin)this).Config.SaveOnConfigSet = false;
		Settings = new MusicSettings(((BaseUnityPlugin)this).Config);
		Fonts = new FontLibrary(Settings.FontFolder.Value);
		Aggregate = new AggregatePlayer(this);
		Client = new MusicClient(Settings, Aggregate);
		Hub = new MuoniumHub(this);
		Renderer = new WorldLyrics(Settings, Logger);
		screen = new ScreenLyrics(Settings);
		folia = new FoliaView(Settings);
		((BaseUnityPlugin)this).Config.SettingChanged += Changed;
		window = new MusicWindow();
		harmony = new Harmony("local.peak.dangomusic");
		harmony.PatchAll(typeof(Plugin).Assembly);
		Logger.LogInfo((object)"DangoMusic: 主菜单 → 音乐播放器，或游戏选项 → 音乐播放器。使用Muonium原生登录、曲库及GD聚合播放。");
		Logger.LogInfo((object)"DangoMusic slider numbers: embedded glyph renderer v1.");
		Logger.LogInfo((object)"DangoMusic adaptive contrast: game day/night + manual override; no screen sampling.");
	}

	private void Changed(object sender, SettingChangedEventArgs args)
	{
		saveAt = Time.unscaledTime + 1f;
		Renderer?.Invalidate();
		folia?.Invalidate();
		if ((object)args.ChangedSetting == Settings.FontPath)
		{
			Renderer?.RetryFonts();
			screen?.RetryFonts();
		}
	}

	private bool queuedOpen;
    internal void QueueOpenPlayer()=>queuedOpen=true;
	private void Update()
	{
        if(queuedOpen){queuedOpen=false;window.Show(null);}
		Keyboard current = Keyboard.current;
		bool flag = current != null && ((ButtonControl)current.escapeKey).wasPressedThisFrame;
		if (!flag && window.Open && current == null)
		{
			try
			{
				flag = Input.GetKeyDown((KeyCode)27);
			}
			catch (Exception)
			{
			}
		}

        bool hideMini = current != null && current.backslashKey.wasPressedThisFrame;
        bool togglePlayer = current != null && current.rightShiftKey.wasPressedThisFrame;
        if(current==null){try{hideMini=Input.GetKeyDown(KeyCode.Backslash);togglePlayer=Input.GetKeyDown(KeyCode.RightShift);}catch(Exception){}}
        var selected=EventSystem.current?.currentSelectedGameObject;
        bool typing=selected!=null && (selected.GetComponent<TMPro.TMP_InputField>()!=null || selected.GetComponent<UnityEngine.UI.InputField>()!=null);
        if(!typing){
            if(togglePlayer){if(window.Open)window.Close();else window.Show(null);}
            if(hideMini)window.ToggleMini();
        }

		if (window.CloseRequested)
		{
			window.Close();
		}
		else if (window.Open & flag)
		{
			window.Close();
		}
		window.CheckOwner();
		window.CheckResponsive();
        window.TickMotion();
		window.KeepModalState();
		Client.Configure(Settings);
		Aggregate.Tick(Settings.PlayerEnabled.Value, Settings.AggregateVolume.Value);
		Hub.Tick();
		if (saveAt >= 0f && Time.unscaledTime >= saveAt)
		{
			saveAt = -1f;
			((BaseUnityPlugin)this).Config.Save();
		}
	}

	private void TickLyrics()
	{
		contrast.Tick();
		Scene activeScene = SceneManager.GetActiveScene();
		string name = activeScene.name;
		bool flag = Time.unscaledTime < previewUntil && preview != null;
		if (!Settings.Enabled.Value)
		{
			LyricDisplayStatus = "歌词显示：已关闭（可在悬浮歌词设置中开启）";
			Renderer.Hide();
			screen.Hide();
			folia.Suspend();
			return;
		}
		long now = MusicClock.Now;
		MusicState musicState = (flag ? new MusicState(connected: true, "preview", preview.Title, preview.Artist, Math.Clamp(now - previewTick, 0L, previewDuration), previewDuration, playing: true, now, 1.0, preview.Timeline, 1, "预览中") : Client.Snapshot);
		long? num = musicState.PositionAt(now);
		if (!num.HasValue)
		{
			LyricDisplayStatus = "歌词显示：等待有效播放进度";
		}
		else if (!musicState.Playing)
		{
			LyricDisplayStatus = "歌词显示：播放器暂停，保留当前句";
		}
		else if (musicState.Timeline.Lines.Count == 0)
		{
			LyricDisplayStatus = "歌词显示：尚未收到同步歌词";
		}
		else
		{
			int num2 = musicState.Timeline.FindIndex(Math.Clamp(num.Value + Settings.Offset.Value, 0L, musicState.DurationMs));
			LyricDisplayStatus = ((num2 < 0) ? "歌词显示：等待第一句开始" : ("当前歌词：" + musicState.Timeline.Lines[num2].Text));
		}
		Camera main = Camera.main;
		screenMode = window.Open || name == "Title" || Cursor.visible || (int)Cursor.lockState != 1 || (Object)(object)Character.localCharacter == (Object)null || (Object)(object)main == (Object)null;
		if (Settings.Style.Value != "dango")
		{
			if (folia.Tick(musicState, main, screenMode, now))
			{
				Renderer.Hide();
				screen.Hide();
				return;
			}
		}
		else
		{
			folia.Suspend();
		}
		if (screenMode)
		{
			Renderer.Hide();
			screen.Tick(musicState, now);
		}
		else
		{
			screen.Hide();
			Renderer.Tick(musicState, main, now);
		}
	}

	private void LateUpdate()
	{
		window?.KeepModalState();
		TickLyrics();
	}

	private void OnDisable()
	{
		window?.Close();
		folia?.Suspend();
	}

	private void OnGUI()
	{
		Hub?.DrawTitleEntry(window?.Open ?? false);
		window?.Draw();
		screen?.Draw();
		folia?.Draw();
	}

	internal void KeepPlayerCursor()=>window?.EnsureCursor();

	internal void OpenPlayer()
	{
		window.Show(null);
	}

	internal void OpenSettings(SharedSettingsMenu owner)
	{
		window.Show(owner);
	}

	internal void Preview(int seconds = 8)
	{
		Settings.Enabled.Value = true;
		previewTick = MusicClock.Now;
		previewDuration = seconds * 1000;
		previewUntil = Time.unscaledTime + (float)seconds;
		LyricsTimeline timeline = LyricsTimeline.FromLines(new string[4] { "跟着音乐，继续攀登", "山风穿过云海的边缘", "每一句都落在此刻", "让歌词陪你走向下一程" }.Select((string text, int index) => new LyricsLine(index * previewDuration / 4, previewDuration / 4, text, null, "Climb with the rhythm")));
		preview = new MusicState(connected: true, "preview", "歌词效果预览", "DangoMusic", 0L, previewDuration, playing: true, previewTick, 1.0, timeline, 1, "预览中");
		Notice = "已开启" + seconds + "秒预览；所有界面均可观察。";
		window.Close();
	}

	private void OnDestroy()
	{
		DiagnosticLog.Write = null;
		DiagnosticLog.Write = null;
		((BaseUnityPlugin)this).Config.SettingChanged -= Changed;
		MusicOptionsEntry.Dispose();
		NumericReadout.Dispose();
		contrast.Dispose();
		try
		{
			((BaseUnityPlugin)this).Config.Save();
		}
		finally
		{
			try
			{
				window?.Dispose();
			}
			finally
			{
				Harmony? val = harmony;
				if (val != null)
				{
					val.UnpatchSelf();
				}
				Client?.Dispose();
				Hub?.Dispose();
				Aggregate?.Dispose();
				Renderer?.Dispose();
				screen?.Dispose();
				folia?.Dispose();
				if ((Object)(object)Instance == (Object)(object)this)
				{
					Instance = null;
				}
			}
		}
	}
}
