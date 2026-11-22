using System;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>
/// 设置里那块调色盘：左边明度/饱和度方块，右边色相条，下面是预设和保存。
/// 界面颜色和歌词字色各用一份，互不影响。
/// </summary>
internal static class ThemePicker
{
	internal static readonly string[] PresetNames = new string[6] { "玻璃白", "桃粉", "晴蓝", "薄荷", "琥珀", "暮紫" };

	internal static readonly string[] PresetHex = new string[6] { "", "#FF9AB0", "#7FC4F2", "#8FE0C4", "#F2C46B", "#B79CF0" };

	// 歌词字色的第一个预设是纯白，不是空值，点了就是白色。
	internal static readonly string[] PresetHexLyric = new string[6] { "#FFFFFF", "#FF9AB0", "#7FC4F2", "#8FE0C4", "#F2C46B", "#B79CF0" };

	private sealed class Wheel
	{
		internal ConfigEntry<string>? Entry;
		internal Action<string>? Applied;
		internal Texture2D? Square;
		internal Texture2D? Strip;
		internal float Hue;
		internal float Sat;
		internal float Val;
		internal float SquareHue = float.NaN;
		internal bool DragSquare;
		internal bool DragStrip;
		internal float SavedAt = -10f;
	}

	private static readonly Wheel ThemeWheel = new Wheel
	{
		Hue = 0.72f,
		Sat = 0.55f,
		Val = 0.9f
	};

	private static readonly Wheel LyricWheel = new Wheel
	{
		Hue = 0f,
		Sat = 0f,
		Val = 1f
	};

	private static GUIStyle? small;
	private static GUIStyle? pill;

	internal static void Load(string hex)
	{
		ThemeWheel.Applied = UiKit.ApplyThemeHex;
		ApplyHex(ThemeWheel, hex, 0.72f, 0.55f, 0.9f);
		UiKit.ApplyThemeHex(hex);
	}

	internal static void Draw(Plugin plugin, float width)
	{
		ThemeWheel.Entry = plugin.Settings.ThemeColor;
		ThemeWheel.Applied = UiKit.ApplyThemeHex;
		DrawWheel(plugin, width, ThemeWheel, "当前：默认玻璃白", "默认玻璃白");
	}

	internal static void DrawLyric(Plugin plugin, float width)
	{
		LyricWheel.Entry = plugin.Settings.LyricColor;
		LyricWheel.Applied = null;
		DrawWheel(plugin, width, LyricWheel, "当前：跟随深浅字色", "恢复跟随");
	}

	private static void ApplyHex(Wheel wheel, string hex, float hue, float sat, float val)
	{
		Color parsed;
		if (!string.IsNullOrEmpty(hex) && UiKit.TryParseHex(hex, out parsed))
		{
			Color.RGBToHSV(new Color(parsed.r, parsed.g, parsed.b, 1f), out hue, out sat, out val);
		}
		wheel.Hue = hue;
		wheel.Sat = sat;
		wheel.Val = val;
	}

	private static Color Picked(Wheel wheel)
	{
		return Color.HSVToRGB(wheel.Hue, Mathf.Clamp01(wheel.Sat), Mathf.Clamp(wheel.Val, 0.35f, 1f));
	}

