using KuaiYi.SelectionReusePrototype;
using Xunit;
namespace KuaiYi.Core.Tests;

public class SelectionReusePrototypeTests
{
    private static readonly ReuseTarget Target=new(42,123456,99);
    private const PersistentResources Kept=PersistentResources.WindowlessMta|PersistentResources.Automation;
    private sealed class Fixture
    {
        public long Tick;public Guid Owner=Guid.NewGuid();
        public ReuseProtocolPrototype Model;public ReuseFrame Request;
        public Fixture(bool initialize=true)
        {Model=new(()=>Tick,Owner,true);Request=Model.Begin(Target,Guid.NewGuid())!;if(initialize)Assert.True(Send(ReuseKind.Initialized));}
        public ReuseFrame Frame(ReuseKind kind,ReuseOutcome outcome=ReuseOutcome.None)=>Request with
        {Kind=kind,ElapsedMs=Tick,Outcome=outcome,Persistent=Kept,Payload=null};
        public bool Send(ReuseKind kind,ReuseOutcome outcome=ReuseOutcome.None)
        {var f=Frame(kind,outcome);if(kind==ReuseKind.Quiescent)f=f with{ResetOk=true};return Model.ReceiveOwnedFrame(ReuseFrameCodec.Encode(f));}
        public void Complete()
        {Assert.True(Send(ReuseKind.Completed,ReuseOutcome.Empty));Assert.True(Send(ReuseKind.Quiescent,ReuseOutcome.Empty));}
    }
    private static void Retired(Fixture f,ReuseFailure reason)
    {Assert.Equal(ReuseState.Retiring,f.Model.State);Assert.Equal(reason,f.Model.Failure);Assert.True(f.Model.TerminateOwnedChildRequested);Assert.False(f.Model.CanReplaceProcess);Assert.False(f.Model.CanAccessTarget());Assert.Null(f.Model.Begin(Target,Guid.NewGuid()));}

