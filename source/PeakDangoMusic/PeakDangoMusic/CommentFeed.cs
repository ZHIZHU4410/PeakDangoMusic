using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PeakDangoMusic;

internal sealed class MusicComment
{
	internal readonly string Nick;

	internal readonly string Body;

	internal readonly string Avatar;

	internal readonly int Likes;

	internal MusicComment(string nick, string body, string avatar, int likes)
	{
		Nick = nick;
		Body = body;
		Avatar = avatar;
		Likes = likes;
	}
}

internal static class CommentFeed
{
	private static readonly HttpClient http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(12.0)
	};

	private static readonly List<MusicComment> pool = new List<MusicComment>();

	private static readonly List<MusicComment> picked = new List<MusicComment>();

	private static readonly Dictionary<string, Texture2D> avatars = new Dictionary<string, Texture2D>();

	private static readonly List<string> avatarOrder = new List<string>();

	private static readonly Dictionary<string, Task<byte[]>> avatarTasks = new Dictionary<string, Task<byte[]>>();

	private static readonly HashSet<string> broken = new HashSet<string>();

	private static readonly HashSet<string> seen = new HashSet<string>();

	private static GdMusicApi.Track? targetTrack;

	private static string targetKey = "";

	private static string loadedKey = "";

	private static string note = "";

	private static Task<JObject>? fetch;

	private static float retryAt = -1f;

	private static int cachedSlot = -1;

	internal static int Count => pool.Count;

	internal static bool Ready => loadedKey.Length > 0 && loadedKey == targetKey;

	internal static bool Loading => fetch != null;

	internal static string Note => note;

	internal static void Poll(GdMusicApi.Track? track)
	{
		string text = ((track == null) ? "" : PlayerLibrary.Key(track));
		if (text != targetKey)
		{
			targetKey = text;
			targetTrack = track;
			loadedKey = "";
			note = "";
			retryAt = -1f;
			pool.Clear();
			picked.Clear();
			cachedSlot = -1;
			fetch = null;
			if (track != null)
			{
				Start(track);
			}
		}
		if (fetch != null && fetch.IsCompleted)
		{
			Task<JObject> task = fetch;
			fetch = null;
			if (task.IsFaulted || task.IsCanceled)
			{
				note = "评论没拉下来，20 秒后自动重试";
				retryAt = Time.unscaledTime + 20f;
			}
			else if (task.Result != null)
			{
				Parse(task.Result);
				loadedKey = targetKey;
				picked.Clear();
				cachedSlot = -1;
			}
		}
		if (fetch == null && loadedKey.Length == 0 && targetTrack != null && retryAt > 0f && Time.unscaledTime >= retryAt)
		{
			retryAt = -1f;
			Start(targetTrack);
		}
	}

	private static void Start(GdMusicApi.Track track)
	{
		if (track.Source != "ncm")
		{
			note = (track.Source == "qq") ? "QQ 音乐没有公开评论接口" : "这个平台的曲目没有评论数据";
			return;
		}
		if (track.Id.Length == 0)
		{
			note = "没拿到歌曲 ID";
			return;
		}
		note = "";
		fetch = Fetch(track.Id);
	}

	private static Task<JObject> Fetch(string id)
	{
		string url = "https://music.163.com/api/v1/resource/comments/R_SO_4_" + id + "?limit=100&offset=0";
		return Task.Run<JObject>(async () =>
		{
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
			request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
			request.Headers.TryAddWithoutValidation("Referer", "https://music.163.com/");
			using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(continueOnCapturedContext: false);
			response.EnsureSuccessStatusCode();
			string text = await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
			JObject? parsed = JsonConvert.DeserializeObject<JObject>(text);
			if (parsed == null)
			{
				throw new InvalidOperationException("评论数据为空");
			}
			if ((int?)parsed["code"] != 200)
			{
				throw new InvalidOperationException("评论接口返回 " + (parsed["code"]?.ToString() ?? "?"));
			}
			return parsed;
		});
	}

	private static void Parse(JObject data)
	{
		pool.Clear();
		seen.Clear();
		Collect(data["hotComments"]);
		Collect(data["comments"]);
		if (pool.Count == 0)
		{
			note = "这首歌暂时没有评论";
		}
	}

	private static void Collect(JToken? token)
	{
		if (!(token is JArray array))
		{
			return;
		}
		foreach (JToken item in array)
		{
			if (!(item is JObject obj))
			{
				continue;
			}
			string text = (((string?)obj["content"]) ?? "").Trim();
			if (text.Length == 0)
			{
				continue;
			}
			string nick = (((string?)obj["user"]?["nickname"]) ?? "").Trim();
			if (nick.Length == 0)
			{
				nick = "网易云用户";
			}
			string avatar = ((string?)obj["user"]?["avatarUrl"]) ?? "";
			int likes = ((int?)obj["likedCount"]) ?? 0;
			if (text.Length > 42)
			{
				text = text.Substring(0, 42) + "…";
			}
			if (pool.Count >= 100 || !seen.Add(nick + '|' + text))
			{
				continue;
			}
			pool.Add(new MusicComment(nick, text, avatar, likes));
		}
	}

	internal static IReadOnlyList<MusicComment> Pick(int count, int slot)
	{
		if (slot != cachedSlot || picked.Count == 0)
		{
			cachedSlot = slot;
			picked.Clear();
			int num = pool.Count;
			if (num == 0)
			{
				return picked;
			}
			int[] order = new int[num];
			for (int i = 0; i < num; i++)
			{
				order[i] = i;
			}
			System.Random random = new System.Random(slot * 1103515 + num * 7919);
			for (int j = num - 1; j > 0; j--)
			{
				int k = random.Next(j + 1);
				int num2 = order[j];
				order[j] = order[k];
				order[k] = num2;
			}
			for (int l = 0; l < count && l < num; l++)
			{
				picked.Add(pool[order[l]]);
			}
		}
		return picked;
	}

	internal static Texture2D? Avatar(string url)
	{
		if (url.Length == 0 || broken.Contains(url))
		{
			return null;
		}
		if (avatars.TryGetValue(url, out Texture2D? value))
		{
			return value;
		}
		if (!avatarTasks.TryGetValue(url, out Task<byte[]>? value2))
		{
			if (avatarTasks.Count > 60)
			{
				avatarTasks.Clear();
			}
			avatarTasks[url] = Download(url);
			return null;
		}
		if (!value2.IsCompleted)
		{
			return null;
		}
		avatarTasks.Remove(url);
		if (value2.IsFaulted || value2.IsCanceled)
		{
			broken.Add(url);
			return null;
		}
		Texture2D? texture2D = Decode(value2.Result);
		if ((UnityEngine.Object)(object)texture2D == null)
		{
			broken.Add(url);
			return null;
		}
		avatars[url] = texture2D;
		avatarOrder.Add(url);
		while (avatarOrder.Count > 26)
		{
			string key = avatarOrder[0];
			avatarOrder.RemoveAt(0);
			if (avatars.TryGetValue(key, out Texture2D? value3))
			{
				avatars.Remove(key);
				UnityEngine.Object.Destroy((UnityEngine.Object)(object)value3);
			}
		}
		return texture2D;
	}

	private static Texture2D? Decode(byte[] bytes)
	{
		Texture2D val = new Texture2D(2, 2, (TextureFormat)4, false);
		if (!ImageConversion.LoadImage(val, bytes))
		{
			UnityEngine.Object.Destroy((UnityEngine.Object)(object)val);
			return null;
		}
		Texture2D? result = UiKit.MaskCircle(val, 48);
		UnityEngine.Object.Destroy((UnityEngine.Object)(object)val);
		return result;
	}

	private static async Task<byte[]> Download(string url)
	{
		using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
		request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
		request.Headers.TryAddWithoutValidation("Referer", "https://music.163.com/");
		using HttpResponseMessage response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(continueOnCapturedContext: false);
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(continueOnCapturedContext: false);
	}
}
