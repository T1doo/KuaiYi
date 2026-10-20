using KuaiYi.Core;
namespace KuaiYi.Imaging;
// Reuses the Core's generic exclusive acquisition lease, without screenshot pixels or local-request enable authority.
public sealed class FileImageIntent:IDisposable
{
    private readonly RuntimeGate gate;private readonly Guid owner;private readonly long epoch;private readonly Activation initial;private bool disposed;
    public FileImageIntent(RuntimeGate gate){this.gate=gate;if(!gate.TryBeginExplicitCapture(out owner,out epoch,out initial))throw new InvalidOperationException("Image acquisition unavailable.");}
    public bool IsCurrent=>!disposed&&gate.IsExplicitCaptureCurrent(owner,epoch,initial);
    public void Dispose(){if(disposed)return;disposed=true;gate.EndExplicitCapture(owner);}
}
public interface IFileImageOperation
{
    Task<OwnedFileImage> LoadAsync(string path,Guid session,long version,long epoch,CancellationToken cancellation);
    ValueTask<bool> TryFinalizeAsync();
}
