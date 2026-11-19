using System.Collections.Generic;
using UnityEngine;

namespace PeakDangoMusic;

internal static class UiKit
{
	internal static readonly Color Ink = new Color(0.043f, 0.05f, 0.062f, 1f);

	internal static readonly Color Panel = new Color(1f, 1f, 1f, 0.22f);

	internal static readonly Color PanelRaised = new Color(1f, 1f, 1f, 0.42f);

	internal static readonly Color Row = new Color(0.1f, 0.13f, 0.18f, 0.06f);

	internal static readonly Color RowAlt = new Color(0.1f, 0.13f, 0.18f, 0.035f);

	internal static readonly Color RowHover = new Color(0.2f, 0.62f, 0.68f, 0.18f);

	internal static readonly Color Accent = new Color(0.29f, 0.78f, 0.82f, 1f);

	internal static readonly Color AccentDeep = new Color(0.12f, 0.3f, 0.34f, 1f);

	internal static readonly Color AccentSoft = new Color(0.17f, 0.38f, 0.42f, 1f);

	internal static readonly Color Text = new Color(0.09f, 0.11f, 0.15f, 1f);

	internal static readonly Color Muted = new Color(0.32f, 0.37f, 0.44f, 1f);

	internal static readonly Color Faint = new Color(0.5f, 0.55f, 0.62f, 1f);

	internal static readonly Color Ok = new Color(0.44f, 0.84f, 0.61f, 1f);

	internal static readonly Color Warn = new Color(0.97f, 0.75f, 0.37f, 1f);

	internal static readonly Color Divider = new Color(0.1f, 0.13f, 0.17f, 0.22f);

	private static readonly Dictionary<string, Texture2D> boxes = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, Texture2D> circles = new Dictionary<string, Texture2D>();

	private static bool initialized;

	private static readonly Dictionary<string, Texture2D> glyphs = new Dictionary<string, Texture2D>();

	internal static bool Ready => initialized;

	internal static void Init()
	{
		initialized = true;
	}

	private static string Key(Color c)
	{
		return Mathf.RoundToInt(c.r * 255f) + "." + Mathf.RoundToInt(c.g * 255f) + "." + Mathf.RoundToInt(c.b * 255f) + "." + Mathf.RoundToInt(c.a * 255f);
	}

