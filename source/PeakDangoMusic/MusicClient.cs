namespace PeakDangoMusic;

// Muonium-native player is now the only runtime clock. Legacy desktop readers
// remain as historical source; no CDP ports, launch scripts or SMTC workers start.
internal sealed class MusicClient : IDisposable
{
    readonly AggregatePlayer player;
    internal MusicState Snapshot=>player.Snapshot;
    internal MusicClient(MusicSettings settings,AggregatePlayer aggregate){player=aggregate;}
    internal void Configure(MusicSettings settings){}
    internal void Reconnect()=>player.Stop();
    public void Dispose(){}
}
