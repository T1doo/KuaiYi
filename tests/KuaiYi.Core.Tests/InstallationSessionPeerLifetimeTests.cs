using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationSessionTests
{
    [Theory][InlineData("healthy")][InlineData("held-after-close")]
    public async Task PeerFixtureRetirementJoinsActualExitAndOriginalRunWithoutReplayingClose(string mode)
    {
        using var rig=new Rig();var selected=await rig.Prepare(mode);Peer? observed=null;Port? originalPort=null;Task<int>? originalRun=null;
        await WithPeer(rig,async peer=>
        {
            observed=peer;var port=originalPort=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(rig));
            var run=originalRun=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));
            await port.WaitForHealthyAsync(run);await peer.Close();
            Assert.False(peer.OriginalRetirementConfirmed);
            if(mode=="held-after-close")
            {Assert.False(port.OriginalExits.Single().Exit.IsCompleted);Assert.False(run.IsCompleted);rig.Fence();}
            // The wrapper must join the original objects. CLOSED alone is deliberately insufficient.
        });
        Assert.NotNull(observed);Assert.True(observed.OriginalRetirementConfirmed);Assert.True(originalPort!.OriginalExits.Single().Exit.IsCompletedSuccessfully);
        Assert.True(originalRun!.IsCompletedSuccessfully);Assert.Equal(0,await originalRun);
        Assert.Equal(1,observed.SentCommands.Count(command=>command=="CLOSE"));
        Assert.Equal(mode=="held-after-close"?1:0,observed.SentCommands.Count(command=>command=="RELEASE_EXIT"));
        int closes=observed.ResourceCloseAttempts;Task first=observed.DisposeAsync().AsTask(),second=observed.DisposeAsync().AsTask();
        Assert.Same(first,second);await second;Assert.Equal(closes,observed.ResourceCloseAttempts);
        output.WriteLine($"PUBLIC_PEER_RETIREMENT mode={mode} originalExit=true originalRunJoined=true closeRequests=1 releaseExit={(mode=="held-after-close"?1:0)} resourceCloses={closes}");
    }

    [Fact]public async Task PeerFixtureActualDisconnectKeepsBodyFirstAndAllCleanupErrors()
    {
        using var rig=new Rig();using var stop=new CancellationTokenSource();var selected=await rig.Prepare();var body=new InvalidOperationException("PUBLIC_ORIGINAL_BODY_BEFORE_DISCONNECTED_CLEANUP");Peer? observed=null;Port? originalPort=null;
        Exception? result=await Record.ExceptionAsync(()=>WithPeer(rig,async peer=>
        {
            observed=peer;peer.CancelAfterExitRequest=stop;var port=originalPort=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(rig));
            var run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath,stop.Token));await port.WaitForHealthyAsync(run);
            await peer.Send("DISCONNECT_PEER");Assert.Equal("PUBLIC_PIPE_DISCONNECTING",await peer.Row());Assert.Null(await peer.Row());
            throw body;
        }));
        var combined=Assert.IsType<AggregateException>(result);Assert.Same(body,combined.InnerExceptions[0]);
        Assert.Contains(combined.Flatten().InnerExceptions,error=>error is IOException&&!ReferenceEquals(error,body));
        Assert.NotNull(observed);Assert.True(originalPort!.OriginalExits.Single().Exit.IsCompletedSuccessfully);
        Assert.Contains(observed.ExitRequestFailures,error=>error is IOException);
        Assert.True(observed.OriginalRetirementConfirmed);Assert.Equal(4,observed.ResourceCloseAttempts);Assert.True(rig.Keep);
        output.WriteLine("PUBLIC_PEER_DISCONNECT originalBodyFirst=true originalExit=true allFourResourceClosesAttempted=true cleanupWriteFaultRetained=true");
    }

    [Fact]public async Task PeerFixtureRepeatedDisposeJoinsTheSamePendingTaskAndRethrowsOriginalFailure()
    {
        var peer=new Peer("ky-peer-dispose-"+Guid.NewGuid().ToString("N"));var entered=Signal();var release=Signal();
        var original=new IOException("PUBLIC_ORIGINAL_RESOURCE_CLOSE_FAILURE");
        peer.BeforeResourceCloseForTest=async()=>{entered.TrySetResult();await release.Task;throw original;};
        Task first=peer.DisposeAsync().AsTask();await entered.Task.WaitAsync(Limit);Task second=peer.DisposeAsync().AsTask();
        Assert.Same(first,second);Assert.False(first.IsCompleted);release.TrySetResult();
        Exception? one=await Record.ExceptionAsync(()=>first),two=await Record.ExceptionAsync(()=>second);
        Assert.Same(original,one);Assert.Same(one,two);Assert.Same(first,peer.DisposeAsync().AsTask());
        Assert.True(peer.Connected.IsCompleted);Assert.Equal(3,peer.ResourceCloseAttempts);
        output.WriteLine("PUBLIC_PEER_DISPOSE originalPendingTaskShared=true originalFailureInstanceShared=true originalConnectionSettled=true allResourcesAttempted=true");
    }
}
