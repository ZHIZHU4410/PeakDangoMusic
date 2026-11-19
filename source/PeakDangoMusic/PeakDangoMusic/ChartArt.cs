using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PeakDangoMusic;

internal static class ChartArt
{
	private static readonly string[] Files = new string[8] { "chart_hot.jpg", "chart_new.jpg", "chart_soar.jpg", "chart_origin.jpg", "qq_hot.jpg", "qq_new.jpg", "qq_soar.jpg", "qq_pop.jpg" };

	private static readonly Dictionary<string, Texture2D> Ready = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, bool> Broken = new Dictionary<string, bool>();

	internal static Texture2D? For(int index, int size)
	{
		if (index < 0 || index >= Files.Length)
		{
			return null;
		}
		size = Mathf.Clamp(size, 64, 256);
		string key = Files[index] + "#" + size;
		if (Ready.TryGetValue(key, out Texture2D value) && (Object)(object)value != (Object)null)
		{
			return value;
		}
		if (Broken.ContainsKey(key))
		{
			return null;
		}
		Texture2D texture2D = Load(Files[index]);
		if ((Object)(object)texture2D == (Object)null)
		{
			Broken[key] = true;
			return null;
		}
		Texture2D texture2D2 = UiKit.MaskRounded(texture2D, size, Mathf.Max(10, size / 14));
		Object.Destroy((Object)(object)texture2D);
		if ((Object)(object)texture2D2 == (Object)null)
		{
			Broken[key] = true;
			return null;
		}
		Ready[key] = texture2D2;
		return texture2D2;
	}

	internal static bool Ready_(int index)
	{
		return index >= 0 && index < Files.Length;
	}

	private static Texture2D? Load(string file)
	{
		try
		{
			using Stream stream = typeof(ChartArt).Assembly.GetManifestResourceStream("PeakDangoMusic." + file);
			if (stream == null)
			{
				return null;
			}
			using MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			Texture2D texture2D = new Texture2D(2, 2, (TextureFormat)4, false)
			{
				hideFlags = (HideFlags)61,
				filterMode = (FilterMode)1,
				wrapMode = (TextureWrapMode)1
			};
			if (!ImageConversion.LoadImage(texture2D, memoryStream.ToArray(), false))
			{
				Object.Destroy((Object)(object)texture2D);
				return null;
			}
			return texture2D;
		}
		catch
		{
			return null;
		}
	}
}
