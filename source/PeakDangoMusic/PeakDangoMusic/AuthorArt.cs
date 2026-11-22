using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PeakDangoMusic;

internal static class AuthorArt
{
	private static readonly string[] Files = new string[4] { "author_boluofen.jpg", "author_dv.jpg", "author_test.jpg", "author_yinwaida.jpg" };

	private static readonly Dictionary<string, Texture2D> Ready = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, bool> Broken = new Dictionary<string, bool>();

	internal static Texture2D? For(int index, int size)
	{
		if (index < 0 || index >= Files.Length)
		{
			return null;
		}
		size = Mathf.Clamp(size, 48, 320);
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
		Texture2D texture2D2 = UiKit.MaskCircle(texture2D, size);
		Object.Destroy((Object)(object)texture2D);
		if ((Object)(object)texture2D2 == (Object)null)
		{
			Broken[key] = true;
			return null;
		}
		Ready[key] = texture2D2;
		return texture2D2;
	}

	private static Texture2D? Load(string file)
	{
		try
		{
			using Stream stream = typeof(AuthorArt).Assembly.GetManifestResourceStream("PeakDangoMusic." + file);
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
