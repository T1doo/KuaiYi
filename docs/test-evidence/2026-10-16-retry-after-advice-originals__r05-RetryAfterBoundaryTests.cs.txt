using System.Globalization;
using System.Net;
using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;

public sealed class RetryAfterBoundaryTests
{
    private static readonly DateTimeOffset Now=new(2026,10,16,12,0,0,TimeSpan.Zero);
    [Theory][InlineData("0",0)][InlineData("15",15)][InlineData("16",16)][InlineData("120",120)][InlineData("86400",86400)]
    public void DeltaSecondsAreNormalizedWithoutClampingTheAutomaticRetryLimit(string raw,int seconds)
    {using var response=Response(429,raw);Assert.Equal(Now.AddSeconds(seconds),RetryAfterParser.Read(response,Now)!.NotBeforeUtc);}
    [Theory][InlineData(-1)][InlineData(0)][InlineData(15)][InlineData(120)][InlineData(86400)][InlineData(86401)]
    public void HttpDatesRejectExpiredAndOutOfRangeAdvice(int seconds)
    {
        using var response=Response(503,Now.AddSeconds(seconds).ToString("R",CultureInfo.InvariantCulture));var advice=RetryAfterParser.Read(response,Now);
        if(seconds<0||seconds>86400)Assert.Null(advice);else Assert.Equal(Now.AddSeconds(seconds),advice!.NotBeforeUtc);
    }
    [Theory][InlineData(null)][InlineData("")][InlineData("-1")][InlineData("+120")][InlineData("1.5")][InlineData("1e2")][InlineData("86401")][InlineData("999999999999999999999999")][InlineData("120, 240")][InlineData("PUBLIC_PRIVATE_HEADER_SENTINEL")]
    public void MissingMalformedAndExcessiveValuesDoNotRetainUntrustedHeaderText(string? raw)
    {
        using var response=Response(429,raw);var advice=RetryAfterParser.Read(response,Now);Assert.Null(advice);Assert.Equal("null",JsonSerializer.Serialize(advice));
    }
    [Fact]public void HeaderLengthAndMultiplicityAreCheckedBeforeTypedParsing()
    {
        using var longValue=Response(429,new string(' ',130)+"120");Assert.Null(RetryAfterParser.Read(longValue,Now));
        using var duplicate=Response(429,null);Assert.True(duplicate.Headers.TryAddWithoutValidation("Retry-After",new[]{"120","240"}));Assert.Null(RetryAfterParser.Read(duplicate,Now));
        using var huge=Response(429,new string('0',RetryAfterParser.MaximumHeaderCharacters+1));Assert.Null(RetryAfterParser.Read(huge,Now));
    }
    [Theory][InlineData(200)][InlineData(400)][InlineData(401)][InlineData(403)][InlineData(500)]
    public void OnlyRateLimitAndUnavailableResponsesProduceAdvice(int status)
    {using var response=Response(status,"120");Assert.Null(RetryAfterParser.Read(response,Now));}
    [Fact]public void AdviceFactoryRejectsNegativeOversizedAndOverflowWithoutKeepingAnOffset()
    {
        Assert.Null(RetryAfterAdvice.FromDelay(Now,TimeSpan.FromTicks(-1)));Assert.Null(RetryAfterAdvice.FromDelay(Now,TimeSpan.FromSeconds(86401)));Assert.Null(RetryAfterAdvice.FromDelay(DateTimeOffset.MaxValue,TimeSpan.FromSeconds(1)));
        var offset=new DateTimeOffset(2026,10,16,14,0,0,TimeSpan.FromHours(2));var advice=RetryAfterAdvice.FromDelay(offset,TimeSpan.FromSeconds(120))!;Assert.Equal(Now.AddSeconds(120),advice.NotBeforeUtc);Assert.Equal(TimeSpan.Zero,advice.NotBeforeUtc.Offset);
    }
    public static IEnumerable<object[]> FinalResponses()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(int status in new[]{429,503})foreach(string? raw in new[]{"180",null,"PUBLIC_PRIVATE_HEADER_SENTINEL",new string('0',160)+"120","future-date","expired-date","out-of-range"})yield return [p,status,raw!];}
    [Theory][MemberData(nameof(FinalResponses))]
    public async Task OnlyTheFinalHttpResponseSuppliesAdviceIncludingAnExplicitUnknown(Protocol protocol,int status,string? finalHeader)
    {
        string? raw=finalHeader switch{"future-date"=>DateTimeOffset.UtcNow.AddSeconds(180).ToString("R",CultureInfo.InvariantCulture),"expired-date"=>DateTimeOffset.UtcNow.AddMinutes(-1).ToString("R",CultureInfo.InvariantCulture),"out-of-range"=>"86401",_=>finalHeader};
        await using var r=await TextUsageRig.Create(protocol);r.Script.ResponseForRequest=n=>n==1?new(429,"0"):new(status,raw);
        var started=DateTimeOffset.UtcNow;var id=r.Start();await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var ended=DateTimeOffset.UtcNow;var version=r.Version(id);
        Assert.Equal(2,r.Script.Requests);Assert.Equal(status==429?TranslationFailureReason.RateLimited:TranslationFailureReason.ProviderError,version.Failure);Assert.Equal(TranslationUsage.Unknown,version.Usage);Assert.False(version.Complete);Assert.Equal("",version.Text);
        if(finalHeader is "180" or "future-date")
        {var advice=Assert.IsType<RetryAfterAdvice>(version.RetryAfter);if(finalHeader=="180")Assert.InRange(advice.NotBeforeUtc,started.AddSeconds(180),ended.AddSeconds(180));else Assert.Equal(DateTimeOffset.ParseExact(raw!,"R",CultureInfo.InvariantCulture),advice.NotBeforeUtc);Assert.Contains("服务端建议不早于",r.Window.FailureNoticeFull);}
        else{Assert.Null(version.RetryAfter);Assert.DoesNotContain("服务端建议不早于",r.Window.FailureNoticeFull);}
        Assert.DoesNotContain("PUBLIC_PRIVATE_HEADER_SENTINEL",JsonSerializer.Serialize(version));Assert.DoesNotContain("PUBLIC_PRIVATE_HEADER_SENTINEL",JsonSerializer.Serialize(r.Events));Assert.Equal(2,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task EachFailureKeepsItsOwnAdviceBodyUsageSourceAndInspectionIntent(Protocol protocol,bool stream)
    {
        await using var r=await TextUsageRig.Create(protocol,stream);var a=r.Start();await r.Requests(1);await r.Reply(r.Counts(10,1),"PUBLIC A1");var savedA=r.Version(a);
        r.Script.ResponseForRequest=n=>n==2?new(429,"120"):n==3?new(429,"0"):n==4?new(503,"180"):new(200);
        var b=r.Start(r.Source("PUBLIC B",SelectionSourceKind.OleDrop));await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var savedB=r.Version(b);Assert.NotNull(savedB.RetryAfter);Assert.Equal(a.VersionId,r.Results.SelectedVersionId);Assert.Equal(savedA,r.Version(a));
        r.Window.SelectFailureVersionForTest(b.VersionId);string bNotice=r.Window.FailureNoticeFull;var b2=r.Start(regenerate:true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));var savedB2=r.Version(b2);
        Assert.Equal(4,r.Script.Requests);Assert.Equal(b.VersionId,r.Results.SelectedVersionId);Assert.Equal(savedB,r.Version(b));Assert.NotEqual(savedB.RetryAfter,savedB2.RetryAfter);Assert.Equal(TranslationFailureReason.ProviderError,savedB2.Failure);
        Assert.Contains(savedB2.RetryAfter!.NotBeforeUtc.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture),r.Window.FailureNoticeFull);Assert.Contains("新尝试未完成",r.Window.FailureNoticeText);
        r.Window.SelectFailureVersionForTest(b.VersionId);Assert.Equal(bNotice.Replace("另一来源尝试 · ","",StringComparison.Ordinal),r.Window.FailureNoticeFull);Assert.Equal(SelectionSourceKind.OleDrop,r.Flow.SelectedSnapshot!.Source.SourceKind);Assert.Equal(TranslationUsage.Unknown,r.Version(b).Usage);
        Assert.Contains(savedB.RetryAfter!.NotBeforeUtc.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture),r.Window.RetryAdviceMenuForTest(b.VersionId));Assert.Contains(savedB2.RetryAfter!.NotBeforeUtc.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture),r.Window.RetryAdviceMenuForTest(b2.VersionId));
        string stableNotice=r.Window.FailureNoticeFull;for(int n=0;n<20;n++){r.Window.RefreshLabelForTest();r.Window.OpenFailureRecoveryForTest();Assert.Equal(stableNotice,r.Window.FailureNoticeFull);}Assert.Equal(4,r.Script.Requests);
        var b3=r.Start(regenerate:true);Assert.Null(r.Version(b3).RetryAfter);Assert.Equal(3,r.Version(b3).Number);await r.Requests(5);await r.Reply(r.Counts(50,5),"PUBLIC B3 PARTIAL",finish:protocol==Protocol.OpenAIChat?"length":"max_tokens");
        Assert.Null(r.Version(b3).RetryAfter);Assert.Equal(new TranslationUsage(50,5),r.Version(b3).Usage);Assert.Equal(b.VersionId,r.Results.SelectedVersionId);Assert.DoesNotContain("服务端建议不早于",r.Window.FailureNoticeFull);
        r.Window.SelectFailureVersionForTest(b2.VersionId);Assert.Equal(savedB2,r.Version(b2));Assert.Contains("服务端建议不早于",r.Window.FailureNoticeFull);Assert.Equal(TranslationUsage.Unknown,r.Version(b2).Usage);
        Assert.InRange(r.Results.Versions.Count,1,3);Assert.Equal(r.Results.Versions.Count,r.Flow.RetainedSnapshotCount);Assert.Equal(5,r.Script.Requests);
        Assert.True(await r.Window.ClearContentForTest());Assert.Empty(r.Results.Versions);Assert.Empty(r.Results.RetainedVersionIds);Assert.Equal(0,r.Flow.RetainedSnapshotCount);Assert.Equal("",r.Window.FailureNoticeFull);
    }
    public static IEnumerable<object[]> Cancellations()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(string change in new[]{"off","stop","clear","replacement"})yield return [p,change];}
    [Theory][MemberData(nameof(Cancellations))]
    public async Task QueuedFinalAdviceCannotCrossCancellationOrReplacement(Protocol protocol,string change)
    {
        await using var r=await TextUsageRig.Create(protocol);var a=r.Start();await r.Requests(1);await r.Reply(r.Counts(10,1),"PUBLIC A1");
        r.Script.ResponseForRequest=n=>n==2?new(429,"120"):new(200);r.Context.Hold=true;var b=r.Start(r.Source("PUBLIC B"));var oldJob=r.Flow.Running;await TextUsageRig.Wait(()=>r.Context.Pending>0);Assert.Null(r.Version(b).RetryAfter);
        RequestIdentity? replacement=null;
        if(change=="off")r.Gate.Disable();else if(change=="stop"){r.Gate.StopGeneration();r.Results.MarkStopped();}else if(change=="clear")Assert.True(await r.Window.ClearContentForTest());else replacement=r.Start(r.Source("PUBLIC C"));
        r.Context.Hold=false;r.Context.Drain();await oldJob.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.DoesNotContain(r.Results.Versions,v=>v.RetryAfter!=null);Assert.DoesNotContain("服务端建议不早于",r.Window.FailureNoticeFull);Assert.False(r.Results.Fail(b,"RATE_LIMITED",RetryAfterAdvice.FromDelay(Now,TimeSpan.FromSeconds(500))));Assert.False(r.Results.ObserveUsage(b,999,999));
        if(replacement is {} c){await r.Requests(3);await r.Reply(r.Counts(30,3),"PUBLIC C1");Assert.Equal(c.VersionId,r.Results.SelectedVersionId);Assert.Null(r.Version(c).RetryAfter);Assert.Equal(new TranslationUsage(30,3),r.Version(c).Usage);}
        else if(change!="clear"){Assert.Contains(a.VersionId,r.Results.RetainedVersionIds);Assert.NotNull(r.Flow.SnapshotForVersion(a.VersionId));Assert.Equal(TranslationUsage.Unknown,r.Version(b).Usage);}
    }
    [Theory][InlineData(Protocol.OpenAIChat,429)][InlineData(Protocol.OpenAIChat,503)][InlineData(Protocol.AnthropicMessages,429)][InlineData(Protocol.AnthropicMessages,503)]
    public async Task SixteenSecondsRemainsBeyondTheAutomaticRetryLimit(Protocol protocol,int status)
    {
        await using var r=await TextUsageRig.Create(protocol);r.Script.ResponseForRequest=_=>new(status,"16");var id=r.Start();await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(1,r.Script.Requests);Assert.NotNull(r.Version(id).RetryAfter);Assert.Contains("服务端建议不早于",r.Window.FailureNoticeFull);Assert.False(r.Gate.HasRunningGeneration);
    }
    [Fact]public void DeadlineConversionDropsAdviceInsteadOfRelabelingItAsAWaitForATimeout()
    {
        var clock=new PhaseClock();var budget=new ProviderRequestBudget(1,clock);var advice=RetryAfterAdvice.FromDelay(Now,TimeSpan.FromSeconds(120));var original=new ModelEvent(ModelEventKind.Failed,Reason:"RATE_LIMITED",RetryAfter:advice);
        clock.Advance(TimeSpan.FromSeconds(1));var admitted=ProviderEventReceiver.Admit(new(default,original),original,budget);Assert.Equal("REQUEST_TIMEOUT",admitted.Reason);Assert.Null(admitted.RetryAfter);Assert.Null(admitted.Usage);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void FullIdentityGateAndNeutralAbandonKeepAdviceAttachedOnlyToItsOwnFailedVersion(bool abandon)
    {
        var gate=new RuntimeGate();var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));var results=new OverlayResults(gate);
        RequestIdentity Start(Guid session){Assert.True(gate.TryStartRequest(session,Guid.NewGuid(),null,out var id,out _));Assert.True(results.Begin(id));return id;}
        var a=Start(Guid.NewGuid());Assert.True(results.Append(a,"PUBLIC A"));Assert.True(results.ObserveUsage(a,10,1));Assert.True(results.Complete(a));var savedA=Assert.Single(results.Versions);
        var b=Start(Guid.NewGuid());Assert.True(results.Fail(b,"RATE_LIMITED",RetryAfterAdvice.FromDelay(Now,TimeSpan.FromSeconds(120))));var savedB=results.Versions.Single(v=>v.Identity==b);Assert.Equal(savedA,results.Versions.Single(v=>v.Identity==a));Assert.True(results.SelectVersion(b.VersionId));
        var b2=Start(b.SessionId);Assert.True(results.Append(b2,"PUBLIC B2"));Assert.True(results.ObserveUsage(b2,20,2));var hint=RetryAfterAdvice.FromDelay(Now,TimeSpan.FromSeconds(180));
        foreach(var forged in new[]{b2 with{SessionId=Guid.NewGuid()},b2 with{VersionId=Guid.NewGuid()},b2 with{RequestId=Guid.NewGuid()},b2 with{Epoch=b2.Epoch-1}})Assert.False(results.Fail(forged,"RATE_LIMITED",hint));
        Assert.True(abandon?results.Abandon(b2):results.Fail(b2,"OUTPUT_TRUNCATED",hint));Assert.Null(results.Versions.Single(v=>v.Identity==b2).RetryAfter);Assert.Equal(new TranslationUsage(20,2),results.Versions.Single(v=>v.Identity==b2).Usage);Assert.Equal(savedB,results.Versions.Single(v=>v.Identity==b));Assert.Equal(b.VersionId,results.SelectedVersionId);
        Assert.False(results.Fail(b,"RATE_LIMITED",hint));Assert.Equal(savedB,results.Versions.Single(v=>v.Identity==b));results.Clear();Assert.Empty(results.Versions);
    }
    private static HttpResponseMessage Response(int status,string? raw)
    {var response=new HttpResponseMessage((HttpStatusCode)status);if(raw!=null)Assert.True(response.Headers.TryAddWithoutValidation("Retry-After",raw));return response;}
}
