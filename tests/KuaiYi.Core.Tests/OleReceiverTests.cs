using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OleReceiverTests
{
    private sealed class Starter(string mode="normal"):IOwnedProcessStarter,IDisposable
    {
        private readonly string statusFile=Path.Combine(Path.GetTempPath(),"kuaiyi-public-status-"+Guid.NewGuid().ToString("N"));private int statusLine;
        public async Task<string> ReadStatusAsync()
        {
            var limit=Stopwatch.StartNew();
            while(limit.Elapsed<TimeSpan.FromSeconds(3))
            {
                if(File.Exists(statusFile)){using var file=new FileStream(statusFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);using var reader=new StreamReader(file);string[] complete=(await reader.ReadToEndAsync()).Split('\n');if(statusLine<complete.Length-1)return complete[statusLine++].TrimEnd('\r');}
                await Task.Delay(10);
            }
            throw new TimeoutException("Public fixture status deadline.");
        }
        public ManualResetEventSlim? Barrier{get;init;}
        public bool Fail{get;init;}
        public TaskCompletionSource<int> Entered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Process> Created{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Process Start(ProcessStartInfo info)
        {
            Entered.TrySetResult(Environment.CurrentManagedThreadId);
            if(Barrier!=null&&!Barrier.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("public OLE startup barrier");
            if(Fail)throw new IOException("public OLE startup failure");
            info.ArgumentList.Add(mode);info.ArgumentList.Add("--test-status");info.ArgumentList.Add(statusFile);var child=SystemOwnedProcessStarter.Instance.Start(info);Created.TrySetResult(child);return child;
        }
        public void Dispose(){Barrier?.Set();Barrier?.Dispose();if(File.Exists(statusFile))File.Delete(statusFile);}
    }
    // Explicit test substitutes: no HWND/COM/Windows mapping is exercised by these portable tests.
    private sealed class Activation(Guid instance):IOleActivationLease
    {
        public string Name{get;}=OleReceiverWire.ActivationName(instance);public bool Enabled,Disposed;public long Epoch;
        public void Set(long epoch,bool enabled){Epoch=epoch;Enabled=enabled;if(enabled)Assert.False(Disposed);}
        public void Dispose(){Assert.False(Enabled);Disposed=true;}
    }
    private sealed class Registrar:IOleTargetRegistrar
    {
        public TaskCompletionSource? RegisterBarrier{get;init;}
        public bool RevokeBlocked{get;set;}
        public Action? OnRegister{get;init;}
        public byte[]? Packet;private Task? original;private bool revoked=true;
        public Task Settlement=>original??Task.CompletedTask;
        public bool IsSettledAndRevoked=>original is not {IsCompleted:false}&&revoked;
        public Task RegisterAsync(long hwnd,byte[] ownedPacket,CancellationToken cancellation){Packet=ownedPacket;revoked=false;OnRegister?.Invoke();return original=Register(ownedPacket);}
        private async Task Register(byte[] packet){try{if(RegisterBarrier!=null)await RegisterBarrier.Task;}finally{Array.Clear(packet);}}
        public ValueTask<bool> TryFinalizeAsync(){if(original is {IsCompleted:false}||RevokeBlocked)return ValueTask.FromResult(false);revoked=true;return ValueTask.FromResult(true);}
    }
    private sealed class FailedKill(Process child):IOwnedHostLifetime
    {
        public bool HasExited=>child.HasExited;
        public void RequestTermination()=>throw new IOException("synthetic held-child kill failure");
        public Task WaitForExitAsync(CancellationToken cancellation)=>child.WaitForExitAsync(cancellation);
    }
    private static string AssemblyPath()=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.OleReceiverFixture/bin",new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name,"net10.0/KuaiYi.OleReceiverFixture.dll"));
    private static OleReceiverHello Hello(Guid id)=>new(id,1,Environment.ProcessId,1,new byte[32],OleReceiverWire.ActivationName(id));
    private static OleReceiverClient Client(Starter starter,Registrar registrar,Activation activation)=>new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",registrar,activation,AssemblyPath(),starter);
    private static Task Run(OleReceiverClient client,Guid id,TaskCompletionSource ready,Func<long,Guid,ReadOnlyMemory<byte>,CancellationToken,Task>? deliver=null,CancellationToken cancellation=default)=>client.RunAsync(Hello(id),e=>{if(e.Phase=="Ready")ready.TrySetResult();},deliver??((_,_,_,_)=>Task.CompletedTask),cancellation);
    private static async Task Cleanup(OleReceiverClient client,Starter starter,Registrar registrar,Task run)
    {
        client.Cancel();starter.Barrier?.Set();registrar.RegisterBarrier?.TrySetResult();registrar.RevokeBlocked=false;
        await registrar.Settlement.WaitAsync(TimeSpan.FromSeconds(4));
        try{await client.StartupSettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));}catch(IOException){/* Asserted failure settles ownership. */}
        if(!client.ObserveExit()&&starter.Created.Task.IsCompletedSuccessfully){var held=await starter.Created.Task;if(!held.HasExited){held.Kill();await held.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));}}
        try{await run.WaitAsync(TimeSpan.FromSeconds(6));}catch(Exception error){Assert.True(error is OperationCanceledException or OleReceiverException or IOException or InvalidDataException or System.Text.DecoderFallbackException,error.GetType().Name);}
        await client.DeliverySettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));
        await client.RevocationSettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));
        Assert.True(await client.TryFinalizeAsync(true));await client.DisposeAsync();
    }
    [Fact]public async Task RealChildProductionServerDeliversOnlyAfterCleanupAndClearsActualArray()
    {
        Guid id=Guid.NewGuid();using var starter=new Starter();var registrar=new Registrar();var activation=new Activation(id);var client=Client(starter,registrar,activation);
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var delivered=new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);int count=0;
        var run=Run(client,id,ready,(epoch,txn,bytes,token)=>{Assert.Equal(1,epoch);Assert.NotEqual(Guid.Empty,txn);Assert.Equal("public local fixture 文本",OleReceiverWire.DecodeText(bytes.Span));Assert.True(MemoryMarshal.TryGetArray(bytes,out var segment));Interlocked.Increment(ref count);delivered.SetResult(segment.Array!);return Task.CompletedTask;});
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.False(activation.Enabled);Assert.True(await client.SetEnabledAsync(1,true));byte[] actual=await delivered.Task.WaitAsync(TimeSpan.FromSeconds(4));await Task.Yield();client.Cancel();await Record.ExceptionAsync(()=>run);Assert.All(actual,b=>Assert.Equal(0,b));Assert.Equal(1,count);Assert.False(activation.Enabled);Assert.All(registrar.Packet!,b=>Assert.Equal(0,b));Assert.True(client.ObserveExit());}
        finally{await Cleanup(client,starter,registrar,run);}
    }
    [Theory][InlineData("query-stall")][InlineData("drop-stall")][InlineData("release-stall")]
    public async Task RealBlockingChildTimesOutWhileOwnPointerAndOffRemainIndependent(string mode)
    {
        Guid id=Guid.NewGuid();using var starter=new Starter(mode);var registrar=new Registrar();var activation=new Activation(id);var client=Client(starter,registrar,activation);var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int delivered=0;var run=Run(client,id,ready,(_,_,_,_)=>{delivered++;return Task.CompletedTask;});
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.True(await client.SetEnabledAsync(1,true));var child=await starter.Created.Task;
            Assert.Equal("off:1",await starter.ReadStatusAsync());Assert.StartsWith("blocked:",await starter.ReadStatusAsync().WaitAsync(TimeSpan.FromSeconds(3)));
            var gesture=new OverlayPointerGesture();var anchor=new PixelRect(-120,20,-72,68);
            Assert.Equal(OverlayPointerAction.Ignored,gesture.Move(new(-100,30),out _));Assert.Equal(OverlayPointerAction.Pressed,gesture.Down(new(-100,30),anchor,true));Assert.Equal(OverlayPointerAction.Moved,gesture.Move(new(-50,50),out var desired));Assert.Equal(new PixelPoint(-70,40),desired);Assert.Equal(OverlayPointerAction.Released,gesture.Up());
            Assert.False(run.IsCompleted);var failure=await Assert.ThrowsAsync<OleReceiverException>(()=>run.WaitAsync(TimeSpan.FromSeconds(5)));Assert.Equal(OleReceiverFailure.Timeout,failure.Failure);Assert.True(child.HasExited);Assert.True(client.ObserveExit());Assert.False(activation.Enabled);Assert.Equal(0,delivered);
        }finally{await Cleanup(client,starter,registrar,run);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CancelDuringActualStartRetainsOwnershipUntilLateChildOrFailureSettles(bool fail)
    {
        Guid id=Guid.NewGuid();using var starter=new Starter{Barrier=new(false),Fail=fail};var registrar=new Registrar();var activation=new Activation(id);var client=Client(starter,registrar,activation);var ready=new TaskCompletionSource();var run=Run(client,id,ready);
        try{await starter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));client.Cancel();Assert.Equal(OleReceiverFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<OleReceiverException>(()=>run.WaitAsync(TimeSpan.FromSeconds(3)))).Failure);Assert.False(await client.TryFinalizeAsync());Assert.False(activation.Disposed);Assert.False(client.ObserveExit());Assert.False(ready.Task.IsCompleted);starter.Barrier.Set();}
        finally{await Cleanup(client,starter,registrar,run);}Assert.True(activation.Disposed);Assert.Equal(!fail,starter.Created.Task.IsCompletedSuccessfully);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualChildExitDoesNotReleaseOriginalRegistrationOrUnconfirmedRevoke(bool delayedRegistration)
    {
        Guid id=Guid.NewGuid();using var starter=new Starter();var registrar=new Registrar{RegisterBarrier=delayedRegistration?new(TaskCreationOptions.RunContinuationsAsynchronously):null,RevokeBlocked=!delayedRegistration};var activation=new Activation(id);var client=Client(starter,registrar,activation);var ready=new TaskCompletionSource();var run=Run(client,id,ready);
        try
        {
            if(!delayedRegistration)await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));else{var limit=Stopwatch.StartNew();while(registrar.Packet==null&&limit.Elapsed<TimeSpan.FromSeconds(4))await Task.Delay(5);Assert.NotNull(registrar.Packet);Assert.Contains(registrar.Packet!,b=>b!=0);}
            client.Cancel();Assert.Equal(OleReceiverFailure.RegistrarUnconfirmed,(await Assert.ThrowsAsync<OleReceiverException>(()=>run.WaitAsync(TimeSpan.FromSeconds(3)))).Failure);Assert.True(client.ObserveExit());Assert.False(await client.TryFinalizeAsync());Assert.False(activation.Disposed);Assert.False(client.IsReady);
            if(delayedRegistration)Assert.Contains(registrar.Packet!,b=>b!=0);registrar.RegisterBarrier?.SetResult();registrar.RevokeBlocked=false;
        }finally{await Cleanup(client,starter,registrar,run);}Assert.All(registrar.Packet!,b=>Assert.Equal(0,b));
    }
    [Fact]public async Task KillFailureKeepsRealChildAndLeaseButOffReachesIndependentControlLoop()
    {
        Guid id=Guid.NewGuid();using var starter=new Starter("drop-stall");var registrar=new Registrar();var activation=new Activation(id);var client=new OleReceiverClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",registrar,activation,AssemblyPath(),starter){LifetimeForTest=p=>new FailedKill(p)};var ready=new TaskCompletionSource();var run=Run(client,id,ready);
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.True(await client.SetEnabledAsync(1,true));var child=await starter.Created.Task;Assert.Equal("off:1",await starter.ReadStatusAsync());Assert.Equal("blocked:drop",await starter.ReadStatusAsync());client.Cancel();Assert.False(activation.Enabled);Assert.Equal("off:"+long.MaxValue,await starter.ReadStatusAsync().WaitAsync(TimeSpan.FromSeconds(3)));Assert.Equal(OleReceiverFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<OleReceiverException>(()=>run.WaitAsync(TimeSpan.FromSeconds(3)))).Failure);Assert.False(child.HasExited);Assert.False(await client.TryFinalizeAsync());Assert.False(activation.Disposed);Assert.Equal(HostExitState.TerminationFailed,client.ExitState);}
        finally{await Cleanup(client,starter,registrar,run);}
    }
    [Theory][InlineData("wrong-instance")][InlineData("missing-cleanup")][InlineData("duplicate")][InlineData("move")][InlineData("bad-utf8")][InlineData("cleanup-false")]
    public async Task MalformedActualChildFramesNeverReachLocalResult(string mode)
    {
        Guid id=Guid.NewGuid();using var starter=new Starter(mode);var registrar=new Registrar();var activation=new Activation(id);var client=Client(starter,registrar,activation);var ready=new TaskCompletionSource();int delivered=0;var run=Run(client,id,ready,(_,_,_,_)=>{delivered++;return Task.CompletedTask;});
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));await client.SetEnabledAsync(1,true);var error=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(4)));Assert.True(error is OleReceiverException or System.Text.DecoderFallbackException);Assert.Equal(0,delivered);Assert.False(activation.Enabled);Assert.True(client.ObserveExit());}
        finally{await Cleanup(client,starter,registrar,run);}
    }
    [Theory][InlineData("off")][InlineData("replace")]
    public async Task QueuedLocalDeliveryRechecksRuntimeGateAndPayloadIsClearedAfterCancel(string route)
    {
        Guid id=Guid.NewGuid();using var starter=new Starter();var registrar=new Registrar();var activation=new Activation(id);var client=Client(starter,registrar,activation);var ready=new TaskCompletionSource();var queued=new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var gate=new RuntimeGate();gate.SetMode(TriggerMode.Drop);Assert.True(gate.TryEnableLocalOperation(true));long epoch=gate.Snapshot.Epoch;byte[]? actual=null;int writes=0;
        var run=Run(client,id,ready,(generation,txn,bytes,token)=>
        {
            Assert.True(MemoryMarshal.TryGetArray(bytes,out var segment));actual=segment.Array;
            async Task Apply(){await release.Task;if(!client.IsReady||token.IsCancellationRequested||generation!=gate.Snapshot.Epoch)return;if(gate.TryStartConfirmedLocalRequest(generation,id,txn,out var identity,out _))gate.TryApply(identity,()=>writes++);}
            var applying=Apply();queued.SetResult(applying);return applying;
        });
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.True(await client.SetEnabledAsync(epoch,true));var applying=await queued.Task.WaitAsync(TimeSpan.FromSeconds(4));if(route=="off")gate.Disable();else{gate.Disable();Assert.True(gate.TryEnableLocalOperation(true));}client.Cancel();await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(4)));Assert.False(await client.TryFinalizeAsync());Assert.False(activation.Disposed);release.SetResult();await applying;await client.DeliverySettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));Assert.Equal(0,writes);Assert.All(actual!,b=>Assert.Equal(0,b));var origin=new EndpointOrigin(new Uri("https://public.invalid/v1"));gate.ConsentTo(origin);bool started=gate.TryStartRequest(id,Guid.NewGuid(),null,out var next,out _);Assert.Equal(route=="replace",started);Assert.False(gate.CanSend(next,origin));}
        finally{release.TrySetResult();await Cleanup(client,starter,registrar,run);}
    }
    [Fact]public void FreshOwnHitIsRequiredAndReleaseCancelCannotBeAnExternalDrop()
    {
        var state=new OverlayPointerGesture();var anchor=new PixelRect(10,10,58,58);Assert.Equal(OverlayPointerAction.Ignored,state.Down(new(20,20),anchor,false));Assert.Equal(OverlayPointerAction.Ignored,state.Down(new(58,20),anchor,true));Assert.Equal(OverlayPointerAction.Ignored,state.Up());Assert.Equal(OverlayPointerAction.Pressed,state.Down(new(20,20),anchor,true));Assert.Equal(OverlayPointerAction.Ignored,state.Move(new(22,21),out _));Assert.Equal(OverlayPointerAction.Clicked,state.Up());Assert.Equal(OverlayPointerAction.Pressed,state.Down(new(20,20),anchor,true));state.Cancel();Assert.Equal(OverlayPointerAction.Ignored,state.Move(new(50,50),out _));Assert.Equal(OverlayPointerAction.Ignored,state.Up());
    }
    [Fact]public async Task CancelSerializesAgainstEnablePublicationAndPreventsLaterReenable()
    {
        Guid id=Guid.NewGuid();using var starter=new Starter("drop-stall");var registrar=new Registrar();var activation=new Activation(id);var client=Client(starter,registrar,activation);var ready=new TaskCompletionSource();var run=Run(client,id,ready);
        try{await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));await Task.WhenAll(Task.Run(async()=>{for(int i=0;i<200;i++)await client.SetEnabledAsync(i+1,true);}),Task.Run(client.Cancel));Assert.False(activation.Enabled);Assert.False(await client.SetEnabledAsync(long.MaxValue,true));await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(4)));Assert.False(activation.Enabled);}
        finally{await Cleanup(client,starter,registrar,run);}
    }
    private sealed class SilentClock:TimeProvider
    {
        private long timestamp;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Interlocked.Read(ref timestamp);public void ExpireStartup()=>Interlocked.Add(ref timestamp,10_001);
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)=>new SilentTimer();
        private sealed class SilentTimer:ITimer{public bool Change(TimeSpan due,TimeSpan period)=>true;public void Dispose(){}public ValueTask DisposeAsync()=>ValueTask.CompletedTask;}
    }
    private sealed class AdmissionRegistrar:IOleTargetRegistrar,IDisposable
    {
        public TaskCompletionSource Entered{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim barrier=new(false);
        public bool IsSettledAndRevoked=>true; // No native registration has been published yet; the caller still owns the in-progress admission.
        public Task RegisterAsync(long hwnd,byte[] ownedPacket,CancellationToken cancellation)
        {
            Entered.SetResult();try{if(!barrier.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("public registrar admission barrier");cancellation.ThrowIfCancellationRequested();return Task.CompletedTask;}finally{Array.Clear(ownedPacket);}
        }
        public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(true);
        public void Release()=>barrier.Set();public void Dispose(){barrier.Set();barrier.Dispose();}
    }
    [Fact]public async Task OriginalWorkBeforeRegistrationPublicationBlocksEarlyFinalization()
    {
        Guid id=Guid.NewGuid();using var starter=new Starter();using var registrar=new AdmissionRegistrar();var activation=new Activation(id);var client=new OleReceiverClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",registrar,activation,AssemblyPath(),starter);var ready=new TaskCompletionSource();var run=Run(client,id,ready);
        try{await registrar.Entered.Task.WaitAsync(TimeSpan.FromSeconds(4));await Assert.ThrowsAsync<InvalidOperationException>(()=>Run(client,id,new TaskCompletionSource()));client.Cancel();Assert.False(await client.TryFinalizeAsync());Assert.True(client.ObserveExit());Assert.False(run.IsCompleted);Assert.False(activation.Disposed);Assert.False(ready.Task.IsCompleted);}
        finally{registrar.Release();client.Cancel();var error=await Record.ExceptionAsync(()=>run.WaitAsync(TimeSpan.FromSeconds(4)));Assert.IsAssignableFrom<OperationCanceledException>(error);await client.RevocationSettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));Assert.True(await client.TryFinalizeAsync(true));await client.DisposeAsync();}
    }
    [Theory][InlineData(false)][InlineData(true)]public async Task OriginalWorkDeliveryRetainsActualBytesAndLeaseUntilDelegateSettles(bool fault)
    {
        Guid id=Guid.NewGuid();using var starter=new Starter();var registrar=new Registrar();var activation=new Activation(id);var client=Client(starter,registrar,activation);var ready=new TaskCompletionSource();var entered=new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);var original=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run=Run(client,id,ready,(_,_,bytes,_)=>{Assert.True(MemoryMarshal.TryGetArray(bytes,out var segment));entered.SetResult(segment.Array!);return original.Task;});
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(4));Assert.True(await client.SetEnabledAsync(1,true));byte[] actual=await entered.Task.WaitAsync(TimeSpan.FromSeconds(4));int writes=0;Assert.False(client.TryApplyLocal(2,()=>writes++));Assert.True(client.TryApplyLocal(1,()=>writes++));Assert.Equal(1,writes);client.Cancel();Assert.False(client.TryApplyLocal(1,()=>writes++));Assert.Equal(1,writes);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>run.WaitAsync(TimeSpan.FromSeconds(4)));
            Assert.False(await client.TryFinalizeAsync());Assert.True(client.ObserveExit());Assert.False(activation.Disposed);Assert.Equal("public local fixture 文本",OleReceiverWire.DecodeText(actual));
            if(fault)original.SetException(new IOException("public late delivery failure"));else original.SetResult();await client.DeliverySettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));await client.RevocationSettlementForTest.WaitAsync(TimeSpan.FromSeconds(4));Assert.True(await client.TryFinalizeAsync());Assert.True(activation.Disposed);Assert.All(actual,b=>Assert.Equal(0,b));Assert.Equal(fault?nameof(IOException):null,client.DeliveryFailureCode);
        }
        finally{original.TrySetResult();await Cleanup(client,starter,registrar,run);}
    }
    [Fact]public async Task ActualRegistrationCompletionStillChecksMonotonicStartupWithoutTimerCallback()
    {
        Guid id=Guid.NewGuid();using var starter=new Starter();var clock=new SilentClock();var registrar=new Registrar{OnRegister=clock.ExpireStartup};var activation=new Activation(id);var client=new OleReceiverClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",registrar,activation,AssemblyPath(),starter,clock);var ready=new TaskCompletionSource();var run=Run(client,id,ready);
        try{Assert.Equal(OleReceiverFailure.Timeout,(await Assert.ThrowsAsync<OleReceiverException>(()=>run.WaitAsync(TimeSpan.FromSeconds(4)))).Failure);Assert.False(ready.Task.IsCompleted);Assert.False(activation.Enabled);Assert.True(client.ObserveExit());}
        finally{await Cleanup(client,starter,registrar,run);}
    }
    [Fact]public async Task PublishedRealExitedChildCannotReleaseWhileOriginalStartupHandoffIsBlocked()
    {
        Guid id=Guid.NewGuid();using var starter=new Starter();using var barrier=new ManualResetEventSlim(false);var published=new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);var registrar=new Registrar();var activation=new Activation(id);var client=new OleReceiverClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",registrar,activation,AssemblyPath(),starter){OwnershipPublishedForTest=p=>{published.SetResult(p);if(!barrier.Wait(TimeSpan.FromSeconds(10)))throw new TimeoutException("public OLE handoff barrier");}};var ready=new TaskCompletionSource();var run=Run(client,id,ready);
        try{var actual=await published.Task.WaitAsync(TimeSpan.FromSeconds(4));actual.Kill();await actual.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(4));client.Cancel();Assert.Equal(OleReceiverFailure.ExitUnconfirmed,(await Assert.ThrowsAsync<OleReceiverException>(()=>run.WaitAsync(TimeSpan.FromSeconds(3)))).Failure);Assert.False(client.ObserveExit());Assert.False(await client.TryFinalizeAsync());Assert.False(activation.Disposed);Assert.False(ready.Task.IsCompleted);}
        finally{barrier.Set();await Cleanup(client,starter,registrar,run);}
    }
}
