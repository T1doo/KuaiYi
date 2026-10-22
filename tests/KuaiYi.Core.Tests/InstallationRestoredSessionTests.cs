using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests
{
    private sealed partial class Rig
    {
        internal bool RestoredRetention,SkipPreviousBootstrap,SkipPreviousHost;
        internal Port? RestoredPort;
        internal InstallationRestoredSession? Restored;
        internal InstallationUpgradeCoordinator? RestoredController;
        internal async Task<InstallationRestoredSession?> BeginRestored(Func<InstallationControlledLaunch,CancellationToken,Task>? publish=null,
            Action<Port>? configure=null,InstallationPreparationFaults? readFaults=null,InstallationAdmissionFaults? admissionFaults=null,InstallationExecutionFaults? executionFaults=null)
        {
            if(Peer!=null){await Peer.DisposeAsync();Peer=null;}
            RestoredPort=new(output);configure?.Invoke(RestoredPort);RestoredController=new(App,Data,RestoredPort,TimeSpan.FromSeconds(2),RestoredPort);
            if(OriginalBefore(this).Current!=null)Peer=new(Pipe);
            return Restored=Compensation!.BeginRestoredSession(RestoredController,publish??((_,_)=>Task.CompletedTask),Stop.Token,readFaults,admissionFaults,executionFaults);
        }
        internal async Task CleanupRestored()
        {
            var errors=new List<Exception>();RestoredPort?.HealthRelease.TrySetResult();RestoredPort?.IoRelease.TrySetResult();
            try{Stop.Cancel();}catch(Exception error){errors.Add(error);}
            if(Restored!=null)
            {
                try{await Restored.Completion.WaitAsync(Limit);}catch(Exception error){errors.Add(error);}
                if(!Restored.SessionWorkSettled)RestoredRetention=true;
                if(Restored.OriginalWorkPending){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original restored fixture task is pending; Rig/Stop/root retained.");}
            }
            if(RestoredController!=null)
            {
                bool owns=Restored!=null&&RestoredController.RestoredOwnsExecution(Restored);
                try{await RestoredController.DisposeAsync();if(owns)errors.Add(new InvalidOperationException("Restored original execution was abandoned."));}
                catch(InstallationUpgradeUnconfirmedException)when(owns){RestoredRetention=true;}
                catch(Exception error){errors.Add(error);}
            }
            InstallationErrors.Throw(errors);
        }
    }
    private Task ExecuteRestored(Func<Rig,Task> body)=>Execute(r=>BodyAndCleanup(()=>body(r),r.CleanupRestored));
    private async Task Compensated(Rig r,bool upgrade=true)
    {
        try{await FailedDeployment(r,upgrade);}catch{if(r.Session?.Completion is{IsCompletedSuccessfully:true})foreach(var error in r.Session.Completion.Result.Errors)output.WriteLine("PUBLIC_ORIGINAL_SESSION_FAILURE "+error);throw;}r.Compensation=r.Session!.BeginCompensation();
        var outcome=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Compensated,outcome.Status);Assert.True(r.Compensation.CleanupConfirmed);
    }
    [Fact]public Task RestoredCompensationIsNotReadyAndOriginalOldGenerationMustPassNewControlledSession()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var failed=r.Port.Proof!;byte[] retired=File.ReadAllBytes(r.Compensation!.RetiredPath);
        var restored=(await r.BeginRestored(configure:p=>p.HoldHealth=true))!;await r.RestoredPort!.Healthy.Task.WaitAsync(Limit);
        Assert.False(restored.Ready.IsCompleted);Assert.False(File.Exists(Path.Combine(r.App,"browser-host-session.json")));Assert.NotSame(r.Controller,r.RestoredController);Assert.NotEqual(failed.Launch,r.RestoredPort.Proof!.Launch);Assert.NotEqual(retired,File.ReadAllBytes(r.Owner));
        r.RestoredPort.HealthRelease.TrySetResult();var ready=await restored.Ready.WaitAsync(Limit);
        Assert.Equal(r.Compensation.Operation,ready.CompensationOperation);Assert.Equal(r.Scope.Operation,ready.DeploymentOperation);Assert.Equal("1.0",ready.Generation.Version);Assert.Equal(r.Prepared.Plan.Host.Before!.Version,ready.Generation);Assert.Equal(r.RestoredPort.Proof.Launch,ready.Launch);Assert.Equal(r.RestoredPort.Proof.Fingerprint,ready.Proof);Assert.Null(r.RestoredPort.Proof.UpgradeRecord);Assert.True(File.Exists(Path.Combine(r.App,"browser-host-session.json")));
        await r.Peer!.Close();var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.CleanExit,result.Status);Assert.Same(ready,result.Ready);Assert.Empty(result.Errors);Assert.True(restored.SessionWorkSettled);Assert.False(File.Exists(r.Owner));Assert.Equal(new[]{0},r.RestoredPort.Exits);Assert.Equal(retired,File.ReadAllBytes(r.Compensation.RetiredPath));Assert.Equal(InstallationCompensationStatus.Compensated,r.Compensation.Completion.Result.Status);
    });
    [Fact]public Task RestoredFirstInstallationAbsenceCreatesNoPreviousSessionTaskOrExecution()=>ExecuteRestored(async r=>
    {
        await Compensated(r,false);Assert.Null(await r.BeginRestored());Assert.Equal(0,r.RestoredPort!.Starts);Assert.Null(r.Peer);Assert.False(File.Exists(r.Owner));Assert.Null(r.Store.ReadActive());
        Assert.Throws<InstallationRecoveryException>(()=>r.Compensation!.BeginRestoredSession(r.RestoredController!,(_,_)=>Task.CompletedTask));
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task RestoredUncertainArchiveCannotIssueAuthorityEvenIfArchiveActuallyExists(bool moved)=>ExecuteRestored(async r=>
    {
        await FailedDeployment(r,true);r.Compensation=r.Session!.BeginCompensation(faults:new(){Archive=(from,to)=>{if(moved)File.Move(from,to);throw new IOException("PUBLIC_RESTORED_ARCHIVE_UNKNOWN");}});
        Assert.Equal(InstallationCompensationStatus.Unconfirmed,(await r.Compensation.Completion.WaitAsync(Limit)).Status);
        r.RestoredPort=new(output);r.RestoredController=new(r.App,r.Data,r.RestoredPort,TimeSpan.FromSeconds(2),r.RestoredPort);
        Assert.Throws<InstallationRecoveryException>(()=>r.Compensation.BeginRestoredSession(r.RestoredController,(_,_)=>Task.CompletedTask));Assert.Equal(0,r.RestoredPort.Starts);
    });
    [Fact]public Task RestoredRejectsFailedControllerAndDuplicateGrantWithoutReusingOldAuthority()=>ExecuteRestored(async r=>
    {
        await Compensated(r);Assert.Throws<InstallationRecoveryException>(()=>r.Compensation!.BeginRestoredSession(r.Controller!,(_,_)=>Task.CompletedTask));
        var restored=(await r.BeginRestored())!;await restored.Ready.WaitAsync(Limit);
        Assert.Throws<InstallationRecoveryException>(()=>r.Compensation!.BeginRestoredSession(r.RestoredController!,(_,_)=>Task.CompletedTask));Assert.Equal(1,r.RestoredPort!.Starts);
        await r.Peer!.Close();Assert.Equal(InstallationRestoredSessionStatus.CleanExit,(await restored.Completion.WaitAsync(Limit)).Status);
    });
    [Theory][InlineData("current")][InlineData("active")][InlineData("host")][InlineData("registry")][InlineData("archive")][InlineData("retired")][InlineData("old-g")][InlineData("old-b")]
    public Task RestoredChangedExactStateNeverStartsOrAcceptsReady(string change)=>ExecuteRestored(async r=>
    {
        await Compensated(r);var before=OriginalBefore(r);string path=change switch{
            "current"=>Path.Combine(r.App,"current.json"),"active"=>InstallationBootstrapDeployment.ActivePath(r.App),"host"=>Path.Combine(r.App,"host-registration.json"),"archive"=>Directory.GetFiles(r.App,"compensation-finished-*.json").Single(),"retired"=>r.Compensation!.RetiredPath,
            "old-g"=>Path.Combine(r.Prepared.Plan.Host.Before!.Version.DirectoryPath,"KuaiYi.Core.dll"),
            "old-b"=>Path.Combine(InstallationBootstrapDeployment.BundlePath(r.App,InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(Convert.FromBase64String(before.BootstrapActive!)).Bundle),"KuaiYi.Core.dll"),_=>""};
        if(change=="registry")r.Registry.Chrome=new(true,"PUBLIC_FOREIGN",true);else File.WriteAllText(path,"PUBLIC_FOREIGN");
        var restored=(await r.BeginRestored())!;var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Null(result.Ready);Assert.Equal(0,r.RestoredPort!.Starts);Assert.False(File.Exists(r.Owner));Assert.Equal(InstallationCompensationStatus.Compensated,r.Compensation!.Completion.Result.Status);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task RestoredPublicationCancellationOrLateFaultNeverAcceptsReadyAndKeepsNewOriginalOwner(bool fault)=>ExecuteRestored(async r=>
    {
        await Compensated(r);var entered=Signal();var release=Signal();var original=new IOException("PUBLIC_RESTORED_LATE_PUBLICATION");
        var restored=(await r.BeginRestored(async(_,_)=>{entered.TrySetResult();await release.Task;if(fault)throw original;}))!;
        await BodyAndCleanup(async()=>{await entered.Task.WaitAsync(Limit);Assert.False(restored.Ready.IsCompleted);r.Stop.Cancel();await r.RestoredPort!.IoEntered.Task.WaitAsync(Limit);},()=>{release.TrySetResult();return Task.CompletedTask;});
        var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Failed,result.Status);Assert.Null(result.Ready);Assert.Contains(Flatten(result.Errors),e=>e is OperationCanceledException);if(fault)Assert.Contains(original,Flatten(result.Errors));Assert.True(restored.SessionWorkSettled);Assert.True(r.RestoredController!.RestoredOwnsExecution(restored));r.LeaseHeld();Assert.False(File.Exists(Path.Combine(r.App,"browser-host-session.json")));Assert.Equal(InstallationCompensationStatus.Compensated,r.Compensation!.Completion.Result.Status);
    });
    [Fact]public Task RestoredActualChildExitBeforePublicationReturnsNeverSignsReady()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var entered=Signal();var release=Signal();var restored=(await r.BeginRestored(async(_,_)=>{entered.TrySetResult();await release.Task;await r.RestoredPort!.Child!.WaitForExitAsync(CancellationToken.None);}))!;
        await BodyAndCleanup(async()=>{await entered.Task.WaitAsync(Limit);await r.Peer!.Close();},()=>{release.TrySetResult();return Task.CompletedTask;});
        var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Failed,result.Status);Assert.Null(result.Ready);Assert.Equal(new[]{0},r.RestoredPort!.Exits);r.LeaseHeld();
    });
    [Fact]public Task RestoredAcceptedReadyIsHistoricalWhenLaterCancellationFailsTheSession()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var restored=(await r.BeginRestored())!;var ready=await restored.Ready.WaitAsync(Limit);r.Stop.Cancel();var result=await restored.Completion.WaitAsync(Limit);
        Assert.Same(ready,result.Ready);Assert.Equal(InstallationRestoredSessionStatus.Failed,result.Status);Assert.True(r.RestoredController!.RestoredOwnsExecution(restored));r.LeaseHeld();Assert.Equal(InstallationCompensationStatus.Compensated,r.Compensation!.Completion.Result.Status);
    });
    [Fact]public Task RestoredNewHealthAndOriginalIoFailuresArePreservedTogether()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var health=new IOException("PUBLIC_RESTORED_HEALTH");var io=new IOException("PUBLIC_RESTORED_IO");
        var restored=(await r.BeginRestored(configure:p=>{p.HealthError=health;p.IoError=io;}))!;var result=await restored.Completion.WaitAsync(Limit);
        Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Contains(health,Flatten(result.Errors));Assert.Contains(io,Flatten(result.Errors));Assert.Null(result.Ready);Assert.False(restored.SessionWorkSettled);r.LeaseHeld();
    });
    [Fact]public Task RestoredHeldOriginalIoCannotBeReplacedByASecondOwner()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var restored=(await r.BeginRestored(configure:p=>{p.HealthError=new IOException("PUBLIC_RESTORED_HEALTH_BEFORE_HELD_IO");p.HoldIo=true;}))!;
        await BodyAndCleanup(async()=>{await r.RestoredPort!.IoEntered.Task.WaitAsync(Limit);Assert.False(restored.Completion.IsCompleted);Assert.False(restored.SessionWorkSettled);r.LeaseHeld();Assert.Throws<InstallationRecoveryException>(()=>r.Compensation!.BeginRestoredSession(r.RestoredController!,(_,_)=>Task.CompletedTask));},()=>{r.RestoredPort!.IoRelease.TrySetResult();return Task.CompletedTask;});
        Assert.Equal(InstallationRestoredSessionStatus.Failed,(await restored.Completion.WaitAsync(Limit)).Status);Assert.True(restored.SessionWorkSettled);
    });
    [Fact]public Task RestoredExecutionAcquireAndOriginalLeaseCloseDoubleFailureRetainsBothOwners()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var acquire=new IOException("PUBLIC_RESTORED_EXECUTION_ACQUIRE");var close=new IOException("PUBLIC_RESTORED_EXECUTION_CLOSE");
        var restored=(await r.BeginRestored(executionFaults:new(){Acquired=()=>throw acquire,BeforeClose=()=>throw close}))!;var result=await restored.Completion.WaitAsync(Limit);
        Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Contains(acquire,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.Null(result.Ready);Assert.Equal(0,r.RestoredPort!.Starts);Assert.False(File.Exists(r.Owner));Assert.True(r.RestoredController!.RestoredOwnsExecution(restored));Assert.ThrowsAny<IOException>(()=>new FileStream(Path.Combine(r.App,"execution-owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None));
    });
    [Fact]public Task RestoredMarkerWriteAndOriginalCloseErrorsStayOnThePublishedLedger()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var write=new IOException("PUBLIC_RESTORED_MARKER_WRITE");var close=new IOException("PUBLIC_RESTORED_MARKER_CLOSE");var faults=new InstallationPreparationFaults{WriteMetadata=(path,file,bytes)=>{file.Write(bytes.Span[..1]);throw write;},BeforeClose=(kind,path)=>{if(path==r.Owner)throw close;}};
        var restored=(await r.BeginRestored(readFaults:faults))!;var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Contains(write,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.Null(result.Ready);Assert.Equal(0,r.RestoredPort!.Starts);Assert.True(File.Exists(r.Owner));Assert.False(restored.SessionWorkSettled);
    });
    [Theory][InlineData(false,true)][InlineData(true,false)][InlineData(true,true)]
    public Task RestoredOldGenerationDoesNotInventMissingBootstrapOrHost(bool noBootstrap,bool noHost)=>ExecuteRestored(async r=>
    {
        r.SkipPreviousBootstrap=noBootstrap;r.SkipPreviousHost=noHost;await Compensated(r);var before=OriginalBefore(r);Assert.Equal(noBootstrap,before.BootstrapActive==null);Assert.Equal(noHost,before.HostReceipt==null);
        var restored=(await r.BeginRestored())!;await restored.Ready.WaitAsync(Limit);Assert.Equal("1.0",r.RestoredPort!.Proof!.Version.Version);
        Assert.Equal(noBootstrap,!File.Exists(InstallationBootstrapDeployment.ActivePath(r.App)));Assert.Equal(noHost,!File.Exists(Path.Combine(r.App,"host-registration.json")));
        Assert.Equal(noHost?null:r.Prepared.Plan.Host.Before!.Manifest,r.Registry.Chrome.Value);Assert.Equal(noHost?null:r.Prepared.Plan.Host.Before!.Manifest,r.Registry.Edge.Value);Assert.True(r.Registry.Chrome.KeyExists);Assert.True(r.Registry.Edge.KeyExists);
        await r.Peer!.Close();Assert.Equal(InstallationRestoredSessionStatus.CleanExit,(await restored.Completion.WaitAsync(Limit)).Status);
    });
    [Fact]public Task RestoredLaunchPreparedPartialAndCloseErrorRemainOwnedInsteadOfBeingDeleted()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var write=new IOException("PUBLIC_RESTORED_PREPARED_WRITE");var close=new IOException("PUBLIC_RESTORED_PREPARED_CLOSE");string? partial=null;
        var faults=new InstallationPreparationFaults{WriteMetadata=(path,file,bytes)=>{if(Path.GetFileName(path).StartsWith("launch-",StringComparison.Ordinal)&&path.EndsWith("-prepared.json",StringComparison.Ordinal)){partial=path;file.Write(bytes.Span[..1]);throw write;}file.Write(bytes.Span);},BeforeClose=(kind,path)=>{if(path==partial)throw close;}};
        var restored=(await r.BeginRestored(readFaults:faults))!;var first=await Task.WhenAny(restored.Ready,restored.Completion).WaitAsync(Limit);Assert.False(restored.Ready.IsCompletedSuccessfully);var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Contains(write,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.NotNull(partial);Assert.True(File.Exists(partial));Assert.Equal(0,r.RestoredPort!.Starts);Assert.Null(result.Ready);Assert.False(restored.SessionWorkSettled);r.LeaseHeld();
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task RestoredOriginalHostPublicationMoveUnknownCannotBecomeKnownFailure(bool moved)=>ExecuteRestored(async r=>
    {
        await Compensated(r);var failure=new IOException("PUBLIC_RESTORED_MOVE_UNKNOWN");
        var faults=new InstallationPreparationFaults{Move=(from,to)=>{if(Path.GetFileName(from).StartsWith("browser-host-prepared-",StringComparison.Ordinal)){if(moved)File.Move(from,to);throw failure;}File.Move(from,to);}};
        var restored=(await r.BeginRestored(readFaults:faults))!;var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Contains(failure,Flatten(result.Errors));Assert.Null(result.Ready);Assert.Equal(1,r.RestoredPort!.Starts);Assert.Equal(moved,File.Exists(Path.Combine(r.App,"browser-host-session.json")));r.LeaseHeld();
    });
    [Fact]public Task RestoredOriginalExecutionCloseUnknownAfterReadyCannotClaimCleanExit()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var failure=new IOException("PUBLIC_RESTORED_EXECUTION_FINAL_CLOSE");var restored=(await r.BeginRestored(executionFaults:new(){BeforeClose=()=>throw failure}))!;
        var ready=await restored.Ready.WaitAsync(Limit);await r.Peer!.Close();var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Same(ready,result.Ready);Assert.Contains(failure,Flatten(result.Errors));Assert.True(r.RestoredController!.RestoredOwnsExecution(restored));Assert.ThrowsAny<IOException>(()=>new FileStream(Path.Combine(r.App,"execution-owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None));
    });
    [Fact]public Task RestoredAdmissionAcquireAndOriginalCloseDoubleErrorKeepsBothFailures()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var acquire=new IOException("PUBLIC_RESTORED_ADMISSION_ACQUIRE");var close=new IOException("PUBLIC_RESTORED_ADMISSION_CLOSE");InstallationAdmission? owner=null;
        var faults=new InstallationAdmissionFaults{OwnerCreated=o=>owner=o,Acquired=()=>throw acquire,BeforeClose=()=>throw close};var restored=(await r.BeginRestored(admissionFaults:faults))!;
        var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Contains(acquire,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.Equal(0,r.RestoredPort!.Starts);Assert.NotNull(owner);Assert.False(owner!.CleanupConfirmed);Assert.False(restored.SessionWorkSettled);faults.BeforeClose=null;
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task RestoredRegistryReadCannotLaunderAReplacedBootstrapOrHostBeforeReady(bool host)=>ExecuteRestored(async r=>
    {
        await Compensated(r);int reads=0;r.Registry.Reading=browser=>{if(browser==IntegrationBrowser.Chrome&&++reads==3)File.WriteAllText(host?Path.Combine(r.App,"host-registration.json"):InstallationBootstrapDeployment.ActivePath(r.App),"PUBLIC_REPLACED_DURING_ORIGINAL_REGISTRY_READ");};
        var restored=(await r.BeginRestored())!;var first=await Task.WhenAny(restored.Ready,restored.Completion).WaitAsync(Limit);Assert.False(restored.Ready.IsCompletedSuccessfully);
        var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Null(result.Ready);Assert.Equal(1,r.RestoredPort!.Starts);r.LeaseHeld();
    });
    [Fact]public Task RestoredReplacementDuringOriginalReadyPublicationFailsFinalQualification()=>ExecuteRestored(async r=>
    {
        await Compensated(r);var restored=(await r.BeginRestored((_,_)=>{File.WriteAllBytes(Path.Combine(r.App,"current.json"),Convert.FromBase64String(r.Prepared.Plan.TargetCurrent));return Task.CompletedTask;}))!;
        var result=await restored.Completion.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.Unconfirmed,result.Status);Assert.Null(result.Ready);Assert.Equal(1,r.RestoredPort!.Starts);r.LeaseHeld();
    });
}
