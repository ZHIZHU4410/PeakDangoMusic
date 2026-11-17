using BepInEx;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PeakDangoMusic;

internal sealed class PlayerLibrary
{
    internal readonly List<GdMusicApi.Track> Favorites=new(),History=new(),Queue=new();
    readonly HashSet<string> favoriteKeys=new(StringComparer.Ordinal);
    static string FilePath=>Path.Combine(Paths.ConfigPath,"PeakDangoMusic-Library.json");
    internal static string Key(GdMusicApi.Track t)=>t.Source+"/"+t.Id;
    int version;
    bool dirty;
    float flushAt=-1;
    internal int Version=>version;
    internal PlayerLibrary()
    {
        try
        {
            if(!File.Exists(FilePath))return;
            if(new FileInfo(FilePath).Length>1024*1024)throw new InvalidDataException("曲库文件超过1MiB");
            var json=JObject.Parse(File.ReadAllText(FilePath));
            Favorites.AddRange(Read(json["favorites"]));History.AddRange(Read(json["history"]));Queue.AddRange(Read(json["queue"]));
            RebuildKeys();
        }
        catch(Exception ex){DiagnosticLog.Report("本地曲库","读取收藏与历史",ex);}
    }
    static IEnumerable<GdMusicApi.Track> Read(JToken? rows)=>rows?.ToObject<GdMusicApi.Track[]>()?.Take(200)??Enumerable.Empty<GdMusicApi.Track>();
    void RebuildKeys(){favoriteKeys.Clear();foreach(var track in Favorites)favoriteKeys.Add(Key(track));}
    // O(1) so a scrolling list never walks 200 tracks per row per frame.
    internal bool Favorite(GdMusicApi.Track t)=>favoriteKeys.Contains(Key(t));
    internal void Toggle(GdMusicApi.Track t)
    {
        string key=Key(t);
        if(favoriteKeys.Remove(key))Favorites.RemoveAll(x=>Key(x)==key);
        else
        {
            Favorites.Insert(0,t);favoriteKeys.Add(key);
            if(Favorites.Count>200)Favorites.RemoveAt(200);
        }
        Touch();
    }
    internal void Played(GdMusicApi.Track t)
    {
        string key=Key(t);History.RemoveAll(x=>Key(x)==key);History.Insert(0,t);
        if(History.Count>200)History.RemoveAt(200);
        Touch();
    }
    /// <summary>Marks the library dirty; the write is deferred and coalesced in <see cref="Flush"/>.</summary>
    internal void Touch(){version++;dirty=true;flushAt=-1;}
    // Writing on every click stalled the game thread (and therefore audio) during rapid
    // queue edits, so writes are coalesced 0.6s after the last change.
    internal void Flush(float now)
    {
        if(!dirty)return;
        if(flushAt<0){flushAt=now+.6f;return;}
        if(now<flushAt)return;
        Save();
    }
    internal void Save()
    {
        dirty=false;flushAt=-1;
        try{File.WriteAllText(FilePath,JsonConvert.SerializeObject(new{favorites=Favorites,history=History,queue=Queue.Take(200)},Formatting.Indented));}
        catch(Exception ex){DiagnosticLog.Report("本地曲库","保存收藏与历史",ex);}
    }
}
