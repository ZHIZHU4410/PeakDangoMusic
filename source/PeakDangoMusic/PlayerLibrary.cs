using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed class PlayerLibrary
{
    internal readonly List<GdMusicApi.Track> Favorites=new(),History=new(),Queue=new();
    static string FilePath=>Path.Combine(Paths.ConfigPath,"PeakDangoMusic-Library.json");
    internal static string Key(GdMusicApi.Track t)=>t.Source+"/"+t.Id;
    internal PlayerLibrary()
    {
        try
        {
            if(!File.Exists(FilePath))return;
            if(new FileInfo(FilePath).Length>1024*1024)throw new InvalidDataException("曲库文件超过1MiB");
            var json=JObject.Parse(File.ReadAllText(FilePath));
            Favorites.AddRange(Read(json["favorites"]));History.AddRange(Read(json["history"]));Queue.AddRange(Read(json["queue"]));
        }
        catch(Exception ex){DiagnosticLog.Report("本地曲库","读取收藏与历史",ex);}
    }
    static IEnumerable<GdMusicApi.Track> Read(JToken? rows)=>rows?.ToObject<GdMusicApi.Track[]>()?.Take(200)??Enumerable.Empty<GdMusicApi.Track>();
    internal bool Favorite(GdMusicApi.Track t)=>Favorites.Any(x=>Key(x)==Key(t));
    internal void Toggle(GdMusicApi.Track t){if(Favorite(t))Favorites.RemoveAll(x=>Key(x)==Key(t));else{Favorites.Insert(0,t);if(Favorites.Count>200)Favorites.RemoveAt(200);}Save();}
    internal void Played(GdMusicApi.Track t){History.RemoveAll(x=>Key(x)==Key(t));History.Insert(0,t);if(History.Count>200)History.RemoveAt(200);Save();}
    internal void Save()
    {
        try{File.WriteAllText(FilePath,JsonConvert.SerializeObject(new{favorites=Favorites,history=History,queue=Queue.Take(200)},Formatting.Indented));}
        catch(Exception ex){DiagnosticLog.Report("本地曲库","保存收藏与历史",ex);}
    }
}
