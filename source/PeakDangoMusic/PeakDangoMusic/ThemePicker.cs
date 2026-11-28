using System;
using BepInEx.Configuration;
using UnityEngine;

namespace PeakDangoMusic;

internal static class ThemePicker
{
	private static readonly string[] PresetNames = { "白色", "黑色", "桃粉", "晴蓝", "薄荷", "琥珀", "暮紫", "正红" };
	private static readonly string[] PresetHex = { "#FFFFFF", "#000000", "#FF9AB0", "#7FC4F2", "#8FE0C4", "#F2C46B", "#B79CF0", "#FF0000" };

	private sealed class Wheel
	{
		internal readonly int ControlHint;
		internal string? Applied;
		internal string Hex = "";
		internal float Hue;
		internal float Saturation;
		internal float Value;
		internal int DragArea;

		internal Wheel(string id)
		{
			ControlHint = id.GetHashCode();
		}

		internal void Sync(string stored, Color fallback)
		{
			Applied = stored;
			Hex = stored;
			Color.RGBToHSV(UiKit.TryParseHex(stored, out var color) ? color : fallback, out Hue, out Saturation, out Value);
		}
	}

	private static readonly Wheel Theme = new Wheel("muonium-theme-color");
	private static readonly Wheel InterfaceText = new Wheel("muonium-interface-text-color");
	private static readonly Wheel Lyrics = new Wheel("muonium-lyric-color");
	private static readonly AdaptiveLyrics.Palette LyricPalette = new AdaptiveLyrics.Palette();
	private static Texture2D? saturationMask;
	private static Texture2D? valueMask;
	private static Texture2D? hueStrip;
	private static GUIStyle? small;
	private static GUIStyle? centered;
	private static GUIStyle? hexStyle;
	private static GUISkin? styleSkin;
	private static int styleVersion = -1;

	internal static void Draw(Plugin plugin)
	{
		DrawWheel(plugin.Settings.ThemeColor, Theme, UiKit.WindowTint, "默认玻璃白");
	}

	internal static void DrawInterfaceText(Plugin plugin)
	{
		DrawWheel(plugin.Settings.InterfaceTextColor, InterfaceText, UiKit.Text, "跟随底色");
	}

	internal static void DrawLyric(Plugin plugin)
	{
		LyricPalette.Update(default);
		DrawWheel(plugin.Settings.LyricColor, Lyrics, LyricPalette.Face, "跟随深浅");
	}

	private static void DrawWheel(ConfigEntry<string> entry, Wheel wheel, Color fallback, string resetLabel)
	{
		EnsureStyles();
		if (wheel.Applied != entry.Value)
			wheel.Sync(entry.Value, fallback);
		else if (wheel.Hex.Length == 0)
			Color.RGBToHSV(fallback, out wheel.Hue, out wheel.Saturation, out wheel.Value);

		const float height = 118f;
		Rect area = GUILayoutUtility.GetRect(190f, height, GUILayout.ExpandWidth(true));
		float controlsWidth = Mathf.Clamp(area.width * 0.42f, 92f, 180f);
		Rect square = new Rect(area.x, area.y, Mathf.Max(32f, area.width - controlsWidth - 38f), height);
		Rect strip = new Rect(square.xMax + 10f, area.y, 16f, height);
		float controlsX = strip.xMax + 12f;

		HandleMouse(entry, wheel, square, strip);
		if (Event.current.type == EventType.Repaint)
		{
			EnsureTextures();
			UiKit.Fill(square, Color.HSVToRGB(wheel.Hue, 1f, 1f));
			GUI.DrawTexture(square, saturationMask, ScaleMode.StretchToFill, true);
			GUI.DrawTexture(square, valueMask, ScaleMode.StretchToFill, true);
			GUI.DrawTexture(strip, hueStrip, ScaleMode.StretchToFill, false);

			float x = square.x + wheel.Saturation * (square.width - 1f);
			float y = square.y + (1f - wheel.Value) * (square.height - 1f);
			UiKit.Circle(new Rect(x - 5f, y - 5f, 10f, 10f), Color.black);
			UiKit.Circle(new Rect(x - 3.5f, y - 3.5f, 7f, 7f), Color.white);
			float markerY = strip.y + wheel.Hue * (strip.height - 1f);
			UiKit.Fill(new Rect(strip.x - 1f, markerY - 2f, strip.width + 2f, 4f), Color.black);
			UiKit.Fill(new Rect(strip.x, markerY - 1f, strip.width, 2f), Color.white);
		}

		Color shown = UiKit.TryParseHex(entry.Value, out var custom) ? custom : fallback;
		Rect sample = new Rect(controlsX, area.y, controlsWidth, 30f);
		if (wheel == Theme)
			DrawSwatch(sample, shown, 6f);
		else
		{
			float half = sample.width * 0.5f;
			DrawSwatch(new Rect(sample.x, sample.y, half, sample.height), Color.white, 3f);
			DrawSwatch(new Rect(sample.x + half, sample.y, half, sample.height), new Color(0.08f, 0.09f, 0.12f), 3f);
			centered!.normal.textColor = shown;
			GUI.Label(new Rect(sample.x, sample.y, half, sample.height), "字色", centered);
			GUI.Label(new Rect(sample.x + half, sample.y, half, sample.height), "字色", centered);
		}

		Rect input = new Rect(controlsX, area.y + 36f, controlsWidth, 26f);
		string edited = GUI.TextField(input, wheel.Hex, 7, hexStyle);
		if (edited != wheel.Hex)
		{
			wheel.Hex = edited;
			if ((edited.Length == 6 && edited[0] != '#' || edited.Length == 7 && edited[0] == '#') &&
				UiKit.TryParseHex(edited, out var color))
			{
				Color.RGBToHSV(color, out wheel.Hue, out wheel.Saturation, out wheel.Value);
				Apply(entry, wheel, UiKit.ToHex(color));
			}
		}
		string hint = wheel.Hex.Length == 0 ? "#RRGGBB" : UiKit.TryParseHex(wheel.Hex, out _) ? "实时应用" : "输入 6 位色号";
		GUI.Label(new Rect(controlsX, area.y + 64f, controlsWidth, 18f), hint, small);
		if (UiKit.Pill(new Rect(controlsX, area.y + 86f, controlsWidth, 29f), resetLabel, small!, entry.Value.Length == 0))
		{
			Apply(entry, wheel, "");
			wheel.Sync("", wheel == InterfaceText ? UiKit.Text : wheel == Lyrics ? LyricPalette.Face : UiKit.WindowTint);
		}

		GUILayout.Space(6f);
		for (int row = 0; row < 2; row++)
		{
			Rect presets = GUILayoutUtility.GetRect(190f, 30f, GUILayout.ExpandWidth(true));
			float cell = presets.width / 4f;
			for (int col = 0; col < 4; col++)
			{
				int index = row * 4 + col;
				Rect button = new Rect(presets.x + cell * col, presets.y, cell - 4f, 26f);
				string hex = PresetHex[index];
				bool active = string.Equals(entry.Value, hex, StringComparison.OrdinalIgnoreCase);
				if (UiKit.Pill(button, PresetNames[index], small!, active))
				{
					Apply(entry, wheel, hex);
					wheel.Sync(hex, fallback);
				}
				if (button.width >= 68f)
					DrawSwatch(new Rect(button.x + 6f, button.y + 8f, 10f, 10f), UiKit.ParseHexSafe(hex), 2f);
			}
		}
	}

