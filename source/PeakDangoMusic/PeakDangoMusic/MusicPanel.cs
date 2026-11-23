using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using DangoMusic.Core;
using UnityEngine;

namespace PeakDangoMusic;

public static class MusicPanel
{
	private static string? portText;

	private static string? pathText;

	private static GUIStyle? hint;

	private static GUIStyle? title;

	private static GUIStyle? rowLabel;

	private static GUIStyle? valueStyle;

	private static GUIStyle? buttonStyle;

	private static GUIStyle? presetStyle;

	private static GUIStyle? selectedStyle;

	private static GUIStyle? selectedPresetStyle;

	private static GUIStyle? sliderTrack;

	private static GUIStyle? noticeStyle;

	private static Texture2D? pressedTexture;

	private static Texture2D? selectedTexture;

	private static Font? numericFont;

	private static GUISkin? styleSkin;

	private static Vector2 connectionScroll;

	private static Vector2 settingsScroll;

	private static Vector2 narrowScroll;

	private static int settingsTab;

	private static int fontPage;

	private static float entranceAt = -1f;

	private static float feedbackAt = -10f;

	private static string feedback = "";

	private static string fontSearch = "";

	private static string fontFolderEdit = "";

	private const float RowLabelWidth = 144f;

	private const float ValueWidth = 96f;

	private const float ControlGap = 12f;

	private const float GridColumnGap = 16f;

	private const float GridRowGap = 10f;

	private static float rowHeight = 48f;

	private static readonly string[] Modes = new string[3] { "整句", "单词", "逐字（整句）" };

	private static readonly string[] Tabs = new string[4] { "歌词显示与位置", "动画与效果", "Folia歌词样式", "字体" };

	private static readonly string[] Sources = new string[6] { "BLF / LX", "原版网易云", "酷狗音乐", "QQ音乐", "GD聚合音乐", "系统媒体会话" };

	private static readonly string[] EffectPresets = new string[4] { "原项目默认", "轻柔漂浮", "活泼弹跳", "静态清晰" };

	private static readonly string[] Entrance = new string[4] { "淡入", "上浮", "弹入", "直接" };

	private static readonly string[] Exit = new string[4] { "淡出", "上浮", "缩小", "直接" };

	private static string aggregateQuery = "";

	private static string searchedQuery = "";

	private static int aggregatePage = 1;

	private static int searchedPlatform = -1;

	internal static string Feedback
	{
		get
		{
			if (!(Time.unscaledTime - feedbackAt < 2.2f))
			{
				return "";
			}
			return feedback;
		}
	}

	internal static void ResetEntrance()
	{
		entranceAt = -1f;
	}

