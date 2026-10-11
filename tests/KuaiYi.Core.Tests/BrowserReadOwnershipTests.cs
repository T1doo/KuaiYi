using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Imaging;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class BrowserReadOwnershipTests
{
    private sealed class ManualClock:TimeProvider
    {
        private TimerCallback? callback;private object? state;
        public TimeSpan Due {get;private set;}
        public override ITimer CreateTimer(TimerCallback timerCallback,object? timerState,TimeSpan dueTime,TimeSpan period)
        {callback=timerCallback;state=timerState;Due=dueTime;return new ManualTimer();}
        public void Fire()=>callback!(state);
        private sealed class ManualTimer:ITimer
        {
            public bool Change(TimeSpan dueTime,TimeSpan period)=>true;
            public void Dispose(){}
            public ValueTask DisposeAsync()=>ValueTask.CompletedTask;
        }
    }
    private sealed class NoAuthority:IDesktopImageChannel
    {
        public int Offers,Submissions,Disposals;
        public ValueTask<BrowserGrant> OfferAsync(Guid operation,CancellationToken cancellation){Offers++;throw new InvalidOperationException("Read after deadline must never be delivered.");}
        public ValueTask SubmitAsync(BrowserGrant grant,ReceivedImage image,ImageSize size,CancellationToken cancellation){Submissions++;throw new InvalidOperationException("Read after deadline must never submit.");}
        public ValueTask CancelAsync(BrowserGrant grant,CancellationToken cancellation)=>ValueTask.CompletedTask;
        public ValueTask DisposeAsync(){Disposals++;return ValueTask.CompletedTask;}
    }
    [Fact]
    public async Task CompletedReadReturnedAtExpiredDeadlineCannotDeliverOfferAndHasOneNormalOwner()
    {
        using var wire=new MemoryStream();await NativeMessagingCodec.WriteAsync(wire,new{type="OfferImage",operationId=Guid.NewGuid()});wire.Position=0;
        var clock=new ManualClock();var desktop=new NoAuthority();JsonDocument? document=null;int observers=0;
        var session=new BrowserSession(desktop,timeProvider:clock)
        {
            ReadForTest=(_,_)=>
            {
                var completed=NativeMessagingCodec.ReadAsync(wire,CancellationToken.None).AsTask();Assert.True(completed.IsCompletedSuccessfully);
                document=completed.Result;clock.Fire();return completed;
            },
            LateReadObserverStartedForTest=_=>observers++
        };
        using var output=new MemoryStream();Assert.Equal(3,await session.RunAsync(Stream.Null,output).WaitAsync(TimeSpan.FromSeconds(3)));
        Assert.Equal(0,desktop.Offers);Assert.Equal(0,desktop.Submissions);Assert.Equal(1,desktop.Disposals);Assert.Equal(0,observers);Assert.Null(session.LateReadObserverForTest);
        Assert.Throws<ObjectDisposedException>(()=>document!.RootElement.GetProperty("type"));
        output.Position=0;using var error=await NativeMessagingCodec.ReadAsync(output);Assert.Equal("Timeout",error!.RootElement.GetProperty("code").GetString());Assert.Null(await NativeMessagingCodec.ReadAsync(output));
    }
    [Theory][InlineData("success",true)][InlineData("fault",true)][InlineData("eof",true)][InlineData("success",false)][InlineData("fault",false)][InlineData("eof",false)]
    public async Task DeadlineAndUnderlyingCompletionAlwaysHaveOneObservedOwner(string outcome,bool completesBeforeCatchObservation)
    {
        using var wire=new MemoryStream();
        if(outcome!="eof")await NativeMessagingCodec.WriteAsync(wire,new{type="OfferImage",operationId=Guid.NewGuid()});
        if(outcome=="fault")wire.SetLength(wire.Length-1); // Actual codec sees a truncated frame and throws EndOfStreamException.
        wire.Position=0;
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseRead=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var catchEntered=new TaskCompletionSource<Task<JsonDocument?>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseCatch=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var clock=new ManualClock();var desktop=new NoAuthority();int reads=0,observers=0;JsonDocument? actualDocument=null;
        var session=new BrowserSession(desktop,timeProvider:clock)
        {
            ReadForTest=async (_,_)=>
            {
                reads++;entered.SetResult();await releaseRead.Task;
                // This actual protocol read is deliberately uncancellable, modelling the actual Console boundary already tested.
                actualDocument=await NativeMessagingCodec.ReadAsync(wire,CancellationToken.None);return actualDocument;
            },
            BeforeLateReadObserverForTest=async pending=>{catchEntered.SetResult(pending);await releaseCatch.Task;},
            LateReadObserverStartedForTest=pending=>{Assert.Same(catchEntered.Task.Result,pending);observers++;}
        };
        using var output=new MemoryStream();var run=session.RunAsync(Stream.Null,output);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.Equal(BrowserTiming.Handshake,clock.Due); // New explicit initial handshake budget; ownership assertions unchanged.
            clock.Fire(); // Actual deadline CTS/linked token/WaitAsync cancellation; no sleeps or guessed schedule.
            var pending=await catchEntered.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.False(pending.IsCompleted);
            if(completesBeforeCatchObservation)
            {
                releaseRead.SetResult();
                if(outcome=="fault")await Assert.ThrowsAsync<EndOfStreamException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(3)));
                else await pending.WaitAsync(TimeSpan.FromSeconds(3));
                Assert.True(pending.IsCompleted);if(outcome=="success")Assert.Equal("OfferImage",actualDocument!.RootElement.GetProperty("type").GetString());
            }
            releaseCatch.SetResult();Assert.Equal(3,await run.WaitAsync(TimeSpan.FromSeconds(3)));
            if(completesBeforeCatchObservation&&outcome=="success")Assert.Throws<ObjectDisposedException>(()=>actualDocument!.RootElement.GetProperty("type"));
            Assert.Equal(1,observers);Assert.NotNull(session.LateReadObserverForTest);
            if(!completesBeforeCatchObservation)releaseRead.SetResult();
            await session.LateReadObserverForTest!.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(1,reads);Assert.Equal(1,observers);Assert.Equal(0,desktop.Offers);Assert.Equal(0,desktop.Submissions);Assert.Equal(1,desktop.Disposals);
            if(outcome=="success")Assert.Throws<ObjectDisposedException>(()=>actualDocument!.RootElement.GetProperty("type"));
            else Assert.Null(actualDocument);
            Assert.Equal(outcome=="fault"?nameof(EndOfStreamException):null,session.LateReadFailureCode);
            output.Position=0;using var error=await NativeMessagingCodec.ReadAsync(output);Assert.Equal("Error",error!.RootElement.GetProperty("type").GetString());Assert.Equal("Timeout",error.RootElement.GetProperty("code").GetString());Assert.Null(await NativeMessagingCodec.ReadAsync(output));
        }
        finally
        {
            releaseRead.TrySetResult();releaseCatch.TrySetResult();await run.WaitAsync(TimeSpan.FromSeconds(3));
            if(session.LateReadObserverForTest!=null)await session.LateReadObserverForTest.WaitAsync(TimeSpan.FromSeconds(3));
            if(actualDocument!=null)
            {
                bool live;
                try{_=actualDocument.RootElement;live=true;}catch(ObjectDisposedException){live=false;}
                if(live)actualDocument.Dispose(); // Only baseline leaks need teardown; this cannot make the prior assertion pass.
            }
        }
    }
}
