using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using KuaiYi.Core.Installation;
using KuaiYi.NativeHost;
using KuaiYi.Core.Integration;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationDeploymentSessionTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Limit=TimeSpan.FromSeconds(12);
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string Driver=>Path.Combine(MaterializedPayloadSource.Repo,"tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll");
    private sealed class Registry:IHostRegistrationStore
    {
        internal HostRegistrationSlot Chrome=new(false,null),Edge=new(false,null);
        internal Exception? ReadError=null,CloseError=null;
        internal Action<IntegrationBrowser>? Reading=null;
        internal Action<IntegrationBrowser,bool>? Writing=null;
        internal Action? Closing=null;
        internal int Reads,Writes;
        public bool CleanupConfirmed=>CloseError==null;
        public HostRegistrationSlot Read(IntegrationBrowser browser){Reads++;Reading?.Invoke(browser);if(ReadError!=null)throw ReadError;return browser==IntegrationBrowser.Chrome?Chrome:Edge;}
        public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)
        {Writes++;Writing?.Invoke(browser,false);if(expected!=(browser==IntegrationBrowser.Chrome?Chrome:Edge))throw new HostRegistrationConflictException();if(browser==IntegrationBrowser.Chrome)Chrome=new(true,value);else Edge=new(true,value);Writing?.Invoke(browser,true);}
        public void FinalizeCleanup(){Closing?.Invoke();if(CloseError!=null)throw CloseError;}
    }
    private sealed class Peer:IAsyncDisposable
    {
        private static readonly List<Peer> Retained=[];
        private readonly NamedPipeServerStream pipe;
        private readonly StreamReader reader;private StreamWriter? writer;
        private readonly CancellationTokenSource stop=new();
        private readonly Task connected;
        private readonly object sync=new();private readonly List<Task> originals=[];private bool sealedProducer,closed;
        internal bool CleanupConfirmed{get{lock(sync)return closed&&connected.IsCompleted&&originals.All(x=>x.IsCompleted);}}
        internal Peer(string name)
        {pipe=new(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);reader=new(pipe);connected=pipe.WaitForConnectionAsync(stop.Token);}
        private Task<T> Own<T>(Func<Task<T>> action)
        {
            lock(sync)
            {
                if(sealedProducer)throw new IOException("Fixture IO producer is sealed.");var begin=Signal();
                var task=Task.Run(async()=>{await begin.Task;return await action();});originals.Add(task);begin.SetResult();return task;
            }
        }
        internal Task Send(string value)=>Own(async()=>{await connected;writer??=new(pipe);await writer.WriteLineAsync(value.AsMemory(),stop.Token);await writer.FlushAsync(stop.Token);return true;}).WaitAsync(Limit);
        internal Task<string?> ReadPublic()=>Own(()=>reader.ReadLineAsync(stop.Token).AsTask()).WaitAsync(Limit);
        internal async Task Close(){await Send("CLOSE");var original=Own(()=>reader.ReadLineAsync(stop.Token).AsTask());Assert.Equal("PUBLIC_SHELL_CLOSED",await original.WaitAsync(Limit));}
        public async ValueTask DisposeAsync()
        {
            Task[] tasks;lock(sync){sealedProducer=true;tasks=originals.Prepend(connected).ToArray();}
            var errors=new List<Exception>();try{stop.Cancel();}catch(Exception error){errors.Add(error);}
            foreach(var task in tasks)try{await task.WaitAsync(Limit);}catch(OperationCanceledException)when(stop.IsCancellationRequested){}catch(Exception error){errors.Add(error);}
            if(tasks.Any(x=>!x.IsCompleted)){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original fixture IO remains pending.");}
            foreach(IDisposable? value in new IDisposable?[]{writer,reader,pipe,stop})try{value?.Dispose();}catch(Exception error){errors.Add(error);}
            lock(sync)closed=errors.Count==0;
            InstallationErrors.Throw(errors);
        }
    }
    private sealed class Port(ITestOutputHelper output):IInstallationSessionProtocol,IOwnedProcessStarter,IInstallationStartupRemovalProtocol
    {
        private readonly InstallationApplicationClient client=new();
        internal InstallationStartupRemovalServer? StartupServer;
        void IInstallationStartupRemovalProtocol.AttachStartupRemoval(Process child,InstallationStartupRemovalServer server)
        {StartupServer=server;((IInstallationStartupRemovalProtocol)client).AttachStartupRemoval(child,server);}
        Task IInstallationStartupRemovalProtocol.SendStartupRemovalAsync(Process child,string line,CancellationToken cancellation)
            =>((IInstallationStartupRemovalProtocol)client).SendStartupRemovalAsync(child,line,cancellation);
        internal readonly TaskCompletionSource Healthy=Signal(),HealthRelease=Signal(),IoEntered=Signal(),IoRelease=Signal();
        internal bool HoldHealth,HoldIo=false;
        internal Exception? HealthError,IoError;
        internal Action? BeforeInstallerStart=null;
        internal int Starts;internal readonly List<int> Exits=[];
        internal Process? Child;
        internal InstallationControlledLaunch? Proof;
        public Process Start(ProcessStartInfo info)
        {Starts++;BeforeInstallerStart?.Invoke();string selected=Path.GetDirectoryName(info.FileName)!;info.FileName=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet";info.ArgumentList.Insert(0,Path.Combine(selected,"KuaiYi.SettingsStoreFixture.dll"));return Child=Process.Start(info)!;}
        public async Task AdmitLaunchAsync(Process owned,InstallationControlledLaunch proof,CancellationToken cancellation)
        {Proof=proof;await client.AdmitLaunchAsync(owned,proof,cancellation);}
        public async Task ConfirmHealthyAsync(Process owned,InstalledVersion version,Guid launch,CancellationToken cancellation)
        {await client.ConfirmHealthyAsync(owned,version,launch,cancellation);Healthy.TrySetResult();if(HoldHealth)await HealthRelease.Task;if(HealthError!=null)throw HealthError;}
        public Task RequestExitAsync(Process owned,CancellationToken cancellation)=>client.RequestExitAsync(owned,cancellation);
        public async Task SettleAsync(Process owned,CancellationToken cancellation)
        {
            await client.SettleAsync(owned,cancellation);Assert.True(owned.HasExited);Exits.Add(owned.ExitCode);
            output.WriteLine($"PUBLIC_DEPLOYMENT_SESSION originalExit={owned.ExitCode} originalIO=Settled driverSha256={MaterializedPayloadSource.Hash(Driver)} coreSha256={MaterializedPayloadSource.Hash(Path.Combine(Path.GetDirectoryName(Driver)!,"KuaiYi.Core.dll"))}");
            IoEntered.TrySetResult();if(HoldIo)await IoRelease.Task;if(IoError!=null)throw IoError;
        }
        public async Task SettleSessionAsync(Process owned,CancellationToken cancellation)
        {await client.SettleSessionAsync(owned,cancellation);Assert.True(owned.HasExited);Exits.Add(owned.ExitCode);output.WriteLine($"PUBLIC_DEPLOYMENT_SESSION cleanExit={owned.ExitCode} originalIO=Settled");}
    }
    private sealed partial class Rig(ITestOutputHelper output)
    {
        private static readonly List<Rig> Retained=[];
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-upgrade-tests-"+Guid.NewGuid().ToString("N"));
        internal readonly string Pipe="ky-deployment-session-"+Guid.NewGuid().ToString("N");
        internal string App=>Path.Combine(Root,"application");internal string Data=>Path.Combine(Root,"user-data");
        internal string Owner=>Path.Combine(App,"execution-owner.json");
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal readonly Registry Registry=new();
        internal readonly Port Port=new(output);
        internal readonly CancellationTokenSource Stop=new();
        internal InstallationDeploymentScope Scope=null!;internal InstallationDeploymentPrepared Prepared=null!;
        internal InstallationUpgradeCoordinator? Controller;
        internal InstallationDeploymentSession? Session;
        internal InstallationDeploymentCompensation? Compensation;
        internal Peer? Peer;
        internal async Task<(MaterializedPayloadSource Source,BootstrapSource Bootstrap)> Sources(bool upgrade,bool initialize=true)
        {
            if(initialize){using var initial=Store.EnterRoleAdmission();}Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_OFF"),"PUBLIC_PRESERVED");
            var source=new MaterializedPayloadSource(Path.Combine(Root,"source"));
            foreach(string name in new[]{"KuaiYi.SettingsStoreFixture.dll","KuaiYi.SettingsStoreFixture.deps.json","KuaiYi.SettingsStoreFixture.runtimeconfig.json"})File.Copy(Path.Combine(Path.GetDirectoryName(Driver)!,name),Path.Combine(source.Root,name));
            File.WriteAllBytes(Path.Combine(source.Root,"public-install-behavior.json"),JsonSerializer.SerializeToUtf8Bytes(new{version="1.0",mode="healthy",runtimePipe=OperationPreviousPipe??Pipe,controlled=true}));source.Catalog();
            var b=new BootstrapSource(Path.Combine(Root,"bootstrap"));
            if(upgrade)
            {
                await Store.ApplyAsync(source.Root,source.Digest);if(!SkipPreviousBootstrap)await new InstallationBootstrapDeployment(App,Data).DeployAsync(b.Root,b.Digest);if(!SkipPreviousHost)await new HostRegistrationTransaction(App,Data,Registry).RunAsync(true);
                source.Version="2.0";File.WriteAllBytes(Path.Combine(source.Root,"public-install-behavior.json"),JsonSerializer.SerializeToUtf8Bytes(new{version="2.0",mode="healthy",runtimePipe=Pipe,controlled=true}));source.Catalog();
            }
            return(source,b);
        }
        internal async Task Commit(InstallationDeploymentFaults? faults=null,bool upgrade=false)
        {
            var(source,b)=await Sources(upgrade);
            Scope=new(App,Data,source.Digest,b.Digest,faults:faults);
            await Scope.AcquireAsync();await Scope.CaptureBeforeAsync(new InstallationDeploymentRegistrySnapshot(Registry));
            var g=await Scope.PrepareGenerationAsync(source.Root);var bootstrap=await Scope.PrepareBootstrapAsync(b.Root,g);Prepared=await Scope.SealPreparedAsync(g,bootstrap);
            var result=await Scope.CommitPreparedAsync(Prepared);if(faults==null)Assert.Equal(InstallationDeploymentStatus.Committed,result.Status);
        }
        internal InstallationDeploymentSession Begin(Func<InstallationControlledLaunch,CancellationToken,Task>? publish=null,InstallationPreparationFaults? readFaults=null,InstallationAdmissionFaults? admissionFaults=null)
        {
            Peer=new(Pipe);Controller=new(App,Data,Port,TimeSpan.FromSeconds(2),Port);
            return Session=Scope.BeginCommittedSession(Controller,publish??((_,_)=>Task.CompletedTask),Stop.Token,readFaults,admissionFaults);
        }
        internal void LeaseHeld()
        {Assert.True(File.Exists(Owner));Assert.ThrowsAny<IOException>(()=>new FileStream(Path.Combine(App,"execution-owner.lock"),FileMode.Open,FileAccess.ReadWrite,FileShare.None));Assert.ThrowsAny<IOException>(()=>Store.EnterRoleAdmission());}
        internal async Task Cleanup()
        {
            var errors=new List<Exception>();Port.HealthRelease.TrySetResult();Port.IoRelease.TrySetResult();try{Stop.Cancel();}catch(Exception error){errors.Add(error);}
            if(Session!=null)try{await Session.Completion.WaitAsync(Limit);}catch(Exception error){errors.Add(error);}
            if(Session is{Completion.IsCompleted:false}){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original fixture session remains pending.");}
            if(Compensation!=null)try{await Compensation.Completion.WaitAsync(Limit);}catch(Exception error){errors.Add(error);}
            if(Compensation is{Completion.IsCompleted:false}){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original fixture compensation remains pending.");}
            if(Compensation?.OriginalSettlement is{} originalSettlement)
            {
                try{await originalSettlement.WaitAsync(Limit);}catch(Exception error){errors.Add(error);}
                if(!originalSettlement.IsCompleted){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original fixture settlement remains pending.");}
            }
            if(Operation?.OriginalWorkPending==true){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original installation operation remains pending; Stop/root retained.");}
            if(Restored?.OriginalWorkPending==true){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original restored fixture work remains pending; Stop/root retained.");}
            bool retained=RestoredRetention||File.Exists(Owner)||File.Exists(Store.PendingPath)||Compensation is not null&&Compensation.Completion.Result.Status==InstallationCompensationStatus.Unconfirmed;
            if(Controller!=null)
            {
                bool owns=Session!=null&&Controller.DeploymentOwnsExecution(Session);
                try{await Controller.DisposeAsync();if(owns)errors.Add(new InvalidOperationException("Original deployment owner was silently discarded."));}
                catch(InstallationUpgradeUnconfirmedException)when(owns){}
                catch(Exception error){errors.Add(error);}
            }
            if(Peer!=null)try{await Peer.DisposeAsync();}catch(Exception error){errors.Add(error);}
            if(Peer is{CleanupConfirmed:false}){lock(Retained)Retained.Add(this);output.WriteLine("PUBLIC_RETAINED_ORIGINAL_ROOT "+Root);InstallationErrors.Throw(errors);throw new IOException("Original fixture peer remains unconfirmed.");}
            if(OperationPreviousPeer is{CleanupConfirmed:false}){lock(Retained)Retained.Add(this);InstallationErrors.Throw(errors);throw new IOException("Original previous operation peer is unconfirmed; Stop/root retained.");}
            try{Assert.Equal("PUBLIC_PRESERVED",File.ReadAllText(Path.Combine(Data,"PUBLIC_OFF")));}catch(Exception error){errors.Add(error);}
            if(!retained&&errors.Count==0)Directory.Delete(Root,true);else output.WriteLine("PUBLIC_RETAINED_ORIGINAL_ROOT "+Root);
            try{Stop.Dispose();}catch(Exception error){errors.Add(error);}InstallationErrors.Throw(errors);
        }
    }
    private async Task Execute(Func<Rig,Task> body)
    {
        var r=new Rig(output);var errors=new List<Exception>();try{await body(r);}catch(Exception error){errors.Add(error);}
        try{await r.Cleanup();}catch(Exception error){errors.Add(error);}InstallationErrors.Throw(errors);
    }
    [Fact]public Task CommitIsNotReadyAndOnlyOriginalSuccessfulScopeCanActivateOnce()=>Execute(async r=>
    {
        await r.Commit();Assert.True(r.Scope.CleanupConfirmed);Assert.False(File.Exists(r.Owner));
        r.Port.HoldHealth=true;var session=r.Begin();await r.Port.Healthy.Task.WaitAsync(Limit);Assert.False(session.Ready.IsCompleted);Assert.False(File.Exists(Path.Combine(r.App,"browser-host-session.json")));
        Assert.Throws<InstallationRecoveryException>(()=>r.Scope.BeginCommittedSession(r.Controller!,(_,_)=>Task.CompletedTask));
        r.Port.HealthRelease.SetResult();var ready=await session.Ready.WaitAsync(Limit);Assert.Equal(r.Scope.Operation,ready.Operation);Assert.Equal(r.Prepared.Sha256,ready.PreparedSha256);Assert.Equal(r.Prepared.Plan.Generation,ready.Generation);Assert.Equal(r.Port.Proof!.Launch,ready.Launch);Assert.Equal(r.Port.Proof.Fingerprint,ready.Proof);Assert.True(File.Exists(Path.Combine(r.App,"browser-host-session.json")));
        await r.Peer!.Close();var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.CleanExit,result.Status);Assert.Same(ready,result.Ready);Assert.Empty(result.Errors);Assert.False(session.OriginalFailedSessionRetired);Assert.True(session.SessionWorkSettled);Assert.False(File.Exists(r.Owner));Assert.Equal(new[]{0},r.Port.Exits);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task ReceiptUncertainOrUnconfirmedNeverCreatesHealthAuthority(bool moved)=>Execute(async r=>
    {
        await r.Commit(new(){Archive=(from,to)=>{if(moved)File.Move(from,to);throw new IOException("PUBLIC_ARCHIVE_UNCERTAIN");}});
        r.Controller=new(r.App,r.Data,r.Port,TimeSpan.FromSeconds(2),r.Port);
        Assert.Throws<InstallationRecoveryException>(()=>r.Scope.BeginCommittedSession(r.Controller,(_,_)=>Task.CompletedTask));Assert.Equal(0,r.Port.Starts);Assert.False(File.Exists(r.Owner));
    });
    [Theory][InlineData("current")][InlineData("bootstrap")][InlineData("host")][InlineData("archive")][InlineData("registry")]
    public Task ChangedCommittedTargetsNeverStartOrGrantRollback(string target)=>Execute(async r=>
    {
        await r.Commit();string path=target switch{"current"=>Path.Combine(r.App,"current.json"),"bootstrap"=>InstallationBootstrapDeployment.ActivePath(r.App),"host"=>Path.Combine(r.App,"host-registration.json"),_=>Path.Combine(r.App,"deployment-committed-"+r.Scope.Operation+".json")};
        if(target=="registry")r.Registry.Chrome=new(true,"PUBLIC_FOREIGN",true);else File.WriteAllText(path,"PUBLIC_FOREIGN");
        var result=await r.Begin().Completion.WaitAsync(Limit);Assert.Equal(0,r.Port.Starts);Assert.Null(result.Ready);Assert.False(r.Session!.OriginalFailedSessionRetired);Assert.NotEqual(InstallationDeploymentSessionStatus.CleanExit,result.Status);if(File.Exists(r.Owner)){Assert.Equal(InstallationDeploymentSessionStatus.Unconfirmed,result.Status);r.LeaseHeld();}else Assert.Equal(InstallationDeploymentSessionStatus.ActivationBlocked,result.Status);
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task ReadyPublicationFailureOrCancellationRetainsExactRetiredExecution(bool cancel)=>Execute(async r=>
    {
        await r.Commit();var entered=Signal();var release=Signal();var failure=new IOException("PUBLIC_READY_PUBLICATION_FAILED");
        var session=r.Begin(async(_,_)=>{entered.TrySetResult();await release.Task;if(!cancel)throw failure;});
        try{await entered.Task.WaitAsync(Limit);Assert.True(File.Exists(Path.Combine(r.App,"browser-host-session.json")));Assert.False(session.Ready.IsCompleted);if(cancel)r.Stop.Cancel();}finally{release.TrySetResult();}
        var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.RollbackRequired,result.Status);Assert.Null(result.Ready);Assert.True(session.OriginalFailedSessionRetired);Assert.True(session.SessionWorkSettled);Assert.False(File.Exists(Path.Combine(r.App,"browser-host-session.json")));r.LeaseHeld();Assert.Single(r.Port.Exits);Assert.True(File.Exists(Path.Combine(r.App,"deployment-committed-"+r.Scope.Operation+".json")));Assert.False(File.Exists(r.Store.PendingPath));
        if(!cancel)Assert.Contains(failure,Flatten(result.Errors));
        await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>r.Controller!.DisposeAsync().AsTask());r.LeaseHeld();Assert.True(session.OriginalFailedSessionRetired);
    });
    [Fact]public Task FailedHealthKeepsOriginalRoleAndDoesNotPublishHostOrReady()=>Execute(async r=>
    {
        await r.Commit();var failure=new IOException("PUBLIC_APPLICATION_HEALTH_FAILED");r.Port.HealthError=failure;
        var session=r.Begin();var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.RollbackRequired,result.Status);Assert.Null(result.Ready);Assert.Contains(failure,Flatten(result.Errors));Assert.False(File.Exists(Path.Combine(r.App,"browser-host-session.json")));Assert.True(session.OriginalFailedSessionRetired);Assert.Single(r.Port.Exits);r.LeaseHeld();
    });
    [Fact]public Task OriginalHealthAndIoErrorsAreBothPreservedAndUnknownCannotAuthorizeRollback()=>Execute(async r=>
    {
        await r.Commit();var health=new IOException("PUBLIC_HEALTH_ORIGINAL");var cleanup=new IOException("PUBLIC_IO_ORIGINAL");r.Port.HealthError=health;r.Port.IoError=cleanup;
        var session=r.Begin();var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.Unconfirmed,result.Status);Assert.Contains(health,Flatten(result.Errors));Assert.Contains(cleanup,Flatten(result.Errors));Assert.False(session.OriginalFailedSessionRetired);Assert.False(session.SessionWorkSettled);r.LeaseHeld();
    });
    [Theory][InlineData(false)][InlineData(true)]
    public Task ReadyIsHistoricalAndLaterExitOrCancellationDoesNotAuthorizeAutomaticRollback(bool cancel)=>Execute(async r=>
    {
        await r.Commit();var session=r.Begin();var ready=await session.Ready.WaitAsync(Limit);
        if(cancel)r.Stop.Cancel();else await r.Peer!.Close();var result=await session.Completion.WaitAsync(Limit);Assert.Same(ready,result.Ready);Assert.False(session.OriginalFailedSessionRetired);Assert.Equal(cancel?InstallationDeploymentSessionStatus.Unconfirmed:InstallationDeploymentSessionStatus.CleanExit,result.Status);if(cancel)r.LeaseHeld();
    });
    [Theory][InlineData("pending")][InlineData("foreign-owner")][InlineData("wrong-controller")]
    public Task PendingForeignOwnerAndWrongControllerCannotBorrowCommittedAuthority(string fault)=>Execute(async r=>
    {
        await r.Commit();if(fault=="pending")File.WriteAllText(r.Store.PendingPath,"PUBLIC_UNKNOWN_PENDING");
        if(fault=="foreign-owner")File.WriteAllText(r.Owner,"PUBLIC_FOREIGN_OWNER");
        if(fault=="wrong-controller")
        {r.Controller=new(Path.Combine(r.Root,"other"),r.Data,r.Port,TimeSpan.FromSeconds(2),r.Port);r.Session=r.Scope.BeginCommittedSession(r.Controller,(_,_)=>Task.CompletedTask);}
        else r.Begin();var result=await r.Session!.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.ActivationBlocked,result.Status);Assert.Equal(0,r.Port.Starts);Assert.False(r.Session.OriginalFailedSessionRetired);if(fault=="foreign-owner")Assert.Equal("PUBLIC_FOREIGN_OWNER",File.ReadAllText(r.Owner));
    });
    [Fact]public Task ReadyWaitsForOriginalPublicationAndClosedReadResources()=>Execute(async r=>
    {
        await r.Commit();var entered=Signal();var release=Signal();var session=r.Begin(async(_,_)=>{entered.SetResult();await release.Task;});
        try{await entered.Task.WaitAsync(Limit);Assert.False(session.Ready.IsCompleted);Assert.NotNull(r.Controller!.StablePublicationOriginal);Assert.False(r.Controller.StablePublicationOriginal!.IsCompleted);}
        finally{release.TrySetResult();}
        await session.Ready.WaitAsync(Limit);await r.Peer!.Close();Assert.Equal(InstallationDeploymentSessionStatus.CleanExit,(await session.Completion.WaitAsync(Limit)).Status);
    });
    [Fact]public Task LateOriginalPublicationFaultSurvivesCancellationAndRetirement()=>Execute(async r=>
    {
        await r.Commit();var entered=Signal();var release=Signal();var failure=new IOException("PUBLIC_LATE_ORIGINAL_READY_FAULT");
        var session=r.Begin(async(_,_)=>{entered.SetResult();await release.Task;throw failure;});
        try{await entered.Task.WaitAsync(Limit);r.Stop.Cancel();await r.Port.IoEntered.Task.WaitAsync(Limit);}finally{release.TrySetResult();}
        var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.RollbackRequired,result.Status);Assert.Contains(failure,Flatten(result.Errors));Assert.Contains(Flatten(result.Errors),e=>e is OperationCanceledException);Assert.True(session.OriginalFailedSessionRetired);r.LeaseHeld();
    });
    [Fact]public Task AcquisitionAndCloseFailuresBeforeExecutionAreUnconfirmedAndBothPreserved()=>Execute(async r=>
    {
        await r.Commit();var acquire=new IOException("PUBLIC_SESSION_ADMISSION_ACQUIRE");var close=new IOException("PUBLIC_SESSION_ADMISSION_CLOSE");InstallationAdmission? original=null;
        var faults=new InstallationAdmissionFaults{OwnerCreated=o=>original=o,Acquired=()=>throw acquire,BeforeClose=()=>throw close};
        try
        {
            var session=r.Begin(admissionFaults:faults);var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.Unconfirmed,result.Status);Assert.Contains(acquire,Flatten(result.Errors));Assert.Contains(close,Flatten(result.Errors));Assert.False(session.SessionWorkSettled);Assert.False(session.OriginalFailedSessionRetired);Assert.False(File.Exists(r.Owner));Assert.Equal(0,r.Port.Starts);Assert.NotNull(original);Assert.False(original!.CleanupConfirmed);
        }
        finally{faults.BeforeClose=null;}
    });
    [Fact]public Task HeldOriginalCleanupCannotSignFailedSessionRetirement()=>Execute(async r=>
    {
        await r.Commit();r.Port.HealthError=new IOException("PUBLIC_HEALTH_BEFORE_HELD_IO");r.Port.HoldIo=true;var session=r.Begin();
        try{await r.Port.IoEntered.Task.WaitAsync(Limit);Assert.False(session.OriginalFailedSessionRetired);r.LeaseHeld();}
        finally{r.Port.IoRelease.TrySetResult();}
        var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.RollbackRequired,result.Status);Assert.True(session.OriginalFailedSessionRetired);Assert.Null(result.Ready);r.LeaseHeld();
    });
    [Fact]public Task ActualExitBeforeReadyCallbackReturnsCannotSignHealthy()=>Execute(async r=>
    {
        await r.Commit();var entered=Signal();var release=Signal();
        // This diagnostic wait is inside the original, tracked publication callback. It never substitutes for the lifetime fence.
        var session=r.Begin(async(_,_)=>{entered.SetResult();await release.Task;await r.Port.Child!.WaitForExitAsync(CancellationToken.None);});
        try{await entered.Task.WaitAsync(Limit);await r.Peer!.Close();}finally{release.TrySetResult();}
        var result=await session.Completion.WaitAsync(Limit);Assert.Equal(InstallationDeploymentSessionStatus.RollbackRequired,result.Status);Assert.Null(result.Ready);Assert.True(session.OriginalFailedSessionRetired);Assert.Equal(new[]{0},r.Port.Exits);r.LeaseHeld();
    });
    private static IEnumerable<Exception> Flatten(IEnumerable<Exception> errors)=>errors.SelectMany(e=>e is AggregateException a?a.Flatten().InnerExceptions:e is InstallationUpgradeUnconfirmedException {InnerException:not null} u?Flatten([u.InnerException!]):[e]);
}
