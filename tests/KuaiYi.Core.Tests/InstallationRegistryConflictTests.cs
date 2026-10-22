using KuaiYi.Core.Installation;
using KuaiYi.Core.Integration;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class InstallationRegistryConflictTests
{
    private static readonly List<object> Retained=[];
    // Synthetic raw store models an actual compare failure, not a failed post-write observation.
    private sealed class Registry(bool edge,bool closeFault):IHostRegistrationStore,IDisposable
    {
        internal readonly HostRegistrationConflictException Conflict=new();
        internal readonly IOException CloseError=new("PUBLIC_COMPARE_CLOSE");
        internal readonly TaskCompletionSource Compared=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release=new();
        internal HostRegistrationSlot Chrome=new(false,null),Edge=new(false,null),Observed=new(false,null),Expected=new(false,null);
        internal int Reads,Writes,ReadsAtCompare;internal bool FailClose=closeFault;
        private bool rawOpen,injected;
        public bool CleanupConfirmed=>!rawOpen;
        public HostRegistrationSlot Read(IntegrationBrowser browser)
        {Assert.False(rawOpen);Reads++;return browser==IntegrationBrowser.Chrome?Chrome:Edge;}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)
        {
            Assert.False(rawOpen);Writes++;
            if(!injected&&(browser==IntegrationBrowser.Edge)==edge)
            {
                injected=true;rawOpen=true;Expected=expected;
                if(edge)Edge=new(true,"PUBLIC_FOREIGN_COMPARE",true);else Chrome=new(true,"PUBLIC_FOREIGN_COMPARE",true);
                Observed=edge?Edge:Chrome;Assert.NotEqual(expected,Observed);ReadsAtCompare=Reads;Compared.SetResult();
                if(!Release.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("PUBLIC_COMPARE_BARRIER");
                // The exact compare exception survives external restoration before the original close.
                if(FailClose)throw new AggregateException(Conflict,CloseError);
                rawOpen=false;throw Conflict;
            }
            Assert.Equal(expected,browser==IntegrationBrowser.Chrome?Chrome:Edge);
            if(browser==IntegrationBrowser.Chrome)Chrome=new(true,value);else Edge=new(true,value);
        }
        internal void RestoreBefore(){if(edge)Edge=Expected;else Chrome=Expected;}
        public void FinalizeCleanup(){if(rawOpen&&FailClose)throw CloseError;rawOpen=false;}
        public void Dispose(){Assert.False(rawOpen);Release.Dispose();}
    }
    private static IReadOnlyList<Exception> Errors(Exception error)=>error is AggregateException a?a.Flatten().InnerExceptions:[error];
    [Theory][InlineData(false,false)][InlineData(true,false)][InlineData(false,true)][InlineData(true,true)]
    public async Task WriteCompareForeignRestoredBeforeOriginalCloseMustNeverResumeEarlierDependencies(bool edge,bool closeFault)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-write-conflict-"+Guid.NewGuid().ToString("N")),app=Path.Combine(root,"app"),data=Path.Combine(root,"data");Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"PUBLIC_OFF"),"PUBLIC_PRESERVED");
        var raw=new Registry(edge,closeFault);var owner=new InstallationDeploymentRegistrySnapshot(raw);var store=new InstallationStore(app,data,()=>true);using(var initial=store.EnterRoleAdmission()){}
        var gs=new MaterializedPayloadSource(Path.Combine(root,"generation-source"));var bs=new BootstrapSource(Path.Combine(root,"bootstrap-source"));var scope=new InstallationDeploymentScope(app,data,gs.Digest,bs.Digest);
        Task<InstallationDeploymentOutcome>? originalRun=null;var originalFinalization=scope.Finalization;var settlements=new List<Task<IReadOnlyList<Exception>>>();var errors=new List<Exception>();bool snapshotStarted=false;
        try
        {
            await scope.AcquireAsync();snapshotStarted=true;await scope.CaptureBeforeAsync(owner);var g=await scope.PrepareGenerationAsync(gs.Root);var b=await scope.PrepareBootstrapAsync(bs.Root,g);var p=await scope.SealPreparedAsync(g,b);byte[] ticket=File.ReadAllBytes(store.PendingPath);
            originalRun=scope.CommitPreparedAsync(p);
            try
            {
                await raw.Compared.Task.WaitAsync(TimeSpan.FromSeconds(10));Assert.True(raw.Observed.ForeignContent);Assert.NotEqual(raw.Expected,raw.Observed);Assert.False(raw.CleanupConfirmed);Assert.False(originalRun.IsCompleted);raw.RestoreBefore();
            }
            finally{raw.Release.Set();}
            var result=await originalRun.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Contains(raw.Conflict,result.Errors.SelectMany(Errors));if(closeFault)Assert.Contains(raw.CloseError,result.Errors.SelectMany(Errors));
                Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Equal(ticket,File.ReadAllBytes(store.PendingPath));Assert.Equal(p.Plan.TargetCurrent,Convert.ToBase64String(File.ReadAllBytes(Path.Combine(app,"current.json"))));Assert.Equal(p.Plan.Bootstrap.Receipt,Convert.ToBase64String(File.ReadAllBytes(InstallationBootstrapDeployment.ActivePath(app))));
                Assert.Equal(edge?2:1,raw.Writes);Assert.Equal(raw.ReadsAtCompare,raw.Reads);Assert.Equal(!closeFault,scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(app,"deployment-failed-*.json"));Assert.Empty(Directory.GetFiles(app,"deployment-committed-*.json"));
            raw.FailClose=false;var cleanup=scope.SettleOriginalsAsync();settlements.Add(cleanup);Assert.Empty(await cleanup.WaitAsync(TimeSpan.FromSeconds(10)));
            int reads=raw.Reads;await Assert.ThrowsAsync<InvalidOperationException>(()=>owner.ReadPublishedAsync());Assert.Equal(reads,raw.Reads);await owner.CloseOriginalsAsync();Assert.True(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await originalRun).Status);
        }
        catch(Exception body){errors.Add(body);}
        finally
        {
            raw.Release.Set();raw.FailClose=false;
            if(originalRun!=null)try{await originalRun.WaitAsync(TimeSpan.FromSeconds(10));}catch(Exception cleanup){errors.Add(cleanup);}
            else if(!originalFinalization.IsCompleted)
                try{var abort=snapshotStarted?scope.AbortBeforePreparedAsync():scope.AbortBeforeSnapshotAsync();await abort.WaitAsync(TimeSpan.FromSeconds(10));}catch(Exception abort){errors.Add(abort);}
            try{await originalFinalization.WaitAsync(TimeSpan.FromSeconds(10));}catch(Exception cleanup){errors.Add(cleanup);}
            if(originalFinalization.IsCompleted)
                try{var cleanup=scope.SettleOriginalsAsync();settlements.Add(cleanup);errors.AddRange(await cleanup.WaitAsync(TimeSpan.FromSeconds(10)));}catch(Exception cleanup){errors.Add(cleanup);}
            bool settled=(originalRun==null||originalRun.IsCompletedSuccessfully)&&originalFinalization.IsCompletedSuccessfully&&settlements.All(t=>t.IsCompletedSuccessfully)&&scope.CleanupConfirmed&&raw.CleanupConfirmed;
            bool disposed=false;
            if(settled)try{raw.Dispose();disposed=true;}catch(Exception dispose){errors.Add(dispose);}
            if(disposed)
            {
                try{Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(data,"PUBLIC_OFF")));Directory.Delete(root,true);}catch(Exception cleanup){errors.Add(cleanup);}
            }
            else
            {
                lock(Retained)Retained.Add((root,raw,scope,originalRun,originalFinalization,settlements));
                errors.Add(new IOException("PUBLIC_COMPARE_ORIGINALS_UNCONFIRMED_RETAINED"));
            }
        }
        InstallationErrors.Throw(errors);
    }
}
