using KuaiYi.Core.Installation;
using KuaiYi.Core.Integration;
using KuaiYi.NativeHost;
using System.Text.Json;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class InstallationPreparedPlanTests
{
    private sealed class Registry:IHostRegistrationStore
    {
        internal int Reads,Writes;internal Action<int>? Reading;
        internal HostRegistrationSlot Chrome=new(false,null),Edge=new(false,null);
        internal Exception? ReadError=null,CloseError=null;private bool retained;
        public bool CleanupConfirmed=>!retained;
        public HostRegistrationSlot Read(IntegrationBrowser browser){int n=Interlocked.Increment(ref Reads);Reading?.Invoke(n);if(ReadError!=null){retained=true;throw ReadError;}return browser==IntegrationBrowser.Chrome?Chrome:Edge;}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value){Writes++;Assert.Equal(expected,browser==IntegrationBrowser.Chrome?Chrome:Edge);if(browser==IntegrationBrowser.Chrome)Chrome=new(true,value);else Edge=new(true,value);}
        public void FinalizeCleanup(){if(CloseError!=null)throw CloseError;retained=false;}
    }
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-prepared-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");internal string Data=>Path.Combine(Root,"data");
        internal readonly MaterializedPayloadSource Generation;internal readonly BootstrapSource Bootstrap;internal readonly Registry Registry=new();
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal Rig(){using var initial=Store.EnterRoleAdmission();Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_OFF"),"PUBLIC_PRESERVED");Generation=new(Path.Combine(Root,"generation-source"));Bootstrap=new(Path.Combine(Root,"bootstrap-source"));}
        internal async Task<(InstallationDeploymentScope Scope,InstallationGenerationCandidate G,InstallationBootstrapCandidate B)> Begin(CancellationToken cancellation=default,InstallationDeploymentFaults? faults=null)
        {var scope=new InstallationDeploymentScope(App,Data,Generation.Digest,Bootstrap.Digest,cancellation,faults:faults);await scope.AcquireAsync();await scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(Registry));var g=await scope.PrepareGenerationAsync(Generation.Root);return(scope,g,await scope.PrepareBootstrapAsync(Bootstrap.Root,g));}
        internal string Prepared(InstallationDeploymentScope scope)=>Path.Combine(App,"deployment-prepared-"+scope.Operation+".json");
        internal async Task<InstalledVersion> InstallOld(bool bootstrap=true,bool host=true)
        {
            var old=await Store.ApplyAsync(Generation.Root,Generation.Digest);if(bootstrap)await new InstallationBootstrapDeployment(App,Data).DeployAsync(Bootstrap.Root,Bootstrap.Digest);if(host)await new HostRegistrationTransaction(App,Data,Registry).RunAsync(true);Registry.Reads=Registry.Writes=0;return old;
        }
        internal Dictionary<string,string?> Published()=>new[]{Path.Combine(App,"current.json"),InstallationBootstrapDeployment.OwnerPath(App),InstallationBootstrapDeployment.ActivePath(App),Path.Combine(App,"host-registration.json")}.ToDictionary(p=>p,p=>File.Exists(p)?Convert.ToBase64String(File.ReadAllBytes(p)):null);
        internal void Unchanged(Dictionary<string,string?> before){Assert.Equal(before.OrderBy(x=>x.Key),Published().OrderBy(x=>x.Key));Assert.Equal(0,Registry.Writes);}
        public void Dispose(){Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(Data,"PUBLIC_OFF")));Directory.Delete(Root,true);}
    }
    private static IReadOnlyList<Exception> Errors(Exception error)=>error is AggregateException a?a.Flatten().InnerExceptions:[error];
    [Fact]public async Task TargetPayloadChangedDuringOriginalRegistryReadCannotQualifyPrepared()
    {
        using var r=new Rig();var(scope,g,b)=await r.Begin();using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        r.Registry.Reading=n=>{if(n==3){entered.SetResult();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("PUBLIC_PLAN_BARRIER");}};
        var seal=scope.SealPreparedAsync(g,b);await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try{File.AppendAllText(Path.Combine(g.Version.DirectoryPath,"KuaiYi.Core.dll"),"PUBLIC_CHANGED_DURING_READ");}
        finally{release.Set();}
        var error=await Record.ExceptionAsync(async()=>await seal);if(error==null)await scope.AbortAfterPreparedAsync();
        Assert.NotNull(error);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.False(File.Exists(r.Prepared(scope)));Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);Assert.Equal(0,r.Registry.Writes);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task FullySealedPlanBindsTargetsAndOriginalOwnershipWithoutPublishingAndAbortsWithItsOwnSHA(bool upgrade)
    {
        using var r=new Rig();InstalledVersion? old=null;if(upgrade){old=await r.InstallOld();r.Generation.Version="2.0";r.Generation.Catalog();r.Bootstrap.Version="2.0";r.Bootstrap.Catalog();}
        var before=r.Published();var(scope,g,b)=await r.Begin();var prepared=await scope.SealPreparedAsync(g,b);Assert.True(prepared.IsLiveFor(scope));Assert.Same(b,prepared.Bootstrap);Assert.Equal(scope.Operation,prepared.Plan.Operation);Assert.Equal(g.Version,prepared.Plan.Generation);Assert.Equal(g.SourceCatalogSha256,prepared.Plan.GenerationSourceSha256);Assert.Equal(b.CatalogSha256,prepared.Plan.BootstrapSourceSha256);Assert.Equal(b.DirectoryPath,prepared.Plan.Bootstrap.InactiveDirectory);Assert.Equal(InstallationBootstrapDeployment.BundlePath(r.App,Guid.ParseExact(scope.Bundle,"N")),prepared.Plan.Bootstrap.CanonicalDirectory);Assert.False(Directory.Exists(prepared.Plan.Bootstrap.CanonicalDirectory));Assert.InRange(new FileInfo(r.Prepared(scope)).Length,1,131072);Assert.Equal(prepared.Sha256,MaterializedPayloadSource.Hash(r.Prepared(scope)));r.Unchanged(before);
        using(var current=JsonDocument.Parse(Convert.FromBase64String(prepared.Plan.TargetCurrent))){Assert.Equal(scope.Generation,current.RootElement.GetProperty("active").GetProperty("id").GetString());if(old==null)Assert.Equal(JsonValueKind.Null,current.RootElement.GetProperty("previous").ValueKind);else Assert.Equal(Path.GetFileName(old.DirectoryPath),current.RootElement.GetProperty("previous").GetProperty("id").GetString());}
        var target=JsonSerializer.Deserialize<InstallationHostOwnership>(Convert.FromBase64String(prepared.Plan.Host.Receipt))!;Assert.Equal(g.Version,target.Version);Assert.Equal(Path.Combine(g.Version.DirectoryPath,InstallationPayload.HostManifestName),target.Manifest);Assert.Equal(target.Manifest,prepared.Plan.Host.Targets.Chrome.Value);Assert.Equal(target.Manifest,prepared.Plan.Host.Targets.Edge.Value);Assert.Equal(old,prepared.Plan.Host.Before?.Version);if(upgrade)Assert.Equal(before[InstallationBootstrapDeployment.OwnerPath(r.App)],prepared.Plan.Bootstrap.Owner);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.SealPreparedAsync(g,b));Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,(await scope.AbortAfterPreparedAsync()).Status);Assert.False(prepared.IsLiveFor(scope));Assert.True(scope.CleanupConfirmed);r.Unchanged(before);Assert.Equal(6,r.Registry.Reads);
        using var terminal=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"))));Assert.Equal("AbortAfterPrepared",terminal.RootElement.GetProperty("variant").GetString());Assert.Equal(prepared.Sha256,terminal.RootElement.GetProperty("preparedSha256").GetString());Assert.Empty(await scope.SettleOriginalsAsync());
    }

    [Theory][InlineData("same-path-no-receipt")][InlineData("foreign-content")][InlineData("stale-version")][InlineData("empty-owner")][InlineData("noncanonical")][InlineData("damaged-old-payload")]
    public async Task BeforeObservationNeverAuthorizesUnownedOrStaleHostRegistration(string fault)
    {
        using var r=new Rig();var old=await r.InstallOld(host:fault is not("same-path-no-receipt" or "foreign-content"));string path=Path.Combine(r.App,"host-registration.json");
        if(fault=="same-path-no-receipt")r.Registry.Chrome=new(true,Path.Combine(old.DirectoryPath,InstallationPayload.HostManifestName));
        else if(fault=="foreign-content")r.Registry.Edge=new(true,null,true);
        else if(fault=="damaged-old-payload")File.AppendAllText(Path.Combine(old.DirectoryPath,"KuaiYi.Core.dll"),"PUBLIC_DAMAGED_OLD_CORE");
        else
        {
            var receipt=JsonSerializer.Deserialize<InstallationHostOwnership>(File.ReadAllBytes(path))!;
            if(fault=="empty-owner")receipt=receipt with{Owner=Guid.Empty};else if(fault=="stale-version")receipt=receipt with{Version=old with{CatalogSha256=new string('A',64)}};
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(receipt);File.WriteAllBytes(path,fault=="noncanonical"?[(byte)' ',..bytes]:bytes);
        }
        var before=r.Published();var(scope,g,b)=await r.Begin();Assert.NotNull(await Record.ExceptionAsync(()=>scope.SealPreparedAsync(g,b)));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.False(File.Exists(r.Prepared(scope)));Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));r.Unchanged(before);
    }

    [Theory][InlineData("owner")][InlineData("active-generation")][InlineData("unowned-bundles")]
    public async Task OriginalBootstrapReceiptAndContainerNeedRealOwnershipBeforeAnyPlanWrite(string fault)
    {
        using var r=new Rig();var old=await r.InstallOld(bootstrap:fault!="unowned-bundles",host:false);
        if(fault=="unowned-bundles")Directory.CreateDirectory(InstallationBootstrapDeployment.BundlesPath(r.App));
        else if(fault=="owner")
        {string path=InstallationBootstrapDeployment.OwnerPath(r.App);var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(File.ReadAllBytes(path));File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(owner with{UserData=Path.Combine(r.Root,"PUBLIC_OTHER_DATA")}));}
        else
        {string path=InstallationBootstrapDeployment.ActivePath(r.App);var receipt=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(path));File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(receipt with{Generation=old with{CatalogSha256=new string('A',64)}}));}
        var before=r.Published();var(scope,g,b)=await r.Begin();Assert.NotNull(await Record.ExceptionAsync(()=>scope.SealPreparedAsync(g,b)));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.False(File.Exists(r.Prepared(scope)));Assert.True(scope.CleanupConfirmed);r.Unchanged(before);
    }

    [Theory][InlineData("write")][InlineData("flush")][InlineData("close")][InlineData("foreign")]
    public async Task AttemptedPlanPartialFlushCloseAndCreateNewConflictStayPermanentlyUnconfirmed(string fault)
    {
        using var r=new Rig();var before=r.Published();var(scope,g,b)=await r.Begin();string path=r.Prepared(scope);var failure=new IOException("PUBLIC_PLAN_"+fault);FileStream? original=null;if(fault=="foreign")File.WriteAllText(path,"PUBLIC_FOREIGN_PLAN");
        var hooks=new InstallationPreparationFaults{Acquired=(kind,at,value)=>{if(kind=="metadata-output"&&at==path)original=Assert.IsType<FileStream>(value);},WriteMetadata=(at,stream,bytes)=>{if(fault=="write"&&at==path){stream.Write(bytes.Span[..9]);throw failure;}stream.Write(bytes.Span);},BeforeFlush=(at,_)=>{if(fault=="flush"&&at==path)throw failure;},BeforeClose=(kind,at)=>{if(fault=="close"&&kind=="metadata-output"&&at==path)throw failure;}};
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.SealPreparedAsync(g,b,hooks));Assert.NotNull(error);if(fault!="foreign")Assert.Contains(failure,Errors(error));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));Assert.Equal(fault!="close",scope.CleanupConfirmed);if(fault=="write")Assert.Equal(9,new FileInfo(path).Length);if(fault=="foreign")Assert.Equal("PUBLIC_FOREIGN_PLAN",File.ReadAllText(path));
        }
        finally{hooks.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        if(original!=null)Assert.True(original.SafeFileHandle.IsClosed);Assert.True(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);r.Unchanged(before);
    }

    [Theory][InlineData((int)GenerationPreparationStage.SourceVerified)][InlineData((int)GenerationPreparationStage.BeforeQualification)][InlineData((int)GenerationPreparationStage.AfterQualification)]
    public async Task CancellationVictoryIsLinearizedBeforeOrAfterTheOriginalQualification(int phase)
    {
        using var r=new Rig();using var stop=new CancellationTokenSource();var(scope,g,b)=await r.Begin(stop.Token);var error=await Record.ExceptionAsync(()=>scope.SealPreparedAsync(g,b,new(){Stage=s=>{if((int)s==phase)stop.Cancel();}}));Assert.NotNull(error);Assert.Contains(Errors(error),x=>x is OperationCanceledException);var result=await scope.Finalization;
        Assert.Equal(phase==(int)GenerationPreparationStage.SourceVerified?InstallationDeploymentStatus.AbortedBeforePrepared:phase==(int)GenerationPreparationStage.BeforeQualification?InstallationDeploymentStatus.Unconfirmed:InstallationDeploymentStatus.AbortedAfterPrepared,result.Status);Assert.True(scope.CleanupConfirmed);Assert.False(g.IsLiveFor(scope));Assert.Equal(0,r.Registry.Writes);Assert.Equal(phase==(int)GenerationPreparationStage.BeforeQualification,File.Exists(r.Store.PendingPath));
    }

    [Fact]public async Task DeliveredQualificationRetiresOnCancellationButSameOwnerMayAbortAfterPrepared()
    {
        using var r=new Rig();using var stop=new CancellationTokenSource();var(scope,g,b)=await r.Begin(stop.Token);var value=await scope.SealPreparedAsync(g,b);Assert.True(value.IsLiveFor(scope));stop.Cancel();Assert.False(value.IsLiveFor(scope));Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,(await scope.AbortAfterPreparedAsync()).Status);Assert.True(scope.CleanupConfirmed);Assert.Equal(0,r.Registry.Writes);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task AbortDuringOriginalRegistryReadOrPlanFlushCannotAbandonOriginalWork(bool flush)
    {
        using var r=new Rig();using var release=new ManualResetEventSlim();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var(scope,g,b)=await r.Begin();string path=r.Prepared(scope);
        void Hold(){entered.TrySetResult();if(!release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("PUBLIC_PLAN_HELD");}
        if(!flush)r.Registry.Reading=n=>{if(n==3)Hold();};
        var seal=scope.SealPreparedAsync(g,b,new(){BeforeFlush=(at,_)=>{if(flush&&at==path)Hold();}});await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Task<InstallationDeploymentOutcome>? final=null;
        try{final=scope.AbortBeforePreparedAsync();Assert.False(final.IsCompleted);Assert.False(seal.IsCompleted);Assert.False(scope.CleanupConfirmed);await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.SealPreparedAsync(g,b));}
        finally{release.Set();}
        Assert.NotNull(await Record.ExceptionAsync(async()=>await seal));Assert.Equal(flush?InstallationDeploymentStatus.Unconfirmed:InstallationDeploymentStatus.AbortedBeforePrepared,(await final!).Status);Assert.True(scope.CleanupConfirmed);Assert.Equal(0,r.Registry.Writes);
    }

    [Fact]public async Task PreparedOriginalRegistryReadAndCloseErrorsRemainTogetherAndNeverReadTheNextSlot()
    {
        using var r=new Rig();var(scope,g,b)=await r.Begin();var read=new IOException("PUBLIC_PLAN_REG_READ");var close=new IOException("PUBLIC_PLAN_REG_CLOSE");r.Registry.ReadError=read;r.Registry.CloseError=close;
        try{var error=await Record.ExceptionAsync(()=>scope.SealPreparedAsync(g,b));Assert.NotNull(error);Assert.Contains(read,Errors(error));Assert.Contains(close,Errors(error));Assert.Equal(3,r.Registry.Reads);Assert.False(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.False(File.Exists(r.Prepared(scope)));}
        finally{r.Registry.CloseError=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.Equal(3,r.Registry.Reads);Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(0,r.Registry.Writes);
    }

    [Fact]public async Task ExistingBootstrapReadAndCloseUseTheOriginalPreparedChannelRatherThanLegacyUsing()
    {
        using var r=new Rig();await r.InstallOld();var old=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(r.App)));string path=Path.Combine(InstallationBootstrapDeployment.BundlePath(r.App,old.Bundle),"KuaiYi.Core.dll");var(scope,g,b)=await r.Begin();var read=new IOException("PUBLIC_OLD_B_READ");var close=new IOException("PUBLIC_OLD_B_CLOSE");FileStream? original=null;int opens=0;
        var hooks=new InstallationPreparationFaults{Acquired=(kind,at,value)=>{if(kind=="hash-input"&&at==path){opens++;original=Assert.IsType<FileStream>(value);throw read;}},BeforeClose=(kind,at)=>{if(kind=="hash-input"&&at==path)throw close;}};
        try{var error=await Record.ExceptionAsync(()=>scope.SealPreparedAsync(g,b,hooks));Assert.NotNull(error);Assert.Contains(read,Errors(error));Assert.Contains(close,Errors(error));Assert.False(original!.SafeFileHandle.IsClosed);Assert.False(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);}
        finally{hooks.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(original!.SafeFileHandle.IsClosed);Assert.Equal(1,opens);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(0,r.Registry.Writes);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ClosingPlanReplacementOrOriginalNewReadCloseFaultCannotArchiveQualifiedPlan(bool held)
    {
        using var r=new Rig();bool closing=false;var(scope,g,b)=await r.Begin(faults:new(){AfterClosing=()=>{closing=true;return Task.CompletedTask;}});string path=r.Prepared(scope);var read=new IOException("PUBLIC_PLAN_FINAL_READ");var close=new IOException("PUBLIC_PLAN_FINAL_CLOSE");FileStream? original=null;
        var hooks=new InstallationPreparationFaults{Acquired=(kind,at,value)=>{if(held&&closing&&kind=="read"&&at==path){original=Assert.IsType<FileStream>(value);throw read;}},BeforeClose=(kind,at)=>{if(held&&closing&&kind=="read"&&at==path)throw close;}};var value=await scope.SealPreparedAsync(g,b,hooks);if(!held)File.WriteAllText(path,"PUBLIC_FOREIGN_PREPARED");
        try{var result=await scope.AbortAfterPreparedAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.False(value.IsLiveFor(scope));Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(!held,scope.CleanupConfirmed);if(held){Assert.Contains(read,result.Errors.SelectMany(Errors));Assert.Contains(close,result.Errors.SelectMany(Errors));Assert.False(original!.SafeFileHandle.IsClosed);}else Assert.Equal("PUBLIC_FOREIGN_PREPARED",File.ReadAllText(path));}
        finally{hooks.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.Equal(0,r.Registry.Writes);
    }

    [Fact]public async Task ForeignPlanReplacementBeforeQualificationCannotCreateAnAfterPreparedCapability()
    {
        using var r=new Rig();var(scope,g,b)=await r.Begin();string path=r.Prepared(scope);Assert.NotNull(await Record.ExceptionAsync(()=>scope.SealPreparedAsync(g,b,new(){Stage=s=>{if(s==GenerationPreparationStage.BeforeQualification)File.WriteAllText(path,"PUBLIC_FOREIGN_BEFORE_QUALIFICATION");}})));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.AbortAfterPreparedAsync());Assert.Equal("PUBLIC_FOREIGN_BEFORE_QUALIFICATION",File.ReadAllText(path));Assert.True(File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);Assert.Equal(0,r.Registry.Writes);
    }

    [Fact]public void PreparedChannelCannotWriteAnyOtherRootFileOrCreateCopyAndMoveDirectories()
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-plan-only-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);string arbitrary=Path.Combine(root,"PUBLIC_KEEP"),plan=Path.Combine(root,"PUBLIC_PLAN");File.WriteAllText(arbitrary,"PUBLIC_BYTES");
        try
        {
            var original=new InstallationPreparationIO(root,Path.Combine(root,"stage"),Path.Combine(root,"final"),Path.Combine(root,"intent"),default,null);var io=original.CreatePreparedChannel(root,plan,null);
            Assert.Throws<InvalidOperationException>(()=>io.CreateStaging());Assert.Throws<InvalidOperationException>(()=>io.MoveToFinal());Assert.Throws<InvalidOperationException>(()=>io.CopyPayload(arbitrary,arbitrary,new("PUBLIC_KEEP",12,new string('A',64))));Assert.Throws<InvalidDataException>(()=>io.WriteMetadata(arbitrary,"PUBLIC_BAD"u8.ToArray(),1024));Assert.Equal("PUBLIC_BYTES",File.ReadAllText(arbitrary));Assert.False(File.Exists(plan));Assert.True(original.CleanupConfirmed);Assert.True(original.StorageUnconfirmed);
        }
        finally{Directory.Delete(root,true);}
    }
}
