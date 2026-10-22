using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests
{
    private sealed partial class Rig
    {
        internal string? OperationPreviousPipe;
        internal InstallationOperation? Operation;
        internal Peer? OperationPreviousPeer;
        internal readonly List<InstallationOperationSession> FactoryCalls=[];
        internal async Task<InstallationOperation> PrepareOperation(bool upgrade=true,InstallationOperationFaults? faults=null,
            Func<InstallationControlledLaunch,CancellationToken,Task>? published=null,Func<InstallationOperationSession,Task>? beforeFactory=null)
        {
            OperationPreviousPipe=upgrade?"ky-op-previous-"+Guid.NewGuid().ToString("N"):null;
            var(source,b)=await Sources(upgrade);Peer=new(Pipe);if(upgrade)OperationPreviousPeer=new(OperationPreviousPipe!);RestoredPort=new(output);
            return Operation=new(App,Data,source.Root,source.Digest,b.Root,b.Digest,new InstallationDeploymentRegistrySnapshot(Registry),async kind=>
            {
                FactoryCalls.Add(kind);if(beforeFactory!=null)await beforeFactory(kind);
                return kind==InstallationOperationSession.New?new(App,Data,Port,TimeSpan.FromSeconds(2),Port):new(App,Data,RestoredPort,TimeSpan.FromSeconds(2),RestoredPort);
            },published??((_,_)=>Task.CompletedTask),Stop.Token,faults);
        }
        internal async Task CleanupOperation()
        {
            var errors=new List<Exception>();Port.HealthRelease.TrySetResult();Port.IoRelease.TrySetResult();RestoredPort?.HealthRelease.TrySetResult();RestoredPort?.IoRelease.TrySetResult();
            try{Stop.Cancel();}catch(Exception error){errors.Add(error);}
            if(Operation!=null)
            {
                try{await Operation.OriginalWork.WaitAsync(Limit);}catch(Exception error){errors.Add(error);}
                if(Operation.OriginalWorkPending){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original operation/child work pending; Rig/Stop/root retained.");}
                Scope=Operation.Scope!;Session=Operation.NewSession;Controller=Operation.NewController;Compensation=Operation.Compensation;Restored=Operation.PreviousSession;RestoredController=Operation.PreviousController;
                if(!Operation.CleanupConfirmed)RestoredRetention=true;
            }
            try{await CleanupRestored();}catch(Exception error){errors.Add(error);}
            if(OperationPreviousPeer!=null)try{await OperationPreviousPeer.DisposeAsync();}catch(Exception error){errors.Add(error);}
            if(OperationPreviousPeer is{CleanupConfirmed:false}){lock(Retained)Retained.Add(this);RestoredRetention=true;}
            InstallationErrors.Throw(errors);
        }
    }
    private Task ExecuteOperation(Func<Rig,Task> body)=>Execute(r=>BodyAndCleanup(()=>body(r),r.CleanupOperation));
    [Fact]public Task OperationNewReadyIsAHealthResultWhileItsOriginalSessionStillOwnsTheLifetime()=>ExecuteOperation(async r=>
    {
        var op=await r.PrepareOperation();var result=await op.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NewVersionReady,result.Status);Assert.Equal(InstallationDeploymentStatus.Committed,result.Deployment!.Status);Assert.NotNull(result.NewReady);Assert.Null(result.Compensation);Assert.Null(result.PreviousReady);Assert.Equal(new[]{InstallationOperationSession.New},r.FactoryCalls);Assert.Equal("2.0",result.NewReady!.Generation.Version);Assert.False(op.OriginalWork.IsCompleted);Assert.False(op.CleanupConfirmed);Assert.True(op.OriginalWorkPending);Assert.Throws<InvalidOperationException>(()=>{_=op.Start();});
        await r.Peer!.Close();var settled=await op.OriginalWork.WaitAsync(Limit);Assert.Same(result,settled.Result);Assert.Equal(InstallationDeploymentSessionStatus.CleanExit,settled.NewSession!.Status);Assert.True(settled.CleanupConfirmed);Assert.True(op.CleanupConfirmed);Assert.Empty(settled.Errors);
    });
    [Fact]public Task OperationNewFailureRestoresTheOldGenerationAndOnlyItsNewReadySignsPreviousHealth()=>ExecuteOperation(async r=>
    {
        var failure=new IOException("PUBLIC_OPERATION_NEW_HEALTH");r.Port.HealthError=failure;
        var op=await r.PrepareOperation();var result=await op.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.PreviousVersionReady,result.Status);Assert.Null(result.NewReady);Assert.Equal(InstallationCompensationStatus.Compensated,result.Compensation!.Status);Assert.Equal("1.0",result.PreviousReady!.Generation.Version);Assert.Equal(new[]{InstallationOperationSession.New,InstallationOperationSession.Previous},r.FactoryCalls);Assert.Contains(failure,result.Errors);Assert.NotEqual(r.Port.Proof!.Launch,result.PreviousReady.Launch);Assert.False(op.CleanupConfirmed);
        await r.OperationPreviousPeer!.Close();var settled=await op.OriginalWork.WaitAsync(Limit);Assert.Equal(InstallationRestoredSessionStatus.CleanExit,settled.PreviousSession!.Status);Assert.True(op.CleanupConfirmed);Assert.Contains(failure,settled.Errors);
    });
    [Fact]public Task OperationFirstInstallationFailureRestoresAbsenceWithoutAnOldFactoryOrReady()=>ExecuteOperation(async r=>
    {
        r.Port.HealthError=new IOException("PUBLIC_OPERATION_INITIAL_HEALTH");var op=await r.PrepareOperation(false);var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.RestoredNoPrevious,result.Status);Assert.Equal(InstallationCompensationStatus.Compensated,result.Compensation!.Status);Assert.Null(result.NewReady);Assert.Null(result.PreviousReady);Assert.Null(op.PreviousSession);Assert.Null(op.PreviousController);Assert.Equal(new[]{InstallationOperationSession.New},r.FactoryCalls);Assert.Equal(0,r.RestoredPort!.Starts);Assert.Null(r.Store.ReadActive());Assert.True((await op.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Fact]public Task OperationPreCommitCancellationUsesTheOriginalAbortAndNeverCreatesAController()=>ExecuteOperation(async r=>
    {
        var op=await r.PrepareOperation(faults:new(){Publication=new(){Stage=stage=>{if(stage==DeploymentPublicationStage.Active)r.Stop.Cancel();}}});var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.NotCommitted,result.Status);Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,result.Deployment!.Status);Assert.Empty(r.FactoryCalls);Assert.Null(result.NewReady);Assert.Null(result.Compensation);Assert.Equal("1.0",r.Store.ReadActive()!.Version);Assert.True((await op.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Fact]public Task OperationPartialPrepareWriteAndCloseUnknownKeepBothErrorsAndNeverActivate()=>ExecuteOperation(async r=>
    {
        var write=new IOException("PUBLIC_OPERATION_PREPARE_WRITE");var close=new IOException("PUBLIC_OPERATION_PREPARE_CLOSE");string? held=null;
        var op=await r.PrepareOperation(faults:new(){Generation=new(){WriteMetadata=(path,file,bytes)=>{held=path;file.Write(bytes.Span[..1]);throw write;},BeforeClose=(kind,path)=>{if(path==held)throw close;}}});var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Contains(write,result.Errors);Assert.Contains(close,result.Errors);Assert.Empty(r.FactoryCalls);Assert.Null(result.NewReady);Assert.False(op.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task OperationUncertainCommitReceiptNeverStartsAnyVersion(bool moved)=>ExecuteOperation(async r=>
    {
        var failure=new IOException("PUBLIC_OPERATION_ARCHIVE_UNKNOWN");var op=await r.PrepareOperation(faults:new(){Deployment=new(){Archive=(from,to)=>{if(moved)File.Move(from,to);throw failure;}}});var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Equal(moved?InstallationDeploymentStatus.CommittedReceiptUncertain:InstallationDeploymentStatus.Unconfirmed,result.Deployment!.Status);Assert.Contains(failure,result.Errors);Assert.Empty(r.FactoryCalls);Assert.Null(result.NewReady);Assert.Null(op.Compensation);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task OperationCommitClosingCancellationOrForeignExecutionCannotInventRollbackAuthority(bool foreign)=>ExecuteOperation(async r=>
    {
        var op=await r.PrepareOperation(faults:foreign?new(){Phase=phase=>{if(phase==InstallationOperationPhase.Committed)File.WriteAllText(r.Owner,"PUBLIC_UNKNOWN_OPERATION_OWNER");}}:new(){Deployment=new(){AfterClosing=()=>{r.Stop.Cancel();return Task.CompletedTask;}}});var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.CommittedNotReady,result.Status);Assert.Equal(InstallationDeploymentStatus.Committed,result.Deployment!.Status);Assert.Null(result.NewReady);Assert.Null(result.Compensation);Assert.Null(result.PreviousReady);Assert.Equal(0,r.Port.Starts);if(foreign)Assert.Equal("PUBLIC_UNKNOWN_OPERATION_OWNER",File.ReadAllText(r.Owner));else Assert.Empty(r.FactoryCalls);Assert.Null(op.Compensation);
    });
    [Fact]public Task OperationLateFactoryReturnAfterCancellationIsJoinedAndDisposedWithoutStarting()=>ExecuteOperation(async r=>
    {
        var entered=Signal();var release=Signal();var op=await r.PrepareOperation(beforeFactory:async _=>{entered.TrySetResult();await release.Task;});var started=op.Start();
        await BodyAndCleanup(async()=>{await entered.Task.WaitAsync(Limit);r.Stop.Cancel();Assert.False(started.IsCompleted);Assert.True(op.OriginalWorkPending);Assert.Equal(0,r.Port.Starts);},()=>{release.TrySetResult();return Task.CompletedTask;});
        var result=await started.WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.CommittedNotReady,result.Status);Assert.Null(op.NewSession);Assert.Equal(0,r.Port.Starts);Assert.True((await op.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);await Assert.ThrowsAsync<ObjectDisposedException>(()=>op.NewController!.RunActiveSessionAsync(r.Store.ReadActive()!.DirectoryPath));
    });
    [Fact]public Task OperationHeldNewOriginalCleanupCannotBeginCompensationEarly()=>ExecuteOperation(async r=>
    {
        r.Port.HealthError=new IOException("PUBLIC_OPERATION_HEALTH_BEFORE_HELD_IO");r.Port.HoldIo=true;var op=await r.PrepareOperation();var started=op.Start();
        await BodyAndCleanup(async()=>{await r.Port.IoEntered.Task.WaitAsync(Limit);Assert.Null(op.Compensation);Assert.False(started.IsCompleted);Assert.True(op.OriginalWorkPending);Assert.Equal(new[]{InstallationOperationSession.New},r.FactoryCalls);},()=>{r.Port.IoRelease.TrySetResult();return Task.CompletedTask;});
        Assert.Equal(InstallationOperationStatus.PreviousVersionReady,(await started.WaitAsync(Limit)).Status);await r.OperationPreviousPeer!.Close();Assert.True((await op.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Fact]public Task OperationUnknownCompensationStopsBeforeThePreviousControllerFactory()=>ExecuteOperation(async r=>
    {
        r.Port.HealthError=new IOException("PUBLIC_OPERATION_FAILED_NEW");var unknown=new IOException("PUBLIC_OPERATION_COMPENSATION_UNKNOWN");var op=await r.PrepareOperation(faults:new(){Compensation=new(){BeforeMutation=_=>throw unknown}});var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Compensation!.Status);Assert.Contains(unknown,result.Errors);Assert.Equal(new[]{InstallationOperationSession.New},r.FactoryCalls);Assert.Equal(0,r.RestoredPort!.Starts);Assert.Null(result.PreviousReady);Assert.False(op.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
    });
    [Fact]public Task OperationPreviousFailureIsRestoredButNotHealthyAndNeverCompensatesAgain()=>ExecuteOperation(async r=>
    {
        var first=new IOException("PUBLIC_OPERATION_NEW_FAILED");var second=new IOException("PUBLIC_OPERATION_PREVIOUS_FAILED");r.Port.HealthError=first;var op=await r.PrepareOperation();r.RestoredPort!.HealthError=second;var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.RestoredPreviousNotReady,result.Status);Assert.Equal(InstallationCompensationStatus.Compensated,result.Compensation!.Status);Assert.Null(result.PreviousReady);Assert.Contains(first,result.Errors);Assert.Contains(second,result.Errors);Assert.Equal(2,r.FactoryCalls.Count);Assert.Single(Directory.GetFiles(r.App,"compensation-finished-*.json"));Assert.False((await op.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Fact]public Task OperationCancellationTriggersAuthorizedRecoveryButNeverAutomaticallyRestartsPrevious()=>ExecuteOperation(async r=>
    {
        var op=await r.PrepareOperation(published:(_,_)=>{r.Stop.Cancel();return Task.CompletedTask;});var result=await op.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.RestoredPreviousNotReady,result.Status);Assert.Equal(InstallationCompensationStatus.Compensated,result.Compensation!.Status);Assert.Null(result.NewReady);Assert.Null(result.PreviousReady);Assert.Equal(new[]{InstallationOperationSession.New},r.FactoryCalls);Assert.Equal(0,r.RestoredPort!.Starts);Assert.Equal("1.0",r.Store.ReadActive()!.Version);Assert.True((await op.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Fact]public Task OperationLateCancellationAfterCanonicalNewReadyCannotRewriteItsPublishedHealth()=>ExecuteOperation(async r=>
    {
        var op=await r.PrepareOperation();var result=await op.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NewVersionReady,result.Status);r.Stop.Cancel();var settled=await op.OriginalWork.WaitAsync(Limit);
        Assert.Same(result,settled.Result);Assert.Equal(InstallationOperationStatus.NewVersionReady,(await op.Completion).Status);Assert.NotNull(result.NewReady);Assert.Null(op.Compensation);Assert.Contains(settled.Errors,e=>e is OperationCanceledException);Assert.Equal(InstallationDeploymentSessionStatus.Unconfirmed,settled.NewSession!.Status);Assert.False(settled.CleanupConfirmed);Assert.False(op.CleanupConfirmed);
    });
}
