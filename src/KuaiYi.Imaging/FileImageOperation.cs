using System.Security.Cryptography;
namespace KuaiYi.Imaging;
/// <summary>One original read/preparation and its exact Worker owner; never release while the original task is active.</summary>
public sealed class FileImageOperation(Func<ImageWorkerClient> createWorker):IFileImageOperation
{
    private readonly object sync=new();private ImageWorkerClient? worker;private bool started,active,finalized;
    public async Task<OwnedFileImage> LoadAsync(string path,Guid session,long version,long epoch,CancellationToken cancellation)
    {
        lock(sync){if(started||finalized)throw new InvalidOperationException("File operation is single-use.");started=active=true;}byte[]? bytes=null;
        try
        {
            bytes=await LocalImageFile.ReadEncodedAsync(path,cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            worker=createWorker();return await worker.PrepareFileAsync(bytes,new(session,version,epoch,bytes.Length,ImageDecodePolicy.Hash(bytes)),cancellation).ConfigureAwait(false);
        }
        finally{if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);lock(sync)active=false;}
    }
    public async ValueTask<bool> TryFinalizeAsync()
    {
        ImageWorkerClient? held;
        lock(sync){if(active)return false;if(finalized)return true;held=worker;if(held==null){finalized=true;return true;}}
        bool complete=await held.TryFinalizeAsync().ConfigureAwait(false);
        if(complete)lock(sync)finalized=true;return complete;
    }
}
