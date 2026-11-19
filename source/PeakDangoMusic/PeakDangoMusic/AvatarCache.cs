using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class AvatarCache
{
	private static readonly HttpClient http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(12.0)
	};

	private static string requested = "";

	private static string loadedUrl = "";

	private static Texture2D? texture;

	private static Task<byte[]>? download;

	private static bool failed;

	internal static Texture2D? For(string? url, int size)
	{
		url = Normalize(url);
		if (url.Length == 0)
		{
			return null;
		}
		if (url != requested)
		{
			requested = url;
			failed = false;
			download = Download(url);
		}
		if (url == requested && url != loadedUrl && !failed && download != null && download.IsCompleted)
		{
			try
			{
				if (download.IsFaulted || download.IsCanceled)
				{
					failed = true;
				}
				else
				{
					byte[] result = download.Result;
					Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false);
					if (ImageConversion.LoadImage(val, result))
					{
						Texture2D? val2 = UiKit.MaskCircle(val, size);
						if ((Object)(object)texture != (Object)null)
						{
							Object.Destroy((Object)(object)texture);
						}
						texture = val2;
						loadedUrl = url;
					}
					Object.Destroy((Object)(object)val);
				}
			}
			catch
			{
				failed = true;
			}
		}
		if (!(loadedUrl == url))
		{
			return null;
		}
		return texture;
	}

	private static string Normalize(string? url)
	{
		url = (url ?? "").Trim();
		if (url.Length == 0)
		{
			return "";
		}
		if (url.StartsWith("//", StringComparison.Ordinal))
		{
			url = "https:" + url;
		}
		if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
		{
			url = "https://" + url.Substring(7);
		}
		return url;
	}

	private static async Task<byte[]> Download(string url)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
		request.Headers.TryAddWithoutValidation("Referer", "https://music.163.com/");
		using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsByteArrayAsync();
	}

	internal static void Reset()
	{
		requested = "";
		loadedUrl = "";
		failed = false;
		download = null;
		if ((Object)(object)texture != (Object)null)
		{
			Object.Destroy((Object)(object)texture);
			texture = null;
		}
	}
}
