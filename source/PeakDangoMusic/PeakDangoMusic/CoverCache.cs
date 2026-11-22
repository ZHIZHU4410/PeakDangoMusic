using System;
using System.Collections.Generic;
using System.Net.Http;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class CoverCache
{
	private static readonly HttpClient http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(15.0)
	};

	private static readonly Dictionary<string, Texture2D> ready = new Dictionary<string, Texture2D>();

	private static readonly Dictionary<string, byte[]> raw = new Dictionary<string, byte[]>();

	private static readonly List<string> rawOrder = new List<string>();

	private static readonly Dictionary<string, Task<byte[]>> tasks = new Dictionary<string, Task<byte[]>>();

	private static readonly List<string> order = new List<string>();

	private static readonly Dictionary<string, float> retryAfter = new Dictionary<string, float>();

	private static readonly Dictionary<string, int> failures = new Dictionary<string, int>();

	private const int MaxVariants = 300;
	private const int MaxRaw = 96;
	private const int MaxInflight = 18;

    internal static void CollectDownloads()
    {
        if (tasks.Count == 0) return;
        foreach (var entry in new List<KeyValuePair<string, Task<byte[]>>>(tasks))
        {
            var task = entry.Value;
            if (!task.IsCompleted) continue;
            tasks.Remove(entry.Key);
            if (task.Status == TaskStatus.RanToCompletion && task.Result.Length > 0)
                StoreRaw(entry.Key, task.Result);
            else
            {
                _ = task.Exception;
                Fail(entry.Key);
            }
        }
    }

	internal static void Preload(string? url)
	{
		url = Normalize(url);
		if (url.Length == 0 || raw.ContainsKey(url) || tasks.ContainsKey(url))
		{
			return;
		}
		Kick(url);
	}

	internal static Texture2D? For(string? url, int size)
	{
		url = Normalize(url);
		if (url.Length == 0)
		{
			return null;
		}
		size = Mathf.Clamp(size, 16, 256);
		string key = url + "#" + size;
		if (ready.TryGetValue(key, out Texture2D vary) && vary != null)
		{
			return vary;
		}
		if (retryAfter.TryGetValue(url, out float until) && Time.unscaledTime < until)
		{
			return null;
		}
		byte[] data;
		if (raw.TryGetValue(url, out byte[] stored) && stored != null)
		{
			data = stored;
		}
		else
		{
			if (!tasks.TryGetValue(url, out Task<byte[]> pending))
			{
				Kick(url);
				if (!tasks.TryGetValue(url, out pending))
				{
					return null;
				}
			}
			if (!pending.IsCompleted)
			{
				return null;
			}
			tasks.Remove(url);
			if (pending.IsFaulted || pending.IsCanceled)
			{
				try
				{
					_ = pending.Exception;
				}
				catch
				{
				}
				Fail(url);
				return null;
			}
			data = pending.Result;
			if (data == null || data.Length == 0)
			{
				Fail(url);
				return null;
			}
			StoreRaw(url, data);
		}
		if (!TakeDecodeBudget()) return null;
		Texture2D texture = Decode(data, size);
		if (texture == null)
		{
			raw.Remove(url);
			rawOrder.Remove(url);
			Fail(url);
			return null;
		}
		retryAfter.Remove(url);
		failures.Remove(url);
		ready[key] = texture;
		order.Add(key);
		while (order.Count > MaxVariants)
		{
			string old = order[0];
			order.RemoveAt(0);
			if (ready.TryGetValue(old, out Texture2D value) && value != null)
			{
				Object.Destroy(value);
			}
			ready.Remove(old);
		}
		return texture;
	}

	private static void Kick(string url)
	{
		if (tasks.ContainsKey(url))
		{
			return;
		}
		if (tasks.Count >= MaxInflight)
		{
			return;
		}
		if (retryAfter.TryGetValue(url, out float until) && Time.unscaledTime < until)
		{
			return;
		}
		tasks[url] = Download(url);
	}

	private static void StoreRaw(string url, byte[] data)
	{
		if (!raw.ContainsKey(url))
		{
			rawOrder.Add(url);
		}
		raw[url] = data;
		while (raw.Count > MaxRaw && rawOrder.Count > 0)
		{
			string old = rawOrder[0];
			rawOrder.RemoveAt(0);
			if (old != url)
			{
				raw.Remove(old);
			}
		}
	}

	private static void Fail(string url)
	{
		int count = failures.TryGetValue(url, out int previous) ? Math.Min(5, previous + 1) : 1;
		failures[url] = count;
		retryAfter[url] = Time.unscaledTime + Mathf.Min(60f, 4f * (float)Math.Pow(2.0, count - 1));
	}

	private static Texture2D? Decode(byte[] data, int size)
	{
		Texture2D source = null;
		try
		{
			source = new Texture2D(2, 2, (TextureFormat)4, false);
			if (!ImageConversion.LoadImage(source, data))
			{
				Object.Destroy(source);
				return null;
			}
			Texture2D rounded = UiKit.MaskRounded(source, size, Mathf.Max(4, size / 8));
			Object.Destroy(source);
			return rounded;
		}
		catch
		{
			if (source != null)
			{
				Object.Destroy(source);
			}
			return null;
		}
	}

	private static int budgetFrame = -1;

	private static int budgetLeft = 2;

	private static bool TakeDecodeBudget()
	{
		if (budgetFrame != Time.frameCount)
		{
			budgetFrame = Time.frameCount;
			budgetLeft = 2;
		}
		if (budgetLeft <= 0)
		{
			return false;
		}
		budgetLeft--;
		return true;
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
			url += (url.Contains("?") ? "&" : "?") + "param=200y200";
		}
		return url;
	}

	private static async Task<byte[]> Download(string url)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
		request.Headers.TryAddWithoutValidation("Referer", "https://music.163.com/");
		using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        const int maxCoverBytes = 512 * 1024;
        using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, 0, buffer.Length, deadline.Token).ConfigureAwait(false)) > 0)
        {
            if (output.Length + read > maxCoverBytes) throw new IOException("封面超过 512 KiB");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
	}

	internal static void Reset()
	{
		foreach (Texture2D value in ready.Values)
		{
			if (value != null)
			{
				Object.Destroy(value);
			}
		}
		ready.Clear();
		order.Clear();
		retryAfter.Clear();
		failures.Clear();
        // Keep in-flight requests tracked so returning to the page can reuse them.

		raw.Clear();
		rawOrder.Clear();
	}
}
