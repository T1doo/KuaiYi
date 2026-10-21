using System.IO;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private InstallationWarmImageControl? installedWarm;
    private sealed class WarmImageUi(InstallationWarmLease lease,InstalledImageState state)
    {
        internal readonly InstallationWarmLease Lease=lease;
        internal readonly InstalledImageState State=state;
        internal Task Original=Task.CompletedTask;
    }
    private WarmImageUi? warmImageUi;
    private void AttachWarmInstalledImages(InstallationApplicationControl control)
    {
        if(control.Proof.UpgradeRecord!=null)return; // The upgrade/handoff role has no normal-session endpoint.
        installedWarm=control.AttachWarm(TimeProvider.System,()=>lifecycle.Gate.Snapshot.Epoch,
            (warm,lease,path,token)=>PrepareWarmImage(control,warm,lease,path,token),FinalizeWarmImage,ReserveWarmImage);
    }
    private async Task<long?> ReserveWarmImage(InstallationWarmLease lease,CancellationToken token)
    {
        return await Dispatcher.InvokeAsync<long?>(()=>
        {
            if(token.IsCancellationRequested||Volatile.Read(ref installedImageClosed)||exiting||!startupHealthy||!IsLoaded||lifecycle.Settings is not {ReadOnly:false,RequiresRecovery:false})return null;
            var state=lifecycle.Gate.Snapshot;
            if(state.Suspension!=Suspension.None||!CanReceiveBrowserImage())return null;
            installationControl!.Proof.RequireImageRecords(); // Bounded original records, outside WarmSync and Gate.
            if(!IsVisible)ShowOverlay();
            if(token.IsCancellationRequested||Volatile.Read(ref installedImageClosed)||exiting||!IsVisible||!CanReceiveBrowserImage()||lifecycle.Gate.Snapshot.Suspension!=Suspension.None||lifecycle.Gate.Snapshot.Presentation==Presentation.Hidden)return null;
            var frozen=CaptureInstalledImageState();warmImageUi=new(lease,frozen);return frozen.Epoch;
        },DispatcherPriority.Normal,token);
    }
    private async Task PrepareWarmImage(InstallationApplicationControl control,InstallationWarmImageAdmission warm,InstallationWarmLease lease,string path,CancellationToken token)
    {
        // This exact composite is registered in the warm actor; no cancellation waiter substitutes for any hash or UI task.
        await Task.Run(()=>control.Proof.RequireRecords(token),CancellationToken.None).ConfigureAwait(false);
        Task original=await Dispatcher.InvokeAsync(()=>
        {
            token.ThrowIfCancellationRequested();var ui=warmImageUi;
            if(ui==null||!ReferenceEquals(ui.Lease,lease)||ui.State.Epoch!=lease.Grant.Epoch||ui.State!=CaptureInstalledImageState()||Volatile.Read(ref installedImageClosed))return Task.CompletedTask;
            control.Proof.RequireImageRecords();
            bool Current()=>!token.IsCancellationRequested&&!Volatile.Read(ref installedImageClosed)&&ReferenceEquals(warmImageUi,ui)&&lifecycle.Settings?.Version==ui.State.Settings;
            Task Verify(CancellationToken ct)=>Task.Run(()=>{ct.ThrowIfCancellationRequested();control.Proof.RequireRecords(ct);control.Proof.RequireImageRecords();ct.ThrowIfCancellationRequested();},CancellationToken.None);
            ui.Original=StartInstalledImage(path,ui.State,CreateFileImageOperation,token,Current,Verify,(admission,intent,image)=>
            {
                // Last bounded record check immediately precedes the pure WarmSync -> Gate transaction.
                try{control.Proof.RequireImageRecords();}
                catch{image.Dispose();throw;}
                return AcceptFileImage(admission,intent,image,warm,lease);
            });
            return ui.Original;
        },DispatcherPriority.Normal,token);
        await original.ConfigureAwait(false);
    }
    private async ValueTask<bool> FinalizeWarmImage(InstallationWarmLease lease)
    {
        return await Dispatcher.InvokeAsync(()=>
        {
            if(warmImageUi is not {} ui||!ReferenceEquals(ui.Lease,lease))return true; // Reserve rejected before a UI owner was made.
            if(!ui.Original.IsCompletedSuccessfully||fileImageOwner!=null||!fileImageCleanup.IsCompletedSuccessfully)return false;
            warmImageUi=null;return true;
        },DispatcherPriority.Normal);
    }
    partial void SealWarmInstalledImages()=>installedWarm?.Quiesce();
}