	internal static Texture2D Box(int width, int height, Color color, int radius)
	{
		width = Mathf.Clamp((width + 7) / 8 * 8, 8, 1200);
		height = Mathf.Clamp((height + 5) / 5 * 5, 5, 900);
		radius = Mathf.Clamp(radius, 0, Mathf.Min(width, height) / 2);
		string key = width + "x" + height + "/" + radius + "/" + Key(color);
		if (boxes.TryGetValue(key, out Texture2D value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		if (boxes.Count > 1400)
		{
			foreach (Texture2D value2 in boxes.Values)
			{
				if ((Object)(object)value2 != (Object)null)
				{
					Object.Destroy((Object)(object)value2);
				}
			}
			boxes.Clear();
		}
		Texture2D val = BuildRounded(width, height, color, radius);
		boxes[key] = val;
		return val;
	}

	internal static Texture2D BuildRounded(int width, int height, Color color, int radius)
	{
		Texture2D texture = new Texture2D(width, height, (TextureFormat)4, false)
		{
			wrapMode = (TextureWrapMode)1,
			filterMode = (FilterMode)1
		};
		Color32[] pixels = new Color32[width * height];
		float r = Mathf.Clamp((float)radius, 0f, (float)Mathf.Min(width, height) * 0.5f);
		float bright = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
		float w = (float)width;
		float h = (float)height;
		float band = Mathf.Clamp(Mathf.Min(w, h) * 0.22f, 3f, 44f);
		for (int y = 0; y < height; y++)
		{
			float ty = (float)y / Mathf.Max(1f, h - 1f);
			for (int x = 0; x < width; x++)
			{
				float px = (float)x + 0.5f;
				float py = (float)y + 0.5f;
				float cx = (px < r) ? r : ((px > w - r) ? (w - r) : px);
				float cy = (py < r) ? r : ((py > h - r) ? (h - r) : py);
				float dist = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
				float cover = Mathf.Clamp01(r + 0.5f - dist);
				float de = r - dist;
				float noise = (float)(((x * 73856093) ^ (y * 19349663)) & 255) / 255f - 0.5f;
				float red;
				float green;
				float blue;
				float alpha;
				if (bright > 0.5f)
				{
					float eT = py;
					float eB = h - py;
					float eL = px;
					float eR = w - px;
					float kT = Mathf.Pow(Mathf.Clamp01(1f - eT / band), 1.7f);
					float kB = Mathf.Pow(Mathf.Clamp01(1f - eB / band), 1.7f) * 0.92f;
					float kL = Mathf.Pow(Mathf.Clamp01(1f - eL / band), 1.7f) * 0.4f;
					float kR = Mathf.Pow(Mathf.Clamp01(1f - eR / band), 1.7f) * 0.4f;
					float glow = Mathf.Max(Mathf.Max(kT, kB), Mathf.Max(kL, kR));
					float body = 1f + 0.055f * Mathf.Pow(1f - ty, 1.5f) - 0.12f * ty * ty + noise * 0.015f;
					float dMin = Mathf.Min(Mathf.Min(eT, eB), Mathf.Min(eL, eR));
					float lip = (Mathf.Abs(dMin - band * 0.55f) < band * 0.45f) ? ((1f - Mathf.Abs(dMin - band * 0.55f) / (band * 0.45f)) * 0.05f) : 0f;
					red = color.r * body * (1f - lip);
					green = color.g * body * (1f - lip);
					blue = color.b * body * (1f - lip);
					red = red + (1f - red) * glow * 0.9f;
					green = green + (1f - green) * glow * 0.9f;
					blue = blue + (1f - blue) * glow * 0.9f;
					alpha = Mathf.Max(color.a, glow * 0.9f) * cover;
				}
				else
				{
					float rimW = (r < 1f) ? 0f : Mathf.Clamp(r * 0.16f, 1.6f, 4f);
					float sheen2 = Mathf.Pow(1f - ty, 2.1f) * 0.3f;
					float floorTint = Mathf.Pow(ty, 2f) * 0.16f;
					float rim = ((rimW > 0f) && de > 0f && de < rimW) ? (1f - de / rimW) : 0f;
					float innerShade = ((rimW > 0f) && de >= rimW && de < rimW + 2.6f) ? ((1f - (de - rimW) / 2.6f) * 0.16f) : 0f;
					float rimPower = rim * rim * 0.7f * (0.55f + 0.45f * (1f - ty));
					float light = Mathf.Clamp01(sheen2 + rimPower - innerShade + noise * 0.07f);
					red = (color.r + (1f - color.r) * light) * (1f - floorTint);
					green = (color.g + (1f - color.g) * light) * (1f - floorTint);
					blue = (color.b + (1f - color.b) * light) * (1f - floorTint);
					alpha = color.a * cover;
				}
				pixels[y * width + x] = new Color32((byte)(Mathf.Clamp01(red) * 255f), (byte)(Mathf.Clamp01(green) * 255f), (byte)(Mathf.Clamp01(blue) * 255f), (byte)(Mathf.Clamp01(alpha) * 255f));
			}
		}
		texture.SetPixels32(pixels);
		texture.Apply(false, false);
		return texture;
	}

	internal static Texture2D Circle(Color color, int size = 96)
	{
		size = Mathf.Clamp(size, 16, 256);
		string key = "c" + size + "/" + Key(color);
		if (circles.TryGetValue(key, out Texture2D value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		Texture2D val = new Texture2D(size, size, (TextureFormat)4, false)
		{
			wrapMode = (TextureWrapMode)1,
			filterMode = (FilterMode)1
		};
		Color32[] array = new Color32[size * size];
		float num = (float)(size - 1) * 0.5f;
		float num2 = num - 0.5f;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num3 = Mathf.Sqrt(((float)j - num) * ((float)j - num) + ((float)i - num) * ((float)i - num));
				float num4 = Mathf.Clamp01(num2 + 0.5f - num3);
				array[i * size + j] = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), (byte)(color.a * num4 * 255f));
			}
		}
		val.SetPixels32(array);
		val.Apply(false, false);
		circles[key] = val;
		return val;
	}

	internal static void Fill(Rect rect, Color color)
	{
		Color color2 = GUI.color;
		GUI.color = color;
		GUI.DrawTexture(rect, (Texture)(object)Texture2D.whiteTexture, (ScaleMode)0, true);
		GUI.color = color2;
	}

