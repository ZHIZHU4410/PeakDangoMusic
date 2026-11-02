using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace PeakDangoMusic;

internal sealed class FontLibrary
{
    internal sealed class Choice
    {
        internal string Path="",Label="";
    }
    internal readonly List<Choice> Choices=new();
    internal string Status="";
    internal FontLibrary(string folder){Refresh(folder);}
    internal void Refresh(string folder)
    {
        Choices.Clear();Choices.Add(new Choice{Label="系统默认字体"});
        try
        {
            if(!Directory.Exists(folder)){Status="字体目录不存在，可修改目录后刷新。";return;}
            foreach(string path in Directory.EnumerateFiles(folder,"*",SearchOption.AllDirectories)
                .Where(p=>new[]{".ttf",".otf",".ttc"}.Contains(System.IO.Path.GetExtension(p).ToLowerInvariant()))
                .OrderBy(p=>System.IO.Path.GetFileName(p),StringComparer.OrdinalIgnoreCase).Take(2000))
                Choices.Add(new Choice{Path=path,Label=System.IO.Path.GetFileNameWithoutExtension(path)});
            Status="已找到 "+(Choices.Count-1)+" 个本地字体";
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException){Status="字体扫描失败："+e.Message;}
    }
    internal static bool IsFont(string path)=>File.Exists(path)&&new[]{".ttf",".otf",".ttc"}.Contains(System.IO.Path.GetExtension(path).ToLowerInvariant());
    internal static TMP_FontAsset Load(string path)
    {
        var asset=TMP_FontAsset.CreateFontAsset(path,0,64,8,GlyphRenderMode.SDFAA,1024,1024);
        if(asset==null)throw new InvalidOperationException("无法读取所选字体");
        asset.isMultiAtlasTexturesEnabled=true;return asset;
    }
    internal sealed class Asset : IDisposable
    {
        internal TMP_FontAsset Main=null!;
        readonly List<TMP_FontAsset> owned=new();
        internal Asset(string selected)
        {
            try
            {
                if(!IsFont(selected))throw new IOException("所选字体文件不存在");
                Main=Load(selected);owned.Add(Main);
                if(Main.material==null||!Main.material.shader.isSupported)throw new InvalidOperationException("字体材质不可用");
                Main.fallbackFontAssetTable??=new List<TMP_FontAsset>();
                foreach(string name in new[]{"msyh.ttc","malgun.ttf","arial.ttf"})
                {
                    string path=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),name);
                    if(!File.Exists(path))continue;
                    try{var fallback=Load(path);owned.Add(fallback);Main.fallbackFontAssetTable.Add(fallback);}catch(Exception){}
                }
            }
            catch{Dispose();throw;}
        }
        public void Dispose()
        {
            foreach(var asset in owned){foreach(var texture in asset.atlasTextures)if(texture!=null)UnityEngine.Object.Destroy(texture);if(asset.material!=null)UnityEngine.Object.Destroy(asset.material);UnityEngine.Object.Destroy(asset);}
            owned.Clear();
        }
    }
}
