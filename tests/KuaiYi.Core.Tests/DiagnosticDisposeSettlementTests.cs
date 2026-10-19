using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using KuaiYi.Core;
using KuaiYi.SelectionReuseDiagnostic;
using Xunit;

namespace KuaiYi.Core.Tests;

// Actual owned managed child; gates control only the test's startup/IO/exit-observation ports.
// They do not attest Windows UIA/COM/Job settlement or identify the historical r06 schedule.
public sealed class DiagnosticDisposeSettlementTests
{
    private sealed class Gate
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Hold(){Entered.TrySetResult();Release.Task.GetAwaiter().GetResult();}
    }
    private sealed class Lifetime(Process child):IOwnedHostLifetime
    {
        public bool Confirm=true;public int Terminations;
        public readonly TaskCompletionSource ActualExit=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HasExited{get{bool exit=child.HasExited;if(exit)ActualExit.TrySetResult();return Confirm&&exit;}}
        public void RequestTermination(){Interlocked.Increment(ref Terminations);if(!child.HasExited)child.Kill();}
        public async Task WaitForExitAsync(CancellationToken cancellation){await child.WaitForExitAsync(cancellation);ActualExit.TrySetResult();}
    }
    private sealed class Starter(Gate gate):IOwnedProcessStarter
    {public int Calls;public Process Start(ProcessStartInfo info){Interlocked.Increment(ref Calls);gate.Hold();return Process.Start(info)!;}}
    private sealed class LateRead(Stream inner,Gate gate,bool fault=false):Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default)
        {buffer.Span.Fill(0x51);gate.Entered.TrySetResult();await gate.Release.Task;buffer.Span.Fill(0xa5);if(fault)throw new InvalidOperationException("PUBLIC_FIXTURE_READ_FAULT");return 0;}
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush()=>inner.Flush();
        public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    private sealed class Rig
    {
        public readonly Gate Held=new();public readonly ConcurrentQueue<byte[]> Buffers=new();public readonly ReuseDiagnosticHost Host;public readonly string Phase;
        public Lifetime? Child;public Task? Running,Disposing;
        public Rig(string phase)
        {
            Phase=phase;var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,".git"))&&!File.Exists(Path.Combine(root.FullName,".git")))root=root.Parent;
            var assembly=Path.Combine(root!.FullName,"tools","KuaiYi.SelectionReuseDiagnostic","bin","Release","net10.0","KuaiYi.SelectionReuseDiagnostic.dll");
            Host=new(ManagedFixtureHost.Executable,assembly,"normal",true,phase=="startup"?new Starter(Held):null,request:new(new(42,123456,99),1,2,3,4,true),matcher:b=>b.Span.SequenceEqual(new byte[]{1,2,3}));
            Host.SessionForTest.LifetimeForTest=p=>Child=new(p);Host.ReadBufferAllocatedForTest=Buffers.Enqueue;
            if(phase=="stderr")Host.StderrBufferForTest=b=>{Buffers.Enqueue(b);b.AsSpan().Fill(0x51);Held.Hold();};
            if(phase=="stdout")Host.OutputForTest=s=>new LateRead(s,Held);
            if(phase=="exit-wait")Host.ExitWaitForTest=async(p,_)=>{await p.WaitForExitAsync();Held.Entered.TrySetResult();await Held.Release.Task;};
        }
        public async Task PrepareHeld()
        {
            Running=Host.RunAsync(1);await Held.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));await Running.WaitAsync(TimeSpan.FromSeconds(6));
            if(Phase=="startup")Assert.Null(Child);
            else{Assert.NotNull(Child);await Child.ActualExit.Task.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(HostExitState.ExitConfirmed,Host.SessionForTest.ExitState);}
            Assert.Null(Host.Summary);Assert.True(Host.HoldsOwnership);Assert.False(Host.CanReplace);
            if(Phase is "stdout" or "stderr")Assert.Contains(Buffers,b=>b.Any(x=>x!=0));
        }
        public async Task Cleanup()
        {
            Held.Release.TrySetResult();if(Running!=null)await Running.WaitAsync(TimeSpan.FromSeconds(6));
            // Observe a parent's expected failing disposer without replacing the assertion failure.
            if(Disposing!=null)try{await Disposing.WaitAsync(TimeSpan.FromSeconds(6));}catch(IOException){}
            var watch=Stopwatch.StartNew();while(!await Host.TrySettleAsync()){if(watch.Elapsed>TimeSpan.FromSeconds(6))throw new TimeoutException("Owned fixture cleanup did not settle.");await Task.Delay(5);}
            await Host.DisposeAsync();
        }
    }
    [Theory][InlineData("stderr")][InlineData("stdout")][InlineData("exit-wait")][InlineData("startup")]
    public async Task SameDisposeWaitsForHeldSettlementAndOnlyThenReleasesRealOwner(string phase)
    {
        var r=new Rig(phase);
        try
        {
            await r.PrepareHeld();r.Disposing=r.Host.DisposeAsync().AsTask();
            // Running is already complete: a single-shot false probe faults synchronously on the parent.
            Assert.False(r.Disposing.IsCompleted);Assert.Null(r.Host.Summary);Assert.True(r.Host.HoldsOwnership);Assert.False(r.Host.CanReplace);
            r.Held.Release.TrySetResult();await r.Disposing.WaitAsync(TimeSpan.FromSeconds(6));Assert.NotNull(r.Child);Assert.True(r.Child.ActualExit.Task.IsCompletedSuccessfully);
            var summary=Assert.IsType<ReuseRoundSummary>(r.Host.Summary);Assert.True(summary.ExitConfirmed&&summary.BuffersCleared);Assert.False(r.Host.HoldsOwnership);Assert.True(r.Host.CanReplace);Assert.All(r.Buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));
        }
        finally{await r.Cleanup();}
    }
    [Theory][InlineData("stderr")][InlineData("stdout")][InlineData("exit-wait")][InlineData("startup")]
    public async Task HeldPastBudgetFailsWithOwnerRetainedAndLaterSameHostDisposeCanSettle(string phase)
    {
        var r=new Rig(phase);
        try
        {
            await r.PrepareHeld();var watch=Stopwatch.StartNew();r.Disposing=r.Host.DisposeAsync().AsTask();
            var error=await Assert.ThrowsAsync<IOException>(async()=>await r.Disposing.WaitAsync(TimeSpan.FromSeconds(5)));Assert.NotNull(error.InnerException);Assert.True(watch.Elapsed<TimeSpan.FromSeconds(5));
            Assert.False(r.Held.Release.Task.IsCompleted);Assert.Null(r.Host.Summary);Assert.True(r.Host.HoldsOwnership);Assert.False(r.Host.CanReplace);
            if(phase!="startup")Assert.True(r.Child!.HasExited);
            if(phase is "stdout" or "stderr")Assert.Contains(r.Buffers,b=>b.Any(x=>x!=0));
            r.Held.Release.TrySetResult();await r.Host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Host.Summary!.ExitConfirmed);Assert.False(r.Host.HoldsOwnership);Assert.All(r.Buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));
        }
        finally{await r.Cleanup();}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ActualExitDoesNotReplaceUnconfirmedExitObservationOrStackStopBudgets(bool timeout)
    {
        var r=new Rig("observation");
        r.Host.SessionForTest.LifetimeForTest=p=>r.Child=new(p){Confirm=false};
        try
        {
            r.Running=r.Host.RunAsync(1);await r.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.NotNull(r.Child);await r.Child.ActualExit.Task.WaitAsync(TimeSpan.FromSeconds(6));Assert.False(r.Child.HasExited);Assert.Equal(HostExitState.ExitUnconfirmed,r.Host.SessionForTest.ExitState);int stops=r.Child.Terminations;
            r.Disposing=r.Host.DisposeAsync().AsTask();Assert.False(r.Disposing.IsCompleted);Assert.Null(r.Host.Summary);Assert.True(r.Host.HoldsOwnership);Assert.False(r.Host.CanReplace);
            if(timeout){await Assert.ThrowsAsync<IOException>(async()=>await r.Disposing.WaitAsync(TimeSpan.FromSeconds(5)));Assert.Null(r.Host.Summary);Assert.True(r.Host.HoldsOwnership);Assert.Equal(stops,r.Child.Terminations);}
            r.Child.Confirm=true;await (timeout?r.Host.DisposeAsync().AsTask():r.Disposing).WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Host.Summary!.ExitConfirmed);Assert.Equal(stops,r.Child.Terminations);Assert.False(r.Host.HoldsOwnership);
        }
        finally{if(r.Child!=null)r.Child.Confirm=true;await r.Cleanup();}
    }
    [Fact]
    public async Task SettlementSemaphoreWaitUsesTheSameBoundedCleanupBudget()
    {
        var r=new Rig("stderr");var mutex=(SemaphoreSlim)typeof(ReuseDiagnosticHost).GetField("settlement",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(r.Host)!;bool held=false;
        try
        {
            await r.PrepareHeld();Assert.True(mutex.Wait(0));held=true;r.Disposing=r.Host.DisposeAsync().AsTask();r.Held.Release.TrySetResult();
            await Assert.ThrowsAsync<IOException>(async()=>await r.Disposing.WaitAsync(TimeSpan.FromSeconds(5)));Assert.Null(r.Host.Summary);Assert.True(r.Host.HoldsOwnership);Assert.False(r.Host.CanReplace);Assert.True(r.Child!.HasExited);Assert.All(r.Buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));
            mutex.Release();held=false;await r.Host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Host.Summary!.ExitConfirmed);
        }
        finally{if(held)mutex.Release();await r.Cleanup();}
    }
    [Fact]
    public async Task ConcurrentAndRepeatedDisposersJoinTheSameIoWithoutEarlyReleaseOrRestart()
    {
        var r=new Rig("stderr");Task? second=null;
        try
        {
            await r.PrepareHeld();r.Disposing=r.Host.DisposeAsync().AsTask();second=r.Host.DisposeAsync().AsTask();Assert.False(r.Disposing.IsCompleted);Assert.False(second.IsCompleted);Assert.True(r.Host.HoldsOwnership);r.Held.Release.TrySetResult();await Task.WhenAll(r.Disposing,second).WaitAsync(TimeSpan.FromSeconds(6));
            var summary=r.Host.Summary;int stops=r.Child!.Terminations;await r.Host.DisposeAsync();await r.Host.RunAsync(1);Assert.Same(summary,r.Host.Summary);Assert.Equal(stops,r.Child.Terminations);Assert.False(r.Host.HoldsOwnership);Assert.All(r.Buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));
        }
        finally{r.Held.Release.TrySetResult();if(second!=null)try{await second.WaitAsync(TimeSpan.FromSeconds(6));}catch(IOException){}await r.Cleanup();}
    }
    [Fact]
    public async Task FaultedLateIoIsPropagatedAfterErasureAndNeverPublishesSuccessfulSettlement()
    {
        var r=new Rig("stdout");r.Host.OutputForTest=s=>new LateRead(s,r.Held,true);
        try
        {
            await r.PrepareHeld();r.Disposing=r.Host.DisposeAsync().AsTask();Assert.False(r.Disposing.IsCompleted);r.Held.Release.TrySetResult();var error=await Assert.ThrowsAsync<InvalidOperationException>(async()=>await r.Disposing.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal("PUBLIC_FIXTURE_READ_FAULT",error.Message);
            Assert.Null(r.Host.Summary);Assert.True(r.Host.HoldsOwnership);Assert.False(r.Host.CanReplace);Assert.True(r.Child!.HasExited);Assert.All(r.Buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));Assert.False(await r.Host.TrySettleAsync());
            var io=(List<Task>)typeof(ReuseDiagnosticHost).GetField("lateIo",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(r.Host)!;Assert.NotEmpty(io);Assert.All(io,t=>Assert.True(t.IsCompleted));Assert.Contains(io,t=>t.IsFaulted);
        }
        finally
        {
            r.Held.Release.TrySetResult();if(r.Running!=null)await r.Running.WaitAsync(TimeSpan.FromSeconds(6));if(r.Disposing!=null)try{await r.Disposing.WaitAsync(TimeSpan.FromSeconds(6));}catch(InvalidOperationException){}
            // Fixture-only handle cleanup, after actual exit, completed faulty IO and buffer erasure.
            // Host stays quarantined: no Summary/CanReplace or claimed successful Dispose.
            Assert.NotNull(r.Child);Assert.True(r.Child.HasExited);Assert.All(r.Buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));Assert.True(await r.Host.SessionForTest.TryFinalizeAsync(retry:true));Assert.Null(r.Host.Summary);Assert.False(r.Host.CanReplace);
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task DisposedBeforeStartAndPreCancelledStartupNeverStartOrInventAChildExit(bool cancelled)
    {
        var r=new Rig("unused");
        try
        {
            using var stop=new CancellationTokenSource();stop.Cancel();
            if(cancelled){r.Running=r.Host.RunAsync(1,stop.Token);await r.Running;await r.Host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(6));Assert.True(r.Host.NoChildStartupSettled);Assert.True(r.Host.CanReplace);}
            else{await r.Host.DisposeAsync();await r.Host.RunAsync(1);Assert.False(r.Host.NoChildStartupSettled);}
            Assert.Null(r.Child);Assert.Null(r.Host.Summary);Assert.False(r.Host.HoldsOwnership);
        }
        finally{if(cancelled)await r.Cleanup();else await r.Host.DisposeAsync();}
    }
}
