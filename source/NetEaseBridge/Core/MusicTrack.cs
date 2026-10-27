namespace DangoMusic.Core;
public sealed record MusicTrack(long Id,string Title,string Artist,long DurationMs=0,string CoverUrl="",string? LocalPath=null);