	private static void DrawWheel(Plugin plugin, float width, Wheel wheel, string emptyLabel, string resetLabel)
	{
		EnsureStyles();

		Rect area = GUILayoutUtility.GetRect(width, 112f, new GUILayoutOption[2]
		{
			GUILayout.Width(width),
			GUILayout.Height(112f)
		});
		Rect squareRect = new Rect(area.x, area.y, 200f, 110f);
		Rect stripRect = new Rect(area.x + 212f, area.y, 18f, 110f);
		Rect previewRect = new Rect(area.x + 248f, area.y, 46f, 46f);
		Rect hexRect = new Rect(area.x + 248f, area.y + 52f, 240f, 22f);
		Rect resetRect = new Rect(area.x + 248f, area.y + 80f, 108f, 30f);
		Rect saveRect = new Rect(area.x + 364f, area.y + 80f, 96f, 30f);
		Rect savedRect = new Rect(area.x + 470f, area.y + 84f, 90f, 22f);

		HandleMouse(plugin, wheel, squareRect, stripRect);

		EnsureSquare(wheel);
		EnsureStrip(wheel);

		UiKit.Round(new Rect(squareRect.x - 3f, squareRect.y - 3f, squareRect.width + 6f, squareRect.height + 6f), new Color(1f, 1f, 1f, 0.45f), 12);
		if (wheel.Square != null)
		{
			GUI.DrawTexture(squareRect, wheel.Square, ScaleMode.StretchToFill, true);
		}

		float dotX = squareRect.x + wheel.Sat * squareRect.width;
		float dotY = squareRect.y + (1f - wheel.Val) * squareRect.height;
		UiKit.Circle(new Rect(dotX - 6f, dotY - 6f, 12f, 12f), new Color(0.05f, 0.07f, 0.1f, 0.75f));
		UiKit.Circle(new Rect(dotX - 4.5f, dotY - 4.5f, 9f, 9f), Color.white);

		if (wheel.Strip != null)
		{
			GUI.DrawTexture(stripRect, wheel.Strip, ScaleMode.StretchToFill, true);
		}
		UiKit.Round(new Rect(stripRect.x, stripRect.y + wheel.Hue * stripRect.height - 2f, stripRect.width, 4f), Color.white, 2);
		UiKit.Round(previewRect, Picked(wheel), 12);

		string saved = wheel.Entry?.Value ?? "";
		GUI.Label(hexRect, string.IsNullOrEmpty(saved) ? emptyLabel : ("当前：" + saved), small);

		if (UiKit.Pill(resetRect, resetLabel, pill, string.IsNullOrEmpty(saved)))
		{
			if (wheel.Entry != null)
			{
				wheel.Entry.Value = "";
			}
			bool lyric = wheel == LyricWheel;
			ApplyHex(wheel, "", (lyric ? 0f : 0.72f), (lyric ? 0f : 0.55f), (lyric ? 1f : 0.9f));
			wheel.Applied?.Invoke("");
			wheel.SavedAt = Time.unscaledTime;
		}

		if (UiKit.Pill(saveRect, "保存", pill, false))
		{
			Color chosen = Picked(wheel);
			Color stored;
			if (!string.IsNullOrEmpty(saved) && UiKit.TryParseHex(saved, out stored))
			{
				chosen = stored;
			}
			if (wheel.Entry != null)
			{
				wheel.Entry.Value = UiKit.ToHex(chosen);
			}
			wheel.Applied?.Invoke(wheel.Entry?.Value ?? "");
			plugin.Config.Save();
			wheel.SavedAt = Time.unscaledTime;
		}

		if (Time.unscaledTime - wheel.SavedAt < 2.5f)
		{
			GUI.Label(savedRect, "已保存", small);
		}

		GUILayout.Space(12f);
		Rect presetsRect = GUILayoutUtility.GetRect(width, 32f, new GUILayoutOption[2]
		{
			GUILayout.Width(width),
			GUILayout.Height(32f)
		});
		float cell = 104f;
		float cursor = presetsRect.x;
		string[] presets = ((wheel == LyricWheel) ? PresetHexLyric : PresetHex);
		for (int i = 0; i < PresetNames.Length; i++)
		{
			Rect rect = new Rect(cursor, presetsRect.y, cell, 30f);
			string hex = presets[i];
			bool active = ((string.IsNullOrEmpty(hex) && string.IsNullOrEmpty(saved)) || (!string.IsNullOrEmpty(hex) && string.Equals(hex, saved, StringComparison.OrdinalIgnoreCase)));
			UiKit.Circle(new Rect(rect.x + 12f, rect.y + 10f, 10f, 10f), UiKit.ParseHexSafe(hex));
			if (UiKit.Pill(new Rect(rect.x + 22f, rect.y, cell - 22f, 30f), PresetNames[i], pill, active))
			{
				if (wheel.Entry != null)
				{
					wheel.Entry.Value = hex;
				}
				bool lyric = wheel == LyricWheel;
				ApplyHex(wheel, hex, (lyric ? 0f : 0.72f), (lyric ? 0f : 0.55f), (lyric ? 1f : 0.9f));
				wheel.Applied?.Invoke(hex);
				wheel.SavedAt = Time.unscaledTime;
			}
			cursor += cell + 8f;
		}
	}

