using System;
using System.IO;
using BepInEx;
namespace PeakDangoMusic;

internal static class OwnedAudioCache
{
    internal static string Root=>Path.Combine(Paths.BepInExRootPath,"cache","PeakDangoMusic","audio");
    internal static bool Allows(Uri uri)
    {
        if(!uri.IsFile)return false;
        string path=Path.GetFullPath(uri.LocalPath),root=Path.GetFullPath(Root)+Path.DirectorySeparatorChar;
        return path.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&Path.GetExtension(path).Equals(".mp3",StringComparison.OrdinalIgnoreCase)&&File.Exists(path);
    }
}