	internal static void Round(Rect rect, Color color, int radius = 8)
	{
		if (!(rect.width < 2f) && !(rect.height < 2f))
		{
			Color color2 = GUI.color;
			GUI.color = Color.white;
			GUI.DrawTexture(rect, (Texture)(object)Box(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), color, radius), (ScaleMode)0, true);
			GUI.color = color2;
		}
	}

	internal static void Shadow(Rect rect, int radius, float strength = 1f)
	{
		Round(new Rect(rect.x - 6f, rect.y - 2f, rect.width + 12f, rect.height + 14f), new Color(0.05f, 0.07f, 0.1f, 0.07f * strength), radius + 6);
	}

	internal static void Chip(Rect rect, float alpha, int radius)
	{
		Round(new Rect(rect.x - 1.5f, rect.y + 2.5f, rect.width + 3f, rect.height + 3f), new Color(0.06f, 0.09f, 0.13f, 0.10f), radius + 3);
		Round(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), new Color(1f, 1f, 1f, Mathf.Clamp01(alpha + 0.16f)), radius + 1);
		Round(rect, new Color(1f, 1f, 1f, alpha), radius);
	}

	internal static void Circle(Rect rect, Color color)
	{
		Color color2 = GUI.color;
		GUI.color = Color.white;
		GUI.DrawTexture(rect, (Texture)(object)Circle(color), (ScaleMode)0, true);
		GUI.color = color2;
	}

	internal static void Line(float x, float y, float width, Color color)
	{
		Fill(new Rect(x, y, width, 1f), color);
	}

	internal static bool Click(Rect rect)
	{
		Event current = Event.current;
		if (current == null || (int)current.type != 0 || current.button != 0)
		{
			return false;
		}
		if (!rect.Contains(current.mousePosition))
		{
			return false;
		}
		current.Use();
		return true;
	}

	internal static bool Hover(Rect rect)
	{
		Event current = Event.current;
		if (current != null)
		{
			return rect.Contains(current.mousePosition);
		}
		return false;
	}

	internal static bool Pill(Rect rect, string label, GUIStyle style, bool active, bool hoverable = true)
	{
		bool flag = hoverable && Hover(rect);
		int pillR = Mathf.RoundToInt(rect.height * 0.5f);
		Chip(rect, active ? 0.60f : (flag ? 0.44f : 0.26f), pillR);
		Color color = GUI.color;
		if (!active)
		{
			GUI.color = (Color)(flag ? Text : new Color(Muted.r, Muted.g, Muted.b, 0.95f));
		}
		else
		{
			GUI.color = Text;
		}
		GUI.Label(rect, label, style);
		GUI.color = color;
		return Click(rect);
	}

	internal static Texture2D? MaskCircle(Texture2D source, int size)
	{
		if ((Object)(object)source == (Object)null)
		{
			return null;
		}
		size = Mathf.Clamp(size, 16, 256);
		Texture2D val = new Texture2D(size, size, (TextureFormat)4, false)
		{
			wrapMode = (TextureWrapMode)1,
			filterMode = (FilterMode)1
		};
		Color32[] array = new Color32[size * size];
		float num = (float)(size - 1) * 0.5f;
		float num2 = num;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num3 = ((float)j + 0.5f) / (float)size;
				float num4 = ((float)i + 0.5f) / (float)size;
				Color pixelBilinear = source.GetPixelBilinear(num3, num4);
				float num5 = Mathf.Sqrt(((float)j - num) * ((float)j - num) + ((float)i - num) * ((float)i - num));
				float num6 = Mathf.Clamp01(num2 + 0.5f - num5);
				array[i * size + j] = new Color32((byte)(pixelBilinear.r * 255f), (byte)(pixelBilinear.g * 255f), (byte)(pixelBilinear.b * 255f), (byte)(pixelBilinear.a * num6 * 255f));
			}
		}
		val.SetPixels32(array);
		val.Apply(false, false);
		return val;
	}

	internal static Texture2D? MaskRounded(Texture2D source, int size, int radius)
	{
		if ((Object)(object)source == (Object)null)
		{
			return null;
		}
		size = Mathf.Clamp(size, 16, 256);
		radius = Mathf.Clamp(radius, 2, size / 2);
		Texture2D val = new Texture2D(size, size, (TextureFormat)4, false)
		{
			wrapMode = (TextureWrapMode)1,
			filterMode = (FilterMode)1
		};
		Color32[] array = new Color32[size * size];
		float num = size - 1;
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num2 = ((float)j + 0.5f) / (float)size;
				float num3 = ((float)i + 0.5f) / (float)size;
				Color pixelBilinear = source.GetPixelBilinear(num2, num3);
				float num4 = Mathf.Max(0f, Mathf.Max((float)(radius - j) - 0.5f, (float)j + 0.5f - (num - (float)radius)));
				float num5 = Mathf.Max(0f, Mathf.Max((float)(radius - i) - 0.5f, (float)i + 0.5f - (num - (float)radius)));
				float num6 = Mathf.Sqrt(num4 * num4 + num5 * num5);
				float num7 = ((num6 <= (float)radius) ? 1f : Mathf.Clamp01((float)radius + 0.75f - num6));
				array[i * size + j] = new Color32((byte)(pixelBilinear.r * 255f), (byte)(pixelBilinear.g * 255f), (byte)(pixelBilinear.b * 255f), (byte)(pixelBilinear.a * num7 * 255f));
			}
		}
		val.SetPixels32(array);
		val.Apply(false, false);
		return val;
	}

	private static bool GlyphTriangle(float px, float py, float ax, float ay, float bx, float by, float cx, float cy)
	{
		float d1 = (px - bx) * (ay - by) - (ax - bx) * (py - by);
		float d2 = (px - cx) * (by - cy) - (bx - cx) * (py - cy);
		float d3 = (px - ax) * (cy - ay) - (cx - ax) * (py - ay);
		bool hasNeg = (d1 < 0f) || (d2 < 0f) || (d3 < 0f);
		bool hasPos = (d1 > 0f) || (d2 > 0f) || (d3 > 0f);
		return !(hasNeg && hasPos);
	}

	private static bool GlyphRect(float px, float py, float x0, float y0, float x1, float y1, float r)
	{
		if (px < x0 || px > x1 || py < y0 || py > y1)
		{
			return false;
		}
		float num = Mathf.Clamp(px, x0 + r, x1 - r);
		float num2 = Mathf.Clamp(py, y0 + r, y1 - r);
		float num3 = px - num;
		float num4 = py - num2;
		return num3 * num3 + num4 * num4 <= r * r;
	}

	private static bool GlyphCover(string kind, float x, float y)
	{
		if (kind == "play")
		{
			return GlyphTriangle(x, y, 0.3f, 0.15f, 0.3f, 0.85f, 0.79f, 0.5f);
		}
		if (kind == "pause")
		{
			return GlyphRect(x, y, 0.3f, 0.14f, 0.44f, 0.86f, 0.05f) || GlyphRect(x, y, 0.56f, 0.14f, 0.7f, 0.86f, 0.05f);
		}
		if (kind == "prev")
		{
			return GlyphRect(x, y, 0.18f, 0.14f, 0.27f, 0.86f, 0.03f) || GlyphTriangle(x, y, 0.75f, 0.15f, 0.75f, 0.85f, 0.29f, 0.5f);
		}
		if (kind == "next")
		{
			return GlyphRect(x, y, 0.73f, 0.14f, 0.82f, 0.86f, 0.03f) || GlyphTriangle(x, y, 0.25f, 0.15f, 0.25f, 0.85f, 0.71f, 0.5f);
		}
		if (kind == "stop")
		{
			return GlyphRect(x, y, 0.31f, 0.31f, 0.69f, 0.69f, 0.05f);
		}
		if (kind == "heart" || kind == "heartOn")
		{
			float num = (x - 0.33f) * (x - 0.33f) + (y - 0.33f) * (y - 0.33f);
			float num2 = (x - 0.67f) * (x - 0.67f) + (y - 0.33f) * (y - 0.33f);
			if (num <= 0.031f || num2 <= 0.031f)
			{
				return true;
			}
			return GlyphTriangle(x, y, 0.13f, 0.4f, 0.87f, 0.4f, 0.5f, 0.88f);
		}
		return false;
	}

	internal static Texture2D Glyph(string kind, int size, Color color)
	{
		string key = kind + "#" + size + "#" + ((object)color/*cast due to constrained. prefix*/).ToString();
		if (glyphs.TryGetValue(key, out Texture2D value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		size = Mathf.Clamp(size, 12, 96);
		Texture2D val = new Texture2D(size, size, (TextureFormat)4, false)
		{
			wrapMode = (TextureWrapMode)1,
			filterMode = (FilterMode)1
		};
		Color32[] array = new Color32[size * size];
		int num = 4;
		float num2 = 1f / (float)(num * num);
		for (int i = 0; i < size; i++)
		{
			for (int j = 0; j < size; j++)
			{
				float num3 = 0f;
				for (int k = 0; k < num; k++)
				{
					for (int l = 0; l < num; l++)
					{
						float x = ((float)j + ((float)l + 0.5f) / (float)num) / (float)size;
						float y = ((float)i + ((float)k + 0.5f) / (float)num) / (float)size;
						if (GlyphCover(kind, x, y))
						{
							num3 += num2;
						}
					}
				}
				array[i * size + j] = new Color32((byte)(color.r * 255f), (byte)(color.g * 255f), (byte)(color.b * 255f), (byte)(color.a * num3 * 255f));
			}
		}
		val.SetPixels32(array);
		val.Apply(false, false);
		glyphs[key] = val;
		return val;
	}

	internal static string Initial(string name)
	{
		name = name.Trim();
		if (name.Length == 0)
		{
			return "?";
		}
		char c = name[0];
		if (char.IsHighSurrogate(c) && name.Length > 1)
		{
			return name.Substring(0, 2).ToUpperInvariant();
		}
		return c.ToString().ToUpperInvariant();
	}

	internal static Color Tint(string seed)
	{
		int num = 0;
		foreach (char c in seed)
		{
			num = (num * 31 + c) & 0x7FFFFFFF;
		}
		return Color.HSVToRGB((float)(num % 360) / 360f, 0.42f, 0.62f);
	}

	static UiKit()
	{
	}
}