    [Fact] public void Disabled_default_does_not_read_clock_or_accept_requests()
    {int reads=0;var m=new ReuseProtocolPrototype(()=>{reads++;return 0;},Guid.NewGuid());Assert.Null(m.Begin(Target,Guid.NewGuid()));m.Cancel();m.ObserveDeadline();Assert.Equal(ReuseState.Disabled,m.State);Assert.False(m.TerminateOwnedChildRequested);Assert.Equal(0,reads);}
    [Fact] public void Enabled_without_owned_child_stays_disabled()
    {var m=new ReuseProtocolPrototype(()=>0,Guid.Empty,true);Assert.Equal(ReuseState.Disabled,m.State);Assert.False(m.ConfirmOwnedProcessExit(Guid.Empty));}
    [Fact] public void Exactly_two_independent_sequential_requests_and_no_replacement_before_exit()
    {
        var f=new Fixture();Assert.False(f.Request.Reused);Assert.Equal(1,f.Request.Sequence);
        f.Tick=30;f.Complete();Assert.Equal(ReuseState.Idle,f.Model.State);Assert.False(f.Model.CanAccessTarget());Assert.False(f.Model.CanReplaceProcess);
        f.Tick=10000;f.Request=f.Model.Begin(Target,Guid.NewGuid())!;
        Assert.True(f.Request.Reused);Assert.Equal(2,f.Request.Sequence);Assert.True(f.Model.CanAccessTarget());
        // The child's elapsed values have their own new origin; wall time is not carried over.
        var completed=f.Request with{Kind=ReuseKind.Completed,Persistent=Kept,ElapsedMs=10,Outcome=ReuseOutcome.Empty};
        f.Tick=10010;Assert.True(f.Model.ReceiveOwnedFrame(ReuseFrameCodec.Encode(completed)));
        Assert.True(f.Model.ReceiveOwnedFrame(ReuseFrameCodec.Encode(completed with{Kind=ReuseKind.Quiescent,ResetOk=true})));
        Assert.Equal(2,f.Model.ConfirmedRequests);Retired(f,ReuseFailure.Exhausted);
        Assert.False(f.Model.ConfirmOwnedProcessExit(Guid.NewGuid()));Assert.False(f.Model.CanReplaceProcess);
        Assert.True(f.Model.ConfirmOwnedProcessExit(f.Owner));Assert.True(f.Model.CanReplaceProcess);Assert.Equal(ReuseState.Exited,f.Model.State);
        Assert.Null(f.Model.Begin(Target,Guid.NewGuid()));
    }
    [Fact] public void No_target_access_before_first_initialization_and_time_includes_it()
    {var f=new Fixture(false);Assert.False(f.Model.CanAccessTarget());f.Tick=250;Assert.True(f.Send(ReuseKind.Initialized));Assert.False(f.Model.CanAccessTarget());f.Tick=600;f.Model.ObserveDeadline();Retired(f,ReuseFailure.HardTimeout);}
    [Fact] public void Soft_timeout_may_reset_within_original_hard_budget_without_new_request()
    {var f=new Fixture();f.Tick=250;Assert.False(f.Model.CanAccessTarget());Assert.True(f.Send(ReuseKind.Completed,ReuseOutcome.SoftTimeout));f.Tick=599;Assert.True(f.Send(ReuseKind.Quiescent,ReuseOutcome.SoftTimeout));Assert.Equal(1,f.Model.ConfirmedRequests);Assert.Equal(ReuseState.Idle,f.Model.State);}
    [Theory] [InlineData(250)] [InlineData(599)] public void Success_after_soft_budget_is_never_confirmed(int tick)
    {var f=new Fixture();f.Tick=tick;Assert.False(f.Send(ReuseKind.Completed,ReuseOutcome.Success));Retired(f,ReuseFailure.Protocol);Assert.Equal(0,f.Model.ConfirmedRequests);}
    [Theory] [InlineData(600)] [InlineData(601)] public void Cleanup_deadline_is_not_reset_after_result(int tick)
    {var f=new Fixture();f.Tick=200;Assert.True(f.Send(ReuseKind.Completed,ReuseOutcome.Empty));f.Tick=tick;f.Model.ObserveDeadline();Retired(f,ReuseFailure.HardTimeout);}
    [Fact] public void Initialization_at_deadline_is_rejected_and_input_erased()
    {var f=new Fixture(false);var b=ReuseFrameCodec.Encode(f.Frame(ReuseKind.Initialized));f.Tick=600;Assert.False(f.Model.ReceiveOwnedFrame(b));Assert.All(b,x=>Assert.Equal(0,x));Retired(f,ReuseFailure.HardTimeout);}
    [Fact] public void Second_request_has_its_own_600ms_origin()
    {var f=new Fixture();f.Complete();f.Tick=10000;f.Request=f.Model.Begin(Target,Guid.NewGuid())!;f.Tick=10599;f.Model.ObserveDeadline();Assert.Equal(ReuseState.Running,f.Model.State);f.Tick=10600;f.Model.ObserveDeadline();Retired(f,ReuseFailure.HardTimeout);}
    [Fact] public void Repeated_nonce_retires_instead_of_reusing()
    {var f=new Fixture();f.Complete();Assert.Null(f.Model.Begin(Target,f.Request.Nonce));Retired(f,ReuseFailure.Protocol);}
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] public void Changed_pid_start_or_hwnd_retires(int field)
    {var f=new Fixture();f.Complete();var different=field switch{0=>Target with{Pid=43},1=>Target with{StartTicks=123457},_=>Target with{Hwnd=100}};Assert.Null(f.Model.Begin(different,Guid.NewGuid()));Retired(f,ReuseFailure.IdentityChanged);}
    [Fact] public void Identity_change_during_request_retires()
    {var f=new Fixture();f.Model.ObserveIdentity(Target with{Hwnd=100});Retired(f,ReuseFailure.IdentityChanged);}
    [Theory] [InlineData(false)] [InlineData(true)] public void New_request_before_reset_is_forbidden(bool completed)
    {var f=new Fixture();if(completed)Assert.True(f.Send(ReuseKind.Completed,ReuseOutcome.Empty));Assert.Null(f.Model.Begin(Target,Guid.NewGuid()));Retired(f,ReuseFailure.ConcurrentRequest);}
    [Fact] public void Parallel_admission_never_issues_two_requests()
    {var f=new Fixture();f.Complete();int admitted=0;Parallel.Invoke(()=>{if(f.Model.Begin(Target,Guid.NewGuid())!=null)Interlocked.Increment(ref admitted);},()=>{if(f.Model.Begin(Target,Guid.NewGuid())!=null)Interlocked.Increment(ref admitted);});Assert.Equal(1,admitted);Retired(f,ReuseFailure.ConcurrentRequest);}
    [Theory] [InlineData(false)] [InlineData(true)] public void Cancellation_retires_and_does_not_trust_late_completion(bool completed)
    {var f=new Fixture();if(completed)Assert.True(f.Send(ReuseKind.Completed,ReuseOutcome.Empty));f.Model.Cancel();var b=ReuseFrameCodec.Encode(f.Frame(ReuseKind.Quiescent,ReuseOutcome.Empty) with{ResetOk=true});Assert.False(f.Model.ReceiveOwnedFrame(b));Assert.All(b,x=>Assert.Equal(0,x));Retired(f,ReuseFailure.Cancelled);Assert.Equal(0,f.Model.ConfirmedRequests);}
    [Fact] public void Crash_requires_exit_confirmation_even_if_no_pending_buffer()
    {var f=new Fixture();f.Model.Crash();Retired(f,ReuseFailure.Crash);Assert.False(f.Model.CanReplaceProcess);Assert.True(f.Model.ConfirmOwnedProcessExit(f.Owner));Assert.True(f.Model.CanReplaceProcess);}
    [Fact] public void Unexpected_exit_never_confirms_current_request()
    {var f=new Fixture();Assert.True(f.Model.ConfirmOwnedProcessExit(f.Owner));Assert.Equal(ReuseFailure.Crash,f.Model.Failure);Assert.Equal(0,f.Model.ConfirmedRequests);Assert.True(f.Model.CanReplaceProcess);}
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] public void Wrong_nonce_sequence_identity_or_reused_frame_retires(int field)
    {var f=new Fixture();var frame=f.Frame(ReuseKind.Completed,ReuseOutcome.Empty);frame=field switch{0=>frame with{Nonce=Guid.NewGuid()},1=>frame with{Sequence=2},2=>frame with{Target=Target with{Pid=43}},_=>frame with{Reused=true}};Assert.False(f.Model.ReceiveOwnedFrame(ReuseFrameCodec.Encode(frame)));Retired(f,field==2?ReuseFailure.IdentityChanged:ReuseFailure.Protocol);}
    [Fact] public void Late_first_frame_cannot_complete_second_request()
    {var f=new Fixture();var late=ReuseFrameCodec.Encode(f.Frame(ReuseKind.Completed,ReuseOutcome.Empty));f.Complete();f.Request=f.Model.Begin(Target,Guid.NewGuid())!;Assert.False(f.Model.ReceiveOwnedFrame(late));Retired(f,ReuseFailure.Protocol);Assert.Equal(1,f.Model.ConfirmedRequests);}
    [Fact] public void Extra_frame_while_idle_retires()
    {var f=new Fixture();var late=ReuseFrameCodec.Encode(f.Frame(ReuseKind.Initialized));f.Complete();Assert.False(f.Model.ReceiveOwnedFrame(late));Retired(f,ReuseFailure.Protocol);}
    [Theory] [InlineData(ReuseKind.Completed)] [InlineData(ReuseKind.Quiescent)] public void Frames_cannot_skip_initialization(ReuseKind kind)
    {var f=new Fixture(false);Assert.False(f.Send(kind,ReuseOutcome.Empty));Retired(f,ReuseFailure.Protocol);}
    [Fact] public void Reused_request_cannot_reinitialize_or_claim_warmup()
    {var f=new Fixture();f.Complete();f.Request=f.Model.Begin(Target,Guid.NewGuid())!;Assert.False(f.Send(ReuseKind.Initialized));Retired(f,ReuseFailure.Protocol);}
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] public void Reset_failures_or_unsettled_cleanup_never_allow_reuse(int fault)
    {var f=new Fixture();Assert.True(f.Send(ReuseKind.Completed,ReuseOutcome.Empty));var frame=f.Frame(ReuseKind.Quiescent,ReuseOutcome.Empty) with{ResetOk=true};frame=fault switch{0=>frame with{ResetOk=false},1=>frame with{Persistent=PersistentResources.Automation},2=>frame with{PendingOperations=1},_=>frame with{Payload=new byte[]{7}}};Assert.False(f.Model.ReceiveOwnedFrame(ReuseFrameCodec.Encode(frame)));Retired(f,ReuseFailure.ResetFailed);Assert.Equal(0,f.Model.ConfirmedRequests);}
    [Theory] [InlineData(RequestResources.Root)] [InlineData(RequestResources.Focus)] [InlineData(RequestResources.Hit)] [InlineData(RequestResources.Pattern)] [InlineData(RequestResources.Range)] [InlineData(RequestResources.Attributes)] [InlineData(RequestResources.Text)] [InlineData(RequestResources.Buffers)] public void Any_request_reference_prevents_quiescence(RequestResources retained)
    {var f=new Fixture();Assert.True(f.Send(ReuseKind.Completed,ReuseOutcome.Empty));var frame=f.Frame(ReuseKind.Quiescent,ReuseOutcome.Empty) with{ResetOk=true,Transient=retained};Assert.False(f.Model.ReceiveOwnedFrame(ReuseFrameCodec.Encode(frame)));Retired(f,ReuseFailure.ResetFailed);}
    [Theory] [InlineData(false)] [InlineData(true)] public void Host_buffer_is_zeroed_on_reset_or_retirement(bool cancel)
    {var f=new Fixture();var bytes=ReuseFrameCodec.Encode(f.Frame(ReuseKind.Completed,ReuseOutcome.Success) with{Payload=new byte[]{1,2,3}});Assert.True(f.Model.ReceiveOwnedFrame(bytes));Assert.All(bytes,x=>Assert.Equal(0,x));var held=f.Model.BufferedForTest!;Assert.Equal(new byte[]{1,2,3},held);if(cancel)f.Model.Cancel();else Assert.True(f.Send(ReuseKind.Quiescent,ReuseOutcome.Success));Assert.All(held,x=>Assert.Equal(0,x));Assert.False(f.Model.HasBufferedPayload);}
    [Fact] public void Backward_host_or_child_clock_retires()
    {var f=new Fixture();f.Tick=10;Assert.True(f.Send(ReuseKind.Completed,ReuseOutcome.Empty));f.Tick=9;f.Model.ObserveDeadline();Retired(f,ReuseFailure.Protocol);var g=new Fixture();g.Tick=10;Assert.True(g.Send(ReuseKind.Completed,ReuseOutcome.Empty));g.Tick=11;var frame=g.Frame(ReuseKind.Quiescent,ReuseOutcome.Empty) with{ResetOk=true,ElapsedMs=9};Assert.False(g.Model.ReceiveOwnedFrame(ReuseFrameCodec.Encode(frame)));Retired(g,ReuseFailure.Protocol);}
    [Fact] public void Stale_result_retires_without_reset_fallback()
    {var f=new Fixture();Assert.False(f.Send(ReuseKind.Completed,ReuseOutcome.Stale));Retired(f,ReuseFailure.IdentityChanged);}
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] public void Malformed_fixed_wire_retires_and_is_erased(int fault)
    {var f=new Fixture();var bytes=ReuseFrameCodec.Encode(f.Frame(ReuseKind.Completed,ReuseOutcome.Empty));switch(fault){case 0:bytes[0]=2;break;case 1:bytes[1]=255;break;case 2:bytes[5]=33;break;case 3:bytes[6]=4;break;case 4:bytes[11]=1;break;case 5:bytes[56]=1;break;case 6:bytes[95]=1;break;case 7:bytes=bytes[..95];break;}Assert.False(f.Model.ReceiveOwnedFrame(bytes));Assert.All(bytes,x=>Assert.Equal(0,x));Retired(f,ReuseFailure.Protocol);}
    [Fact] public void Codec_is_fixed_size_and_rejects_more_than_32_synthetic_bytes()
    {var f=new Fixture();var frame=f.Frame(ReuseKind.Completed,ReuseOutcome.Success) with{Payload=new byte[32]};var bytes=ReuseFrameCodec.Encode(frame);Assert.Equal(96,bytes.Length);var decoded=ReuseFrameCodec.Decode(bytes);Assert.Equal(frame.Nonce,decoded.Nonce);Assert.Equal(frame.Target,decoded.Target);Assert.Equal(32,decoded.Payload!.Length);Assert.Throws<InvalidDataException>(()=>ReuseFrameCodec.Encode(frame with{Payload=new byte[33]}));}
}
