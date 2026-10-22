using KuaiYi.Core.Installation;
using KuaiYi.Core.Integration;
using KuaiYi.NativeHost;
using System.Text.Json;
using Xunit;
using Xunit.Abstractions;
using System.Diagnostics;
namespace KuaiYi.Core.Tests;

public sealed class InstallationPublicationTests(ITestOutputHelper output)
{
    private sealed class Registry:IHostRegistrationStore
    {
        internal int Reads,Writes;internal Action<int>? Reading=null;
        internal HostRegistrationSlot Chrome=new(false,null),Edge=new(false,null);
        internal Exception? ReadError=null,CloseError=null;internal Action<int,bool>? Writing;internal Action? CleanupAction=null;internal bool HoldCleanup=false;private bool retained;
        public bool CleanupConfirmed=>!retained&&!HoldCleanup;
        public HostRegistrationSlot Read(IntegrationBrowser browser){int n=Interlocked.Increment(ref Reads);Reading?.Invoke(n);if(ReadError!=null){retained=true;throw ReadError;}return browser==IntegrationBrowser.Chrome?Chrome:Edge;}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value){int n=++Writes;Writing?.Invoke(n,false);Assert.Equal(expected,browser==IntegrationBrowser.Chrome?Chrome:Edge);if(browser==IntegrationBrowser.Chrome)Chrome=new(true,value);else Edge=new(true,value);Writing?.Invoke(n,true);}
        public void FinalizeCleanup(){CleanupAction?.Invoke();if(CloseError!=null)throw CloseError;retained=false;}
    }
    private sealed class Rig:IDisposable
    {
        private static readonly List<object> Retained=[];private bool retain;
        internal void Retain(object original){retain=true;lock(Retained)Retained.Add((this,original));}
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-publication-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");internal string Data=>Path.Combine(Root,"data");
        internal readonly MaterializedPayloadSource Generation;internal readonly BootstrapSource Bootstrap;internal readonly Registry Registry=new();
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal Rig(){using var initial=Store.EnterRoleAdmission();Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_OFF"),"PUBLIC_PRESERVED");Generation=new(Path.Combine(Root,"generation-source"));Bootstrap=new(Path.Combine(Root,"bootstrap-source"));}
        internal async Task<(InstallationDeploymentScope Scope,InstallationGenerationCandidate G,InstallationBootstrapCandidate B)> Begin(CancellationToken cancellation=default,InstallationDeploymentFaults? faults=null,InstallationAdmissionFaults? admissionFaults=null)
        {var scope=new InstallationDeploymentScope(App,Data,Generation.Digest,Bootstrap.Digest,cancellation,admissionFaults,faults);await scope.AcquireAsync();await scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(Registry));var g=await scope.PrepareGenerationAsync(Generation.Root);return(scope,g,await scope.PrepareBootstrapAsync(Bootstrap.Root,g));}
        internal string Prepared(InstallationDeploymentScope scope)=>Path.Combine(App,"deployment-prepared-"+scope.Operation+".json");
        internal async Task<InstalledVersion> InstallOld(bool bootstrap=true,bool host=true)
        {
            var old=await Store.ApplyAsync(Generation.Root,Generation.Digest);if(bootstrap)await new InstallationBootstrapDeployment(App,Data).DeployAsync(Bootstrap.Root,Bootstrap.Digest);if(host)await new HostRegistrationTransaction(App,Data,Registry).RunAsync(true);Registry.Reads=Registry.Writes=0;return old;
        }
        internal Dictionary<string,string?> Published()=>new[]{Path.Combine(App,"current.json"),InstallationBootstrapDeployment.OwnerPath(App),InstallationBootstrapDeployment.ActivePath(App),Path.Combine(App,"host-registration.json")}.ToDictionary(p=>p,p=>File.Exists(p)?Convert.ToBase64String(File.ReadAllBytes(p)):null);
        internal void Unchanged(Dictionary<string,string?> before){Assert.Equal(before.OrderBy(x=>x.Key),Published().OrderBy(x=>x.Key));Assert.Equal(0,Registry.Writes);}
        public void Dispose(){Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(Data,"PUBLIC_OFF")));if(!retain)Directory.Delete(Root,true);}
    }
    private static IReadOnlyList<Exception> Errors(Exception error)=>error is AggregateException a?a.Flatten().InnerExceptions:[error];
    private static string[] LogicalPaths(Rig r)=>[Path.Combine(r.App,"current.json"),InstallationBootstrapDeployment.ActivePath(r.App),Path.Combine(r.App,"host-registration.json")];
    private static void Restored(Rig r,Dictionary<string,string?> before)
    {foreach(var path in LogicalPaths(r))Assert.Equal(before[path],File.Exists(path)?Convert.ToBase64String(File.ReadAllBytes(path)):null);}
    [Fact]public async Task ObservedForeignRegistryCannotBeForgottenByANewReadEvenIfItChangesBack()
    {
        using var r=new Rig();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);bool arm=false,restore=false;int foreignReadEnd=0;
        r.Registry.Reading=_=>{if(arm){arm=false;restore=true;r.Registry.Edge=new(true,"PUBLIC_TRANSIENT_FOREIGN",true);}};
        r.Registry.CleanupAction=()=>{if(restore){restore=false;foreignReadEnd=r.Registry.Reads;r.Registry.Edge=new(false,null);}};
        var result=await scope.CommitPreparedAsync(p,new(){Stage=s=>{if(s==DeploymentPublicationStage.Current)arm=true;}});
        Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.True(File.Exists(r.Store.PendingPath));Assert.True(File.Exists(Path.Combine(r.App,"current.json")));Assert.Equal(foreignReadEnd,r.Registry.Reads);Assert.Equal(0,r.Registry.Writes);Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task InitialAndOwnedUpgradeCommitOnlyAfterOriginalResourcesAndExactArchive(bool upgrade)
    {
        using var r=new Rig();InstalledVersion? old=upgrade?await r.InstallOld():null;byte[]? oldCore=old==null?null:File.ReadAllBytes(Path.Combine(old.DirectoryPath,"KuaiYi.Core.dll"));
        var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);int observed=0;
        var result=await scope.CommitPreparedAsync(p,new(){Stage=s=>{if(s>=DeploymentPublicationStage.Current&&s<=DeploymentPublicationStage.HostReceipt){observed++;Assert.True(File.Exists(r.Store.PendingPath));Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());}}});
        Assert.Equal(InstallationDeploymentStatus.Committed,result.Status);Assert.Empty(result.Errors);Assert.True(scope.CleanupConfirmed);Assert.Equal(5,observed);Assert.False(File.Exists(r.Store.PendingPath));Assert.False(p.IsLiveFor(scope));Assert.Equal(g.Version,r.Store.ReadActive());Assert.False(Directory.Exists(b.DirectoryPath));Assert.True(Directory.Exists(p.Plan.Bootstrap.CanonicalDirectory));Assert.Equal(2,r.Registry.Writes);Assert.Equal(p.Plan.Host.Targets.Chrome.Value,r.Registry.Chrome.Value);Assert.Equal(p.Plan.Host.Targets.Edge.Value,r.Registry.Edge.Value);
        using var terminal=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-committed-*.json"))));Assert.Equal("CommitCandidate",terminal.RootElement.GetProperty("variant").GetString());Assert.Equal(p.Sha256,terminal.RootElement.GetProperty("preparedSha256").GetString());Assert.True(terminal.RootElement.GetProperty("publicationRetention").GetProperty("CanonicalReady").GetBoolean());Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));Assert.Empty(await scope.SettleOriginalsAsync());if(old!=null)Assert.Equal(oldCore,File.ReadAllBytes(Path.Combine(old.DirectoryPath,"KuaiYi.Core.dll")));await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.CommitPreparedAsync(p));
    }
    [Theory][InlineData(1,false)][InlineData(2,false)][InlineData(3,false)][InlineData(4,false)][InlineData(5,false)][InlineData(1,true)][InlineData(2,true)][InlineData(3,true)][InlineData(4,true)][InlineData(5,true)]
    public async Task EveryProvisionalStageFailureOrCancellationRestoresInReverseIncludingHostReceipt(int step,bool cancel)
    {
        using var r=new Rig();await r.InstallOld();var before=r.Published();using var stop=new CancellationTokenSource();var(scope,g,b)=await r.Begin(stop.Token);var p=await scope.SealPreparedAsync(g,b);var failure=new IOException("PUBLIC_FORWARD_"+step);int reversed=0;
        var result=await scope.CommitPreparedAsync(p,new(){Stage=s=>{if(s==(DeploymentPublicationStage)((int)DeploymentPublicationStage.Current+step-1)){if(cancel)stop.Cancel();else throw failure;}if(s==DeploymentPublicationStage.AfterReverse)reversed++;}});
        Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,result.Status);Assert.Equal(step,reversed);Restored(r,before);Assert.Equal(before[InstallationBootstrapDeployment.OwnerPath(r.App)],Convert.ToBase64String(File.ReadAllBytes(InstallationBootstrapDeployment.OwnerPath(r.App))));Assert.True(Directory.Exists(p.Plan.Bootstrap.CanonicalDirectory));Assert.False(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);Assert.Contains(result.Errors.SelectMany(Errors),e=>cancel?e is OperationCanceledException:ReferenceEquals(e,failure));Assert.Empty(Directory.GetFiles(r.App,"deployment-committed-*.json"));
    }
    [Fact]public async Task InitialAbortRetainsOwnedBundleAndEmptyPhysicalKeysInTerminal()
    {
        using var r=new Rig();var before=r.Published();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var failure=new IOException("PUBLIC_AFTER_RECEIPT");var result=await scope.CommitPreparedAsync(p,new(){Stage=s=>{if(s==DeploymentPublicationStage.HostReceipt)throw failure;}});
        Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,result.Status);Restored(r,before);Assert.Equal(new(true,null),r.Registry.Chrome);Assert.Equal(new(true,null),r.Registry.Edge);Assert.True(File.Exists(InstallationBootstrapDeployment.OwnerPath(r.App)));Assert.True(Directory.Exists(p.Plan.Bootstrap.CanonicalDirectory));using var t=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"))));var retention=t.RootElement.GetProperty("publicationRetention");Assert.True(retention.GetProperty("Registry").GetProperty("Chrome").GetProperty("KeyExists").GetBoolean());Assert.Equal(p.Plan.Bootstrap.Owner,retention.GetProperty("Owner").GetString());Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(5)]
    public async Task FileOperationThatPerformsEffectThenThrowsIsObservedBeforeReverse(int step)
    {
        using var r=new Rig();await r.InstallOld();var before=r.Published();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var failure=new IOException("PUBLIC_MOVE_RECEIPT_LOST");bool injected=false;
        var result=await scope.CommitPreparedAsync(p,new(){Publish=(from,to,replace)=>{if(replace)File.Replace(from,to,null);else File.Move(from,to);if(!injected&&from.Contains("-"+step+"-forward.tmp",StringComparison.Ordinal)){injected=true;throw failure;}}});
        Assert.True(injected);Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,result.Status);Assert.Contains(failure,result.Errors.SelectMany(Errors));Restored(r,before);Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RegistrySideEffectThenExceptionIsProvisionalAndCannotSkipRollback(bool edge)
    {
        using var r=new Rig();await r.InstallOld();var before=r.Published();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var failure=new IOException("PUBLIC_REGISTRY_AFTER_WRITE");r.Registry.Writing=(n,after)=>{if(n==(edge?2:1)&&after)throw failure;};var result=await scope.CommitPreparedAsync(p);Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,result.Status);Assert.Contains(failure,result.Errors.SelectMany(Errors));Restored(r,before);Assert.Equal(p.Plan.Host.Before!.Manifest,r.Registry.Chrome.Value);Assert.Equal(p.Plan.Host.Before.Manifest,r.Registry.Edge.Value);Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData((int)DeploymentPublicationStage.ContainerCreated)][InlineData((int)DeploymentPublicationStage.OwnerWritten)][InlineData((int)DeploymentPublicationStage.BundleMoved)]
    public async Task UnsettledCanonicalPreparationNeverPublishesCurrentOrCleansUnknownOwnership(int stage)
    {
        using var r=new Rig();var before=r.Published();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var failure=new IOException("PUBLIC_CANONICAL_STAGE");var result=await scope.CommitPreparedAsync(p,new(){Stage=s=>{if((int)s==stage)throw failure;}});Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Restored(r,before);Assert.Contains(failure,result.Errors.SelectMany(Errors));Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(0,r.Registry.Writes);Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task LastPreClosingCancellationRevertsButAfterClosingCancellationOnlySettles(bool after)
    {
        using var r=new Rig();var before=r.Published();using var stop=new CancellationTokenSource();var(scope,g,b)=await r.Begin(stop.Token,new(){BeforeClosing=()=>{if(!after)stop.Cancel();return Task.CompletedTask;},AfterClosing=()=>{if(after)stop.Cancel();return Task.CompletedTask;}});var p=await scope.SealPreparedAsync(g,b);var result=await scope.CommitPreparedAsync(p);Assert.Equal(after?InstallationDeploymentStatus.Committed:InstallationDeploymentStatus.AbortedAfterPrepared,result.Status);if(!after)Restored(r,before);Assert.True(scope.CleanupConfirmed);Assert.False(File.Exists(r.Store.PendingPath));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ArchiveErrorIsUnknownOrExactHistoricalReceiptWithoutReplay(bool moved)
    {
        using var r=new Rig();var failure=new IOException("PUBLIC_ARCHIVE");var(scope,g,b)=await r.Begin(faults:new(){Archive=(from,to)=>{if(moved)File.Move(from,to);throw failure;}});var p=await scope.SealPreparedAsync(g,b);var task=scope.CommitPreparedAsync(p);var result=await task;Assert.Equal(moved?InstallationDeploymentStatus.CommittedReceiptUncertain:InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(failure,result.Errors.SelectMany(Errors));Assert.Equal(!moved,File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);int writes=r.Registry.Writes;Assert.Empty(await scope.SettleOriginalsAsync());Assert.Same(result,await task);Assert.Equal(writes,r.Registry.Writes);
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public async Task ReverseFailureStopsEveryEarlierDependencyAndPreservesBothErrors(int reverseStep)
    {
        using var r=new Rig();await r.InstallOld();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var forward=new IOException("PUBLIC_FORWARD_ORIGINAL");var reverse=new IOException("PUBLIC_REVERSE_ORIGINAL");var done=new List<int>();
        var result=await scope.CommitPreparedAsync(p,new(){Stage=s=>{if(s==DeploymentPublicationStage.HostReceipt)throw forward;},BeforeMutation=(step,back)=>{if(back){if(step==reverseStep)throw reverse;done.Add(step);}}});Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(forward,result.Errors.SelectMany(Errors));Assert.Contains(reverse,result.Errors.SelectMany(Errors));Assert.Equal(Enumerable.Range(reverseStep+1,5-reverseStep).Reverse(),done);Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(r.App,"deployment-committed-*.json"));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]
    public async Task ForeignReverseTargetIsPreservedAndStopsEarlierWrites(int step)
    {
        using var r=new Rig();await r.InstallOld();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var forward=new IOException("PUBLIC_FORWARD_FOREIGN");int attempts=0;
        var result=await scope.CommitPreparedAsync(p,new(){Stage=s=>{if(s==DeploymentPublicationStage.HostReceipt)throw forward;},BeforeMutation=(at,back)=>{if(back){attempts++;if(at==step){if(step is 3 or 4){if(step==3)r.Registry.Chrome=new(true,"PUBLIC_FOREIGN",true);else r.Registry.Edge=new(true,"PUBLIC_FOREIGN",true);}else File.WriteAllText(LogicalPaths(r)[step==1?0:step==2?1:2],"PUBLIC_FOREIGN");}}}});
        Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Equal(6-step,attempts);Assert.Contains(forward,result.Errors.SelectMany(Errors));Assert.True(File.Exists(r.Store.PendingPath));if(step is 3 or 4)Assert.True((step==3?r.Registry.Chrome:r.Registry.Edge).ForeignContent);else Assert.Equal("PUBLIC_FOREIGN",File.ReadAllText(LogicalPaths(r)[step==1?0:step==2?1:2]));Assert.True(scope.CleanupConfirmed);
    }
    [Fact]public async Task AbortHeldRegistryWriteJoinsTheOriginalAndDoesNotWaitForAWriterLock()
    {
        using var r=new Rig();var before=r.Published();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);r.Registry.Writing=(n,after)=>{if(n==1&&after){entered.SetResult();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("PUBLIC_HELD_WRITE");}};
        var run=scope.CommitPreparedAsync(p);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Task<InstallationDeploymentOutcome>? abort=null;
        try{abort=scope.AbortAfterPreparedAsync();Assert.Same(run,abort);Assert.False(run.IsCompleted);Assert.False(scope.CleanupConfirmed);Assert.False(p.IsLiveFor(scope));Assert.False(g.IsLiveFor(scope));Assert.False(b.IsLiveFor(scope));await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.CommitPreparedAsync(p));}
        finally{release.Set();}
        Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,(await abort!).Status);Restored(r,before);Assert.True(scope.CleanupConfirmed);
    }
    [Fact]public async Task OriginalWriteAndCloseFaultsRemainTogetherAndNoNewRegistryReadOrReverseIsAllowed()
    {
        using var r=new Rig();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var body=new IOException("PUBLIC_WRITE_BODY");var close=new IOException("PUBLIC_WRITE_CLOSE");int reads=0;r.Registry.Writing=(n,after)=>{if(n==1&&after){reads=r.Registry.Reads;r.Registry.HoldCleanup=true;r.Registry.CloseError=close;throw body;}};
        InstallationDeploymentOutcome result;
        try{result=await scope.CommitPreparedAsync(p);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(body,result.Errors.SelectMany(Errors));Assert.Contains(close,result.Errors.SelectMany(Errors));Assert.Equal(reads,r.Registry.Reads);Assert.Equal(1,r.Registry.Writes);Assert.False(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));}
        finally{r.Registry.HoldCleanup=false;r.Registry.CloseError=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(1,r.Registry.Writes);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task NewPublicationMetadataPartialOrOriginalCloseUnknownIsNeverDowngradedToRollback(bool held)
    {
        using var r=new Rig();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var body=new IOException("PUBLIC_TEMP_BODY");var close=new IOException("PUBLIC_TEMP_CLOSE");FileStream? original=null;
        bool Target(string path)=>path.EndsWith("-1-forward.tmp",StringComparison.Ordinal);
        var hooks=new InstallationPreparationFaults{Acquired=(kind,path,value)=>{if(kind=="publication-output"&&Target(path))original=Assert.IsType<FileStream>(value);},WriteMetadata=(path,stream,bytes)=>{if(Target(path)){stream.Write(bytes.Span[..7]);throw body;}stream.Write(bytes.Span);},BeforeClose=(kind,path)=>{if(held&&kind=="publication-output"&&Target(path))throw close;}};
        try{var result=await scope.CommitPreparedAsync(p,publicationIOFaults:hooks);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(body,result.Errors.SelectMany(Errors));if(held)Assert.Contains(close,result.Errors.SelectMany(Errors));Assert.Equal(!held,scope.CleanupConfirmed);Assert.Equal(0,r.Registry.Writes);Assert.False(File.Exists(Path.Combine(r.App,"current.json")));Assert.True(File.Exists(r.Store.PendingPath));}
        finally{hooks.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(original!.SafeFileHandle.IsClosed);Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task DamagedOldRestoreTargetAfterSealCannotBeCertifiedAsRolledBack(bool bootstrap)
    {
        using var r=new Rig();var old=await r.InstallOld();string target=bootstrap?Path.Combine(InstallationBootstrapDeployment.BundlePath(r.App,InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App))).Bundle),"KuaiYi.Core.dll"):Path.Combine(old.DirectoryPath,"KuaiYi.Core.dll");var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var error=new IOException("PUBLIC_RESTORE_TARGET_DAMAGED");
        var result=await scope.CommitPreparedAsync(p,new(){Stage=at=>{if(at==DeploymentPublicationStage.HostReceipt){File.AppendAllText(target,"PUBLIC_TAMPERED_OLD");throw error;}}});
        Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(error,result.Errors.SelectMany(Errors));Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(p.Plan.TargetCurrent,Convert.ToBase64String(File.ReadAllBytes(Path.Combine(r.App,"current.json"))));Assert.Equal(p.Plan.Host.Receipt,Convert.ToBase64String(File.ReadAllBytes(Path.Combine(r.App,"host-registration.json"))));Assert.Equal(2,r.Registry.Writes);Assert.True(scope.CleanupConfirmed);
    }
    [Fact]public async Task AttemptedWriteForeignObservationCannotBeReplacedByLaterTargetRead()
    {
        using var r=new Rig();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);bool restore=false;int reads=0;r.Registry.Writing=(n,after)=>{if(n==1&&after){r.Registry.Chrome=new(true,"PUBLIC_ATTEMPT_FOREIGN",true);restore=true;}};r.Registry.CleanupAction=()=>{if(restore){restore=false;reads=r.Registry.Reads;r.Registry.Chrome=new(true,p.Plan.Host.Targets.Chrome.Value);}};
        var result=await scope.CommitPreparedAsync(p);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Equal(reads,r.Registry.Reads);Assert.Equal(1,r.Registry.Writes);Assert.True(File.Exists(r.Store.PendingPath));Assert.True(File.Exists(Path.Combine(r.App,"current.json")));Assert.True(scope.CleanupConfirmed);
    }
    [Fact]public async Task AdmissionAndRegistryClosingErrorsAreBothPreservedByOriginalFinalizer()
    {
        using var r=new Rig();var admission=new IOException("PUBLIC_ADMISSION_CLOSE");var registry=new IOException("PUBLIC_REG_CLOSE");var af=new InstallationAdmissionFaults{BeforeClose=()=>throw admission};var(scope,g,b)=await r.Begin(faults:new(){AfterClosing=()=>{r.Registry.HoldCleanup=true;r.Registry.CloseError=registry;return Task.CompletedTask;}},admissionFaults:af);var p=await scope.SealPreparedAsync(g,b);
        try{var result=await scope.CommitPreparedAsync(p);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(admission,result.Errors.SelectMany(Errors));Assert.Contains(registry,result.Errors.SelectMany(Errors));Assert.False(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-committed-*.json"));}
        finally{af.BeforeClose=null;r.Registry.HoldCleanup=false;r.Registry.CloseError=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
    }
    [Fact]public async Task HistoricalReceiptCannotConfirmAMoveWhenItsOriginalReadCloseIsUnknown()
    {
        using var r=new Rig();var archive=new IOException("PUBLIC_MOVE_ACK_LOST");var close=new IOException("PUBLIC_HISTORY_CLOSE");bool held=true;var(scope,g,b)=await r.Begin(faults:new(){Archive=(from,to)=>{File.Move(from,to);throw archive;},BeforeClose=path=>{if(held&&Path.GetFileName(path).StartsWith("deployment-committed-",StringComparison.Ordinal))throw close;}});var p=await scope.SealPreparedAsync(g,b);
        try{var result=await scope.CommitPreparedAsync(p);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(archive,result.Errors.SelectMany(Errors));Assert.Contains(close,result.Errors.SelectMany(Errors));Assert.False(scope.CleanupConfirmed);Assert.False(File.Exists(r.Store.PendingPath));Assert.Single(Directory.GetFiles(r.App,"deployment-committed-*.json"));}
        finally{held=false;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CanonicalMoveBeforeOrAfterEffectFailureKeepsAllOwnershipAndBlocksPublication(bool moved)
    {
        using var r=new Rig();var before=r.Published();var(scope,g,b)=await r.Begin();var p=await scope.SealPreparedAsync(g,b);var failure=new IOException("PUBLIC_B_MOVE_UNKNOWN");var result=await scope.CommitPreparedAsync(p,new(){MoveBundle=(from,to)=>{if(moved)Directory.Move(from,to);throw failure;}});Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(failure,result.Errors.SelectMany(Errors));Restored(r,before);Assert.Equal(!moved,Directory.Exists(b.DirectoryPath));Assert.Equal(moved,Directory.Exists(p.Plan.Bootstrap.CanonicalDirectory));Assert.True(File.Exists(InstallationBootstrapDeployment.OwnerPath(r.App)));Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData("Current")][InlineData("Active")][InlineData("Chrome")][InlineData("Edge")][InlineData("HostReceipt")][InlineData("Closing")][InlineData("Archive")]
    public async Task ActualSyntheticChildCrashKeepsPendingOrAnExactCommittedHistoryWithoutAdoption(string phase)
    {
        using var r=new Rig();File.WriteAllText(Path.Combine(r.Root,"PUBLIC_CRASH_ALLOWED"),"PUBLIC_SYNTHETIC_ONLY");string driver=Path.Combine(MaterializedPayloadSource.Repo,"tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll");var info=new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet"){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true};foreach(string arg in new[]{driver,"--deployment-publication-crash",r.Root,r.Generation.Digest,r.Bootstrap.Digest,phase})info.ArgumentList.Add(arg);var child=Process.Start(info)!;var originalExit=child.WaitForExitAsync();var stdout=child.StandardOutput.ReadToEndAsync();var stderr=child.StandardError.ReadToEndAsync();var errors=new List<Exception>();
        try{await originalExit.WaitAsync(TimeSpan.FromSeconds(12));Assert.Equal(73,child.ExitCode);Assert.Equal("PUBLIC_DEPLOYMENT_CRASH_"+phase,(await stdout.WaitAsync(TimeSpan.FromSeconds(3))).Trim());Assert.Equal("",await stderr.WaitAsync(TimeSpan.FromSeconds(3)));}
        catch(Exception body){errors.Add(body);}
        try{if(!child.HasExited)child.Kill();}catch(Exception kill){errors.Add(kill);}
        try{await originalExit.WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception exit){errors.Add(exit);}
        try{await stdout.WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception read){errors.Add(read);}
        try{await stderr.WaitAsync(TimeSpan.FromSeconds(3));}catch(Exception read){errors.Add(read);}
        if(originalExit.IsCompletedSuccessfully&&stdout.IsCompletedSuccessfully&&stderr.IsCompletedSuccessfully)
        {try{if(!child.HasExited)throw new IOException("PUBLIC_CHILD_EXIT_UNCONFIRMED");child.Dispose();}catch(Exception dispose){errors.Add(dispose);r.Retain((child,originalExit,stdout,stderr));}}
        else r.Retain((child,originalExit,stdout,stderr));
        InstallationErrors.Throw(errors);
        bool committed=phase=="Archive";Assert.Equal(!committed,File.Exists(r.Store.PendingPath));if(committed){Assert.Single(Directory.GetFiles(r.App,"deployment-committed-*.json"));using var read=r.Store.EnterRoleAdmission();}else Assert.Throws<InstallationRecoveryException>(()=>r.Store.EnterRoleAdmission());Assert.True(File.Exists(Path.Combine(r.Root,"PUBLIC_SYNTHETIC_REGISTRY.json")));output.WriteLine($"PUBLIC_CHILD phase={phase} exit=73 driverSHA256={MaterializedPayloadSource.Hash(driver)} originalExitAndIO=Settled registry=SyntheticFileOnly");
    }
}
