using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DangoMusic.Core;

namespace DangoMusic.Desktop;

public static class LxLyricParser
{
	private static readonly Regex LineTags = new Regex("^(?:\\[[\\d:.]+\\])+", RegexOptions.CultureInvariant);

	private static readonly Regex TagValue = new Regex("\\[([\\d:.]+)\\]", RegexOptions.CultureInvariant);

	private static readonly Regex OffsetTag = new Regex("\\[offset:([+-]?\\d{1,10})\\]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

	private static readonly Regex WordMarker = new Regex("<(-?\\d{1,9}),(-?\\d{1,9})>", RegexOptions.CultureInvariant);

	private static readonly Regex Markers = new Regex("<-?\\d{1,9},-?\\d{1,9}>", RegexOptions.CultureInvariant);

	public static LyricsTimeline Parse(string? lyric, string? lxlyric = null, string? translation = null)
	{
		bool flag = !string.IsNullOrWhiteSpace(lxlyric);
		string? text = (flag ? lxlyric : lyric);
		long offset = GetOffset(text);
		List<LyricsLine> list = ParseTimed(text, offset);
		if (((list.Count == 0) & flag) && !string.IsNullOrWhiteSpace(lyric))
		{
			offset = GetOffset(lyric);
			list = ParseTimed(lyric, offset);
		}
		if (list.Count == 0)
		{
			return LyricsTimeline.Empty;
		}
		Dictionary<long, string> dictionary = ParseTranslation(translation, offset);
		return LyricsTimeline.FromLines(list, (dictionary.Count == 0) ? null : ToLrc(dictionary));
	}

	internal static long GetOffset(string? text)
	{
		if (string.IsNullOrEmpty(text))
		{
			return 0L;
		}
		long result = 0L;
		foreach (Match item in OffsetTag.Matches(text))
		{
			if (long.TryParse(item.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result2))
			{
				result = result2;
			}
		}
		return result;
	}

	internal static bool TryParseTime(string value, out long milliseconds)
	{
		milliseconds = 0L;
		value = value.Trim();
		if (value.Length == 0)
		{
			return false;
		}
		int num = value.IndexOf('.');
		string text = ((num >= 0) ? value.Substring(0, num) : value);
		string text2 = ((num >= 0) ? value.Substring(num + 1) : "");
		if (text2.Length > 3 || !AllDigits(text2))
		{
			return false;
		}
		string[] array = text.Split(':');
		int num2 = array.Length;
		if ((num2 < 1 || num2 > 3) ? true : false)
		{
			return false;
		}
		long[] array2 = new long[array.Length];
		for (int i = 0; i < array.Length; i++)
		{
			num2 = array[i].Length;
			bool flag = ((num2 < 1 || num2 > 3) ? true : false);
			if (flag || !AllDigits(array[i]))
			{
				return false;
			}
			array2[i] = long.Parse(array[i], NumberStyles.None, CultureInfo.InvariantCulture);
		}
		long num3 = array2[^1];
		long num4 = ((array.Length >= 2) ? array2[^2] : 0);
		long num5 = ((array.Length == 3) ? array2[0] : 0);
		long num6 = ((text2.Length == 0) ? 0 : long.Parse(text2, NumberStyles.None, CultureInfo.InvariantCulture));
		milliseconds = num5 * 3600000 + num4 * 60000 + num3 * 1000 + num6;
		return true;
	}

	private static List<LyricsLine> ParseTimed(string? text, long offset)
	{
		List<LyricsLine> list = new List<LyricsLine>();
		if (string.IsNullOrWhiteSpace(text))
		{
			return list;
		}
		string[] array = text.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			if (text2.Length == 0)
			{
				continue;
			}
			Match match = LineTags.Match(text2);
			if (!match.Success)
			{
				continue;
			}
			List<long> list2 = new List<long>(2);
			foreach (Match item in TagValue.Matches(match.Value))
			{
				if (TryParseTime(item.Groups[1].Value, out var milliseconds))
				{
					list2.Add(milliseconds);
				}
			}
			if (list2.Count == 0)
			{
				continue;
			}
			string text3 = text2.Substring(match.Length).Trim();
			if (text3.Length == 0 || text3 == "//")
			{
				continue;
			}
			LyricsWord[] array2 = ParseWords(text3);
			string text4 = ((array2 == null) ? Markers.Replace(text3, "").Trim() : string.Concat(array2.Select((LyricsWord word) => word.Text)));
			if (text4.Length == 0)
			{
				continue;
			}
			foreach (long item2 in list2)
			{
				long start = Math.Max(0L, item2 + offset);
				if (array2 == null)
				{
					list.Add(new LyricsLine(start, 0L, text4));
					continue;
				}
				LyricsWord[] words = array2.Select((LyricsWord word) => new LyricsWord(Math.Max(0L, start + word.StartMs), word.DurationMs, word.Text)).ToArray();
				list.Add(new LyricsLine(start, 0L, text4, words));
			}
		}
		return list;
	}

