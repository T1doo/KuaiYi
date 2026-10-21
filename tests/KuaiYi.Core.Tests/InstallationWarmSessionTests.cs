using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationSessionTests
{
    private sealed class WarmCase(Rig rig,Peer peer,Port port,InstallationWarmImageClient warm,Task<int> run,CancellationTokenSource lifetime)
    {
        internal Rig Rig=>rig;internal Peer Peer=>peer;internal Port Port=>port;internal InstallationWarmImageClient Warm=>warm;internal Task<int> Run=>run;internal CancellationTokenSource Lifetime=>lifetime;
        internal bool CloseSent,ClosedSeen;
        internal async Task Row(string command,string expected){await peer.Send(command);Assert.Equal(expected,await peer.Row());}
    }
    private async Task WarmScenario(string mode,bool failure,Func<WarmCase,Task> body)
    {
        using var rig=new Rig();var selected=await rig.Prepare(mode);var peer=new Peer(rig.Pipe);var port=new Port(output);
        var coordinator=new InstallationUpgradeCoordinator(rig.App,rig.Data,port,TimeSpan.FromSeconds(10),port);using var lifetime=new CancellationTokenSource();
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,lifetime.Token);Exception? primary=null;List<Exception> cleanup=[];WarmCase? test=null;
        try{await port.WaitForHealthyAsync(run);test=new(rig,peer,port,port.Warm(),run,lifetime);await body(test);}
        catch(Exception error){primary=error;}
        finally
        {
            foreach(string release in new[]{"RELEASE_WARM_WORK","RELEASE_WARM_CALLBACK","RELEASE_WARM_WRITE","RELEASE_WARM_FINALIZE"})
                try{if(test?.CloseSent!=true&&test?.ClosedSeen!=true&&!port.WarmActualExitObserved&&!run.IsCompleted)await peer.Send(release);}catch(IOException)when(port.WarmActualExitObserved){}catch(Exception error){cleanup.Add(error);}
            try
            {
                if(mode=="warm-false-finalize"&&test?.CloseSent!=true&&!port.WarmActualExitObserved)
                {await peer.Send("RETRY_WARM_CLEANUP");Assert.StartsWith("PUBLIC_WARM_RETRY|",await peer.Row());}
                if(test?.CloseSent!=true&&test?.ClosedSeen!=true&&!port.WarmActualExitObserved){await peer.Send("CLOSE");if(test!=null)test.CloseSent=true;}
                if(test?.ClosedSeen!=true){Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());if(test!=null)test.ClosedSeen=true;}
                if(failure)await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));else Assert.Equal(0,await run.WaitAsync(Limit));
                Assert.True(port.WarmActualExitObserved);Assert.Equal(0,port.WarmActualExitCode);
                if(failure){Assert.True(File.Exists(rig.Owner));rig.Fence();}else{Assert.False(File.Exists(rig.Owner));Assert.Equal(1,port.SessionIoCalls);}
            }
            catch(Exception error){cleanup.Add(error);}
            try{await coordinator.DisposeAsync();}catch(InstallationBusyException)when(failure&&port.WarmActualExitObserved){rig.Fence();}
            catch(InstallationUpgradeUnconfirmedException error)when(failure&&port.WarmActualExitObserved&&error.InnerException is InstallationBusyException)
            {
                rig.Fence();if(test?.Warm.Active is {} held){Assert.True(held.Lease.Originals.All(t=>t.IsCompleted));Assert.True(held.Finish?.IsFaulted==true);}
                output.WriteLine("PUBLIC_EXPECTED_PROTOCOL_UNCONFIRMED originalWarmTasksCompleted=true ownerFenceRetained=true");
            }catch(Exception error){cleanup.Add(error);}
            try{await peer.DisposeAsync();}catch(Exception error){cleanup.Add(error);}
            output.WriteLine($"PUBLIC_WARM_SESSION syntheticPreparation=true actualChildExit={(port.WarmActualExitObserved?port.WarmActualExitCode:-1)} expectedProtocolFailure={failure} driverSha256={Hash(Driver)} coreSha256={Hash(Path.Combine(Path.GetDirectoryName(Driver)!,"KuaiYi.Core.dll"))}");
        }
        if(primary!=null&&cleanup.Count!=0)throw new AggregateException(new[]{primary}.Concat(cleanup));
        if(primary!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primary).Throw();if(cleanup.Count!=0)throw new AggregateException(cleanup);
    }
    [Fact]public Task WarmSyntheticPreparationUsesTheSameOriginalChildTwiceWithSeparateOneShotConnections()=>WarmScenario("warm-healthy",false,async test=>
    {
        var connection=new InstallationWarmConnection();var request=await test.Warm.ReserveAsync(connection).WaitAsync(Limit);
        var result=await test.Warm.CommitAsync(request,ImagePath).WaitAsync(Limit);Assert.Equal(InstallationWarmOutcome.PreviewReady,result.Outcome);Assert.NotEqual(Guid.Empty,result.Source);Assert.True(await request.Finish!.WaitAsync(Limit));
        await test.Row("WAIT_WARM_IDLE","PUBLIC_WARM_ORIGINAL_SETTLED");await Assert.ThrowsAsync<InvalidOperationException>(()=>test.Warm.ReserveAsync(connection));
        var second=await test.Warm.ReserveAsync(new()).WaitAsync(Limit);Assert.True(second.Lease.Grant.Identity.Ordinal>request.Lease.Grant.Identity.Ordinal);
        var next=await test.Warm.CommitAsync(second,ImagePath).WaitAsync(Limit);Assert.NotEqual(result.Source,next.Source);Assert.True(await second.Finish!.WaitAsync(Limit));await test.Row("WAIT_WARM_IDLE","PUBLIC_WARM_ORIGINAL_SETTLED");
        await test.Row("WARM_STATUS","PUBLIC_WARM|busy=False|prepares=2|accepted=2|discarded=0|finalized=2");Assert.Single(test.Port.Children);test.Rig.Fence();
        foreach(var p in Directory.GetFiles(test.Rig.App,"*.json"))Assert.DoesNotContain(ImagePath,File.ReadAllText(p));Assert.Equal("PUBLIC_OFF",File.ReadAllText(Path.Combine(test.Rig.Data,"PUBLIC_SENTINEL")));
    });
    [Theory][InlineData("warm-held-work")][InlineData("warm-held-callback")]
    public Task CancellationRetainsTheOriginalSyntheticOwnerAndActualChildUntilWorkAndCallbackSettle(string mode)=>WarmScenario(mode,false,async test=>
    {
        var request=await test.Warm.ReserveAsync(new()).WaitAsync(Limit);var commit=test.Warm.CommitAsync(request,ImagePath);
        await test.Row("WAIT_WARM_PREPARE","PUBLIC_SYNTHETIC_WARM_PREPARE");Task cancel=test.Warm.Cancel(request);await cancel.WaitAsync(Limit);
        await test.Row("WAIT_WARM_CALLBACK","PUBLIC_SYNTHETIC_WARM_CANCEL_HELD");Assert.True(request.Lease.Token.IsCancellationRequested);Assert.True(test.Warm.Busy);Assert.False(commit.IsCompleted);
        Assert.True(File.Exists(Path.Combine(test.Rig.Root,"PUBLIC_SYNTHETIC_WARM_OWNER")));Assert.False(test.Run.IsCompleted);test.Rig.Fence();
        await test.Peer.Send("RELEASE_WARM_WORK");await test.Peer.Send("RELEASE_WARM_CALLBACK");await Assert.ThrowsAsync<IOException>(()=>commit.WaitAsync(Limit));
        Assert.True(await request.Finish!.WaitAsync(Limit));await test.Row("WAIT_WARM_IDLE","PUBLIC_WARM_ORIGINAL_SETTLED");
        await test.Row("WARM_STATUS","PUBLIC_WARM|busy=False|prepares=1|accepted=0|discarded=1|finalized=1");Assert.False(File.Exists(Path.Combine(test.Rig.Root,"PUBLIC_SYNTHETIC_WARM_OWNER")));
    });
    [Theory][InlineData("warm-result-write")][InlineData("warm-result-flush")]
    public Task OriginalChildWriteOrFlushBackpressureBlocksCleanExitAndKeepsTheInstallationLease(string mode)=>WarmScenario(mode,false,async test=>
    {
        var request=await test.Warm.ReserveAsync(new()).WaitAsync(Limit);var commit=test.Warm.CommitAsync(request,ImagePath);
        await test.Row("WAIT_WARM_WRITE","PUBLIC_ORIGINAL_WARM_WRITE_HELD");Assert.Equal(InstallationWarmOutcome.PreviewReady,(await commit.WaitAsync(Limit)).Outcome);
        await test.Row("WARM_STATUS","PUBLIC_WARM|busy=True|prepares=1|accepted=1|discarded=0|finalized=1");
        await test.Peer.Send("CLOSE");test.CloseSent=true;await test.Row("WAIT_CLEANUP","PUBLIC_CLEANUP_ENTERED");
        Assert.False(test.Run.IsCompleted);Assert.False(test.Port.Children.Single().HasExited);test.Rig.Fence();await test.Peer.Send("RELEASE_WARM_WRITE");
    });
    [Fact]public Task LostReceiptDoesNotReplayOrRollBackAppOwnedSourceAndCannotClaimCleanProtocolSettlement()=>WarmScenario("warm-drop-result",true,async test=>
    {
        var request=await test.Warm.ReserveAsync(new()).WaitAsync(Limit);var commit=test.Warm.CommitAsync(request,ImagePath);await test.Row("WAIT_WARM_WRITE","PUBLIC_ORIGINAL_WARM_WRITE_HELD");await test.Row("WAIT_WARM_IDLE","PUBLIC_WARM_ORIGINAL_SETTLED");
        Assert.False(commit.IsCompleted);Assert.True(test.Warm.Busy);await test.Warm.Cancel(request).WaitAsync(Limit);
        await test.Row("WARM_STATUS","PUBLIC_WARM|busy=False|prepares=1|accepted=1|discarded=0|finalized=1");test.Rig.Fence();
        await test.Peer.Send("CLOSE");test.CloseSent=true;Assert.Equal("PUBLIC_SHELL_CLOSED",await test.Peer.Row());test.ClosedSeen=true;
        await Assert.ThrowsAsync<EndOfStreamException>(()=>commit.WaitAsync(Limit));await Assert.ThrowsAsync<AggregateException>(()=>request.Finish!);Assert.True(request.Finish!.IsFaulted);Assert.True(test.Warm.Busy);Assert.Single(File.ReadAllLines(Path.Combine(test.Rig.Root,"PUBLIC_SHELL_CREATED")));
    });
    [Fact]public Task AFalseOwnerFinalizationCanOnlyRetryTheSameOwnerAndNeverRepeatPreparation()=>WarmScenario("warm-false-finalize",false,async test=>
    {
        var request=await test.Warm.ReserveAsync(new()).WaitAsync(Limit);var commit=test.Warm.CommitAsync(request,ImagePath);
        await test.Row("WAIT_WARM_FINALIZE","PUBLIC_SYNTHETIC_FINALIZE_OBSERVED");await test.Row("WAIT_WARM_IDLE","PUBLIC_WARM_CLEANUP_UNCONFIRMED");Assert.False(commit.IsCompleted);Assert.True(test.Warm.Busy);test.Rig.Fence();
        Assert.True(File.Exists(Path.Combine(test.Rig.Root,"PUBLIC_SYNTHETIC_WARM_OWNER")));await test.Peer.Send("RELEASE_WARM_FINALIZE");await test.Row("RETRY_WARM_CLEANUP","PUBLIC_WARM_RETRY|True");
        Assert.Equal(InstallationWarmOutcome.Rejected,(await commit.WaitAsync(Limit)).Outcome);Assert.True(await request.Finish!.WaitAsync(Limit));await test.Row("WAIT_WARM_IDLE","PUBLIC_WARM_ORIGINAL_SETTLED");
        await test.Row("WARM_STATUS","PUBLIC_WARM|busy=False|prepares=1|accepted=0|discarded=1|finalized=2");
    });
    [Theory][InlineData("IMAGE")][InlineData("OFFER")]
    public Task AConsumedWarmRequestNeverReopensTheColdImageOrHandoffPath(string command)=>WarmScenario("warm-healthy",true,async test=>
    {
        var request=await test.Warm.ReserveAsync(new()).WaitAsync(Limit);await test.Warm.CommitAsync(request,ImagePath).WaitAsync(Limit);Assert.True(await request.Finish!.WaitAsync(Limit));await test.Row("WAIT_WARM_IDLE","PUBLIC_WARM_ORIGINAL_SETTLED");
        var proof=test.Port.Proof!;await SendControl(test.Port,command=="IMAGE"?"IMAGE|"+new InstallationImageOffer(proof.Launch,proof.Fingerprint,Guid.NewGuid(),ImagePath).Encode():"OFFER|PUBLIC_INVALID_OFFER");
        Assert.Equal("PUBLIC_SHELL_CLOSED",await test.Peer.Row());test.ClosedSeen=true;
    });
    [Fact]public Task NormalPublicRunDoesNotOptInToTheInternalWarmActor()=>WarmScenario("healthy",true,async test=>
    {await Assert.ThrowsAsync<EndOfStreamException>(()=>test.Warm.ReserveAsync(new()).WaitAsync(Limit));Assert.Equal("PUBLIC_SHELL_CLOSED",await test.Peer.Row());test.ClosedSeen=true;Assert.False(File.Exists(Path.Combine(test.Rig.Root,"PUBLIC_SYNTHETIC_WARM_OWNER")));});
}
