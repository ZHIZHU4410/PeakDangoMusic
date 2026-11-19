using System.IO;
using UnityEngine;

namespace PeakDangoMusic;

internal static class NumericReadout
{
	internal const string Characters = "0123456789.-+ msHz";

	private static Texture2D? atlas;

	internal static void Draw(Rect rect, string text)
	{
		if ((int)Event.current.type != 7 || text.Length == 0)
		{
			return;
		}
		if ((Object)(object)atlas == (Object)null)
		{
			using Stream stream = typeof(NumericReadout).Assembly.GetManifestResourceStream("PeakDangoMusic.NumericGlyphs.png") ?? throw new InvalidDataException("Missing numeric glyph atlas");
			using MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			atlas = new Texture2D(2, 2, (TextureFormat)4, false)
			{
				hideFlags = (HideFlags)61,
				filterMode = (FilterMode)1,
				wrapMode = (TextureWrapMode)1
			};
			if (!ImageConversion.LoadImage(atlas, memoryStream.ToArray(), true))
			{
				throw new InvalidDataException("Invalid numeric glyph atlas");
			}
		}
		float num = Mathf.Min(10f, rect.width / (float)text.Length);
		float num2 = num * 1.8f;
		float num3 = rect.xMax - num * (float)text.Length;
		float num4 = rect.center.y - num2 * 0.5f;
		Color color = GUI.color;
		try
		{
			if (!GUI.enabled)
			{
				GUI.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
			}
			for (int i = 0; i < text.Length; i++)
			{
				int num5 = "0123456789.-+ msHz".IndexOf(text[i]);
				if (num5 < 0)
				{
					throw new InvalidDataException("Unsupported numeric character: " + text[i]);
				}
				if (text[i] != ' ')
				{
					GUI.DrawTextureWithTexCoords(new Rect(num3 + (float)i * num, num4, num, num2), (Texture)(object)atlas, new Rect((float)num5 / (float)"0123456789.-+ msHz".Length, 0f, 1f / (float)"0123456789.-+ msHz".Length, 1f), true);
				}
			}
		}
		finally
		{
			GUI.color = color;
		}
	}

	internal static void Dispose()
	{
		if ((Object)(object)atlas != (Object)null)
		{
			Object.Destroy((Object)(object)atlas);
		}
		atlas = null;
	}
}
