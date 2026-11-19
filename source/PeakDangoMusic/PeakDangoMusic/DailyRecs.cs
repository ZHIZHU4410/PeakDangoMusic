using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal static class DailyRecs
{
	internal static Task<GdMusicApi.Track[]> Load(MuoniumClient client)
	{
		return Task.Run<GdMusicApi.Track[]>(async () =>
		{
			JObject data = await client.Call("daily", "ncm").ConfigureAwait(continueOnCapturedContext: false);
			JToken token = data["tracks"] ?? data["list"] ?? data["songs"];
			if (!(token is JArray array))
			{
				return Array.Empty<GdMusicApi.Track>();
			}
			List<GdMusicApi.Track> list = new List<GdMusicApi.Track>(array.Count);
			foreach (JToken item in array)
			{
				if (!(item is JObject obj))
				{
					continue;
				}
				string text = ((string?)obj["id"]) ?? ((string?)obj["trackId"]) ?? "";
				if (text.Length == 0)
				{
					continue;
				}
				string title = ((string?)obj["title"]) ?? "";
				string artist = ((string?)obj["artist"]) ?? "";
				string album = ((string?)obj["album"]) ?? "";
				string cover = ((string?)obj["cover"]) ?? "";
				string source = ((string?)obj["source"]) ?? "ncm";
				list.Add(new GdMusicApi.Track(text, title, artist, album, "", source, cover));
			}
			return list.ToArray();
		});
	}
}
