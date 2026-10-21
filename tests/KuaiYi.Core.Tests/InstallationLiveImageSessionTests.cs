using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed partial class InstallationSessionTests
{
    [Fact]public async Task AnUnstartedCoordinatorCannotMintALiveImageSessionFromProofOrProcessClaims()
    {
        using var rig=new Rig();var coordinator=new InstallationUpgradeCoordinator(rig.App,rig.Data,new InstallationApplicationClient(),TimeSpan.FromSeconds(1));
        Assert.Throws<InstallationRecoveryException>(()=>new InstallationUpgradeCoordinator.LiveImageSession(coordinator));await coordinator.DisposeAsync();
    }
    [Fact]public async Task AnIdleWarmChildCanCloseOverOriginalStdioWithoutGivingItsLegacyControllerWarmAuthority()
    {
        using var rig=new Rig();var selected=await rig.Prepare("warm-healthy");await using var peer=new Peer(rig.Pipe);var starter=new Port(output);
        var coordinator=new InstallationUpgradeCoordinator(rig.App,rig.Data,starter,TimeSpan.FromSeconds(10),starter);Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);
        await starter.WaitForHealthyAsync(run);await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));await coordinator.DisposeAsync();Assert.False(File.Exists(rig.Owner));Assert.True(starter.WarmActualExitObserved);Assert.Equal(0,starter.WarmActualExitCode);
    }
    [Fact]public async Task AnIdleHandoffSealNeverWritesToTheClosedOriginalChannelAndCannotSealAUsedActor()
    {
        var proof=InstallationWarmImageTests.Proof();int writes=0;
        var idle=new InstallationWarmImageControl(proof,TimeProvider.System,()=>1,(_,_,_,_)=>Task.CompletedTask,_=>ValueTask.FromResult(true),(_,_)=>{writes++;throw new IOException("PUBLIC_CLOSED_RUNTIME_CHANNEL");});
        idle.SealUnusedForHandoff();await idle.SettleAsync();Assert.Equal(0,writes);Assert.Throws<InvalidDataException>(idle.SealUnusedForHandoff);
        var issuer=new InstallationWarmImageAdmission(proof,TimeProvider.System);var issued=issuer.Reserve(new(),1);var granted=Signal();
        var used=new InstallationWarmImageControl(proof,TimeProvider.System,()=>1,(_,_,_,_)=>Task.CompletedTask,_=>ValueTask.FromResult(true),(line,_)=>{if(line.StartsWith("WARM_GRANTED|"))granted.TrySetResult();return Task.CompletedTask;});
        used.Handle("WARM_RESERVE|"+InstallationWarmImageWire.Encode(issued.Grant.Identity));await granted.Task.WaitAsync(Limit);Assert.Throws<InvalidDataException>(used.SealUnusedForHandoff);Assert.True(used.Admission.Busy);await used.SettleAsync().WaitAsync(Limit);await issuer.Revoke(issued);await issuer.RetireAsync(issued,()=>ValueTask.FromResult(true),_=>Task.CompletedTask);
    }
    [Fact]public async Task RealOriginalSessionCapabilityChecksHeldRecordsAndJoinsItsExactEndpointBeforeOwnerRelease()
    {
        // Actual owned fixture child / original production stdio. The injected endpoint is managed-only, no Windows peer claim.
        using var rig=new Rig();var selected=await rig.Prepare();await using var peer=new Peer(rig.Pipe);var starter=new Port(output);
        var captured=new TaskCompletionSource<InstallationUpgradeCoordinator.LiveImageSession>(TaskCreationOptions.RunContinuationsAsynchronously);var sealedEndpoint=Signal();var release=Signal();
        var coordinator=new InstallationUpgradeCoordinator(rig.App,rig.Data,new InstallationApplicationClient(),TimeSpan.FromSeconds(10),starter)
        {
            WarmImageEndpoint=async(cap,token)=>{using var registration=token.Register(()=>sealedEndpoint.TrySetResult());captured.SetResult(cap);await release.Task;token.ThrowIfCancellationRequested();}
        };
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath);Exception? body=null;List<Exception> cleanup=[];bool close=false;
        try
        {
            var cap=await captured.Task.WaitAsync(Limit);Assert.Same(starter.Children.Single(),cap.OriginalChild);Assert.True(cap.MemoryCurrent);await cap.VerifyAsync(default);rig.Fence();
            string pending=Path.Combine(rig.App,"upgrade-pending.json");File.WriteAllText(pending,"PUBLIC_PENDING");Assert.Throws<InstallationRecoveryException>(cap.RequireRecords);File.Delete(pending);cap.RequireRecords();
            string executable=Path.Combine(selected.DirectoryPath,"KuaiYi.App.exe");byte[] original=File.ReadAllBytes(executable);
            try{File.AppendAllText(executable,"PUBLIC_CHANGED");await Assert.ThrowsAsync<InvalidDataException>(()=>cap.VerifyAsync(default));}finally{File.WriteAllBytes(executable,original);}
            await cap.VerifyAsync(default);await peer.Close();close=true;await sealedEndpoint.Task.WaitAsync(Limit);await cap.OriginalChild.WaitForExitAsync().WaitAsync(Limit);
            Assert.False(cap.MemoryCurrent);Assert.True(cap.Token.IsCancellationRequested);Assert.False(run.IsCompleted);rig.Fence();release.SetResult();Assert.Equal(0,await run.WaitAsync(Limit));Assert.False(File.Exists(rig.Owner));
        }
        catch(Exception error){body=error;}
        finally
        {
            release.TrySetResult();if(!close)try{await peer.Close();}catch(Exception error){cleanup.Add(error);}
            try{await run.WaitAsync(Limit);}catch(Exception error){cleanup.Add(error);}try{await coordinator.DisposeAsync();}catch(Exception error){cleanup.Add(error);}
        }
        if(body!=null&&cleanup.Count!=0)throw new AggregateException(new[]{body}.Concat(cleanup));if(body!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(body).Throw();if(cleanup.Count!=0)throw new AggregateException(cleanup);
    }
}
