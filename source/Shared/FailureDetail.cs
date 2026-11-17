using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace DangoMusic.Diagnostics;

internal static class FailureDetail
{
    internal const string Prefix="错误位置：";
    internal static string Describe(string module,string step,Exception error,
        [CallerFilePath]string file="",[CallerLineNumber]int line=0)
    {
        var cause=error.GetBaseException();
        string location="报告于 "+Path.GetFileName(file)+":"+line;
        // Prefer the throwing application frame; keep the reporting site as a fallback
        // when Unity Mono cannot read the embedded symbols.
        var frames=new StackTrace(cause,true).GetFrames()??Array.Empty<StackFrame>();
        if(!ReferenceEquals(cause,error))frames=frames.Concat(new StackTrace(error,true).GetFrames()??Array.Empty<StackFrame>()).ToArray();
        var frame=frames.FirstOrDefault(f=>f.GetMethod()?.DeclaringType?.Namespace is string ns&&
            (ns.StartsWith("PeakDangoMusic",StringComparison.Ordinal)||ns.StartsWith("DangoMusic",StringComparison.Ordinal)));
        if(frame?.GetMethod() is { } method)
        {
            string name=method.DeclaringType?.Name??"";
            location=name+"."+method.Name;
            var asyncName=Regex.Match(name,@"^<([^>]+)>");
            if(method.Name=="MoveNext"&&asyncName.Success)location=(method.DeclaringType?.DeclaringType?.Name??name)+"."+asyncName.Groups[1].Value;
            if(frame.GetFileLineNumber()>0)location+=" ("+Path.GetFileName(frame.GetFileName())+":"+frame.GetFileLineNumber()+")";
            else location+="（报告于 "+Path.GetFileName(file)+":"+line+"）";
        }
        return Prefix+module+" / "+step+"\n源码："+location+"\n原因："+cause.GetType().Name+" · "+Clean(cause.Message);
    }
    internal static string Clean(string value)
    {
        value=Regex.Replace(value??"",@"(?i)\b(authorization|cookie)\s*[:=][^\r\n]+","$1=<已隐藏>");
        value=Regex.Replace(value,@"https?://[^\s<>""']+",m=>
            Uri.TryCreate(m.Value,UriKind.Absolute,out var uri)?new UriBuilder(uri){UserName="",Password="",Query="",Fragment=""}.Uri.GetLeftPart(UriPartial.Path):"<地址>");
        value=Regex.Replace(value,@"(?i)\b(authorization|cookie|token|password|accesskey)\s*[:=]\s*[^\s,;]+","$1=<已隐藏>");
        string profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if(profile.Length>0)value=value.Replace(profile,"<用户目录>");
        value=value.Replace('\r',' ').Replace('\n',' ');
        return value.Length>300?value.Substring(0,300)+"…":value;
    }
}
