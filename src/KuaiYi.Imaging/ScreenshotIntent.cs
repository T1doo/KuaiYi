using KuaiYi.Core;
namespace KuaiYi.Imaging;
/// <summary>Explicit acquisition without replacing the old request. Cancel leaves its content/identity intact.</summary>
public sealed class ScreenshotIntent:IDisposable
{
    private readonly RuntimeGate gate;
    private readonly long epoch;
    private readonly Guid owner;
    private readonly Activation initial;
    private bool disposed;
    public ScreenshotIntent(RuntimeGate gate)
    {
        this.gate=gate;if(!gate.TryBeginExplicitCapture(out owner,out epoch,out initial))throw new InvalidOperationException("Capture unavailable while suspended.");
    }
    public bool IsCurrent=>!disposed&&gate.IsExplicitCaptureCurrent(owner,epoch,initial);
    public bool RequiresConfirmation(bool alwaysPreview)=>initial==Activation.Off||alwaysPreview;
    public bool TryCommit(bool explicitlyConfirmed,Guid session,Guid version,out RequestIdentity identity,out CancellationToken cancellation)
    {
        identity=default;cancellation=default;
        if(!IsCurrent||initial==Activation.Off&&!explicitlyConfirmed)return false;
        Dispose(); // No active identity was created while selecting; release passive suspension before local-only request.
        return gate.TryStartConfirmedLocalRequest(epoch+1,session,version,out identity,out cancellation);
    }
    public void Dispose(){if(disposed)return;disposed=true;gate.EndExplicitCapture(owner);}
}
