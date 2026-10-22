// Native receiver/capture adapters only; scheduling and shutdown are the linked App methods.
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private sealed class DropCapturePort:IAsyncDisposable
    {
        public DropCaptureLease? Lease {get;set;}
        public bool IsLeaseCurrent(DropCaptureLease lease)=>Lease==lease;
        public int Disposals;
        public ValueTask DisposeAsync(){Disposals++;return ValueTask.CompletedTask;}
    }
    private sealed class DropClientPort
    {
        public bool IsReady=>false;
        public Func<Task<bool>> Finalize=()=>Task.FromResult(true);
        public int Finalizations;
        public Task<bool> TryFinalizeAsync(bool retry){if(!retry)throw new InvalidOperationException();Finalizations++;return Finalize();}
    }
    private sealed class TextDropPeer
    {
        public DropClientPort Client {get;}=new();
        public DropCaptureLease Lease {get;}=new(Guid.NewGuid(),1,default,"");
        public CancellationTokenSource Cancellation {get;}=new();
        public Task Run=Task.CompletedTask,Control=Task.CompletedTask;
        public DropTargetGeometry? SentTarget=null;
        public Exception? Failure=null;public List<Exception> SettlementFailures{get;}=[];public TimeoutException? WaitTimeout=null;public TaskCompletionSource Settled{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Stopping=1;
    }
    private readonly TimerPort dropPoll=new();
    private DropCapturePort? dropCapture=null;
    private TextDropPeer? dropPeer=null;
    private Task dropReconcile=Task.CompletedTask;
    private bool imageBitmapDropEnabled=false;
    private bool dropVisible,geometryReady=true;
    private string? dropUnavailable=null;
    private DropTargetGeometry? dropBounds=null;
    private Task ReconcileTextDrop(){DropReconciliations++;return DropCleanup;}
    private Task PublishDropControl(TextDropPeer peer,Task previous,DropTargetGeometry target)=>previous;
    private void SetDropHover(bool show)=>externalDropHover=show;
    internal int DropReconciliations {get;private set;}
    internal void DropPeerForTest(Task? run=null,Task? control=null,Func<Task<bool>>? finalize=null)
    {dropCapture=new();dropPeer=new(){Run=run??Task.CompletedTask,Control=control??Task.CompletedTask};if(finalize!=null)dropPeer.Client.Finalize=finalize;}
    internal void ExistingDropReconcileForTest(Task pending)=>dropReconcile=pending;
    internal Task PeerSettlementForTest=>dropPeer!.Settled.Task;
    internal void DropTransportFailureForTest(Exception error)=>dropPeer!.Failure=error;
    internal Task StopDropForTest()=>StopTextDrop();
    internal Task SettleDropForTest()=>SettleClearedTextDrop();
    internal void RefreshDropForTest()=>RefreshTextDrop();
    internal bool DropStopping=>dropStopping;
    internal bool HasDropPeer=>dropPeer!=null;
    internal int DropFinalizations=>dropPeer?.Client.Finalizations??0;
    internal int DropCaptureDisposals=>dropCapture?.Disposals??0;
}
