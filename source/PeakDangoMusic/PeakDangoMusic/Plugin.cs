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

[BepInPlugin("local.peak.dangomusic", "PEAK DangoMusic · Muonium 音乐播放器", Plugin.Version)]
[DefaultExecutionOrder(1000)]
public sealed class Plugin : BaseUnityPlugin
{
	internal const string Version = "2.4.7";

	internal MusicSettings Settings;

	internal FontLibrary Fonts;

	internal MusicClient Client;

	internal AggregatePlayer Aggregate;

	internal MuoniumHub Hub;

	internal WorldLyrics Renderer;

    internal PlayerPerspective Perspective { get; private set; }

    private WorldComments worldComments;

	private ScreenLyrics screen;

	private CinematicLyrics cinematicLyrics;

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
		UiKit.ApplyAppearance(Settings.ThemeColor.Value, Settings.InterfaceTextColor.Value);
		Fonts = new FontLibrary(Settings.FontFolder.Value);
		Aggregate = new AggregatePlayer(this);
		Client = new MusicClient(Settings, Aggregate);
		Hub = new MuoniumHub(this);
		Renderer = new WorldLyrics(Settings, Logger);
        Perspective = new PlayerPerspective();
        worldComments = new WorldComments(this);
		screen = new ScreenLyrics(Settings);
		cinematicLyrics = new CinematicLyrics(Settings);
		folia = new FoliaView(Settings);
		((BaseUnityPlugin)this).Config.SettingChanged += Changed;
		window = new MusicWindow();
		harmony = new Harmony("local.peak.dangomusic");
		harmony.PatchAll(typeof(Plugin).Assembly);
		Logger.LogInfo((object)"DangoMusic: 主菜单 → 音乐播放器，或游戏选项 → 音乐播放器。使用Muonium原生登录、曲库及GD聚合播放。");
		Logger.LogInfo((object)"DangoMusic slider numbers: embedded glyph renderer v1.");
		Logger.LogInfo((object)"DangoMusic adaptive contrast: game day/night + manual override; no screen sampling.");
	}

	private void Changed(object sender, SettingChangedEventArgs args)	{
		saveAt = Time.unscaledTime + 1f;
		if (args.ChangedSetting == Settings.ThemeColor || args.ChangedSetting == Settings.InterfaceTextColor)
		{
			UiKit.ApplyAppearance(Settings.ThemeColor.Value, Settings.InterfaceTextColor.Value);
			return;
		}
		if (args.ChangedSetting == Settings.LyricColor)
			return;
		Renderer?.Invalidate();
		folia?.Invalidate();
        if (args.ChangedSetting == Settings.LxScriptPath || args.ChangedSetting == Settings.LxChannel)
            Hub?.InvalidateAccountCaches("lx-native");
		if ((object)args.ChangedSetting == Settings.FontPath)
		{
			Renderer?.RetryFonts();
			screen?.RetryFonts();
		}
	}

	private bool queuedOpen;
	internal enum CameraBinding { None, Perspective, Cinematic }
	internal CameraBinding RecordingCameraBinding { get; set; }

	private static bool IsCameraKey(Key key, int keyCount) =>
		(int)key > 0 && (int)key <= keyCount &&
		key is not (Key.Escape or Key.RightShift or Key.Backslash or Key.IMESelected);

	internal static string CameraKeyName(Key key)
	{
		Keyboard? keyboard = Keyboard.current;
		if (!IsCameraKey(key, keyboard?.allKeys.Count ?? Keyboard.KeyCount)) return "未设置";
		return keyboard?[key].displayName ?? key.ToString();
	}

	private bool UpdateCameraBinding(Keyboard? keyboard, bool escapePressed)
	{
		if (RecordingCameraBinding == CameraBinding.None) return false;
		if (!window.Open || !Hub.ViewingSettings)
		{
			RecordingCameraBinding = CameraBinding.None;
			return false;
		}
		if (escapePressed)
		{
			RecordingCameraBinding = CameraBinding.None;
		}
		else if (keyboard != null)
		{
			var keys = keyboard.allKeys;
			bool perspective = RecordingCameraBinding == CameraBinding.Perspective;
			var binding = perspective ? Settings.PerspectiveKey : Settings.CinematicKey;
			Key other = (perspective ? Settings.CinematicKey : Settings.PerspectiveKey).Value;
			foreach (KeyControl control in keys)
			{
				if (!control.wasPressedThisFrame || control.keyCode == other ||
					!IsCameraKey(control.keyCode, keys.Count)) continue;
				binding.Value = control.keyCode;
				RecordingCameraBinding = CameraBinding.None;
				break;
			}
		}
		return true;
	}

	private static bool IsTyping()
	{
		var selected = EventSystem.current?.currentSelectedGameObject;
		return selected != null && (selected.GetComponent<TMPro.TMP_InputField>() != null ||
			selected.GetComponent<UnityEngine.UI.InputField>() != null);
	}

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

        bool capturing = UpdateCameraBinding(current, flag);
        bool hideMini = current != null && current.backslashKey.wasPressedThisFrame;
        bool togglePlayer = current != null && current.rightShiftKey.wasPressedThisFrame;
        if(current==null){try{hideMini=Input.GetKeyDown(KeyCode.Backslash);togglePlayer=Input.GetKeyDown(KeyCode.RightShift);}catch(Exception){}}
        if(!capturing && !IsTyping()){
            if(togglePlayer){if(window.Open)window.Close();else window.Show(null);}
            if(hideMini)window.ToggleMini();
            Key key = Settings.PerspectiveKey.Value;
            if (!window.Open && current != null && Cursor.lockState == CursorLockMode.Locked &&
                IsCameraKey(key, current.allKeys.Count) && current[key].wasPressedThisFrame &&
                Perspective.GameplayCamera != null)
                Perspective.Mode = (Perspective.Mode + 1) % PlayerPerspective.Names.Length;
        }

		if (window.CloseRequested)
		{
			window.Close();
		}
		else if (window.Open && flag && !capturing)
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

	private void TickLyrics(bool cinematic = false, Camera? currentCamera = null)
	{
		contrast.Tick();
		cinematicLyrics.Tick(cinematic, Time.unscaledDeltaTime);
		Scene activeScene = SceneManager.GetActiveScene();
		string name = activeScene.name;
		if (!Settings.Enabled.Value)
		{
			cinematicLyrics.SetLyrics(null, 0L);
			LyricDisplayStatus = "歌词显示：已关闭（可在悬浮歌词设置中开启）";
			Renderer.Hide();
			screen.Hide();
			folia.Suspend();
			return;
		}
		MusicState musicState;
		if (Time.unscaledTime < previewUntil && preview is { } previewTrack)
		{
			long sampleTick = MusicClock.Now;
			musicState = new MusicState(true, "preview", previewTrack.Title, previewTrack.Artist,
				Math.Clamp(sampleTick - previewTick, 0L, previewDuration), previewDuration,
				true, sampleTick, 1.0, previewTrack.Timeline, 1, "预览中");
		}
		else
		{
			musicState = Client.Snapshot;
		}
		// All lyric views use the same playback sample and its own timestamp for this frame.
		long now = musicState.SampleTick;
		cinematicLyrics.SetLyrics(musicState, now);
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
		Camera? main = currentCamera != null ? currentCamera : Camera.main;
		screenMode = window.Open || name == "Title" || (!cinematic && (Cursor.visible || Cursor.lockState != CursorLockMode.Locked))
			|| Character.localCharacter == null || main == null;
		CinematicLyrics.WorldPlacement? shot = cinematic && !screenMode && main != null
			? cinematicLyrics.Place(main, Perspective.IdleShotId) : null;
		if (Settings.Style.Value != "dango")
		{
			if (folia.Tick(musicState, main, screenMode, now, shot))
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
			Renderer.Tick(musicState, main!, now, shot);
		}
	}

    private static bool HasCameraInput(CharacterInput? input)
    {
        if (Keyboard.current?.anyKey.wasPressedThisFrame == true) return true;
        if (input == null) return false;
        float lookThreshold = CharacterInput.action_look?.activeControl?.device is Gamepad ? 0.0625f : 0.25f;
        return input.movementInput.sqrMagnitude > 0.0625f || input.lookInput.sqrMagnitude > lookThreshold ||
            input.jumpIsPressed || input.jumpWasPressed || input.sprintIsPressed || input.sprintWasPressed ||
            input.interactIsPressed || input.interactWasPressed || input.usePrimaryIsPressed || input.usePrimaryWasPressed ||
            input.useSecondaryIsPressed || input.useSecondaryWasPressed || input.crouchIsPressed || input.crouchWasPressed ||
            input.dropWasPressed || input.pauseWasPressed || input.emoteIsPressed || input.pingWasPressed ||
            input.selectBackpackWasPressed || input.selectSlotForwardWasPressed || input.selectSlotBackwardWasPressed ||
            Mathf.Abs(input.scrollInput) > 0.01f;
    }

    private IdleCameraBlock GetIdleCameraBlock(Character player)
    {
        if (!Application.isFocused) return IdleCameraBlock.Background;
        if (window?.Open == true) return IdleCameraBlock.PlayerPanel;
        var gameUi = GUIManager.instance;
        if (Time.timeScale <= 0f || GUIManager.InPauseMenu || (gameUi != null && gameUi.windowBlockingInput) || IsTyping())
            return IdleCameraBlock.Menu;

        return HasCameraInput(player.input) ? IdleCameraBlock.Input : IdleCameraBlock.None;
    }

    private void LateUpdate()
	{
		window?.KeepModalState();
        var camera = Perspective.GameplayCamera;
        var player = Character.localCharacter;
        var scene = SceneManager.GetActiveScene();
        if (scene.name == "Title" || player == null || player.data.dead || player.data.fullyPassedOut ||
            camera == null)
        {
            Perspective.ClearView();
            worldComments.Reset();
            TickLyrics();
            return;
        }
        Keyboard? keyboard = Keyboard.current;
        Key key = Settings.CinematicKey.Value;
        bool toggle = keyboard != null && Cursor.lockState == CursorLockMode.Locked &&
            key != Settings.PerspectiveKey.Value && IsCameraKey(key, keyboard.allKeys.Count) &&
            keyboard[key].wasPressedThisFrame;
        Pose view = Perspective.UpdateView(camera, player, GetIdleCameraBlock(player), Settings.IdleCameraSeconds.Value,
            Settings.IdleCameraEnabled.Value, toggle);
        // Place and face lyrics using the current shot, then restore the gameplay camera.
        if (Perspective.IdleCameraActive)
        {
            Perspective.ApplyView(camera);
            try { TickLyrics(cinematic: true, currentCamera: camera); }
            finally { Perspective.RestoreCamera(); }
        }
        else
        {
            TickLyrics(currentCamera: camera);
        }
        if (Settings.WorldCommentsEnabled.Value)
            worldComments.Tick(view, scene.handle, Aggregate.PlayingTrack);
        else
            worldComments.Reset();
	}

    private void OnApplicationFocus(bool focused)
    {
        if (!focused) Perspective?.ClearView(IdleCameraBlock.Background);
    }

	private void OnDisable()
	{
		RecordingCameraBinding = CameraBinding.None;
		window?.Close();
		folia?.Suspend();
        Perspective?.ClearView();
        Perspective?.RestoreCamera();
        worldComments?.Reset();
	}

	private void OnGUI()
	{
		Hub?.DrawTitleEntry(window?.Open ?? false);
		window?.Draw();
		screen?.Draw();
		folia?.Draw();
		cinematicLyrics?.Draw(window?.Open ?? false);
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
        worldComments?.Dispose();
        Perspective?.Dispose();
        CoverCache.Reset();
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
				cinematicLyrics?.Dispose();
				folia?.Dispose();
				if ((Object)(object)Instance == (Object)(object)this)
				{
					Instance = null;
				}
			}
		}
	}
}
