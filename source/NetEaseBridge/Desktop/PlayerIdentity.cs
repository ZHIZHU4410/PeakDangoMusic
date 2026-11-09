namespace DangoMusic.Desktop;
public static class PlayerIdentity
{
    public static bool Matches(string source,string id)=>source switch
    {
        "kugou"=>id.Equals("KuGou.exe",StringComparison.OrdinalIgnoreCase)||id.Contains("KuGou",StringComparison.OrdinalIgnoreCase),
        "qqmusic"=>id.Equals("QQMusic.exe",StringComparison.OrdinalIgnoreCase)||id.Contains("QQMusic",StringComparison.OrdinalIgnoreCase)||id.StartsWith("Tencent.QQMusic",StringComparison.OrdinalIgnoreCase),
        "netease"=>id.Equals("cloudmusic.exe",StringComparison.OrdinalIgnoreCase)||id.Contains("NetEase.CloudMusic",StringComparison.OrdinalIgnoreCase)||id.StartsWith("CloudMusic",StringComparison.OrdinalIgnoreCase),
        _=>false
    };
    public static string Label(string source)=>source switch{"kugou"=>"酷狗音乐","qqmusic"=>"QQ音乐",_=>"原版网易云音乐"};
}
