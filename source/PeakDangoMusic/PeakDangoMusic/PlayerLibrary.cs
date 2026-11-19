using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed class PlayerLibrary
{
	internal readonly List<GdMusicApi.Track> Favorites = new List<GdMusicApi.Track>();

	internal readonly List<GdMusicApi.Track> History = new List<GdMusicApi.Track>();

	internal readonly List<GdMusicApi.Track> Queue = new List<GdMusicApi.Track>();

	private readonly HashSet<string> favoriteKeys = new HashSet<string>(StringComparer.Ordinal);

	private int version;

	private bool dirty;

	private float flushAt = -1f;

	private static string FilePath => Path.Combine(Paths.ConfigPath, "PeakDangoMusic-Library.json");

	internal int Version => version;

	internal static string Key(GdMusicApi.Track t)
	{
		return t.Source + "/" + t.Id;
	}

	internal PlayerLibrary()
	{
		try
		{
			if (File.Exists(FilePath))
			{
				if (new FileInfo(FilePath).Length > 1048576)
				{
					throw new InvalidDataException("曲库文件超过1MiB");
				}
				JObject val = JObject.Parse(File.ReadAllText(FilePath));
				Favorites.AddRange(Read(val["favorites"]));
				History.AddRange(Read(val["history"]));
				Queue.AddRange(Read(val["queue"]));
				RebuildKeys();
			}
		}
		catch (Exception error)
		{
			DiagnosticLog.Report("本地曲库", "读取收藏与历史", error, "PlayerLibrary.cs", 27);
		}
	}

	private static IEnumerable<GdMusicApi.Track> Read(JToken? rows)
	{
		return ((rows == null) ? null : rows.ToObject<GdMusicApi.Track[]>()?.Take(200)) ?? Enumerable.Empty<GdMusicApi.Track>();
	}

	private void RebuildKeys()
	{
		favoriteKeys.Clear();
		foreach (GdMusicApi.Track favorite in Favorites)
		{
			favoriteKeys.Add(Key(favorite));
		}
	}

	internal bool Favorite(GdMusicApi.Track t)
	{
		return favoriteKeys.Contains(Key(t));
	}

	internal void Toggle(GdMusicApi.Track t)
	{
		string key = Key(t);
		if (favoriteKeys.Remove(key))
		{
			Favorites.RemoveAll((GdMusicApi.Track x) => Key(x) == key);
		}
		else
		{
			Favorites.Insert(0, t);
			favoriteKeys.Add(key);
			if (Favorites.Count > 200)
			{
				Favorites.RemoveAt(200);
			}
		}
		Touch();
	}

	internal void Played(GdMusicApi.Track t)
	{
		string key = Key(t);
		History.RemoveAll((GdMusicApi.Track x) => Key(x) == key);
		History.Insert(0, t);
		if (History.Count > 200)
		{
			History.RemoveAt(200);
		}
		Touch();
	}

	internal void Touch()
	{
		version++;
		dirty = true;
		flushAt = -1f;
	}

	internal void Flush(float now)
	{
		if (dirty)
		{
			if (flushAt < 0f)
			{
				flushAt = now + 0.6f;
			}
			else if (!(now < flushAt))
			{
				Save();
			}
		}
	}

	internal void Save()
	{
		dirty = false;
		flushAt = -1f;
		try
		{
			File.WriteAllText(FilePath, JsonConvert.SerializeObject((object)new
			{
				favorites = Favorites,
				history = History,
				queue = Queue.Take(200)
			}, (Formatting)1));
		}
		catch (Exception error)
		{
			DiagnosticLog.Report("本地曲库", "保存收藏与历史", error, "PlayerLibrary.cs", 65);
		}
	}
}
