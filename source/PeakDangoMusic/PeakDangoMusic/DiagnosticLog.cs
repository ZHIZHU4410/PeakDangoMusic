using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using DangoMusic.Diagnostics;

namespace PeakDangoMusic;

internal static class DiagnosticLog
{
	private sealed class Entry
	{
		internal string Text = "";

		internal DateTime At;

		internal int Count = 1;
	}

	private static readonly object gate = new object();

	private static readonly List<Entry> entries = new List<Entry>();

	internal static Action<string>? Write;

	internal static string Snapshot
	{
		get
		{
			lock (gate)
			{
				return string.Join("\n\n", from e in entries.AsEnumerable().Reverse()
					select e.At.ToString("HH:mm:ss") + ((e.Count > 1) ? (" · 重复 " + e.Count + " 次") : "") + "\n" + e.Text);
			}
		}
	}

	internal static string Report(string module, string step, Exception error, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
	{
		string text = FailureDetail.Describe(module, step, error, file, line);
		Report(text);
		return text;
	}

	internal static void Report(string text)
	{
		if (!text.StartsWith("错误位置：", StringComparison.Ordinal))
		{
			return;
		}
		bool flag = true;
		DateTime now = DateTime.Now;
		lock (gate)
		{
			Entry entry = entries.FirstOrDefault((Entry e) => e.Text == text);
			if (entry != null)
			{
				entry.At = now;
				entry.Count++;
				entries.Remove(entry);
				entries.Add(entry);
				flag = entry.Count % 30 == 0;
			}
			else
			{
				entries.Add(new Entry
				{
					Text = text,
					At = now
				});
				if (entries.Count > 12)
				{
					entries.RemoveAt(0);
				}
			}
		}
		if (flag)
		{
			Write?.Invoke(text);
		}
	}

	internal static void Clear()
	{
		lock (gate)
		{
			entries.Clear();
		}
	}
}
