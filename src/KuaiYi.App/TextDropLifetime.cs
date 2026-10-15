using System.IO;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private bool dropStopping;
    private async Task SettleClearedTextDrop()
    {
        var cleared=dropPeer;if(cleared==null)return;
        // The existing reconciliation remains the only peer finalizer; poll/exit share it.
        if(!dropStopping&&dropReconcile.IsCompleted)dropReconcile=ReconcileTextDrop();
        await dropReconcile;
        if(ReferenceEquals(dropPeer,cleared))throw new IOException("Cleared drop ownership/registration is unconfirmed.");
    }
    private void RefreshTextDrop()
    {
        if(dropStopping||dropCapture==null)return;if(lifecycle.Gate.Snapshot.Mode!=TriggerMode.Drop)dropUnavailable=null;Volatile.Write(ref dropVisible,IsVisible&&!exiting&&geometryReady);
        var state=lifecycle.Gate.Snapshot;bool wanted=dropVisible&&state.Activation==Activation.On&&state.Mode==TriggerMode.Drop&&state.Suspension==Suspension.None;
        if(dropPeer is {} current&&(!wanted||dropCapture.Lease!=current.Lease||!lifecycle.Gate.IsCaptureCurrent(current.Lease.Ticket)||lifecycle.Settings?.Version!=current.Lease.SettingsVersion))RevokeTextDrop();
        if(!wanted&&externalDropHover)SetDropHover(false);
        if(dropReconcile.IsCompleted&&(dropPeer==null&&wanted||dropPeer is {Stopping:1}))dropReconcile=ReconcileTextDrop();
        if(wanted&&dropPeer is {Stopping:0} peer&&peer.Client.IsReady&&dropBounds is {} target&&peer.SentTarget!=target)
        {peer.SentTarget=target;peer.Control=PublishDropControl(peer,peer.Control,target);}
        if(!wanted&&state.Mode==TriggerMode.Drop&&state.Activation==Activation.Off&&!exiting)button.ToolTip=dropUnavailable??"请先启用翻译，再投放文字。";
    }
    private async Task StopTextDrop()
    {
        // Latch before any await: late Refresh/Clear cannot race this peer finalizer.
        dropStopping=true;dropPoll.Stop();RevokeTextDrop(true);await dropReconcile;
        if(dropPeer is {} peer){await peer.Run.WaitAsync(TimeSpan.FromSeconds(3));await peer.Control;if(!await peer.Client.TryFinalizeAsync(true))throw new IOException("Drop ownership/registration is unconfirmed.");dropPeer=null;peer.Cancellation.Dispose();}
        if(dropCapture!=null)await dropCapture.DisposeAsync();
    }
}
