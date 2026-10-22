using KuaiYi.App;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class TextDropLifetimeTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task LateRefreshAndClearCannotStartAnotherFinalizerDuringShutdown(bool confirmed)
    {
        var w=new ShellWindow();var run=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var control=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.DropPeerForTest(run.Task,control.Task,()=>{entered.TrySetResult();return release.Task;});var stop=w.StopDropForTest();Assert.True(w.DropStopping);Assert.False(stop.IsCompleted);
        w.RefreshDropForTest();await Assert.ThrowsAsync<IOException>(()=>w.SettleDropForTest());Assert.Equal(0,w.DropReconciliations);Assert.Equal(0,w.DropFinalizations);
        run.SetResult();control.SetResult();await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));w.RefreshDropForTest();await Assert.ThrowsAsync<IOException>(()=>w.SettleDropForTest());Assert.Equal(0,w.DropReconciliations);Assert.Equal(1,w.DropFinalizations);
        release.SetResult(confirmed);if(confirmed)await stop;else await Assert.ThrowsAsync<IOException>(()=>stop);Assert.Equal(!confirmed,w.HasDropPeer);Assert.Equal(confirmed?1:0,w.DropCaptureDisposals);Assert.True(w.DropStopping);
        w.RefreshDropForTest();Assert.Equal(0,w.DropReconciliations);
        if(!confirmed){await Assert.ThrowsAsync<IOException>(()=>w.StopDropForTest());Assert.Equal(2,w.DropFinalizations);Assert.True(w.HasDropPeer);Assert.True(w.DropStopping);}
    }
    [Fact]public async Task OriginalTransportAndCleanupErrorsSurviveUnconfirmedAttemptAndSuccessfulRetry()
    {
        var w=new ShellWindow();var transport=new IOException("original transport");var cleanup=new IOException("original cleanup");int attempt=0;
        w.DropPeerForTest(finalize:()=>++attempt==1?Task.FromException<bool>(cleanup):Task.FromResult(true));w.DropTransportFailureForTest(transport);var settlement=w.PeerSettlementForTest;
        await Assert.ThrowsAsync<IOException>(()=>w.StopDropForTest());Assert.True(w.HasDropPeer);Assert.False(settlement.IsCompleted);
        await w.StopDropForTest();Assert.False(w.HasDropPeer);var error=await Assert.ThrowsAsync<AggregateException>(()=>settlement);Assert.Contains(transport,error.Flatten().InnerExceptions);Assert.Contains(cleanup,error.Flatten().InnerExceptions);Assert.Equal(2,error.Flatten().InnerExceptions.Count);
    }
    [Fact]public async Task ShutdownAndClearOnlyWaitForAlreadyRunningReconcile()
    {
        var w=new ShellWindow();var reconcile=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        w.DropPeerForTest(finalize:()=>{entered.TrySetResult();return release.Task;});w.ExistingDropReconcileForTest(reconcile.Task);var clear=w.SettleDropForTest();var stop=w.StopDropForTest();Assert.False(clear.IsCompleted);Assert.False(stop.IsCompleted);Assert.True(w.DropStopping);w.RefreshDropForTest();Assert.Equal(0,w.DropReconciliations);Assert.Equal(0,w.DropFinalizations);
        reconcile.SetResult();await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));await Assert.ThrowsAsync<IOException>(()=>clear);Assert.Equal(1,w.DropFinalizations);w.RefreshDropForTest();Assert.Equal(0,w.DropReconciliations);release.SetResult(true);await stop;Assert.False(w.HasDropPeer);Assert.Equal(1,w.DropCaptureDisposals);
    }
}
