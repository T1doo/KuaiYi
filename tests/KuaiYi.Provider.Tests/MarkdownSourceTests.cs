using KuaiYi.App;
using KuaiYi.Core;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class MarkdownSourceTests
{
    private static Expander Source(RichTextBox host)=>Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(host.Document.Blocks[^1]).Child);
    private static TextBox Text(Expander source)=>Assert.IsType<TextBox>(source.Content);
    private static void Drain(ShellWindow w){for(int i=0;i<4&&w.FlushPending;i++)w.TickForTest();Assert.False(w.FlushPending);}
    [Fact]public void NativeSourceDefaultsCollapsedAndDisplaysExactInertRawTextOnlyWhenOpened()
    {
        const string raw=" \r\n**source** <script>file:///inert</script> ![x](https://public.invalid/x)\r\n ";int reads=0,changes=0;
        using var region=new MarkdownSourceRegion(raw,()=>reads++,()=>changes++,()=>true);Assert.False(region.View.IsExpanded);Assert.Empty(region.Text.Text);
        region.Constrain(220,100,20,System.Windows.Media.Brushes.Black);region.View.IsExpanded=true;Assert.Equal(raw,region.Text.Text);Assert.Equal(1,reads);Assert.Equal(1,changes);
        Assert.True(region.Text.IsReadOnly);Assert.False(region.Text.IsUndoEnabled);Assert.False(region.Text.AllowDrop);Assert.Equal(System.Windows.TextWrapping.Wrap,region.Text.TextWrapping);Assert.Equal(ScrollBarVisibility.Disabled,region.Text.HorizontalScrollBarVisibility);Assert.Equal(ScrollBarVisibility.Visible,region.Text.VerticalScrollBarVisibility);Assert.Equal(50,region.Text.MaxHeight);Assert.Equal(220,region.View.MaxWidth);Assert.Equal(20,region.Text.FontSize);
        region.View.IsExpanded=false;Assert.Empty(region.Text.Text);Assert.Equal(2,reads);Assert.Equal(2,changes);
    }
    [Theory][InlineData(1d,1d)][InlineData(388d,560d)][InlineData(16d,20d)]
    public void SourceAndTranslationShareFiniteViewportAndSourceHeightIsBounded(double width,double height)
    {
        var host=new RichTextBox();var view=new MarkdownFlowView(host);var version=Guid.NewGuid();view.SetSource(version,new string('a',48000));view.Apply(new RestrictedMarkdown().UpdateForTest("text"),15,new(width,height),version);
        var source=Source(host);var text=Text(source);Assert.Equal(Math.Max(1,width-17),source.MaxWidth);Assert.Equal(Math.Min(180,Math.Max(1,height/2)),text.MaxHeight);Assert.Equal(width,host.MaxWidth);Assert.Equal(height,host.MaxHeight);Assert.Empty(text.Text);view.Clear();
    }
    [Fact]public void StreamingUpdatesKeepNativeSourceAndItsSelectionAndInsertMarkdownBeforeFooter()
    {
        var host=new RichTextBox();var view=new MarkdownFlowView(host);var version=Guid.NewGuid();var parser=new RestrictedMarkdown();parser.Update(version,"stable\n\ntail",false);view.SetSource(version,"raw text");view.Apply(parser.Blocks,15,new(388,560),version);
        var source=Source(host);source.IsExpanded=true;var text=Text(source);text.Select(1,3);text.ScrollToVerticalOffset(35);var stable=host.Document.Blocks[0];
        parser.Update(version,"stable\n\ntail grows\n\nlast",false);view.SetSource(version,"raw text");view.Apply(parser.Blocks,20,new(260,400),version);
        Assert.Same(source,Source(host));Assert.Same(stable,host.Document.Blocks[0]);Assert.Equal(6,host.Document.Blocks.Count);Assert.Equal("last",Assert.IsType<Run>(Assert.Single(Assert.IsType<Paragraph>(host.Document.Blocks[^2]).Inlines)).Text);Assert.Equal((1,3),(text.SelectionStart,text.SelectionLength));Assert.Equal(35,text.VerticalOffset);Assert.Equal(20,text.FontSize);view.Clear();
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void VersionOrSourceReplacementClearsOldControlAndRejectsCapturedLateExpansion(bool sameVersion)
    {
        var host=new RichTextBox();int changes=0;var view=new MarkdownFlowView(host,sourceChanged:()=>changes++);var version=Guid.NewGuid();view.SetSource(version,"first");var old=Source(host);old.IsExpanded=true;var text=Text(old);Action late=old.CaptureExpand();Action input=text.CaptureWheel();
        view.SetSource(sameVersion?version:Guid.NewGuid(),"second");var current=Source(host);Assert.NotSame(old,current);Assert.False(current.IsExpanded);Assert.Empty(Text(current).Text);Assert.Empty(text.Text);Assert.Null(old.Content);Assert.Equal(0,old.Handlers);Assert.Equal(0,text.ReaderHandlerCount);int count=changes;late();input();Assert.Equal(count,changes);Assert.False(current.IsExpanded);view.Clear();
    }
    [Fact]public void ClearDisposesSourceAndDropsAllControlsAndOldEventDelegates()
    {
        int reads=0;var host=new RichTextBox();var view=new MarkdownFlowView(host,()=>reads++);view.SetSource(Guid.NewGuid(),"raw");var source=Source(host);source.IsExpanded=true;var text=Text(source);Action late=source.CaptureExpand();Action input=text.CaptureWheel();view.Clear();int count=reads;late();input();Assert.Equal(count,reads);Assert.Empty(host.Document.Blocks);Assert.Empty(text.Text);Assert.Equal(0,text.ReaderHandlerCount);Assert.Equal(0,source.Handlers);
    }
    [Fact]public void LockedOrExitingReadGuardPreventsNewExpansionAndLateInput()
    {
        bool canRead=true;int reads=0,changes=0;using var region=new MarkdownSourceRegion("raw",()=>reads++,()=>changes++,()=>canRead);canRead=false;region.View.IsExpanded=true;region.Text.RaiseWheel();Assert.False(region.View.IsExpanded);Assert.Empty(region.Text.Text);Assert.Equal(0,reads);Assert.Equal(0,changes);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)]
    public void SourceReaderInputsCallExistingPausePolicy(int input)
    {
        int reads=0;using var region=new MarkdownSourceRegion("raw",()=>reads++,null,()=>true);region.Expand();int before=reads;
        switch(input){case 0:region.Text.RaiseWheel();break;case 1:region.Text.RaiseMouse(MouseButton.Left);break;case 2:region.Text.RaiseMove(MouseButtonState.Pressed);break;case 3:region.Text.RaiseKey(Key.PageUp);break;case 4:region.Text.RaiseTouch();break;case 5:region.Text.RaiseTouchMove();break;case 6:region.Text.RaiseScroll();break;}
        Assert.Equal(before+1,reads);
    }
    [Fact]public async Task ActualSelectedSnapshotUsesRawSourceAndNeverChangesResultCopyOrMarkdownModel()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start(source:"normalized",rawSource:" raw\r\nsource ");Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"**result**")));Drain(w);
        var source=Source(w.MarkdownBody);Assert.False(source.IsExpanded);Assert.Empty(Text(source).Text);w.ShowSourceForTest();Assert.True(source.IsExpanded);Assert.Equal(" raw\r\nsource ",Text(source).Text);Assert.Equal("normalized",rig.Flow.SelectedSnapshot!.Source.Text);Assert.Equal("**result**",w.MarkdownResults.Text);Assert.Equal("**result**",Assert.Single(w.MarkdownModel.Blocks).Source);Assert.False(w.Following);Assert.True(w.FlushPending);
        w.TickForTest();Assert.Equal(" raw\r\nsource ",Text(Source(w.MarkdownMeasurement)).Text);Assert.NotSame(Source(w.MarkdownBody),Source(w.MarkdownMeasurement));Assert.False(Text(Source(w.MarkdownMeasurement)).Dispatcher.Pending>0);
    }
    [Fact]public async Task PendingDifferentSourceConfirmationAndRejectionKeepOriginalOfRetainedVersion()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start(source:"source A");Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result A")));w.StopForTest();await rig.Flow.Running;Drain(w);
        string b=new('b',12001);var selection=new SelectionSourceSnapshot(new(17,92823,132000,@"C:\Synthetic\fixture-editor.exe"),b,b,[new(1,2,20,22)],[7,8],rig.Settings.Version,b.Length,true);var offer=rig.Flow.Prepare(selection);Assert.NotNull(offer);Assert.Equal(KuaiYi.Providers.TextSubmission.ConfirmationRequired,rig.Flow.Submit(offer));w.ShowSourceForTest();Assert.Equal("source A",Text(Source(w.MarkdownBody)).Text);rig.Flow.RejectConfirmation(offer);w.TickForTest();Assert.Equal(id.VersionId,w.MarkdownResults.SelectedVersionId);Assert.Equal("source A",Text(Source(w.MarkdownBody)).Text);Assert.Equal(1,rig.ConnectionAttempts);
    }
    [Fact]public async Task DifferentSourceFailureRollbackUsesRetainedSnapshotAndStartsFolded()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var first=await rig.Start(source:"source A");Assert.True(await w.ReceivePresentationAsync(first,new(OverlayResultEventKind.Delta,"result A")));w.StopForTest();await rig.Flow.Running;Drain(w);w.ShowSourceForTest();var old=Source(w.MarkdownBody);var oldText=Text(old);
        var second=await rig.Start(source:"source B");Assert.Empty(oldText.Text);Assert.Null(old.Content);Assert.True(w.MarkdownResults.Fail(second));w.RenderMarkdown();var restored=Source(w.MarkdownBody);Assert.False(restored.IsExpanded);Assert.Equal(first.VersionId,w.MarkdownResults.SelectedVersionId);w.ShowSourceForTest();Assert.Equal("source A",Text(restored).Text);Assert.Equal("result A",w.MarkdownResults.Text);
    }
    [Fact]public async Task StopFinalizationPreservesExpandedSourceSelectionAndReadingPause()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start(source:"public source");Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"```\nx\n```")));Drain(w);w.ShowSourceForTest();var source=Source(w.MarkdownBody);var text=Text(source);text.Select(1,4);text.ScrollToVerticalOffset(42);w.StopForTest();await rig.Flow.Running;w.TickForTest();Assert.Same(source,Source(w.MarkdownBody));Assert.True(source.IsExpanded);Assert.Equal((1,4),(text.SelectionStart,text.SelectionLength));Assert.Equal(42,text.VerticalOffset);Assert.False(w.Following);Assert.True(w.MarkdownModel.Blocks[0].Closed);
    }
    [Fact]public async Task CollapseClearsPreviewControlsButOffReopenUsesOnlyRetainedSnapshot()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result")));Drain(w);w.ShowSourceForTest();var source=Source(w.MarkdownBody);var text=Text(source);w.CollapseForTest();Assert.Empty(text.Text);Assert.Null(source.Content);w.MarkdownGate.Disable();await rig.Flow.Running;int calls=rig.ConnectionAttempts;w.ShowSourceForTest();Assert.Equal("Synthetic public source",Text(Source(w.MarkdownBody)).Text);Assert.Equal(Activation.Off,w.MarkdownGate.Snapshot.Activation);Assert.Equal(calls,rig.ConnectionAttempts);Assert.Equal(id.VersionId,w.MarkdownResults.SelectedVersionId);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task LockOrExitRejectsExplicitSourceViewAndRenderDropsSource(bool exit)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result")));Drain(w);w.ShowSourceForTest();var source=Source(w.MarkdownBody);var text=Text(source);Action late=source.CaptureExpand();if(exit)w.Exit();else w.Lock();w.ShowSourceForTest();w.RenderMarkdown();Assert.Empty(text.Text);Assert.Null(source.Content);late();Assert.DoesNotContain(w.MarkdownBody.Document.Blocks,b=>b is BlockUIContainer block&&block.Child is Expander);
    }
    [Fact]public async Task SourceExpansionInvalidatesQueuedOuterEndAndMeasurementDoesNotRestoreFollow()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result")));Drain(w);w.ShowSourceForTest();w.ReaderScroll(275);w.DrainResultDispatchForTest();Assert.Equal(275,w.ScrollOffset);Assert.False(w.Following);w.RenderMarkdown(false);Assert.False(w.Following);Assert.Equal(275,w.ScrollOffset);
    }
    [Fact]public void NoSelectedSnapshotDoesNotExposeCapturedOrSyntheticResultAsOriginal()
    {
        var w=new ShellWindow();w.InitializeResultFlow();w.RefreshLabelForTest();w.ShowSourceForTest();Assert.Empty(w.MarkdownBody.Document.Blocks);Assert.False(w.FlushPending);Assert.False(w.OriginalSourceCommandEnabled);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task ExplicitSourceCommandRequestsCurrentFooterAfterLayoutAndFencesLaterOuterRestore(bool restoreFirst)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,new string('a',4000))));Drain(w);w.DrainResultDispatchForTest();w.ReaderScroll(0);w.ShowSourceForTest();var source=Source(w.MarkdownBody);var text=Text(source);Assert.Equal(0,source.BringIntoViewCalls);Assert.Equal(1,text.Dispatcher.Pending);w.TickForTest();int scrolls=w.ScrollCalls;if(restoreFirst)w.DrainResultDispatchForTest();text.Dispatcher.Deliver();Assert.Equal(1,source.BringIntoViewCalls);w.DrainResultDispatchForTest();Assert.Equal(scrolls+(restoreFirst?1:0),w.ScrollCalls);Assert.False(w.Following);
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)]
    public async Task SourceReadingTargetRejectsNewInputCollapseVersionLockAndExit(int replacement)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result")));Drain(w);w.ShowSourceForTest();var source=Source(w.MarkdownBody);var text=Text(source);Assert.Equal(1,text.Dispatcher.Pending);
        switch(replacement){case 0:w.ReaderScroll(275);break;case 1:w.CollapseForTest();break;case 2:await rig.Start(true);break;case 3:w.Lock();break;case 4:w.Exit();break;}
        while(text.Dispatcher.Pending>0)text.Dispatcher.Deliver();Assert.Equal(0,source.BringIntoViewCalls);
    }
    [Fact]public void MeasurementAndOrdinaryExpansionDoNotScheduleSourceReadingTarget()
    {
        using var region=new MarkdownSourceRegion("raw",null,null,()=>true);region.SetExpanded(true);Assert.Equal("raw",region.Text.Text);Assert.Equal(0,region.Text.Dispatcher.Pending);region.View.IsExpanded=false;region.View.IsExpanded=true;Assert.Equal(0,region.Text.Dispatcher.Pending);Assert.Equal(0,region.View.BringIntoViewCalls);
    }
    [Fact]public async Task SelectedRetainedVersionReopensItsOwnFoldWithoutNewNetworkOrFourthCachedVersion()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var ids=new List<RequestIdentity>();
        for(int i=0;i<4;i++){var id=await rig.Start(i>0,rawSource:" raw original ");ids.Add(id);Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result "+i)));w.StopForTest();await rig.Flow.Running;Drain(w);}
        w.ShowSourceForTest();var newest=Source(w.MarkdownBody);var oldText=Text(newest);int attempts=rig.ConnectionAttempts;Assert.True(w.MarkdownResults.SelectVersion(ids[1].VersionId));w.ReplaceResult();w.RenderMarkdown();var retained=Source(w.MarkdownBody);Assert.NotSame(newest,retained);Assert.Empty(oldText.Text);Assert.False(retained.IsExpanded);Assert.Equal(2,rig.Flow.SelectedSnapshot!.VersionNumber);w.ShowSourceForTest();Assert.Equal(" raw original ",Text(retained).Text);Assert.Equal("result 1",w.MarkdownResults.Text);Assert.Equal(3,w.MarkdownResults.Versions.Count);Assert.Equal(3,rig.Flow.RetainedSnapshotCount);Assert.Equal(attempts,rig.ConnectionAttempts);
    }
    [Fact]public async Task ExistingCoordinatorClearThenProductionBridgeDisposesEverySourceAndCannotReopenIt()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result")));Drain(w);w.ShowSourceForTest();var source=Source(w.MarkdownBody);var text=Text(source);rig.Flow.Clear();await rig.Flow.Running;w.RenderMarkdown();w.ShowSourceForTest();while(text.Dispatcher.Pending>0)text.Dispatcher.Deliver();Assert.Empty(w.MarkdownResults.Versions);Assert.Equal(0,rig.Flow.RetainedSnapshotCount);Assert.Empty(w.MarkdownBody.Document.Blocks);Assert.Empty(w.MarkdownMeasurement.Document.Blocks);Assert.Empty(text.Text);Assert.Equal(0,source.BringIntoViewCalls);Assert.Null(source.Content);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task RollbackBeforeFlushRejectsOldSourceEventsAndCommandUsingLiveSelectedVersion(bool deliverUpdate)
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var first=await rig.Start(source:"source A");Assert.True(await w.ReceivePresentationAsync(first,new(OverlayResultEventKind.Delta,"result A")));w.StopForTest();await rig.Flow.Running;Drain(w);
        var second=await rig.Start(source:"source B");Assert.True(await w.ReceivePresentationAsync(second,new(OverlayResultEventKind.Delta,"result B")));Drain(w);w.ShowSourceForTest();var source=Source(w.MarkdownBody);var text=Text(source);Action late=source.CaptureExpand();Assert.Equal("source B",text.Text);var snapshot=rig.Flow.SelectedSnapshot!;Assert.True(w.MarkdownResults.Fail(second));Assert.Equal(first.VersionId,w.MarkdownResults.SelectedVersionId);
        if(deliverUpdate){w.CoordinatorUpdate(new(second,new(OverlayResultEventKind.Stopped),snapshot,"STREAM_INTERRUPTED"));Assert.Empty(text.Text);Assert.Null(source.Content);}else{source.IsExpanded=false;late();Assert.Empty(text.Text);Assert.False(source.IsExpanded);}
        while(text.Dispatcher.Pending>0)text.Dispatcher.Deliver();Assert.Equal(0,source.BringIntoViewCalls);w.ShowSourceForTest();Assert.Equal("source A",Text(Source(w.MarkdownBody)).Text);
    }
    [Fact]public async Task ActualSourceToolLabelIsEnabledForOffRetainedResultAndDisabledOnLockAndClear()
    {
        await using var rig=new OverlayStopFlowTests.RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.True(w.OriginalSourceCommandEnabled);Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"result")));w.StopForTest();await rig.Flow.Running;w.MarkdownGate.Disable();w.RefreshLabelForTest();Assert.True(w.OriginalSourceCommandEnabled);w.Lock();w.RefreshLabelForTest();Assert.False(w.OriginalSourceCommandEnabled);rig.Flow.Clear();w.RefreshLabelForTest();Assert.False(w.OriginalSourceCommandEnabled);
    }
}
internal static class SourceTestMarkdown
{
    internal static IReadOnlyList<MarkdownBlock> UpdateForTest(this RestrictedMarkdown parser,string text){parser.Update(Guid.NewGuid(),text,true);return parser.Blocks;}
}
