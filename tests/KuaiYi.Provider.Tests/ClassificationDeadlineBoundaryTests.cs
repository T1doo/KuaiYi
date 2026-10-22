using System.Text.Json;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotTranslationTests
{
    private static string ClassificationTextEvent(Protocol protocol,string text)
        =>"data: "+(protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,delta=new{content=text}}}}):JsonSerializer.Serialize(new{type="content_block_delta",index=0,delta=new{type="text_delta",text}}))+"\n\n";
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ClassificationDeadlineStopsContinuousEffectiveTrafficAtThirtySeconds(Protocol protocol)
    {
        var clock=new PhaseClock();var queue=new ClassificationQueue();await using var r=await Rig.Create(protocol,stream:true,auth:true,clock:clock,phaseClock:clock,destination:queue);LongImageBudget(r);
        Assert.True(r.Flow.Submit(r.Offer(),true));var original=r.Flow.Running;
        await Until(()=>queue.Pending>0);queue.One();await r.Requests(1);await Until(()=>queue.Pending>0);queue.One(); // State, then HTTP Started.
        string start=protocol==Protocol.OpenAIChat?"":"data: {\"type\":\"message_start\",\"message\":{}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\n";
        await r.Script.Write(start+ClassificationTextEvent(protocol,"{"));await Until(()=>queue.Pending>0);queue.One();
        foreach(var duration in new[]{10000,10000,9999})
        {clock.Advance(TimeSpan.FromMilliseconds(duration));await r.Script.Write(ClassificationTextEvent(protocol," "));await Until(()=>queue.Pending>0);queue.One();Assert.False(original.IsCompleted);}
        clock.Advance(TimeSpan.FromMilliseconds(1));await Until(()=>original.IsCompleted,queue.Drain);await original;Assert.Equal(1,r.Script.Requests);Assert.Equal(1,r.SecretReads);Assert.Equal(TranslationFailureReason.RequestTimeout,Assert.Single(r.Results.Versions).Failure);Assert.Null(r.Flow.SelectedSnapshot!.Classification);Assert.Null(r.Flow.SelectedSnapshot.GenerationUsage);Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Generating);
    }
    private sealed class ClassificationQueue:SynchronizationContext
    {
        private readonly Queue<Action> pending=[];internal int Pending{get{lock(pending)return pending.Count;}}
        public override void Post(SendOrPostCallback callback,object? state){lock(pending)pending.Enqueue(()=>callback(state));}
        internal void One(){Action action;lock(pending)action=pending.Dequeue();action();}
        internal void Drain(){while(Pending>0)One();}
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ClassificationDeadlineRechecksQueuedCompletedAtExactBoundary(Protocol protocol)
    {
        var clock=new PhaseClock();var queue=new ClassificationQueue();await using var r=await Rig.Create(protocol,auth:true,destination:queue,clock:clock);LongImageBudget(r);
        Assert.True(r.Flow.Submit(r.Offer(),true));var original=r.Flow.Running;
        await Until(()=>queue.Pending>0);queue.One(); // Prepared/Classifying synthetic state.
        await r.Requests(1);await Until(()=>queue.Pending>0);queue.One(); // Actual HTTP Started.
        string text=ImageClassificationTests.Json();string body=protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason="end_turn"});
        await r.Script.Write(body,true);await Until(()=>queue.Pending>0);queue.One(); // Exact non-streaming TextDelta; no usage in this response.
        await Until(()=>queue.Pending>0);Assert.False(original.IsCompleted);Assert.Null(r.Flow.SelectedSnapshot!.Classification);
        clock.Advance(TimeSpan.FromSeconds(30),dispatch:false);queue.One(); // Completed was queued before the monotonic expiry.
        await Until(()=>original.IsCompleted,queue.Drain);await original;Assert.Equal(TranslationFailureReason.RequestTimeout,Assert.Single(r.Results.Versions).Failure);Assert.Null(r.Flow.SelectedSnapshot.Classification);Assert.Null(r.Flow.SelectedSnapshot.GenerationUsage);Assert.Equal(1,r.SecretReads);Assert.Equal(1,r.Script.Requests);Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Generating);
    }
    [Theory][InlineData("clear")][InlineData("off")][InlineData("replace")]
    public async Task ClassificationDeadlineStillJoinsHeldCredentialAndRejectsLateSource(string change)
    {
        var clock=new PhaseClock();await using var r=await Rig.Create(auth:true,clock:clock);LongImageBudget(r);var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        r.BeforeSecret=async _=>{entered.TrySetResult();await release.Task;};Task? cleanup=null;var raw=r.Raw;
        try
        {
            Assert.True(r.Flow.Submit(r.Offer(),true));var original=r.Flow.Running;await entered.Task.WaitAsync(TimeSpan.FromSeconds(6));clock.Advance(TimeSpan.FromSeconds(30));Assert.False(original.IsCompleted);
            if(change=="off")r.Gate.Disable();if(change=="replace")r.ReplaceImage();cleanup=r.Flow.ClearAsync();Assert.False(cleanup.IsCompleted);Assert.Equal(0,r.Script.Requests);release.TrySetResult();await cleanup.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(original.IsCompleted);Assert.Equal(0,r.Script.Requests);Assert.Equal(1,r.SecretReads);Assert.All(r.Leases,lease=>Assert.Throws<ObjectDisposedException>(()=>lease.HeaderValue()));Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Generating||u.Kind==ProductionScreenshotEvent.Completed);if(change=="replace")Assert.All(raw,b=>Assert.Equal(0,b));
        }
        finally{release.TrySetResult();if(cleanup!=null)await cleanup.WaitAsync(TimeSpan.FromSeconds(6));}
    }
}
public sealed class ImagePhaseBudgetTests
{
    [Fact]public void PhaseBudgetKeepsProfileIdentityAndCannotExtendItsParent()
    {
        var clock=new PhaseClock();var parent=new ProviderRequestBudget(120,clock);clock.Advance(TimeSpan.FromSeconds(20));var phase=parent.LimitPhase(30);Assert.Equal(120,phase.Seconds);Assert.Equal(TimeSpan.FromSeconds(30),phase.Remaining);using var deadline=phase.Link(default);
        clock.Advance(TimeSpan.FromMilliseconds(29999));Assert.False(deadline.IsCancellationRequested);clock.Advance(TimeSpan.FromMilliseconds(1));Assert.True(deadline.IsCancellationRequested);Assert.Throws<OperationCanceledException>(phase.Check);Assert.Equal(TimeSpan.FromSeconds(70),parent.Remaining);using var generation=parent.Link(default);Assert.False(generation.IsCancellationRequested);
    }
    [Fact]public void ShorterWholeBudgetWinsOverPhaseCap()
    {var clock=new PhaseClock();var parent=new ProviderRequestBudget(20,clock);var phase=parent.LimitPhase(30);using var deadline=phase.Link(default);Assert.Equal(20,phase.Seconds);clock.Advance(TimeSpan.FromSeconds(20));Assert.True(deadline.IsCancellationRequested);Assert.Equal(TimeSpan.Zero,phase.Remaining);Assert.Equal(TimeSpan.Zero,parent.Remaining);}
    [Fact]public void PhaseMonotonicFenceDoesNotDependOnTimerDispatch()
    {var clock=new PhaseClock();var parent=new ProviderRequestBudget(120,clock);var phase=parent.LimitPhase(30);using var deadline=phase.Link(default);clock.Advance(TimeSpan.FromSeconds(30),dispatch:false);Assert.False(deadline.Token.IsCancellationRequested);Assert.Throws<OperationCanceledException>(deadline.Check);Assert.Equal(TimeSpan.FromSeconds(90),parent.Remaining);Assert.Throws<ArgumentOutOfRangeException>(()=>parent.LimitPhase(0));Assert.Throws<ArgumentOutOfRangeException>(()=>parent.LimitPhase(601));}
}
