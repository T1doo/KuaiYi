using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionBrokerProcessTests
{
    private sealed class MonotonicClock:TimeProvider
    {public long Milliseconds=10000;public DateTimeOffset Utc=DateTimeOffset.UtcNow;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Interlocked.Read(ref Milliseconds);public override DateTimeOffset GetUtcNow()=>Utc;}
    private sealed class ExpiredStarter(MonotonicClock clock,int elapsed):FixtureStarter("normal")
    {public Process? Witness;public override Process Start(ProcessStartInfo info){clock.Milliseconds+=elapsed;var child=base.Start(info);Witness=Process.GetProcessById(child.Id);return child;}}
    [Fact]public async Task PreCancelledSelectionCreatesNoOwnedChild()
    {
        using var cancellation=new CancellationTokenSource();cancellation.Cancel();var starter=new FixtureStarter("normal");await using var client=new SelectionBrokerClient("public-fixture",starter);Assert.Equal(SelectionCaptureStatus.Cancelled,(await client.CaptureAsync(Gesture(),cancellation.Token)).Status);await client.DisposeAsync();Assert.Equal(0,starter.Starts);Assert.Null(starter.Child);Assert.Null(client.ExitState);
    }
    [Theory][InlineData(600)][InlineData(601)]
    public async Task CompletedStartupBeyondAbsoluteBoundaryRejectsBeforeReadingAndRetainsOwnedChildCleanup(int elapsed)
    {
        var clock=new MonotonicClock();var starter=new ExpiredStarter(clock,elapsed);await using var client=new SelectionBrokerClient("public-fixture",starter,clock);Assert.Equal(SelectionCaptureStatus.HardTimeout,(await client.CaptureAsync(Gesture(),default)).Status);using var witness=starter.Witness;Assert.NotNull(witness);await client.DisposeAsync();Assert.Null(client.ExitState);Assert.Equal(1,starter.Starts);Assert.True(witness.HasExited);
    }
    [Theory][InlineData(599,SelectionCaptureStatus.Success)][InlineData(600,SelectionCaptureStatus.HardTimeout)][InlineData(601,SelectionCaptureStatus.HardTimeout)]
    public async Task FinalValidationUsesAbsoluteMonotonicDeadlineWithoutTimerCallback(int elapsed,SelectionCaptureStatus expected)
    {
        var clock=new MonotonicClock();var starter=new FixtureStarter("normal");byte[]? held=null;await using var client=new SelectionBrokerClient("public-fixture",starter,clock){BeforeSelectionDeliveryForTest=payload=>{held=payload;clock.Milliseconds+=elapsed;clock.Utc=clock.Utc.AddDays(-3);}};
        var result=await client.CaptureAsync(Gesture(),default);Assert.Equal(expected,result.Status);Assert.Equal(expected==SelectionCaptureStatus.Success?"public selected\n  code":"",result.Text);Assert.NotNull(held);Assert.All(held!,b=>Assert.Equal(0,b));await client.DisposeAsync();Assert.Null(client.ExitState);Assert.Equal(1,starter.Starts);
    }
    [Fact]public async Task CancellationAtFinalValidationCannotDeliverDecodedSelection()
    {
        using var cancellation=new CancellationTokenSource();var starter=new FixtureStarter("normal");byte[]? held=null;await using var client=new SelectionBrokerClient("public-fixture",starter){BeforeSelectionDeliveryForTest=payload=>{held=payload;cancellation.Cancel();}};
        var result=await client.CaptureAsync(Gesture(),cancellation.Token);Assert.Equal(SelectionCaptureStatus.Cancelled,result.Status);Assert.Equal("",result.Text);Assert.NotNull(held);Assert.All(held!,b=>Assert.Equal(0,b));await client.DisposeAsync();Assert.Null(client.ExitState);Assert.Equal(1,starter.Starts);
    }
    private class FixtureStarter(string fault):IOwnedProcessStarter
    {
        public int Starts;public Process? Child;
        public virtual Process Start(ProcessStartInfo original)
        {
            Interlocked.Increment(ref Starts);string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.CaptureControlFixture/bin",configuration,"net10.0/KuaiYi.CaptureControlFixture.dll"));
            var info=OwnedProcessSession.Info(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",null);info.ArgumentList.Clear();info.ArgumentList.Add(assembly);info.ArgumentList.Add("--selection-fixture");info.ArgumentList.Add(fault);foreach(string argument in original.ArgumentList)info.ArgumentList.Add(argument);
            return Child=Process.Start(info)??throw new IOException("Public fixture start failed.");
        }
    }
    private static SelectionGesture Gesture(string application="public.exe")=>new(new(123,456,789,application),new(3,3),new(70,3),1,1);
    [Theory][InlineData("normal",SelectionCaptureStatus.Success)][InlineData("identity",SelectionCaptureStatus.ProtocolFailure)][InlineData("role",SelectionCaptureStatus.ProtocolFailure)][InlineData("source",SelectionCaptureStatus.ProtocolFailure)][InlineData("runtime",SelectionCaptureStatus.ProtocolFailure)][InlineData("bounds",SelectionCaptureStatus.ProtocolFailure)][InlineData("oversize",SelectionCaptureStatus.ProtocolFailure)][InlineData("extra",SelectionCaptureStatus.ProtocolFailure)][InlineData("soft",SelectionCaptureStatus.SoftTimeout)][InlineData("error-text",SelectionCaptureStatus.EmptyOrUnsupported)][InlineData("crash",SelectionCaptureStatus.Crash)]
    public async Task ActualOwnedFixtureEnforcesSelectionWireAndIdentity(string fault,SelectionCaptureStatus expected)
    {
        var starter=new FixtureStarter(fault);await using var client=new SelectionBrokerClient("public-fixture",starter);var result=await client.CaptureAsync(Gesture(),default);Assert.Equal(expected,result.Status);Assert.Equal(1,starter.Starts);
        if(expected==SelectionCaptureStatus.Success){Assert.Equal("public selected\n  code",result.Text);Assert.Equal(new[]{12,4},result.RuntimeId);Assert.Equal(new PixelRect(0,0,100,20),Assert.Single(result.Ranges));}else Assert.Equal("",result.Text);
    }
    [Theory][InlineData("hang")][InlineData("late")]
    public async Task HardDeadlineReturnsBeforeCleanupAndHeldChildActuallyExits(string fault)
    {
        var starter=new FixtureStarter(fault);var client=new SelectionBrokerClient("public-fixture",starter);var watch=Stopwatch.StartNew();var result=await client.CaptureAsync(Gesture(),default);Assert.Equal(SelectionCaptureStatus.HardTimeout,result.Status);Assert.True(watch.Elapsed<TimeSpan.FromSeconds(2));Assert.Equal("",result.Text);await client.DisposeAsync();Assert.Null(client.ExitState);Assert.Equal(1,starter.Starts);
    }
    [Fact]public async Task CancellationDuringActualChildWaitNeverAllowsLateText()
    {
        var starter=new FixtureStarter("cancel");await using var client=new SelectionBrokerClient("public-fixture",starter);using var cancellation=new CancellationTokenSource();var pending=client.CaptureAsync(Gesture(),cancellation.Token);
        using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(5));while(starter.Child==null)await Task.Delay(5,guard.Token);cancellation.Cancel();Assert.Equal(SelectionCaptureStatus.Cancelled,(await pending).Status);await client.DisposeAsync();Assert.Null(client.ExitState);
    }
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class DelayedStarter():FixtureStarter("hang")
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);public int Entries;
        public override Process Start(ProcessStartInfo info){Interlocked.Increment(ref Entries);Entered.TrySetResult();Release.Task.GetAwaiter().GetResult();return base.Start(info);}
    }
    [Fact]public async Task HardDeadlineDuringStartupRetainsOwnershipAndForbidsSecondWriterUntilLateChildSettles()
    {
        var starter=new DelayedStarter();var client=new SelectionBrokerClient("public-fixture",starter);var pending=client.CaptureAsync(Gesture(),default);await starter.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try{Assert.Equal(SelectionCaptureStatus.HardTimeout,(await pending).Status);Assert.Equal(SelectionCaptureStatus.CleanupPending,(await client.CaptureAsync(Gesture(),default)).Status);Assert.Equal(1,starter.Entries);Assert.Equal(0,starter.Starts);}
        finally{starter.Release.TrySetResult();await client.DisposeAsync();}
        Assert.Equal(1,starter.Starts);Assert.Null(client.ExitState);
    }
    [Fact]public async Task ThreeActualCrashesPauseOnlyThatApplicationAndResumeAfterThirtySeconds()
    {
        var starter=new FixtureStarter("crash");var clock=new Clock();await using var client=new SelectionBrokerClient("public-fixture",starter,clock);
        for(int i=0;i<3;i++)Assert.Equal(SelectionCaptureStatus.Crash,(await client.CaptureAsync(Gesture(),default)).Status);
        Assert.Equal(SelectionCaptureStatus.Paused,(await client.CaptureAsync(Gesture(),default)).Status);Assert.Equal(3,starter.Starts);
        Assert.Equal(SelectionCaptureStatus.Crash,(await client.CaptureAsync(Gesture("other.exe"),default)).Status);clock.Now=clock.Now.AddSeconds(31);Assert.Equal(SelectionCaptureStatus.Crash,(await client.CaptureAsync(Gesture(),default)).Status);Assert.Equal(5,starter.Starts);
    }
}
