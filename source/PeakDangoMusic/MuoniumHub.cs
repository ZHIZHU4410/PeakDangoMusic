using System.Globalization;
using DangoMusic.Core;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PeakDangoMusic;

/// <summary>PEAK adaptation of Muonium's sidebar, discovery/library/account and persistent transport.</summary>
internal sealed class MuoniumHub : IDisposable
{
    // Fixed metrics: every list row and every toolbar button keeps one size, so a click
    // can never re-wrap text and stretch the surrounding layout.
    const float SidebarWide=190, SidebarNarrow=140;
    const float NavHeight=42, NavGap=6, PlatformHeight=40, PlatformGap=8;
    const float TrackRowHeight=68, TrackRowGap=8, TrackActionWidth=86, TrackAddWidth=64, ColumnGap=8;
    const float RowButtonHeight=46, PlaylistHeight=52;
    const int TrackPageSize=60, PlaylistPageSize=60;

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
    GUIStyle? heading,muted,messageStyle,card,cardOn,nav,navSelected,section,bigHeading,titleEntry,trackTitle;
    Font? titleFont;

    // Per-frame allocation caches.
    readonly List<string> trackLabels=new();
    readonly struct PlaylistRow
    {
        internal readonly string Id,Name;internal readonly int Count;
        internal PlaylistRow(string id,string name,int count){Id=id;Name=name;Count=count;}
    }
    readonly List<PlaylistRow> playlistRows=new();
    GdMusicApi.Track[] cachedTracks=Array.Empty<GdMusicApi.Track>();
    int cachedCount=-1;
    float cachedLabelWidth=-1;
    int cachedLibraryVersion=-1;
    // Library views are rebuilt only when the library changes, not once per draw.
    GdMusicApi.Track[] favoritesView=Array.Empty<GdMusicApi.Track>(),historyView=Array.Empty<GdMusicApi.Track>(),queueView=Array.Empty<GdMusicApi.Track>(),recentView=Array.Empty<GdMusicApi.Track>();
    int viewVersion=-1;
    long cachedPositionSecond=-1,cachedDurationSecond=-1;
    string cachedPositionText="00:00",cachedDurationText="00:00";

