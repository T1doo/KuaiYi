using System.Diagnostics;
using System.Collections.Concurrent;
using KuaiYi.Core;
using KuaiYi.SelectionReusePrototype;
using KuaiYi.SelectionReuseDiagnostic;
using Xunit;
namespace KuaiYi.Core.Tests;

public class SelectionReuseHostTests
{
    private static string ChildAssembly()
    {var root=new DirectoryInfo(AppContext.BaseDirectory);while(root!=null&&!Directory.Exists(Path.Combine(root.FullName,".git"))&&!File.Exists(Path.Combine(root.FullName,".git")))root=root.Parent;return Path.Combine(root!.FullName,"tools","KuaiYi.SelectionReuseDiagnostic","bin","Release","net10.0","KuaiYi.SelectionReuseDiagnostic.dll");}
    private static ReuseDiagnosticHost Host(string scenario="normal",IOwnedProcessStarter? starter=null)=>new(Environment.ProcessPath!,ChildAssembly(),scenario,true,starter,request:Spec(),matcher:bytes=>bytes.Span.SequenceEqual(new byte[]{1,2,3}));
    private static DiagnosticRequest Spec()=>new(new(42,123456,99),1,2,3,4,true);
    private static async Task Settle(ReuseDiagnosticHost host)
    {var watch=Stopwatch.StartNew();while(!await host.TrySettleAsync()){if(watch.Elapsed>TimeSpan.FromSeconds(3))throw new TimeoutException();await Task.Delay(10);}}
    [Fact] public async Task Default_off_never_starts_process_or_publishes_summary()
    {var starter=new NeverStarter();await using var host=new ReuseDiagnosticHost("synthetic","synthetic",starter:starter);await host.RunAsync(2);Assert.Equal(0,starter.Calls);Assert.Null(host.Summary);Assert.False(host.CanReplace);}
    [Theory] [InlineData(1)] [InlineData(2)] public async Task Normal_round_uses_real_process_and_publishes_only_after_exit(int count)
    {await using var host=Host();Assert.Null(host.Summary);await host.RunAsync(count);await Settle(host);var s=host.Summary!;Assert.Equal(ReuseRoundReason.Completed,s.Reason);Assert.Equal(count,s.Confirmed);Assert.Equal(ReuseMatch.Matched,s.FirstMatch);Assert.Equal(count==2?ReuseMatch.Matched:ReuseMatch.NotRun,s.SecondMatch);Assert.True(s.ExitConfirmed&&s.BuffersCleared);Assert.True(host.CanReplace);Assert.False(host.HoldsOwnership);}
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
    {await using var host=Host();host.BetweenRequestsForTest=token=>Task.Delay(Timeout.Infinite,token);await host.RunAsync(2);await Settle(host);Assert.Equal(ReuseRoundReason.IdleLease,host.Summary!.Reason);Assert.Equal(1,host.Summary.Confirmed);Assert.Equal(ReuseMatch.NotRun,host.Summary.SecondMatch);}
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
    {await using var host=new ReuseDiagnosticHost(Path.Combine(Path.GetTempPath(),"KuaiYi-missing-"+Guid.NewGuid()),ChildAssembly(),enabled:true,request:Spec());await host.RunAsync(1);Assert.True(await host.TrySettleAsync());Assert.True(host.NoChildStartupSettled);Assert.False(host.HoldsOwnership);Assert.True(host.CanReplace);Assert.Null(host.Summary);}
    [Fact] public async Task Pre_start_cancellation_settles_empty_owner_and_never_calls_starter()
    {var starter=new NeverStarter();await using var host=Host(starter:starter);using var c=new CancellationTokenSource();c.Cancel();await host.RunAsync(1,c.Token);await Settle(host);Assert.Equal(0,starter.Calls);Assert.True(host.NoChildStartupSettled);Assert.True(host.CanReplace);Assert.False(host.HoldsOwnership);Assert.Null(host.Summary);}
    [Fact] public async Task Idle_return_past_absolute_lease_does_not_start_second_request()
    {await using var host=Host();host.BetweenRequestsForTest=_=>{Thread.Sleep(150);return Task.CompletedTask;};await host.RunAsync(2);await Settle(host);Assert.Equal(ReuseRoundReason.IdleLease,host.Summary!.Reason);Assert.Equal(1,host.Summary.Confirmed);Assert.Equal(ReuseMatch.NotRun,host.Summary.SecondMatch);}
    [Fact] public async Task Case_timeout_waits_for_original_ignoring_task_and_owned_child_settlement()
    {
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var timeout=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);ReuseDiagnosticHost? host=null;
        var result=DiagnosticCaseRunner.ExecuteAsync(async()=>{host=Host("hang");var run=host.RunAsync(1);await host.StartedForTest.Task.WaitAsync(TimeSpan.FromSeconds(2));await gate.Task;await run;},TimeSpan.FromMilliseconds(150),()=>timeout.SetResult());
        try{await timeout.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.False(result.IsCompleted);gate.SetResult();var outcome=await result.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(outcome.Passed);Assert.True(outcome.TimedOut&&outcome.CleanupSettled);Assert.True(host!.Summary!.ExitConfirmed);Assert.False(host.HoldsOwnership);}
        finally{gate.TrySetResult();await result;}
    }
    [Theory] [InlineData("soft",ReuseOutcome.SoftTimeout)] [InlineData("empty",ReuseOutcome.Empty)] [InlineData("sensitive",ReuseOutcome.Sensitive)] [InlineData("protected",ReuseOutcome.UnsupportedOrProtected)] public async Task Legitimate_no_text_status_is_preserved_with_match_not_run(string scenario,ReuseOutcome outcome)
    {await using var host=Host(scenario);await host.RunAsync(2);await Settle(host);Assert.Equal(ReuseRoundReason.Completed,host.Summary!.Reason);Assert.Equal(outcome,host.Summary.FirstOutcome);Assert.Equal(outcome,host.Summary.SecondOutcome);Assert.Equal(ReuseMatch.NotRun,host.Summary.FirstMatch);Assert.Equal(ReuseMatch.NotRun,host.Summary.SecondMatch);Assert.Equal(2,host.Summary.Confirmed);}
    [Fact] public async Task Initialization_failure_does_not_claim_ready_or_second_request()
    {await using var host=Host("init-failed");await host.RunAsync(2);await Settle(host);Assert.Equal(WorkerInitialization.Failed,host.Summary!.Initialization);Assert.Equal(ReuseOutcome.InitializationFailed,host.Summary.FirstOutcome);Assert.Null(host.Summary.SecondOutcome);Assert.Equal(0,host.Summary.Confirmed);Assert.Equal(ReuseMatch.NotRun,host.Summary.FirstMatch);}
    [Fact] public async Task Arbitrary_natural_nonzero_exit_is_not_limited_to_code_23()
    {await using var host=Host("crash-other");await host.RunAsync(1);await Settle(host);Assert.Equal(ReuseRoundReason.Crash,host.Summary!.Reason);Assert.Equal(WorkerTermination.NaturalNonZero,host.Summary.Termination);}
    [Fact] public async Task Watchdog_shutdown_is_parent_requested_not_natural_crash()
    {await using var host=Host("hang");await host.RunAsync(1);await Settle(host);Assert.Equal(ReuseRoundReason.HardTimeout,host.Summary!.Reason);Assert.Equal(WorkerTermination.ParentRequested,host.Summary.Termination);}
    [Fact] public async Task Stderr_flood_is_drained_erased_and_never_forwarded()
    {await using var host=Host("stderr-flood");byte[]? bytes=null;host.StderrBufferForTest=b=>bytes=b;await host.RunAsync(1);await Settle(host);Assert.NotNull(bytes);Assert.All(bytes!,b=>Assert.Equal(0,b));Assert.NotEqual(ReuseRoundReason.Completed,host.Summary!.Reason);Assert.True(host.Summary.BuffersCleared);}
    [Theory] [InlineData(false,true,true,true,true)] [InlineData(true,false,true,true,true)] [InlineData(true,true,false,true,true)] [InlineData(true,true,true,false,true)] [InlineData(true,true,true,true,false)] public void Job_prerequisite_failure_never_allows_first_request(bool kill,bool exclusive,bool inherit,bool assign,bool member)
    {var gate=new JobBeforeTargetGate(kill,exclusive,inherit);Assert.False(gate.CanSendFirstRequest);Assert.Throws<InvalidOperationException>(()=>gate.VerifyAssignment(assign,member));Assert.False(gate.CanSendFirstRequest);}
    [Fact] public void Job_gate_requires_verified_membership_and_revokes_on_failure()
    {var gate=new JobBeforeTargetGate(true,true,true);Assert.False(gate.CanSendFirstRequest);gate.VerifyAssignment(true,true);Assert.True(gate.CanSendFirstRequest);Assert.Throws<InvalidOperationException>(()=>gate.VerifyAssignment(false,true));Assert.False(gate.CanSendFirstRequest);}
    [Fact] public void Real_source_and_gesture_encode_without_fixture_constants()
    {var spec=new DiagnosticRequest(new(44,98765,101),-10,20,300,-400,false);var bytes=spec.Encode(1234,true);Assert.Equal(25,bytes.Length);Assert.Equal(-10,System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes));Assert.Equal(300,System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(8)));Assert.Equal(2,bytes[16]);Assert.Equal(1234,System.Buffers.Binary.BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(17)));}
    [Fact] public async Task Soft_expiry_before_initialization_is_not_reported_mta_ready()
    {await using var host=Host("init-soft");await host.RunAsync(2);await Settle(host);Assert.Equal(WorkerInitialization.NotRun,host.Summary!.Initialization);Assert.Equal(ReuseOutcome.SoftTimeout,host.Summary.FirstOutcome);Assert.Null(host.Summary.SecondOutcome);Assert.Equal(ReuseMatch.NotRun,host.Summary.FirstMatch);Assert.Equal(0,host.Summary.Confirmed);}
    [Fact] public async Task Source_stale_is_terminal_and_retains_actual_denial_status()
    {await using var host=Host("stale");await host.RunAsync(2);await Settle(host);Assert.Equal(ReuseRoundReason.SourceStale,host.Summary!.Reason);Assert.Equal(ReuseOutcome.Stale,host.Summary.FirstOutcome);Assert.Null(host.Summary.SecondOutcome);Assert.Equal(ReuseMatch.NotRun,host.Summary.FirstMatch);Assert.Equal(0,host.Summary.Confirmed);}
    [Theory] [InlineData(false)] [InlineData(true)] public async Task Exit_wait_timeout_retains_original_observation_before_summary(bool earlyEof)
    {
        var host=Host(earlyEof?"hang":"normal");if(earlyEof)host.OutputForTest=s=>new LateStream(s,Task.CompletedTask);var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        host.ExitWaitForTest=async(process,token)=>{await process.WaitForExitAsync();entered.TrySetResult();await gate.Task;};
        try{await host.RunAsync(1);await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.Null(host.Summary);Assert.False(await host.TrySettleAsync());Assert.True(host.HoldsOwnership);Assert.False(host.CanReplace);gate.SetResult();await Settle(host);Assert.Equal(earlyEof?ReuseRoundReason.Protocol:ReuseRoundReason.HardTimeout,host.Summary!.Reason);Assert.True(host.Summary.ExitConfirmed);}
        finally{gate.TrySetResult();await Settle(host);await host.DisposeAsync();}
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
