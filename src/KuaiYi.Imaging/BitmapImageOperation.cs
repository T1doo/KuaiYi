using KuaiYi.Core;
namespace KuaiYi.Imaging;
/// <summary>Owns a copied Drop snapshot until both original peer cleanup and Worker exit are confirmed.</summary>
public sealed class BitmapImageOperation(OwnedOleBitmap owned,Task peerSettlement,Func<ImageWorkerClient> createWorker):IBitmapImageOperation
{
    private readonly object sync=new();private ImageWorkerClient? worker;private bool started,active,finalized;
    public async Task<OwnedFileImage> LoadAsync(Guid session,long version,long epoch,CancellationToken cancellation)
    {
        lock(sync){if(started||finalized)throw new InvalidOperationException("Bitmap operation is single-use.");started=active=true;}
        try
        {
            await peerSettlement.WaitAsync(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            var bytes=owned.Bytes;OleBitmapDrop.ValidateBmp(bytes.Span);string hash=ImageDecodePolicy.Hash(bytes.Span);cancellation.ThrowIfCancellationRequested();
            worker=createWorker();return await worker.PrepareBitmapAsync(bytes,new(session,version,epoch,bytes.Length,hash),cancellation).ConfigureAwait(false);
        }
        finally{lock(sync)active=false;}
    }
    public async Task ObservePeerCleanupAsync(){try{await peerSettlement.ConfigureAwait(false);}catch{if(!peerSettlement.IsCompleted)throw;_=peerSettlement.Exception;}}
    public async ValueTask<bool> TryFinalizeAsync()
    {
        ImageWorkerClient? held;lock(sync){if(active||!peerSettlement.IsCompleted)return false;if(finalized)return true;held=worker;}
        // Observe a failed transport, while still permitting cleanup of its separately owned bytes.
        if(peerSettlement.IsFaulted)_=peerSettlement.Exception;
        if(held!=null&&!await held.TryFinalizeAsync().ConfigureAwait(false))return false;
        lock(sync){owned.Dispose();finalized=true;}return true;
    }
}
