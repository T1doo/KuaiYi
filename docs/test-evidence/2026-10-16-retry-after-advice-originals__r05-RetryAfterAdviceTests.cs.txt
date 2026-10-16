using System.Globalization;
using KuaiYi.Core;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;

public sealed class RetryAfterAdviceTests
{
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task OversizedUnparseableHeaderDoesNotAddAdviceOrExpandExistingRetryBudget(Protocol protocol)
    {
        await using var r=await TextUsageRig.Create(protocol);r.Script.ResponseForRequest=_=>new(429,new string('0',160)+"120");
        var id=r.Start();await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(2,r.Script.Requests);Assert.Equal(TranslationFailureReason.RateLimited,r.Version(id).Failure);
        Assert.DoesNotContain("服务端建议不早于",r.Window.FailureNoticeFull);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task FinalRateLimitShowsBoundedAdviceAtTheExistingVersionStateEntry(Protocol protocol,bool date)
    {
        await using var r=await TextUsageRig.Create(protocol);var a=r.Start();await r.Requests(1);await r.Reply(r.Counts(10,3),"PUBLIC A1");var original=r.Version(a);
        var retryAt=DateTimeOffset.UtcNow.AddMinutes(2);string header=date?retryAt.ToString("R",CultureInfo.InvariantCulture):"120";
        r.Script.ResponseForRequest=_=>new(429,header);var b=r.Start(r.Source("PUBLIC SOURCE B",SelectionSourceKind.OleDrop));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(2,r.Script.Requests);Assert.Equal(a.VersionId,r.Results.SelectedVersionId);Assert.Equal(original,r.Version(a));Assert.Equal(TranslationFailureReason.RateLimited,r.Version(b).Failure);
        Assert.Contains("服务端建议不早于",r.Window.FailureNoticeFull);Assert.Contains("UTC",r.Window.FailureAutomation);Assert.Contains("不是倒计时",r.Window.FailureNoticeFull);Assert.Contains("手动重试",r.Window.FailureNoticeFull);
        if(date)Assert.Contains(retryAt.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture),r.Window.FailureNoticeFull);
        Assert.Contains("服务端建议不早于",r.Window.RetryAdviceMenuForTest(b.VersionId));Assert.DoesNotContain("服务端建议不早于",r.Window.RetryAdviceMenuForTest(a.VersionId));
        r.Window.SelectFailureVersionForTest(b.VersionId);Assert.Contains("服务端建议不早于",r.Window.FailureNoticeFull);Assert.Equal(TranslationUsage.Unknown,r.Version(b).Usage);Assert.Equal("",r.Results.Text);Assert.Equal(2,r.Script.Requests);
        Assert.True(await r.Window.ClearContentForTest());Assert.Equal("",r.Window.FailureNoticeFull);Assert.Empty(r.Results.Versions);
    }
}
