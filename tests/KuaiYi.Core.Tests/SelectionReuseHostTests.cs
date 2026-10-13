using System.Diagnostics;
using System.Collections.Concurrent;
using KuaiYi.Core;
using KuaiYi.SelectionReuseDiagnostic;
using Xunit;
namespace KuaiYi.Core.Tests;

public class SelectionReuseHostTests
{
    private static string ChildAssembly()
    {var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,".git"))&&!File.Exists(Path.Combine(root.FullName,".git")))root=root.Parent;return Path.Combine(root!.FullName,"tools","KuaiYi.SelectionReuseDiagnostic","bin","Release","net10.0","KuaiYi.SelectionReuseDiagnostic.dll");}
    private static ReuseDiagnosticHost Host(string scenario="normal",IOwnedProcessStarter? starter=null)=>new(Environment.ProcessPath!,ChildAssembly(),scenario,true,starter);
    private static async Task Settle(ReuseDiagnosticHost host)
    {var watch=Stopwatch.StartNew();while(!await host.TrySettleAsync()){if(watch.Elapsed>TimeSpan.FromSeconds(3))throw new TimeoutException();await Task.Delay(10);}}
    [Fact] public async Task Default_off_never_starts_process_or_publishes_summary()
    {var starter=new NeverStarter();await using var host=new ReuseDiagnosticHost("synthetic","synthetic",starter:starter);await host.RunAsync(2);Assert.Equal(0,starter.Calls);Assert.Null(host.Summary);Assert.False(host.CanReplace);}
    [Theory] [InlineData(1)] [InlineData(2)] public async Task Normal_round_uses_real_process_and_publishes_only_after_exit(int count)
    {await using var host=Host();Assert.Null(host.Summary);await host.RunAsync(count);await Settle(host);var s=host.Summary!;Assert.Equal(ReuseRoundReason.Completed,s.Reason);Assert.Equal(count,s.Confirmed);Assert.True(s.FirstMatch);Assert.Equal(count==2,s.SecondMatch);Assert.True(s.ExitConfirmed&&s.BuffersCleared);Assert.True(host.CanReplace);Assert.False(host.HoldsOwnership);}
    [Fact] public async Task Fragmented_stream_is_assembled_and_all_read_buffers_erased()
    {await using var host=Host("fragmented");var buffers=new ConcurrentQueue<byte[]>();host.ReadBufferAllocatedForTest=buffers.Enqueue;await host.RunAsync(2);await Settle(host);Assert.Equal(ReuseRoundReason.Completed,host.Summary!.Reason);Assert.Equal(2,host.Summary.Confirmed);Assert.All(buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));}
    [Theory] [InlineData("truncated",ReuseRoundReason.Protocol)] [InlineData("disconnect",ReuseRoundReason.Protocol)] [InlineData("extra",ReuseRoundReason.Protocol)] [InlineData("reset-failed",ReuseRoundReason.ResetFailed)] [InlineData("pending",ReuseRoundReason.ResetFailed)] [InlineData("mismatch",ReuseRoundReason.Protocol)] [InlineData("crash",ReuseRoundReason.Crash)] public async Task Real_faulting_child_is_retired_and_exit_observed(string scenario,ReuseRoundReason expected)
    {await using var host=Host(scenario);var buffers=new ConcurrentQueue<byte[]>();host.ReadBufferAllocatedForTest=buffers.Enqueue;await host.RunAsync(2);await Settle(host);Assert.Equal(expected,host.Summary!.Reason);Assert.True(host.Summary.ExitConfirmed&&host.Summary.BuffersCleared);Assert.True(host.CanReplace);Assert.All(buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));}
    [Theory] [InlineData("hang")] [InlineData("late")] public async Task Real_600ms_watchdog_kills_ignoring_child_before_publishing(string scenario)
    {await using var host=Host(scenario);var watch=Stopwatch.StartNew();await host.RunAsync(2);await Settle(host);Assert.True(watch.Elapsed>=TimeSpan.FromMilliseconds(550));Assert.True(watch.Elapsed<TimeSpan.FromSeconds(3));Assert.Equal(ReuseRoundReason.HardTimeout,host.Summary!.Reason);Assert.True(host.Summary.ExitConfirmed);Assert.Equal(0,host.Summary.Confirmed);}
    [Theory] [InlineData(false)] [InlineData(true)] public async Task Cancellation_or_parent_disconnect_stops_delivery_and_observes_exit(bool disconnect)
    {await using var host=Host("hang");using var cancel=new CancellationTokenSource();var running=host.RunAsync(2,cancel.Token);await host.StartedForTest.Task.WaitAsync(TimeSpan.FromSeconds(2));if(disconnect)host.ParentDisconnected();else cancel.Cancel();await running;await Settle(host);Assert.Equal(disconnect?ReuseRoundReason.ParentDisconnected:ReuseRoundReason.Cancelled,host.Summary!.Reason);Assert.Equal(0,host.Summary.Confirmed);Assert.True(host.Summary.ExitConfirmed);}
    [Fact] public async Task Dispose_during_active_request_retires_and_settles_actual_child()
    {var host=Host("hang");var running=host.RunAsync(2);await host.StartedForTest.Task.WaitAsync(TimeSpan.FromSeconds(2));await host.DisposeAsync();await running;Assert.Equal(ReuseRoundReason.Disposed,host.Summary!.Reason);Assert.True(host.Summary.ExitConfirmed);}
    [Fact] public async Task Idle_lease_expires_without_preparing_next_request()
    {await using var host=Host();host.BetweenRequestsForTest=token=>Task.Delay(Timeout.Infinite,token);await host.RunAsync(2);await Settle(host);Assert.Equal(ReuseRoundReason.IdleLease,host.Summary!.Reason);Assert.Equal(1,host.Summary.Confirmed);Assert.False(host.Summary.SecondMatch);}
    [Fact] public async Task Concurrent_round_does_not_create_replacement_child()
    {await using var host=Host("hang");var first=host.RunAsync(2);await host.StartedForTest.Task.WaitAsync(TimeSpan.FromSeconds(2));await host.RunAsync(1);await first;await Settle(host);Assert.Equal(ReuseRoundReason.Concurrent,host.Summary!.Reason);Assert.Equal(0,host.Summary.Confirmed);}
    [Fact] public async Task Late_start_remains_owned_and_is_killed_after_start_settles()
    {
        var starter=new DelayedStarter();var host=Host("hang",starter);
        try{await host.RunAsync(2);Assert.Null(host.Summary);Assert.True(host.HoldsOwnership);Assert.False(host.CanReplace);starter.Release.Set();await host.SessionForTest.StartupSettlement.WaitAsync(TimeSpan.FromSeconds(3));await Settle(host);Assert.Equal(ReuseRoundReason.HardTimeout,host.Summary!.Reason);Assert.True(host.Summary.ExitConfirmed);Assert.Equal(1,starter.Calls);}
        finally{starter.Release.Set();await Settle(host);await host.DisposeAsync();}
    }
    [Fact] public async Task Kill_return_and_unconfirmed_exit_do_not_allow_summary_or_replacement()
    {
        var host=Host();GatedLifetime? held=null;host.SessionForTest.LifetimeForTest=p=>held=new(p);
        try{await host.RunAsync(2);Assert.NotNull(held);Assert.True(held!.Kills>0);Assert.Null(host.Summary);Assert.True(host.HoldsOwnership);Assert.False(host.CanReplace);held.Confirm=true;await Settle(host);Assert.True(host.Summary!.ExitConfirmed);}
        finally{if(held!=null)held.Confirm=true;await Settle(host);await host.DisposeAsync();}
    }
    [Fact] public async Task Late_read_buffer_is_erased_before_exit_confirmation_and_summary()
    {
        var host=Host();var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var buffers=new ConcurrentQueue<byte[]>();host.ReadBufferAllocatedForTest=buffers.Enqueue;host.OutputForTest=s=>new LateStream(s,gate.Task);
        try{await host.RunAsync(1);Assert.Null(host.Summary);Assert.False(host.CanReplace);Assert.True(host.HoldsOwnership);gate.SetResult();await Settle(host);Assert.All(buffers,b=>Assert.All(b,x=>Assert.Equal(0,x)));Assert.Equal(ReuseRoundReason.HardTimeout,host.Summary!.Reason);Assert.True(host.Summary.ExitConfirmed);}
        finally{gate.TrySetResult();await Settle(host);await host.DisposeAsync();}
    }
    [Fact] public async Task Trailing_extra_after_final_request_is_rejected()
    {await using var host=Host("extra");await host.RunAsync(1);await Settle(host);Assert.Equal(ReuseRoundReason.Protocol,host.Summary!.Reason);Assert.Equal(1,host.Summary.Confirmed);Assert.True(host.Summary.ExitConfirmed);}
    [Fact] public async Task Invalid_executable_confirms_empty_owner_without_faking_child_exit()
    {await using var host=new ReuseDiagnosticHost(Path.Combine(Path.GetTempPath(),"KuaiYi-missing-"+Guid.NewGuid()),ChildAssembly(),enabled:true);await host.RunAsync(1);Assert.True(await host.TrySettleAsync());Assert.True(host.NoChildStartupSettled);Assert.False(host.HoldsOwnership);Assert.True(host.CanReplace);Assert.Null(host.Summary);}
    [Fact] public async Task Pre_start_cancellation_settles_empty_owner_and_never_calls_starter()
    {var starter=new NeverStarter();await using var host=Host(starter:starter);using var c=new CancellationTokenSource();c.Cancel();await host.RunAsync(1,c.Token);await Settle(host);Assert.Equal(0,starter.Calls);Assert.True(host.NoChildStartupSettled);Assert.True(host.CanReplace);Assert.False(host.HoldsOwnership);Assert.Null(host.Summary);}
    [Fact] public async Task Idle_return_past_absolute_lease_does_not_start_second_request()
    {await using var host=Host();host.BetweenRequestsForTest=_=>{Thread.Sleep(150);return Task.CompletedTask;};await host.RunAsync(2);await Settle(host);Assert.Equal(ReuseRoundReason.IdleLease,host.Summary!.Reason);Assert.Equal(1,host.Summary.Confirmed);Assert.False(host.Summary.SecondMatch);}
    [Fact] public async Task Case_timeout_waits_for_original_ignoring_task_and_owned_child_settlement()
    {
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var timeout=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);ReuseDiagnosticHost? host=null;
        var result=DiagnosticCaseRunner.ExecuteAsync(async()=>{host=Host("hang");var run=host.RunAsync(1);await host.StartedForTest.Task.WaitAsync(TimeSpan.FromSeconds(2));await gate.Task;await run;},TimeSpan.FromMilliseconds(150),()=>timeout.SetResult());
        try{await timeout.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.False(result.IsCompleted);gate.SetResult();var outcome=await result.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(outcome.Passed);Assert.True(outcome.TimedOut&&outcome.CleanupSettled);Assert.True(host!.Summary!.ExitConfirmed);Assert.False(host.HoldsOwnership);}
        finally{gate.TrySetResult();await result;}
    }
    private sealed class NeverStarter:IOwnedProcessStarter {public int Calls;public Process Start(ProcessStartInfo info){Calls++;throw new InvalidOperationException();}}
    private sealed class DelayedStarter:IOwnedProcessStarter
    {public readonly ManualResetEventSlim Release=new();public int Calls;public Process Start(ProcessStartInfo info){Calls++;Release.Wait();return Process.Start(info)!;}}
    private sealed class GatedLifetime(Process child):IOwnedHostLifetime
    {public bool Confirm;public int Kills;public bool HasExited=>Confirm&&child.HasExited;public void RequestTermination(){Kills++;child.Kill();}public Task WaitForExitAsync(CancellationToken cancellation)=>child.WaitForExitAsync(cancellation);}
    private sealed class LateStream(Stream inner,Task gate):Stream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellation=default){await gate;buffer.Span.Fill(0xa5);return 0;}
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Flush()=>inner.Flush();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
}
