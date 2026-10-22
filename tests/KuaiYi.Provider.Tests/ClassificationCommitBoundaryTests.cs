using KuaiYi.Core;
using KuaiYi.App;
using System.Text.Json;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotTranslationTests
{
    // Post intentionally returns only after advancing time. The actual canonical callback
    // finishes first, while its asynchronous caller cannot resume until Post returns.
    private sealed class CommitBoundaryDestination:SynchronizationContext
    {
        internal Action<int>? Before;
        internal Action<int>? After;
        internal int Count;
        public override void Post(SendOrPostCallback callback,object? state)
        {int index=++Count;Before?.Invoke(index);callback(state);After?.Invoke(index);}
    }
    private static Task ReplyClassificationWithoutUsage(Rig r,Protocol protocol)
    {string text=ImageClassificationTests.Json();return r.Script.Write(protocol==Protocol.OpenAIChat?JsonSerializer.Serialize(new{choices=new[]{new{index=0,message=new{content=text},finish_reason="stop"}}}):JsonSerializer.Serialize(new{type="message",content=new[]{new{type="text",text}},stop_reason="end_turn"}),true);}
    [Theory]
    [InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.AnthropicMessages,false)]
    [InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task TimelyCanonicalClassificationSurvivesDeadlineBeforePostReturns(Protocol protocol,bool dispatchTimer)
    {
        var clock=new PhaseClock();var destination=new CommitBoundaryDestination();
        await using var r=await Rig.Create(protocol,auth:true,clock:clock,destination:destination);LongImageBudget(r);
        int committedAtPost=0;r.BeforeSecret=read=>{if(read==1)clock.Advance(TimeSpan.FromMilliseconds(29999),dispatch:false);return Task.CompletedTask;};
        destination.After=index=>
        {
            if(committedAtPost==0&&r.Flow.SelectedSnapshot?.Classification!=null)
            {committedAtPost=index;clock.Advance(TimeSpan.FromMilliseconds(2),dispatch:dispatchTimer);}
        };
        Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await ReplyClassificationWithoutUsage(r,protocol);
        await Until(()=>r.Flow.Running.IsCompleted||r.Script.Requests==2);
        Assert.Equal(5,committedAtPost);Assert.Equal(2,r.Script.Requests);
        await r.Reply("PUBLIC COMMITTED CLASSIFICATION OUTPUT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.True(Assert.Single(r.Results.Versions).Complete);Assert.Equal(2,r.SecretReads);Assert.NotNull(r.Flow.SelectedSnapshot!.Classification);
    }
    [Theory]
    [InlineData(Protocol.OpenAIChat,"off")][InlineData(Protocol.AnthropicMessages,"off")]
    [InlineData(Protocol.OpenAIChat,"whole-budget")][InlineData(Protocol.AnthropicMessages,"whole-budget")]
    public async Task TimelyClassificationDoesNotBypassCancellationOrWholeBudgetBeforePostReturns(Protocol protocol,string change)
    {
        var clock=new PhaseClock();var destination=new CommitBoundaryDestination();
        await using var r=await Rig.Create(protocol,auth:true,clock:clock,destination:destination);LongImageBudget(r);
        bool committed=false;destination.After=_=>
        {
            if(committed||r.Flow.SelectedSnapshot?.Classification==null)return;
            committed=true;if(change=="off")r.Gate.Disable();else clock.Advance(TimeSpan.FromSeconds(120));
        };
        Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await ReplyClassificationWithoutUsage(r,protocol);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.True(committed);Assert.Equal(1,r.Script.Requests);Assert.Equal(1,r.SecretReads);
        Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Generating||u.Kind==ProductionScreenshotEvent.Completed);
        if(change=="whole-budget")Assert.Equal(TranslationFailureReason.RequestTimeout,Assert.Single(r.Results.Versions).Failure);
    }
    [Theory]
    [InlineData(Protocol.OpenAIChat,30000)][InlineData(Protocol.AnthropicMessages,30000)]
    [InlineData(Protocol.OpenAIChat,30001)][InlineData(Protocol.AnthropicMessages,30001)]
    public async Task CanonicalClassificationCommitAtOrAfterDeadlineStillRejected(Protocol protocol,int milliseconds)
    {
        var clock=new PhaseClock();var destination=new CommitBoundaryDestination();
        await using var r=await Rig.Create(protocol,auth:true,clock:clock,destination:destination);LongImageBudget(r);
        bool heldCommit=false;destination.Before=index=>
        {
            if(index!=5)return;
            Assert.Null(r.Flow.SelectedSnapshot!.Classification);heldCommit=true;
            clock.Advance(TimeSpan.FromMilliseconds(milliseconds),dispatch:false);
        };
        Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await ReplyClassificationWithoutUsage(r,protocol);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.True(heldCommit);Assert.Equal(1,r.Script.Requests);Assert.Equal(1,r.SecretReads);
        Assert.Null(r.Flow.SelectedSnapshot!.Classification);Assert.Null(r.Flow.SelectedSnapshot.GenerationUsage);
        Assert.Equal(TranslationFailureReason.RequestTimeout,Assert.Single(r.Results.Versions).Failure);
        Assert.DoesNotContain(r.Updates,u=>u.Kind==ProductionScreenshotEvent.Generating);
    }
}
