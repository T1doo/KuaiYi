using KuaiYi.Core.Installation;
using System.Text;
using Xunit;
namespace KuaiYi.Core.Tests;

public sealed class InstallationWarmImageTests
{
    private static readonly TimeSpan Guard=TimeSpan.FromSeconds(5);
    internal static InstallationControlledLaunch Proof()=>new(Guid.NewGuid(),"KuaiYi.App.exe",Path.GetFullPath("PUBLIC_INSTALL"),Path.GetFullPath("PUBLIC_DATA"),new("1.0",Path.GetFullPath("PUBLIC_GENERATION"),new string('A',64)),"e30=","e30=",null);
    private const string Image="C:\\public\\synthetic.gif";
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static ValueTask<bool> Confirm()=>ValueTask.FromResult(true);
    private static Task Receipt(InstallationWarmResult _)=>Task.CompletedTask;
    internal sealed class Clock:TimeProvider
    {
        internal long Now;internal readonly List<Timer> Timers=[];
        public override long TimestampFrequency=>1000;
        public override long GetTimestamp()=>Interlocked.Read(ref Now);
        public override ITimer CreateTimer(TimerCallback callback,object? state,TimeSpan dueTime,TimeSpan period)
        {var timer=new Timer(this,callback,state,dueTime);Timers.Add(timer);return timer;}
        internal void Advance(long ms,bool fire=true){Interlocked.Add(ref Now,ms);if(fire)foreach(var timer in Timers.ToArray())timer.FireDue();}
        internal sealed class Timer(Clock clock,TimerCallback callback,object? state,TimeSpan due):ITimer
        {
            private long at=clock.Now+(long)due.TotalMilliseconds;private bool disposed,fired;
            internal TaskCompletionSource? DisposeHeld;
            internal readonly TaskCompletionSource DisposeEntered=Signal();
            internal void FireDue(){if(!disposed&&!fired&&clock.Now>=at){fired=true;callback(state);}}
            internal void DeliverStale()=>callback(state); // Deliberately delayed callback delivery; identity+phase must still fence it.
            public bool Change(TimeSpan d,TimeSpan period){at=clock.Now+(long)d.TotalMilliseconds;return !disposed;}
            public void Dispose(){disposed=true;}
            public ValueTask DisposeAsync(){disposed=true;DisposeEntered.TrySetResult();return new(DisposeHeld?.Task??Task.CompletedTask);}
        }
    }
    [Theory][InlineData(9999,true)][InlineData(10000,false)][InlineData(10001,false)]
    public async Task GrantUsesTheExactMonotonicBoundaryEvenWhenTimerDeliveryIsHeld(long elapsed,bool expected)
    {
        var clock=new Clock();var policy=new InstallationWarmImageAdmission(Proof(),clock);var lease=policy.Reserve(new(),3);
        clock.Advance(elapsed,false);Assert.Equal(expected,policy.Commit(lease,new(lease.Grant,Image)));
        if(expected)Assert.Equal(InstallationWarmPhase.Preparing,policy.Phase(lease));
        await policy.Revoke(lease);Assert.True(await policy.RetireAsync(lease,Confirm,Receipt));
    }
    [Theory][InlineData(14999,true)][InlineData(15000,false)][InlineData(15001,false)]
    public async Task PrepareHasItsOwnExactBoundaryAndGrantTimerCannotRevokeIt(long elapsed,bool expected)
    {
        var clock=new Clock();var policy=new InstallationWarmImageAdmission(Proof(),clock);var lease=policy.Reserve(new(),3);
        clock.Advance(9999,false);Assert.True(policy.Commit(lease,new(lease.Grant,Image)));
        clock.Timers[0].DeliverStale();Assert.False(lease.Token.IsCancellationRequested);
        clock.Advance(elapsed,false);int accepts=0;
        Assert.Equal(expected,policy.TryAccept(lease,()=>{accepts++;return new(Guid.NewGuid(),1);}));Assert.Equal(expected?1:0,accepts);
        await policy.Revoke(lease);Assert.True(await policy.RetireAsync(lease,Confirm,Receipt));
    }
    [Fact]public async Task SingleSlotBurnsAConnectionAndRejectsCrossConnectionReplayWithoutASeenSet()
    {
        var proof=Proof();var clock=new Clock();var policy=new InstallationWarmImageAdmission(proof,clock);var firstConnection=new InstallationWarmConnection();var first=policy.Reserve(firstConnection,4);
        var busyConnection=new InstallationWarmConnection();Assert.Throws<InvalidOperationException>(()=>policy.Reserve(busyConnection,4));
        await policy.Revoke(first);Assert.True(await policy.RetireAsync(first,Confirm,Receipt));
        Assert.Throws<InvalidOperationException>(()=>policy.Reserve(firstConnection,4));Assert.Throws<InvalidOperationException>(()=>policy.Reserve(busyConnection,4));
        var next=policy.Reserve(new(),4);Assert.True(next.Grant.Identity.Ordinal>first.Grant.Identity.Ordinal);
        Assert.Throws<InvalidDataException>(()=>policy.Commit(next,new(first.Grant,Image)));
        clock.Timers[0].DeliverStale();Assert.False(next.Token.IsCancellationRequested);Assert.Equal(InstallationWarmPhase.Reserved,policy.Phase(next));
        await policy.Revoke(next);Assert.True(await policy.RetireAsync(next,Confirm,Receipt));
        Assert.Throws<InvalidDataException>(()=>policy.ReceiveReserve(first.Grant.Identity,4));
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task AcceptAndCancellationHaveBothDeterministicallyOrderedWinningCases(bool cancelWins)
    {
        var policy=new InstallationWarmImageAdmission(Proof(),new Clock());var lease=policy.Reserve(new(),1);Assert.True(policy.Commit(lease,new(lease.Grant,Image)));
        var entered=Signal();var release=Signal();int accepts=0;Task? originalCancel=null;Task<bool>? accept=null;
        if(cancelWins)
        {
            using var registration=lease.Token.Register(()=>{entered.TrySetResult();release.Task.GetAwaiter().GetResult();});
            try
            {
                originalCancel=policy.Revoke(lease);await entered.Task.WaitAsync(Guard);
                Assert.False(policy.TryAccept(lease,()=>{accepts++;return new(Guid.NewGuid(),1);}));Assert.Equal(0,accepts);
                var retiring=policy.RetireAsync(lease,Confirm,Receipt);Assert.False(retiring.IsCompleted);Assert.True(policy.Busy);release.TrySetResult();Assert.True(await retiring.WaitAsync(Guard));
            }
            finally{release.TrySetResult();if(originalCancel!=null)await originalCancel.WaitAsync(Guard);}
        }
        else
        {
            try
            {
                accept=Task.Run(()=>policy.TryAccept(lease,()=>{entered.TrySetResult();release.Task.GetAwaiter().GetResult();accepts++;return new(Guid.NewGuid(),1);}));await entered.Task.WaitAsync(Guard);
                var cancelAttempted=Signal();originalCancel=Task.Run(async()=>{cancelAttempted.TrySetResult();await policy.Revoke(lease);});await cancelAttempted.Task.WaitAsync(Guard);
                Assert.False(originalCancel.IsCompleted);release.TrySetResult();Assert.True(await accept.WaitAsync(Guard));await originalCancel.WaitAsync(Guard);
                Assert.False(lease.Token.IsCancellationRequested);Assert.Equal(1,accepts);Assert.True(await policy.RetireAsync(lease,Confirm,Receipt));
            }
            finally{release.TrySetResult();if(accept!=null)await accept.WaitAsync(Guard);if(originalCancel!=null)await originalCancel.WaitAsync(Guard);}
        }
    }
    [Fact]public async Task OriginalWorkCancellationAndTimerDisposalAllFenceRetirementAndOldTimerCannotTouchNewLease()
    {
        var clock=new Clock();var policy=new InstallationWarmImageAdmission(Proof(),clock);var lease=policy.Reserve(new(),1);var work=Signal();var started=Signal();
        _=policy.RunOwned(lease,_=>{started.TrySetResult();return work.Task;});await started.Task.WaitAsync(Guard);
        var timer=clock.Timers[0];timer.DisposeHeld=Signal();await policy.Revoke(lease);int finals=0;
        var retiring=policy.RetireAsync(lease,()=>{finals++;return ValueTask.FromResult(false);},Receipt);
        try
        {
            Assert.False(retiring.IsCompleted);Assert.True(policy.Busy);Assert.Equal(0,finals);work.TrySetResult();await timer.DisposeEntered.Task.WaitAsync(Guard);
            Assert.False(retiring.IsCompleted);Assert.Equal(0,finals);timer.DisposeHeld.TrySetResult();Assert.False(await retiring.WaitAsync(Guard));Assert.True(policy.Busy);
            Assert.True(await policy.RetireAsync(lease,Confirm,Receipt));var next=policy.Reserve(new(),1);timer.DeliverStale();Assert.False(next.Token.IsCancellationRequested);
            await policy.Revoke(next);Assert.True(await policy.RetireAsync(next,Confirm,Receipt));
        }
        finally{work.TrySetResult();timer.DisposeHeld.TrySetResult();await retiring.WaitAsync(Guard);}
    }
    private sealed class Backpressure(bool flush):MemoryStream
    {
        internal readonly TaskCompletionSource Entered=Signal(),Release=Signal();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> data,CancellationToken cancellationToken=default)
        {await base.WriteAsync(data,CancellationToken.None);if(!flush){Entered.TrySetResult();await Release.Task;}}
        public override async Task FlushAsync(CancellationToken cancellationToken){if(flush){Entered.TrySetResult();await Release.Task;}await base.FlushAsync(CancellationToken.None);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OriginalReceiptWriteAndFlushBackpressureCannotClearSlotAndQuiesceDoesNotWaitForWriter(bool flush)
    {
        var policy=new InstallationWarmImageAdmission(Proof(),new Clock());var lease=policy.Reserve(new(),1);await policy.Revoke(lease);using var stream=new Backpressure(flush);
        var retiring=policy.RetireAsync(lease,Confirm,r=>InstallationControlWire.Send(stream,"WARM_RESULT|"+InstallationWarmImageWire.Encode(r),CancellationToken.None));
        try
        {
            await stream.Entered.Task.WaitAsync(Guard);Assert.NotNull(lease.FinalWrite);Assert.False(lease.FinalWrite.IsCompleted);
            Assert.True(policy.Quiesce().IsCompletedSuccessfully);Assert.True(lease.Token.IsCancellationRequested);Assert.False(retiring.IsCompleted);Assert.True(policy.Busy);
            stream.Release.TrySetResult();Assert.True(await retiring.WaitAsync(Guard));Assert.False(policy.Busy);
        }
        finally{stream.Release.TrySetResult();await retiring.WaitAsync(Guard);}
    }
    [Fact]public async Task BodyAndCleanupFailuresAreBothPreservedAndNeverEmitASuccessOrReleaseTheSlot()
    {
        var policy=new InstallationWarmImageAdmission(Proof(),new Clock());var lease=policy.Reserve(new(),1);var body=new IOException("PUBLIC_BODY");var cleanup=new InvalidOperationException("PUBLIC_CLEANUP");
        Task original=policy.RunOwned(lease,_=>Task.FromException(body));await policy.Revoke(lease);int receipts=0;
        var error=await Assert.ThrowsAsync<AggregateException>(()=>policy.RetireAsync(lease,()=>ValueTask.FromException<bool>(cleanup),_=>{receipts++;return Task.CompletedTask;}));
        Assert.Contains(body,error.InnerExceptions);Assert.Contains(cleanup,error.InnerExceptions);Assert.True(original.IsFaulted);Assert.Equal(0,receipts);Assert.True(policy.Busy);
    }
    [Theory][InlineData("whitespace")][InlineData("unknown")][InlineData("oversize")][InlineData("missing")]
    public void WarmWireRejectsNoncanonicalAndOversizedPayloads(string fault)
    {
        var proof=Proof();var policy=new InstallationWarmImageAdmission(proof,new Clock());var lease=policy.Reserve(new(),2);string encoded=InstallationWarmImageWire.Encode(lease.Grant);
        string bad=fault switch{"whitespace"=>encoded.Insert(4," "),"unknown"=>Convert.ToBase64String(Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Convert.FromBase64String(encoded))[..^1]+",\"Unknown\":1}")),"oversize"=>new string('A',49153),_=>"e30="};
        var error=Record.Exception(()=>InstallationWarmImageWire.Decode<InstallationWarmGrant>(bad));Assert.True(error is InvalidDataException or System.Text.Json.JsonException);
    }
}
