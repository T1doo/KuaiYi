using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;
using KuaiYi.Core.Installation;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;

public sealed partial class InstallationStableLauncherTests(ITestOutputHelper output)
{
    private static readonly TimeSpan Limit=TimeSpan.FromSeconds(12);
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string Repo=>MaterializedPayloadSource.Repo;
    private static string Driver=>Path.Combine(Repo,"tools/KuaiYi.SettingsStoreFixture/bin/Release/net10.0/KuaiYi.SettingsStoreFixture.dll");
    private sealed class Rig:IDisposable
    {
        internal readonly string Root=Path.Combine(Path.GetTempPath(),"kuaiyi-upgrade-tests-"+Guid.NewGuid().ToString("N"));
        internal readonly string Pipe="ky-stable-"+Guid.NewGuid().ToString("N");
        internal bool Keep;
        internal string App=>Path.Combine(Root,"application");internal string Data=>Path.Combine(Root,"user-data");
        internal string Owner=>Path.Combine(App,"execution-owner.json");
        internal Rig(){Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"PUBLIC_SENTINEL"),"PUBLIC_OFF");}
        internal InstallationStore Store=>new(App,Data,()=>true);
        internal async Task<InstalledVersion> Prepare(string mode="healthy",string version="1.0")
        {
            var source=new MaterializedPayloadSource(Path.Combine(Root,"source-"+version),version);
            foreach(string name in new[]{"KuaiYi.SettingsStoreFixture.dll","KuaiYi.SettingsStoreFixture.deps.json","KuaiYi.SettingsStoreFixture.runtimeconfig.json"})source.Put(name,File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(Driver)!,name)));
            source.Put("public-install-behavior.json",JsonSerializer.SerializeToUtf8Bytes(new{version,mode,runtimePipe=Pipe,controlled=true}));source.Catalog();return await Store.ApplyAsync(source.Root,source.Digest);
        }
        internal void Fence()=>Assert.ThrowsAny<IOException>(()=>Store.EnterRoleAdmission());
        public void Dispose(){Assert.Equal("PUBLIC_OFF",File.ReadAllText(Path.Combine(Data,"PUBLIC_SENTINEL")));if(File.Exists(Owner))Keep=true;if(!Keep&&Directory.Exists(Root))Directory.Delete(Root,true);}
    }
    private sealed class Peer:IAsyncDisposable
    {
        private readonly NamedPipeServerStream pipe;private StreamReader? reader;private StreamWriter? writer;private readonly Task connected;
        internal Peer(string name){pipe=new(name,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);connected=pipe.WaitForConnectionAsync();}
        internal async Task Send(string row){await connected.WaitAsync(Limit);writer??=new(pipe);await writer.WriteLineAsync(row);await writer.FlushAsync();}
        internal async Task<string?> Row(){await connected.WaitAsync(Limit);reader??=new(pipe);return await reader.ReadLineAsync().WaitAsync(Limit);}
        internal async Task Close(){await Send("CLOSE");Assert.Equal("PUBLIC_SHELL_CLOSED",await Row());}
        public ValueTask DisposeAsync(){List<Exception> errors=[];foreach(IDisposable item in new IDisposable?[]{writer,reader,pipe}.OfType<IDisposable>())try{item.Dispose();}catch(Exception error){errors.Add(error);}InstallationControllerBridge.Throw(errors);return ValueTask.CompletedTask;}
    }
    private sealed class Starter:IOwnedProcessStarter
    {
        internal bool HoldBeforeStart,HoldAfterStart;internal string? Fault;
        internal readonly TaskCompletionSource Held=Signal(),Release=Signal();internal readonly List<Process> Processes=[];
        public Process Start(ProcessStartInfo info)
        {
            Assert.Equal("KuaiYi.App.exe",Path.GetFileName(info.FileName));Assert.Equal(Path.GetDirectoryName(info.FileName),info.WorkingDirectory);Assert.Equal("--installation-controller",info.ArgumentList[0]);Assert.Equal(2,info.ArgumentList.Count);Assert.False(info.UseShellExecute);
            if(HoldBeforeStart){Held.TrySetResult();Release.Task.GetAwaiter().GetResult();}
            string directory=info.WorkingDirectory;info.FileName=Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet";
            if(Fault!=null){info.ArgumentList.Clear();info.ArgumentList.Add("--stable-controller-fault");info.ArgumentList.Add(Fault);}
            info.ArgumentList.Insert(0,Path.Combine(directory,"KuaiYi.SettingsStoreFixture.dll"));Process child=Process.Start(info)??throw new IOException("Fixture actual Start failed.");Processes.Add(child);
            if(HoldAfterStart){Held.TrySetResult();Release.Task.GetAwaiter().GetResult();}return child;
        }
    }
    private static async Task Finish(Rig rig,InstallationStableLauncher launcher,Task<int> original,Func<Task> body,Action? release=null)
    {
        List<Exception> errors=[];try{await body();}catch(Exception error){errors.Add(error);}
        try{release?.Invoke();}catch(Exception error){errors.Add(error);}
        try{await launcher.DisposeAsync();}catch(Exception error){errors.Add(error);}
        try{await original.WaitAsync(Limit);}catch(Exception error){_ =error;}
        if(!launcher.CleanupConfirmed)rig.Keep=true;
        _=launcher.Ready.Exception;
        InstallationControllerBridge.Throw(errors);
    }
    [Fact]public async Task StableCurrentLaunchRunsTwoRealControllerSessionsAndPublishesOnlyTheOriginalReady()
    {
        using var r=new Rig();var selected=await r.Prepare();
        for(int i=0;i<2;i++)
        {
            await using var peer=new Peer(r.Pipe);var starter=new Starter();var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(4),starter);Task<int> run=launcher.RunAsync();
            await Finish(r,launcher,run,async()=>{
                var ready=await launcher.Ready.WaitAsync(Limit);Assert.Equal(selected,ready.Version);Assert.NotEqual(Guid.Empty,ready.UiLaunch);Assert.Equal(64,ready.UiProof.Length);
                Assert.True(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));Assert.True(File.Exists(r.Owner));r.Fence();Assert.Single(starter.Processes);
                await peer.Send("PING");Assert.Equal("PUBLIC_SHELL|Created",await peer.Row());await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));
                Assert.True(launcher.CleanupConfirmed);Assert.Equal(0,launcher.ObservedControllerExitCode);Assert.False(File.Exists(r.Owner));Assert.False(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));
            });
        }
        Assert.Equal(2,Directory.GetFiles(r.App,"execution-finished-*.json").Length);output.WriteLine("PUBLIC_STABLE two_actual_controller_sessions=true UI_transport=Synthetic original_Control_READY_and_Host_publish=true WindowsExecution=NotRun");
    }
    [Fact]public async Task StableReadyCannotPrecedeTheOriginalUiHealthAndHostPublication()
    {
        using var r=new Rig();await r.Prepare("held-ready");await using var peer=new Peer(r.Pipe);var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(4),new Starter());Task<int> run=launcher.RunAsync();
        await Finish(r,launcher,run,async()=>{
            await peer.Send("PING");Assert.Equal("PUBLIC_SHELL|Created",await peer.Row());Assert.False(launcher.Ready.IsCompleted);Assert.False(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));
            await peer.Send("READY");await launcher.Ready.WaitAsync(Limit);Assert.True(File.Exists(InstallationBrowserHostSession.PathFor(r.App)));await peer.Close();Assert.Equal(0,await run.WaitAsync(Limit));
        });
    }
    [Theory][InlineData("owner.txt")][InlineData("current.json")][InlineData("upgrade-pending.json")][InlineData("execution-owner.json")][InlineData("host-registration-pending.json")]
    public async Task UnknownExistingStateNeverStartsOrRewritesAController(string record)
    {
        using var r=new Rig();await r.Prepare();string path=Path.Combine(r.App,record);File.WriteAllText(path,"PUBLIC_UNKNOWN_RETAIN");var starter=new Starter();var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(1),starter);Task<int> run=launcher.RunAsync();
        await Finish(r,launcher,run,async()=>{Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(Limit)));Assert.Empty(starter.Processes);Assert.Equal("PUBLIC_UNKNOWN_RETAIN",File.ReadAllText(path));Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));});
    }
    [Fact]public async Task MissingStoreCannotBeInitializedByTheStableEntry()
    {
        using var r=new Rig();var starter=new Starter();var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(1),starter);Task<int> run=launcher.RunAsync();
        await Finish(r,launcher,run,async()=>{Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(Limit)));Assert.False(Directory.Exists(r.App));Assert.Empty(starter.Processes);});
    }
    [Fact]public async Task AChangedCurrentBetweenSelectionAndActualStartIsRejectedByTheOriginalAdmission()
    {
        using var r=new Rig();await r.Prepare();var starter=new Starter{HoldBeforeStart=true};var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(3),starter);Task<int> run=launcher.RunAsync();
        await Finish(r,launcher,run,async()=>{
            await starter.Held.Task.WaitAsync(Limit);var next=await r.Prepare(version:"2.0");byte[] pointer=File.ReadAllBytes(Path.Combine(r.App,"current.json"));starter.Release.TrySetResult();
            Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(Limit)));Assert.False(launcher.Ready.IsCompletedSuccessfully);Assert.False(File.Exists(r.Owner));Assert.Equal(next,r.Store.ReadActive());Assert.Equal(pointer,File.ReadAllBytes(Path.Combine(r.App,"current.json")));Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));
        },()=>starter.Release.TrySetResult());
    }
    [Fact]public async Task CancellationUsesOriginalControllerEofAndRetainsItsExecutionFence()
    {
        using var r=new Rig();await r.Prepare();await using var peer=new Peer(r.Pipe);using var cancel=new CancellationTokenSource();var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(4),new Starter());Task<int> run=launcher.RunAsync(cancel.Token);
        await Finish(r,launcher,run,async()=>{
            await launcher.Ready.WaitAsync(Limit);byte[] owner=File.ReadAllBytes(r.Owner);cancel.Cancel();/* Original cancellation settles the held child; it does not promise a graceful Shell receipt. */Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(Limit)));
            Assert.True(launcher.CleanupConfirmed);Assert.Equal(3,launcher.ObservedControllerExitCode);Assert.Equal(owner,File.ReadAllBytes(r.Owner));r.Fence();Assert.Empty(Directory.GetFiles(r.App,"execution-finished-*.json"));
        });
    }
    [Fact]public async Task LateActualStartCannotBeAbandonedOrReceiveASelectionAfterCancellation()
    {
        using var r=new Rig();await r.Prepare();var starter=new Starter{HoldAfterStart=true};using var cancel=new CancellationTokenSource();var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(1),starter);Task<int> run=launcher.RunAsync(cancel.Token);
        await Finish(r,launcher,run,async()=>{
            await starter.Held.Task.WaitAsync(Limit);cancel.Cancel();await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));Assert.False(launcher.CleanupConfirmed);Assert.False(launcher.Ready.IsCompletedSuccessfully);
            starter.Release.TrySetResult();await Record.ExceptionAsync(async()=>await launcher.DisposeAsync());Assert.True(launcher.CleanupConfirmed);Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));Assert.False(File.Exists(r.Owner));
        },()=>starter.Release.TrySetResult());
    }
    [Theory][InlineData("show")][InlineData("exit-zero")][InlineData("wrong-ready")]
    public async Task ShowOrActualExitZeroCannotReplaceTheBoundUiReady(string fault)
    {
        using var r=new Rig();await r.Prepare();var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(2),new Starter{Fault=fault});Task<int> run=launcher.RunAsync();
        await Finish(r,launcher,run,async()=>{Assert.NotNull(await Record.ExceptionAsync(()=>run.WaitAsync(Limit)));Assert.False(launcher.Ready.IsCompletedSuccessfully);Assert.True(launcher.CleanupConfirmed);Assert.False(File.Exists(r.Owner));Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));});
    }
    private sealed class HeldWrite(Stream inner):Stream
    {
        internal readonly TaskCompletionSource Entered=Signal(),Release=Signal();
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes,CancellationToken cancellation=default){Entered.TrySetResult();await Release.Task;await inner.WriteAsync(bytes,cancellation);}
        public override Task FlushAsync(CancellationToken cancellation)=>inner.FlushAsync(cancellation);
        public override void Flush()=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    [Fact]public async Task CancellationNeverWaitsForWriteBackpressureOrForgetsTheOriginalWrite()
    {
        using var r=new Rig();await r.Prepare();using var cancel=new CancellationTokenSource();HeldWrite? held=null;var writeCreated=Signal();
        var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(1),new Starter()){WriteStreamForTest=stream=>{held=new(stream);writeCreated.TrySetResult();return held;}};Task<int> run=launcher.RunAsync(cancel.Token);
        await Finish(r,launcher,run,async()=>{
            // The wrapper is published by the actual first write; use a completion source rather than a timing delay.
            await writeCreated.Task.WaitAsync(Limit);await held!.Entered.Task.WaitAsync(Limit);cancel.Cancel();
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(()=>run.WaitAsync(Limit));Assert.False(launcher.CleanupConfirmed);
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await launcher.DisposeAsync());Assert.False(held.Release.Task.IsCompleted);
            held.Release.TrySetResult();await Record.ExceptionAsync(async()=>await launcher.DisposeAsync());Assert.True(launcher.CleanupConfirmed);
            Assert.False(launcher.Ready.IsCompletedSuccessfully);Assert.False(File.Exists(r.Owner));Assert.False(File.Exists(Path.Combine(r.Root,"PUBLIC_SHELL_CREATED")));
        },()=>held?.Release.TrySetResult());
    }

    [Fact]public async Task DisposeCannotFinalizeBeforeTheSynchronousWriteOwnerIsPublished()
    {
        using var r=new Rig();await r.Prepare();using var cancel=new CancellationTokenSource();var entered=Signal();var release=Signal();var starter=new Starter();
        var launcher=new InstallationStableLauncher(r.App,r.Data,TimeSpan.FromSeconds(1),starter){WriteStreamForTest=stream=>{entered.TrySetResult();release.Task.GetAwaiter().GetResult();return stream;}};Task<int> run=launcher.RunAsync(cancel.Token);
        await Finish(r,launcher,run,async()=>{
            await entered.Task.WaitAsync(Limit);cancel.Cancel();await starter.Processes.Single().WaitForExitAsync().WaitAsync(Limit);
            await Assert.ThrowsAsync<InstallationUpgradeUnconfirmedException>(async()=>await launcher.DisposeAsync());Assert.False(launcher.CleanupConfirmed);
        },()=>release.TrySetResult());
    }

}
