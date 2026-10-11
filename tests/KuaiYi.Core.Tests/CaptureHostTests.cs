using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class CaptureHostTests
{
    private sealed class SilentClock:TimeProvider
    {
        private long timestamp;
        public override long TimestampFrequency=>1000;
        public override long GetTimestamp()=>Interlocked.Read(ref timestamp);
        public void Advance()=>Interlocked.Add(ref timestamp,5001);
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)=>new SilentTimer();
        private sealed class SilentTimer:ITimer{public bool Change(TimeSpan due,TimeSpan period)=>true;public void Dispose(){}public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
    private sealed class Starter(string mode="normal"):IImageProcessStarter,IDisposable
    {
        public ManualResetEventSlim? Barrier {get;init;}
        public bool Fail {get;init;}
        public TaskCompletionSource<int> Entered {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Process> Created {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Process Start(ProcessStartInfo info)
        {
            Entered.TrySetResult(Environment.CurrentManagedThreadId);
            if(Barrier!=null&&!Barrier.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("public capture start barrier");
            if(Fail)throw new IOException("public capture start failure");
            info.Environment["KUAIYI_CAPTURE_FIXTURE"]=mode;var child=SystemImageProcessStarter.Instance.Start(info);Created.TrySetResult(child);return child;
        }
        public void Dispose(){Barrier?.Set();Barrier?.Dispose();}
    }
    private sealed class FailedKill(Process child,bool waitFailure=false):IOwnedHostLifetime
    {
        public bool HasExited=>child.HasExited;
        public void RequestTermination(){if(!waitFailure)throw new IOException("synthetic kill failure on held real child");}
        public Task WaitForExitAsync(CancellationToken cancellation)=>Task.Delay(Timeout.Infinite,cancellation);
    }
    private sealed class UncancellableRead(Stream inner):Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)=>inner.ReadAsync(buffer,CancellationToken.None);
        public override int Read(byte[] buffer,int offset,int count)=>inner.Read(buffer,offset,count);
        public override void Flush()=>throw new NotSupportedException();public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();public override void SetLength(long value)=>throw new NotSupportedException();public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
    private static string AssemblyPath()
    {
        string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.CaptureControlFixture/bin",configuration,"net10.0/KuaiYi.CaptureControlFixture.dll"));
    }
    private static string Dotnet=>Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet";
    private static CaptureJob Job()=>new(Guid.NewGuid(),1,Environment.ProcessId,[1],CaptureBackend.WindowsGraphicsCapture);
    private static CaptureHostClient Client(Starter starter,TimeSpan? budget=null)=>new(Dotnet,AssemblyPath(),starter,budget);
    private static async Task<Process> Ready(Starter starter,string mode)
    {
        var child=await starter.Created.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.Equal("entered:"+mode,await child.StandardError.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(4)));return child;
    }
    private static async Task Cleanup(CaptureHostClient client,Starter starter)
    {
        starter.Barrier?.Set();
        try{await client.StartupSettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));}catch(IOException){/* Asserted startup failure is still observed. */}
        if(client.ObserveExit()){Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();return;} // Production finalization may already have disposed the confirmed Process.
        if(starter.Created.Task.IsCompletedSuccessfully)
        {
            var held=await starter.Created.Task;if(!held.HasExited){held.Kill();await held.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));}
        }
        Assert.True(await client.TryFinalizeAsync());await client.DisposeAsync();
    }
    [Fact] public async Task RealPrivateSessionPreservesPhysicalPixelsDpiAndClearsActualArrays()
    {
        using var starter=new Starter();var client=Client(starter);
        try
        {
            using var snapshot=await client.RunAsync(Job()).WaitAsync(TimeSpan.FromSeconds(6));
            Assert.True(client.ObserveExit());Assert.Equal(HostExitState.ExitConfirmed,client.ExitState);
            Assert.Equal(new PixelRect(-8,0,8,8),snapshot.Topology.Monitors.Single().Bounds);Assert.Equal(1.75,snapshot.Topology.Monitors.Single().Dpi.Factor);
            Assert.True(MemoryMarshal.TryGetArray(snapshot.Tiles.Single().Bytes,out var segment));Assert.Equal(512,segment.Count);
            Assert.Equal(Enumerable.Range(0,512).Select(i=>(byte)(i%251+1)),segment.Array!);snapshot.Dispose();Assert.All(segment.Array!,b=>Assert.Equal(0,b));
        }finally{await Cleanup(client,starter);}
    }
    [Theory][InlineData("stall-compositor",false)][InlineData("stall-native",false)][InlineData("stall-compositor",true)][InlineData("stall-native",true)]
    public async Task RealChildIgnoringCancellationIsKilledAndUiFinallyCanRestore(string mode,bool timeout)
    {
        using var starter=new Starter(mode);var client=Client(starter);using var cancel=new CancellationTokenSource();bool restored=false;int accepted=0;
        async Task UiFlow(){try{using var pixels=await client.RunAsync(Job(),cancel.Token);accepted++;}finally{restored=true;}}
        var watch=Stopwatch.StartNew();var run=UiFlow();
        try
        {
            var child=await Ready(starter,mode);if(!timeout)cancel.Cancel();
            var failure=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(8)));
            if(timeout)Assert.Equal(CaptureFailure.Timeout,Assert.IsType<CaptureException>(failure).Failure);else Assert.IsAssignableFrom<OperationCanceledException>(failure);
            Assert.True(restored);Assert.Equal(0,accepted);Assert.True(child.HasExited);Assert.True(client.ObserveExit());Assert.Equal(HostExitState.ExitConfirmed,client.ExitState);Assert.True(watch.Elapsed<TimeSpan.FromSeconds(8));
        }finally{await Cleanup(client,starter);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task KillFailureOrUnconfirmedWaitRetainsOwnerAndBlocksFinalizeWithoutBlockingUi(bool waitFailure)
    {
        using var starter=new Starter("stall-native");var client=new CaptureHostClient(Dotnet,AssemblyPath(),starter){LifetimeForTest=p=>new FailedKill(p,waitFailure)};
        using var cancel=new CancellationTokenSource();var run=client.RunAsync(Job(),cancel.Token);
        try
        {
            var child=await Ready(starter,"stall-native");cancel.Cancel();
            Assert.Equal(CaptureFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<CaptureException>(()=>run.WaitAsync(TimeSpan.FromSeconds(4)))).Failure);
            Assert.False(child.HasExited);Assert.False(client.ObserveExit());Assert.False(await client.TryFinalizeAsync());Assert.False(await client.TryFinalizeAsync(true));
            Assert.Equal(CaptureFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<CaptureException>(()=>client.DisposeAsync().AsTask())).Failure);
            Assert.Equal(waitFailure?HostExitState.ExitUnconfirmed:HostExitState.TerminationFailed,client.ExitState);
        }finally{await Cleanup(client,starter);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancelDuringStartDoesNotAbandonLateChildOrInventExit(bool fail)
    {
        using var starter=new Starter{Barrier=new(false),Fail=fail};var client=Client(starter);using var cancel=new CancellationTokenSource();var run=client.RunAsync(Job(),cancel.Token);
        try
        {
            await starter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));cancel.Cancel();
            Assert.Equal(CaptureFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<CaptureException>(()=>run.WaitAsync(TimeSpan.FromSeconds(2)))).Failure);
            Assert.False(client.ObserveExit());Assert.False(await client.TryFinalizeAsync());starter.Barrier.Set();
            try{await client.StartupSettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));}catch(IOException){Assert.True(fail);}
            if(fail){Assert.False(starter.Created.Task.IsCompleted);Assert.Equal("IOException",client.StartupFailureCode);}
            else
            {
                var held=await starter.Created.Task;await held.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));Assert.True(held.HasExited);
            }
            Assert.True(await client.TryFinalizeAsync());
        }finally{await Cleanup(client,starter);}
    }
    [Fact] public async Task PublishedAlreadyExitedRealChildCannotFinalizeBeforeStartSettles()
    {
        using var starter=new Starter("exit");using var barrier=new ManualResetEventSlim(false);var entered=new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client=new CaptureHostClient(Dotnet,AssemblyPath(),starter){OwnershipPublishedForTest=p=>{entered.SetResult(p);if(!barrier.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("public handoff");}};
        using var cancel=new CancellationTokenSource();var run=client.RunAsync(Job(),cancel.Token);
        try
        {
            var held=await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));await held.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));Assert.True(held.HasExited);
            Assert.False(client.ObserveExit());Assert.False(await client.TryFinalizeAsync());cancel.Cancel();Assert.Equal(CaptureFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<CaptureException>(()=>run.WaitAsync(TimeSpan.FromSeconds(2)))).Failure);
        }finally{barrier.Set();await Cleanup(client,starter);}
    }
    [Fact] public async Task UncancellableRealPipeReadAfterOffClearsLatePayloadAndCannotCommit()
    {
        using var starter=new Starter("late-frame");var cleared=new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var client=new CaptureHostClient(Dotnet,AssemblyPath(),starter){LifetimeForTest=p=>new FailedKill(p),ReadStreamForTest=s=>new UncancellableRead(s),LateReadClearedForTest=b=>cleared.TrySetResult(b)};
        var gate=new RuntimeGate();using var intent=new ScreenshotIntent(gate);using var cancel=new CancellationTokenSource();
        var run=ScreenshotAcquisition.AcquireAsync(intent,t=>client.RunAsync(Job(),t),cancel.Token);
        try
        {
            await Ready(starter,"late-frame");gate.Disable();cancel.Cancel();
            Assert.Equal(CaptureFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<CaptureException>(()=>run.WaitAsync(TimeSpan.FromSeconds(2)))).Failure);
            Assert.False(client.ObserveExit());var late=await cleared.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.NotEmpty(late);Assert.All(late,b=>Assert.Equal(0,b));
            Assert.False(intent.IsCurrent);Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.False(intent.TryCommit(true,Guid.NewGuid(),Guid.NewGuid(),out _,out _));
        }finally{await Cleanup(client,starter);}
    }
    [Theory][InlineData("operation")][InlineData("epoch")][InlineData("role")][InlineData("oversize")][InlineData("hash")][InlineData("chunk")][InlineData("commit")]
    public async Task ActualPrivateChannelRejectsWrongIdentityRoleBoundsAndIncompleteFrames(string fault)
    {
        using var starter=new Starter("bad-"+fault);var client=Client(starter);
        try
        {
            var failure=await Record.ExceptionAsync(()=>client.RunAsync(Job()).WaitAsync(TimeSpan.FromSeconds(6)));
            if(fault=="oversize")Assert.Equal(ScreenshotFailure.SnapshotTooLarge,Assert.IsType<ScreenshotException>(failure).Code);else Assert.Equal(CaptureFailure.ProtocolFailure,Assert.IsType<CaptureException>(failure).Failure);
            Assert.True(client.ObserveExit());Assert.True((await starter.Created.Task).HasExited);
        }finally{await Cleanup(client,starter);}
    }
    [Fact] public async Task HiddenVerificationFailureHasNoPixelPayloadAndChildExitIsConfirmed()
    {
        using var starter=new Starter("hidden-failed");var client=Client(starter);
        try{Assert.Equal(CaptureFailure.HiddenWindowNotVerified,(await Assert.ThrowsAsync<CaptureException>(()=>client.RunAsync(Job()))).Failure);Assert.True(client.ObserveExit());}
        finally{await Cleanup(client,starter);}
    }
    [Fact] public async Task PreCancelledCaptureCreatesNoProcess()
    {
        using var starter=new Starter();await using var client=Client(starter);using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>client.RunAsync(Job(),cancel.Token));Assert.False(starter.Entered.Task.IsCompleted);Assert.True(client.ObserveExit());
    }
    [Fact] public async Task CompletedStartupBeyondMonotonicBudgetIsRejectedEvenWithoutTimerCallback()
    {
        using var starter=new Starter();var clock=new SilentClock();var client=new CaptureHostClient(Dotnet,AssemblyPath(),starter,timeProvider:clock){OwnershipPublishedForTest=_=>clock.Advance()};
        try{Assert.Equal(CaptureFailure.Timeout,(await Assert.ThrowsAsync<CaptureException>(()=>client.RunAsync(Job()))).Failure);Assert.True(client.ObserveExit());Assert.True((await starter.Created.Task).HasExited);}
        finally{await Cleanup(client,starter);}
    }
    [Fact] public void UnconfirmedCurrentCaptureRevokesOldNetworkIdentityAndCannotSendAfterRelease()
    {
        var gate=new RuntimeGate();var origin=new EndpointOrigin(new Uri("https://public.example.test"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var prior,out var cancellation));
        using var intent=new ScreenshotIntent(gate);Assert.True(intent.FailUnconfirmed());Assert.True(cancellation.IsCancellationRequested);intent.Dispose();Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.False(gate.CanSend(prior,origin));Assert.False(gate.TryBeginPassiveCapture(TriggerMode.Selection,out _));
    }
    [Fact] public void StaleCaptureFailureCannotDisableReplacementIdentity()
    {
        var gate=new RuntimeGate();Assert.True(gate.TryEnableLocalOperation(true));using var old=new ScreenshotIntent(gate);Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var replacement,out _));
        Assert.False(old.FailUnconfirmed());Assert.Equal(Activation.On,gate.Snapshot.Activation);Assert.True(gate.CanApply(replacement));
    }
}