	private static void Apply(ConfigEntry<string> entry, Wheel wheel, string hex)
	{
		wheel.Applied = hex;
		if (entry.Value != hex)
			entry.Value = hex;
	}

	private static void HandleMouse(ConfigEntry<string> entry, Wheel wheel, Rect square, Rect strip)
	{
		Event input = Event.current;
		int id = GUIUtility.GetControlID(wheel.ControlHint, FocusType.Passive);
		var type = input.GetTypeForControl(id);
		if (type == EventType.MouseDown && input.button == 0)
		{
			wheel.DragArea = square.Contains(input.mousePosition) ? 1 : strip.Contains(input.mousePosition) ? 2 : 0;
			if (wheel.DragArea != 0)
			{
				GUIUtility.hotControl = id;
				GUIUtility.keyboardControl = 0;
			}
		}
		if (GUIUtility.hotControl != id || (type != EventType.MouseDown && type != EventType.MouseDrag && type != EventType.MouseUp))
			return;

		if (wheel.DragArea == 1)
		{
			wheel.Saturation = Mathf.Clamp01((input.mousePosition.x - square.x) / (square.width - 1f));
			wheel.Value = 1f - Mathf.Clamp01((input.mousePosition.y - square.y) / (square.height - 1f));
		}
		else
		{
			wheel.Hue = Mathf.Clamp01((input.mousePosition.y - strip.y) / (strip.height - 1f));
		}
		wheel.Hex = UiKit.ToHex(Color.HSVToRGB(wheel.Hue, wheel.Saturation, wheel.Value));
		Apply(entry, wheel, wheel.Hex);
		if (type == EventType.MouseUp)
		{
			GUIUtility.hotControl = 0;
			wheel.DragArea = 0;
		}
		input.Use();
	}

	private static void EnsureStyles()
	{
		if (styleSkin != GUI.skin)
		{
			styleSkin = GUI.skin;
			styleVersion = -1;
			small = new GUIStyle(GUI.skin.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false };
			centered = new GUIStyle(small) { fontSize = 14, fontStyle = FontStyle.Bold };
			hexStyle = new GUIStyle(GUI.skin.textField)
			{
				fontSize = 13,
				alignment = TextAnchor.MiddleCenter,
				padding = new RectOffset(4, 4, 0, 0)
			};
		}
		if (styleVersion == UiKit.ThemeVersion)
			return;
		styleVersion = UiKit.ThemeVersion;
		small!.normal.textColor = UiKit.Muted;
		UiKit.SetTextColor(hexStyle!, UiKit.Text);
		hexStyle!.normal.background = GUI.skin.textField.normal.background;
		hexStyle.hover.background = GUI.skin.textField.hover.background;
		hexStyle.focused.background = GUI.skin.textField.focused.background;
	}

	private static void DrawSwatch(Rect rect, Color color, float radius)
	{
		GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, color, 0f, radius);
	}

	private static void EnsureTextures()
	{
		if (hueStrip != null)
			return;
		const int steps = 256;
		var colors = new Color[steps];
		for (int i = 0; i < steps; i++)
			colors[i] = Color.HSVToRGB(1f - i / (steps - 1f), 1f, 1f);
		hueStrip = Gradient(1, steps, colors);
		for (int i = 0; i < steps; i++)
			colors[i] = new Color(1f, 1f, 1f, 1f - i / (steps - 1f));
		saturationMask = Gradient(steps, 1, colors);
		for (int i = 0; i < steps; i++)
			colors[i] = new Color(0f, 0f, 0f, 1f - i / (steps - 1f));
		valueMask = Gradient(1, steps, colors);
	}

	private static Texture2D Gradient(int width, int height, Color[] colors)
	{
		var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
		{
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		texture.SetPixels(colors);
		texture.Apply(false, true);
		return texture;
	}
}
