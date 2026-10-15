using KuaiYi.App;
using KuaiYi.Core;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class MarkdownLinkProductionTests
{
    private const string Link="[public](https://public.invalid/a?q=b#part)";
    private static ShellWindow Window()
    {var w=new ShellWindow();w.InitializeResultFlow();w.WithGeometry();var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));return w;}
    private static RequestIdentity Begin(ShellWindow w,string text=Link,Guid? session=null,bool complete=false)
    {Assert.True(w.MarkdownGate.TryStartRequest(session??Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));Assert.True(w.MarkdownResults.Begin(id));Assert.True(w.MarkdownResults.Append(id,text));if(complete)Assert.True(w.MarkdownResults.Complete(id));w.ReplaceResult();w.ResetMarkdownReadingForTest();w.RenderMarkdown();w.OpenForTest();w.RenderMarkdown();return id;}
    private static Hyperlink First(RichTextBox host)=>Assert.IsType<Hyperlink>(Assert.IsType<Paragraph>(host.Document.Blocks[0]).Inlines[0]);
    private static Table Table(RichTextBox host)=>Assert.IsType<Table>(Assert.Single(Assert.IsType<RichTextBox>(Assert.IsType<BlockUIContainer>(Assert.Single(host.Document.Blocks)).Child).Document.Blocks));
    private static Hyperlink Cell(Table table,int row,int column)=>Assert.IsType<Hyperlink>(Assert.IsType<Paragraph>(table.RowGroups[0].Rows[row].Cells[column].Blocks[0]).Inlines[0]);
    [Fact]public void ActualBodyConfirmsDomainOnlyOnExplicitActionWhileMeasurementCannotLaunch()
    {
        var w=Window();Begin(w);var link=First(w.MarkdownBody);var measured=First(w.MarkdownMeasurement);
        Assert.Empty(w.LinkConfirmations);Assert.Empty(w.LinkLaunches);Assert.Equal(1,link.HandlerCount);Assert.Equal(0,measured.HandlerCount);Assert.Null(link.NavigateUri);Assert.Null(measured.NavigateUri);Assert.Contains("目标域名：public.invalid",Assert.IsType<string>(link.ToolTip));
        measured.RaiseClick();Assert.Empty(w.LinkConfirmations);Assert.True(link.RaiseClick().Handled);
        Assert.Equal("public.invalid",Assert.Single(w.LinkConfirmations).Domain);Assert.Equal("https://public.invalid/a?q=b#part",Assert.Single(w.LinkLaunches).Url);Assert.False(w.Following);Assert.Equal(Link,w.MarkdownResults.Text);
    }
    [Fact]public void DeclineAndLaunchFailureDoNotMutateRawCopyOrAutomaticallyRetry()
    {
        var w=Window();Begin(w);var link=First(w.MarkdownBody);w.LinkConsent=false;link.RaiseClick();Assert.Single(w.LinkConfirmations);Assert.Empty(w.LinkLaunches);Assert.Equal(0,w.LinkErrors);
        w.LinkConsent=true;w.LinkLaunchError=new IOException("Synthetic browser failure");link.RaiseClick();Assert.Equal(2,w.LinkConfirmations.Count);Assert.Empty(w.LinkLaunches);Assert.Equal(1,w.LinkErrors);w.RenderMarkdown();Assert.Equal(2,w.LinkConfirmations.Count);Assert.Equal(Link,w.MarkdownResults.Text);
    }
    private static void Invalidate(ShellWindow w,string kind)
    {
        switch(kind)
        {
            case "cancel":w.MarkdownGate.Disable();break;
            case "stop":w.StopForTest();break;
            case "lock":w.ApplyLockForTest();break;
            case "exit":w.Exit();break;
            case "hide":w.LinkHidden();break;
            case "capture":w.LinkCapture();break;
            case "settings":w.MarkdownGate.SetSuspension(Suspension.Settings,true);break;
            case "collapse":w.CollapseForTest();break;
            case "peek":w.LinkPeek();break;
            case "reader":w.ObserveReaderInput();break;
            case "revision":w.ReplaceResult();break;
            case "detach":w.DropMarkdown();break;
            case "version":Begin(w,"[other](https://other.invalid)");break;
            case "clear":Assert.True(w.ClearContentForTest().GetAwaiter().GetResult());break;
            default:throw new ArgumentException(kind);
        }
    }
    [Theory]
    [InlineData("cancel")][InlineData("stop")][InlineData("lock")][InlineData("exit")][InlineData("hide")]
    [InlineData("capture")][InlineData("settings")][InlineData("collapse")][InlineData("peek")]
    [InlineData("reader")][InlineData("revision")][InlineData("detach")][InlineData("version")][InlineData("clear")]
    public void ConfirmationReentryRechecksActualShellVersionReaderAndLifecycle(string kind)
    {
        var w=Window();var id=Begin(w);var link=First(w.MarkdownBody);w.DuringLinkConfirmation=()=>Invalidate(w,kind);link.RaiseClick();Assert.Single(w.LinkConfirmations);Assert.Empty(w.LinkLaunches);Assert.Equal(0,w.LinkErrors);
        // Capture/settings suspend acquisition and links; existing request identity remains
        // valid by RuntimeGate contract. Cancellation/lock/clear reject late deltas.
        if(kind is "cancel" or "stop" or "lock" or "hide" or "clear")Assert.False(w.MarkdownResults.Append(id," late"));
    }
    [Theory][InlineData("lock")][InlineData("exit")][InlineData("hide")][InlineData("capture")][InlineData("settings")][InlineData("collapse")][InlineData("peek")][InlineData("detach")][InlineData("version")][InlineData("clear")]
    public void AlreadyIneligibleOrDetachedLinkNeverEvenShowsConfirmation(string kind)
    {var w=Window();Begin(w);var link=First(w.MarkdownBody);var late=link.CaptureClick();Invalidate(w,kind);late();Assert.Empty(w.LinkConfirmations);Assert.Empty(w.LinkLaunches);}
    [Fact]public void OffRetainedResultRequiresANewExplicitActionAfterCancelledConfirmation()
    {var w=Window();Begin(w);var link=First(w.MarkdownBody);w.DuringLinkConfirmation=()=>w.MarkdownGate.Disable();link.RaiseClick();Assert.Empty(w.LinkLaunches);w.DuringLinkConfirmation=null;link.RaiseClick();Assert.Single(w.LinkLaunches);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);}
    [Fact]public void TransientSuspensionReturningToEligibleStateStillInvalidatesThePendingConfirmation()
    {var w=Window();Begin(w);w.DuringLinkConfirmation=()=>{w.MarkdownGate.SetSuspension(Suspension.Capture,true);w.MarkdownGate.SetSuspension(Suspension.Capture,false);};First(w.MarkdownBody).RaiseClick();Assert.Single(w.LinkConfirmations);Assert.Empty(w.LinkLaunches);Assert.Equal(Suspension.None,w.MarkdownGate.Snapshot.Suspension);}
    [Fact]public void ConfirmationAllowsAnUnchangedStableLinkWhenOnlyTheFollowingTailGrows()
    {
        var w=Window();var id=Begin(w,Link+"\n\ntail");var stable=First(w.MarkdownBody);w.DuringLinkConfirmation=()=>{Assert.True(w.MarkdownResults.Append(id," grows"));w.RenderMarkdown();Assert.Same(stable,First(w.MarkdownBody));};stable.RaiseClick();Assert.Single(w.LinkLaunches);Assert.Equal(Link+"\n\ntail grows",w.MarkdownResults.Text);Assert.False(w.Following);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void ConfirmationDuringTableAppendAllowsStableCellAndRejectsReplacedCell(bool changed)
    {
        var w=Window();var id=Begin(w,Link+"|h\n---|---\nx|"+Link);var table=Table(w.MarkdownBody);var link=Cell(table,changed?1:0,changed?1:0);
        w.DuringLinkConfirmation=()=>{Assert.True(w.MarkdownResults.Append(id," grows"));w.RenderMarkdown();Assert.Same(table,Table(w.MarkdownBody));};link.RaiseClick();Assert.Single(w.LinkConfirmations);Assert.Equal(changed?0:1,w.LinkLaunches.Count);Assert.Equal(changed?0:1,link.HandlerCount);
    }
    [Fact]public void StableLinkSurvivesTailWhileReplacedTailAndClearReleaseCapturedClicks()
    {
        var w=Window();var id=Begin(w,Link+"\n\n"+Link);var stable=First(w.MarkdownBody);var tail=Assert.IsType<Hyperlink>(Assert.IsType<Paragraph>(w.MarkdownBody.Document.Blocks[^1]).Inlines[0]);var late=tail.CaptureClick();
        Assert.True(w.MarkdownResults.Append(id," grows"));w.RenderMarkdown();Assert.Same(stable,First(w.MarkdownBody));Assert.Equal(1,stable.HandlerCount);Assert.Equal(0,tail.HandlerCount);Assert.Empty(tail.Inlines);Assert.Null(tail.ToolTip);late();Assert.Empty(w.LinkConfirmations);
        var stableLate=stable.CaptureClick();w.DropMarkdown();Assert.Equal(0,stable.HandlerCount);Assert.Empty(stable.Inlines);stableLate();Assert.Empty(w.LinkLaunches);Assert.Empty(w.MarkdownMeasurement.Document.Blocks);
    }
    [Fact]public void NativeTableAllowsExplicitLinkWhileStableCellsSurviveAndChangedCellCallbacksExpire()
    {
        var w=Window();var id=Begin(w,Link+"|h\n---|---\n"+Link+"|"+Link);var table=Table(w.MarkdownBody);var stable=Cell(table,0,0);var old=Cell(table,1,1);var late=old.CaptureClick();Assert.Equal("SemiBold",table.RowGroups[0].Rows[0].Cells[0].Paragraph.FontWeight);
        stable.RaiseClick();Assert.Single(w.LinkLaunches);Assert.True(w.MarkdownResults.Append(id," grows"));w.RenderMarkdown();Assert.Same(table,Table(w.MarkdownBody));Assert.Same(stable,Cell(table,0,0));Assert.Equal(1,stable.HandlerCount);Assert.Equal(0,old.HandlerCount);late();Assert.Single(w.LinkConfirmations);
        Cell(table,1,1).RaiseClick();Assert.Equal(2,w.LinkLaunches.Count);var measured=Cell(Table(w.MarkdownMeasurement),1,1);Assert.Equal(0,measured.HandlerCount);Assert.Null(measured.NavigateUri);
        Assert.True(w.MarkdownResults.Append(id," [bad](file:///inert)"));w.RenderMarkdown();Assert.IsType<Paragraph>(Assert.Single(w.MarkdownBody.Document.Blocks));Assert.Equal(0,stable.HandlerCount);Assert.Empty(stable.Inlines);
    }
    [Fact]public async Task ActualCoordinatorThreeVersionSelectionStopClearAndLateEventsKeepLinksIsolated()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;w.WithGeometry();var ids=new List<RequestIdentity>();var links=new List<Hyperlink>();var callbacks=new List<Action>();
        for(int i=0;i<4;i++)
        {var id=await rig.Start(i>0);ids.Add(id);Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,$"[v{i}](https://public.invalid/{i})")));w.TickForTest();w.OpenForTest();w.TickForTest();var link=First(w.MarkdownBody);links.Add(link);callbacks.Add(link.CaptureClick());if(i<3){w.StopForTest();await rig.Flow.Running;w.TickForTest();}}
        Assert.Equal(3,w.MarkdownResults.Versions.Count);Assert.True(w.MarkdownResults.SelectVersion(ids[1].VersionId));w.ReplaceResult();w.RenderMarkdown();w.OpenForTest();var selected=First(w.MarkdownBody);
        foreach(var old in callbacks)old();Assert.Empty(w.LinkConfirmations);selected.RaiseClick();Assert.Equal("https://public.invalid/1",Assert.Single(w.LinkLaunches).Url);Assert.Equal("[v1](https://public.invalid/1)",w.MarkdownResults.Text);
        w.StopForTest();await rig.Flow.Running;w.TickForTest();Assert.Equal(ids[1].VersionId,w.MarkdownResults.SelectedVersionId);Assert.False(await w.ReceivePresentationAsync(ids[3],new(OverlayResultEventKind.Delta," late")));Assert.False(await w.ReceivePresentationAsync(ids[0],new(OverlayResultEventKind.Complete)));
        var lateSelected=selected.CaptureClick();Assert.True(await w.ClearContentForTest());lateSelected();Assert.Single(w.LinkLaunches);Assert.Empty(w.MarkdownResults.Versions);Assert.Empty(w.MarkdownBody.Document.Blocks);Assert.Empty(w.MarkdownMeasurement.Document.Blocks);Assert.False(await w.ReceivePresentationAsync(ids[3],new(OverlayResultEventKind.Delta,Link)));
    }
}
