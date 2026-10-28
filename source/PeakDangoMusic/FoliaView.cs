using DangoMusic.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace PeakDangoMusic;

/// <summary>Displays Folia's original pixels as a local world canvas or an input-free screen stage.</summary>
internal sealed class FoliaView : IDisposable
{
    readonly MusicSettings settings;
    readonly FoliaClient client=new();
    Texture2D? texture;
    GameObject? root;
    RawImage? image;
    Canvas? canvas;
    Material? material;
    bool overlay,visible;
    int anchorIndex=-1;
    string anchorKey="";
    Vector3 anchor,viewport;
    Quaternion orientation;
    int side;
    Camera? anchorCamera;
    bool wasOverlay=true;
    string fault="";
    float retryAt;
    readonly LyricLineView lineView=new();
    internal string Status=>fault.Length>0?fault:client.Status;
    internal FoliaView(MusicSettings settings){this.settings=settings;}
    internal void Invalidate(){anchorIndex=-1;wasOverlay=true;fault="";retryAt=0;}
    internal void Restart(){client.Restart();Invalidate();}
    internal void Suspend(){visible=false;overlay=false;if(root!=null)root.SetActive(false);client.Set(null);}
    internal bool Tick(MusicState state,Camera? camera,bool menu,long now)
    {
        if(fault.Length>0&&Time.unscaledTime<retryAt)return false;
        try{return Paint(state,camera,menu,now);}
        catch(Exception ex) when(ex is UnityException or ArgumentException or InvalidOperationException)
        {
            fault="Folia显示暂不可用，已回退Dango："+ex.Message;retryAt=Time.unscaledTime+5;
            visible=overlay=false;if(root!=null)root.SetActive(false);client.Set(null);return false;
        }
    }
    bool Paint(MusicState state,Camera? camera,bool menu,long now)
    {
        fault="";
        client.Set(new FoliaClient.Request(state,settings));
        var frame=client.TakeFrame();
        if(frame!=null)
        {
            if(texture==null||texture.width!=frame.Width||texture.height!=frame.Height)
            {if(texture!=null)UnityEngine.Object.Destroy(texture);texture=new Texture2D(frame.Width,frame.Height,TextureFormat.BGRA32,false){filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};}
            try{texture.LoadRawTextureData(frame.Pixels);texture.Apply(false,false);}finally{client.ReturnFrame(frame);}
        }
        long? position=state.PositionAt(now);
        visible=settings.Enabled.Value&&position!=null&&state.Timeline.Lines.Count>0;
        if(texture==null||!client.Ready){if(root!=null)root.SetActive(false);overlay=false;return false;}
        var timeline=lineView.Get(state.Timeline,settings.SplitLines.Value);
        int index=timeline.FindIndex(Math.Clamp((position??0)+settings.Offset.Value,0,Math.Max(0,state.DurationMs)));
        if(index!=anchorIndex||state.Key!=anchorKey)
        {
            anchorIndex=index;anchorKey=state.Key;side=Math.Max(0,index)%2;
            if(camera!=null)Place(camera);
        }
        overlay=menu||camera==null||settings.FoliaPlacement.Value!=0;
        if(overlay){wasOverlay=true;if(root!=null)root.SetActive(false);return true;}
        if(wasOverlay||anchorCamera!=camera){Place(camera!);wasOverlay=false;}
        EnsureWorld();
        if(root==null||image==null||canvas==null)return false;
        if(!visible){root.SetActive(false);return true;}
        if(settings.Follow.Value)anchor=camera!.ViewportToWorldPoint(viewport);
        if(settings.Smart.Value)
        {
            var point=camera!.WorldToViewportPoint(anchor);
            if(point.z<1||point.x<.03f||point.x>.97f||point.y<.08f||point.y>.95f)Place(camera);
        }
        anchor=LyricPlacement.KeepDistance(anchor,camera!,settings);
        root.transform.position=anchor;
        root.transform.rotation=settings.FacePlayer.Value?LyricPlacement.Face(anchor,camera!):orientation;
        float width=Mathf.Clamp(settings.Distance.Value*.65f*settings.FoliaSize.Value,1.5f,18);
        root.transform.localScale=Vector3.one*(width/960);
        canvas.worldCamera=camera;
        image.texture=texture;image.color=new Color(settings.Brightness.Value,settings.Brightness.Value,settings.Brightness.Value,settings.FoliaOpacity.Value);
        material?.SetInt("unity_GUIZTestMode",settings.AllowWalls.Value&&!settings.BehindPlayer.Value?(int)CompareFunction.Always:(int)CompareFunction.LessEqual);
        bool allowed=settings.AllowWalls.Value||!Physics.Linecast(camera!.transform.position,anchor,LayerMask.GetMask("Map","Terrain"),QueryTriggerInteraction.Ignore);
        root.SetActive(allowed);return true;
    }
    void Place(Camera camera)
    {
        viewport=LyricPlacement.Candidates(camera,settings,side).First();anchor=camera.ViewportToWorldPoint(viewport);orientation=LyricPlacement.Face(anchor,camera);anchorCamera=camera;
    }
    void EnsureWorld()
    {
        if(root!=null)return;
        root=new GameObject("DangoMusic Folia stage",typeof(RectTransform),typeof(Canvas));UnityEngine.Object.DontDestroyOnLoad(root);
        canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.overrideSorting=true;canvas.sortingOrder=32760;
        var rect=(RectTransform)root.transform;rect.sizeDelta=new Vector2(960,540);
        var content=new GameObject("Folia pixels",typeof(RectTransform),typeof(CanvasRenderer),typeof(RawImage));content.transform.SetParent(root.transform,false);
        image=content.GetComponent<RawImage>();image.raycastTarget=false;
        var panel=(RectTransform)content.transform;panel.anchorMin=Vector2.zero;panel.anchorMax=Vector2.one;panel.offsetMin=panel.offsetMax=Vector2.zero;
        var shader=Shader.Find("UI/Default");if(shader!=null){material=new Material(shader);image.material=material;}
    }
    internal void Draw()
    {
        if(!overlay||!visible||texture==null||!client.Running||Event.current.type!=EventType.Repaint)return;
        var oldMatrix=GUI.matrix;var oldColor=GUI.color;int oldDepth=GUI.depth;bool oldEnabled=GUI.enabled;
        try
        {
            GUI.matrix=Matrix4x4.identity;GUI.depth=-2000;GUI.enabled=true;
            GUI.color=new Color(settings.Brightness.Value,settings.Brightness.Value,settings.Brightness.Value,settings.FoliaOpacity.Value);
            Rect rect;
            if(settings.FoliaPlacement.Value==2)rect=new Rect(0,0,Screen.width,Screen.height);
            else
            {
                float width=Mathf.Min(Screen.width*.46f,Screen.width*.36f*settings.FoliaSize.Value),height=width*9/16;
                float center=Screen.width*(side==0?.25f:.75f);
                rect=new Rect(Mathf.Clamp(center-width*.5f,0,Mathf.Max(0,Screen.width-width)),Mathf.Clamp(Screen.height*.39f-height*.5f,0,Mathf.Max(0,Screen.height-height)),width,height);
            }
            GUI.DrawTexture(rect,texture,ScaleMode.ScaleToFit,true);
        }
        finally{GUI.matrix=oldMatrix;GUI.color=oldColor;GUI.depth=oldDepth;GUI.enabled=oldEnabled;}
    }
    public void Dispose(){client.Dispose();if(root!=null)UnityEngine.Object.Destroy(root);if(material!=null)UnityEngine.Object.Destroy(material);if(texture!=null)UnityEngine.Object.Destroy(texture);}
}
