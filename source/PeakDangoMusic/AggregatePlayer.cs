using System.Collections;
using DangoMusic.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace PeakDangoMusic;

// Unity objects are touched only by Tick/UI/coroutines on the game thread.
internal sealed class AggregatePlayer : IDisposable
{
    readonly Plugin owner;
    readonly GdMusicApi api=new();
    readonly AudioSource audio;
    CancellationTokenSource cancel=new();
    Task<GdMusicApi.Track[]>? search;
    Task<GdMusicApi.Resolved>? resolve;
    Coroutine? loading;
    UnityWebRequest? request;
    GdMusicApi.Track? current;
    LyricsTimeline timeline=LyricsTimeline.Empty;
    int generation;
    bool active,paused;
    internal GdMusicApi.Track[] Results {get;private set;}=Array.Empty<GdMusicApi.Track>();
    internal string Status {get;private set;}="输入歌名或歌手搜索";
    internal bool Busy=>search!=null||resolve!=null||loading!=null;
    internal event Action<GdMusicApi.Track>? TrackStarted;
    internal event Action? TrackEnded;
    bool endReported;
    internal void PlayResolved(GdMusicApi.Resolved value)
    {
        if(!active||Busy)return;
        loading=owner.StartCoroutine(Load(value));
    }
    internal AggregatePlayer(Plugin plugin)
    {
        owner=plugin;audio=plugin.gameObject.AddComponent<AudioSource>();audio.playOnAwake=false;
        audio.spatialBlend=0;audio.ignoreListenerPause=true;
    }
    internal MusicState Snapshot=>new(active,current==null?"":"gd/"+current.Source+"/"+current.Id,current?.Title??"",current?.Artist??"",
        audio.clip!=null?(long)(audio.time*1000):0,audio.clip!=null?(long)(audio.clip.length*1000):0,
        audio.isPlaying,MusicClock.Now,1,timeline,generation,Status);
    internal void Tick(bool enabled,float volume)
    {
        if(active!=enabled){active=enabled;if(!enabled)Stop();}
        audio.volume=volume;
        if(search?.IsCompleted==true)
        {
            var completed=search;search=null;
            try{Results=completed.GetAwaiter().GetResult();Status=Results.Length==0?"该平台没有搜索结果，可更换平台":$"找到 {Results.Length} 首，点击播放";}
            catch(Exception ex){Status=DiagnosticLog.Report("GD聚合音乐","搜索歌曲",ex);}
        }
        if(resolve?.IsCompleted==true)
        {
            var completed=resolve;resolve=null;
            try{loading=owner.StartCoroutine(Load(completed.GetAwaiter().GetResult()));}
            catch(Exception ex){Status=DiagnosticLog.Report("GD聚合音乐","解析播放地址",ex);}
        }
        if(current!=null&&audio.clip!=null&&!audio.isPlaying&&!paused&&!Busy&&!endReported)
        {endReported=true;Status="播放结束，可选择下一首";TrackEnded?.Invoke();}
    }
    internal void Search(string platform,string keyword,int page)
    {
        if(Busy||!active||string.IsNullOrWhiteSpace(keyword))return;
        Results=Array.Empty<GdMusicApi.Track>();Status="正在搜索…";
        var token=cancel.Token;search=Task.Run(()=>api.Search(platform,keyword,page,token));
    }
    internal void Play(GdMusicApi.Track track,int bitrate)
    {
        if(Busy||!active)return;
        Status="正在解析播放地址…";
        var token=cancel.Token;resolve=Task.Run(()=>api.Resolve(track,bitrate,token));
    }
    IEnumerator Load(GdMusicApi.Resolved result)
    {
        // Yield once so loading has been assigned even for a synchronous failure.
        yield return null;
        Status="正在加载音频…";
        string format=result.Format.ToLowerInvariant();
        if(format.Length==0)format=Path.GetExtension(new Uri(result.Url).AbsolutePath).TrimStart('.').ToLowerInvariant();
        var type=format switch{"ogg"=>AudioType.OGGVORBIS,"wav"=>AudioType.WAV,"aiff" or "aif"=>AudioType.AIFF,_=>AudioType.MPEG};
        if(format is "flac" or "m4a" or "aac")
        {Status="当前播放地址为 "+format+"，Unity音频后端不支持；请选择128或320 kbps重试";loading=null;yield break;}
        // PEAK rejects plaintext HTTP. Music CDNs expose the same resource on TLS;
        // upgrade this request only instead of weakening the game's global policy.
        var address=new Uri(result.Url);
        string audioUrl=address.Scheme=="http"?new UriBuilder(address){Scheme="https",Port=address.IsDefaultPort?-1:address.Port}.Uri.AbsoluteUri:result.Url;
        using(var download=UnityWebRequestMultimedia.GetAudioClip(audioUrl,type))
        {
            request=download;download.timeout=30;
            ((DownloadHandlerAudioClip)download.downloadHandler).streamAudio=true;
            UnityWebRequestAsyncOperation? operation=null;
            try{operation=download.SendWebRequest();}
            catch(Exception ex){Status=DiagnosticLog.Report("Muonium音乐","发起音频请求",ex);request=null;loading=null;}
            if(operation==null)yield break;
            yield return operation;request=null;
            if(download.result!=UnityWebRequest.Result.Success)
                Status=DiagnosticLog.Report("GD聚合音乐","下载音频",new IOException(download.error));
            else
            {
                AudioClip? clip=null;
                try{clip=DownloadHandlerAudioClip.GetContent(download);}
                catch(Exception ex){Status=DiagnosticLog.Report("GD聚合音乐","解码音频",ex);}
                if(clip!=null)
                {
                    ReleaseClip();audio.clip=clip;current=result.Track;timeline=result.Lyrics;generation++;paused=false;endReported=false;audio.Play();TrackStarted?.Invoke(current);
                    Status=timeline.Lines.Count>0?"正在播放 · GD同步歌词":"正在播放 · 该歌曲未提供同步歌词";
                    if(result.Note.Length>0)Status+="\n"+result.Note;
                }
            }
        }
        loading=null;
    }
    internal void TogglePause()
    {
        if(audio.clip==null)return;
        if(paused){audio.UnPause();paused=false;Status="正在播放";}
        else if(!audio.isPlaying){audio.time=0;audio.Play();endReported=false;Status="正在播放";}
        else{audio.Pause();paused=true;Status="已暂停";}
    }
    internal void Seek(float fraction){if(audio.clip!=null)audio.time=Mathf.Clamp(fraction,0,.999f)*audio.clip.length;}
    void ReleaseClip(){audio.Stop();var clip=audio.clip;audio.clip=null;if(clip!=null)UnityEngine.Object.Destroy(clip);}
    internal void Stop()
    {
        cancel.Cancel();cancel.Dispose();cancel=new();
        // Observe failures from jobs abandoned by a source change.
        if(search!=null)_=search.ContinueWith(t=>{_ = t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        if(resolve!=null)_=resolve.ContinueWith(t=>{_ = t.Exception;},TaskContinuationOptions.OnlyOnFaulted);
        search=null;resolve=null;var activeRequest=request;request=null;
        try{activeRequest?.Abort();}catch(NullReferenceException){}catch(ObjectDisposedException){}
        if(loading!=null){owner.StopCoroutine(loading);loading=null;}
        ReleaseClip();current=null;timeline=LyricsTimeline.Empty;paused=false;generation++;Status="已停止";
    }
    public void Dispose(){Stop();cancel.Dispose();api.Dispose();UnityEngine.Object.Destroy(audio);}
}
