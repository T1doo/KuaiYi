using KuaiYi.App;
using KuaiYi.Core;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class OverlayCopyTests
{
    private static ShellWindow Window()
    {
        var w=new ShellWindow();w.InitializeResultFlow();var origin=new EndpointOrigin(new Uri("https://public.invalid"));
        w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));return w;
    }
    private static async Task<RequestIdentity> Begin(ShellWindow w,string text,Guid? session=null)
    {
        Assert.True(w.MarkdownGate.TryStartRequest(session??Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));
        Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Begin)));
        if(text.Length>0)Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,text)));
        return id;
    }
    [Fact]public async Task ExplicitCopyPreservesExactMarkdownUnicodeAndConfirmsSelectedCompleteVersion()
    {
        var w=Window();var id=await Begin(w,"**译文**\n```\nx😀\n```\n");
        Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Complete)));
        w.CopyResultForTest();Assert.Equal(w.MarkdownResults.Text,Assert.Single(w.ClipboardWrites));
        Assert.StartsWith("已复制版本 1 · 完整结果",w.ResultLabel);Assert.Contains("系统剪贴板历史可能保留",w.ResultTip);
    }
    [Theory][InlineData("stream")][InlineData("stop")][InlineData("fail")]
    public async Task StreamingStoppedAndFailedTextIsAlwaysMarkedPartial(string kind)
    {
        var w=Window();var id=await Begin(w,"partial");
        if(kind=="stop")w.StopForTest();
        if(kind=="fail")Assert.True(w.MarkdownResults.Fail(id,"NETWORK"));
        w.CopyResultForTest();Assert.Equal("partial",Assert.Single(w.ClipboardWrites));
        Assert.Contains("复制时为部分结果",w.ResultLabel);Assert.DoesNotContain("完整结果",w.ResultLabel);
    }
    [Fact]public async Task ReadingOldCompleteVersionCopiesOldTextWhileNewVersionStreams()
    {
        var w=Window();var old=await Begin(w,"old");Assert.True(await w.ReceivePresentationAsync(old,new(OverlayResultEventKind.Complete)));
        await Begin(w,"new partial",old.SessionId);w.SelectCopyVersionForTest(old.VersionId);
        w.CopyResultForTest();Assert.Equal("old",Assert.Single(w.ClipboardWrites));Assert.StartsWith("已复制版本 1 · 完整结果",w.ResultLabel);
    }
    [Fact]public async Task ClipboardExceptionRetainsResultAndAllowsOneExplicitRetryWithoutAutomaticWrites()
    {
        var w=Window();await Begin(w,"kept");w.ClipboardWriteForTest=_=>throw new InvalidOperationException("SYNTHETIC_PRIVATE_DO_NOT_DISPLAY");
        w.CopyResultForTest();Assert.Single(w.ClipboardWrites);Assert.Equal("kept",w.MarkdownResults.Text);
        Assert.Contains("复制未确认",w.ResultLabel);Assert.DoesNotContain("SYNTHETIC_PRIVATE",w.ResultLabel);
        w.ClipboardWriteForTest=null;w.CopyResultForTest();Assert.Equal(2,w.ClipboardWrites.Count);Assert.Contains("已复制",w.ResultLabel);
    }
    [Theory][InlineData("lock")][InlineData("hidden")][InlineData("capture")][InlineData("exit")][InlineData("empty")]
    public async Task PrivacyOrAbsentContentPreventsAnyClipboardWrite(string condition)
    {
        var w=Window();await Begin(w,condition=="empty"?"":"private");
        if(condition=="lock")w.MarkdownGate.SetSessionLocked(true);
        if(condition=="hidden")w.MarkdownGate.Disable(hide:true);
        if(condition=="capture")w.MarkdownGate.SetSuspension(Suspension.Capture,true);
        if(condition=="exit")w.ExitCopyForTest();
        w.CopyResultForTest();Assert.Empty(w.ClipboardWrites);Assert.DoesNotContain("已复制",w.ResultLabel);
    }
    [Theory][InlineData("append")][InlineData("complete")][InlineData("lock-unlock")][InlineData("capture-restore")][InlineData("clear")]
    public async Task ReentrancyCannotPublishSuccessAfterContentOrPrivacyChanges(string change)
    {
        var w=Window();var id=await Begin(w,"initial");
        w.ClipboardWriteForTest=_=>
        {
            if(change=="append")Assert.True(w.MarkdownResults.Append(id," tail"));
            if(change=="complete")Assert.True(w.MarkdownResults.Complete(id));
            if(change=="lock-unlock"){w.MarkdownGate.SetSessionLocked(true);w.MarkdownGate.SetSessionLocked(false);}
            if(change=="capture-restore"){w.MarkdownGate.SetSuspension(Suspension.Capture,true);w.MarkdownGate.SetSuspension(Suspension.Capture,false);}
            if(change=="clear")w.MarkdownResults.Clear();
        };
        w.CopyResultForTest();Assert.Single(w.ClipboardWrites);Assert.DoesNotContain("已复制",w.ResultLabel);
    }
    [Fact]public async Task SelectionAwayAndBackDuringNativeWriteDoesNotConfirmOldAttempt()
    {
        var w=Window();var a=await Begin(w,"a");Assert.True(await w.ReceivePresentationAsync(a,new(OverlayResultEventKind.Complete)));
        var b=await Begin(w,"b",a.SessionId);
        w.ClipboardWriteForTest=_=>{w.SelectCopyVersionForTest(a.VersionId);w.SelectCopyVersionForTest(b.VersionId);};
        w.CopyResultForTest();Assert.Single(w.ClipboardWrites);Assert.DoesNotContain("已复制",w.ResultLabel);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task NestedCopyCannotLeaveSuccessWhenOuterWriteReturnsOrThrows(bool throwsAfter)
    {
        var w=Window();await Begin(w,"reentrant");
        w.ClipboardWriteForTest=_=>
        {
            w.ClipboardWriteForTest=null;w.CopyResultForTest();
            if(throwsAfter)throw new InvalidOperationException("PUBLIC_OUTER_FAILURE");
        };
        w.CopyResultForTest();Assert.Equal(2,w.ClipboardWrites.Count);Assert.DoesNotContain("已复制",w.ResultLabel);
    }
    [Fact]public async Task ActualToolbarEligibilityUsesTheSamePrivacyGateAsTheCopyCommand()
    {
        var w=Window();w.AddCopyToolForTest();await Begin(w,"readable");w.RefreshLabelForTest();Assert.True(w.CopyToolEnabled);
        w.MarkdownGate.SetSuspension(Suspension.Capture,true);w.RefreshLabelForTest();Assert.False(w.CopyToolEnabled);
        w.CopyResultForTest();Assert.Empty(w.ClipboardWrites);
        w.MarkdownGate.SetSuspension(Suspension.Capture,false);w.MarkdownGate.Disable();w.RefreshLabelForTest();Assert.True(w.CopyToolEnabled);
    }
    [Fact]public async Task ExistingNoticeExpiresOnAppendSelectionOrClearAndOffDoesNotForbidLocalReading()
    {
        var w=Window();var id=await Begin(w,"text");w.CopyResultForTest();Assert.Contains("已复制",w.ResultLabel);
        Assert.True(w.MarkdownResults.Append(id," more"));w.RefreshLabelForTest();Assert.DoesNotContain("已复制",w.ResultLabel);
        w.MarkdownGate.Disable();w.CopyResultForTest();Assert.Equal(2,w.ClipboardWrites.Count);Assert.Contains("已复制",w.ResultLabel);
        w.MarkdownResults.Clear();w.RefreshLabelForTest();Assert.Empty(w.ResultLabel);
    }
    [Fact]public async Task ChangingCopyDisplayTextKeepsCommandEligibilityAndResetsWithoutAnotherWrite()
    {
        var w=Window();w.AddCopyToolForTest();var id=await Begin(w,"PUBLIC copy result");
        Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Complete)));
        w.CopyResultForTest();Assert.Equal("已复制",w.CopyToolLabel);Assert.Equal(1,w.CopyResetSchedules);
        w.MarkdownGate.SetSuspension(Suspension.Capture,true);w.RefreshLabelForTest();
        Assert.False(w.CopyToolEnabled);Assert.Equal("复制",w.CopyToolLabel);
        w.MarkdownGate.SetSuspension(Suspension.Capture,false);w.RefreshLabelForTest();Assert.True(w.CopyToolEnabled);
        w.CopyResultForTest();Assert.Equal("已复制",w.CopyToolLabel);
        w.ExpireCopyForTest();Assert.Equal("复制",w.CopyToolLabel);Assert.DoesNotContain("已复制",w.ResultLabel);
        Assert.Equal(2,w.ClipboardWrites.Count); // Timer expiry never retries the native write.
    }
    [Fact]public async Task AnOlderSuccessTimerCannotEraseANewerUnconfirmedCopy()
    {
        var w=Window();w.AddCopyToolForTest();await Begin(w,"PUBLIC content");w.CopyResultForTest();
        w.ClipboardWriteForTest=_=>throw new InvalidOperationException("synthetic write failure");w.CopyResultForTest();
        Assert.Equal("未确认",w.CopyToolLabel);w.ExpireCopyForTest();
        Assert.Equal("未确认",w.CopyToolLabel);Assert.Contains("复制未确认",w.ResultLabel);Assert.Equal(2,w.ClipboardWrites.Count);
    }
}
