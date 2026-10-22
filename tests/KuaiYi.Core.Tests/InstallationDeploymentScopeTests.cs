using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class InstallationDeploymentScopeTests
{
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-scope-"+Guid.NewGuid().ToString("N"));
        internal string App=>Path.Combine(Root,"app");
        internal string Data=>Path.Combine(Root,"data");
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal Rig(){using var initial=Store.EnterRoleAdmission();}
        public void Dispose()=>Directory.Delete(Root,true);
        internal InstallationDeploymentScope Scope(CancellationToken cancellation=default,InstallationAdmissionFaults? admission=null,InstallationDeploymentFaults? faults=null)
            =>new(App,Data,new string('A',64),new string('B',64),cancellation,admission,faults);
    }

    [Fact]public void AcquisitionAndOriginalCloseFailuresKeepBothOriginalExceptions()
    {
        using var r=new Rig();var primary=new IOException("PUBLIC_ACQUIRE");var close=new IOException("PUBLIC_CLOSE");InstallationAdmission? owner=null;
        var faults=new InstallationAdmissionFaults{OwnerCreated=value=>owner=value,Acquired=()=>throw primary,BeforeClose=()=>throw close};
        try
        {
            var error=Record.Exception(()=>new InstallationAdmission(r.App,true,true,faults));
            var errors=Assert.IsType<AggregateException>(error).Flatten().InnerExceptions;
            Assert.Contains(primary,errors);Assert.Contains(close,errors);
        }
        finally{faults.BeforeClose=null;owner?.Dispose();}
    }

    [Theory][InlineData("setup")][InlineData("host")][InlineData("upgrade")]
    public void EveryAcquisitionSubownerIsRetainedBeforeValidationAndCloseFailure(string stage)
    {
        using var r=new Rig();var primary=new IOException("PUBLIC_ACQUIRE_"+stage);var close=new IOException("PUBLIC_CLOSE_"+stage);
        var faults=new InstallationAdmissionFaults{AcquiredLease=at=>{if(at==stage)throw primary;},ClosingLease=at=>{if(at==stage)throw close;}};
        var owner=InstallationAdmission.CreateOwner(r.App,faults);
        try
        {
            var error=Record.Exception(()=>{try{r.Store.AcquireAdmission(owner);}catch(Exception original){owner.Fail(original);}});
            var errors=Assert.IsType<AggregateException>(error).Flatten().InnerExceptions;Assert.Contains(primary,errors);Assert.Contains(close,errors);
            Assert.False(owner.CleanupConfirmed);Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());
        }
        finally{faults.ClosingLease=null;owner.Dispose();}
        Assert.True(owner.CleanupConfirmed);using var next=r.Store.EnterRoleAdmission();
    }

    [Fact]public async Task ScopeRetainsTheOriginalAdmissionWhenAcquisitionAndCleanupBothFail()
    {
        using var r=new Rig();var primary=new IOException("PUBLIC_SCOPE_ACQUIRE");var close=new IOException("PUBLIC_SCOPE_CLOSE");
        var faults=new InstallationAdmissionFaults{Acquired=()=>throw primary,BeforeClose=()=>throw close};var scope=r.Scope(admission:faults);
        try
        {
            var error=await Record.ExceptionAsync(scope.AcquireAsync);var errors=Assert.IsType<AggregateException>(error).Flatten().InnerExceptions;
            Assert.Contains(primary,errors);Assert.Contains(close,errors);Assert.False(scope.CleanupConfirmed);Assert.False(File.Exists(r.Store.PendingPath));
            Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);using var next=r.Store.EnterRoleAdmission();
    }

    [Fact]public async Task ExactTicketBlocksOldAdmissionAndOnlySettledAbortRemovesPending()
    {
        using var r=new Rig();var scope=r.Scope();await scope.AcquireAsync();
        Assert.False(scope.Finalization.IsCompleted);Assert.False(scope.CleanupConfirmed);Assert.Throws<InstallationBusyException>(()=>r.Store.EnterRoleAdmission());
        using(var document=System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(r.Store.PendingPath)))
        {Assert.Equal("KuaiYiUnifiedDeploymentTicket",document.RootElement.GetProperty("kind").GetString());Assert.Equal(scope.Generation,document.RootElement.GetProperty("generation").GetString());}
        var outcome=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,outcome.Status);Assert.Empty(outcome.Errors);Assert.True(scope.CleanupConfirmed);
        Assert.False(File.Exists(r.Store.PendingPath));string archived=Assert.Single(Directory.GetFiles(r.App,"deployment-failed-*.json"));
        using(var document=System.Text.Json.JsonDocument.Parse(File.ReadAllBytes(archived)))
        {var root=document.RootElement;Assert.Equal("AbortBeforeSnapshot",root.GetProperty("variant").GetString());Assert.Equal(System.Text.Json.JsonValueKind.Null,root.GetProperty("beforeSha256").ValueKind);Assert.Equal(System.Text.Json.JsonValueKind.Null,root.GetProperty("preparedSha256").ValueKind);}
        Assert.Same(scope.Finalization,scope.AbortBeforeSnapshotAsync());await Assert.ThrowsAsync<InvalidOperationException>(scope.AcquireAsync);
        using var next=r.Store.EnterRoleAdmission();
    }

    [Fact]public async Task PartialTicketIsRetainedAndNeverArchivedAsCleanAbort()
    {
        using var r=new Rig();var write=new IOException("PUBLIC_PARTIAL_WRITE");var close=new IOException("PUBLIC_PARTIAL_CLOSE");
        var faults=new InstallationDeploymentFaults{WriteTicket=(stream,bytes)=>{stream.Write(bytes.AsSpan(0,7));throw write;},BeforeClose=path=>{if(path==r.Store.PendingPath)throw close;}};
        var scope=r.Scope(faults:faults);
        try
        {
            var error=await Record.ExceptionAsync(scope.AcquireAsync);var errors=Assert.IsType<AggregateException>(error).Flatten().InnerExceptions;Assert.Contains(write,errors);Assert.Contains(close,errors);
            Assert.False(scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
        }
        finally{faults.BeforeClose=null;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.Equal(7,new FileInfo(r.Store.PendingPath).Length);Assert.True(scope.CleanupConfirmed);Assert.Throws<InstallationRecoveryException>(()=>r.Store.EnterRoleAdmission());
        Assert.Equal(InstallationDeploymentStatus.Unconfirmed,(await scope.Finalization).Status);
    }

    [Fact]public async Task CancellationDoesNotWaitForHeldOriginalWriterOrAbandonIt()
    {
        using var r=new Rig();using var cancel=new CancellationTokenSource();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var scope=r.Scope(cancel.Token,faults:new(){WriteTicket=(stream,bytes)=>{entered.SetResult();release.Task.GetAwaiter().GetResult();stream.Write(bytes);}});
        Task original=scope.AcquireAsync();await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try{cancel.Cancel();Assert.False(original.IsCompleted);Assert.False(scope.Finalization.IsCompleted);Assert.False(scope.CleanupConfirmed);}
        finally{release.SetResult();}
        await original;var result=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,result.Status);Assert.Contains(result.Errors,x=>x is OperationCanceledException);Assert.True(scope.CleanupConfirmed);
    }

    private sealed class FinalRead(string path):IInstallationDeploymentFinalRead
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Exception? ReadError,CloseError;
        internal Func<Task>? CloseHook;
        private FileStream? original;
        internal int Opens,Closes;
        public bool CleanupConfirmed=>original==null;
        public async Task VerifyAsync()
        {
            original=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None);Opens++;Entered.SetResult();await Release.Task;
            if(ReadError!=null)throw ReadError;
            Assert.Equal(1,original.Length);
        }
        public async Task CloseOriginalsAsync(){Closes++;if(CloseHook!=null)await CloseHook();if(CloseError!=null)throw CloseError;original?.Dispose();original=null;}
    }
    [Fact]public async Task ConcurrentSettlementSharesOnePublishedOriginalRetryTask()
    {
        using var r=new Rig();string path=Path.Combine(r.Root,"public-retry.bin");File.WriteAllBytes(path,[42]);var reader=new FinalRead(path){CloseError=new IOException("PUBLIC_FIRST_CLOSE")};
        var scope=r.Scope();scope.RegisterFinalRead(reader);await scope.AcquireAsync();reader.Release.SetResult();var outcome=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);
        reader.CloseError=null;var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        reader.CloseHook=async()=>{entered.TrySetResult();await release.Task;};
        Task<IReadOnlyList<Exception>> first=scope.SettleOriginalsAsync();await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));Task<IReadOnlyList<Exception>> second=scope.SettleOriginalsAsync();
        try{Assert.Same(first,second);Assert.Equal(2,reader.Closes);Assert.False(scope.CleanupConfirmed);}
        finally{release.SetResult();await first;await second;}
        Assert.True(scope.CleanupConfirmed);Assert.Equal(1,reader.Opens);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PostClosingReadIsOwnedByThePublishedFinalizationAndBothErrorsArePreserved(bool fail)
    {
        using var r=new Rig();string path=Path.Combine(r.Root,"public-read.bin");File.WriteAllBytes(path,[42]);var reader=new FinalRead(path);
        var read=new IOException("PUBLIC_READ");var close=new IOException("PUBLIC_READ_CLOSE");if(fail){reader.ReadError=read;reader.CloseError=close;}
        var scope=r.Scope();scope.RegisterFinalRead(reader);await scope.AcquireAsync();Task<InstallationDeploymentOutcome> original=scope.AbortBeforeSnapshotAsync();
        Assert.Same(scope.Finalization,original);await reader.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try
        {
            Assert.False(original.IsCompleted);Assert.False(scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));
            Assert.Throws<InstallationRecoveryException>(()=>r.Store.EnterRoleAdmission());
            Assert.Throws<InvalidOperationException>(()=>scope.RegisterFinalRead(new FinalRead(path)));
        }
        finally{reader.Release.SetResult();}
        var outcome=await original;Assert.Equal(1,reader.Opens);
        if(fail)
        {
            Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);Assert.Contains(read,outcome.Errors);Assert.Contains(close,outcome.Errors);Assert.False(scope.CleanupConfirmed);
            reader.CloseError=null;Assert.Empty(await scope.SettleOriginalsAsync());Assert.Equal(1,reader.Opens);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
        }
        else{Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,outcome.Status);Assert.Empty(outcome.Errors);Assert.True(scope.CleanupConfirmed);Assert.Equal(1,reader.Closes);}
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancellationOnEitherSideOfClosingDoesNotInventACommitOrReplay(bool after)
    {
        using var r=new Rig();using var cancel=new CancellationTokenSource();int writes=0;
        Func<Task> signal=()=>{cancel.Cancel();return Task.CompletedTask;};var scope=r.Scope(cancel.Token,faults:new(){BeforeClosing=after?null:signal,AfterClosing=after?signal:null,WriteTicket=(stream,bytes)=>{writes++;stream.Write(bytes);}});
        await scope.AcquireAsync();var outcome=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,outcome.Status);Assert.Equal(1,writes);Assert.True(scope.CleanupConfirmed);
        Assert.Equal(after?0:1,outcome.Errors.Count(x=>x is OperationCanceledException));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ArchiveFailureOrLostReceiptIsUnconfirmedAndNeverReplayed(bool moved)
    {
        using var r=new Rig();int attempts=0;var failure=new IOException("PUBLIC_ARCHIVE");var scope=r.Scope(faults:new(){Archive=(from,to)=>{attempts++;if(moved)File.Move(from,to);throw failure;}});
        await scope.AcquireAsync();var outcome=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);Assert.Contains(failure,outcome.Errors);Assert.True(scope.CleanupConfirmed);
        Assert.Equal(!moved,File.Exists(r.Store.PendingPath));Assert.Equal(moved?1:0,Directory.GetFiles(r.App,"deployment-failed-*.json").Length);
        Assert.Same(scope.Finalization,scope.AbortBeforeSnapshotAsync());Assert.Empty(await scope.SettleOriginalsAsync());Assert.Equal(1,attempts);
        if(!moved)Assert.Throws<InstallationRecoveryException>(()=>r.Store.EnterRoleAdmission());
    }

    [Fact]public async Task ForeignTicketCannotBeReplacedOrArchived()
    {
        using var r=new Rig();var scope=r.Scope();await scope.AcquireAsync();byte[] foreign="PUBLIC_FOREIGN"u8.ToArray();File.WriteAllBytes(r.Store.PendingPath,foreign);
        var outcome=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);Assert.Equal(foreign,File.ReadAllBytes(r.Store.PendingPath));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));Assert.True(scope.CleanupConfirmed);
    }

    [Theory][InlineData("terminal")][InlineData("pending")][InlineData("backup")]
    public async Task FinalizationReadAndWriteCloseFailuresKeepTheirExactOriginalHandles(string stage)
    {
        using var r=new Rig();bool armed=false;var failure=new IOException("PUBLIC_FINAL_CLOSE");
        var faults=new InstallationDeploymentFaults{BeforeClose=path=>{if(armed&&(stage=="terminal"?path.EndsWith(".tmp",StringComparison.Ordinal):stage=="pending"?path==r.Store.PendingPath:Path.GetFileName(path).StartsWith("deployment-ticket-",StringComparison.Ordinal)))throw failure;}};
        var scope=r.Scope(faults:faults);await scope.AcquireAsync();armed=true;
        try{var outcome=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);Assert.Contains(failure,outcome.Errors.SelectMany(x=>x is AggregateException a?a.Flatten().InnerExceptions:[x]));Assert.False(scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));}
        finally{armed=false;Assert.Empty(await scope.SettleOriginalsAsync());}
        Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
    }

    [Fact]public async Task MissingExistingStoreIsNotInitializedAndWritesNoTicket()
    {
        using var r=new Rig();string missing=Path.Combine(r.Root,"missing");var scope=new InstallationDeploymentScope(missing,r.Data,new string('A',64),new string('B',64));
        Assert.NotNull(await Record.ExceptionAsync(scope.AcquireAsync));Assert.False(Directory.Exists(missing));Assert.True(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Refused,(await scope.Finalization).Status);
    }

    private sealed class Registry(string path):IHostRegistrationStore
    {
        internal readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Exception? ReadError,CloseError;
        internal int Reads,Closes;
        private FileStream? original;
        public bool CleanupConfirmed=>original==null;
        public HostRegistrationSlot Read(IntegrationBrowser browser)
        {
            original=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.None);Reads++;Entered.TrySetResult();Release.Task.GetAwaiter().GetResult();
            if(ReadError!=null)throw ReadError;
            original.Dispose();original=null;return new(false,null);
        }
        public void FinalizeCleanup(){Closes++;if(CloseError!=null)throw CloseError;original?.Dispose();original=null;}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)=>throw new InvalidOperationException("Read-only fixture must never write.");
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualRegistryReadAdapterIsOwnedAfterClosingAndRetainsReadAndRawCloseFaults(bool fail)
    {
        using var r=new Rig();string path=Path.Combine(r.Root,"public-registry-handle.bin");File.WriteAllBytes(path,[42]);var registry=new Registry(path);
        var read=new IOException("PUBLIC_REGISTRY_READ");var close=new IOException("PUBLIC_REGISTRY_CLOSE");if(fail){registry.ReadError=read;registry.CloseError=close;}
        var reader=new InstallationDeploymentRegistryRead(registry,new(false,null),new(false,null));var scope=r.Scope();scope.RegisterFinalRead(reader);
        Assert.Throws<InvalidOperationException>(()=>scope.RegisterFinalRead(reader));await scope.AcquireAsync();var original=scope.AbortBeforeSnapshotAsync();
        await registry.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        try{Assert.False(original.IsCompleted);Assert.False(scope.CleanupConfirmed);Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));}
        finally{registry.Release.SetResult();}
        var outcome=await original;
        if(fail)
        {
            Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);Assert.Contains(read,outcome.Errors);Assert.Contains(close,outcome.Errors);Assert.Equal(1,registry.Reads);Assert.False(scope.CleanupConfirmed);
            registry.CloseError=null;Assert.Empty(await scope.SettleOriginalsAsync());Assert.Equal(1,registry.Reads);Assert.True(scope.CleanupConfirmed);Assert.True(File.Exists(r.Store.PendingPath));
        }
        else{Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,outcome.Status);Assert.Equal(2,registry.Reads);Assert.True(scope.CleanupConfirmed);Assert.Empty(outcome.Errors);}
    }
    [Fact]public async Task SuccessfulArchiveHasNoFallibleResourceCleanupIncludingExplicitSettlement()
    {
        using var r=new Rig();string path=Path.Combine(r.Root,"public-last-cleanup.bin");File.WriteAllBytes(path,[42]);var reader=new FinalRead(path);reader.Release.SetResult();
        InstallationDeploymentScope? scope=null;var faults=new InstallationDeploymentFaults{Archive=(from,to)=>{Assert.True(reader.CleanupConfirmed);Assert.Equal(1,reader.Closes);reader.CloseError=new IOException("PUBLIC_MUST_NOT_CLOSE_AGAIN");File.Move(from,to);}};
        scope=r.Scope(faults:faults);scope.RegisterFinalRead(reader);await scope.AcquireAsync();var outcome=await scope.AbortBeforeSnapshotAsync();
        Assert.Equal(InstallationDeploymentStatus.AbortedBeforeSnapshot,outcome.Status);Assert.Empty(outcome.Errors);Assert.True(scope.CleanupConfirmed);Assert.Empty(await scope.SettleOriginalsAsync());Assert.Equal(1,reader.Closes);
    }
    [Fact]public async Task ForeignBackupIsPreservedWithoutReplacingTheTicket()
    {
        using var r=new Rig();var scope=r.Scope();await scope.AcquireAsync();byte[] ticket=File.ReadAllBytes(r.Store.PendingPath);string backup=Path.Combine(r.App,"deployment-ticket-"+scope.Operation+".json");File.WriteAllText(backup,"PUBLIC_FOREIGN_BACKUP");
        var outcome=await scope.AbortBeforeSnapshotAsync();Assert.Equal(InstallationDeploymentStatus.Unconfirmed,outcome.Status);Assert.Equal(ticket,File.ReadAllBytes(r.Store.PendingPath));Assert.Equal("PUBLIC_FOREIGN_BACKUP",File.ReadAllText(backup));Assert.Empty(Directory.GetFiles(r.App,"deployment-failed-*.json"));Assert.True(scope.CleanupConfirmed);
    }
    [Fact]public async Task ExistingUnknownPendingIsNotClaimedOrRewritten()
    {
        using var r=new Rig();byte[] unknown="PUBLIC_UNKNOWN_PENDING"u8.ToArray();File.WriteAllBytes(r.Store.PendingPath,unknown);var scope=r.Scope();
        await Assert.ThrowsAsync<InstallationRecoveryException>(scope.AcquireAsync);Assert.Equal(unknown,File.ReadAllBytes(r.Store.PendingPath));Assert.True(scope.CleanupConfirmed);Assert.Equal(InstallationDeploymentStatus.Refused,(await scope.Finalization).Status);Assert.Empty(Directory.GetFiles(r.App,"deployment-*.json"));
    }
}
