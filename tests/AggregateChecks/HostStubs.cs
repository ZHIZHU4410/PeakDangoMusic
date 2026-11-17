namespace PeakDangoMusic;
// Console checks use the real API client and parser without starting Unity/audio.
internal static class MusicClock{internal static long Now=>Environment.TickCount64;}
internal static class DiagnosticLog{internal static string Report(string module,string stage,Exception ex)=>module+" / "+stage+": "+ex.Message;}
