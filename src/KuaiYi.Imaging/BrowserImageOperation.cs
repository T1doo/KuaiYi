namespace KuaiYi.Imaging;
/// <summary>One borrowed, already received encoded raster. Caller retains its receipt through this original Task.
/// No path, file reader or screenshot coordinate authority.</summary>
public sealed class BrowserImageOperation(Func<ImageWorkerClient> createWorker):IBrowserImageOperation
{
    private readonly object sync=new();private ImageWorkerClient? worker;private bool started,active,finalized;
    public async Task<OwnedFileImage> LoadAsync(ReadOnlyMemory<byte> encoded,Guid session,long version,long epoch,CancellationToken cancellation)
    {
        lock(sync){if(started||finalized)throw new InvalidOperationException("Browser image operation is single-use.");started=active=true;}
        try
        {
            // Keep bounded container inspection and digest work off the destination UI thread.
            // The caller retains its receipt until this original task and Worker settlement return.
            string hash=await Task.Run(()=>
            {
                cancellation.ThrowIfCancellationRequested();var source=ImageDecodePolicy.Inspect(encoded.Span,cancellation);
                _=BrowserEncodedImagePolicy.Mime(source.Format);var digest=ImageDecodePolicy.Hash(encoded.Span);
                cancellation.ThrowIfCancellationRequested();return digest;
            },cancellation).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();worker=createWorker();
            return await worker.PrepareBrowserAsync(encoded,new(session,version,epoch,encoded.Length,hash),cancellation).ConfigureAwait(false);
        }
        finally{lock(sync)active=false;}
    }
    public async ValueTask<bool> TryFinalizeAsync()
    {
        ImageWorkerClient? held;lock(sync){if(active)return false;if(finalized)return true;held=worker;if(held==null){finalized=true;return true;}}
        bool complete=await held.TryFinalizeAsync().ConfigureAwait(false);if(complete)lock(sync)finalized=true;return complete;
    }
}
