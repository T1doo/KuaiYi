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
        var state=lifecycle.Gate.Snapshot;bool wanted=dropVisible&&(state.Activation==Activation.On||imageBitmapDropEnabled)&&state.Mode==TriggerMode.Drop&&state.Suspension==Suspension.None;
        if(dropPeer is {} current&&(!wanted||dropCapture.Lease!=current.Lease||dropCapture.IsLeaseCurrent(current.Lease)!=true||lifecycle.Settings?.Version!=current.Lease.SettingsVersion))RevokeTextDrop();
        if(!wanted&&externalDropHover)SetDropHover(false);
        if(dropReconcile.IsCompleted&&(dropPeer==null&&wanted||dropPeer is {Stopping:1}))dropReconcile=ReconcileTextDrop();
        if(wanted&&dropPeer is {Stopping:0} peer&&peer.Client.IsReady&&dropBounds is {} target&&peer.SentTarget!=target)
        {peer.SentTarget=target;peer.Control=PublishDropControl(peer,peer.Control,target);}
        if(!wanted&&state.Mode==TriggerMode.Drop&&state.Activation==Activation.Off&&!exiting)button.ToolTip=dropUnavailable??"请先启用翻译，再投放。";
    }
    private async Task<bool> SettleTextDropPeer(TextDropPeer peer)
    {
        // Keep the original transport failure separate. Repeated polls never replace it with a cleanup wrapper.
        var errors=peer.SettlementFailures;
        // Store original leaves, never the newly created Task.Exception/diagnostic wrapper on each poll.
        void Remember(Exception error)
        {
            IEnumerable<Exception> originals=error is AggregateException aggregate?aggregate.Flatten().InnerExceptions:[error];
            foreach(var original in originals)if(!errors.Any(previous=>ReferenceEquals(previous,original)))errors.Add(original);
        }
        bool joined=true;
        try{await peer.Run.WaitAsync(TimeSpan.FromSeconds(3));}catch(TimeoutException error)when(!peer.Run.IsCompleted){peer.WaitTimeout??=error;Remember(peer.WaitTimeout);joined=false;}catch(Exception error){Remember(peer.Run.Exception??error);}
        try{await peer.Control;}catch(Exception error){Remember(peer.Control.Exception??error);}
        bool finalized=false;try{finalized=await peer.Client.TryFinalizeAsync(true);}catch(Exception error){Remember(error);}
        if(!joined||!finalized){button.ToolTip="拖放进程、注册、IO 或交付清理尚未确认。";return false;}
        List<Exception> complete=[..errors];if(peer.Failure!=null&&!complete.Any(error=>ReferenceEquals(error,peer.Failure)))complete.Insert(0,peer.Failure);
        if(ReferenceEquals(dropPeer,peer)){dropPeer=null;peer.Cancellation.Dispose();}
        if(complete.Count==0)peer.Settled.TrySetResult();else{var error=new AggregateException("Original Drop settlement failed.",complete);peer.Settled.TrySetException(error);_=peer.Settled.Task.Exception;Report("拖放失败；原清理已确认，位图未发送",error);}
        return true;
    }
    private async Task StopTextDrop()
    {
        // Latch before any await: late Refresh/Clear cannot race this peer finalizer.
        dropStopping=true;dropPoll.Stop();RevokeTextDrop(true);await dropReconcile;
        if(dropPeer is {} peer&&!await SettleTextDropPeer(peer))throw new IOException("Drop ownership/registration is unconfirmed.");
        if(dropCapture!=null)await dropCapture.DisposeAsync();
    }
}