    static readonly string[] Pages={"首页","搜索音乐","发现歌单","我的歌单","本地收藏","播放历史","播放队列","账号中心","悬浮歌词","连接与诊断"};
    internal MuoniumHub(Plugin owner){plugin=owner;plugin.Aggregate.TrackStarted+=Started;plugin.Aggregate.TrackEnded+=Ended;}
    bool Busy=>pending!=null;
    string Source=>platform==0?"ncm":platform==1?"qq":GdMusicApi.Keys[plugin.Settings.AggregatePlatform.Value];
    void Styles()
    {
        if(heading!=null)return;
        heading=new GUIStyle(GUI.skin.label){fontSize=30,fontStyle=FontStyle.Bold,wordWrap=true};
        bigHeading=new GUIStyle(heading){fontSize=22,wordWrap=false,clipping=TextClipping.Clip};
        section=new GUIStyle(heading){fontSize=20};
        muted=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true};muted.normal.textColor=new Color(.61f,.69f,.77f);
        // Single-line status text: fixed height plus clip means the footer never grows.
        messageStyle=new GUIStyle(muted){wordWrap=false,clipping=TextClipping.Clip};
        trackTitle=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=false,clipping=TextClipping.Clip};
        card=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,wordWrap=false,clipping=TextClipping.Clip,padding=new RectOffset(14,10,6,6),fontSize=16};
        cardOn=new GUIStyle(card);
        cardOn.normal.background=GUI.skin.button.onNormal.background;
        cardOn.hover.background=GUI.skin.button.onHover.background;
        nav=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleLeft,padding=new RectOffset(16,12,6,6),fontSize=17,wordWrap=false,clipping=TextClipping.Clip};
        navSelected=new GUIStyle(nav);navSelected.normal.background=GUI.skin.button.onNormal.background;
    }
    internal void Tick()
    {
        library.Flush(Time.unscaledTime);
        if(pending?.IsCompleted==true)
        {
            var task=pending;pending=null;
            try
            {
                var data=task.GetAwaiter().GetResult();
                if(data["tracks"] is JArray tracks){rows=ParseTracks(tracks);InvalidateTrackCache();}
                if(data["playlists"] is JArray lists){playlists=lists.OfType<JObject>().ToArray();RebuildPlaylistRows();}
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
        if(replaceQueue){library.Queue.Clear();library.Queue.AddRange(rows.Length>0?rows:new[]{track});queueIndex=library.Queue.FindIndex(t=>PlayerLibrary.Key(t)==PlayerLibrary.Key(track));library.Touch();}
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
    void RefreshViews()
    {
        if(viewVersion==library.Version)return;
        viewVersion=library.Version;
        favoritesView=library.Favorites.ToArray();historyView=library.History.ToArray();queueView=library.Queue.ToArray();
        int recent=Math.Min(5,historyView.Length);
        recentView=recent==historyView.Length?historyView:historyView[0..recent];
    }
    internal void Draw(float width,float height)
    {
        Styles();
        RefreshViews();
        float sidebar=width<800?SidebarNarrow:SidebarWide;
        GUILayout.BeginHorizontal(GUILayout.Height(Mathf.Max(160,height-260)));
        GUILayout.BeginVertical(GUI.skin.box,GUILayout.Width(sidebar));
        GUILayout.Label("MUONIUM",bigHeading);GUILayout.Label("PEAK PLAYER",muted);GUILayout.Space(8);
        sidebarScroll=GUILayout.BeginScrollView(sidebarScroll);
        for(int i=0;i<Pages.Length;i++)
        {
            if(GUILayout.Button(Pages[i],page==i?navSelected!:nav!,GUILayout.Height(NavHeight))){page=i;scroll=Vector2.zero;}
            if(i<Pages.Length-1)GUILayout.Space(NavGap);
        }
        GUILayout.EndScrollView();GUILayout.EndVertical();
        GUILayout.Space(16);
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        if(page is not (8 or 9))
        {
            GUILayout.BeginHorizontal();
            string[] sources={"网易云 · 原生","QQ · 原生","GD聚合"};
            bool sourceEnabled=GUI.enabled;GUI.enabled=sourceEnabled&&!Busy;
            for(int i=0;i<3;i++)
            {
                if(GUILayout.Button(sources[i],platform==i?navSelected!:nav!,GUILayout.Height(PlatformHeight)))
                {platform=i;polling=false;ClearQr();rows=Array.Empty<GdMusicApi.Track>();InvalidateTrackCache();playlists=Array.Empty<JObject>();playlistRows.Clear();message="已切换平台";loggedIn=false;accountName="未登录";}
                if(i<2)GUILayout.Space(PlatformGap);
            }
            GUI.enabled=sourceEnabled;
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
            if(platform==2)
            {
                var s=plugin.Settings;int selected=s.AggregatePlatform.Value;
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("‹",nav!,GUILayout.Width(40),GUILayout.Height(PlatformHeight)))s.AggregatePlatform.Value=(selected+9)%10;
                GUILayout.Space(PlatformGap);
                GUILayout.Label(GdMusicApi.Names[s.AggregatePlatform.Value],GUILayout.Height(PlatformHeight),GUILayout.Width(140));
                GUILayout.Space(PlatformGap);
                if(GUILayout.Button("›",nav!,GUILayout.Width(40),GUILayout.Height(PlatformHeight)))s.AggregatePlatform.Value=(selected+1)%10;
                GUILayout.EndHorizontal();
            }
        }
        scroll=GUILayout.BeginScrollView(scroll);
        switch(page)
        {
            case 0:Home();break;
            case 1:Search();break;
            case 2:Discover();break;
            case 3:Library();break;
            case 4:TrackRows(favoritesView,"本地收藏");break;
            case 5:TrackRows(historyView,"最近播放");break;
            case 6:Queue();break;
            case 7:Account();break;
            case 8:MusicPanel.DrawLyricsSettings();break;
            case 9:Diagnostics();break;
        }
        GUILayout.Space(10);
        GUILayout.Label(message,messageStyle,GUILayout.Height(24));
        GUILayout.EndScrollView();GUILayout.EndVertical();GUILayout.EndHorizontal();
        Transport();
    }
    bool ActionButton(string label){bool enabled=GUI.enabled;GUI.enabled=enabled&&!Busy;bool click=GUILayout.Button(label,card,GUILayout.Height(RowButtonHeight));GUI.enabled=enabled;return click;}
    void Home()
    {
        GUILayout.Space(18);GUILayout.Label("让音乐陪你继续攀登",heading);GUILayout.Label("Muonium 曲库与账号  ·  GD 多平台聚合  ·  Dango / Folia 悬浮歌词",muted);GUILayout.Space(18);
        GUILayout.BeginHorizontal();if(ActionButton("搜索喜欢的音乐  →"))page=1;GUILayout.Space(10);if(ActionButton("发现精选歌单  →")){page=2;LoadDiscover();}GUILayout.EndHorizontal();
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();if(ActionButton("账号与云端曲库  →")){page=7;if(platform!=2)Request("account",Source);}GUILayout.Space(10);if(ActionButton("我的本地收藏  →"))page=4;GUILayout.EndHorizontal();
        GUILayout.Space(8);
        if(platform==0&&ActionButton("网易云每日推荐 · 登录后使用")){page=1;Request("daily","ncm");}
        if(recentView.Length>0){GUILayout.Space(20);TrackRows(recentView,"继续听");}
    }
    void Search()
    {
        GUILayout.Label("搜索音乐",section);query=GUILayout.TextField(query,200,GUILayout.Height(44));
        GUILayout.Space(8);
        if(ActionButton("搜索")){searchPage=1;DoSearch();}
        GUILayout.Space(8);
        GUILayout.BeginHorizontal();if(ActionButton("上一页")&&searchPage>1){searchPage--;DoSearch();}GUILayout.Space(10);if(ActionButton("下一页")){searchPage++;DoSearch();}GUILayout.EndHorizontal();
        GUILayout.Space(10);
        TrackRows(platform==2?plugin.Aggregate.Results:rows,"搜索结果 · 第 "+searchPage+" 页");
    }
    void DoSearch(){if(query.Trim().Length==0){message="请输入歌名或歌手";return;}if(platform==2){Activate();plugin.Aggregate.Search(Source,query,searchPage);}else Request("search",Source,new JObject{["query"]=query,["page"]=searchPage});}
    void LoadDiscover(){if(platform==2){message="GD聚合提供歌曲搜索；精选歌单请使用原生平台";return;}Request("discover",Source);}
    void Discover(){GUILayout.Label("发现歌单",section);if(ActionButton("刷新推荐歌单"))LoadDiscover();GUILayout.Space(10);PlaylistRows();}
    void Library()
    {
        GUILayout.Label("我的歌单",section);
        if(platform!=2&&ActionButton("加载账号歌单"))Request("library",Source);
        GUILayout.Space(10);
        GUILayout.Label("通过平台歌单ID打开",muted);playlistId=GUILayout.TextField(playlistId,24,GUILayout.Height(42));
        GUILayout.Space(8);
        if(platform!=2&&ActionButton("打开歌单"))Request("playlist",Source,new JObject{["playlistId"]=playlistId});
        GUILayout.Space(10);PlaylistRows();GUILayout.Space(10);TrackRows(rows,"歌单歌曲");
    }
    void RebuildPlaylistRows()
    {
        playlistRows.Clear();
        foreach(var list in playlists.Take(PlaylistPageSize))
        {
            string id=(string?)list["id"]??"",name=(string?)list["name"]??"歌单";
            int count=(int?)list["trackCount"]??0;
            playlistRows.Add(new PlaylistRow(id,name,count));
        }
    }
    void PlaylistRows()
    {
        if(playlistRows.Count==0){GUILayout.Label("还没有歌单，先刷新推荐歌单或加载账号歌单",muted);return;}
        foreach(var row in playlistRows)
        {
            if(GUILayout.Button(row.Name+(row.Count>0?" · "+row.Count+" 首":""),card,GUILayout.Height(PlaylistHeight)))
            {playlistId=row.Id;page=3;rows=Array.Empty<GdMusicApi.Track>();InvalidateTrackCache();Request("playlist",Source,new JObject{["playlistId"]=row.Id});}
            GUILayout.Space(6);
        }
    }
    void TrackRows(GdMusicApi.Track[] tracks,string title)
    {
        GUILayout.Label(title,section);
        GUILayout.Space(6);
        if(tracks.Length==0)
        {
            // Fixed-height placeholder: an empty result is exactly as tall as a loaded list,
            // so switching pages never makes the panel jump.
            GUILayout.Label("还没有歌曲，搜索或加载歌单开始聆听",muted,GUILayout.Height(24));
            GUILayout.Space(TrackRowHeight*2);
            return;
        }
        int count=Math.Min(tracks.Length,TrackPageSize);
        float pitch=TrackRowHeight+TrackRowGap;
        Rect area=GUILayoutUtility.GetRect(GUIContent.none,GUIStyle.none,GUILayout.MinWidth(0),GUILayout.ExpandWidth(true),GUILayout.Height(count*pitch));
        float mainWidth=Mathf.Max(120f,area.width-TrackActionWidth-TrackAddWidth-ColumnGap*2);
        RebuildTrackCache(tracks,count,mainWidth);
        for(int i=0;i<count;i++)
        {
            var track=tracks[i];float y=area.y+i*pitch;
            if(GUI.Button(new Rect(area.x,y,mainWidth,TrackRowHeight),trackLabels[i],card))
            {rows=tracks;Play(track,true);}
            bool favorite=library.Favorite(track);
            if(GUI.Button(new Rect(area.x+mainWidth+ColumnGap,y,TrackActionWidth,TrackRowHeight),favorite?"已收藏":"收藏",favorite?cardOn:card))
                library.Toggle(track);
            if(GUI.Button(new Rect(area.x+mainWidth+ColumnGap+TrackActionWidth+ColumnGap,y,TrackAddWidth,TrackRowHeight),"加入",card))
            {if(library.Queue.Count<200){library.Queue.Add(track);library.Touch();message="已加入播放队列";}}
        }
        if(tracks.Length>TrackPageSize){GUILayout.Space(6);GUILayout.Label("已显示前 "+TrackPageSize+" 首；用搜索分页查看其余结果",muted);}
    }
    void RebuildTrackCache(GdMusicApi.Track[] tracks,int count,float width)
    {
        if(ReferenceEquals(cachedTracks,tracks)&&cachedCount==count&&Mathf.Abs(cachedLabelWidth-width)<.5f&&cachedLibraryVersion==library.Version)return;
        cachedTracks=tracks;cachedCount=count;cachedLabelWidth=width;cachedLibraryVersion=library.Version;
        trackLabels.Clear();
        for(int i=0;i<count;i++)trackLabels.Add(TrackLabel(tracks[i],width));
    }
    void InvalidateTrackCache(){cachedTracks=Array.Empty<GdMusicApi.Track>();cachedCount=-1;trackLabels.Clear();}
    static string TrackLabel(GdMusicApi.Track track,float width)
    {
        float textWidth=Mathf.Max(40f,width-24);
        string meta=track.Artist.Length>0?(track.Album.Length>0?track.Artist+" · "+track.Album:track.Artist):track.Album;
        return Fit(track.Title,textWidth)+"\n"+Fit(meta,textWidth);
    }
    // Binary search on measured width: the label always fits its cell, never overflows.
    static string Fit(string text,float width)
    {
        var style=FitStyle;
        if(text.Length==0||style.CalcSize(new GUIContent(text)).x<=width)return text;
        int low=0,high=text.Length;
        while(low<high)
        {
            int mid=(low+high+1)/2;
            if(style.CalcSize(new GUIContent(text.Substring(0,mid)+"…")).x<=width)low=mid;else high=mid-1;
        }
        return low<=0?"…":text.Substring(0,low).TrimEnd()+"…";
    }
    static GUIStyle? fitStyle;
    static GUIStyle FitStyle
    {
        get
        {
            if(fitStyle==null)
            {
                fitStyle=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=false};
                fitStyle.normal.textColor=Color.white;
            }
            return fitStyle;
        }
    }
    void Queue(){GUILayout.Label("播放队列",section);if(ActionButton("清空队列")){library.Queue.Clear();queueIndex=-1;library.Touch();RefreshViews();}GUILayout.Space(10);TrackRows(queueView,"队列曲目");}
    void Account()
    {
        GUILayout.Label("账号中心",section);
        if(platform==2){GUILayout.Label("GD聚合不需要登录。网易云/QQ账号分别在原生平台管理。",muted);return;}
        GUILayout.Label(accountName,bigHeading);
        GUILayout.Label("使用对应平台手机App扫码，凭证由Windows当前用户加密保存。退出登录会清除本模组的对应平台登录态。",muted);
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        if(ActionButton("生成登录二维码")){polling=false;ClearQr();Request("qr",Source);}GUILayout.Space(10);
        if(ActionButton("刷新账号状态"))Request("account",Source);GUILayout.Space(10);
        if(ActionButton("退出此账号")){polling=false;ClearQr();Request("logout",Source);}
        GUILayout.EndHorizontal();
        if(qr!=null){GUILayout.Space(10);var rect=GUILayoutUtility.GetRect(280,280,GUILayout.Width(280),GUILayout.Height(280));GUI.DrawTexture(rect,qr,ScaleMode.ScaleToFit);}
    }
    void Diagnostics()
    {
        GUILayout.Label("连接与诊断",section);
        GUILayout.Label("当前只运行Muonium原生曲库与GD聚合，不启动旧桌面播放器接口。音乐开关与歌词显示开关互相独立。",muted);
        GUILayout.Space(8);
        plugin.Settings.PlayerEnabled.Value=GUILayout.Toggle(plugin.Settings.PlayerEnabled.Value,"启用游戏内播放器",GUILayout.Height(RowButtonHeight));
        GUILayout.Space(8);
        string errors=DiagnosticLog.Snapshot;
        GUILayout.Label(errors.Length>0?errors:"当前没有最近错误",muted);
        GUILayout.Space(8);
        if(ActionButton("复制错误详情"))GUIUtility.systemCopyBuffer=errors;
    }
    void Transport()
    {
        GUILayout.Space(12);GUILayout.BeginVertical(GUI.skin.box);
        var state=plugin.Aggregate.Snapshot;
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical(GUILayout.Width(220));GUILayout.Label(current?.Title??"选择一首音乐",trackTitle);GUILayout.Label(current?.Artist??"PEAK · Muonium",messageStyle,GUILayout.Height(20));GUILayout.EndVertical();
        GUILayout.Space(8);
        if(GUILayout.Button("上一首",nav!,GUILayout.Width(85),GUILayout.Height(42)))Next(-1);GUILayout.Space(6);
        if(GUILayout.Button(state.Playing?"暂停":"播放",nav!,GUILayout.Width(85),GUILayout.Height(42)))plugin.Aggregate.TogglePause();GUILayout.Space(6);
        if(GUILayout.Button("下一首",nav!,GUILayout.Width(85),GUILayout.Height(42)))Next(1);GUILayout.Space(6);
        if(GUILayout.Button(new[]{"顺序播放","随机播放","单曲循环"}[playMode],nav!,GUILayout.Width(110),GUILayout.Height(42)))playMode=(playMode+1)%3;GUILayout.Space(6);
        if(GUILayout.Button("停止",nav!,GUILayout.Width(65),GUILayout.Height(42)))plugin.Aggregate.Stop();
        GUILayout.FlexibleSpace();GUILayout.Label("音量",GUILayout.Width(44));plugin.Settings.AggregateVolume.Value=GUILayout.HorizontalSlider(plugin.Settings.AggregateVolume.Value,0,1,GUILayout.Width(110));
        GUILayout.EndHorizontal();
        if(state.DurationMs>0)
        {
            long positionSecond=state.PositionMs/1000,durationSecond=state.DurationMs/1000;
            if(positionSecond!=cachedPositionSecond){cachedPositionSecond=positionSecond;cachedPositionText=Clock(state.PositionMs);}
            if(durationSecond!=cachedDurationSecond){cachedDurationSecond=durationSecond;cachedDurationText=Clock(state.DurationMs);}
            GUILayout.BeginHorizontal();
            GUILayout.Label(cachedPositionText,GUILayout.Width(60));
            float fraction=(float)state.PositionMs/state.DurationMs;float next=GUILayout.HorizontalSlider(fraction,0,1);
            if(Math.Abs(next-fraction)>.001f)plugin.Aggregate.Seek(next);
            GUILayout.Label(cachedDurationText,GUILayout.Width(60));
            GUILayout.EndHorizontal();
        }
        GUILayout.Label(playback!=null?"正在从原生曲库解析音频…":plugin.Aggregate.Status,messageStyle,GUILayout.Height(24));GUILayout.EndVertical();
    }
    static string Clock(long ms)=>TimeSpan.FromMilliseconds(ms).ToString(@"mm\:ss",CultureInfo.InvariantCulture);
    void ClearQr(){if(qr!=null)UnityEngine.Object.Destroy(qr);qr=null;}
    internal void DrawTitleEntry(bool open)
    {
        if(open||UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Title")return;
        if(titleEntry==null)
        {
            titleFont=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},19);
            titleEntry=new GUIStyle(GUI.skin.button){font=titleFont,fontSize=19};
        }
        if(GUI.Button(new Rect(Screen.width-260,Screen.height-76,230,48),"♫ 音乐播放器",titleEntry))plugin.OpenPlayer();
    }
    public void Dispose(){client.Dispose();ClearQr();if(titleFont!=null)UnityEngine.Object.Destroy(titleFont);plugin.Aggregate.TrackStarted-=Started;plugin.Aggregate.TrackEnded-=Ended;}
}
