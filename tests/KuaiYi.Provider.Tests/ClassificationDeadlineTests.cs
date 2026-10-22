using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotTranslationTests
{
    private static void LongImageBudget(Rig r)=>r.Edit(s=>s with{Profiles=s.Profiles.SetItem(1,s.Profiles[1] with{TimeoutSeconds=120})});
    private static async Task Until(Func<bool> condition,Action? pump=null)
    {using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(!condition()){guard.Token.ThrowIfCancellationRequested();pump?.Invoke();await Task.Yield();}}
    [Theory][InlineData(Protocol.OpenAIChat,30000)][InlineData(Protocol.AnthropicMessages,30000)][InlineData(Protocol.OpenAIChat,30001)][InlineData(Protocol.AnthropicMessages,30001)]
    public async Task ClassificationDeadlineRejectsLateCredentialWithoutTimerDelivery(Protocol protocol,int milliseconds)
    {
        var clock=new PhaseClock();await using var r=await Rig.Create(protocol,auth:true,clock:clock);LongImageBudget(r);
        r.BeforeSecret=_=>{clock.Advance(TimeSpan.FromMilliseconds(milliseconds),dispatch:false);return Task.CompletedTask;};
        Assert.True(r.Flow.Submit(r.Offer(),true));var original=r.Flow.Running;await Until(()=>original.IsCompleted||r.Script.Requests>0);
        Assert.Equal(0,r.Script.Requests);await original.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.SecretReads);Assert.All(r.Leases,lease=>Assert.Throws<ObjectDisposedException>(()=>lease.HeaderValue()));Assert.Equal(TranslationFailureReason.RequestTimeout,Assert.Single(r.Results.Versions).Failure);Assert.Null(r.Flow.SelectedSnapshot!.Classification);Assert.False(r.Image.IsDisposed);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ClassificationDeadlineDoesNotLeakIntoGenerationAfterTimelyCommit(Protocol protocol)
    {
        var clock=new PhaseClock();await using var r=await Rig.Create(protocol,auth:true,clock:clock);LongImageBudget(r);
        r.BeforeSecret=read=>{clock.Advance(TimeSpan.FromMilliseconds(read==1?29999:2),dispatch:false);return Task.CompletedTask;};
        Assert.True(r.Flow.Submit(r.Offer(),true));await r.Requests(1);await r.Reply(ImageClassificationTests.Json());await r.Requests(2);await r.Reply("PUBLIC TIMELY CLASSIFICATION OUTPUT");await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.True(Assert.Single(r.Results.Versions).Complete);Assert.Equal(2,r.SecretReads);Assert.NotNull(r.Flow.SelectedSnapshot!.Classification);
    }
}
