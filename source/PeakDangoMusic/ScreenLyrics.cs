using DangoMusic.Core;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>Non-interactive lyric layer above menus/settings, without a scene camera or character.</summary>
internal sealed class ScreenLyrics : IDisposable
{
    readonly MusicSettings settings;
    readonly LyricLineView lineView=new();
    readonly List<Glyph> glyphs=new();
    Font? font;
    GUIStyle? style,translationStyle;
    LyricsLine? line;
    LyricAnimation? animation;
    long position,end;
    int index,mode=-1,generation=-1,tokenIndex=-1,serial=-1;
    string song="";
    LyricsTimeline? source;
    LyricTokens? tokens;
    bool visible,dirty;
    int layoutWidth,layoutHeight;
    float layoutScale;
    readonly List<float> rowWidths=new();
    readonly struct Glyph
    {
        internal readonly int Index,Row;
        internal readonly string Text;
        internal readonly float X,Width;
        internal Glyph(int index,int row,string text,float x,float width)
        {Index=index;Row=row;Text=text;X=x;Width=width;}
    }
    internal ScreenLyrics(MusicSettings settings){this.settings=settings;}
    internal void Hide(){visible=false;}
    internal void Tick(MusicState state,long now)
    {
        long? clock=state.PositionAt(now);
        if(clock==null){Hide();return;}
        var timeline=lineView.Get(state.Timeline,settings.SplitLines.Value);
        position=Math.Clamp(clock.Value+settings.Offset.Value,0,state.DurationMs);
        int nextIndex=timeline.FindIndex(position);
        if(nextIndex<0){Hide();return;}
        bool changed=source!=timeline || song!=state.Key || generation!=state.LyricGeneration || index!=nextIndex || mode!=settings.Mode.Value;
        if(changed)
        {
            source=timeline;song=state.Key;generation=state.LyricGeneration;index=nextIndex;mode=settings.Mode.Value;
            line=LyricUnicode.Normalize(timeline.Lines[index]);
            end=index+1<timeline.Lines.Count?timeline.Lines[index+1].StartMs:Math.Min(state.DurationMs,line.StartMs+Math.Max(5000,line.DurationMs));
            tokens=mode==0?null:new LyricTokens(line,end,mode==2);tokenIndex=-2;serial=index;
        }
        if(position>=end || string.IsNullOrWhiteSpace(line!.Text)){Hide();return;}
        LyricsLine selected=line;
        int nextToken=-1;
        if(tokens!=null)
        {
            nextToken=tokens.FindIndex(position);if(nextToken<0){Hide();return;}
            var token=tokens.Tokens[nextToken];
            selected=new LyricsLine(token.StartMs,Math.Max(1,token.EndMs-token.StartMs),token.Text,new[]{new LyricsWord(token.StartMs,token.EndMs-token.StartMs,token.Text)},line.Translation);
        }
        if(changed || nextToken!=tokenIndex || animation==null)
        {animation=new LyricAnimation(selected,tokens==null?end:selected.StartMs+selected.DurationMs);tokenIndex=nextToken;dirty=true;}
        visible=true;
    }
    void Layout()
    {
        font??=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Malgun Gothic","Arial"},32);
        style??=new GUIStyle{richText=false,alignment=TextAnchor.UpperLeft,padding=new RectOffset(0,0,0,0),margin=new RectOffset(0,0,0,0),clipping=TextClipping.Overflow};
        style.font=font;style.fontSize=Mathf.RoundToInt(Mathf.Clamp(Screen.height/1080f*28*settings.FontSize.Value,16,84));
        style.normal.textColor=Color.white;
        translationStyle=new GUIStyle(style){fontSize=Mathf.RoundToInt(style.fontSize*.7f),alignment=TextAnchor.UpperCenter,wordWrap=true};
        glyphs.Clear();rowWidths.Clear();float x=0,maxWidth=Screen.width*.32f;int row=0;
        if(animation!=null)
        for(int i=0;i<Math.Min(animation.Graphemes.Count,180);i++)
        {
            string text=animation.Graphemes[i].Text;
            float width=Mathf.Max(style.CalcSize(new GUIContent(text)).x,style.fontSize*.12f);
            if(text.Contains('\n') || x+width>maxWidth && x>0)
            {rowWidths.Add(x);x=0;if(++row>=3)break;if(text.Contains('\n'))continue;}
            glyphs.Add(new Glyph(i,row,text,x,width));x+=width;
        }
        rowWidths.Add(x);layoutWidth=Screen.width;layoutHeight=Screen.height;layoutScale=settings.FontSize.Value;dirty=false;
    }
    internal void Draw()
    {
        if(!visible || animation==null || Event.current.type!=EventType.Repaint)return;
        if(dirty || style==null || layoutWidth!=Screen.width || layoutHeight!=Screen.height || layoutScale!=settings.FontSize.Value)Layout();
        if(style==null)return;
        var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;bool oldEnabled=GUI.enabled;
        try
        {
            GUI.matrix=Matrix4x4.identity;GUI.depth=-2000;GUI.enabled=true;
            float center=Screen.width*(serial%2==0?.22f:.78f),top=Screen.height*.61f,height=style.fontSize*1.35f;
            Color color=LyricPlacement.TextColor*Mathf.Min(settings.Brightness.Value,2);color.a=1;
            foreach(var glyph in glyphs)
            {
                var frame=animation.Sample(glyph.Index,position,Time.realtimeSinceStartup,settings.Pop.Value,settings.Wiggle.Value,settings.Frequency.Value,settings.Amount.Value);
                if(frame.Alpha<=0)continue;
                float x=center-rowWidths[glyph.Row]*.5f+glyph.X+(float)frame.XOffset;
                float y=top+glyph.Row*height+(float)frame.YOffset;
                var pivot=new Vector2(x+glyph.Width*.5f,y+style.fontSize*.5f);
                GUI.matrix=Matrix4x4.identity;
                GUIUtility.ScaleAroundPivot(new Vector2((float)frame.Scale,(float)frame.Scale),pivot);
                GUIUtility.RotateAroundPivot((float)frame.Rotation*Mathf.Rad2Deg,pivot);
                GUI.color=new Color(0,0,0,(float)frame.Alpha*.8f);
                GUI.Label(new Rect(x+2,y+2,glyph.Width+12,height),glyph.Text,style);
                if(settings.Glow.Value)
                {
                    var glow=color;glow.a=(float)frame.Alpha*Mathf.Clamp01(settings.GlowStrength.Value)*.25f;GUI.color=glow;
                    GUI.Label(new Rect(x-2,y,glyph.Width+12,height),glyph.Text,style);
                    GUI.Label(new Rect(x+2,y,glyph.Width+12,height),glyph.Text,style);
                }
                color.a=(float)frame.Alpha;GUI.color=color;
                GUI.Label(new Rect(x,y,glyph.Width+12,height),glyph.Text,style);
            }
            if(settings.Translation.Value && !string.IsNullOrWhiteSpace(line?.Translation))
            {
                GUI.matrix=Matrix4x4.identity;GUI.color=LyricPlacement.TextColor;
                GUI.Label(new Rect(center-Screen.width*.16f,top+rowWidths.Count*height+8,Screen.width*.32f,height*2),line!.Translation,translationStyle);
            }
        }
        finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;GUI.enabled=oldEnabled;}
    }
    public void Dispose(){Hide();if(font!=null)UnityEngine.Object.Destroy(font);font=null;style=null;translationStyle=null;}
}