	private static LyricsWord[]? ParseWords(string body)
	{
		MatchCollection matchCollection = WordMarker.Matches(body);
		if (matchCollection.Count == 0)
		{
			return null;
		}
		if (!string.IsNullOrWhiteSpace(Markers.Replace(body.Substring(0, matchCollection[0].Index), "")))
		{
			return null;
		}
		List<LyricsWord> list = new List<LyricsWord>(matchCollection.Count);
		long num = long.MinValue;
		for (int i = 0; i < matchCollection.Count; i++)
		{
			Match match = matchCollection[i];
			if (!long.TryParse(match.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var result) || !long.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var result2))
			{
				return null;
			}
			if (result < num || result2 < 0)
			{
				return null;
			}
			num = result;
			int num2 = match.Index + match.Length;
			int num3 = ((i + 1 < matchCollection.Count) ? matchCollection[i + 1].Index : body.Length);
			long startMs = result;
			long durationMs = result2;
			int num4 = num2;
			list.Add(new LyricsWord(startMs, durationMs, body.Substring(num4, num3 - num4)));
		}
		return list.ToArray();
	}

	private static Dictionary<long, string> ParseTranslation(string? text, long offset)
	{
		Dictionary<long, string> dictionary = new Dictionary<long, string>();
		if (string.IsNullOrWhiteSpace(text))
		{
			return dictionary;
		}
		string[] array = text.Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text2 = array[i].Trim();
			if (text2.Length == 0)
			{
				continue;
			}
			Match match = LineTags.Match(text2);
			if (!match.Success)
			{
				continue;
			}
			List<long> list = new List<long>(2);
			foreach (Match item in TagValue.Matches(match.Value))
			{
				if (TryParseTime(item.Groups[1].Value, out var milliseconds))
				{
					list.Add(milliseconds);
				}
			}
			if (list.Count == 0)
			{
				continue;
			}
			string text3 = Markers.Replace(text2.Substring(match.Length), "").Trim();
			if (text3.Length == 0 || text3 == "//")
			{
				continue;
			}
			foreach (long item2 in list)
			{
				long key = Math.Max(0L, item2 + offset);
				dictionary[key] = (dictionary.TryGetValue(key, out var value) ? (value + " / " + text3) : text3);
			}
		}
		return dictionary;
	}

	private static string ToLrc(Dictionary<long, string> translated)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (KeyValuePair<long, string> item in translated.OrderBy((KeyValuePair<long, string> pair) => pair.Key))
		{
			item.Deconstruct(out var key, out var value);
			long num = key;
			string value2 = value;
			StringBuilder stringBuilder2 = stringBuilder.Append('[').Append((num / 60000).ToString("00", CultureInfo.InvariantCulture)).Append(':')
				.Append((num / 1000 % 60).ToString("00", CultureInfo.InvariantCulture))
				.Append('.');
			key = num % 1000;
			stringBuilder2.Append(key.ToString("000", CultureInfo.InvariantCulture)).Append(']').Append(value2)
				.Append('\n');
		}
		return stringBuilder.ToString();
	}

	private static bool AllDigits(string value)
	{
		foreach (char c in value)
		{
			if ((c < '0' || c > '9') ? true : false)
			{
				return false;
			}
		}
		return value.Length > 0;
	}
}
