using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace DangoMusic.Diagnostics;

internal static class FailureDetail
{
	internal const string Prefix = "错误位置：";

	internal static string Describe(string module, string step, Exception error, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
	{
		Exception baseException = error.GetBaseException();
		string text = "报告于 " + Path.GetFileName(file) + ":" + line;
		StackFrame[] array = new StackTrace(baseException, fNeedFileInfo: true).GetFrames() ?? Array.Empty<StackFrame>();
		if (baseException != error)
		{
			array = array.Concat(new StackTrace(error, fNeedFileInfo: true).GetFrames() ?? Array.Empty<StackFrame>()).ToArray();
		}
		StackFrame stackFrame = array.FirstOrDefault((StackFrame f) =>
		{
			string text3 = f.GetMethod()?.DeclaringType?.Namespace;
			return text3 != null && (text3.StartsWith("PeakDangoMusic", StringComparison.Ordinal) || text3.StartsWith("DangoMusic", StringComparison.Ordinal));
		});
		MethodBase methodBase = stackFrame?.GetMethod();
		if ((object)methodBase != null)
		{
			string text2 = methodBase.DeclaringType?.Name ?? "";
			text = text2 + "." + methodBase.Name;
			Match match = Regex.Match(text2, "^<([^>]+)>");
			if (methodBase.Name == "MoveNext" && match.Success)
			{
				text = (methodBase.DeclaringType?.DeclaringType?.Name ?? text2) + "." + match.Groups[1].Value;
			}
			text = ((stackFrame.GetFileLineNumber() <= 0) ? (text + "（报告于 " + Path.GetFileName(file) + ":" + line + "）") : (text + " (" + Path.GetFileName(stackFrame.GetFileName()) + ":" + stackFrame.GetFileLineNumber() + ")"));
		}
		return "错误位置：" + module + " / " + step + "\n源码：" + text + "\n原因：" + baseException.GetType().Name + " · " + Clean(baseException.Message);
	}

	internal static string Clean(string value)
	{
		value = Regex.Replace(value ?? "", "(?i)\\b(authorization|cookie)\\s*[:=][^\\r\\n]+", "$1=<已隐藏>");
		value = Regex.Replace(value, "https?://[^\\s<>\"']+", (Match m) => (!Uri.TryCreate(m.Value, UriKind.Absolute, out Uri result)) ? "<地址>" : new UriBuilder(result)
		{
			UserName = "",
			Password = "",
			Query = "",
			Fragment = ""
		}.Uri.GetLeftPart(UriPartial.Path));
		value = Regex.Replace(value, "(?i)\\b(authorization|cookie|token|password|accesskey)\\s*[:=]\\s*[^\\s,;]+", "$1=<已隐藏>");
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
		if (folderPath.Length > 0)
		{
			value = value.Replace(folderPath, "<用户目录>");
		}
		value = value.Replace('\r', ' ').Replace('\n', ' ');
		if (value.Length <= 300)
		{
			return value;
		}
		return value.Substring(0, 300) + "…";
	}
}
