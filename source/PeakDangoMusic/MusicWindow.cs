using UnityEngine;
using UnityEngine.SceneManagement;

namespace PeakDangoMusic;

/// <summary>Optional standalone configuration UI; no dependence on the all-in-one menu.</summary>
internal sealed class MusicWindow : IDisposable
{
    internal bool Open {get;private set;}
    Rect rect;
    bool centered;
    Font? font;
    GUISkin? skin;
    GUIStyle? heading,subtitle;
    readonly List<Texture2D> textures=new();
    CursorLockMode previousLock;
    bool previousVisible;
    float previousTimeScale;
    int suppressInputUntil=-1;
    internal bool BlocksPlayerInput=>Open || Time.frameCount<=suppressInputUntil;
    internal bool Pausing=>Open && SceneManager.GetActiveScene().name!="Title" && Character.localCharacter!=null && Camera.main!=null;
    internal void Toggle()
    {
        if(Open){Close();return;}
        previousLock=Cursor.lockState;previousVisible=Cursor.visible;previousTimeScale=Time.timeScale;Open=true;KeepCursor();
    }
    internal void Close()
    {
        if(!Open)return;
        Open=false;Time.timeScale=previousTimeScale;Cursor.lockState=previousLock;Cursor.visible=previousVisible;
        suppressInputUntil=Time.frameCount+1;
        Plugin.Instance?.Config.Save();
    }
    internal void KeepCursor(){if(Open){if(Pausing)Time.timeScale=0;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
    internal void Draw()
    {
        if(!Open)return;
        var original=GUI.skin;
        var previousMatrix=GUI.matrix;var previousColor=GUI.color;var previousContentColor=GUI.contentColor;var previousBackgroundColor=GUI.backgroundColor;int previousDepth=GUI.depth;
        if(skin==null)
        {
            font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","SimHei","Arial"},20);
            skin=UnityEngine.Object.Instantiate(original);skin.font=font;
            foreach(var style in new[]{skin.label,skin.button,skin.toggle,skin.textField,skin.window,skin.box})
            {
                style.font=font;style.fontSize=18;
                foreach(var state in new[]{style.normal,style.hover,style.active,style.focused,style.onNormal,style.onHover,style.onActive,style.onFocused})state.textColor=Color.white;
            }
            skin.window.normal.background=Background(new Color(.045f,.065f,.09f,.99f));
            skin.window.onNormal.background=skin.window.normal.background;
            skin.window.border=new RectOffset(0,0,0,0);
            skin.window.padding=new RectOffset(24,24,20,22);
            skin.box.normal.background=Background(new Color(.075f,.10f,.135f,1));
            skin.box.border=new RectOffset(0,0,0,0);skin.box.padding=new RectOffset(18,18,16,16);skin.box.margin=new RectOffset(0,0,0,0);
            skin.button.normal.background=Background(new Color(.14f,.19f,.25f,1));
            skin.button.hover.background=Background(new Color(.20f,.29f,.37f,1));
            skin.button.active.background=Background(new Color(.10f,.38f,.43f,1));
            skin.button.onNormal.background=skin.button.active.background;
            skin.button.onHover.background=Background(new Color(.13f,.45f,.49f,1));
            skin.button.onActive.background=skin.button.active.background;
            skin.button.border=new RectOffset(0,0,0,0);skin.button.padding=new RectOffset(12,12,10,10);
            skin.button.margin=new RectOffset(0,0,4,4);skin.button.fixedHeight=0;
            skin.label.wordWrap=true;skin.label.margin=new RectOffset(0,0,4,6);
            skin.toggle.padding.top=8;skin.toggle.padding.bottom=8;skin.toggle.margin=new RectOffset(0,0,4,6);
            skin.textField.padding=new RectOffset(10,10,10,10);skin.textField.margin=new RectOffset(0,0,4,4);skin.textField.fixedHeight=0;
            skin.textField.clipping=TextClipping.Clip;
            skin.horizontalSlider.normal.background=Background(new Color(.025f,.04f,.06f,1));skin.horizontalSlider.border=new RectOffset(0,0,0,0);skin.horizontalSlider.fixedHeight=10;
            skin.horizontalSliderThumb.normal.background=Background(new Color(.35f,.78f,.79f,1));skin.horizontalSliderThumb.hover.background=skin.horizontalSliderThumb.normal.background;skin.horizontalSliderThumb.active.background=skin.horizontalSliderThumb.normal.background;
            skin.horizontalSliderThumb.border=new RectOffset(0,0,0,0);skin.horizontalSliderThumb.fixedWidth=24;skin.horizontalSliderThumb.fixedHeight=24;
            skin.verticalScrollbar.fixedWidth=18;skin.verticalScrollbar.margin=new RectOffset(10,0,0,0);
            heading=new GUIStyle(skin.label){fontSize=25,fontStyle=FontStyle.Bold};
            subtitle=new GUIStyle(skin.label){fontSize=14};subtitle.normal.textColor=new Color(.72f,.82f,.88f,1);
        }
        GUI.skin=skin;
        try
        {
            float scale=Mathf.Clamp(Mathf.Min(Screen.width/1920f,Screen.height/1080f),.85f,1.5f);
            float width=Screen.width/scale,height=Screen.height/scale;
            GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));GUI.color=GUI.contentColor=GUI.backgroundColor=Color.white;GUI.depth=-1000;
            rect.width=Mathf.Min(1440,Mathf.Max(280,width-48));rect.height=Mathf.Min(940,Mathf.Max(240,height-48));
            if(!centered){rect.x=(width-rect.width)*.5f;rect.y=(height-rect.height)*.5f;centered=true;}
            rect.x=Mathf.Clamp(rect.x,0,Mathf.Max(0,width-rect.width));rect.y=Mathf.Clamp(rect.y,0,Mathf.Max(0,height-rect.height));
            rect=GUILayout.Window(0x50444d,rect,Content,"");
        }
        finally{GUI.skin=original;GUI.matrix=previousMatrix;GUI.color=previousColor;GUI.contentColor=previousContentColor;GUI.backgroundColor=previousBackgroundColor;GUI.depth=previousDepth;}
    }
    void Content(int id)
    {
        GUILayout.BeginHorizontal();GUILayout.BeginVertical();
        GUILayout.Label("DangoMusic · 漂浮歌词",heading);
        GUILayout.Label((Pausing?"本机游戏已暂停":"歌词设置")+"  ·  F8 显隐歌词  ·  设置自动保存  ·  v"+Plugin.Version,subtitle);
        GUILayout.EndVertical();
        bool close=GUILayout.Button("返回游戏  ·  F9 / Esc",GUILayout.Width(220),GUILayout.Height(48));
        GUILayout.EndHorizontal();GUILayout.Space(18);
        MusicPanel.DrawStandalone(rect.width-48);
        GUI.DragWindow(new Rect(0,0,Mathf.Max(0,rect.width-270),90));
        if(close)Close();
    }
    Texture2D Background(Color color)
    {
        var texture=new Texture2D(1,1);texture.SetPixel(0,0,color);texture.Apply();textures.Add(texture);return texture;
    }
    public void Dispose()
    {
        Close();if(skin!=null)UnityEngine.Object.Destroy(skin);if(font!=null)UnityEngine.Object.Destroy(font);foreach(var texture in textures)UnityEngine.Object.Destroy(texture);textures.Clear();skin=null;font=null;
    }
}
