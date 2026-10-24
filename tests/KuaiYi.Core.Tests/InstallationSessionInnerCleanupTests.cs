using System.IO.Pipes;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationSessionTests
{
    [Fact]public async Task InnerFixtureCleanupPreservesBodyAndActualDisconnectedReleaseFailure()
    {
        string name="ky-inner-finally-"+Guid.NewGuid().ToString("N");var peer=new Peer(name);
        using var stop=new CancellationTokenSource(Limit);
        using var client=new NamedPipeClientStream(".",name,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await client.ConnectAsync(stop.Token);await peer.Connected.WaitAsync(Limit);client.Dispose();
        var body=new InvalidOperationException("PUBLIC_INNER_ORIGINAL_BODY_PIPE");bool laterCleanup=false;
        Exception? observed=await Record.ExceptionAsync(()=>FixtureBodyAndCleanup(
            ()=>Task.FromException(body),()=>peer.Send("RELEASE_CANCEL"),()=>{laterCleanup=true;return Task.CompletedTask;}));
        Exception? resource=await Record.ExceptionAsync(()=>peer.DisposeAsync().AsTask());
        output.WriteLine($"PUBLIC_INNER_PIPE result={observed?.GetType().Name} bodyFirst={observed is AggregateException aggregate&&ReferenceEquals(body,aggregate.InnerExceptions[0])} laterCleanup={laterCleanup} resource={resource?.GetType().Name}");
        output.WriteLine("PUBLIC_INNER_PIPE_RAW "+observed);
        var combined=Assert.IsType<AggregateException>(observed);Assert.Same(body,combined.InnerExceptions[0]);
        Assert.Contains(combined.InnerExceptions,error=>error is IOException);Assert.True(laterCleanup);
        Assert.True(peer.Connected.IsCompleted);Assert.Equal(4,peer.ResourceCloseAttempts);Assert.NotNull(resource);
    }

    [Fact]public async Task InnerFixtureCleanupPreservesBodyAndActualCoordinatorDisposeFailureAndContinues()
    {
        using var rig=new Rig();var selected=await rig.Prepare();
        await WithPeer(rig,async peer=>
        {
            var port=peer.Own(new Port(output){HoldIo=true});var coordinator=peer.Own(port.Coordinator(rig));
            var run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));
            await port.WaitForHealthyAsync(run);await peer.Close();await port.IoEntered.Task.WaitAsync(Limit);
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));
            var body=new InvalidOperationException("PUBLIC_INNER_ORIGINAL_BODY_COORDINATOR");bool laterCleanup=false;int disposals=0;
            Exception? observed=await Record.ExceptionAsync(()=>FixtureBodyAndCleanup(
                ()=>Task.FromException(body),
                async()=>{disposals++;await coordinator.DisposeAsync();},
                ()=>{laterCleanup=true;port.IoRelease.TrySetResult();return Task.CompletedTask;},
                async()=>{disposals++;await coordinator.DisposeAsync();}));
            // Exact-owner rescue for the old red path, whose first throwing finally skipped the release.
            // It does not change laterCleanup/disposals and cannot turn the red assertions into a pass.
            port.IoRelease.TrySetResult();await coordinator.DisposeAsync();
            output.WriteLine($"PUBLIC_INNER_COORDINATOR result={observed?.GetType().Name} bodyFirst={observed is AggregateException aggregate&&ReferenceEquals(body,aggregate.InnerExceptions[0])} laterCleanup={laterCleanup} disposals={disposals} originalExit={port.OriginalExits.Single().Exit.IsCompletedSuccessfully}");
            output.WriteLine("PUBLIC_INNER_COORDINATOR_RAW "+observed);
            var combined=Assert.IsType<AggregateException>(observed);Assert.Same(body,combined.InnerExceptions[0]);
            Assert.Contains(combined.InnerExceptions,error=>error is InstallationUpgradeUnconfirmedException);
            Assert.True(laterCleanup);Assert.Equal(2,disposals);Assert.True(port.OriginalExits.Single().Exit.IsCompletedSuccessfully);
        });
    }
}
