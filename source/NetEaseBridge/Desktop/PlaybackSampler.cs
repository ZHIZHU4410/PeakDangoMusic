namespace DangoMusic.Desktop;

// A hung media/UI provider cannot block the stdout heartbeat or spawn unbounded retries.
public sealed class PlaybackSampler : IDisposable
{
    readonly CancellationTokenSource cancel;
    readonly object gate=new();
    readonly PlaybackBuffer buffer=new();
    readonly Task worker;
    public PlaybackSampler(Func<CancellationToken,Task<DesktopPlayback?>> read,CancellationToken lifetime)
    {
        cancel=CancellationTokenSource.CreateLinkedTokenSource(lifetime);
        worker=Task.Run(async()=>
        {
            try
            {
                while(!cancel.IsCancellationRequested)
                {
                    DesktopPlayback? sample=null;
                    try{sample=await read(cancel.Token).ConfigureAwait(false);}catch(Exception ex)when(ex is not OutOfMemoryException){}
                    lock(gate)buffer.Observe(sample,Environment.TickCount64);
                    await Task.Delay(250,cancel.Token).ConfigureAwait(false);
                }
            }
            catch(OperationCanceledException){}
        });
    }
    public DesktopPlayback? Snapshot{get{lock(gate)return buffer.Read(Environment.TickCount64);}}
    public void Dispose(){cancel.Cancel();}
}
