using System.Runtime.CompilerServices;
using DangoMusic.Diagnostics;

namespace PeakDangoMusic;

internal static class DiagnosticLog
{
    sealed class Entry{internal string Text="";internal DateTime At;internal int Count=1;}
    static readonly object gate=new();
    static readonly List<Entry> entries=new();
    internal static Action<string>? Write;
    internal static string Report(string module,string step,Exception error,
        [CallerFilePath]string file="",[CallerLineNumber]int line=0)
    {
        string text=FailureDetail.Describe(module,step,error,file,line);Report(text);return text;
    }
    internal static void Report(string text)
    {
        if(!text.StartsWith(FailureDetail.Prefix,StringComparison.Ordinal))return;
        bool log=true;DateTime now=DateTime.Now;
        lock(gate)
        {
            var previous=entries.FirstOrDefault(e=>e.Text==text);
            if(previous!=null){previous.At=now;previous.Count++;entries.Remove(previous);entries.Add(previous);log=previous.Count%30==0;}
            else{entries.Add(new Entry{Text=text,At=now});if(entries.Count>12)entries.RemoveAt(0);}
        }
        if(log)Write?.Invoke(text);
    }
    internal static string Snapshot
    {
        get{lock(gate)return string.Join("\n\n",entries.AsEnumerable().Reverse().Select(e=>e.At.ToString("HH:mm:ss")+(e.Count>1?" · 重复 "+e.Count+" 次":"")+"\n"+e.Text));}
    }
    internal static void Clear(){lock(gate)entries.Clear();}
}