	private static void Styles()
	{
		if (styleSkin != GUI.skin)
		{
			styleSkin = GUI.skin;
			float num = ((GUI.skin.button.fontSize > 0) ? ((float)GUI.skin.button.fontSize) : 18f);
			rowHeight = Mathf.Max(48f, Mathf.Ceil(num * 1.5f) + (float)GUI.skin.button.padding.vertical + 2f);
			hint = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true,
				fontSize = 14
			};
			title = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true,
				fontStyle = (FontStyle)1,
				fontSize = 20
			};
			rowLabel = new GUIStyle(GUI.skin.label)
			{
				alignment = (TextAnchor)3,
				wordWrap = false,
				clipping = (TextClipping)1,
				fontSize = 16,
				margin = new RectOffset()
			};
			noticeStyle = new GUIStyle(GUI.skin.label)
			{
				wordWrap = true,
				clipping = TextClipping.Overflow,
				fontSize = 14
			};
			valueStyle = new GUIStyle(GUI.skin.label)
			{
				alignment = (TextAnchor)5,
				fontSize = 15,
				margin = new RectOffset(),
				padding = new RectOffset()
			};
			sliderTrack = new GUIStyle(GUI.skin.horizontalSlider)
			{
				fixedHeight = 24f,
				margin = new RectOffset(),
				padding = new RectOffset(),
				border = new RectOffset()
			};
			GUIStyleState[] array = new GUIStyleState[4] { sliderTrack.normal, sliderTrack.hover, sliderTrack.active, sliderTrack.focused };
			for (int i = 0; i < array.Length; i++)
			{
				array[i].background = null;
			}
			if (numericFont == null)
			{
				numericFont = Font.CreateDynamicFontFromOSFont(new string[3] { "Cascadia Mono", "Consolas", "Courier New" }, 16);
			}
			valueStyle.font = numericFont;
			if ((Object)(object)pressedTexture != (Object)null)
			{
				Object.Destroy((Object)(object)pressedTexture);
			}
			if ((Object)(object)selectedTexture != (Object)null)
			{
				Object.Destroy((Object)(object)selectedTexture);
			}
			pressedTexture = ButtonSurface(new Color(0.64f, 0.69f, 0.73f, 0.90f));
			selectedTexture = ButtonSurface(new Color(1f, 1f, 1f, 0.74f));
			buttonStyle = new GUIStyle(GUI.skin.button)
			{
				border = new RectOffset(),
				wordWrap = false,
				clipping = (TextClipping)1
			};
			buttonStyle.active.background = pressedTexture;
			buttonStyle.onActive.background = pressedTexture;
			buttonStyle.active.textColor = UiKit.Text;
			buttonStyle.onActive.textColor = UiKit.Text;
			presetStyle = new GUIStyle(buttonStyle)
			{
				alignment = (TextAnchor)3,
				wordWrap = false,
				clipping = (TextClipping)1,
				padding = new RectOffset(12, 12, 10, 10),
				margin = new RectOffset(0, 0, 0, 0),
				fixedHeight = rowHeight
			};
			selectedStyle = new GUIStyle(buttonStyle);
			selectedStyle.normal.background = GUI.skin.button.onNormal.background ?? selectedTexture;
			selectedStyle.hover.background = GUI.skin.button.onHover.background ?? selectedTexture;
			selectedStyle.normal.textColor = UiKit.Text;
			selectedStyle.hover.textColor = UiKit.Text;
			selectedPresetStyle = new GUIStyle(presetStyle);
			selectedPresetStyle.normal.background = GUI.skin.button.onNormal.background ?? selectedTexture;
			selectedPresetStyle.hover.background = GUI.skin.button.onHover.background ?? selectedTexture;
			selectedPresetStyle.normal.textColor = UiKit.Text;
			selectedPresetStyle.hover.textColor = UiKit.Text;
		}
	}

	public static void Draw()
	{
		Plugin plugin = Plugin.Instance;
		if ((Object)(object)plugin == (Object)null)
		{
			GUILayout.Label("漂浮歌词模块未加载，请重新打开 PEAK。", Array.Empty<GUILayoutOption>());
			return;
		}
		Styles();
		GUILayout.Label("DangoMusic · 地图漂浮歌词", title, Array.Empty<GUILayoutOption>());
		GUILayout.Label("游戏选项 → 悬浮歌词 → 打开设置；歌词显隐也在此菜单中调整。", hint, Array.Empty<GUILayoutOption>());
		DrawGroup(() =>
		{
			Connection(plugin);
		}, 0f);
		GUILayout.Space(24f);
		DrawGroup(() =>
		{
			Fonts(plugin);
		}, 0.07f);
		GUILayout.Space(24f);
		DrawGroup(() =>
		{
			FoliaStyle(plugin);
		}, 0.14f);
		GUILayout.Space(24f);
		DrawGroup(() =>
		{
			Display(plugin);
		}, 0.21f);
		GUILayout.Space(24f);
		DrawGroup(() =>
		{
			Effects(plugin);
		}, 0.28f);
	}

	internal static void DrawStandalone(float availableWidth)
	{
		Plugin plugin = Plugin.Instance;
		if ((Object)(object)plugin == (Object)null)
		{
			return;
		}
		Styles();
		float num = (float)GUI.skin.scrollView.padding.horizontal + 20f;
		if (availableWidth < 880f)
		{
			narrowScroll = GUILayout.BeginScrollView(narrowScroll, false, false, GUIStyle.none, GUIStyle.none, Array.Empty<GUILayoutOption>());
			GUILayout.BeginVertical(new GUILayoutOption[1] { GUILayout.Width(Mathf.Max(180f, availableWidth - num)) });
			DrawGroup(() =>
			{
				Connection(plugin);
			}, 0f);
			GUILayout.Space(24f);
			DrawGroup(() =>
			{
				Fonts(plugin);
			}, 0.07f);
			GUILayout.Space(24f);
			DrawGroup(() =>
			{
				FoliaStyle(plugin);
			}, 0.14f);
			GUILayout.Space(24f);
			DrawGroup(() =>
			{
				Display(plugin);
			}, 0.21f);
			GUILayout.Space(24f);
			DrawGroup(() =>
			{
				Effects(plugin);
			}, 0.28f);
			GUILayout.EndVertical();
			GUILayout.EndScrollView();
			return;
		}
		float num2 = availableWidth * 0.36f;
		float num3 = availableWidth - num2 - 22f;
		float num4 = (float)GUI.skin.box.padding.horizontal + num;
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.BeginVertical(GUI.skin.box, new GUILayoutOption[2]
		{
			GUILayout.Width(num2),
			GUILayout.ExpandHeight(true)
		});
		connectionScroll = GUILayout.BeginScrollView(connectionScroll, false, false, GUIStyle.none, GUIStyle.none, Array.Empty<GUILayoutOption>());
		GUILayout.BeginVertical(new GUILayoutOption[1] { GUILayout.Width(Mathf.Max(180f, num2 - num4)) });
		DrawGroup(() =>
		{
			Connection(plugin);
		}, 0f);
		GUILayout.EndVertical();
		GUILayout.EndScrollView();
		GUILayout.EndVertical();
		GUILayout.Space(22f);
		GUILayout.BeginVertical(GUI.skin.box, new GUILayoutOption[2]
		{
			GUILayout.Width(num3),
			GUILayout.ExpandHeight(true)
		});
		int num5 = Selection(settingsTab, Tabs);
		if (num5 != settingsTab)
		{
			settingsTab = num5;
			settingsScroll = Vector2.zero;
			ResetEntrance();
		}
		GUILayout.Space(16f);
		settingsScroll = GUILayout.BeginScrollView(settingsScroll, false, false, GUIStyle.none, GUIStyle.none, Array.Empty<GUILayoutOption>());
		GUILayout.BeginVertical(new GUILayoutOption[1] { GUILayout.Width(Mathf.Max(180f, num3 - num4)) });
		if (settingsTab == 0)
		{
			DrawGroup(() =>
			{
				Display(plugin);
			}, 0.08f);
		}
		else if (settingsTab == 1)
		{
			DrawGroup(() =>
			{
				Effects(plugin);
			}, 0.08f);
		}
		else if (settingsTab == 2)
		{
			DrawGroup(() =>
			{
				FoliaStyle(plugin);
			}, 0.08f);
		}
		else
		{
			DrawGroup(() =>
			{
				Fonts(plugin);
			}, 0.08f);
		}
		GUILayout.EndVertical();
		GUILayout.EndScrollView();
		GUILayout.EndVertical();
		GUILayout.EndHorizontal();
	}

	private static void Connection(Plugin plugin)
	{
		MusicSettings s = plugin.Settings;
		SectionTitle("播放器连接");
		GUILayout.Label("播放器来源", hint, Array.Empty<GUILayoutOption>());
		PresetGrid(s.EffectiveSource, Sources, momentary: false, (int value) =>
		{
			s.Source.Value = value;
		});
		GUILayout.Space(24f);
		SectionTitle("播放状态");
		Toggle("启用漂浮歌词", s.Enabled);
		MusicState snapshot = plugin.Client.Snapshot;
		GUILayout.Label(snapshot.Message, hint, Array.Empty<GUILayoutOption>());
		GUILayout.Label("同步歌词：" + snapshot.Timeline.Lines.Count + " 行", hint, Array.Empty<GUILayoutOption>());
		GUILayout.Label(plugin.LyricDisplayStatus, hint, Array.Empty<GUILayoutOption>());
		GUILayout.Label(plugin.TextRenderStatus, hint, Array.Empty<GUILayoutOption>());
		if (s.Style.Value == "dango" && snapshot.Timeline.Lines.Count > 0)
		{
			int num = snapshot.Timeline.FindIndex(snapshot.PositionAt(MusicClock.Now) ?? snapshot.PositionMs);
			if (num >= 0)
			{
				IReadOnlyList<LyricsWord> words = snapshot.Timeline.Lines[num].Words;
				if (words == null || words.Count <= 0)
				{
					GUILayout.Label("当前歌词只有行时间，逐字同步为估算。", hint, Array.Empty<GUILayoutOption>());
				}
			}
		}
		GUILayout.Label((snapshot.Title.Length > 0) ? BalancedTitle(snapshot.Title + " — " + snapshot.Artist) : "", hint, new GUILayoutOption[1] { GUILayout.Height(44f) });
		long ms = snapshot.PositionAt(MusicClock.Now) ?? snapshot.PositionMs;
		GUILayout.Label(new GUIContent((snapshot.Playing ? "播放中  " : "已暂停  ") + TimeText(ms) + " / " + TimeText(snapshot.DurationMs)), valueStyle, new GUILayoutOption[1] { GUILayout.Height(24f) });
		string snapshot2 = DiagnosticLog.Snapshot;
		if (snapshot2.Length > 0)
		{
			GUILayout.Space(16f);
			SectionTitle("最近错误");
			GUILayout.Label(snapshot2, hint, Array.Empty<GUILayoutOption>());
			GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
			if (Button("复制错误详情"))
			{
				GUIUtility.systemCopyBuffer = snapshot2;
			}
			if (Button("清空记录"))
			{
				DiagnosticLog.Clear();
			}
			GUILayout.EndHorizontal();
		}
		GUILayout.Space(24f);
		SectionTitle("连接设置");
		if (s.EffectiveSource == 4)
		{
			AggregateControls(plugin);
			return;
		}
		if (s.EffectiveSource == 5)
		{
			GUILayout.Label("读取当前正在播放的 Windows 媒体会话；支持提供有效进度的 Spotify、Apple Music、浏览器等。", hint, Array.Empty<GUILayoutOption>());
			if (Button("重新探测系统会话"))
			{
				plugin.Client.Reconnect();
			}
			return;
		}
		if (s.EffectiveSource >= 2)
		{
			string text = ((s.EffectiveSource == 2) ? "酷狗音乐" : "QQ音乐");
			GUILayout.Label((s.EffectiveSource == 2) ? "通过酷狗本机播放接口同步，不扫描播放器控件。" : "读取QQ音乐的系统媒体会话与进度。", hint, Array.Empty<GUILayoutOption>());
			GUILayout.Label((s.EffectiveSource == 2) ? "酷狗优先读取本机KRC原版逐字歌词，再尝试公开曲库。" : "QQ音乐优先匹配本平台同步歌词；接口无结果时尝试备用曲库。", hint, Array.Empty<GUILayoutOption>());
			if (s.EffectiveSource == 2)
			{
				GUILayout.Label("酷狗歌词缓存目录（留空自动查找）", hint, Array.Empty<GUILayoutOption>());
				s.KugouLyricsFolder.Value = GUILayout.TextField(s.KugouLyricsFolder.Value, new GUILayoutOption[1] { GUILayout.Height(44f) });
			}
			if (Button("重新连接" + text))
			{
				plugin.Client.Reconnect();
			}
			GUILayout.Label((s.EffectiveSource == 2) ? "先完全退出酷狗，再运行安装包内“启动酷狗歌词连接.cmd”，打开推荐页并播放。" : "QQ音乐需提供有效系统媒体会话进度。", hint, Array.Empty<GUILayoutOption>());
			if (s.EffectiveSource == 3)
			{
				GUILayout.Label("QQ音乐尚未在本机安装，当前适配待实机验证。", hint, Array.Empty<GUILayoutOption>());
			}
			return;
		}
		if (s.EffectiveSource == 1)
		{
			GUILayout.Label("读取原版网易云的 Windows 媒体信息与本机播放进度，歌词由网易云公开接口获取。", hint, Array.Empty<GUILayoutOption>());
			GUILayout.Space(10f);
			if (Button("重新连接网易云"))
			{
				plugin.Client.Reconnect();
			}
			GUILayout.Space(12f);
			GUILayout.Label("没有进度时，先退出网易云，再运行安装包内的“启动网易云歌词连接.cmd”。不需要登录账号或填写 Cookie。", hint, Array.Empty<GUILayoutOption>());
			GUILayout.Label("此新增接口按要求未做实机测试。", hint, Array.Empty<GUILayoutOption>());
			return;
		}
		GUILayout.Label("在 BLF / LX 的设置中开启本机开放 API，端口与下方保持一致。", hint, Array.Empty<GUILayoutOption>());
		GUILayout.Space(8f);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		GUILayout.Label("端口", new GUILayoutOption[1] { GUILayout.Width(48f) });
		GUILayout.Space(8f);
		portText = GUILayout.TextField(portText ?? s.Port.Value.ToString(), 6, new GUILayoutOption[2]
		{
			GUILayout.MinWidth(0f),
			GUILayout.Height(46f)
		});
		GUILayout.Space(10f);
		if (Button("应用端口", 110f))
		{
			if (int.TryParse(portText, out var result) && result >= 1 && result <= 65535)
			{
				s.Port.Value = result;
				plugin.Client.Reconnect();
				plugin.Notice = "已更新本机连接端口。";
			}
			else
			{
				plugin.Notice = "端口应为1–65535。";
			}
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(16f);
		GUILayout.Label("播放器路径（可选）", Array.Empty<GUILayoutOption>());
		GUILayout.Label("手动打开播放器时可留空；填写 exe 路径后可用下方按钮启动。", hint, Array.Empty<GUILayoutOption>());
		pathText = GUILayout.TextField(pathText ?? s.PlayerPath.Value, new GUILayoutOption[2]
		{
			GUILayout.MinWidth(0f),
			GUILayout.Height(46f)
		});
		GUILayout.Space(10f);
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (Button("打开 BLF / LX"))
		{
			string text2 = (pathText ?? "").Trim();
			if (!File.Exists(text2))
			{
				plugin.Notice = "未找到播放器，请检查上面的 exe 路径。";
			}
			else
			{
				try
				{
					s.PlayerPath.Value = text2;
					Process.Start(new ProcessStartInfo(text2)
					{
						UseShellExecute = true,
						WindowStyle = ProcessWindowStyle.Normal
					});
					plugin.Notice = "已打开播放器。";
				}
				catch (Exception error)
				{
					plugin.Notice = DiagnosticLog.Report("BLF / LX", "启动播放器", error, "MusicPanel.cs", 192);
				}
			}
		}
		GUILayout.Space(12f);
		if (Button("重新连接"))
		{
			plugin.Client.Reconnect();
		}
		GUILayout.EndHorizontal();
		GUILayout.Space(14f);
		if (Button("重新加载歌词字体"))
		{
			plugin.Renderer.RetryFonts();
		}
		GUILayout.Space(12f);
		GUILayout.Label((plugin.Notice.Length > 0) ? plugin.Notice : "", noticeStyle, new GUILayoutOption[1] { GUILayout.Height(22f) });
		GUILayout.Space(24f);
		SectionTitle("使用提示");
		GUILayout.Label("主菜单、机场、地图和设置界面均可接续显示歌词。音乐由播放器独立播放，不随窗口暂停。联机队友继续运行。", hint, Array.Empty<GUILayoutOption>());
	}

	private static void AggregateControls(Plugin plugin)
	{
		MusicSettings settings = plugin.Settings;
		AggregatePlayer aggregate = plugin.Aggregate;
		GUILayout.Label("GD聚合平台（全部10项）", hint, Array.Empty<GUILayoutOption>());
		settings.AggregatePlatform.Value = PresetGrid(settings.AggregatePlatform.Value, GdMusicApi.Names);
		GUILayout.Label("部分平台可能未开放，空结果或失败时可换源。", hint, Array.Empty<GUILayoutOption>());
		aggregateQuery = GUILayout.TextField(aggregateQuery, 200, new GUILayoutOption[1] { GUILayout.Height(44f) });
		bool enabled = GUI.enabled;
		GUI.enabled = enabled && !aggregate.Busy;
		if (Button("搜索歌曲 / 歌手"))
		{
			aggregatePage = 1;
			searchedQuery = aggregateQuery;
			searchedPlatform = settings.AggregatePlatform.Value;
			aggregate.Search(GdMusicApi.Keys[searchedPlatform], searchedQuery, aggregatePage);
		}
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (Button("上一页") && aggregatePage > 1 && searchedPlatform >= 0)
		{
			aggregatePage--;
			aggregate.Search(GdMusicApi.Keys[searchedPlatform], searchedQuery, aggregatePage);
		}
		if (Button("下一页") && searchedPlatform >= 0)
		{
			aggregatePage++;
			aggregate.Search(GdMusicApi.Keys[searchedPlatform], searchedQuery, aggregatePage);
		}
		GUILayout.EndHorizontal();
		GUI.enabled = enabled;
		GUILayout.Label("第 " + aggregatePage + " 页 · " + aggregate.Status, hint, Array.Empty<GUILayoutOption>());
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (Button("播放 / 暂停"))
		{
			aggregate.TogglePause();
		}
		if (Button("停止"))
		{
			aggregate.Stop();
		}
		GUILayout.EndHorizontal();
		GUILayout.Label("音量", hint, Array.Empty<GUILayoutOption>());
		settings.AggregateVolume.Value = GUILayout.HorizontalSlider(settings.AggregateVolume.Value, 0f, 1f, Array.Empty<GUILayoutOption>());
		MusicState snapshot = aggregate.Snapshot;
		if (snapshot.DurationMs > 0)
		{
			GUILayout.Label(snapshot.Title + " · " + snapshot.Artist, hint, Array.Empty<GUILayoutOption>());
			float num = (float)snapshot.PositionMs / (float)snapshot.DurationMs;
			float num2 = GUILayout.HorizontalSlider(num, 0f, 1f, Array.Empty<GUILayoutOption>());
			if (Math.Abs(num2 - num) > 0.001f)
			{
				aggregate.Seek(num2);
			}
		}
		GUILayout.Label("请求音质：" + settings.AggregateBitrate.Value + " kbps", hint, Array.Empty<GUILayoutOption>());
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		int[] array = new int[3] { 128, 320, 999 };
		for (int i = 0; i < array.Length; i++)
		{
			int num3 = array[i];
			if (Button((num3 == 999) ? "无损" : num3.ToString()))
			{
				settings.AggregateBitrate.Value = num3;
			}
		}
		GUILayout.EndHorizontal();
		GUI.enabled = enabled && !aggregate.Busy;
		GdMusicApi.Track[] results = aggregate.Results;
		foreach (GdMusicApi.Track track in results)
		{
			if (GUILayout.Button(track.Title + "\n" + track.Artist + "  ·  " + track.Album, buttonStyle, new GUILayoutOption[1] { GUILayout.Height(64f) }))
			{
				aggregate.Play(track, settings.AggregateBitrate.Value);
			}
			GUILayout.Space(8f);
		}
		GUI.enabled = enabled;
	}

	internal static void DrawLyricsSettings()
	{
		Plugin instance = Plugin.Instance;
		if (!((Object)(object)instance == (Object)null))
		{
			Styles();
			GUILayout.Label("地图悬浮歌词", title, Array.Empty<GUILayoutOption>());
			instance.Settings.Enabled.Value = GUILayout.Toggle(instance.Settings.Enabled.Value, "显示悬浮歌词（不影响音乐播放）", Array.Empty<GUILayoutOption>());
			instance.Settings.LyricWeather.Value = GUILayout.Toggle(instance.Settings.LyricWeather.Value, "地图歌词天气效果", Array.Empty<GUILayoutOption>());
			Display(instance);
			GUILayout.Space(20f);
			Tint(instance);
			GUILayout.Space(20f);
			Effects(instance);
			GUILayout.Space(20f);
			FoliaStyle(instance);
			GUILayout.Space(20f);
			Fonts(instance);
		}
	}

	private static void Tint(Plugin plugin)
	{
		MusicSettings settings = plugin.Settings;
		SectionTitle("歌词字色");
		GUILayout.Label("调色盘选的色只作用在游戏内悬浮歌词的字上，上面的深/浅字色、昼夜自动切换照旧，选了色就盖过去。光晕颜色设成「随字色」时会跟着一起变。", hint, new GUILayoutOption[2]
		{
			GUILayout.MinHeight(24f),
			GUILayout.ExpandHeight(false)
		});
		Rect probe = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[2]
		{
			GUILayout.ExpandWidth(true),
			GUILayout.Height(1f)
		});
		ThemePicker.DrawLyric(plugin, Mathf.Clamp(probe.width, 360f, 1000f));
		GUILayout.Space(6f);
		GUILayout.Label(string.IsNullOrEmpty(settings.LyricColor.Value) ? "当前没有自定义，跟着深浅字色走。" : ("当前字色 " + settings.LyricColor.Value + "，改完立刻生效。"), hint, Array.Empty<GUILayoutOption>());
	}

	private static void Fonts(Plugin plugin)
	{
		MusicSettings settings = plugin.Settings;
		SectionTitle("歌词字体");
		GUILayout.Label("当前：" + ((settings.FontPath.Value.Length == 0) ? "系统默认" : Path.GetFileNameWithoutExtension(settings.FontPath.Value)), hint, Array.Empty<GUILayoutOption>());
		if (Button("恢复系统字体"))
		{
			settings.FontPath.Value = "";
		}
		GUILayout.Space(10f);
		if (fontFolderEdit.Length == 0)
		{
			fontFolderEdit = settings.FontFolder.Value;
		}
		GUILayout.Label("字体素材目录", Array.Empty<GUILayoutOption>());
		fontFolderEdit = GUILayout.TextField(fontFolderEdit, new GUILayoutOption[2]
		{
			GUILayout.MinWidth(0f),
			GUILayout.Height(44f)
		});
		if (Button("刷新字体列表"))
		{
			settings.FontFolder.Value = fontFolderEdit;
			plugin.Fonts.Refresh(fontFolderEdit);
			fontPage = 0;
		}
		GUILayout.Label(plugin.Fonts.Status, hint, Array.Empty<GUILayoutOption>());
		GUILayout.Label("搜索字体", Array.Empty<GUILayoutOption>());
		string text = GUILayout.TextField(fontSearch, new GUILayoutOption[2]
		{
			GUILayout.MinWidth(0f),
			GUILayout.Height(44f)
		});
		if (text != fontSearch)
		{
			fontSearch = text;
			fontPage = 0;
		}
		FontLibrary.Choice[] array = plugin.Fonts.Choices.Where((FontLibrary.Choice f) => f.Path.Length > 0 && (fontSearch.Length == 0 || f.Label.IndexOf(fontSearch, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
		int num = Math.Max(1, (array.Length + 9) / 10);
		fontPage = Math.Clamp(fontPage, 0, num - 1);
		foreach (FontLibrary.Choice item in array.Skip(fontPage * 10).Take(10))
		{
			if (GUILayout.Button(new GUIContent(item.Label, item.Path), (settings.FontPath.Value == item.Path) ? selectedStyle : buttonStyle, new GUILayoutOption[2]
			{
				GUILayout.MinWidth(0f),
				GUILayout.Height(rowHeight - 4f)
			}))
			{
				settings.FontPath.Value = item.Path;
				SetFeedback("已选择字体：" + item.Label);
			}
			GUILayout.Space(6f);
		}
		GUILayout.BeginHorizontal(Array.Empty<GUILayoutOption>());
		if (Button("上一页"))
		{
			fontPage = Math.Max(0, fontPage - 1);
		}
		GUILayout.Label(fontPage + 1 + " / " + num, valueStyle, new GUILayoutOption[1] { GUILayout.Width(70f) });
		if (Button("下一页"))
		{
			fontPage = Math.Min(num - 1, fontPage + 1);
		}
		GUILayout.EndHorizontal();
		GUILayout.Label("字体只从本地读取；英文专用字体缺少中文时使用系统字体补齐。", hint, Array.Empty<GUILayoutOption>());
		if (Button("预览20秒并返回游戏"))
		{
			plugin.Preview(20);
		}
	}

	private static void Display(Plugin plugin)
	{
		MusicSettings settings = plugin.Settings;
		SectionTitle("歌词显示");
		settings.Mode.Value = NamedSelection("播放方式", settings.Mode.Value, Modes);
		GUILayout.Label((settings.Mode.Value == 2) ? "整句保持同一位置，按演唱顺序逐字显现；下一句才换位置。" : "", noticeStyle, new GUILayoutOption[] { GUILayout.MinHeight(24f), GUILayout.ExpandHeight(false) });
		if(settings.Mode.Value==2){
            var timeline=plugin.Client.Snapshot.Timeline;
            int timed=timeline.Lines.Count(l=>l.Words!=null&&l.Words.Count>0);
            GUILayout.Label(timed>0?"2.1.2 逐字链路 · 当前音源含 "+timed+" 行逐字时间":"2.1.2 逐字链路 · 音源仅提供逐行时间，句内出字为估算",noticeStyle);
        }
        GUILayout.Label((settings.Style.Value != "dango") ? "当前为Folia样式，自带逐词排版；上方三种播放方式用于Dango样式。" : "", noticeStyle, new GUILayoutOption[] { GUILayout.MinHeight(24f), GUILayout.ExpandHeight(false) });
		GUILayout.Space(16f);
		if (Button("恢复 Sorihiko 原项目默认"))
		{
			settings.ApplyOriginalDefaults();
			plugin.Notice = "已恢复原项目歌词默认参数；播放器连接设置保留。";
		}
		GUILayout.Label("默认在左右两侧生成，固定世界位置；缩放1、距离10、散布30°。", hint, Array.Empty<GUILayoutOption>());
		Toggle("显示播放器汉化歌词（扩展，默认关）", settings.Translation);
		Toggle("自动拆分合并歌词", settings.SplitLines);
		Toggle("智能防离屏（原项目默认关）", settings.Smart);
		Toggle("固定跟随视角（扩展，默认关）", settings.Follow);
		GUILayout.Space(12f);
		Toggle("昼夜自动字色（白天深／夜晚浅）", settings.AutoDayNight);
		bool enabled = GUI.enabled;
		GUI.enabled = enabled && !settings.AutoDayNight.Value;
		Toggle("手动使用深色字（关闭为浅色）", settings.ManualDark);
		GUI.enabled = enabled;
		GUILayout.Label("两种字色均保留光晕；字色不随靠近、视角或天气变化。", hint, Array.Empty<GUILayoutOption>());
		Slider("文字亮度", settings.Brightness, 1f, 6f, "F1");
		Slider("缩放倍率", settings.FontSize, 0.5f, 3f, "F1");
		Slider("歌词距离", settings.Distance, 3f, 40f, "F1");
		Slider("左右散布角", settings.Spread, 0f, 60f, "F0");
		Toggle("始终朝向玩家", settings.FacePlayer);
		Toggle("靠近时后退", settings.KeepAway);
		Toggle("保持视线以上（默认关）", settings.AboveGround);
		GUILayout.Space(24f);
		SectionTitle("位置与遮挡");
		Toggle("允许歌词出现在墙内", settings.AllowWalls);
		Toggle("歌词位于人物后景", settings.BehindPlayer);
		GUILayout.Label("后景模式使用场景深度；关闭穿墙时会避开实体岩壁，没有空间就跳过。", hint, Array.Empty<GUILayoutOption>());
		GUILayout.Space(16f);
		settings.Offset.Value = (int)SliderRow("时间偏移", settings.Offset.Value, -10000f, 10000f, (float value) => value.ToString("F0", CultureInfo.InvariantCulture) + " ms", 50f);
		GUILayout.Label("正值提前，负值延后。", hint, Array.Empty<GUILayoutOption>());
	}

	private static void Effects(Plugin plugin)
	{
		MusicSettings settings = plugin.Settings;
		SectionTitle("效果预设");
		int num = PresetGrid(-1, EffectPresets, momentary: true);
		if (num >= 0)
		{
			settings.ApplyPreset(num);
		}
		GUILayout.Space(24f);
		SectionTitle("文字动画");
		Toggle("文字光晕（深浅字均生效）", settings.Glow);
		Toggle("逐字弹出", settings.Pop);
		GUILayout.Label("光晕没有单独配色了，直接跟字色走：自定义了字色就用那个色，没自定义就用当前的深/浅字色。", hint, new GUILayoutOption[2]
		{
			GUILayout.MinHeight(24f),
			GUILayout.ExpandHeight(false)
		});
		Slider("光晕强度", settings.GlowStrength, 0f, 3f, "F2");
		Slider("光晕半径", settings.GlowRadius, 0f, 1f, "F2");
		GUILayout.Space(24f);
		SectionTitle("文字抖动");
		Toggle("轻微抖动", settings.Wiggle);
		GUILayout.Label(settings.Wiggle.Value ? "频率控制每秒变化次数，幅度控制抖动强弱；幅度为0时没有抖动。" : "抖动已关闭，下方参数暂不生效；重新开启后沿用原数值。", hint, Array.Empty<GUILayoutOption>());
		bool enabled = GUI.enabled;
		GUI.enabled = enabled && settings.Wiggle.Value;
		try
		{
			settings.Frequency.Value = (int)SliderRow("抖动频率", settings.Frequency.Value, 4f, 24f, (float value) => value.ToString("F0", CultureInfo.InvariantCulture) + " Hz", 1f);
			GUILayout.Space(12f);
			Slider("抖动幅度", settings.Amount, 0f, 2f, "F2");
		}
		finally
		{
			GUI.enabled = enabled;
		}
		GUILayout.Space(12f);
		settings.Entrance.Value = NamedSelection("入场动画", settings.Entrance.Value, Entrance);
		GUILayout.Space(14f);
		settings.Exit.Value = NamedSelection("退场动画", settings.Exit.Value, Exit);
		GUILayout.Space(22f);
		if (Button("预览8秒并返回游戏"))
		{
			plugin.Preview();
		}
		GUILayout.Label("优先使用播放器逐字时间；普通 LRC 按行时长估算。歌词只在本机显示。", hint, Array.Empty<GUILayoutOption>());
	}

	private static void FoliaStyle(Plugin plugin)
	{
		MusicSettings settings = plugin.Settings;
		int num = FoliaStyles.Index(settings.Style.Value);
		SectionTitle("歌词显示样式");
		GUILayout.Label("保留Dango原样式，新增Folia全部15种内置样式。选择后自动保存。", hint, Array.Empty<GUILayoutOption>());
		GUILayout.Space(12f);
		int num2 = PresetGrid(num, FoliaStyles.Labels);
		if (num2 != num)
		{
			settings.Style.Value = FoliaStyles.Ids[num2];
			num = num2;
		}
		GUILayout.Space(8f);
		GUILayout.Label(FoliaStyles.Descriptions[num], hint, Array.Empty<GUILayoutOption>());
		GUILayout.Space(16f);
		if (num == 0)
		{
			GUILayout.Label("Dango沿用当前左右位置、字号、亮度和动画设置。", hint, Array.Empty<GUILayoutOption>());
			return;
		}
		GUILayout.Label(plugin.TextRenderStatus, hint, Array.Empty<GUILayoutOption>());
		if (Button("重新准备Folia样式"))
		{
			plugin.RestartFolia();
		}
		GUILayout.Space(24f);
		SectionTitle("舞台显示");
		settings.FoliaPlacement.Value = NamedSelection("舞台位置", settings.FoliaPlacement.Value, new string[3] { "两侧悬浮", "两侧屏幕", "全屏舞台" });
		GUILayout.Label("两侧悬浮在菜单、加载和设置界面自动接续为屏幕舞台。", hint, Array.Empty<GUILayoutOption>());
		Slider("舞台大小", settings.FoliaSize, 0.5f, 1.8f, "F2");
		Slider("整体透明度", settings.FoliaOpacity, 0.2f, 1f, "F2");
		Slider("Folia字体倍率", settings.FoliaFontScale, 0.5f, 2f, "F2");
		Toggle("舞台底色", settings.FoliaBackground);
		settings.FoliaIntensity.Value = NamedSelection("动效强度", settings.FoliaIntensity.Value, new string[3] { "轻柔", "标准", "活泼" });
		GUILayout.Space(24f);
		SectionTitle("画质与性能");
		settings.FoliaQuality.Value = NamedSelection("画质", settings.FoliaQuality.Value, new string[3] { "省资源 360p", "均衡 540p", "清晰 720p" });
		int[] array = new int[4] { 15, 24, 30, 60 };
		settings.FoliaFps.Value = array[NamedSelection("舞台帧率", Math.Max(0, Array.IndexOf(array, settings.FoliaFps.Value)), new string[4] { "15", "24", "30", "60" })];
		Toggle("兼容渲染（显卡渲染失败时开启）", settings.FoliaSoftware);
		GUILayout.Label("原样式使用现有曲目、时间轴和歌词。未提供的音频频谱／节拍／封面不会伪造；相关附加效果以实际输入为准。", hint, Array.Empty<GUILayoutOption>());
		GUILayout.Space(16f);
		if (Button("预览20秒并返回游戏"))
		{
			plugin.Preview(20);
		}
		GUILayout.Label("使用「启用漂浮歌词」开关显隐；切回Dango会停止Folia舞台以释放资源。", hint, Array.Empty<GUILayoutOption>());
	}

	private static bool Button(string label, float width = 0f)
	{
		bool flag = ((width > 0f) ? GUILayout.Button(label, buttonStyle, new GUILayoutOption[2]
		{
			GUILayout.Width(width),
			GUILayout.Height(rowHeight)
		}) : GUILayout.Button(label, buttonStyle, new GUILayoutOption[1] { GUILayout.Height(rowHeight) }));
		if (flag)
		{
			SetFeedback("已执行：" + label);
		}
		return flag;
	}

	private static int Selection(int selected, string[] labels)
	{
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(rowHeight)
		});
		float num = 9.6f;
		float num2 = Mathf.Max(0f, (rect.width - num * (float)(labels.Length - 1)) / (float)labels.Length);
		for (int i = 0; i < labels.Length; i++)
		{
			if (GUI.Button(new Rect(rect.x + (float)i * (num2 + num), rect.y, num2, rect.height), labels[i], (selected == i) ? selectedStyle : buttonStyle))
			{
				selected = i;
				SetFeedback("已选择：" + labels[i]);
			}
		}
		return selected;
	}

	private static int NamedSelection(string label, int selected, string[] labels)
	{
		GUILayout.BeginHorizontal(new GUILayoutOption[1] { GUILayout.Height(rowHeight) });
		GUILayout.Label(label, rowLabel, new GUILayoutOption[2]
		{
			GUILayout.Width(144f),
			GUILayout.Height(rowHeight)
		});
		GUILayout.Space(12f);
		int result = Selection(selected, labels);
		GUILayout.EndHorizontal();
		GUILayout.Space(6f);
		return result;
	}

	private static void Toggle(string label, ConfigEntry<bool> entry)
	{
		GUILayout.BeginHorizontal(new GUILayoutOption[1] { GUILayout.Height(rowHeight) });
		GUILayout.Label(label, rowLabel, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(rowHeight)
		});
		bool flag = GUILayout.Toggle(entry.Value, entry.Value ? "已开启" : "已关闭", buttonStyle, new GUILayoutOption[2]
		{
			GUILayout.Width(96f),
			GUILayout.Height(rowHeight)
		});
		GUILayout.EndHorizontal();
		if (flag != entry.Value)
		{
			entry.Value = flag;
			SetFeedback(label + (flag ? "：已开启" : "：已关闭"));
		}
		GUILayout.Space(8f);
	}

	private static void Slider(string label, ConfigEntry<float> entry, float min, float max, string format)
	{
		float step;
		if (format == "F0")
		{
			step = 1f;
		}
		else
		{
			step = ((format == "F1") ? 0.1f : 0.01f);
		}
		float num = SliderRow(label, entry.Value, min, max, (float value) => value.ToString(format, CultureInfo.InvariantCulture), step);
		if (GUI.enabled && Math.Abs(num - entry.Value) > 0.001f)
		{
			entry.Value = num;
			SetFeedback(label + "：" + num.ToString(format));
		}
		GUILayout.Space(16f);
	}

	private static float SliderRow(string label, float value, float min, float max, Func<float, string> display, float step)
	{
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height(36f)
		});
		float num = Mathf.Min(144f, rect.width * 0.38f);
		float num2 = Mathf.Min(96f, rect.width * 0.25f);
		GUI.Label(new Rect(rect.x, rect.y, num, rect.height), label, rowLabel);
		Rect val = new Rect(rect.x + num + 12f, rect.y + (rect.height - 24f) * 0.5f, Mathf.Max(0f, rect.width - num - num2 - 24f), 24f);
		float fixedWidth = GUI.skin.horizontalSliderThumb.fixedWidth;
		GUI.Box(new Rect(val.x + fixedWidth * 0.5f, rect.center.y - 4f, Mathf.Max(0f, val.width - fixedWidth), 8f), GUIContent.none, GUI.skin.horizontalSlider);
		float num3 = GUI.HorizontalSlider(val, Mathf.Clamp(value, min, max), min, max, sliderTrack, GUI.skin.horizontalSliderThumb);
		num3 = Mathf.Clamp(Mathf.Round(num3 / step) * step, min, max);
		NumericReadout.Draw(new Rect(rect.xMax - num2, rect.y, num2, rect.height), display(num3));
		return num3;
	}

	private static void SectionTitle(string label)
	{
		GUILayout.Space(4f);
		GUILayout.Label(label, title, Array.Empty<GUILayoutOption>());
		GUILayout.Space(14f);
	}

	private static string BalancedTitle(string text)
	{
		if (text.Length <= 24)
		{
			return text;
		}
		int num = text.Length / 2;
		int num2 = -1;
		int num3 = int.MaxValue;
		for (int i = 8; i < text.Length - 8; i++)
		{
			if ((text[i] == ' ' || text[i] == '·' || text[i] == '—' || text[i] == '/' || text[i] == '、') && Math.Abs(i - num) < num3)
			{
				num2 = i;
				num3 = Math.Abs(i - num);
			}
		}
		if (num2 >= 0)
		{
			return text.Substring(0, num2).TrimEnd() + "\n" + text.Substring(num2 + 1).TrimStart();
		}
		return text;
	}

	private static void DrawGroup(Action draw, float delay)
	{
		if (entranceAt < 0f)
		{
			entranceAt = Time.unscaledTime;
		}
		Color color = GUI.color;
		if ((int)Event.current.type == 7)
		{
			float num = Mathf.Clamp01((Time.unscaledTime - entranceAt - delay) / 0.32f);
			num = num * num * (3f - 2f * num);
			GUI.color = new Color(color.r, color.g, color.b, color.a * num);
		}
		try
		{
			draw();
		}
		finally
		{
			GUI.color = color;
		}
	}

	internal static void SetFeedback(string message)
	{
		feedback = message;
		feedbackAt = Time.unscaledTime;
	}

	private static Texture2D ButtonSurface(Color fill)
	{
		Texture2D val = new Texture2D(1, 1, (TextureFormat)4, false)
		{
			wrapMode = (TextureWrapMode)1
		};
		val.SetPixel(0, 0, fill);
		val.Apply();
		return val;
	}

	private static int PresetGrid(int selected, string[] labels, bool momentary = false, Action<int>? onPick = null)
	{
		float num = 10f;
		float num2 = rowHeight;
		int num3 = (labels.Length + 2 - 1) / 2;
		Rect rect = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, new GUILayoutOption[3]
		{
			GUILayout.MinWidth(0f),
			GUILayout.ExpandWidth(true),
			GUILayout.Height((float)num3 * num2 + (float)Math.Max(0, num3 - 1) * num)
		});
		float num4 = Mathf.Max(0f, (rect.width - 16f) / 2f);
		int result = selected;
		for (int i = 0; i < labels.Length; i++)
		{
			if (GUI.Button(new Rect(rect.x + (float)(i % 2) * (num4 + 16f), rect.y + (float)(i / 2) * (num2 + num), num4, num2), labels[i], (!momentary && selected == i) ? selectedPresetStyle : presetStyle))
			{
				result = i;
				SetFeedback("已选择：" + labels[i]);
				onPick?.Invoke(i);
			}
		}
		return result;
	}

	private static string TimeText(long ms)
	{
		return ((int)(ms / 60000)).ToString("00") + ":" + ((int)(ms / 1000 % 60)).ToString("00");
	}
}
