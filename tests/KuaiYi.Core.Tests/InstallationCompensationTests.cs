using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.Core.Integration;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests
{
    private static async Task FailedDeployment(Rig r,bool upgrade=false)
    {
        await r.Commit(upgrade:upgrade);r.Port.HealthError=new IOException("PUBLIC_COMPENSATION_ORIGINAL_HEALTH");r.Session=r.Begin();
        Assert.Equal(InstallationDeploymentSessionStatus.RollbackRequired,(await r.Session.Completion.WaitAsync(Limit)).Status);Assert.True(r.Session.OriginalFailedSessionRetired);r.Registry.Reads=r.Registry.Writes=0;
    }
    private static InstallationDeploymentBefore OriginalBefore(Rig r)=>JsonSerializer.Deserialize<InstallationDeploymentBefore>(File.ReadAllBytes(Path.Combine(r.App,"deployment-before-"+r.Scope.Operation+".json")))!;
    private static void CompensationPrefix(Rig r,int prefix)
    {
        var before=OriginalBefore(r);var target=r.Prepared.Plan;
        foreach(var item in new[]{(1,"host-registration.json",before.HostReceipt,target.Host.Receipt),(4,"stable-bootstrap.json",before.BootstrapActive,target.Bootstrap.Receipt),(5,"current.json",before.Current,target.TargetCurrent)})
        {
            string path=Path.Combine(r.App,item.Item2);string? expected=prefix>=item.Item1?item.Item3:item.Item4;
            if(expected==null)Assert.False(File.Exists(path));else Assert.Equal(Convert.FromBase64String(expected),File.ReadAllBytes(path));
        }
        InstallationDeploymentRegistrationSlot Restored(InstallationDeploymentRegistrationSlot x)=>x.KeyExists?x:new(true,null);
        Assert.Equal(prefix>=2?Restored(before.Registry.Edge):target.Host.Targets.Edge,new(r.Registry.Edge.KeyExists,r.Registry.Edge.Value,r.Registry.Edge.ForeignContent));
        Assert.Equal(prefix>=3?Restored(before.Registry.Chrome):target.Host.Targets.Chrome,new(r.Registry.Chrome.KeyExists,r.Registry.Chrome.Value,r.Registry.Chrome.ForeignContent));
    }
    private static void CompensationFence(Rig r,bool lease=true)
    {
        Assert.True(File.Exists(r.Store.PendingPath));Assert.ThrowsAny<IOException>(()=>r.Store.EnterRoleAdmission());
        if(lease)Assert.ThrowsAny<IOException>(()=>new FileStream(Path.Combine(r.App,"execution-owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None));
    }
    private static async Task BodyAndCleanup(Func<Task> body,Func<Task> cleanup)
    {
        var errors=new List<Exception>();try{await body();}catch(Exception error){errors.Add(error);}
        try{await cleanup();}catch(Exception error){errors.Add(error);}InstallationErrors.Throw(errors);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationRestoresOwnedUpgradeOrInitialAbsenceBeforeRetiringOriginalExecution(bool upgrade)=>Execute(async r=>
    {
        await FailedDeployment(r,upgrade);var before=OriginalBefore(r);var steps=new List<int>();
        r.Compensation=r.Session!.BeginCompensation(faults:new(){BeforeMutation=step=>{steps.Add(step);r.LeaseHeld();}});
        var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Compensated,result.Status);Assert.Empty(result.Errors);Assert.Contains(Flatten(result.OriginalSessionErrors),e=>e.Message=="PUBLIC_COMPENSATION_ORIGINAL_HEALTH");Assert.Equal(new[]{1,2,3,4,5},steps);CompensationPrefix(r,5);
        Assert.False(File.Exists(r.Owner));Assert.False(File.Exists(r.Store.PendingPath));Assert.True(r.Compensation.CleanupConfirmed);Assert.False(r.Compensation.OriginalExecutionLeaseRetained);Assert.False(r.Session.OriginalFailedSessionRetired);Assert.True(File.Exists(r.Compensation.RetiredPath));Assert.True(Directory.Exists(r.Prepared.Plan.Bootstrap.CanonicalDirectory));Assert.True(File.Exists(InstallationBootstrapDeployment.OwnerPath(r.App)));Assert.True(File.Exists(Path.Combine(r.App,"deployment-committed-"+r.Scope.Operation+".json")));
        Assert.Single(Directory.GetFiles(r.App,"compensation-finished-*.json"));Assert.Throws<InstallationRecoveryException>(()=>r.Session.BeginCompensation());
        if(upgrade)Assert.Equal(r.Prepared.Plan.Host.Before!.Version,r.Store.ReadActive());else Assert.Null(r.Store.ReadActive());
        using(var admitted=r.Store.EnterRoleAdmission()){}
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationCancellationDuringOriginalVerificationCannotStartNextMutation(bool inHook)=>Execute(async r=>
    {
        await FailedDeployment(r);using var cancel=new CancellationTokenSource();using var release=new ManualResetEventSlim();var entered=Signal();bool armed=false;var attempted=new List<int>();
        var faults=new InstallationCompensationFaults{Stage=stage=>{if(stage==InstallationCompensationStage.Verified&&!inHook){armed=true;r.Registry.Reading=_=>{if(!armed)return;armed=false;entered.TrySetResult();if(!release.Wait(Limit))throw new TimeoutException("PUBLIC_HELD_ORIGINAL_REGISTRY_READ");};}},BeforeMutation=step=>{attempted.Add(step);if(inHook)cancel.Cancel();}};
        r.Compensation=r.Session!.BeginCompensation(cancel.Token,faults);
        try{if(!inHook){await entered.Task.WaitAsync(Limit);cancel.Cancel();}}finally{release.Set();}
        var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);CompensationPrefix(r,0);Assert.False(File.Exists(r.Compensation.RetiredPath));CompensationFence(r);Assert.Contains(Flatten(result.Errors),e=>e is OperationCanceledException);
    });
    [Fact]public Task CompensationCancellationAfterTemporaryPreparationCannotReplaceFirstRecord()=>Execute(async r=>
    {
        await FailedDeployment(r,true);using var cancel=new CancellationTokenSource();
        var io=new InstallationPreparationFaults{BeforeFlush=(path,_)=>{if(Path.GetFileName(path).StartsWith("compensation-",StringComparison.Ordinal)&&path.EndsWith("-1.tmp",StringComparison.Ordinal))cancel.Cancel();}};
        r.Compensation=r.Session!.BeginCompensation(cancel.Token,ioFaults:io);var result=await r.Compensation.Completion.WaitAsync(Limit);
        Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);CompensationPrefix(r,0);CompensationFence(r);Assert.False(File.Exists(r.Compensation.RetiredPath));Assert.Contains(Flatten(result.Errors),e=>e is OperationCanceledException);
    });
    [Theory][InlineData(1,false)][InlineData(2,false)][InlineData(3,false)][InlineData(4,false)][InlineData(5,false)][InlineData(1,true)][InlineData(2,true)][InlineData(3,true)][InlineData(4,true)][InlineData(5,true)]
    public Task CompensationEveryReverseBoundaryFailureOrCancellationRetainsTheKnownPrefix(int step,bool cancel)=>Execute(async r=>
    {
        await FailedDeployment(r,true);using var stop=new CancellationTokenSource();var failure=new IOException("PUBLIC_REVERSE_BOUNDARY_"+step);var attempts=new List<int>();
        r.Compensation=r.Session!.BeginCompensation(stop.Token,new(){BeforeMutation=n=>attempts.Add(n),Stage=stage=>{if(stage==(InstallationCompensationStage)((int)InstallationCompensationStage.HostReceipt+step-1)){if(cancel)stop.Cancel();else throw failure;}}});
        var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Equal(Enumerable.Range(1,step),attempts);CompensationPrefix(r,step);CompensationFence(r);Assert.False(File.Exists(r.Compensation.RetiredPath));Assert.Empty(Directory.GetFiles(r.App,"compensation-finished-*.json"));Assert.Contains(Flatten(result.Errors),e=>cancel?e is OperationCanceledException:ReferenceEquals(e,failure));
    });
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public Task CompensationForeignAtEachAttemptStopsEarlierDependenciesAndCannotBeOverwritten(int step)=>Execute(async r=>
    {
        await FailedDeployment(r,true);var attempts=new List<int>();string? foreign=null;
        r.Compensation=r.Session!.BeginCompensation(faults:new(){BeforeMutation=n=>{attempts.Add(n);if(n!=step)return;if(n is 2 or 3){if(n==2)r.Registry.Edge=new(true,"PUBLIC_FOREIGN",true);else r.Registry.Chrome=new(true,"PUBLIC_FOREIGN",true);}else{foreign=Path.Combine(r.App,n==1?"host-registration.json":n==4?"stable-bootstrap.json":"current.json");File.WriteAllText(foreign,"PUBLIC_FOREIGN");}}});
        var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Equal(Enumerable.Range(1,step),attempts);if(foreign!=null)Assert.Equal("PUBLIC_FOREIGN",File.ReadAllText(foreign));else Assert.Equal("PUBLIC_FOREIGN",(step==2?r.Registry.Edge:r.Registry.Chrome).Value);
        if(step<5)Assert.Equal(Convert.FromBase64String(r.Prepared.Plan.TargetCurrent),File.ReadAllBytes(Path.Combine(r.App,"current.json")));if(step<4)Assert.Equal(Convert.FromBase64String(r.Prepared.Plan.Bootstrap.Receipt),File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App)));CompensationFence(r);Assert.False(File.Exists(r.Compensation.RetiredPath));
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationDamagedOldGenerationOrBootstrapNeverStartsReverseWrites(bool bootstrap)=>Execute(async r=>
    {
        await FailedDeployment(r,true);var before=OriginalBefore(r);string directory=bootstrap?InstallationBootstrapDeployment.BundlePath(r.App,InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(Convert.FromBase64String(before.BootstrapActive!)).Bundle):r.Prepared.Plan.Host.Before!.Version.DirectoryPath;
        File.AppendAllText(Path.Combine(directory,"KuaiYi.Core.dll"),"PUBLIC_DAMAGED_OLD_TARGET");int mutations=0;
        r.Compensation=r.Session!.BeginCompensation(faults:new(){BeforeMutation=_=>mutations++});var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Equal(0,mutations);Assert.Equal(0,r.Registry.Writes);CompensationPrefix(r,0);CompensationFence(r);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationOriginalRegistryCompareConflictStaysForeignAfterExternalRestoration(bool chrome)=>Execute(async r=>
    {
        await FailedDeployment(r,true);int seenReads=-1;bool restore=false;var original=chrome?r.Registry.Chrome:r.Registry.Edge;var attempts=new List<int>();
        r.Registry.Writing=(browser,after)=>{if(!after&&browser==(chrome?IntegrationBrowser.Chrome:IntegrationBrowser.Edge)){seenReads=r.Registry.Reads;restore=true;if(chrome)r.Registry.Chrome=new(true,"PUBLIC_TRANSIENT_FOREIGN",true);else r.Registry.Edge=new(true,"PUBLIC_TRANSIENT_FOREIGN",true);}};
        r.Registry.Closing=()=>{if(restore){restore=false;if(chrome)r.Registry.Chrome=original;else r.Registry.Edge=original;}};
        r.Compensation=r.Session!.BeginCompensation(faults:new(){BeforeMutation=n=>attempts.Add(n)});var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(Flatten(result.Errors),e=>e is HostRegistrationConflictException);Assert.Equal(Enumerable.Range(1,chrome?3:2),attempts);Assert.Equal(seenReads,r.Registry.Reads);Assert.Equal(original,chrome?r.Registry.Chrome:r.Registry.Edge);CompensationFence(r);Assert.Equal(Convert.FromBase64String(r.Prepared.Plan.TargetCurrent),File.ReadAllBytes(Path.Combine(r.App,"current.json")));
    });
    [Fact]public Task CompensationRegistryWriteAndCloseErrorsSurviveTogetherWithoutEarlierDependencyWrites()=>Execute(async r=>
    {
        await FailedDeployment(r,true);var body=new IOException("PUBLIC_COMPENSATION_WRITE_BODY");var close=new IOException("PUBLIC_COMPENSATION_REGISTRY_CLOSE");
        r.Registry.Writing=(_,after)=>{if(after){r.Registry.CloseError=close;throw body;}};
        await BodyAndCleanup(async()=>
        {
            r.Compensation=r.Session!.BeginCompensation();var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(body,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.Equal(1,r.Registry.Writes);Assert.False(r.Compensation.CleanupConfirmed);CompensationFence(r);Assert.Equal(Convert.FromBase64String(r.Prepared.Plan.TargetCurrent),File.ReadAllBytes(Path.Combine(r.App,"current.json")));
        },async()=>{r.Registry.CloseError=null;if(r.Compensation is{Completion.IsCompleted:true})Assert.Empty(await r.Compensation.SettleOriginalsAsync());});
        Assert.Equal(InstallationCompensationStatus.Unconfirmed,r.Compensation!.Completion.Result.Status);CompensationFence(r);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationPartialTicketOrPlanRetainsOriginalFileOwnerAndBothWriteCloseErrors(bool plan)=>Execute(async r=>
    {
        await FailedDeployment(r);var body=new IOException("PUBLIC_PARTIAL_METADATA_BODY");var close=new IOException("PUBLIC_PARTIAL_METADATA_CLOSE");string? partial=null;int mutations=0;
        bool Matches(string path)=>plan?Path.GetFileName(path).StartsWith("compensation-plan-",StringComparison.Ordinal):Path.GetFileName(path)=="upgrade-pending.json";
        var io=new InstallationPreparationFaults{WriteMetadata=(path,file,bytes)=>{if(Matches(path)){partial=path;file.Write(bytes.Span[..3]);throw body;}file.Write(bytes.Span);},BeforeClose=(_,path)=>{if(Matches(path))throw close;}};
        await BodyAndCleanup(async()=>
        {
            r.Compensation=r.Session!.BeginCompensation(faults:new(){BeforeMutation=_=>mutations++},ioFaults:io);var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(body,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.Equal(0,mutations);Assert.True(r.Compensation.PendingAttempted);Assert.False(r.Compensation.CleanupConfirmed);CompensationFence(r);
        },async()=>{io.BeforeClose=null;if(r.Compensation is{Completion.IsCompleted:true})Assert.Empty(await r.Compensation.SettleOriginalsAsync());});
        Assert.NotNull(partial);Assert.Equal(3,File.ReadAllBytes(partial!).Length);Assert.False(r.Compensation!.CleanupConfirmed);Assert.Equal(InstallationCompensationStatus.Unconfirmed,r.Compensation.Completion.Result.Status);CompensationPrefix(r,0);CompensationFence(r);
    });
    [Fact]public Task CompensationAdmissionAcquireAndCloseFailuresKeepOriginalExecutionAndActualOwner()=>Execute(async r=>
    {
        await FailedDeployment(r);var acquire=new IOException("PUBLIC_COMPENSATION_ADMISSION_ACQUIRE");var close=new IOException("PUBLIC_COMPENSATION_ADMISSION_CLOSE");InstallationAdmission? original=null;
        var faults=new InstallationAdmissionFaults{OwnerCreated=o=>original=o,Acquired=()=>throw acquire,BeforeClose=()=>throw close};
        await BodyAndCleanup(async()=>
        {
            r.Compensation=r.Session!.BeginCompensation(admissionFaults:faults);var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Contains(acquire,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.NotNull(original);Assert.False(original!.CleanupConfirmed);Assert.False(r.Compensation.PendingAttempted);r.LeaseHeld();
        },async()=>{faults.BeforeClose=null;if(r.Compensation is{Completion.IsCompleted:true})Assert.Empty(await r.Compensation.SettleOriginalsAsync());});
        Assert.True(original!.CleanupConfirmed);Assert.False(r.Compensation!.CleanupConfirmed);r.LeaseHeld();
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationRetirementMoveUnknownCannotReleaseOrReclaimOriginalExecution(bool moved)=>Execute(async r=>
    {
        await FailedDeployment(r);var fault=new IOException("PUBLIC_RETIREMENT_MOVE_UNKNOWN");int moves=0;
        r.Compensation=r.Session!.BeginCompensation(faults:new(){Retire=(from,to)=>{moves++;if(moved)File.Move(from,to);throw fault;}});var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(fault,Flatten(result.Errors));Assert.Equal(1,moves);Assert.Equal(moved,File.Exists(r.Compensation.RetiredPath));Assert.Equal(!moved,File.Exists(r.Owner));CompensationPrefix(r,5);CompensationFence(r);Assert.Empty(await r.Compensation.SettleOriginalsAsync());Assert.Equal(1,moves);CompensationFence(r);Assert.False(r.Compensation.CleanupConfirmed);Assert.False(r.Session!.OriginalFailedSessionRetired);
    });
    [Fact]public Task CompensationExecutionCloseUnknownOnlySameRetirementCanSettleWithoutPromotingOutcome()=>Execute(async r=>
    {
        await FailedDeployment(r);var fault=new IOException("PUBLIC_EXECUTION_CLOSE_UNKNOWN");var faults=new InstallationCompensationFaults{BeforeExecutionClose=()=>throw fault};
        await BodyAndCleanup(async()=>
        {
            r.Compensation=r.Session!.BeginCompensation(faults:faults);var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(fault,Flatten(result.Errors));Assert.True(File.Exists(r.Compensation.RetiredPath));CompensationFence(r);Assert.False(r.Compensation.CleanupConfirmed);
        },async()=>{faults.BeforeExecutionClose=null;if(r.Compensation is{Completion.IsCompleted:true})Assert.Empty(await r.Compensation.SettleOriginalsAsync());});
        Assert.True(r.Compensation!.CleanupConfirmed);Assert.Equal(InstallationCompensationStatus.Unconfirmed,r.Compensation.Completion.Result.Status);CompensationPrefix(r,5);CompensationFence(r,false);Assert.Empty(Directory.GetFiles(r.App,"compensation-finished-*.json"));
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationFinalArchiveUnknownNeverReplaysEvenAfterSideEffect(bool moved)=>Execute(async r=>
    {
        await FailedDeployment(r);var fault=new IOException("PUBLIC_COMPENSATION_ARCHIVE_UNKNOWN");int moves=0;
        r.Compensation=r.Session!.BeginCompensation(faults:new(){Archive=(from,to)=>{moves++;if(moved)File.Move(from,to);throw fault;}});var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(fault,Flatten(result.Errors));Assert.Equal(!moved,File.Exists(r.Store.PendingPath));Assert.Equal(moved,Directory.GetFiles(r.App,"compensation-finished-*.json").Length==1);Assert.True(r.Compensation.CleanupConfirmed);Assert.False(File.Exists(r.Owner));CompensationPrefix(r,5);Assert.Empty(await r.Compensation.SettleOriginalsAsync());Assert.Equal(1,moves);Assert.Throws<InstallationRecoveryException>(()=>r.Session!.BeginCompensation());
    });
    [Fact]public Task CompensationAfterClosingCancellationOnlySettlesAndDoesNoIoAfterArchive()=>Execute(async r=>
    {
        await FailedDeployment(r,true);using var stop=new CancellationTokenSource();bool archived=false;
        r.Registry.Closing=()=>{if(archived)throw new IOException("PUBLIC_REGISTRY_CLOSE_AFTER_ARCHIVE");};
        r.Compensation=r.Session!.BeginCompensation(stop.Token,new(){Stage=stage=>{if(stage==InstallationCompensationStage.Closing)stop.Cancel();},Archive=(from,to)=>{File.Move(from,to);archived=true;}},new(){Acquired=(_,_,_)=>{if(archived)throw new IOException("PUBLIC_IO_AFTER_ARCHIVE");}});
        var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.True(stop.IsCancellationRequested);Assert.True(archived);Assert.Equal(InstallationCompensationStatus.Compensated,result.Status);Assert.Empty(result.Errors);Assert.True(r.Compensation.CleanupConfirmed);CompensationPrefix(r,5);Assert.Empty(await r.Compensation.SettleOriginalsAsync());Assert.False(File.Exists(r.Owner));Assert.False(File.Exists(r.Store.PendingPath));
    });
    [Fact]public Task CompensationClosingFreshReadCloseUnknownRemainsOwnedAndNeverArchives()=>Execute(async r=>
    {
        await FailedDeployment(r,true);bool arm=false;int acquired=0;var fault=new IOException("PUBLIC_CLOSING_READ_CLOSE_UNKNOWN");
        var io=new InstallationPreparationFaults{Acquired=(_,_,_)=>acquired++,BeforeClose=(_,path)=>{if(arm&&Path.GetFileName(path).StartsWith("compensation-execution-",StringComparison.Ordinal))throw fault;}};
        await BodyAndCleanup(async()=>
        {
            r.Compensation=r.Session!.BeginCompensation(faults:new(){Stage=stage=>{if(stage==InstallationCompensationStage.Retired)arm=true;}},ioFaults:io);var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(fault,Flatten(result.Errors));Assert.False(r.Compensation.CleanupConfirmed);CompensationFence(r);Assert.Empty(Directory.GetFiles(r.App,"compensation-finished-*.json"));
        },async()=>{io.BeforeClose=null;if(r.Compensation is{Completion.IsCompleted:true}){int old=acquired;Assert.Empty(await r.Compensation.SettleOriginalsAsync());Assert.Equal(old,acquired);}});
        Assert.True(r.Compensation!.CleanupConfirmed);CompensationFence(r,false);Assert.Equal(InstallationCompensationStatus.Unconfirmed,r.Compensation.Completion.Result.Status);
    });
    [Fact]public Task CompensationHeldOriginalRegistryWriteOwnsTheAttemptAndCancelDoesNotWaitForItsLock()=>Execute(async r=>
    {
        await FailedDeployment(r,true);using var stop=new CancellationTokenSource();using var release=new ManualResetEventSlim();var entered=Signal();bool hold=true;
        r.Registry.Writing=(_,after)=>{if(after&&hold){hold=false;entered.SetResult();if(!release.Wait(Limit))throw new TimeoutException("PUBLIC_HELD_ORIGINAL_REVERSE_WRITE");}};
        r.Compensation=r.Session!.BeginCompensation(stop.Token);
        try{await entered.Task.WaitAsync(Limit);stop.Cancel();Assert.False(r.Compensation.Completion.IsCompleted);Assert.Throws<InstallationRecoveryException>(()=>r.Session.BeginCompensation());CompensationFence(r);}
        finally{release.Set();}
        var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);CompensationPrefix(r,2);CompensationFence(r);Assert.Equal(1,r.Registry.Writes);
    });
    [Theory][InlineData("ready")][InlineData("blocked")][InlineData("unknown")]
    public Task CompensationRequiresTheOriginalSettledFailureAndCannotBorrowAnotherOwner(string state)=>Execute(async r=>
    {
        await r.Commit();if(state=="blocked")File.WriteAllText(r.Store.PendingPath,"PUBLIC_UNKNOWN_OWNER");if(state=="unknown"){r.Port.HealthError=new IOException("PUBLIC_UNHEALTHY");r.Port.IoError=new IOException("PUBLIC_UNSETTLED");}
        var session=r.Begin();if(state=="ready")await session.Ready.WaitAsync(Limit);else await session.Completion.WaitAsync(Limit);
        Assert.Throws<InstallationRecoveryException>(()=>session.BeginCompensation());if(state=="ready"){await r.Peer!.Close();Assert.Equal(InstallationDeploymentSessionStatus.CleanExit,(await session.Completion.WaitAsync(Limit)).Status);}else Assert.Equal(state=="blocked"?InstallationDeploymentSessionStatus.ActivationBlocked:InstallationDeploymentSessionStatus.Unconfirmed,session.Completion.Result.Status);
    });

    [Fact]public Task CompensationSharedHostFenceRejectsWithoutAdoptingAnyProcessOrCreatingTicket()=>Execute(async r=>
    {
        await FailedDeployment(r);using var host=InstallationHostActivity.Open(r.App,false);int mutations=0;
        r.Compensation=r.Session!.BeginCompensation(faults:new(){BeforeMutation=_=>mutations++});var result=await r.Compensation.Completion.WaitAsync(Limit);
        Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Contains(Flatten(result.Errors),e=>e is InstallationBusyException);Assert.False(r.Compensation.PendingAttempted);Assert.False(File.Exists(r.Store.PendingPath));Assert.Equal(0,mutations);Assert.Equal(0,r.Registry.Writes);host.Require(r.App);r.LeaseHeld();CompensationPrefix(r,0);Assert.Empty(await r.Compensation.SettleOriginalsAsync());Assert.False(r.Compensation.CleanupConfirmed);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task CompensationCancellationAtTicketOrPlanDoesNotStartReverse(bool plan)=>Execute(async r=>
    {
        await FailedDeployment(r);using var stop=new CancellationTokenSource();int mutations=0;
        r.Compensation=r.Session!.BeginCompensation(stop.Token,new(){BeforeMutation=_=>mutations++,Stage=stage=>{if(stage==(plan?InstallationCompensationStage.Plan:InstallationCompensationStage.Ticket))stop.Cancel();}});
        var result=await r.Compensation.Completion.WaitAsync(Limit);Assert.Equal(InstallationCompensationStatus.Unconfirmed,result.Status);Assert.Equal(0,mutations);Assert.Equal(0,r.Registry.Writes);CompensationPrefix(r,0);CompensationFence(r);Assert.Equal(plan,File.Exists(Path.Combine(r.App,"compensation-plan-"+r.Compensation.Operation+".json")));Assert.False(File.Exists(r.Compensation.RetiredPath));
    });
    [Fact]public Task CompensationSamePendingSettlementAndSuccessfulSettlementStayTheOriginalTask()=>Execute(async r=>
    {
        await FailedDeployment(r);var firstFailure=new IOException("PUBLIC_FIRST_EXECUTION_CLOSE_FAILURE");var faults=new InstallationCompensationFaults{BeforeExecutionClose=()=>throw firstFailure};using var release=new ManualResetEventSlim();var entered=Signal();Task<IReadOnlyList<Exception>>? original=null;
        r.Compensation=r.Session!.BeginCompensation(faults:faults);Assert.Equal(InstallationCompensationStatus.Unconfirmed,(await r.Compensation.Completion.WaitAsync(Limit)).Status);
        faults.BeforeExecutionClose=()=>{entered.SetResult();if(!release.Wait(Limit))throw new TimeoutException("PUBLIC_HELD_SAME_OWNER_CLOSE");};
        await BodyAndCleanup(async()=>{original=r.Compensation.SettleOriginalsAsync();await entered.Task.WaitAsync(Limit);Assert.Same(original,r.Compensation.SettleOriginalsAsync());Assert.False(r.Compensation.CleanupConfirmed);CompensationFence(r);},async()=>{release.Set();faults.BeforeExecutionClose=null;if(original!=null)Assert.Empty(await original.WaitAsync(Limit));});
        Assert.Same(original,r.Compensation.SettleOriginalsAsync());Assert.True(r.Compensation.CleanupConfirmed);Assert.Equal(InstallationCompensationStatus.Unconfirmed,r.Compensation.Completion.Result.Status);CompensationFence(r,false);Assert.Empty(Directory.GetFiles(r.App,"compensation-finished-*.json"));
    });

}
