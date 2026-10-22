using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using System.Text.Json;
using System.Security.Cryptography;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class InstallationDeploymentBeforeTests
{
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-before-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");internal string Data=>Path.Combine(Root,"data");
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal Rig(){using var initial=Store.EnterRoleAdmission();}
        internal InstallationDeploymentScope Scope(InstallationDeploymentFaults? faults=null,CancellationToken cancellation=default)=>new(App,Data,new string('A',64),new string('B',64),cancellation,faults:faults);
        public void Dispose()=>Directory.Delete(Root,true);
    }
    private sealed class Slots:IInstallationDeploymentSnapshotRead
    {
        public bool CleanupConfirmed=>true;
        public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync()=>Task.FromResult(new InstallationDeploymentRegistrationSnapshot(new(false,null),new(true,null)));
        public Task VerifyAsync()=>Task.CompletedTask;
        public Task CloseOriginalsAsync()=>Task.CompletedTask;
    }
    [Fact]public async Task PartialBeforeMustNeverArchiveAsBeforeSnapshotOrPreparedAbort()
    {
        using var r=new Rig();var failure=new IOException("PUBLIC_PARTIAL_BEFORE");var scope=r.Scope(new(){WriteBefore=(original,bytes)=>{original.Write(bytes.AsSpan(0,9));throw failure;}});await scope.AcquireAsync();
        Assert.Same(failure,await Assert.ThrowsAsync<IOException>(()=>scope.CaptureBeforeAsync(new Slots())));
        Assert.True(scope.BeforeUnconfirmed);
        var outcome=await scope.Finalization;Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);Assert.True(scope.CleanupConfirmed);
        Assert.True(File.Exists(r.Store.PendingPath));Assert.Equal(9,new FileInfo(Assert.Single(Directory.GetFiles(r.App,"deployment-before-*.json"))).Length);Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
    }
    private sealed class Registry(string file):IHostRegistrationStore
    {
        internal HostRegistrationSlot Chrome=new(false,null),Edge=new(true,null);
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Exception? ReadError,CloseError;internal int Reads,Closes;private FileStream? original;
        public bool CleanupConfirmed=>original==null;
        public HostRegistrationSlot Read(IntegrationBrowser browser)
        {
            original=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None);Reads++;Entered.TrySetResult();Release.Task.GetAwaiter().GetResult();
            if(ReadError!=null)throw ReadError;
            original.Dispose();original=null;return browser==IntegrationBrowser.Chrome?Chrome:Edge;
        }
        public void FinalizeCleanup(){Closes++;if(CloseError!=null)throw CloseError;original?.Dispose();original=null;}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)=>throw new InvalidOperationException("Snapshot must never write registry slots.");
    }
    private static Registry RegistryFor(Rig r,bool hold=false)
    {
        string path=Path.Combine(r.Root,"PUBLIC_REGISTRY_HANDLE");File.WriteAllBytes(path,[42]);var registry=new Registry(path);if(!hold)registry.Release.SetResult();return registry;
    }
    private static IReadOnlyList<Exception> Errors(Exception error)=>error is AggregateException a?a.Flatten().InnerExceptions:[error];
    [Fact]public async Task CompleteBeforeBindsExactFilesAndBothPhysicalSlotsWithoutPublishingCandidates()
    {
        using var r=new Rig();var registry=RegistryFor(r);registry.Chrome=new(true,"PUBLIC_FOREIGN_DEFAULT",true);
        var files=new[]{"current.json","bootstrap-store.json","stable-bootstrap.json","host-registration.json"}.ToDictionary(x=>x,x=>System.Text.Encoding.UTF8.GetBytes("PUBLIC_OBSERVED_"+x));
        foreach(var (name,bytes) in files)File.WriteAllBytes(Path.Combine(r.App,name),bytes);
        var scope=r.Scope();await scope.AcquireAsync();var value=await scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry));
        Assert.Equal(Convert.ToBase64String(files["current.json"]),value.Current);Assert.Equal(Convert.ToBase64String(files["bootstrap-store.json"]),value.BootstrapOwner);
        Assert.Equal(Convert.ToBase64String(files["stable-bootstrap.json"]),value.BootstrapActive);Assert.Equal(Convert.ToBase64String(files["host-registration.json"]),value.HostReceipt);
        Assert.Equal(new(true,"PUBLIC_FOREIGN_DEFAULT",true),value.Registry.Chrome);Assert.Equal(new(true,null),value.Registry.Edge);Assert.Equal(2,registry.Reads);Assert.True(registry.CleanupConfirmed);
        byte[] before=File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-before-*.json")));Assert.InRange(before.Length,1,131072);Assert.Equal(scope.Operation,value.Operation);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(r.Store.PendingPath))),value.TicketSha256);Assert.False(scope.BeforeUnconfirmed);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.AbortBeforeSnapshotAsync());var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,result.Status);Assert.Empty(result.Errors);Assert.True(scope.CleanupConfirmed);
        using var terminal=JsonDocument.Parse(File.ReadAllBytes(Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"))));Assert.Equal("AbortBeforePrepared",terminal.RootElement.GetProperty("variant").GetString());Assert.Equal(Convert.ToHexString(SHA256.HashData(before)),terminal.RootElement.GetProperty("beforeSha256").GetString());Assert.Equal(JsonValueKind.Null,terminal.RootElement.GetProperty("preparedSha256").ValueKind);
        foreach(var (name,bytes) in files)Assert.Equal(bytes,File.ReadAllBytes(Path.Combine(r.App,name)));
        Assert.False(Directory.Exists(Path.Combine(r.App,"versions")));Assert.False(Directory.Exists(InstallationBootstrapDeployment.BundlesPath(r.App)));Assert.Equal(4,registry.Reads);Assert.Equal(2,registry.Closes);
    }
    [Theory][InlineData("current.json",4096)][InlineData("bootstrap-store.json",4096)][InlineData("stable-bootstrap.json",8192)][InlineData("host-registration.json",8192)]
    public async Task EachConsumerBoundDistinguishesEmptyDirectoryAndOversizeFromAbsence(string name,int limit)
    {
        foreach(string fault in new[]{"empty","directory","oversize"})
        {
            using var r=new Rig();string path=Path.Combine(r.App,name);if(fault=="directory")Directory.CreateDirectory(path);else File.WriteAllBytes(path,new byte[fault=="empty"?0:limit+1]);
            var registry=RegistryFor(r);var scope=r.Scope();await scope.AcquireAsync();Assert.NotNull(await Record.ExceptionAsync(()=>scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry))));
            var outcome=await scope.Finalization;Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,outcome.Status);Assert.NotEmpty(outcome.Errors);Assert.True(scope.CleanupConfirmed);Assert.Equal(0,registry.Reads);Assert.Empty(Directory.GetFiles(r.App,"deployment-before-*.json"));
            if(fault=="directory")Assert.True(Directory.Exists(path));else Assert.Equal(fault=="empty"?0:limit+1,new FileInfo(path).Length);
        }
    }
    [Fact]public async Task OriginalFileReadAndCloseFaultsKeepBothErrorsAndOnlyTheSameHandleCanSettle()
    {
        using var r=new Rig();string path=Path.Combine(r.App,"current.json");File.WriteAllText(path,"PUBLIC_OBSERVED");var read=new IOException("PUBLIC_READ");var close=new IOException("PUBLIC_CLOSE");int opens=0;FileStream? original=null;
        var faults=new InstallationDeploymentFaults{BeforeRead=(at,stream)=>{if(at==path){opens++;original=stream;throw read;}},BeforeClose=at=>{if(at==path)throw close;}};
        var scope=r.Scope(faults);await scope.AcquireAsync();
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.CaptureBeforeAsync(new Slots()));Assert.NotNull(error);Assert.Contains(read,Errors(error));Assert.Contains(close,Errors(error));Assert.False(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.False(original!.SafeFileHandle.IsClosed);
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.Equal(1,opens);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-before-*.json"));
    }
    [Fact]public async Task OriginalRegistryReadAndCloseFaultsPreventBeforeAndDoNotReadTheNextSlot()
    {
        using var r=new Rig();var registry=RegistryFor(r);var read=new IOException("PUBLIC_REG_READ");var close=new IOException("PUBLIC_REG_CLOSE");registry.ReadError=read;registry.CloseError=close;
        var scope=r.Scope();await scope.AcquireAsync();
        try
        {
            var error=await Record.ExceptionAsync(()=>scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry)));Assert.NotNull(error);Assert.Contains(read,Errors(error));Assert.Contains(close,Errors(error));Assert.False(scope.CleanupConfirmed);Assert.Equal(1,registry.Reads);Assert.Empty(Directory.GetFiles(r.App,"deployment-before-*.json"));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);
        }
        finally{registry.CloseError=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.Equal(1,registry.Reads);Assert.True(File.Exists(r.Store.PendingPath));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task AbortAndCancellationCannotAbandonThePublishedSnapshotRead(bool cancel)
    {
        using var r=new Rig();using var stop=new CancellationTokenSource();var registry=RegistryFor(r,true);var scope=r.Scope(cancellation:stop.Token);await scope.AcquireAsync();
        var capture=scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry));await registry.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var final=scope.AbortBeforePreparedAsync();
        try
        {
            Assert.Same(scope.Finalization,final);if(cancel)stop.Cancel();Assert.False(capture.IsCompleted);Assert.False(final.IsCompleted);Assert.False(scope.CleanupConfirmed);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.CaptureBeforeAsync(new Slots()));await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.AbortBeforeSnapshotAsync());Assert.Empty(Directory.GetFiles(r.App,"deployment-before-*.json"));
        }
        finally{registry.Release.SetResult();}
        if(cancel){var error=await Record.ExceptionAsync(async()=>await capture);Assert.NotNull(error);Assert.Contains(Errors(error),x=>x is OperationCanceledException);}
        else{var before=await capture;Assert.Equal(scope.Operation,before.Operation);}
        var result=await final;Assert.Equal(cancel?InstallationDeploymentStatus.AbortedBeforeSnapshot:InstallationDeploymentStatus.AbortedBeforePrepared,result.Status);Assert.True(scope.CleanupConfirmed);Assert.True(registry.CleanupConfirmed);Assert.Equal(cancel?2:4,registry.Reads);
    }
    [Fact]public async Task FullBeforeBytesWithFailedOriginalCloseStayUnconfirmedAfterSameOwnerCleanup()
    {
        using var r=new Rig();FileStream? original=null;var close=new IOException("PUBLIC_BEFORE_CLOSE");var faults=new InstallationDeploymentFaults{WriteBefore=(stream,bytes)=>{original=stream;stream.Write(bytes);},BeforeClose=path=>{if(Path.GetFileName(path).StartsWith("deployment-before-",StringComparison.Ordinal))throw close;}};
        var scope=r.Scope(faults);await scope.AcquireAsync();
        try{var error=await Record.ExceptionAsync(()=>scope.CaptureBeforeAsync(new Slots()));Assert.NotNull(error);Assert.Contains(close,Errors(error));Assert.True(scope.BeforeUnconfirmed);Assert.False(scope.CleanupConfirmed);Assert.False(original!.SafeFileHandle.IsClosed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);}
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.BeforeUnconfirmed);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualEncodedBeforeBoundIncludesRegistryEscapingBeforeCreateNew(bool escaped)
    {
        using var r=new Rig();var registry=RegistryFor(r);string value=escaped?new string('中',16000):new string('A',30000);registry.Chrome=new(true,value);registry.Edge=new(true,value);var scope=r.Scope();await scope.AcquireAsync();
        if(escaped)
        {
            await Assert.ThrowsAsync<InvalidDataException>(()=>scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry)));Assert.Empty(Directory.GetFiles(r.App,"deployment-before-*.json"));Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,(await scope.Finalization).Status);
        }
        else{await scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry));Assert.InRange(new FileInfo(Assert.Single(Directory.GetFiles(r.App,"deployment-before-*.json"))).Length,60000,131072);Assert.Equal(InstallationDeploymentStatus.AbortedBeforePrepared,(await scope.AbortBeforePreparedAsync()).Status);}
        Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData("before")][InlineData("source-absence")][InlineData("registry-key-existence")]
    public async Task ClosingRejectsChangedBeforeFilesAndPhysicalSlotShape(string changed)
    {
        using var r=new Rig();var registry=RegistryFor(r);var scope=r.Scope();await scope.AcquireAsync();await scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry));
        string before=Assert.Single(Directory.GetFiles(r.App,"deployment-before-*.json"));
        if(changed=="before")File.AppendAllText(before,"PUBLIC_FOREIGN");else if(changed=="source-absence")File.WriteAllBytes(Path.Combine(r.App,"current.json"),[]);else registry.Edge=new(false,null);
        var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.NotEmpty(result.Errors);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
    }
    [Fact]public async Task ClosingSealsSnapshotProducerBeforeAnyNewOwnerStarts()
    {
        using var r=new Rig();var scope=r.Scope();await scope.AcquireAsync();var final=scope.AbortBeforeSnapshotAsync();var registry=RegistryFor(r);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry)));Assert.Equal(0,registry.Reads);Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,(await final).Status);Assert.True(scope.CleanupConfirmed);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PreparedAbortArchiveReceiptLossDoesNotReplaySnapshotOrFinalization(bool moved)
    {
        using var r=new Rig();var registry=RegistryFor(r);var failure=new IOException("PUBLIC_BEFORE_ARCHIVE");int archives=0;
        var scope=r.Scope(new(){Archive=(from,to)=>{archives++;Assert.True(registry.CleanupConfirmed);if(moved)File.Move(from,to);throw failure;}});await scope.AcquireAsync();await scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry));
        var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(failure,result.Errors);Assert.Equal(4,registry.Reads);int closed=registry.Closes;Assert.Same(scope.Finalization,scope.AbortBeforePreparedAsync());Assert.Empty(await scope.SettleOriginalsAsync());Assert.Equal(4,registry.Reads);Assert.Equal(closed,registry.Closes);Assert.Equal(1,archives);Assert.Equal(!moved,File.Exists(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);
    }

    [Fact]public async Task ParentQueryFailureCannotBecomeConfirmedAbsenceOrStartRegistryReads()
    {
        using var r=new Rig();string parked=Path.Combine(r.Root,"PUBLIC_OWNED_PARKED_APP");bool replaced=false;var registry=RegistryFor(r);
        var scope=r.Scope(new(){BeforeSnapshotQuery=path=>
        {
            if(!replaced&&path==Path.Combine(r.App,"current.json"))
            {Directory.Move(r.App,parked);File.WriteAllText(r.App,"PUBLIC_OWNED_BLOCKING_FILE");replaced=true;}
        }});await scope.AcquireAsync();
        try
        {
            Assert.NotNull(await Record.ExceptionAsync(()=>scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry))));
            Assert.Equal(0,registry.Reads);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.True(scope.CleanupConfirmed);
        }
        finally
        {
            await scope.Finalization;
            if(replaced){Assert.Equal("PUBLIC_OWNED_BLOCKING_FILE",File.ReadAllText(r.App));File.Delete(r.App);Directory.Move(parked,r.App);}
        }
        Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-before-*.json"));
    }

    [Fact]public async Task PostClosingRegistryReadAndCloseFaultsKeepBeforePendingAndOriginalOwner()
    {
        using var r=new Rig();var registry=RegistryFor(r);var scope=r.Scope();await scope.AcquireAsync();await scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(registry));
        var read=new IOException("PUBLIC_CLOSING_READ");var close=new IOException("PUBLIC_CLOSING_CLOSE");registry.ReadError=read;registry.CloseError=close;
        try
        {
            var result=await scope.AbortBeforePreparedAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,result.Status);Assert.Contains(read,result.Errors);Assert.Contains(close,result.Errors);Assert.Equal(3,registry.Reads);Assert.False(scope.CleanupConfirmed);Assert.Single(Directory.GetFiles(r.App,"deployment-before-*.json"));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
        }
        finally{registry.CloseError=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.Equal(3,registry.Reads);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
    }
    [Fact]public async Task OriginalFlushPhaseFailureCannotMakeFullBeforeBytesACompletePlan()
    {
        using var r=new Rig();var scope=r.Scope(new(){WriteBefore=(original,bytes)=>{original.Write(bytes);original.Dispose();}});await scope.AcquireAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(()=>scope.CaptureBeforeAsync(new Slots()));Assert.True(scope.BeforeUnconfirmed);Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.True(scope.CleanupConfirmed);
        Assert.True(new FileInfo(Assert.Single(Directory.GetFiles(r.App,"deployment-before-*.json"))).Length>9);Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
    }

    [Fact]public async Task ForeignBeforeAppearingAfterAdmissionCannotBeClearedByAnEarlyAbort()
    {
        using var r=new Rig();var scope=r.Scope();await scope.AcquireAsync();string before=Path.Combine(r.App,"deployment-before-"+scope.Operation+".json");byte[] foreign="PUBLIC_FOREIGN_BEFORE"u8.ToArray();File.WriteAllBytes(before,foreign);
        Assert.NotNull(await Record.ExceptionAsync(()=>scope.CaptureBeforeAsync(new Slots())));Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);Assert.Equal(foreign,File.ReadAllBytes(before));Assert.True(File.Exists(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));Assert.True(scope.CleanupConfirmed);
    }

}
