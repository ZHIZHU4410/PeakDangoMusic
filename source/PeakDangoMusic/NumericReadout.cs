using UnityEngine;

namespace PeakDangoMusic;

// Numeric labels use an embedded glyph atlas rather than IMGUI text generation.
// This avoids the repeated caption observed in the slider value column in game.
internal static class NumericReadout
{
    internal const string Characters="0123456789.-+ msHz";
    static Texture2D? atlas;

    internal static void Draw(Rect rect,string text)
    {
        if(Event.current.type!=EventType.Repaint || text.Length==0)return;
        if(atlas==null)
        {
            using var stream=typeof(NumericReadout).Assembly.GetManifestResourceStream("PeakDangoMusic.NumericGlyphs.png")
                ?? throw new InvalidDataException("Missing numeric glyph atlas");
            using var bytes=new MemoryStream();stream.CopyTo(bytes);
            atlas=new Texture2D(2,2,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            if(!atlas.LoadImage(bytes.ToArray(),true))throw new InvalidDataException("Invalid numeric glyph atlas");
        }
        float width=Mathf.Min(10,rect.width/text.Length),height=width*1.8f;
        float x=rect.xMax-width*text.Length,y=rect.center.y-height*.5f;
        var previous=GUI.color;
        try
        {
            if(!GUI.enabled)GUI.color=new Color(previous.r,previous.g,previous.b,previous.a*.5f);
            for(int i=0;i<text.Length;i++)
            {
                int glyph=Characters.IndexOf(text[i]);
                if(glyph<0)throw new InvalidDataException("Unsupported numeric character: "+text[i]);
                if(text[i]==' ')continue;
                GUI.DrawTextureWithTexCoords(new Rect(x+i*width,y,width,height),atlas,
                    new Rect(glyph/(float)Characters.Length,0,1f/Characters.Length,1),true);
            }
        }
        finally{GUI.color=previous;}
    }

    internal static void Dispose(){if(atlas!=null)UnityEngine.Object.Destroy(atlas);atlas=null;}
}
