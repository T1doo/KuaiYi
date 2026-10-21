namespace KuaiYi.Imaging;
/// <summary>One borrowed, already received PNG. Caller retains its receipt through this original Task.
/// No path, file reader or screenshot coordinate authority.</summary>
public sealed class BrowserImageOperation(Func<ImageWorkerClient> createWorker):IBrowserImageOperation
{
    private readonly object sync=new();private ImageWorkerClient? worker;private bool started,active,finalized;
    public async Task<OwnedFileImage> LoadAsync(ReadOnlyMemory<byte> png,Guid session,long version,long epoch,CancellationToken cancellation)
    {
        lock(sync){if(started||finalized)throw new InvalidOperationException("Browser image operation is single-use.");started=active=true;}
        try
        {
            cancellation.ThrowIfCancellationRequested();_=ImagePolicy.InspectPng(png.Span);
            worker=createWorker();return await worker.PrepareBrowserAsync(png,new(session,version,epoch,png.Length,ImageDecodePolicy.Hash(png.Span)),cancellation).ConfigureAwait(false);
        }
        finally{lock(sync)active=false;}
    }
    public async ValueTask<bool> TryFinalizeAsync()
    {
        ImageWorkerClient? held;lock(sync){if(active)return false;if(finalized)return true;held=worker;if(held==null){finalized=true;return true;}}
        bool complete=await held.TryFinalizeAsync().ConfigureAwait(false);if(complete)lock(sync)finalized=true;return complete;
    }
}
