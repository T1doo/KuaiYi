using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Core.Installation;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationSessionTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Limit=TimeSpan.FromSeconds(8);
    private static string Repo{get{var p=new DirectoryInfo(AppContext.BaseDirectory);while(p!=null&&!File.Exists(Path.Combine(p.FullName,"SPEC.md")))p=p.Parent;return p!.FullName;}}
    private static string Driver=>Path.Combine(Repo,"tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll");
    private static string Host=>Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet";
    private static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-upgrade-tests-"+Guid.NewGuid().ToString("N"));
        internal readonly string Pipe="ky-session-"+Guid.NewGuid().ToString("N");
        internal bool Keep;internal string Mode="healthy";
        internal string App=>Path.Combine(Root,"application");internal string Data=>Path.Combine(Root,"user-data");
        internal string Owner=>Path.Combine(App,"execution-owner.json");
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal async Task<InstalledVersion> Prepare(string mode="healthy")
        {
            Mode=mode;
            Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_SENTINEL"),"PUBLIC_OFF");string path=Path.Combine(Root,"package");Directory.CreateDirectory(path);
            foreach(string name in new[]{"KuaiYi.SettingsStoreFixture.dll","KuaiYi.SettingsStoreFixture.runtimeconfig.json","KuaiYi.SettingsStoreFixture.deps.json","KuaiYi.Core.dll"})File.Copy(Path.Combine(Path.GetDirectoryName(Driver)!,name),Path.Combine(path,name));
            File.WriteAllText(Path.Combine(path,"KuaiYi.App.exe"),"PUBLIC_DOTNET_PORT_ONLY");
            File.WriteAllBytes(Path.Combine(path,"public-install-behavior.json"),JsonSerializer.SerializeToUtf8Bytes(new{version="1.0",mode,runtimePipe=Pipe,controlled=true}));
            var files=Directory.GetFiles(path).Order().Select(p=>new{path=Path.GetFileName(p),bytes=new FileInfo(p).Length,sha256=Hash(p)}).ToArray();
            File.WriteAllBytes(Path.Combine(path,"package.json"),JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",version="1.0",files}));
            return await Store.ApplyAsync(path,Hash(Path.Combine(path,"package.json")));
        }
        internal void Fence()=>Assert.ThrowsAny<IOException>(()=>Store.EnterRoleAdmission());
        public void Dispose(){if(!Keep&&Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private sealed class Parent:IAsyncDisposable
    {
        private readonly Process process;private readonly Task<string> error;private readonly List<Task<string?>> reads=[];
        private Task? disposal;private Task<string>? remainder;private int started,retired;
        internal readonly List<string> RetirementEvidenceRows=[];
        internal readonly Task OriginalExit;
        internal Parent(Process original,Task<string> originalError){process=original;error=originalError;OriginalExit=process.WaitForExitAsync();}
        internal Process Process=>process;
        private void Observe(string? row)
        {
            if(row=="PUBLIC_SESSION_CHILD_STARTED")started++;
            if((row?.StartsWith("PUBLIC_SESSION_CHILD_EXIT|",StringComparison.Ordinal)==true||row?.StartsWith("PUBLIC_SESSION_CHILD_CLEANUP_EXIT|",StringComparison.Ordinal)==true)
                &&(row.Contains("|originalIO=Settled|",StringComparison.Ordinal)||row.Contains("|originalIO=FaultObserved|",StringComparison.Ordinal)))
            {retired++;RetirementEvidenceRows.Add(row);}
        }
        internal bool ChildRetirementObserved=>OriginalExit.IsCompletedSuccessfully&&started==retired;
        internal bool ChildWasObserved=>started!=0;
        internal async Task<string?> Row()
        {
            long began=Stopwatch.GetTimestamp();
            while(true)
            {
                TimeSpan remaining=Limit-Stopwatch.GetElapsedTime(began);if(remaining<=TimeSpan.Zero)throw new TimeoutException("Original parent row deadline expired.");
                var original=process.StandardOutput.ReadLineAsync();reads.Add(original);string? row=await original.WaitAsync(remaining);Observe(row);
                if(row?.StartsWith("PUBLIC_SESSION_CHILD_CLEANUP_EXIT|",StringComparison.Ordinal)==true)continue;
                return row;
            }
        }
        internal async Task JoinOriginalAsync()
        {
            await OriginalExit.WaitAsync(Limit);
            foreach(var original in reads)await original.WaitAsync(Limit);
            string rest=await (remainder??=process.StandardOutput.ReadToEndAsync()).WaitAsync(Limit);
            foreach(string row in rest.Split('\n',StringSplitOptions.RemoveEmptyEntries))Observe(row.TrimEnd('\r'));
            Assert.Equal("",await error.WaitAsync(Limit));
        }
        internal async Task Finish(int exit)
        {await OriginalExit.WaitAsync(Limit);Assert.Equal(exit,process.ExitCode);Assert.Equal("",await error);}
        public ValueTask DisposeAsync()=>new(disposal??=DisposeCore());
        private async Task DisposeCore()
        {
            List<Exception> errors=[];
            try{if(!OriginalExit.IsCompleted)process.Kill();}catch(Exception e){errors.Add(e);}
            try{await OriginalExit.WaitAsync(Limit);}catch(Exception e){errors.Add(e);}
            try{await error.WaitAsync(Limit);}catch(Exception e){errors.Add(e);}
            try{process.Dispose();}catch(Exception e){errors.Add(e);}
            ThrowBrowserErrors(errors);
        }
    }
    private static Parent Launch(Rig r,InstalledVersion selected)
    {
        var info=new ProcessStartInfo(Host){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{Driver,"--installed-session-launch",r.Root,selected.DirectoryPath})info.ArgumentList.Add(arg);
        Process p=Process.Start(info)!;return new(p,p.StandardError.ReadToEndAsync());
    }
    private sealed class FailingStartupOutput(ITestOutputHelper inner):ITestOutputHelper
    {
        public void WriteLine(string message)
        {if(message.StartsWith("PUBLIC_SESSION_STARTUP ",StringComparison.Ordinal))throw new InvalidOperationException("PUBLIC_DIAGNOSTIC_OUTPUT_FAILED");inner.WriteLine(message);}
        public void WriteLine(string format,params object[] args)=>WriteLine(string.Format(format,args));
    }
    private sealed class Port(ITestOutputHelper output):IInstallationImageSessionProtocol,IOwnedProcessStarter
    {
        private readonly InstallationApplicationClient client=new();
        internal InstallationControlledLaunch? Proof;
        internal readonly TaskCompletionSource ImageQueued=Signal(),BrowserPublished=Signal();
        public async Task QueueImageAsync(Process p,string path,CancellationToken ct){await client.QueueImageAsync(p,path,ct);ImageQueued.TrySetResult();}
        internal InstallationWarmImageClient Warm(TimeProvider? clock=null)=>client.AttachWarm(Children.Single(),clock??TimeProvider.System);
        internal void HoldWarmWrites(Func<Stream,Stream> wrap)=>client.WriteStreamForTest=wrap;
        internal readonly List<Process> Children=[];
        internal readonly List<(Process Process,Task Exit)> OriginalExits=[];
        internal readonly List<Task> OriginalIo=[];
        private int warmExitObserved,warmExitCode;
        internal bool WarmActualExitObserved=>Volatile.Read(ref warmExitObserved)!=0;
        internal int WarmActualExitCode=>Volatile.Read(ref warmExitCode);
        private void ObserveWarmExit(Process p){Assert.True(p.HasExited);Volatile.Write(ref warmExitCode,p.ExitCode);Volatile.Write(ref warmExitObserved,1);}
        internal readonly TaskCompletionSource ExitRequested=Signal();
        internal readonly TaskCompletionSource Healthy=Signal(),StartHeld=Signal(),StartRelease=Signal(),IoEntered=Signal(),IoRelease=Signal(),HealthRelease=Signal();
        internal bool HoldStart,HoldIo,FailIo,HoldHealth,HoldCleanupIo;
        internal readonly TaskCompletionSource CleanupIoEntered=Signal(),CleanupIoRelease=Signal();
        internal int SessionIoCalls;
        internal readonly List<int> CleanupExits=[];
        private string startupPhase="NotStarted";
        private Task? clientAdmission,clientHealthReceipt;
        private int diagnosticPid,diagnosticExit,diagnosticExitKnown;
        internal Exception? DiagnosticFailure{get;private set;}
        private void ReportStartupFailure(Task originalRun)
        {
            try
            {
                output.WriteLine("PUBLIC_SESSION_STARTUP "+JsonSerializer.Serialize(new{
                Run=originalRun.Status.ToString(),Healthy=Healthy.Task.Status.ToString(),Phase=Volatile.Read(ref startupPhase),
                ClientAdmission=Volatile.Read(ref clientAdmission)?.Status.ToString(),ClientHealthReceipt=Volatile.Read(ref clientHealthReceipt)?.Status.ToString(),
                ActualPid=Volatile.Read(ref diagnosticPid),ActualExit=Volatile.Read(ref diagnosticExitKnown)==0?(int?)null:Volatile.Read(ref diagnosticExit),
                OriginalIoSettled=Volatile.Read(ref diagnosticExitKnown)!=0,
                RunFailure=originalRun.Exception?.GetBaseException().GetType().Name,RunHResult=originalRun.Exception?.GetBaseException().HResult,
                RunInnerFailure=originalRun.Exception?.InnerException?.InnerException?.GetType().Name,RunInnerHResult=originalRun.Exception?.InnerException?.InnerException?.HResult,
                Host,driverSha256=Hash(Driver),coreSha256=Hash(Path.Combine(Path.GetDirectoryName(Driver)!,"KuaiYi.Core.dll"))}));
            }
            catch(Exception diagnosticFailure){DiagnosticFailure=diagnosticFailure;}
        }
        internal async Task WaitForHealthyAsync(Task originalRun)
        {
            try
            {
                await Task.WhenAny(Healthy.Task,originalRun).WaitAsync(Limit);
                if(originalRun.IsCompleted)
                {
                    await originalRun;
                    if(!Healthy.Task.IsCompleted)throw new InvalidOperationException("Session ended before a healthy receipt.");
                }
                await Healthy.Task;
            }
            catch{ReportStartupFailure(originalRun);throw;}
        }
        public Process Start(ProcessStartInfo info)
        {
            string selected=Path.GetDirectoryName(info.FileName)!;info.FileName=Host;info.ArgumentList.Insert(0,Path.Combine(selected,"KuaiYi.SettingsStoreFixture.dll"));
            Volatile.Write(ref startupPhase,"Start");Process p=Process.Start(info)!;Volatile.Write(ref diagnosticPid,p.Id);Volatile.Write(ref startupPhase,"Started");
            Children.Add(p);OriginalExits.Add((p,p.WaitForExitAsync()));if(HoldStart){StartHeld.TrySetResult();StartRelease.Task.GetAwaiter().GetResult();}return p;
        }
        public async Task AdmitLaunchAsync(Process p,InstallationControlledLaunch proof,CancellationToken ct)
        {
            Proof=proof;
            Volatile.Write(ref startupPhase,"Admission");var original=client.AdmitLaunchAsync(p,proof,ct);Volatile.Write(ref clientAdmission,original);
            await original;Volatile.Write(ref startupPhase,"Admitted");
        }
        public async Task ConfirmHealthyAsync(Process p,InstalledVersion v,Guid launch,CancellationToken ct)
        {
            Volatile.Write(ref startupPhase,"Health");var original=client.ConfirmHealthyAsync(p,v,launch,ct);Volatile.Write(ref clientHealthReceipt,original);
            await original;Volatile.Write(ref startupPhase,"Healthy");Healthy.TrySetResult();if(HoldHealth)await HealthRelease.Task;
        }
        public Task RequestExitAsync(Process p,CancellationToken ct){ExitRequested.TrySetResult();return client.RequestExitAsync(p,ct);}
        public async Task SettleAsync(Process p,CancellationToken ct)
        {ObserveWarmExit(p);var original=client.SettleAsync(p,ct);OriginalIo.Add(original);await original;CleanupIoEntered.TrySetResult();if(HoldCleanupIo)await CleanupIoRelease.Task;Assert.True(p.HasExited);Volatile.Write(ref diagnosticExit,p.ExitCode);Volatile.Write(ref diagnosticExitKnown,1);CleanupExits.Add(p.ExitCode);output.WriteLine($"PUBLIC_SESSION_FAILURE_CLEANUP actualExit={p.ExitCode} originalIO=Settled driverSha256={Hash(Driver)} coreSha256={Hash(Path.Combine(Path.GetDirectoryName(Driver)!,"KuaiYi.Core.dll"))}");}
        public async Task SettleSessionAsync(Process p,CancellationToken ct)
        {
            ObserveWarmExit(p);Interlocked.Increment(ref SessionIoCalls);var original=client.SettleSessionAsync(p,ct);OriginalIo.Add(original);await original;IoEntered.TrySetResult();if(HoldIo)await IoRelease.Task;
            if(FailIo)throw new IOException("PUBLIC_IO_CLEANUP_FAILED");
            output.WriteLine($"PUBLIC_DIRECT_SESSION actualExit={p.ExitCode} originalIO=Settled driverSha256={Hash(Driver)} coreSha256={Hash(Path.Combine(Path.GetDirectoryName(Driver)!,"KuaiYi.Core.dll"))}");
        }
        internal async Task WaitForBrowserPublishedAsync(Task originalRun)
        {
            try
            {
                await Task.WhenAny(BrowserPublished.Task,originalRun).WaitAsync(Limit);
                if(originalRun.IsCompleted)await originalRun;
                await BrowserPublished.Task.WaitAsync(Limit);
            }
            catch{ReportStartupFailure(originalRun);throw;}
        }
        internal InstallationUpgradeCoordinator Coordinator(Rig r)=>new(r.App,r.Data,this,TimeSpan.FromSeconds(1),this){BrowserHostPublishedForTest=()=>BrowserPublished.TrySetResult()};
    }
    [Fact]public async Task SameProductionAppEntryOwnsTwoActualNormalSessionsAndRejectsDuplicateLaunches()
    {
        using var r=new Rig();var selected=await r.Prepare();byte[] pointer=File.ReadAllBytes(Path.Combine(r.App,"current.json"));var owners=new HashSet<string>();
        for(int i=0;i<2;i++)
        {
            await WithPeer(r,async peer=>
        {
            var parent=peer.OwnParent(Launch(r,selected));
                Assert.Equal("PUBLIC_SESSION_CHILD_STARTED",await parent.Row());Assert.Equal("PUBLIC_SESSION_HEALTHY",await parent.Row());
                Assert.True(owners.Add(File.ReadAllText(r.Owner)));r.Fence();Assert.False(parent.Process.HasExited);
                {var duplicate=peer.OwnParent(Launch(r,selected));Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await duplicate.Row());await duplicate.Finish(3);}
                Assert.Equal(i+1,File.ReadAllLines(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")).Length);
                await peer.Close();string receipt=(await parent.Row())!;Assert.StartsWith("PUBLIC_SESSION_CHILD_EXIT|0|originalIO=Settled|",receipt);output.WriteLine(receipt);
                Assert.Equal("PUBLIC_SESSION_COMPLETE|0",await parent.Row());await parent.Finish(0);
                Assert.False(File.Exists(r.Owner));using var admitted=r.Store.EnterRoleAdmission();
        });
}
        Assert.Equal(2,Directory.GetFiles(r.App,"execution-finished-*.json").Length);Assert.Equal(2,Directory.GetFiles(r.App,"launch-*-finished.json").Length);
        Assert.Equal(pointer,File.ReadAllBytes(Path.Combine(r.App,"current.json")));Assert.Equal("PUBLIC_OFF",File.ReadAllText(Path.Combine(r.Data,"PUBLIC_SENTINEL")));
        Assert.False(File.Exists(Path.Combine(r.App,"handoff-pending.json")));Assert.False(File.Exists(Path.Combine(r.App,"upgrade-pending.json")));
    }
    [Theory][InlineData("EXIT0")][InlineData("EXIT23")]
    public async Task ActualExitWithoutTheBoundCleanCloseReceiptRetainsTheFence(string command)
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var parent=peer.OwnParent(Launch(r,selected));
            Assert.Equal("PUBLIC_SESSION_CHILD_STARTED",await parent.Row());Assert.Equal("PUBLIC_SESSION_HEALTHY",await parent.Row());byte[] owner=File.ReadAllBytes(r.Owner);
            await peer.Send(command);Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await parent.Row());await parent.Finish(3);
            Assert.Equal(owner,File.ReadAllBytes(r.Owner));Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));r.Fence();
            var fresh=peer.OwnParent(Launch(r,selected));Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await fresh.Row());await fresh.Finish(3);
        });
    }
    [Fact]public async Task BoundCloseReceiptCannotReleaseOwnerWhileTheActualChildIsStillAlive()
    {
        using var r=new Rig();var selected=await r.Prepare("held-after-close");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);await peer.Close();
            Assert.False(port.Children.Single().HasExited);Assert.False(run.IsCompleted);r.Fence();
            await Assert.ThrowsAsync<InstallationBusyException>(async()=>await coordinator.DisposeAsync());
            await Assert.ThrowsAsync<InstallationBusyException>(()=>peer.Observe(()=>coordinator.StartActiveRoleAsync()));
            await peer.Send("RELEASE_EXIT");Assert.Equal(0,await run.WaitAsync(Limit));await coordinator.DisposeAsync();Assert.False(File.Exists(r.Owner));
        });
    }
    [Fact]public async Task FailedUserCloseKeepsTheSameSessionAndFenceUntilAnExplicitSuccessfulClose()
    {
        using var r=new Rig();var selected=await r.Prepare("fail-close-once");await WithPeer(r,async peer=>
        {
            var parent=peer.OwnParent(Launch(r,selected));
            Assert.Equal("PUBLIC_SESSION_CHILD_STARTED",await parent.Row());Assert.Equal("PUBLIC_SESSION_HEALTHY",await parent.Row());byte[] owner=File.ReadAllBytes(r.Owner);
            await peer.Send("CLOSE");Assert.Equal("PUBLIC_CLOSE_UNCONFIRMED|IOException",await peer.Row());Assert.False(parent.Process.HasExited);Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();
            {var duplicate=peer.OwnParent(Launch(r,selected));Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await duplicate.Row());await duplicate.Finish(3);}
            await peer.Close();string receipt=(await parent.Row())!;Assert.StartsWith("PUBLIC_SESSION_CHILD_EXIT|0|originalIO=Settled|",receipt);output.WriteLine(receipt);
            Assert.Equal("PUBLIC_SESSION_COMPLETE|0",await parent.Row());await parent.Finish(0);Assert.False(File.Exists(r.Owner));
        });
    }
    [Fact]public async Task CancellationOfAHealthySessionSettlesOnlyItsOwnChildAndNeverClearsItsOwner()
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));using var cancel=new CancellationTokenSource();
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath,cancel.Token));await port.Healthy.Task.WaitAsync(Limit);byte[] owner=File.ReadAllBytes(r.Owner);cancel.Cancel();
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
            Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();Assert.Single(port.Children);Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));
        });
    }
    [Fact]public async Task ACancelledWaiterCannotReleaseAnActualStartThatReturnsItsProcessLate()
    {
        using var r=new Rig();var selected=await r.Prepare();var port=new Port(output){HoldStart=true};var coordinator=port.Coordinator(r);using var cancel=new CancellationTokenSource();
        Task<int> run=coordinator.RunActiveSessionAsync(selected.DirectoryPath,cancel.Token);await port.StartHeld.Task.WaitAsync(Limit);byte[] owner=File.ReadAllBytes(r.Owner);cancel.Cancel();
        try{await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));Assert.False(port.Children.Single().HasExited);r.Fence();}
        finally{port.StartRelease.TrySetResult();}
        await port.Children.Single().WaitForExitAsync().WaitAsync(Limit);await coordinator.DisposeAsync();Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();
        Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));
    }
    [Fact]public async Task AnOriginalHealthTaskThatIgnoresCancellationRemainsOwnedUntilItReallySettles()
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output){HoldHealth=true});var coordinator=peer.Own(port.Coordinator(r));using var cancel=new CancellationTokenSource();
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath,cancel.Token));await port.Healthy.Task.WaitAsync(Limit);cancel.Cancel();
            try
            {
                await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));r.Fence();
                await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await coordinator.DisposeAsync());
            }
            finally{port.HealthRelease.TrySetResult();}
            await coordinator.DisposeAsync();r.Fence();Assert.True(File.Exists(r.Owner));
        });
    }
    [Fact]public async Task ActualExitDoesNotReleaseAnOriginalIoTaskThatOutlivesItsTimeoutOrLaterDispose()
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output){HoldIo=true});var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);await peer.Close();await port.IoEntered.Task.WaitAsync(Limit);
            Assert.True(port.Children.Single().HasExited);Assert.False(run.IsCompleted);byte[] owner=File.ReadAllBytes(r.Owner);r.Fence();
            try
            {
                await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));r.Fence();
                await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await coordinator.DisposeAsync());Assert.Equal(1,port.SessionIoCalls);
            }
            finally{port.IoRelease.TrySetResult();}
            await coordinator.DisposeAsync();Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();
        });
    }
    [Fact]public async Task CompletedButFaultedOriginalIoCannotBePrunedIntoSuccessfulOwnerRelease()
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output){FailIo=true});var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);await peer.Close();
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();r.Fence();Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));
        });
    }
    [Fact]
    public async Task EarlyOriginalSessionFailureCannotBeHiddenByAnUncompletedHealthySignal()
    {
        using var r=new Rig();var selected=await r.Prepare("held-ready");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));
            await FixtureBodyAndCleanup(async()=>
            {
                var observed=await Record.ExceptionAsync(()=>port.WaitForHealthyAsync(run));
                var original=await Record.ExceptionAsync(()=>run.WaitAsync(Limit));
                var failure=Assert.IsType<InstallationUpgradeUnconfirmedException>(observed);Assert.Same(original,failure);
                Assert.IsType<TimeoutException>(failure.InnerException);Assert.False(port.Healthy.Task.IsCompleted);Assert.Single(port.CleanupExits);
            },async()=>{await Record.ExceptionAsync(()=>run.WaitAsync(Limit));},()=>coordinator.DisposeAsync().AsTask());
        });
    }
    [Fact]
    public async Task DiagnosticOutputFailureCannotReplaceTheOriginalSessionFailure()
    {
        using var r=new Rig();var selected=await r.Prepare("held-ready");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(new FailingStartupOutput(output)));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));
            await FixtureBodyAndCleanup(async()=>
            {
                var observed=await Record.ExceptionAsync(()=>port.WaitForHealthyAsync(run));
                var original=await Record.ExceptionAsync(()=>run.WaitAsync(Limit));
                Assert.Same(original,observed);Assert.IsType<TimeoutException>(Assert.IsType<InstallationUpgradeUnconfirmedException>(observed).InnerException);
                Assert.Equal("PUBLIC_DIAGNOSTIC_OUTPUT_FAILED",Assert.IsType<InvalidOperationException>(port.DiagnosticFailure).Message);
                Assert.False(port.Healthy.Task.IsCompleted);Assert.Single(port.CleanupExits);
            },async()=>{await Record.ExceptionAsync(()=>run.WaitAsync(Limit));},()=>coordinator.DisposeAsync().AsTask());
        });
    }
    [Theory][InlineData("execution-owner.json")][InlineData("current.json")][InlineData("upgrade-pending.json")]
    public async Task ChangedExactRecordsAreRetainedEvenAfterCleanActualExit(string record)
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.WaitForHealthyAsync(run);await port.WaitForBrowserPublishedAsync(run);string path=Path.Combine(r.App,record);File.WriteAllText(path,"PUBLIC_UNKNOWN_CHANGED");
            await peer.Close();await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
            Assert.Equal("PUBLIC_UNKNOWN_CHANGED",File.ReadAllText(path));Assert.True(File.Exists(r.Owner));r.Fence();Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));
        });
    }
    [Theory][InlineData("execution-owner.json")][InlineData("upgrade-pending.json")][InlineData("handoff-pending.json")][InlineData("upgrade.lock")]
    public async Task UnknownAdmissionRecordIsNeverAdoptedOrReplaced(string record)
    {
        using var r=new Rig();var selected=await r.Prepare();string path=Path.Combine(r.App,record);File.WriteAllText(path,"PUBLIC_UNKNOWN_RETAIN");
        await WithParentBody(Launch(r,selected),async parent=>{Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await parent.Row());await parent.Finish(3);
        Assert.Equal("PUBLIC_UNKNOWN_RETAIN",File.ReadAllText(path));Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));});
    }
    [Fact]public async Task LostActualParentObserverCannotBeReplacedByAFreshLauncherOrAPrivateCloseReceipt()
    {
        using var r=new Rig();var selected=await r.Prepare();Exception? retirement=await Record.ExceptionAsync(()=>WithPeer(r,async peer=>
        {var parent=peer.OwnParent(Launch(r,selected));
        Assert.Equal("PUBLIC_SESSION_CHILD_STARTED",await parent.Row());Assert.Equal("PUBLIC_SESSION_HEALTHY",await parent.Row());byte[] owner=File.ReadAllBytes(r.Owner);
        // Kill only this test's actual Start-returned parent. Never open/adopt/terminate its child by a reported PID.
        r.Keep=true;parent.Process.Kill();await parent.Process.WaitForExitAsync().WaitAsync(Limit);
        Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row()); // Production EOF requests close; this is deliberately not an OS-exit assertion.
        Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();
        var fresh=peer.OwnParent(Launch(r,selected));Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await fresh.Row());await fresh.Finish(3);
        output.WriteLine("PUBLIC_OBSERVER_LOST parentActualExit=True childExit=Unobserved privateCloseReceiptOnly=True retainedPublicFixture="+r.Root);
        }));
        var unconfirmed=Assert.IsType<AggregateException>(retirement);
        Assert.All(unconfirmed.InnerExceptions,error=>Assert.IsType<FixtureRetirementUnconfirmedException>(error));
        Assert.True(r.Keep);
    }
    [Fact]public async Task NonSelectedGenerationCannotClaimAnOwnerAndUnmanagedLayoutRemainsSeparate()
    {
        using var r=new Rig();await r.Prepare();string other=Path.Combine(r.App,"versions",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(other);var port=new Port(output);var coordinator=port.Coordinator(r);
        await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>coordinator.RunActiveSessionAsync(other));await coordinator.DisposeAsync();Assert.Empty(port.Children);Assert.False(File.Exists(r.Owner));
        Assert.Null(InstallationNormalEntry.FindInstallationRoot(Path.Combine(r.Root,"development")));
        Assert.Throws<InstallationRecoveryException>(()=>InstallationNormalEntry.FindInstallationRoot(Path.Combine(r.App,"VERSIONS",Guid.NewGuid().ToString("N"))));
    }
    [Fact]public async Task ASessionCannotStealOrDisposeTheEarlierRoleHeldByTheSameCoordinator()
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            await peer.Observe(()=>coordinator.StartActiveRoleAsync());byte[] owner=File.ReadAllBytes(r.Owner);
            Exception? failure=await Record.ExceptionAsync(()=>peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath)));
            await FixtureBodyAndCleanup(()=>{Assert.False(port.Children.Single().HasExited);Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();Assert.Empty(port.CleanupExits);return Task.CompletedTask;},
                ()=>peer.Close(),()=>port.OriginalExits.Single().Exit.WaitAsync(Limit),()=>coordinator.DisposeAsync().AsTask());
            Assert.IsType<InvalidOperationException>(failure);Assert.Single(port.CleanupExits);Assert.False(File.Exists(r.Owner));using var admission=r.Store.EnterRoleAdmission();
        });
    }
    private static readonly List<Process> RetainedInstanceControls=[];
    [Fact]public async Task ComputingTheExistingShowScopeCannotAcquireTheUiMutexAheadOfTheActualChild()
    {
        string authority="PUBLIC_SESSION_SCOPE_"+Guid.NewGuid().ToString("N");string scope=SessionSingleInstance.ScopeFor(authority,17);
        var info=new ProcessStartInfo(Host){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        foreach(string arg in new[]{Driver,"--instance",authority,"17"})info.ArgumentList.Add(arg);
        Process p=Process.Start(info)!;Task<string> error=p.StandardError.ReadToEndAsync();Task originalExit=p.WaitForExitAsync();Task<string?> first=p.StandardOutput.ReadLineAsync();
        string? diagnostics=null;int? actualExit=null;bool ioSettled=false,disposed=false;
        await FixtureBodyAndCleanup(async()=>{Assert.Equal("OWNER",await first.WaitAsync(Limit));p.StandardInput.Close();await originalExit.WaitAsync(Limit);Assert.Equal(0,p.ExitCode);Assert.Equal("",await error);},
            ()=>{if(!p.HasExited)p.Kill();return Task.CompletedTask;},
            async()=>{await originalExit.WaitAsync(Limit);actualExit=p.ExitCode;},
            async()=>{await first.WaitAsync(Limit);},async()=>{diagnostics=await error.WaitAsync(Limit);},
            ()=>
            {
                ioSettled=first.IsCompleted&&error.IsCompleted;
                if(!originalExit.IsCompletedSuccessfully||!ioSettled)
                {lock(RetainedInstanceControls)RetainedInstanceControls.Add(p);throw new IOException("Original scope control process/IO is unconfirmed; retained.");}
                p.Dispose();disposed=true;return Task.CompletedTask;
            },
            ()=>{output.WriteLine("PUBLIC_INSTANCE_SCOPE_CONTROL "+JsonSerializer.Serialize(new{FirstLine=first.IsCompletedSuccessfully?first.Result:null,Stderr=diagnostics,ExitCode=actualExit,OriginalExit=originalExit.Status.ToString(),OriginalFirstLine=first.Status.ToString(),OriginalStderr=error.Status.ToString(),OriginalIoTasksSettled=ioSettled,ResourcesDisposed=disposed}));return Task.CompletedTask;});
        Assert.Equal(scope,SessionSingleInstance.ScopeFor(authority,17));
    }
    [Theory][InlineData("EXTRA_OUTPUT")][InlineData("WRONG_CLOSING")][InlineData("DUPLICATE_CLOSING")]
    public async Task UnexpectedOrRepeatedActualControlOutputCannotBecomeACleanSession(string command)
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var parent=peer.OwnParent(Launch(r,selected));
            Assert.Equal("PUBLIC_SESSION_CHILD_STARTED",await parent.Row());Assert.Equal("PUBLIC_SESSION_HEALTHY",await parent.Row());byte[] owner=File.ReadAllBytes(r.Owner);
            await peer.Send(command);
            if(command!="DUPLICATE_CLOSING"){Assert.Equal("PUBLIC_OUTPUT_WRITTEN",await peer.Row());await peer.Close();}
            Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await parent.Row());await parent.Finish(3);
            Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));
        });
    }
    [Fact]public async Task CancellationThatCompletesTheOriginalLoopBeforeTheReceiptDecisionAllowsTwoCleanActualSessions()
    {
        using var r=new Rig();var selected=await r.Prepare("cancel-loop-first");
        for(int i=0;i<2;i++)
        {
            await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
                Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);r.Fence();await peer.Close();
                Exception? failure=await Record.ExceptionAsync(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
                if(failure!=null)Assert.Equal(new[]{0},port.CleanupExits); // Red still proves the exact child actually exited0 and original IO joined.
                Assert.Null(failure);Assert.Equal(0,await run);Assert.False(File.Exists(r.Owner));using var admitted=r.Store.EnterRoleAdmission();
        });
}
        Assert.Equal(2,Directory.GetFiles(r.App,"execution-finished-*.json").Length);
    }
    [Fact]public async Task AProtocolFaultInTheOriginalLoopCannotReleaseOwnerAfterBoundClosingAndActualExitZero()
    {
        using var r=new Rig();var selected=await r.Prepare("fault-close-pending");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);byte[] owner=File.ReadAllBytes(r.Owner);
            await port.Children.Single().StandardInput.WriteLineAsync("PUBLIC_INVALID_COMMAND");await port.Children.Single().StandardInput.FlushAsync();Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());
            Exception? failure=await Record.ExceptionAsync(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
            Assert.IsType<InstallationUpgradeUnconfirmedException>(failure);Assert.Equal(new[]{0},port.CleanupExits);Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));
        });
    }
    [Fact]public async Task ALoopAlreadyCompletedByActualEofBeforeCloseNeverManufacturesACleanSession()
    {
        using var r=new Rig();var selected=await r.Prepare("ended-loop-first");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);byte[] owner=File.ReadAllBytes(r.Owner);port.Children.Single().StandardInput.Close();
            Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
            Assert.Equal(new[]{0},port.CleanupExits);Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();
        });
    }
    [Theory][InlineData("TERMINAL_MISSING")][InlineData("TERMINAL_WRONG")][InlineData("TERMINAL_DUPLICATE")][InlineData("TERMINAL_EARLY")]
    public async Task AClosingRequestWithoutOneCorrectFinalSettlementReceiptCannotReleaseOwner(string command)
    {
        using var r=new Rig();var selected=await r.Prepare("held-close");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.WaitForHealthyAsync(run);
            // READY precedes the original browser-host publication. Only issue this closing-protocol
            // fault after that publication has finished, so an exited child cannot preempt the IO assertion.
            await port.WaitForBrowserPublishedAsync(run);byte[] owner=File.ReadAllBytes(r.Owner);await peer.Send(command);
            Exception? failure=await Record.ExceptionAsync(()=>run.WaitAsync(Limit));Assert.IsType<InstallationUpgradeUnconfirmedException>(failure);
            if(command is "TERMINAL_EARLY" or "TERMINAL_DUPLICATE")
            {
                // JoinSession has observed both original streams before propagating this reader fault. A failed cleanup still refuses Dispose.
                Assert.IsType<InvalidDataException>(failure!.GetBaseException());await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await coordinator.DisposeAsync());
                Assert.True(port.Children.Single().HasExited);Assert.Equal(0,port.Children.Single().ExitCode);port.Children.Single().Dispose();r.Keep=true;
                output.WriteLine($"PUBLIC_SESSION_IO_FAULT actualExit=0 originalIO=FaultObserved cleanup=Unconfirmed driverSha256={Hash(Driver)} coreSha256={Hash(Path.Combine(Path.GetDirectoryName(Driver)!,"KuaiYi.Core.dll"))} retainedPublicFixture={r.Root}");
            }
            else{await coordinator.DisposeAsync();Assert.Equal(new[]{0},port.CleanupExits);}
            Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();
        });
    }
    [Fact]public async Task TerminalDuplicateBeforeOriginalBrowserPublicationPreservesBothExitAndReaderFaults()
    {
        using var r=new Rig();var selected=await r.Prepare("held-close");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output){HoldHealth=true});var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));
            await FixtureBodyAndCleanup(async()=>
            {
                await port.WaitForHealthyAsync(run);Assert.False(port.BrowserPublished.Task.IsCompleted);
                await peer.Send("TERMINAL_DUPLICATE");await port.OriginalExits.Single().Exit.WaitAsync(Limit);
                Assert.True(port.Children.Single().HasExited);Assert.Equal(0,port.Children.Single().ExitCode);
            },()=>{port.HealthRelease.TrySetResult();return Task.CompletedTask;});
            Exception? failure=await Record.ExceptionAsync(()=>run.WaitAsync(Limit));
            Assert.IsType<InstallationUpgradeUnconfirmedException>(failure);
            Assert.IsType<IOException>(failure!.GetBaseException());
            Assert.Equal("Selected application exited before health confirmation.",failure.GetBaseException().Message);
            Assert.False(port.BrowserPublished.Task.IsCompleted);
            Exception? reader=await Record.ExceptionAsync(()=>port.SettleAsync(port.Children.Single(),CancellationToken.None));
            Assert.IsType<InvalidDataException>(reader);
            Assert.Contains("duplicate application settlement",reader!.Message);
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await coordinator.DisposeAsync());
            Assert.True(File.Exists(r.Owner));r.Fence();Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));r.Keep=true;
            output.WriteLine("PUBLIC_PREPUBLICATION_TERMINAL actualExit=0 browserPublished=False primary=IOException originalReader=InvalidDataException ownerRetained=True");
        });
    }
    [Fact]public async Task AFinalOwnerArchiveCollisionRetainsExactOwnerAndFilesAfterActualExitAndIoSettlement()
    {
        using var r=new Rig();var selected=await r.Prepare();await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);byte[] owner=File.ReadAllBytes(r.Owner),pointer=File.ReadAllBytes(Path.Combine(r.App,"current.json"));
            using var record=JsonDocument.Parse(owner);string id=record.RootElement.GetProperty("owner").GetString()!;string collision=Path.Combine(r.App,"execution-finished-"+id+".json");File.WriteAllText(collision,"PUBLIC_ARCHIVE_COLLISION_RETAIN");
            await peer.Close();await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
            Assert.Equal(1,port.SessionIoCalls);Assert.Equal("PUBLIC_ARCHIVE_COLLISION_RETAIN",File.ReadAllText(collision));Assert.Equal(owner,File.ReadAllBytes(r.Owner));Assert.Equal(pointer,File.ReadAllBytes(Path.Combine(r.App,"current.json")));r.Fence();
            var fresh=peer.OwnParent(Launch(r,selected));Assert.StartsWith("PUBLIC_SESSION_REFUSED|",await fresh.Row());await fresh.Finish(3);Assert.Single(Directory.GetFiles(r.App,"launch-*-finished.json"));
        });
    }
    [Fact]public async Task UnsolicitedActualEofOverlappingCloseDispatchCannotAttestAHealthySession()
    {
        using var r=new Rig();var selected=await r.Prepare("eof-close-pending");await WithPeer(r,async peer=>
        {
            var port=peer.Own(new Port(output));var coordinator=peer.Own(port.Coordinator(r));
            Task<int> run=peer.Observe(()=>coordinator.RunActiveSessionAsync(selected.DirectoryPath));await port.Healthy.Task.WaitAsync(Limit);byte[] owner=File.ReadAllBytes(r.Owner);port.Children.Single().StandardInput.Close();
            Assert.Equal("PUBLIC_SHELL_CLOSED",await peer.Row());await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));await coordinator.DisposeAsync();
            Assert.Equal(new[]{0},port.CleanupExits);Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();
        });
    }
}
