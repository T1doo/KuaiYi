using System.Text.Json;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Providers.ApiProtocol;
namespace KuaiYi.Provider.Tests;
public sealed class TextTerminalFailureTests
{
    public static IEnumerable<object[]> Cases()
    {foreach(var p in Enum.GetValues<Protocol>())foreach(bool stream in new[]{false,true})foreach(var source in new[]{SelectionSourceKind.Uia,SelectionSourceKind.OleDrop,SelectionSourceKind.OcrOnly})foreach(string reason in new[]{"truncated","refused","interrupted","invalid"})yield return[p,stream,source,reason];}
    private static string Response(TextUsageRig r,string reason)
    {
        string finish=reason=="truncated"?(r.Protocol==Protocol.OpenAIChat?"length":"max_tokens"):reason=="refused"?(r.Protocol==Protocol.OpenAIChat?"content_filter":"refusal"):"PUBLIC_UNKNOWN_FINISH";
        if(reason=="invalid")return r.Stream?"data: {PUBLIC_INVALID_JSON}\n\n":"{PUBLIC_INVALID_JSON}";
        if(r.Stream)return r.StartFrames+r.EndFrames("PUBLIC PARTIAL",terminal:reason!="interrupted",finish:reason=="interrupted"?"PUBLIC_UNKNOWN_FINISH":finish);
        return r.Protocol==Protocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"PUBLIC PARTIAL\"},\"finish_reason\":\""+finish+"\"}]}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"PUBLIC PARTIAL\"}],\"stop_reason\":\""+finish+"\"}";
    }
    [Theory][MemberData(nameof(Cases))]
    public async Task SharedThreeSourceChainPersistsSafeReasonAndExposesActualRecoveryWithoutAutomaticSend(Protocol protocol,bool stream,SelectionSourceKind sourceKind,string reason)
    {
        await using var r=await TextUsageRig.Create(protocol,stream);var source=sourceKind==SelectionSourceKind.OcrOnly?await r.OcrSource():r.Source(kind:sourceKind);var id=r.Start(source);await r.Requests(1);var frozen=r.Version(id);await r.Script.Write(Response(r,reason),true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        var v=r.Version(id);Assert.False(v.Complete);Assert.True(v.Stopped);Assert.NotNull(v.Failure);r.Window.TickForTest();Assert.True(r.Window.Frames!.IsExpanded);if(reason=="truncated")Assert.Equal(TranslationFailureReason.OutputTruncated,v.Failure);if(reason=="refused"){Assert.Equal(TranslationFailureReason.ContentRefused,v.Failure);Assert.Equal("PUBLIC PARTIAL",v.Text);}if(reason=="invalid")Assert.Equal(TranslationFailureReason.InvalidProtocol,v.Failure);Assert.Null(frozen.Failure);Assert.Equal(v,r.Results.LastFailure);Assert.Equal(sourceKind,r.Flow.SelectedSnapshot!.Source.SourceKind);Assert.Contains("未完成",r.Window.ResultLabel);Assert.Contains("未完成",r.Window.FailureNoticeText);Assert.Equal(32,r.Window.FailureHeight);Assert.Equal(r.Window.FailureNoticeFull,r.Window.FailureAutomation);Assert.Contains("API",r.Window.FailureNoticeFull);Assert.Contains("不会自动重发",r.Window.FailureNoticeFull);Assert.Contains("本次会话版本",r.Window.FailureNoticeFull);Assert.DoesNotContain("PUBLIC_INVALID_JSON",r.Window.FailureNoticeFull);
        r.Window.OpenFailureRecoveryForTest();Assert.True(r.Window.RecoveryOpened);r.Window.CollapseForTest();Assert.True(r.Results.Reopen());r.Window.SelectFailureVersionForTest(id.VersionId);Assert.Equal(v,r.Version(id));Assert.Equal(1,r.Script.Requests);Assert.False(r.Gate.HasRunningGeneration);Assert.Contains("未完成",r.Window.UsageVersionMenuForTest(v));Assert.DoesNotContain(source.Text,r.Window.UsageVersionMenuForTest(v));
        using var body=JsonDocument.Parse(Assert.Single(r.Script.Bodies));Assert.Equal(4096,body.RootElement.GetProperty(protocol==Protocol.OpenAIChat?"max_completion_tokens":"max_tokens").GetInt32());Assert.DoesNotContain("image_url",r.Script.Body);Assert.DoesNotContain("base64",r.Script.Body);Assert.False(r.Results.Fail(id,"AUTH_FAILED"));Assert.Equal(v,r.Version(id));
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task ProviderErrorTextNeverEntersReasonBodyLabelAutomationOrMenu(Protocol protocol,bool stream)
    {
        await using var r=await TextUsageRig.Create(protocol,stream);var id=r.Start();await r.Requests(1);string untrusted="PUBLIC_SENTINEL_KEY_AND_HTML_<script>_ECHO";
        if(stream)await r.Script.Write((protocol==Protocol.AnthropicMessages?r.StartFrames:"")+"data: {\"type\":\"error\",\"error\":{\"message\":\""+untrusted+"\"}}\n\n",true);
        else await r.Script.Write("{\"error\":{\"message\":\""+untrusted+"\"}}",true);
        await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.NotNull(r.Version(id).Failure);Assert.Equal("",r.Results.Text);foreach(string text in new[]{r.Window.ResultLabel,r.Window.FailureNoticeFull,r.Window.FailureAutomation,r.Window.UsageVersionMenuForTest(r.Version(id)),r.Window.ButtonTip as string??""})Assert.DoesNotContain(untrusted,text);Assert.Equal(1,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task RollbackDisplaysOldBodyWithItsOwnMetadataAndSeparateFailedAttemptIncludingSource(Protocol protocol,bool sameSource)
    {
        await using var r=await TextUsageRig.Create(protocol);var first=r.Start();await r.Requests(1);await r.Reply(r.Counts(7,2),"PUBLIC OLD COMPLETE");var old=r.Version(first);var second=r.Start(r.Source("PUBLIC OTHER SOURCE",SelectionSourceKind.OleDrop),regenerate:sameSource);await r.Requests(2);await r.Script.Write(Response(r,"truncated"),true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        Assert.Equal(first.VersionId,r.Results.SelectedVersionId);Assert.Equal("PUBLIC OLD COMPLETE",r.Results.Text);Assert.Equal(old,r.Version(first));Assert.Equal(TranslationFailureReason.OutputTruncated,r.Version(second).Failure);Assert.Contains("正文为旧结果",r.Window.FailureNoticeText);Assert.Contains("此原因属于上述失败尝试",r.Window.FailureNoticeFull);Assert.DoesNotContain("未完成",r.Window.ResultLabel);Assert.Contains(sameSource?"选区":"拖放文字",r.Window.FailureNoticeFull);Assert.NotNull(r.Flow.SnapshotForVersion(second.VersionId));
        r.Window.SelectFailureVersionForTest(second.VersionId);Assert.Equal("PUBLIC PARTIAL",r.Results.Text);Assert.Equal(second.VersionId,r.Results.SelectedVersionId);Assert.Contains("未完成",r.Window.ResultLabel);Assert.Equal(sameSource?3:2,r.Flow.NextGeneration!.VersionNumber);var regen=r.Start(regenerate:true);Assert.Equal(second.SessionId,regen.SessionId);Assert.Null(r.Version(regen).Failure);Assert.Null(r.Results.LastFailure);Assert.Equal("",r.Window.FailureNoticeText);await r.Requests(3);await r.Reply(null,"PUBLIC MANUAL RECOVERY");Assert.True(r.Version(regen).Complete);Assert.Null(r.Version(regen).Failure);Assert.Equal(3,r.Script.Requests);Assert.InRange(r.Results.Versions.Count,1,3);
    }
    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ExplicitRefusalWithNoBodyRemainsKeyboardMenuSelectableAndCanBeManuallyRegenerated(Protocol protocol)
    {
        await using var r=await TextUsageRig.Create(protocol);var id=r.Start();await r.Requests(1);string response=protocol==Protocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"refusal\":\"PUBLIC_UNTRUSTED_REFUSAL_TEXT\"},\"finish_reason\":\"stop\"}]}":"{\"type\":\"message\",\"content\":[],\"stop_reason\":\"refusal\"}";await r.Script.Write(response,true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(TranslationFailureReason.ContentRefused,r.Version(id).Failure);Assert.Empty(r.Results.Text);Assert.Contains("没有可显示正文",r.Window.FailureNoticeFull);Assert.True(r.Results.SelectVersion(id.VersionId));Assert.True(r.Results.Reopen());Assert.DoesNotContain("PUBLIC_UNTRUSTED_REFUSAL_TEXT",r.Window.FailureNoticeFull);r.Window.SelectFailureVersionForTest(id.VersionId);Assert.Equal(1,r.Script.Requests);Assert.NotNull(r.Flow.PrepareRegeneration());Assert.Equal(1,r.Script.Requests);
    }
    [Theory][InlineData("stop")][InlineData("off")][InlineData("lock")][InlineData("clear")][InlineData("source")][InlineData("settings")][InlineData("newer")]
    public async Task QueuedFailureCannotAttachToCancelledClearedOrNewerVersion(string change)
    {
        await using var r=await TextUsageRig.Create();var id=r.Start();await r.Requests(1);r.Context.Hold=true;await r.Script.Write(Response(r,"truncated"),true);await TextUsageRig.Wait(()=>r.Context.Pending>0);
        switch(change){case "stop":r.Window.StopForTest();break;case "off":r.Gate.Disable();break;case "lock":r.Gate.SetSessionLocked(true);break;case "clear":r.Flow.Clear();break;case "source":r.Current=false;break;case "settings":r.Settings=r.Settings with{Version="CHANGED_PUBLIC_VERSION"};break;case "newer":r.Start(r.Source("PUBLIC NEW SOURCE"));break;}
        r.Context.Hold=false;r.Context.Drain();if(change=="newer"){await r.Requests(2);await r.Reply(null,"PUBLIC NEW COMPLETE");}else await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.DoesNotContain(r.Results.Versions,v=>v.Identity==id&&v.Failure!=null);Assert.Null(r.Results.LastFailure);Assert.DoesNotContain(r.Events,e=>e.Identity==id&&e.Presentation.Kind is OverlayResultEventKind.Delta or OverlayResultEventKind.Complete);Assert.Equal(change=="newer"?2:1,r.Script.Requests);Assert.False(r.Gate.HasRunningGeneration);
    }
    [Theory][InlineData("collapse")][InlineData("clear-before-flush")][InlineData("new-before-flush")]
    public async Task FailureRevealHonorsUserCollapseAndNewerPresentation(string action)
    {
        await using var r=await TextUsageRig.Create();var id=r.Start();await r.Requests(1);if(action=="collapse")r.Window.CollapseForTest();await r.Script.Write(Response(r,"refused"),true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));
        if(action=="clear-before-flush")await r.Window.ClearContentForTest();if(action=="new-before-flush")r.Start(r.Source("PUBLIC NEWER SOURCE"));r.Window.TickForTest();Assert.False(r.Window.Frames!.IsExpanded);
        if(action=="collapse"){Assert.True(r.Results.Reopen());r.Window.OpenForTest();Assert.True(r.Window.Frames.IsExpanded);Assert.Equal(1,r.Script.Requests);}else Assert.Equal("",r.Window.FailureNoticeText);
        if(action=="new-before-flush"){await r.Requests(2);await r.Reply(null,"PUBLIC NEWER COMPLETE");Assert.Null(r.Results.LastFailure);}
    }
    [Theory][InlineData("AUTH_FAILED")][InlineData("PUBLIC_UNTRUSTED_RAW_ERROR_<script>")][InlineData(null)]
    public void CoreReasonIsBoundedAndCannotBeChangedAfterTerminalClearOrNewVersion(string? code)
    {
        var gate=new RuntimeGate();var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));var results=new OverlayResults(gate);Assert.True(results.Begin(id));Assert.True(results.Fail(id,code));var value=Assert.Single(results.Versions);Assert.Equal(TranslationFailures.Normalize(code),value.Failure);Assert.False(results.Fail(id,"OUTPUT_TRUNCATED"));Assert.False(results.Append(id,"PUBLIC LATE"));Assert.False(results.Complete(id));Assert.Equal(value,Assert.Single(results.Versions));results.Clear();Assert.Null(results.LastFailure);Assert.False(results.Fail(id,"OUTPUT_TRUNCATED"));Assert.Empty(results.Versions);
    }

    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task ActualEofAfterPartialTextIsInterruptedAndNeverComplete(Protocol protocol)
    {
        await using var r=await TextUsageRig.Create(protocol,true);var id=r.Start();await r.Requests(1);string content=protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"PUBLIC EOF PARTIAL\"}}]}\n\n":"data: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"PUBLIC EOF PARTIAL\"}}\n\n";await r.Script.Write(r.StartFrames+content,true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(TranslationFailureReason.Interrupted,r.Version(id).Failure);Assert.False(r.Version(id).Complete);Assert.Equal("PUBLIC EOF PARTIAL",r.Results.Text);Assert.Contains("部分结果",r.Window.FailureNoticeFull);Assert.Equal(1,r.Script.Requests);
    }
    [Theory][InlineData(Protocol.OpenAIChat,401)][InlineData(Protocol.AnthropicMessages,403)][InlineData(Protocol.OpenAIChat,502)][InlineData(Protocol.AnthropicMessages,400)]
    public async Task HttpFailureProducesBoundedPersistentStatusAndNoAutomaticRecovery(Protocol protocol,int status)
    {
        await using var r=await TextUsageRig.Create(protocol);r.Script.StatusCode=status;var id=r.Start();await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(status is 401 or 403?TranslationFailureReason.Authentication:TranslationFailureReason.ProviderError,r.Version(id).Failure);Assert.Equal(1,r.Script.Requests);r.Window.TickForTest();Assert.True(r.Window.Frames!.IsExpanded);Assert.Contains("未完成",r.Window.FailureNoticeFull);Assert.Empty(r.Results.Text);
    }
    [Theory][InlineData(Protocol.OpenAIChat,false)][InlineData(Protocol.OpenAIChat,true)][InlineData(Protocol.AnthropicMessages,false)][InlineData(Protocol.AnthropicMessages,true)]
    public async Task FailedNotificationCanClearOrStartCWithoutOldFinallyWritingBack(Protocol protocol,bool replace)
    {
        await using var r=await TextUsageRig.Create(protocol);var first=r.Start();var old=r.Flow.Running;await r.Requests(1);RequestIdentity? newer=null;
        r.OnUpdate=u=>{if(u.Failure==null)return;r.OnUpdate=null;if(replace)newer=r.Start(r.Source("PUBLIC REENTRANT C"));else{r.Flow.Clear();r.Window.RefreshLabelForTest();}};
        await r.Script.Write(Response(r,"truncated"),true);await old.WaitAsync(TimeSpan.FromSeconds(6));if(replace){await r.Requests(2);Assert.NotNull(newer);Assert.True(r.Gate.CanApply(newer.Value));Assert.True(r.Gate.HasRunningGeneration);Assert.Null(r.Version(newer.Value).Failure);Assert.Null(r.Results.LastFailure);await r.Reply(null,"PUBLIC C COMPLETE");Assert.Equal(newer,r.Results.Identity);Assert.Equal("PUBLIC C COMPLETE",r.Results.Text);}else{Assert.Empty(r.Results.Versions);Assert.Null(r.Results.LastFailure);Assert.Equal("",r.Window.FailureNoticeText);}Assert.False(r.Gate.HasRunningGeneration);
    }

    [Theory][InlineData(Protocol.OpenAIChat)][InlineData(Protocol.AnthropicMessages)]
    public async Task OcrConfirmedFailureKeepsActualShellVersionInsteadOfClearingBackToLocalImage(Protocol protocol)
    {
        await using var r=await TextUsageRig.Create(protocol);byte[] pixels=Enumerable.Repeat((byte)255,256).ToArray();using var owner=new KuaiYi.Imaging.OwnedSourceImage(new KuaiYi.Imaging.ScreenshotPixels(new(0,0,8,8),pixels));var input=r.Window.ImageInputForTest();using(input.Intent)using(input.Cancellation){try{Assert.True(input.Commit(owner));}finally{r.Window.EndRestoreForTest(input.Cancellation);}}
        var prefix=new Uri(r.Settings.Data.Profiles[0].BaseUrl);Assert.True(r.Gate.TryEnable(new(prefix),true));var source=await r.OcrSource();var id=r.Start(source);await r.Requests(1);await r.Script.Write(Response(r,"truncated"),true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(id.VersionId,r.Window.SelectedSourceVersionForTest);Assert.Null(r.Window.SelectedImageForTest);Assert.Equal(SelectionSourceKind.OcrOnly,r.Flow.SelectedSnapshot!.Source.SourceKind);Assert.Equal(TranslationFailureReason.OutputTruncated,r.Version(id).Failure);Assert.Contains("OCR-only",r.Window.FailureNoticeFull);Assert.NotNull(r.Flow.PrepareRegeneration());Assert.False(owner.IsDisposed);Assert.True(await r.Window.ClearContentForTest());Assert.True(owner.IsDisposed);Assert.All(pixels,p=>Assert.Equal(0,p));Assert.Empty(r.Results.Versions);Assert.Equal("",r.Window.FailureNoticeText);Assert.Equal(1,r.Script.Requests);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ThreeVersionBoundAndOriginalOrdinalSurviveCrossSourceFailureAndExplicitRecovery(bool oldest)
    {
        await using var r=await TextUsageRig.Create();var ids=new List<RequestIdentity>();for(int n=1;n<=4;n++){var id=r.Start(regenerate:n>1);ids.Add(id);await r.Requests(n);await r.Reply(null,"PUBLIC COMPLETE "+n);}if(oldest)r.Window.SelectFailureVersionForTest(ids[1].VersionId);var selected=r.Results.SelectedVersionId;var failed=r.Start(r.Source("PUBLIC OTHER SOURCE"));await r.Requests(5);await r.Script.Write(Response(r,"truncated"),true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal(selected,r.Results.SelectedVersionId);Assert.Equal(3,r.Results.Versions.Count);Assert.Equal(5,r.Flow.NextGeneration!.VersionNumber);Assert.Equal(3,r.Flow.RetainedSnapshotCount);Assert.Contains("另一来源",r.Window.UsageVersionMenuForTest(r.Version(failed)));Assert.Equal(TranslationFailureReason.OutputTruncated,r.Version(failed).Failure);var next=r.Start(regenerate:true);Assert.Equal(5,r.Version(next).Number);Assert.Null(r.Version(next).Failure);Assert.InRange(r.Results.Versions.Count,1,3);await r.Requests(6);await r.Reply(null,"PUBLIC RECOVERY");Assert.True(r.Version(next).Complete);Assert.True(await r.Window.ClearContentForTest());Assert.Equal(0,r.Flow.RetainedSnapshotCount);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExplicitRefusalKeepsOnlyLegitimateContentAndReportedUsage(bool stream)
    {
        await using var r=await TextUsageRig.Create(Protocol.OpenAIChat,stream);var id=r.Start();await r.Requests(1);string message="{\"content\":\"PUBLIC LEGITIMATE PARTIAL\",\"refusal\":\"PUBLIC_REFUSAL_SENTINEL_MUST_NOT_DISPLAY\"}";string body=stream?"data: {\"choices\":[{\"index\":0,\"delta\":"+message+"}],\"usage\":{\"prompt_tokens\":14,\"completion_tokens\":3}}\n\n":"{\"choices\":[{\"index\":0,\"message\":"+message+",\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":14,\"completion_tokens\":3}}";await r.Script.Write(body,true);await r.Flow.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("PUBLIC LEGITIMATE PARTIAL",r.Results.Text);Assert.Equal(new TranslationUsage(14,3),r.Version(id).Usage);Assert.Equal(TranslationFailureReason.ContentRefused,r.Version(id).Failure);Assert.False(r.Version(id).Complete);Assert.DoesNotContain("PUBLIC_REFUSAL_SENTINEL",r.Window.FailureNoticeFull);Assert.DoesNotContain("PUBLIC_REFUSAL_SENTINEL",r.Results.Text);Assert.Equal(1,r.Script.Requests);
    }

}
