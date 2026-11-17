using DangoMusic.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>PEAK adaptation of Muonium's sidebar, discovery/library/account and persistent transport.</summary>
internal sealed class MuoniumHub : IDisposable
{
    readonly Plugin plugin;
    readonly MuoniumClient client=new();
    readonly PlayerLibrary library=new();
    Task<JObject>? pending,playback;
    string pendingOp="",pendingSource="",query="",playlistId="",message="选择平台，搜索歌曲或登录账号",accountName="未登录";
    string native="ncm";
    bool loggedIn,polling;
    float pollAt;
    int page,platform,searchPage=1,queueIndex=-1,playMode;
    readonly System.Random random=new();
    Vector2 scroll,sidebarScroll;
    GdMusicApi.Track[] rows=Array.Empty<GdMusicApi.Track>();
    GdMusicApi.Track? requested,current;
    JObject[] playlists=Array.Empty<JObject>();
    Texture2D? qr;
    GUIStyle? heading,muted,card,nav,navSelected;
    Font? titleFont;
    static readonly string[] Pages={"首页","搜索音乐","发现歌单","我的歌单","本地收藏","播放历史","播放队列","账号中心","悬浮歌词","连接与诊断"};
    internal MuoniumHub(Plugin owner){plugin=owner;plugin.Aggregate.TrackStarted+=Started;plugin.Aggregate.TrackEnded+=Ended;}
    bool Busy=>pending!=null;
    string Source=>platform==0?"ncm":platform==1?"qq":GdMusicApi.Keys[plugin.Settings.AggregatePlatform.Value];
    void Styles()
    {
        if(heading!=null)return;
        heading=new GUIStyle(GUI.skin.label){fontSize=30,fontStyle=FontStyle.Bold,wordWrap=true};
        muted=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};muted.normal.textColor=new Color(.61f,.69f,.77f);
        card=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,wordWrap=true,padding=new RectOffset(18,18,12,12),fontSize=17};
        nav=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(16,12,8,8),fontSize=17};
        navSelected=new GUIStyle(nav);navSelected.normal.background=GUI.skin.button.onNormal.background;
    }
    internal void Tick()
    {
        if(pending?.IsCompleted==true)
        {
            var task=pending;pending=null;
            try
            {
                var data=task.GetAwaiter().GetResult();
                if(data["tracks"] is JArray tracks)rows=ParseTracks(tracks);
                if(data["playlists"] is JArray lists)playlists=lists.OfType<JObject>().ToArray();
                if(data["image"]!=null)
                {
                    ClearQr();qr=new Texture2D(2,2);qr.LoadImage(Convert.FromBase64String((string)data["image"]!));qr.filterMode=FilterMode.Point;
                    native=pendingSource;polling=true;pollAt=Time.unscaledTime+2;message="请用对应平台的手机App扫描二维码并确认";
                }
                else if(data["state"]!=null)
                {
                    string state=(string)data["state"]!;
                    message=state switch{"WAITING"=>"等待扫码","SCANNED"=>"已扫码，请在手机上确认","CONFIRMED"=>"登录成功","EXPIRED"=>"二维码已过期，请重新生成",_=>"登录失败，请重新生成二维码"};
                    polling=state is "WAITING" or "SCANNED";pollAt=Time.unscaledTime+2;
                    if(state=="CONFIRMED"){ClearQr();Request("account",pendingSource);}
                }
                else if(data["loggedIn"]!=null){loggedIn=(bool)data["loggedIn"]!;accountName=loggedIn?(string?)data["name"]??"已登录":"未登录";message=loggedIn?"登录态由Windows当前用户加密保存":"当前平台尚未登录";}
                else message=(string?)data["message"]??"已加载";
                if(data["vaultWarning"]!=null)message=(string)data["vaultWarning"]!;
            }
            catch(Exception ex){polling=false;message=DiagnosticLog.Report("Muonium音乐",pendingOp,ex);}
        }
        if(playback?.IsCompleted==true)
        {
            var task=playback;playback=null;
            try
            {
                var data=task.GetAwaiter().GetResult();var track=requested!;
                var lines=(data["lines"] as JArray)?.Select(l=>new LyricsLine((long?)l["startMs"]??0,(long?)l["durationMs"]??0,(string?)l["text"]??"",
                    (l["words"] as JArray)?.Select(w=>new LyricsWord((long?)w["startMs"]??0,(long?)w["durationMs"]??0,(string?)w["text"]??"")).ToArray(),(string?)l["translation"]??""));
                string url=(string?)data["url"]??"";
                if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("http" or "https"))throw new InvalidDataException("原生平台没有返回有效播放地址");
                Activate();plugin.Aggregate.PlayResolved(new(track,url,LyricsTimeline.FromLines(lines??Enumerable.Empty<LyricsLine>()),
                    (bool?)data["trial"]==true?"平台返回试听片段，需按账号权限播放":"",(string?)data["format"]??""));
            }
            catch(Exception ex){message=DiagnosticLog.Report("Muonium音乐","原生曲库播放解析",ex);}
        }
        if(polling&&!Busy&&Time.unscaledTime>=pollAt){pollAt=Time.unscaledTime+3;Request("poll",native);}
    }
    static GdMusicApi.Track[] ParseTracks(JArray data)=>data.OfType<JObject>().Select(t=>new GdMusicApi.Track((string?)t["id"]??"",(string?)t["title"]??"",(string?)t["artist"]??"",(string?)t["album"]??"","",(string?)t["source"]??"ncm")).Where(t=>t.Id.Length>0).ToArray();
    void Request(string op,string source,JObject? fields=null)
    {
        if(Busy)return;pendingOp=op;pendingSource=source;message="正在加载…";pending=client.Call(op,source,fields);
    }
    void Activate(){plugin.Settings.Source.Value=4;plugin.Settings.PlayerEnabled.Value=true;plugin.Aggregate.Tick(true,plugin.Settings.AggregateVolume.Value);}
    void Play(GdMusicApi.Track track,bool replaceQueue=false)
    {
        if(playback!=null||plugin.Aggregate.Busy){message="正在加载音频，请稍候";return;}
        Activate();requested=track;
        if(replaceQueue){library.Queue.Clear();library.Queue.AddRange(rows.Length>0?rows:new[]{track});queueIndex=library.Queue.FindIndex(t=>PlayerLibrary.Key(t)==PlayerLibrary.Key(track));library.Save();}
        if(track.Source is "ncm" or "qq")playback=client.Call("resolve",track.Source,new JObject{["trackId"]=track.Id,["track"]=new JObject{["id"]=track.Id,["title"]=track.Title,["artist"]=track.Artist,["album"]=track.Album}});
        else plugin.Aggregate.Play(track,plugin.Settings.AggregateBitrate.Value);
    }
    void Started(GdMusicApi.Track track){current=track;library.Played(track);queueIndex=library.Queue.FindIndex(t=>PlayerLibrary.Key(t)==PlayerLibrary.Key(track));}
    void Ended(){if(playMode==2&&current!=null)Play(current);else Next(1);}
    void Next(int direction)
    {
        if(library.Queue.Count==0)return;
        int index=playMode==1?random.Next(library.Queue.Count):queueIndex+direction;
        if(index<0||index>=library.Queue.Count){message="播放队列已结束";return;}
        queueIndex=index;Play(library.Queue[index]);
    }
    internal void Draw(float width,float height)
    {
        Styles();
        float sidebar=width<800?140:190;
        GUILayout.BeginHorizontal(GUILayout.Height(Mathf.Max(180,height-320)));
        GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width(sidebar));
        GUILayout.Label("MUONIUM",new GUIStyle(heading!){fontSize=22});GUILayout.Label("PEAK PLAYER",muted);
        sidebarScroll=GUILayout.BeginScrollView(sidebarScroll);
        for(int i=0;i<Pages.Length;i++)if(GUILayout.Button(Pages[i],page==i?navSelected!:nav!,GUILayout.Height(43))){page=i;scroll=Vector2.zero;}
        GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.Space(16);
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if(page is not (8 or 9))
        {
            GUILayout.BeginHorizontal();
            string[] sources={"网易云 · 原生","QQ · 原生","GD聚合"};
            bool sourceEnabled=GUI.enabled;GUI.enabled=sourceEnabled&&!Busy;
            for(int i=0;i<3;i++)if(GUILayout.Button(sources[i],platform==i?navSelected!:nav!,GUILayout.Height(40)))
            {platform=i;polling=false;ClearQr();rows=Array.Empty<GdMusicApi.Track>();playlists=Array.Empty<JObject>();message="已切换平台";loggedIn=false;accountName="未登录";}
            GUI.enabled=sourceEnabled;
            GUILayout.EndHorizontal();
            if(platform==2)
            {
                var s=plugin.Settings;int selected=s.AggregatePlatform.Value;
                GUILayout.BeginHorizontal();if(GUILayout.Button("‹",GUILayout.Width(40)))s.AggregatePlatform.Value=(selected+9)%10;
                GUILayout.Label(GdMusicApi.Names[s.AggregatePlatform.Value],GUILayout.Height(40));if(GUILayout.Button("›",GUILayout.Width(40)))s.AggregatePlatform.Value=(selected+1)%10;GUILayout.EndHorizontal();
            }
        }
        scroll=GUILayout.BeginScrollView(scroll);
        switch(page)
        {
            case 0:Home();break;
            case 1:Search();break;
            case 2:Discover();break;
            case 3:Library();break;
            case 4:TrackRows(library.Favorites.ToArray(),"本地收藏");break;
            case 5:TrackRows(library.History.ToArray(),"最近播放");break;
            case 6:Queue();break;
            case 7:Account();break;
            case 8:MusicPanel.DrawLyricsSettings();break;
            case 9:Diagnostics();break;
        }
        GUILayout.Space(12);GUILayout.Label(message,muted);GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();
        Transport();
    }
    bool ActionButton(string label){bool enabled=GUI.enabled;GUI.enabled=enabled&&!Busy;bool click=GUILayout.Button(label,card,GUILayout.MinHeight(46));GUI.enabled=enabled;return click;}
    void Home()
    {
        GUILayout.Space(18);GUILayout.Label("让音乐陪你继续攀登",heading);GUILayout.Label("Muonium 曲库与账号  ·  GD 多平台聚合  ·  Dango / Folia 悬浮歌词",muted);GUILayout.Space(18);
        GUILayout.BeginHorizontal();if(ActionButton("搜索喜欢的音乐  →"))page=1;if(ActionButton("发现精选歌单  →")){page=2;LoadDiscover();}GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();if(ActionButton("账号与云端曲库  →")){page=7;if(platform!=2)Request("account",Source);}if(ActionButton("我的本地收藏  →"))page=4;GUILayout.EndHorizontal();
        if(platform==0&&ActionButton("网易云每日推荐 · 登录后使用")){page=1;Request("daily","ncm");}
        if(library.History.Count>0){GUILayout.Space(20);TrackRows(library.History.Take(5).ToArray(),"继续听");}
    }
    void Search()
    {
        GUILayout.Label("搜索音乐",heading);query=GUILayout.TextField(query,200,GUILayout.MinHeight(44));
        if(ActionButton("搜索")){searchPage=1;DoSearch();}
        GUILayout.BeginHorizontal();if(ActionButton("上一页")&&searchPage>1){searchPage--;DoSearch();}if(ActionButton("下一页")){searchPage++;DoSearch();}GUILayout.EndHorizontal();
        TrackRows(platform==2?plugin.Aggregate.Results:rows,"搜索结果 · 第 "+searchPage+" 页");
    }
    void DoSearch(){if(query.Trim().Length==0){message="请输入歌名或歌手";return;}if(platform==2){Activate();plugin.Aggregate.Search(Source,query,searchPage);}else Request("search",Source,new JObject{["query"]=query,["page"]=searchPage});}
    void LoadDiscover(){if(platform==2){message="GD聚合提供歌曲搜索；精选歌单请使用原生平台";return;}Request("discover",Source);}
    void Discover(){GUILayout.Label("发现歌单",heading);if(ActionButton("刷新推荐歌单"))LoadDiscover();PlaylistRows();}
    void Library()
    {
        GUILayout.Label("我的歌单",heading);
        if(platform!=2&&ActionButton("加载账号歌单"))Request("library",Source);
        GUILayout.Label("通过平台歌单ID打开",muted);playlistId=GUILayout.TextField(playlistId,24,GUILayout.MinHeight(42));
        if(platform!=2&&ActionButton("打开歌单"))Request("playlist",Source,new JObject{["playlistId"]=playlistId});PlaylistRows();TrackRows(rows,"歌单歌曲");
    }
    void PlaylistRows()
    {
        foreach(var list in playlists)
        {
            string id=(string?)list["id"]??"",name=(string?)list["name"]??"歌单";int count=(int?)list["trackCount"]??0;
            if(ActionButton(name+(count>0?" · "+count+" 首":""))){playlistId=id;page=3;rows=Array.Empty<GdMusicApi.Track>();Request("playlist",Source,new JObject{["playlistId"]=id});}
        }
    }
    void TrackRows(GdMusicApi.Track[] tracks,string title)
    {
        GUILayout.Label(title,new GUIStyle(heading!){fontSize=20});
        if(tracks.Length==0)GUILayout.Label("还没有歌曲，搜索或加载歌单开始聆听",muted);
        foreach(var track in tracks.Take(200))
        {
            GUILayout.BeginHorizontal();
            if(GUILayout.Button(track.Title+"\n"+track.Artist+"  ·  "+track.Album,card!,GUILayout.MinHeight(60))){rows=tracks;Play(track,true);}
            if(GUILayout.Button(library.Favorite(track)?"已收藏":"收藏",GUILayout.Width(78),GUILayout.Height(60)))library.Toggle(track);
            if(GUILayout.Button("加入",GUILayout.Width(60),GUILayout.Height(60))){if(library.Queue.Count<200){library.Queue.Add(track);library.Save();message="已加入播放队列";}}
            GUILayout.EndHorizontal();
        }
    }
    void Queue(){GUILayout.Label("播放队列",heading);if(ActionButton("清空队列")){library.Queue.Clear();queueIndex=-1;library.Save();}TrackRows(library.Queue.ToArray(),"队列曲目");}
    void Account()
    {
        GUILayout.Label("账号中心",heading);
        if(platform==2){GUILayout.Label("GD聚合不需要登录。网易云/QQ账号分别在原生平台管理。",muted);return;}
        GUILayout.Label(accountName,new GUIStyle(heading!){fontSize=22});
        GUILayout.Label("使用对应平台手机App扫码，凭证由Windows当前用户加密保存。退出登录会清除本模组的对应平台登录态。",muted);
        GUILayout.BeginHorizontal();if(ActionButton("生成登录二维码")){polling=false;ClearQr();Request("qr",Source);}if(ActionButton("刷新账号状态"))Request("account",Source);if(ActionButton("退出此账号")){polling=false;ClearQr();Request("logout",Source);}GUILayout.EndHorizontal();
        if(qr!=null){var rect=GUILayoutUtility.GetRect(280,280,GUILayout.Width(280),GUILayout.Height(280));GUI.DrawTexture(rect,qr,ScaleMode.ScaleToFit);}
    }
    void Diagnostics()
    {
        GUILayout.Label("连接与诊断",heading);
        GUILayout.Label("当前只运行Muonium原生曲库与GD聚合，不启动旧桌面播放器接口。音乐开关与歌词显示开关互相独立。",muted);
        plugin.Settings.PlayerEnabled.Value=GUILayout.Toggle(plugin.Settings.PlayerEnabled.Value,"启用游戏内播放器");
        string errors=DiagnosticLog.Snapshot;
        GUILayout.Label(errors.Length>0?errors:"当前没有最近错误",muted);
        if(ActionButton("复制错误详情"))GUIUtility.systemCopyBuffer=errors;
    }
    void Transport()
    {
        GUILayout.Space(12);GUILayout.BeginVertical(GUI.skin.box);
        var state=plugin.Aggregate.Snapshot;
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(220));GUILayout.Label(current?.Title??"选择一首音乐");GUILayout.Label(current?.Artist??"PEAK · Muonium",muted);GUILayout.EndVertical();
        if(GUILayout.Button("上一首",GUILayout.Width(85),GUILayout.Height(42)))Next(-1);
        if(GUILayout.Button(state.Playing?"暂停":"播放",GUILayout.Width(85),GUILayout.Height(42)))plugin.Aggregate.TogglePause();
        if(GUILayout.Button("下一首",GUILayout.Width(85),GUILayout.Height(42)))Next(1);
        if(GUILayout.Button(new[]{"顺序播放","随机播放","单曲循环"}[playMode],GUILayout.Width(110),GUILayout.Height(42)))playMode=(playMode+1)%3;
        if(GUILayout.Button("停止",GUILayout.Width(65),GUILayout.Height(42)))plugin.Aggregate.Stop();
        GUILayout.FlexibleSpace();GUILayout.Label("音量",GUILayout.Width(44));plugin.Settings.AggregateVolume.Value=GUILayout.HorizontalSlider(plugin.Settings.AggregateVolume.Value,0,1,GUILayout.Width(110));
        GUILayout.EndHorizontal();
        if(state.DurationMs>0){GUILayout.BeginHorizontal();GUILayout.Label(Clock(state.PositionMs),GUILayout.Width(60));float fraction=(float)state.PositionMs/state.DurationMs;float next=GUILayout.HorizontalSlider(fraction,0,1);if(Math.Abs(next-fraction)>.001f)plugin.Aggregate.Seek(next);GUILayout.Label(Clock(state.DurationMs),GUILayout.Width(60));GUILayout.EndHorizontal();}
        GUILayout.Label(playback!=null?"正在从原生曲库解析音频…":plugin.Aggregate.Status,muted);GUILayout.EndVertical();
    }
    static string Clock(long ms)=>TimeSpan.FromMilliseconds(ms).ToString(@"mm\:ss");
    void ClearQr(){if(qr!=null)UnityEngine.Object.Destroy(qr);qr=null;}
    internal void DrawTitleEntry(bool open)
    {
        if(open||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Title")return;
        titleFont??=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},19);
        if(GUI.Button(new Rect(Screen.width-260,Screen.height-76,230,48),"♫ 音乐播放器",new GUIStyle(GUI.skin.button){font=titleFont,fontSize=19}))plugin.OpenPlayer();
    }
    public void Dispose(){client.Dispose();ClearQr();if(titleFont!=null)UnityEngine.Object.Destroy(titleFont);plugin.Aggregate.TrackStarted-=Started;plugin.Aggregate.TrackEnded-=Ended;}
}
