using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    int suppressInputUntil=-1;
    SharedSettingsMenu? owner;
    bool standalone;
    string openedScene="";
    EventSystem? eventSystem;
    bool previousNavigation;
    bool navigationCaptured;
    GameObject? blocker;
    bool closeRequested;
    int lastDrawFrame=-1;
    bool wasFocused=true;
    float diagnosedWidth=-1,diagnosedHeight=-1;
    internal bool BlocksPlayerInput=>Open || Time.frameCount<=suppressInputUntil;
    internal bool CloseRequested=>closeRequested;
    internal void RequestClose()=>closeRequested=true;
    internal void Show(SharedSettingsMenu? source)
    {
        if(Open)return;
        owner=source;standalone=source==null;openedScene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;closeRequested=false;Open=true;suppressInputUntil=-1;MusicPanel.ResetEntrance();lastDrawFrame=Time.frameCount;wasFocused=Application.isFocused;
        SuspendNativeUI();
    }
    // Never disable the EventSystem or its input module: other mods and PEAK's own
    // navigation handlers keep working. A top-most raycast blocker consumes native
    // UI clicks, and sendNavigationEvents is paused only for this modal.
    void SuspendNativeUI()
    {
        eventSystem=EventSystem.current;
        if(eventSystem!=null && !navigationCaptured)
        {
            previousNavigation=eventSystem.sendNavigationEvents;
            navigationCaptured=true;
            eventSystem.sendNavigationEvents=false;
        }
        CreateBlocker();
    }
    void CreateBlocker()
    {
        if(blocker!=null)return;
        try
        {
            blocker=new GameObject("DangoMusic Input Blocker",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(blocker);
            var canvas=blocker.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder=32767;
            var surface=new GameObject("Surface",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
            surface.transform.SetParent(blocker.transform,false);
            var rect=(RectTransform)surface.transform;
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var image=surface.GetComponent<Image>();
            image.color=new Color(0,0,0,0.001f);
            image.raycastTarget=true;
        }
        catch(Exception ex){DiagnosticLog.Report("设置窗口","创建输入遮挡层",ex);}
    }
    void DestroyBlocker()
    {
        if(blocker!=null){UnityEngine.Object.Destroy(blocker);blocker=null;}
    }
    internal void KeepModalState()
    {
        if(!Open)return;
        if(eventSystem==null)
        {
            eventSystem=EventSystem.current;
            if(eventSystem!=null && !navigationCaptured){previousNavigation=eventSystem.sendNavigationEvents;navigationCaptured=true;}
        }
        if(eventSystem!=null)eventSystem.sendNavigationEvents=false;
        CreateBlocker();
    }
    internal void CheckOwner()
    {
        if(!Open)return;
        if((!standalone&&(owner==null||!owner.isActiveAndEnabled))||openedScene!=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name)Close();
    }
    // A modal that cannot draw for three focused seconds is not usable: release its
    // input block instead of leaving gameplay dead until the game is restarted.
    internal void CheckResponsive()
    {
        if(!Open)return;
        if(!Application.isFocused){wasFocused=false;lastDrawFrame=Time.frameCount;return;}
        if(!wasFocused){wasFocused=true;lastDrawFrame=Time.frameCount;return;}
        if(Time.frameCount-lastDrawFrame>180)
        {
            DiagnosticLog.Report("设置窗口","绘制响应监测",new TimeoutException("设置界面连续180帧未绘制，已自动关闭并恢复输入"));
            Close();
        }
    }
    internal void Close()
    {
        if(!Open)return;
        Open=false;closeRequested=false;
        try
        {
            // Time scale and cursor stay owned by PEAK's pause menu/CursorHandler.
            // Only the temporary raycast blocker and navigation pause are undone here.
            if(navigationCaptured && eventSystem!=null)eventSystem.sendNavigationEvents=previousNavigation;
            DestroyBlocker();
        }
        finally
        {
            navigationCaptured=false;eventSystem=null;owner=null;
            suppressInputUntil=Time.frameCount+1;
            GUI.FocusControl(null);
            try{Plugin.Instance?.Config.Save();}
            catch(Exception ex){DiagnosticLog.Report("设置窗口","保存配置",ex);}
            var gui=GUIManager.instance;
            Plugin.Instance?.Log("DangoMusic 设置已关闭，游戏输入已恢复。timeScale="+Time.timeScale.ToString("F2")
                +" pauseMenu="+GUIManager.InPauseMenu+" windowBlockingInput="+(gui!=null && gui.windowBlockingInput)
                +" eventSystem="+(EventSystem.current!=null)+" cursor="+Cursor.lockState);
        }
    }
    internal void Draw()
    {
        if(!Open)return;
        lastDrawFrame=Time.frameCount;
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
            skin.button.active.background=Background(new Color(.055f,.09f,.12f,1));
            skin.button.onNormal.background=Background(new Color(.10f,.38f,.43f,1));
            skin.button.onHover.background=Background(new Color(.13f,.45f,.49f,1));
            skin.button.onActive.background=skin.button.active.background;
            skin.button.border=new RectOffset(0,0,0,0);skin.button.padding=new RectOffset(12,12,10,10);
            skin.button.margin=new RectOffset(0,0,4,4);skin.button.fixedHeight=0;
            skin.label.wordWrap=true;skin.label.margin=new RectOffset(0,0,4,6);
            skin.toggle.padding.top=8;skin.toggle.padding.bottom=8;skin.toggle.margin=new RectOffset(0,0,4,6);
            skin.textField.padding=new RectOffset(10,10,10,10);skin.textField.margin=new RectOffset(0,0,4,4);skin.textField.fixedHeight=0;
            skin.textField.clipping=TextClipping.Clip;
            skin.horizontalSlider.normal.background=Background(new Color(.025f,.04f,.06f,1));skin.horizontalSlider.border=new RectOffset(0,0,0,0);skin.horizontalSlider.fixedHeight=10;
            skin.horizontalSliderThumb.normal.background=Background(new Color(.35f,.78f,.79f,1));skin.horizontalSliderThumb.hover.background=skin.horizontalSliderThumb.normal.background;skin.horizontalSliderThumb.active.background=Background(new Color(.13f,.38f,.42f,1));
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
            GUI.color=new Color(0,0,0,.55f);GUI.DrawTexture(new Rect(0,0,width,height),Texture2D.whiteTexture);GUI.color=Color.white;
            rect.width=Mathf.Min(1440,Mathf.Max(280,width-48));rect.height=Mathf.Min(940,Mathf.Max(240,height-48));
            if(!centered){rect.x=(width-rect.width)*.5f;rect.y=(height-rect.height)*.5f;centered=true;}
            rect.x=Mathf.Clamp(rect.x,0,Mathf.Max(0,width-rect.width));rect.y=Mathf.Clamp(rect.y,0,Mathf.Max(0,height-rect.height));
            // GUI.Window keeps our exact size. GUILayout.Window resized itself to its
            // content, which is what made the panel stretch the instant a label changed.
            GUI.Window(0x50444d,rect,Content,"");
            // Diagnostic: proves whether the frame itself moves on click/scroll.
            if(rect.width!=diagnosedWidth||rect.height!=diagnosedHeight)
            {
                diagnosedWidth=rect.width;diagnosedHeight=rect.height;
                Plugin.Instance?.Log("音乐窗口外框 "+rect.width.ToString("F0")+"x"+rect.height.ToString("F0")+" @ "+rect.x.ToString("F0")+","+rect.y.ToString("F0"));
            }
        }
        finally{GUI.skin=original;GUI.matrix=previousMatrix;GUI.color=previousColor;GUI.contentColor=previousContentColor;GUI.backgroundColor=previousBackgroundColor;GUI.depth=previousDepth;}
    }
    void Content(int id)
    {
        float innerWidth=Mathf.Max(200,rect.width-48);
        GUILayout.BeginArea(new Rect(24,20,innerWidth,Mathf.Max(160,rect.height-40)));
        GUILayout.BeginHorizontal();GUILayout.BeginVertical();
        GUILayout.Label("Muonium · PEAK 音乐播放器",heading);
        GUILayout.Label("原生曲库 / 多平台聚合  ·  设置自动保存  ·  v"+Plugin.Version+(MusicPanel.Feedback.Length>0?"  ·  "+MusicPanel.Feedback:""),subtitle,GUILayout.Height(24));
        GUILayout.EndVertical();
        // The support button sits next to close now: the old footer row was covered by
        // PEAK's own bottom UI, so clicks never reached it.
        GUILayout.FlexibleSpace();
        if(GUILayout.Button(new GUIContent("支持我","在浏览器中打开布洛芬的爱发电主页"),GUILayout.Width(128),GUILayout.Height(48)))
            Application.OpenURL("https://afdian.com/a/blf4410");
        GUILayout.Space(10);
        bool close=GUILayout.Button("关闭  ·  Esc",GUILayout.Width(220),GUILayout.Height(48));
        // Record the request before drawing the panel. A layout exception in the
        // panel can then no longer strand the modal in the open(input-blocking) state.
        if(close)RequestClose();
        GUILayout.EndHorizontal();GUILayout.Space(18);
        try{Plugin.Instance!.Hub.Draw(innerWidth,rect.height-40);}
        catch(Exception ex){DiagnosticLog.Report("设置窗口","绘制设置面板",ex);}
        GUILayout.EndArea();
        // Header drag strip stops before the two buttons so they stay clickable.
        GUI.DragWindow(new Rect(0,0,Mathf.Max(0,rect.width-420),90));
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
