using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests
{
    // Synthetic named-value adapter; no Microsoft.Win32 or Windows API. Neighbours never participate in compare/write.
    private sealed class StartupRegistry(IInstallationDeploymentPublicationRegistry host):IInstallationStartupRegistry
    {
        internal InstallationStartupValue Value=new(false);
        internal readonly Dictionary<string,string> Neighbours=new(){["PUBLIC_OTHER_PRODUCT"]="PUBLIC_UNCHANGED"};
        internal Action? BeforeWrite,AfterWrite,Close;
        internal int Writes;
        private bool conflict,unsettled;
        private Task? original;
        public bool ConflictObserved=>conflict||host.ConflictObserved;
        public bool CleanupConfirmed=>!unsettled&&(original==null||original.IsCompleted)&&host.CleanupConfirmed;
        public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync()=>host.CaptureAsync();
        public Task<InstallationDeploymentRegistrationSnapshot> ReadForPreparationAsync()=>host.ReadForPreparationAsync();
        public Task VerifyAsync()=>host.VerifyAsync();
        public Task<InstallationDeploymentRegistrationSnapshot> ReadPublishedAsync()=>host.ReadPublishedAsync();
        public Task WriteDefaultAsync(bool chrome,InstallationDeploymentRegistrationSlot expected,string? value)=>host.WriteDefaultAsync(chrome,expected,value);
        private Task<T> Own<T>(Func<T> action)
        {
            if(!CleanupConfirmed||ConflictObserved)throw new IOException("PUBLIC_STARTUP_ORIGINAL_UNSETTLED");
            var begin=Signal();var task=Task.Run(async()=>{await begin.Task;return action();});original=task;begin.SetResult();return task;
        }
        public Task<InstallationStartupValue> ReadStartupAsync()=>Own(()=>Value);
        public Task WriteStartupAsync(InstallationStartupValue expected,InstallationStartupValue next)=>Own(()=>
        {
            BeforeWrite?.Invoke();if(Value!=expected){conflict=true;throw new HostRegistrationConflictException();}
            Writes++;Value=next;AfterWrite?.Invoke();return true;
        });
        public async Task CloseOriginalsAsync()
        {
            var errors=new List<Exception>();if(original is{IsCompleted:false})throw new InvalidOperationException("Join original startup task before close.");
            try{await host.CloseOriginalsAsync();}catch(Exception error){errors.Add(error);}
            try{Close?.Invoke();unsettled=false;}catch(Exception error){unsettled=true;errors.Add(error);}
            InstallationErrors.Throw(errors);
        }
    }
    private sealed partial class Rig
    {
        internal StartupRegistry Startup=null!;
        internal string? OldStartupOwner;
        internal InstallationStartupValue OldStartupValue=new(false);
        internal async Task<InstallationInstallerEntry> PrepareStartup(bool upgrade=true,InstallationOperationFaults? faults=null,bool selfContained=true,Func<InstallationOperationOutcome,Task>? report=null)
        {
            OperationPreviousPipe=upgrade?"ky-startup-previous-"+Guid.NewGuid().ToString("N"):null;
            var(source,b)=await Sources(false,initialize:upgrade);
            if(selfContained)
            {
                string runtime=Path.Combine(MaterializedPayloadSource.Repo,"artifacts/2026-10-23-product-payload/final/verification-b-source");
                foreach(var entry in InstallationBootstrapPackage.RuntimeFiles)
                {
                    string sourceFile=Path.Combine(runtime,entry.Path),target=Path.Combine(b.Root,entry.Path);
                    Assert.Equal(entry.Bytes,new FileInfo(sourceFile).Length);Assert.Equal(entry.Sha256,InstallationBootstrapPackage.Hash(sourceFile),ignoreCase:true);
                    Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(sourceFile,target);
                }
                File.WriteAllText(Path.Combine(b.Root,"KuaiYi.Launcher.runtimeconfig.json"),InstallationBootstrapPackage.SelfContainedRuntime);
                File.Copy(Path.Combine(MaterializedPayloadSource.Repo,"tests/fixtures/payload-runtime/KuaiYi.Launcher.deps.json"),Path.Combine(b.Root,"KuaiYi.Launcher.deps.json"),true);
                var files=Directory.GetFiles(b.Root,"*",SearchOption.AllDirectories).Where(p=>Path.GetFileName(p)!="package.json").Order(StringComparer.Ordinal).Select(p=>new{path=Path.GetRelativePath(b.Root,p).Replace('\\','/'),bytes=new FileInfo(p).Length,sha256=InstallationBootstrapPackage.Hash(p)}).ToArray();
                File.WriteAllBytes(Path.Combine(b.Root,"package.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version=b.Version,files}));
            }
            Startup=new(new InstallationDeploymentRegistrySnapshot(Registry));
            if(upgrade)
            {
                await Store.ApplyAsync(source.Root,source.Digest);await new InstallationBootstrapDeployment(App,Data).DeployAsync(b.Root,b.Digest);await new HostRegistrationTransaction(App,Data,Registry).RunAsync(true);
                var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(File.ReadAllBytes(InstallationBootstrapDeployment.OwnerPath(App)));
                var active=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.Receipt>(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(App)));
                string command="\""+Path.Combine(InstallationBootstrapDeployment.BundlePath(App,active.Bundle),"KuaiYi.Launcher.exe")+"\"";
                var receipt=new InstallationStartupOwnership(1,"KuaiYi",owner.Owner,App,Data,active.Generation,active.Bundle,source.Digest,b.Digest,command);
                var bytes=JsonSerializer.SerializeToUtf8Bytes(receipt);File.WriteAllBytes(InstallationStartupRegistration.OwnerPath(App),bytes);OldStartupOwner=Convert.ToBase64String(bytes);
                Startup.Value=OldStartupValue=new(true,"REG_SZ",command);
                source.Version="2.0";File.WriteAllBytes(Path.Combine(source.Root,"public-install-behavior.json"),JsonSerializer.SerializeToUtf8Bytes(new{version="2.0",mode="healthy",runtimePipe=Pipe,controlled=true}));source.Catalog();
            }
            Peer=new(Pipe);if(upgrade)OperationPreviousPeer=new(OperationPreviousPipe!);RestoredPort=new(output);
            var pins=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,kind="KuaiYiInstallerSourcePins",availability="Pinned",generationSha256=source.Digest,bootstrapSha256=b.Digest});
            return Installer=new(pins,App,Data,source.Root,b.Root,Startup,kind=>{FactoryCalls.Add(kind);return Task.FromResult(kind==InstallationOperationSession.New?new InstallationUpgradeCoordinator(App,Data,Port,TimeSpan.FromSeconds(2),Port):new InstallationUpgradeCoordinator(App,Data,RestoredPort,TimeSpan.FromSeconds(2),RestoredPort));},report??(value=>{foreach(var error in value.Errors)output.WriteLine(error.ToString());return Task.CompletedTask;}),Stop.Token,faults);
        }
        internal void StartupRestored()
        {
            Assert.Equal(OldStartupValue,Startup.Value);Assert.Equal(OldStartupOwner,Convert.ToBase64String(File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(App))));
            Assert.Equal("PUBLIC_UNCHANGED",Startup.Neighbours["PUBLIC_OTHER_PRODUCT"]);Assert.Equal("1.0",Store.ReadActive()!.Version);
        }
    }
    [Fact]public Task StartupUpgradeReplacesExactOwnedTargetBeforeReadyAndJoinsOriginalExit()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NewVersionReady,result.Status);
        Assert.Equal(1,r.Startup.Writes);Assert.NotEqual(r.OldStartupValue,r.Startup.Value);Assert.Equal("REG_SZ",r.Startup.Value.Kind);Assert.StartsWith("\""+InstallationBootstrapDeployment.BundlesPath(r.App),r.Startup.Value.Value);
        Assert.EndsWith("KuaiYi.Launcher.exe\"",r.Startup.Value.Value);Assert.DoesNotContain(" --",r.Startup.Value.Value);
        var receipt=InstallationStartupRegistration.ReadOwner(File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App)));Assert.Equal(result.NewReady!.Generation,receipt.Generation);Assert.Equal(r.Startup.Value.Value,receipt.Command);
        await r.Peer!.Close();Assert.Equal(0,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
        Assert.Equal("PUBLIC_UNCHANGED",r.Startup.Neighbours["PUBLIC_OTHER_PRODUCT"]);
    });
    [Fact]public Task StartupFirstInstallSevenStepNoOptInStillReachesReady()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup(false);var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NewVersionReady,result.Status);Assert.Equal(new(false),r.Startup.Value);Assert.Equal(0,r.Startup.Writes);Assert.False(File.Exists(InstallationStartupRegistration.OwnerPath(r.App)));
        await r.Peer!.Close();Assert.Equal(0,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
    });
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)]
    public Task StartupCancellationAfterEachProvisionalPrefixRestoresExactBefore(int prefix)=>ExecuteInstaller(async r=>
    {
        var stages=new[]{DeploymentPublicationStage.Current,DeploymentPublicationStage.Active,DeploymentPublicationStage.Chrome,DeploymentPublicationStage.Edge,DeploymentPublicationStage.HostReceipt,DeploymentPublicationStage.StartupValue,DeploymentPublicationStage.StartupOwner};
        var entry=await r.PrepareStartup(faults:new(){Publication=new(){Stage=s=>{if(s==stages[prefix-1])r.Stop.Cancel();}}});var result=await entry.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.NotCommitted,result.Status);Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,result.Deployment!.Status);r.StartupRestored();Assert.Empty(r.FactoryCalls);Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Theory][InlineData(false)][InlineData(true)]public Task StartupIndependentWriteThenThrowRetainsErrorAndCompensatesKnownEffect(bool ownerFile)=>ExecuteInstaller(async r=>
    {
        var error=new IOException("PUBLIC_STARTUP_AFTER_EFFECT");int once=0;var entry=await r.PrepareStartup(faults:ownerFile?new(){Publication=new(){Publish=(from,to,replace)=>{if(replace)File.Replace(from,to,null,false);else File.Move(from,to);if(to==InstallationStartupRegistration.OwnerPath(r.App)&&Interlocked.Exchange(ref once,1)==0)throw error;}}}:null);
        if(!ownerFile)r.Startup.AfterWrite=()=>{if(Interlocked.Exchange(ref once,1)==0)throw error;};
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.NotCommitted,result.Status);Assert.Contains(error,result.Errors);Assert.Equal(InstallationDeploymentStatus.AbortedAfterPrepared,result.Deployment!.Status);r.StartupRestored();
    });
    [Fact]public Task StartupForeignCompareCannotBeWashedByRestoringBeforeAndClosing()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();r.Startup.BeforeWrite=()=>r.Startup.Value=new(true,"REG_SZ","PUBLIC_FOREIGN");r.Startup.Close=()=>r.Startup.Value=r.OldStartupValue;
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.True(r.Startup.ConflictObserved);Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal("2.0",r.Store.ReadActive()!.Version);Assert.Empty(r.FactoryCalls);Assert.NotEmpty(result.Errors);
    });
    [Fact]public Task StartupWriteAndCloseErrorsBothSurviveAndBlockEarlierDependencyReverse()=>ExecuteInstaller(async r=>
    {
        var write=new IOException("PUBLIC_STARTUP_WRITE");var close=new IOException("PUBLIC_STARTUP_CLOSE");var entry=await r.PrepareStartup();r.Startup.AfterWrite=()=>throw write;r.Startup.Close=()=>{if(r.Startup.Writes>0)throw close;};
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Contains(write,result.Errors);Assert.Contains(close,result.Errors);Assert.Equal("2.0",r.Store.ReadActive()!.Version);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(r.FactoryCalls);
        r.Startup.AfterWrite=null;r.Startup.Close=null;await r.Startup.CloseOriginalsAsync();
    });
    [Fact]public Task StartupFailedReadyUsesNewTicketSevenStepCompensationAndOldReady()=>ExecuteInstaller(async r=>
    {
        r.Port.HealthError=new IOException("PUBLIC_STARTUP_HEALTH");var entry=await r.PrepareStartup();var result=await entry.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.PreviousVersionReady,result.Status);Assert.Equal(InstallationCompensationStatus.Compensated,result.Compensation!.Status);Assert.NotEqual(entry.Operation!.Scope!.Operation,entry.Operation.Compensation!.Operation);r.StartupRestored();Assert.Equal("1.0",result.PreviousReady!.Generation.Version);
        await r.OperationPreviousPeer!.Close();Assert.Equal(4,(await entry.OriginalWork.WaitAsync(Limit)).ExitCode);
    });
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)]public Task StartupPostReadyCompensationPartialEffectNeverArchivesSuccessOrTouchesEarlierDependencies(int step)=>ExecuteInstaller(async r=>
    {
        r.Port.HealthError=new IOException("PUBLIC_STARTUP_NEW_HEALTH");var failure=new IOException("PUBLIC_STARTUP_RESTORE_STOP");var entry=await r.PrepareStartup(faults:new(){Compensation=new(){Stage=s=>{if(s==new[]{InstallationCompensationStage.StartupOwner,InstallationCompensationStage.StartupValue,InstallationCompensationStage.HostReceipt,InstallationCompensationStage.Edge,InstallationCompensationStage.Chrome,InstallationCompensationStage.Active,InstallationCompensationStage.Current}[step-1])throw failure;}}});
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Contains(failure,result.Errors);Assert.Equal(step<7?"2.0":"1.0",r.Store.ReadActive()!.Version);Assert.Equal(r.OldStartupOwner,Convert.ToBase64String(File.ReadAllBytes(InstallationStartupRegistration.OwnerPath(r.App))));if(step>=2)Assert.Equal(r.OldStartupValue,r.Startup.Value);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"compensation-finished-*.json"));Assert.Single(r.FactoryCalls);
    });
    [Fact]public Task StartupDevelopmentBootstrapNeverPublishes()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup(selfContained:false);var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Equal(0,r.Startup.Writes);Assert.Empty(r.FactoryCalls);r.StartupRestored();
    });
    [Fact]public Task StartupPreparedRemovalPlanRequiresLiveOriginalScopeAndNamesOnlyOwnedValue()=>ExecuteInstaller(async r=>
    {
        InstallationStartupRemovalPlan? removal=null;
        var entry=await r.PrepareStartup(faults:new(){Phase=p=>{if(p==InstallationOperationPhase.Publishing){removal=r.Installer!.Operation!.Scope!.PlanPreparedStartupRemoval();Assert.NotNull(removal);Assert.NotEqual(r.OldStartupValue,removal.Expected);Assert.Equal(removal.Expected.Value,InstallationStartupRegistration.ReadOwner(Convert.FromBase64String(removal.Owner)).Command);r.Stop.Cancel();}}});
        var result=await entry.Start().WaitAsync(Limit);Assert.NotNull(removal);Assert.Equal(InstallationOperationStatus.NotCommitted,result.Status);r.StartupRestored();
        Assert.Throws<InstallationRecoveryException>(()=>entry.Operation!.Scope!.PlanPreparedStartupRemoval());
    });
    [Fact]public Task StartupRetiredEntryCannotCreateRestoredSessionFromHistoricalCompensation()=>ExecuteInstaller(async r=>
    {
        r.Port.HealthError=new IOException("PUBLIC_STARTUP_NEW_FAILURE");var entry=await r.PrepareStartup(faults:new(){Phase=p=>{if(p==InstallationOperationPhase.Restored)r.Stop.Cancel();}});
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(InstallationOperationStatus.RestoredPreviousNotReady,result.Status);Assert.Equal(InstallationCompensationStatus.Compensated,result.Compensation!.Status);
        await entry.OriginalWork.WaitAsync(Limit);Assert.Null(entry.Operation!.PreviousSession);
        var fresh=new InstallationUpgradeCoordinator(r.App,r.Data,r.RestoredPort!,TimeSpan.FromSeconds(2),r.RestoredPort!);InstallationRestoredSession? attempted=null;
        try{Assert.Throws<InstallationRecoveryException>(()=>attempted=entry.Operation.Compensation!.BeginRestoredSession(fresh,(_,_)=>Task.CompletedTask,new CancellationToken(true)));}
        finally
        {
            // A broken admission assertion must still join the exact returned task. Pre-cancelled: no child start.
            if(attempted!=null)await attempted.Completion.WaitAsync(Limit);
            fresh.DisposeUnusedOperationController(r.App,r.Data);
        }
        Assert.Equal(0,r.RestoredPort!.Starts);
    });
    [Fact]public Task StartupHeldOriginalWriteCannotReleaseScopeOrPretendCancellationSettled()=>ExecuteInstaller(async r=>
    {
        var entered=Signal();var release=Signal();int once=0;var entry=await r.PrepareStartup();r.Startup.BeforeWrite=()=>{if(Interlocked.Exchange(ref once,1)==0){entered.TrySetResult();release.Task.GetAwaiter().GetResult();}};var work=entry.Start();
        await BodyAndCleanup(async()=>{await entered.Task.WaitAsync(Limit);r.Stop.Cancel();Assert.False(work.IsCompleted);Assert.False(entry.OriginalWork.IsCompleted);Assert.True(File.Exists(r.Store.PendingPath));},()=>{release.TrySetResult();return Task.CompletedTask;});
        Assert.Equal(InstallationOperationStatus.NotCommitted,(await work.WaitAsync(Limit)).Status);r.StartupRestored();Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });
    [Fact]public Task StartupTargetChangedBeforeNewSessionCannotStartAppOrClaimReady()=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup(faults:new(){Phase=p=>{if(p==InstallationOperationPhase.NewSession)r.Startup.Value=new(true,"REG_SZ","PUBLIC_FOREIGN_READY");}});var result=await entry.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.Unconfirmed,result.Status);Assert.Null(result.NewReady);Assert.Equal(0,r.Port.Starts);Assert.Null(entry.Operation!.Compensation);Assert.Equal("PUBLIC_FOREIGN_READY",r.Startup.Value.Value);
    });

    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)]public Task StartupForeignOrUnsupportedOriginalOwnershipCannotReachPublication(int kind)=>ExecuteInstaller(async r=>
    {
        var entry=await r.PrepareStartup();string path=InstallationStartupRegistration.OwnerPath(r.App);var owner=InstallationStartupRegistration.ReadOwner(File.ReadAllBytes(path));
        switch(kind)
        {
            case 0:r.Startup.Value=new(true,"REG_SZ","PUBLIC_FOREIGN");break;
            case 1:File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(owner with{Command="PUBLIC_FOREIGN"}));break;
            case 2:File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(owner with{Root=r.Root}));break;
            case 3:File.WriteAllBytes(path,JsonSerializer.SerializeToUtf8Bytes(owner with{GenerationSourceSha256=new string('A',64)}));break;
            case 4:r.Startup.Value=new(true,"REG_EXPAND_SZ",owner.Command);break;
            case 5:File.Delete(path);break; // Test-owned synthetic input only, before operation begins.
        }
        var result=await entry.Start().WaitAsync(Limit);Assert.Equal(kind==4?InstallationOperationStatus.NotCommitted:InstallationOperationStatus.Unconfirmed,result.Status);Assert.Equal(0,r.Startup.Writes);Assert.Equal("1.0",r.Store.ReadActive()!.Version);Assert.Empty(r.FactoryCalls);Assert.Empty(Directory.GetFiles(r.App,"deployment-committed-*.json"));Assert.Equal("PUBLIC_UNCHANGED",r.Startup.Neighbours["PUBLIC_OTHER_PRODUCT"]);
    });
    [Fact]public Task StartupFirstInstallHealthFailureRestoresAbsenceWithoutFabricatedOldSession()=>ExecuteInstaller(async r=>
    {
        r.Port.HealthError=new IOException("PUBLIC_STARTUP_INITIAL_HEALTH");var entry=await r.PrepareStartup(false);var result=await entry.Start().WaitAsync(Limit);
        Assert.Equal(InstallationOperationStatus.RestoredNoPrevious,result.Status);Assert.Equal(InstallationCompensationStatus.Compensated,result.Compensation!.Status);Assert.Null(result.PreviousReady);Assert.Null(entry.Operation!.PreviousSession);Assert.Equal(0,r.Startup.Writes);Assert.Equal(new(false),r.Startup.Value);Assert.False(File.Exists(InstallationStartupRegistration.OwnerPath(r.App)));Assert.Null(r.Store.ReadActive());Assert.True((await entry.OriginalWork.WaitAsync(Limit)).CleanupConfirmed);
    });

}
