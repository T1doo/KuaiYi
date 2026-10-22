using System.Text;
using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests
{
    private sealed partial class Rig
    {
        internal InstallationInstallerEntry? Installer;
        internal async Task<InstallationInstallerEntry> PrepareInstaller(bool upgrade=false,InstallationOperationFaults? faults=null,Func<InstallationOperationOutcome,Task>? report=null,byte[]? pins=null)
        {
            var(source,b)=await Sources(upgrade,initialize:upgrade);Peer=new(Pipe);
            return Installer=new(pins??JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=source.Digest,bootstrapSha256=b.Digest}),App,Data,source.Root,b.Root,
                new InstallationDeploymentRegistrySnapshot(Registry),kind=>{FactoryCalls.Add(kind);return Task.FromResult(new InstallationUpgradeCoordinator(App,Data,Port,TimeSpan.FromSeconds(2),Port));},report??(value=>{output.WriteLine("PUBLIC_INSTALLER_STAGE "+value.Status);foreach(var error in value.Errors)output.WriteLine(error.ToString());return Task.CompletedTask;}),Stop.Token,faults);
        }
        internal async Task CleanupInstaller()
        {
            var errors=new List<Exception>();try{Stop.Cancel();}catch(Exception error){errors.Add(error);}
            if(Installer!=null)
            {
                try{await Installer.OriginalWork.WaitAsync(Limit);}catch(Exception error){errors.Add(error);}
                if(!Installer.OriginalWork.IsCompleted){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original installer lifetime pending; root/Stop retained.");}
                Operation=Installer.Operation;
                if(Installer.OriginalWork is{IsCompletedSuccessfully:true}&&Installer.OriginalWork.Result.Stage?.Status==InstallationOperationStatus.Unconfirmed)RestoredRetention=true;
            }
            try{await CleanupOperation();}catch(Exception error){errors.Add(error);}InstallationErrors.Throw(errors);
        }
    }
    private Task ExecuteInstaller(Func<Rig,Task> body)=>Execute(r=>BodyAndCleanup(()=>body(r),r.CleanupInstaller));
    [Fact]public async Task InstallerCancellationSealsAndJoinsTheOriginalThrowingCallbackWithoutDroppingIt()
    {
        var cancel=new InstallationInstallerCancellation();var entered=Signal();var release=Signal();var error=new IOException("PUBLIC_CONSOLE_CANCEL_CALLBACK");
        using var registration=cancel.Token.Register(()=>{entered.TrySetResult();release.Task.GetAwaiter().GetResult();throw error;});
        var original=cancel.Request();await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);Assert.Same(original,cancel.Request());var join=cancel.SealAndJoinAsync();Assert.False(join.IsCompleted);Assert.False(original.IsCompleted);Assert.Same(original,cancel.Request());
        },()=>{release.TrySetResult();return Task.CompletedTask;});
        var errors=await cancel.SealAndJoinAsync().WaitAsync(Limit);Assert.True(original.IsCompleted);Assert.Contains(error,Flatten(errors));Assert.Same(original,cancel.Request());
    }
    [Fact]public Task InstallerUnavailableOrUntrustedPinShapeNeverCreatesRootOrReadsRegistry()=>ExecuteInstaller(async r=>
    {
        byte[] unavailable=File.ReadAllBytes(Path.Combine(MaterializedPayloadSource.Repo,"packaging/installer-source-pins.json"));
        var entry=await r.PrepareInstaller(pins:unavailable);await Assert.ThrowsAsync<InstallationRecoveryException>(()=>entry.Start());
        var settled=await entry.OriginalWork.WaitAsync(Limit);Assert.Equal(3,settled.ExitCode);Assert.Null(entry.Operation);Assert.True(settled.CleanupConfirmed);Assert.False(Directory.Exists(r.App));Assert.Equal(0,r.Registry.Reads);Assert.Empty(r.FactoryCalls);
        foreach(string text in new[]{"{}",new string(' ',4097),Encoding.UTF8.GetString(unavailable).Replace("\"availability\":","\"schemaVersion\":1,\"availability\":"),Encoding.UTF8.GetString(unavailable).Replace("\"availability\":","\"host_permissions\":[\"*\"],\"availability\":"),Encoding.UTF8.GetString(unavailable).Replace("Unavailable","Pinned")})
            Assert.ThrowsAny<Exception>(()=>InstallationInstallerPins.Read(Encoding.UTF8.GetBytes(text)));
    });
    [Fact]public Task InstallerFirstRootFlowsThroughOriginalTicketPrepareCommitReadyAndActualExit()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareInstaller();Assert.False(Directory.Exists(r.App));var result=await entry.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.NewVersionReady,result.Status);Assert.Equal(InstallationDeploymentStatus.Committed,result.Deployment!.Status);Assert.NotNull(result.NewReady);Assert.False(entry.OriginalWork.IsCompleted);Assert.False(File.Exists(InstallationRootInitialization.Pending(r.App)));Assert.Single(Directory.GetFiles(r.App,"root-initialized-*.json"));Assert.Single(Directory.GetFiles(r.App,"deployment-ticket-*.json"));Assert.Single(Directory.GetFiles(r.App,"deployment-committed-*.json"));Assert.Single(r.FactoryCalls);Assert.Null(entry.Operation!.NewController!.WarmImageEndpoint);Assert.Throws<InvalidOperationException>(()=>{_=entry.Start();});
        await r.Peer!.Close();var settled=await entry.OriginalWork.WaitAsync(Limit);Assert.Same(result,settled.Stage);Assert.Equal(0,settled.ExitCode);Assert.True(settled.CleanupConfirmed);Assert.Equal(new[]{0},r.Port.Exits);Assert.Empty(settled.Errors);
    });
    [Fact]public Task InstallerExistingOwnedRootUsesTheSameOperationWithoutReinitialization()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareInstaller(upgrade:true);var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NewVersionReady,result.Status);Assert.Equal("2.0",result.NewReady!.Generation.Version);Assert.Empty(Directory.GetFiles(r.App,"root-initialized-*.json"));await r.Peer!.Close();Assert.Equal(0,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
    });
    [Fact]public Task InstallerExistingEmptyDirectoryIsNeverAdopted()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareInstaller();Directory.CreateDirectory(r.App);var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NotCommitted,result.Status);Assert.Empty(Directory.GetFileSystemEntries(r.App));Assert.Empty(r.FactoryCalls);Assert.Equal(0,r.Registry.Reads);Assert.Equal(6,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
    });
    [Fact]public Task InstallerInitialWriteAndOriginalCloseErrorsAreBothRetainedBeforeAnyTicket()=>ExecuteInstaller(async r=>
    {
        var write=new IOException("PUBLIC_INITIAL_WRITE");var close=new IOException("PUBLIC_INITIAL_CLOSE");string? original=null;
        var entry=await r.PrepareInstaller(faults:new(){Initialization=new(){WriteMetadata=(path,file,bytes)=>{original=path;file.Write(bytes.Span[..1]);throw write;},BeforeClose=(_,path)=>{if(path==original)throw close;}}});
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Contains(write,result.Errors);Assert.Contains(close,result.Errors);Assert.Empty(r.FactoryCalls);Assert.Equal(0,r.Registry.Reads);Assert.False(Directory.Exists(r.App));Assert.NotNull(original);Assert.True(File.Exists(original));Assert.False((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Theory][InlineData(false)][InlineData(true)]public Task InstallerInitialDirectoryMoveUnknownCannotBecomeAnOrdinaryAbort(bool moved)=>ExecuteInstaller(async r=>
    {
        var error=new IOException("PUBLIC_INITIAL_MOVE_UNKNOWN");var entry=await r.PrepareInstaller(faults:new(){Initialization=new(){Move=(from,to)=>{if(moved)Directory.Move(from,to);throw error;}}});
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Deployment!.Status);Assert.Contains(error,result.Errors);Assert.Empty(r.FactoryCalls);Assert.Equal(0,r.Registry.Reads);Assert.Equal(moved,Directory.Exists(r.App));Assert.Equal(3,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
        if(moved){Assert.True(File.Exists(InstallationRootInitialization.Pending(r.App)));Assert.Throws<InstallationRecoveryException>(()=>r.Store.EnterRoleAdmission());Assert.Throws<InstallationRecoveryException>(()=>InstallationStore.OpenExistingSetup(r.App));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));}
    });
    [Theory][InlineData(false)][InlineData(true)]public Task InstallerInitializationHandoffMoveUnknownAlwaysRetainsTheOriginalTicket(bool moved)=>ExecuteInstaller(async r=>
    {
        var error=new IOException("PUBLIC_INITIAL_HANDOFF_UNKNOWN");var entry=await r.PrepareInstaller(faults:new(){Initialization=new(){Move=(from,to)=>{if(Directory.Exists(from))Directory.Move(from,to);else{if(moved)File.Move(from,to);throw error;}}}});
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Contains(error,result.Errors);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));Assert.Empty(r.FactoryCalls);Assert.Equal(0,r.Registry.Reads);Assert.ThrowsAny<IOException>(()=>r.Store.EnterRoleAdmission());Assert.Equal(3,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
    });
    [Fact]public Task InstallerTicketPartialNeverRetiresInitializationOrLetsOtherEntrypointsAdmit()=>ExecuteInstaller(async r=>
    {
        var error=new IOException("PUBLIC_INITIAL_TICKET_PARTIAL");var entry=await r.PrepareInstaller(faults:new(){Deployment=new(){WriteTicket=(file,bytes)=>{file.Write(bytes.AsSpan(..1));throw error;}}});var result=await entry.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Contains(error,result.Errors);Assert.True(File.Exists(InstallationRootInitialization.Pending(r.App)));Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"root-initialized-*.json"));Assert.ThrowsAny<IOException>(()=>r.Store.EnterRoleAdmission());Assert.Empty(r.FactoryCalls);Assert.Equal(0,r.Registry.Reads);
    });
    [Fact]public Task InstallerHeldOriginalInitializationReadCannotGrantAdmissionOrFinishEarly()=>ExecuteInstaller(async r=>
    {
        var entered=Signal();var release=Signal();int once=0;var entry=await r.PrepareInstaller(faults:new(){Initialization=new(){Acquired=(kind,_,_)=>{if(kind=="read"&&Interlocked.Exchange(ref once,1)==0){entered.TrySetResult();release.Task.GetAwaiter().GetResult();}}}});var started=entry.Start();
        await BodyAndCleanup(async()=>{await entered.Task.WaitAsync(Limit);r.Stop.Cancel();Assert.False(started.IsCompleted);Assert.False(entry.OriginalWork.IsCompleted);Assert.False(Directory.Exists(r.App));Assert.Empty(r.FactoryCalls);},()=>{release.TrySetResult();return Task.CompletedTask;});
        Assert.Equal(InstallationOperationStatus.Unconfirmed,(await started.WaitAsync(Limit)).Status);Assert.Equal(3,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);Assert.Equal(0,r.Registry.Reads);
    });
    [Fact]public Task InstallerStatusSinkFailureStillJoinsOriginalHealthySessionAndCannotRewriteReady()=>ExecuteInstaller(async r=>
    {
        var error=new IOException("PUBLIC_STATUS_SINK");var entered=Signal();var entry=await r.PrepareInstaller(report:_=>{entered.TrySetResult();throw error;});var result=await entry.Start().WaitAsync(Limit);await entered.Task.WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NewVersionReady,result.Status);Assert.False(entry.OriginalWork.IsCompleted);Assert.Null(entry.Operation!.Compensation);
        await r.Peer!.Close();var settled=await entry.OriginalWork.WaitAsync(Limit);Assert.Same(result,settled.Stage);Assert.Equal(3,settled.ExitCode);Assert.True(settled.CleanupConfirmed);Assert.Contains(error,settled.Errors);Assert.Null(entry.Operation.Compensation);Assert.Equal(new[]{0},r.Port.Exits);
    });

    [Fact]public Task InstallerUnconfirmedStageCannotReturnWhileOriginalProtocolCleanupIsHeld()=>ExecuteInstaller(async r=>
    {
        // Inline report continuation is a scheduler barrier: the old unfenced entry runs to return on SetResult.
        var reportRelease=new TaskCompletionSource();var reported=Signal();
        r.Port.HealthError=new IOException("PUBLIC_INSTALLER_HEALTH_FAILED");r.Port.HoldIo=true;
        var entry=await r.PrepareInstaller(report:_=>{reported.TrySetResult();return reportRelease.Task;});var started=entry.Start();
        await BodyAndCleanup(async()=>
        {
            await r.Port.IoEntered.Task.WaitAsync(Limit);var result=await started.WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);
            await reported.Task.WaitAsync(Limit);await entry.Operation!.OriginalWork.WaitAsync(Limit);Assert.True(entry.Operation.OriginalWorkPending);
            reportRelease.SetResult();Assert.False(entry.OriginalWork.IsCompleted);
        },()=>{reportRelease.TrySetResult();r.Port.IoRelease.TrySetResult();return Task.CompletedTask;});
        var settled=await entry.OriginalWork.WaitAsync(Limit);Assert.Equal(3,settled.ExitCode);Assert.False(settled.CleanupConfirmed);Assert.False(entry.Operation!.OriginalWorkPending);Assert.True(r.Port.Child!.HasExited);Assert.Equal(r.Port.Child.ExitCode,Assert.Single(r.Port.Exits));
    });
    [Fact]public Task InstallerMismatchedFixedPinsCannotPublishOrStartFromTheSourcesOwnCatalog()=>ExecuteInstaller(async r=>
    {
        var pins=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=new string('A',64),bootstrapSha256=new string('B',64)});
        var entry=await r.PrepareInstaller(pins:pins);var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NotCommitted,result.Status);Assert.NotEmpty(result.Errors);Assert.Null(r.Store.ReadActive());Assert.False(File.Exists(InstallationBootstrapDeployment.ActivePath(r.App)));Assert.False(File.Exists(Path.Combine(r.App,"host-registration.json")));Assert.Equal(0,r.Registry.Writes);Assert.Empty(r.FactoryCalls);Assert.Equal(6,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
    });
    [Fact]public Task InstallerLateStartRemainsOwnedUntilItsOriginalObserverAndActualChildExit()=>ExecuteInstaller(async r=>
    {
        var entered=Signal();var release=Signal();var reportRelease=new TaskCompletionSource();var reported=Signal();
        r.Port.BeforeInstallerStart=()=>{entered.TrySetResult();release.Task.GetAwaiter().GetResult();};
        var entry=await r.PrepareInstaller(report:_=>{reported.TrySetResult();return reportRelease.Task;});var started=entry.Start();
        await BodyAndCleanup(async()=>
        {
            await entered.Task.WaitAsync(Limit);var result=await started.WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);await reported.Task.WaitAsync(Limit);await entry.Operation!.OriginalWork.WaitAsync(Limit);
            Assert.True(entry.Operation.OriginalWorkPending);Assert.Null(r.Port.Child);reportRelease.SetResult();Assert.False(entry.OriginalWork.IsCompleted);
        },()=>{reportRelease.TrySetResult();release.TrySetResult();return Task.CompletedTask;});
        var settled=await entry.OriginalWork.WaitAsync(Limit);Assert.Equal(3,settled.ExitCode);Assert.False(settled.CleanupConfirmed);Assert.False(entry.Operation!.OriginalWorkPending);Assert.NotNull(r.Port.Child);Assert.True(r.Port.Child!.HasExited);Assert.Single(r.FactoryCalls);Assert.Null(entry.Operation.Compensation);Assert.Empty(r.Port.Exits);
    });

}
