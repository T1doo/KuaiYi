using KuaiYi.Core;
namespace KuaiYi.Imaging;
// Reuses the Core's generic exclusive acquisition lease, without screenshot pixels or local-request enable authority.
public sealed class FileImageIntent:IDisposable
{
    private readonly RuntimeGate gate;private readonly Guid owner;private readonly long epoch;private readonly Activation initial;private bool disposed;
    public FileImageIntent(RuntimeGate gate,long? expectedEpoch=null,Activation? expectedActivation=null){this.gate=gate;if(!gate.TryBeginExplicitCapture(out owner,out epoch,out initial,expectedEpoch:expectedEpoch,expectedActivation:expectedActivation))throw new InvalidOperationException("Image acquisition unavailable.");}
    public bool IsCurrent=>!disposed&&gate.IsExplicitCaptureCurrent(owner,epoch,initial);
    public bool TryCommitLocalImage(Func<bool> current,Action publish,out CancellationTokenSource? detached)
    {detached=null;if(disposed||!gate.TryCommitExplicitLocalImage(owner,epoch,initial,current,publish,out detached))return false;disposed=true;return true;}
    public void Dispose(){if(disposed)return;disposed=true;gate.EndExplicitCapture(owner);}
}
public interface IImagePreparationOperation
{ValueTask<bool> TryFinalizeAsync();}
public interface IFileImageOperation:IImagePreparationOperation
{
    Task<OwnedFileImage> LoadAsync(string path,Guid session,long version,long epoch,CancellationToken cancellation);
}

public interface IBrowserImageOperation:IImagePreparationOperation
{Task<OwnedFileImage> LoadAsync(ReadOnlyMemory<byte> png,Guid session,long version,long epoch,CancellationToken cancellation);}
