using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class CoverCache
{
	private static readonly HttpClient http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(12.0)
	};

	private static readonly Dictionary<string, Texture2D> ready = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, Task<byte[]>> tasks = new Dictionary<string, Task<byte[]>>();

	private static readonly List<string> order = new List<string>();

	private const int MaxEntries = 160;
    private static readonly Dictionary<string,float> retryAfter = new Dictionary<string,float>();

	internal static Texture2D? For(string? url, int size)
	{
		url = Normalize(url);
		if (url.Length == 0)
		{
			return null;
		}
		string text = url + "#" + size;
		if (ready.TryGetValue(text, out Texture2D value))
		{
			return value;
		}
		if (retryAfter.TryGetValue(text, out float retry) && Time.unscaledTime < retry) return null;
		if (!tasks.ContainsKey(text))
		{
            if(tasks.Count >= 12)return null;
			tasks[text] = Download(url);
		}
		Task<byte[]> task = tasks[text];
		if (!task.IsCompleted)
		{
			return null;
		}
		tasks.Remove(text);
		if (task.IsFaulted || task.IsCanceled)
		{
            _ = task.Exception;retryAfter[text]=Time.unscaledTime+30;
			return null;
		}
		try
		{
			Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false);
			if (!ImageConversion.LoadImage(val, task.Result))
			{
				Object.Destroy((Object)(object)val);
				return null;
			}
			Texture2D val2 = UiKit.MaskRounded(val, size, Mathf.Max(4, size / 8));
			Object.Destroy((Object)(object)val);
			ready[text] = val2;
			order.Add(text);
			while (order.Count > 160)
			{
				string key = order[0];
				order.RemoveAt(0);
				if (ready.TryGetValue(key, out Texture2D value2))
				{
					if ((Object)(object)value2 != (Object)null)
					{
						Object.Destroy((Object)(object)value2);
					}
					ready.Remove(key);
				}
			}
			return val2;
		}
		catch
		{
			return null;
		}
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
		if (url.Contains("music.126.net") && !url.Contains("param="))
		{
			url += "?param=200y200";
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
		foreach (Texture2D value in ready.Values)
		{
			if ((Object)(object)value != (Object)null)
			{
				Object.Destroy((Object)(object)value);
			}
		}
		ready.Clear();retryAfter.Clear();
		tasks.Clear();
		order.Clear();
	}
}