	private static void HandleMouse(Plugin plugin, Wheel wheel, Rect squareRect, Rect stripRect)
	{
		Event current = Event.current;
		if (current == null)
		{
			return;
		}
		if (current.type == EventType.MouseDown && current.button == 0)
		{
			if (squareRect.Contains(current.mousePosition))
			{
				wheel.DragSquare = true;
			}
			else if (stripRect.Contains(current.mousePosition))
			{
				wheel.DragStrip = true;
			}
		}
		if (wheel.DragSquare && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag))
		{
			// 方块上面是亮、下面是暗，左边饱和度低、右边高
			wheel.Sat = Mathf.Clamp01((current.mousePosition.x - squareRect.x) / squareRect.width);
			wheel.Val = Mathf.Clamp01(1f - (current.mousePosition.y - squareRect.y) / squareRect.height);
			current.Use();
		}
		if (wheel.DragStrip && (current.type == EventType.MouseDown || current.type == EventType.MouseDrag))
		{
			wheel.Hue = Mathf.Clamp01((current.mousePosition.y - stripRect.y) / stripRect.height);
			current.Use();
		}
		if (current.type == EventType.MouseUp)
		{
			if (wheel.DragSquare || wheel.DragStrip)
			{
				Save(wheel);
			}
			wheel.DragSquare = false;
			wheel.DragStrip = false;
		}
	}

	private static void Save(Wheel wheel)
	{
		if (wheel.Entry == null)
		{
			return;
		}
		wheel.Entry.Value = UiKit.ToHex(Picked(wheel));
		wheel.Applied?.Invoke(wheel.Entry.Value);
		wheel.SavedAt = Time.unscaledTime;
	}

	private static void EnsureStyles()
	{
		if (small == null)
		{
			small = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12
			};
			small.normal.textColor = UiKit.Muted;
		}
		if (pill == null)
		{
			pill = new GUIStyle(GUI.skin.label)
			{
				fontSize = 12,
				fontStyle = FontStyle.Bold,
				alignment = TextAnchor.MiddleCenter
			};
		}
	}

	// 方块贴图的第 0 行在界面最下面，所以越往上越亮。
	private static void EnsureSquare(Wheel wheel)
	{
		if (wheel.Square != null && Mathf.Abs(wheel.SquareHue - wheel.Hue) < 0.0005f)
		{
			return;
		}
		if (wheel.Square != null)
		{
			UnityEngine.Object.Destroy(wheel.Square);
		}

		const int width = 120;
		const int height = 66;
		wheel.Square = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};

		Color32[] pixels = new Color32[width * height];
		for (int y = 0; y < height; y++)
		{
			float brightness = (float)y / (float)(height - 1);
			for (int x = 0; x < width; x++)
			{
				float saturation = (float)x / (float)(width - 1);
				pixels[y * width + x] = Color.HSVToRGB(wheel.Hue, saturation, Mathf.Max(0.02f, brightness));
			}
		}
		wheel.Square.SetPixels32(pixels);
		wheel.Square.Apply(false, false);
		wheel.SquareHue = wheel.Hue;
	}

	// 色相条同理，红色在最上面。
	private static void EnsureStrip(Wheel wheel)
	{
		if (wheel.Strip != null)
		{
			return;
		}

		const int width = 4;
		const int height = 90;
		wheel.Strip = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			wrapMode = TextureWrapMode.Clamp,
			filterMode = FilterMode.Bilinear
		};

		Color32[] pixels = new Color32[width * height];
		for (int y = 0; y < height; y++)
		{
			Color color = Color.HSVToRGB(1f - (float)y / (float)(height - 1), 1f, 1f);
			for (int x = 0; x < width; x++)
			{
				pixels[y * width + x] = color;
			}
		}
		wheel.Strip.SetPixels32(pixels);
		wheel.Strip.Apply(false, false);
	}
}
