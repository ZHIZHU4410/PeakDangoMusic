namespace PeakDangoMusic;

internal sealed class MusicClient : IDisposable
{
    readonly LxClient lx;
    readonly NativeMusicClient native=new();
    int source;
    internal MusicState Snapshot=>source!=0?native.Snapshot:lx.Snapshot;
    internal MusicClient(MusicSettings settings)
    {source=settings.EffectiveSource;lx=new LxClient(settings.Port.Value,settings.Enabled.Value && source==0);Configure(settings);}
    internal void Configure(MusicSettings settings)
    {
        source=settings.EffectiveSource;
        lx.Configure(settings.Port.Value,settings.Enabled.Value && source==0);
        native.Configure(settings.NetEasePort.Value,settings.Enabled.Value && source!=0,source,settings.KugouLyricsFolder.Value);
    }
    internal void Reconnect(){if(source!=0)native.Reconnect();else lx.Reconnect();}
    public void Dispose(){lx.Dispose();native.Dispose();}
}
