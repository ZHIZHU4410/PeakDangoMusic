using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal static class QqChart
{
	private const int PageSize = 50;

	private static readonly HttpClient http = new HttpClient
	{
		Timeout = TimeSpan.FromSeconds(15.0)
	};

	private static bool started;

	internal static Task<GdMusicApi.Track[]> Load(int topId, int count)
	{
		return Task.Run(() => Fetch(topId, count));
	}

	private static async Task<GdMusicApi.Track[]> Fetch(int topId, int count)
	{
		if (!started)
		{
			started = true;
			http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120 Safari/537.36");
			http.DefaultRequestHeaders.Referrer = new Uri("https://y.qq.com/");
		}
		List<GdMusicApi.Track> tracks = new List<GdMusicApi.Track>();
		int begin = 0;
		while (tracks.Count < count && begin < 300)
		{
			string url = BuildUrl(topId, begin, PageSize);
			byte[] body = await http.GetByteArrayAsync(url).ConfigureAwait(continueOnCapturedContext: false);
			JObject root = JObject.Parse(Encoding.UTF8.GetString(body));
			JArray? list = (JArray?)(object?)root["songlist"];
			if (list == null || list.Count == 0)
			{
				break;
			}
			Append(list, tracks);
			begin += PageSize;
		}
		if (tracks.Count == 0)
		{
			throw new InvalidOperationException("QQ榜单没有返回歌曲");
		}
		if (tracks.Count > count)
		{
			tracks.RemoveRange(count, tracks.Count - count);
		}
		return tracks.ToArray();
	}

	private static string BuildUrl(int topId, int begin, int size)
	{
		return "https://c.y.qq.com/v8/fcg-bin/fcg_v8_toplist_cp.fcg?topid=" + topId.ToString()
			+ "&page=detail&type=top&song_begin=" + begin.ToString()
			+ "&song_num=" + size.ToString()
			+ "&format=json&inCharset=utf8&outCharset=utf-8&notice=0&platform=yqq.json";
	}

	private static void Append(JArray list, List<GdMusicApi.Track> tracks)
	{
		foreach (JToken item in (IEnumerable<JToken>)list)
		{
			JObject? data = (JObject?)(object?)item["data"];
			if (data == null)
			{
				continue;
			}
			string mid = (string?)data["songmid"] ?? "";
			if (mid.Length == 0)
			{
				continue;
			}
			string title = (string?)data["songname"] ?? "";
			string artist = "";
			JArray? singers = (JArray?)(object?)data["singer"];
			if (singers != null)
			{
				List<string> names = new List<string>();
				foreach (JToken singer in (IEnumerable<JToken>)singers)
				{
					string name = (string?)singer["name"] ?? "";
					if (name.Length != 0)
					{
						names.Add(name);
					}
				}
				artist = string.Join(" / ", names);
			}
			string album = (string?)data["albumname"] ?? "";
			string albumMid = (string?)data["albummid"] ?? "";
			string cover = (albumMid.Length == 0) ? "" : ("https://y.gtimg.cn/music/photo_new/T002R300x300M000" + albumMid + ".jpg");
			tracks.Add(new GdMusicApi.Track(mid, title, artist, album, "", "qq", cover));
		}
	}
}
