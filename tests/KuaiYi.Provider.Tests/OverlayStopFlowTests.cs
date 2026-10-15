using System.Collections.Concurrent;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using System.Windows.Controls;
using System.Windows.Documents;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class OverlayStopFlowTests
{
    private static ShellWindow Window()
    {
        var w=new ShellWindow();w.InitializeResultFlow();var origin=new EndpointOrigin(new Uri("https://public.invalid"));w.MarkdownGate.ConsentTo(origin);Assert.True(w.MarkdownGate.TryEnable(origin,true));return w;
    }
    private static async Task<(RequestIdentity Id,CancellationToken Cancellation)> Begin(ShellWindow w,string? source=null,Guid? session=null)
    {
        Assert.True(w.MarkdownGate.TryStartRequest(session??Guid.NewGuid(),Guid.NewGuid(),null,out var id,out var ct));Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Begin)));
        if(source is not null)Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,source)));return(id,ct);
    }
    private static TextBox Code(ShellWindow w)=>Assert.IsType<TextBox>(Assert.IsType<BlockUIContainer>(Assert.Single(w.MarkdownBody.Document.Blocks)).Child);
    private static string Plain(ShellWindow w)
    {
        var blocks=w.MarkdownBody.Document.Blocks;Assert.InRange(blocks.Count,1,2);
        if(blocks.Count==2)Assert.IsType<Expander>(Assert.IsType<BlockUIContainer>(blocks[1]).Child);
        return Assert.IsType<Run>(Assert.Single(Assert.IsType<Paragraph>(blocks[0]).Inlines)).Text;
    }
    private static void Drain(ShellWindow w)
    {for(int i=0;i<4&&w.FlushPending;i++)w.TickForTest();Assert.False(w.FlushPending);}
    private sealed class DeferredDestination:SynchronizationContext
    {
        private readonly Queue<Action> queue=[];
        public override void Post(SendOrPostCallback callback,object? state)=>queue.Enqueue(()=>callback(state));
        public void Drain(){while(queue.TryDequeue(out var callback))callback();}
    }
    [Fact]public async Task DrainedStopSchedulesRealTimerTickAndFinalizesNoLfClosingFence()
    {
        var w=Window();var (id,ct)=await Begin(w,"```cs\nx\n```");Drain(w);Assert.False(w.MarkdownModel.Blocks[0].Closed);Assert.Equal("x\n```",Code(w).Text);int starts=w.FlushStarts;long revision=w.ResultRevision;
        w.StopForTest();Assert.True(ct.IsCancellationRequested);Assert.False(w.MarkdownGate.CanApply(id));Assert.True(w.Dirty);Assert.True(w.FlushPending);Assert.Equal(starts+1,w.FlushStarts);Assert.Equal(revision,w.ResultRevision);Assert.False(w.PendingReveal);
        w.TickForTest();Assert.True(w.MarkdownModel.Blocks[0].Closed);Assert.Equal("x\n",Code(w).Text);Assert.Equal("```cs\nx\n```",w.MarkdownResults.Text);Assert.True(Assert.Single(w.MarkdownResults.Versions).Stopped);Assert.False(Assert.Single(w.MarkdownResults.Versions).Complete);
    }
    [Fact]public async Task PendingDeltaUsesOneExistingTimerAndRetainsEveryAcceptedCharacter()
    {
        var w=Window();var (id,_)=await Begin(w,"**accepted**");Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta," *tail")));int starts=w.FlushStarts;
        w.StopForTest();Assert.Equal(starts,w.FlushStarts);Assert.True(w.FlushPending);w.TickForTest();Assert.Equal("**accepted** *tail",w.MarkdownResults.Text);Assert.Equal("**accepted** *tail",Plain(w));Assert.False(w.Waiting);Assert.False(w.ProgressVisible);Assert.False(w.Awaiting);Assert.False(w.PendingReveal);
    }
    [Theory][InlineData("**unclosed")][InlineData("a|b\n---|")][InlineData("[inert](file:///fixture)")]
    public async Task UnclosedAndUnsupportedTerminalMarkdownKeepsExactLiteralSource(string source)
    {
        var w=Window();await Begin(w,source);Drain(w);w.StopForTest();Assert.True(w.FlushPending);w.TickForTest();Assert.Equal(source,w.MarkdownResults.Text);Assert.Equal(source,Plain(w));Assert.Equal(source,string.Concat(w.MarkdownModel.Blocks.Select(b=>b.Source)));
    }
    [Fact]public async Task NoLfTableSeparatorBecomesNativeOnlyAtLocalStopTerminalFlush()
    {
        var w=Window();await Begin(w,"a|b\n---|---");Drain(w);Assert.DoesNotContain(w.MarkdownModel.Blocks,b=>b.Kind==MarkdownBlockKind.Table);w.StopForTest();Assert.True(w.FlushPending);w.TickForTest();Assert.Equal(MarkdownBlockKind.Table,Assert.Single(w.MarkdownModel.Blocks).Kind);
        foreach(var host in new[]{w.MarkdownBody,w.MarkdownMeasurement}){var region=Assert.IsType<RichTextBox>(Assert.IsType<BlockUIContainer>(Assert.Single(host.Document.Blocks)).Child);Assert.IsType<Table>(Assert.Single(region.Document.Blocks));}Assert.Equal("a|b\n---|---",w.MarkdownResults.Text);
    }
    [Fact]public async Task TrulyUnclosedFenceKeepsCodeAndDoesNotInventClosingMarker()
    {
        var w=Window();await Begin(w,"```cs\nunfinished");Drain(w);w.StopForTest();Assert.True(w.FlushPending);w.TickForTest();Assert.False(Assert.Single(w.MarkdownModel.Blocks).Closed);Assert.Equal("unfinished",Code(w).Text);Assert.Equal("```cs\nunfinished",w.MarkdownResults.Text);
    }
    [Fact]public async Task EmptyActiveRequestStopsWaitingAndFlushesWithoutCreatingText()
    {
        var w=Window();var (_,ct)=await Begin(w);Drain(w);Assert.True(w.Awaiting);Assert.True(w.Waiting);w.StopForTest();Assert.True(ct.IsCancellationRequested);Assert.False(w.ProgressVisible);Assert.False(w.Waiting);Assert.True(w.FlushPending);w.TickForTest();Assert.Empty(w.MarkdownModel.Blocks);Assert.Empty(w.MarkdownResults.Text);Assert.True(Assert.Single(w.MarkdownResults.Versions).Stopped);
    }
    [Fact]public async Task RepeatedStopIsNoOpAndDoesNotScheduleOrInvalidateAgain()
    {
        var w=Window();await Begin(w,"partial");Drain(w);w.StopForTest();long epoch=w.MarkdownGate.Snapshot.Epoch;int starts=w.FlushStarts;object? tip=w.ButtonTip;w.StopForTest();Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);Assert.Equal(starts,w.FlushStarts);Assert.Equal(tip,w.ButtonTip);w.TickForTest();w.StopForTest();Assert.False(w.FlushPending);Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);Assert.Single(w.MarkdownResults.Versions);
    }
    [Fact]public void NoTaskStopChangesNothingAndCreatesNoFakeStoppedState()
    {
        var w=Window();long epoch=w.MarkdownGate.Snapshot.Epoch;w.StopForTest();Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);Assert.False(w.FlushPending);Assert.False(w.Dirty);Assert.Null(w.ButtonTip);Assert.Empty(w.ResultLabel);Assert.Empty(w.MarkdownResults.Versions);
    }
    [Fact]public async Task CompletedBeforeStopKeepsCompletedStatusAndHasNoStopSideEffects()
    {
        var w=Window();var (id,_)=await Begin(w,"complete");Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Complete)));w.MarkdownGate.MarkRequestTerminal(id);Drain(w);long epoch=w.MarkdownGate.Snapshot.Epoch;w.StopForTest();Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);Assert.False(w.FlushPending);var version=Assert.Single(w.MarkdownResults.Versions);Assert.True(version.Complete);Assert.False(version.Stopped);Assert.Null(w.ButtonTip);
    }
    [Theory][InlineData(OverlayResultEventKind.Begin)][InlineData(OverlayResultEventKind.Delta)][InlineData(OverlayResultEventKind.Complete)][InlineData(OverlayResultEventKind.Stopped)]
    public async Task QueuedEventsCannotCrossActualStopGate(OverlayResultEventKind kind)
    {
        var w=Window();var (id,_)=await Begin(w,"accepted");Drain(w);var destination=new DeferredDestination();w.ResultDestination(destination);Task<bool> late=w.ReceivePresentationAsync(id,new(kind," late"));Assert.False(late.IsCompleted);w.StopForTest();destination.Drain();Assert.False(await late);Assert.True(w.FlushPending);w.TickForTest();Assert.Equal("accepted",w.MarkdownResults.Text);Assert.True(Assert.Single(w.MarkdownResults.Versions).Stopped);
    }
    [Fact]public async Task NewBeginBeforeOldStopTickRendersCurrentStateOnly()
    {
        var w=Window();var (old,_)=await Begin(w,"```\nold\n```");Drain(w);w.StopForTest();var (current,_)=await Begin(w,"**new**");w.TickForTest();Assert.Equal(current.VersionId,w.MarkdownResults.SelectedVersionId);Assert.Equal("**new**",w.MarkdownResults.Text);Assert.Equal(MarkdownBlockKind.Paragraph,Assert.Single(w.MarkdownModel.Blocks).Kind);Assert.False(w.MarkdownResults.Versions.Single(v=>v.Identity==current).Stopped);Assert.False(await w.ReceivePresentationAsync(old,new(OverlayResultEventKind.Delta," late")));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task StopPreservesCollapsedAndExpandedGeometryWithoutAutoReveal(bool collapsed)
    {
        var w=Window();w.WithGeometry();await Begin(w,"```\nsource\n```");Drain(w);w.OpenForTest();Drain(w);if(collapsed)w.CollapseForTest();bool expanded=w.Frames!.IsExpanded;long revision=w.ResultRevision;w.StopForTest();w.TickForTest();Assert.Equal(expanded,w.Frames.IsExpanded);Assert.Equal(!collapsed,w.Frames.IsExpanded);Assert.Equal(revision,w.ResultRevision);Assert.False(w.PendingReveal);if(collapsed)Assert.Equal(Presentation.Compact,w.MarkdownGate.Snapshot.Presentation);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task StopKeepsPausedOuterAndNativeSelectionOffsets(bool table)
    {
        var w=Window();await Begin(w,table?"a|b\n---|---\nx|body":"```\nabcdef\n```");Drain(w);w.DrainResultDispatchForTest();w.ReaderScroll(145);var child=Assert.IsType<BlockUIContainer>(Assert.Single(w.MarkdownBody.Document.Blocks)).Child;
        if(child is TextBox code){code.RaiseWheel();code.Select(1,2);code.ScrollToHorizontalOffset(70);code.ScrollToVerticalOffset(20);}else{var view=Assert.IsType<RichTextBox>(child);view.RaiseWheel();view.Selection.Select(view.Document.ContentStart.GetPositionAtOffset(1)!,view.Document.ContentStart.GetPositionAtOffset(3)!);view.ScrollToHorizontalOffset(70);view.ScrollToVerticalOffset(20);}
        w.StopForTest();Assert.True(w.FlushPending);Assert.False(w.Following);w.TickForTest();Assert.True(w.NewContentVisible);Assert.False(w.Following);
        if(child is TextBox current){while(current.Dispatcher.Pending>0)current.Dispatcher.Deliver();Assert.Equal((1,2),(current.SelectionStart,current.SelectionLength));Assert.Equal(70,current.HorizontalOffset);Assert.Equal(20,current.VerticalOffset);}else{var currentView=(RichTextBox)child;while(currentView.Dispatcher.Pending>0)currentView.Dispatcher.Deliver();Assert.Equal(1,currentView.Selection.Start.Offset);Assert.Equal(3,currentView.Selection.End.Offset);Assert.Equal(70,currentView.HorizontalOffset);Assert.Equal(20,currentView.VerticalOffset);}
        int calls=w.ScrollCalls;w.DrainResultDispatchForTest();Assert.Equal(calls+1,w.ScrollCalls);Assert.Equal(145,w.ScrollOffset);
    }
    [Fact]public async Task ExitingStopDoesNotScheduleNewUiOrMutateRequest()
    {
        var w=Window();await Begin(w,"accepted");Drain(w);long epoch=w.MarkdownGate.Snapshot.Epoch;w.Exit();w.StopForTest();Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);Assert.False(w.FlushPending);Assert.Null(w.ButtonTip);
    }

    internal sealed class RealCoordinatorRig:IAsyncDisposable
    {
        public ShellWindow Window {get;}=new();
        public ProviderClient Provider {get;}
        public TextTranslationCoordinator Flow {get;}
        public SettingsSnapshot Settings {get;}
        public ConcurrentQueue<TextTranslationUpdate> Updates {get;}=new();
        public int ConnectionAttempts;
        private TaskCompletionSource connected=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public RealCoordinatorRig()
        {
            Window.InitializeResultFlow();Settings=new(new(){ActiveProfileId="synthetic",Profiles=[new(){Id="synthetic",BaseUrl="https://public.invalid/v1/",Model="TEST_ONLY_STOP_MODEL",Authentication=ApiAuthentication.None}]},"synthetic",SettingsLoadState.Current);
            Window.MarkdownGate.ObserveSettingsMetadata(Settings);var origin=new EndpointOrigin(new Uri(Settings.Data.Profiles[0].BaseUrl));Window.MarkdownGate.ConsentTo(origin);Assert.True(Window.MarkdownGate.TryEnable(origin,true));
            Provider=new(null,null,null,async(_,ct)=>
            {
                Interlocked.Increment(ref ConnectionAttempts);
                // A pooled connection attempt can outlive a cancelled HTTP waiter. This
                // fixture holds no socket and must release its own hold on the request gate.
                var identity=Window.MarkdownResults.Identity!.Value;
                if(!Window.MarkdownGate.TryLinkRequestCancellation(identity,ct,out var linked))throw new OperationCanceledException(ct);
                using(linked){connected.TrySetResult();await Task.Delay(Timeout.Infinite,linked!.Token);return Stream.Null;}
            });
            Flow=new(Window.MarkdownGate,()=>Settings,(_,_)=>true,Provider,new Immediate(),Window.MarkdownResults,u=>{Updates.Enqueue(u);Window.CoordinatorUpdate(u);},(_,_,_)=>throw new InvalidOperationException("Fixed AuthNone fixture must never read secrets."));Window.UseTranslation(Flow,Settings);
        }
        private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
        public async Task<RequestIdentity> Start(bool regenerate=false,string source="Synthetic public source",string? rawSource=null)
        {
            connected=new(TaskCreationOptions.RunContinuationsAsynchronously);var snapshot=new SelectionSourceSnapshot(new(17,92823,132000,@"C:\Synthetic\fixture-editor.exe"),rawSource??source,source,[new(1,2,20,22)],[7,8],Settings.Version,source.Length,false);
            var offer=regenerate?Flow.PrepareRegeneration():Flow.Prepare(snapshot);Assert.NotNull(offer);Assert.Equal(TextSubmission.Started,Flow.Submit(offer));
            try{await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));}
            catch(TimeoutException error){throw new InvalidOperationException($"Synthetic connector did not enter version {offer.Snapshot.VersionNumber}; running completed={Flow.Running.IsCompleted}; updates={string.Join(',',Updates.Select(u=>$"{u.Snapshot.VersionNumber}:{u.Presentation.Kind}:{u.Failure}"))}",error);}
            return Window.MarkdownResults.Identity!.Value;
        }
        public async ValueTask DisposeAsync(){Window.MarkdownGate.Disable();await Flow.DisposeAsync();Provider.Dispose();}
    }
    [Fact]public async Task ActualCoordinatorCancellationPublishesNoStoppedButLocalStopUpdatesActualVersionLabel()
    {
        await using var rig=new RealCoordinatorRig();var w=rig.Window;var id=await rig.Start();Assert.StartsWith("生成版本 1",w.ResultLabel);Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"```\nx\n```")));Drain(w);
        w.StopForTest();await rig.Flow.Running.WaitAsync(TimeSpan.FromSeconds(5));Assert.DoesNotContain(rig.Updates,u=>u.Presentation.Kind==OverlayResultEventKind.Stopped);Assert.StartsWith("版本 1",w.ResultLabel);Assert.EndsWith(" · 已停止",w.ResultLabel);Assert.Contains("TEST_ONLY_STOP_MODEL",w.ResultLabel);Assert.Contains("base@1+faithful@1",w.ResultTip);Assert.True(w.FlushPending);w.TickForTest();Assert.True(w.MarkdownModel.Blocks[0].Closed);Assert.False(w.MarkdownGate.HasRunningGeneration);
    }
    [Fact]public async Task ActualSnapshotCacheKeepsSelectedOldVersionWhileCurrentGenerationStops()
    {
        await using var rig=new RealCoordinatorRig();var w=rig.Window;var ids=new List<RequestIdentity>();
        for(int i=0;i<4;i++){var id=await rig.Start(i>0);ids.Add(id);Assert.True(await w.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,"version "+i)));if(i<3){w.StopForTest();await rig.Flow.Running;Drain(w);}}
        Assert.Equal(3,w.MarkdownResults.Versions.Count);Assert.True(w.MarkdownResults.SelectVersion(ids[1].VersionId));w.RefreshLabelForTest();string label=w.ResultLabel;w.StopForTest();await rig.Flow.Running;w.TickForTest();Assert.Equal(ids[1].VersionId,w.MarkdownResults.SelectedVersionId);Assert.Equal("version 1",w.MarkdownResults.Text);Assert.Equal(label,w.ResultLabel);Assert.StartsWith("版本 2",w.ResultLabel);Assert.True(w.MarkdownResults.Versions.Single(v=>v.Identity==ids[3]).Stopped);Assert.False(await w.ReceivePresentationAsync(ids[3],new(OverlayResultEventKind.Complete)));Assert.Equal(3,w.MarkdownResults.Versions.Count);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task StoppingCurrentGenerationKeepsChosenCompletedVersionAndItsRealLabel(bool selectAfterStop)
    {
        await using var rig=new RealCoordinatorRig();var w=rig.Window;var first=await rig.Start();Assert.True(await w.ReceivePresentationAsync(first,new(OverlayResultEventKind.Delta,"completed old")));Assert.True(await w.ReceivePresentationAsync(first,new(OverlayResultEventKind.Complete)));w.MarkdownGate.MarkRequestTerminal(first);Drain(w);
        var second=await rig.Start(true);Assert.True(await w.ReceivePresentationAsync(second,new(OverlayResultEventKind.Delta,"new partial")));if(!selectAfterStop){Assert.True(w.MarkdownResults.SelectVersion(first.VersionId));w.RefreshLabelForTest();}
        w.StopForTest();await rig.Flow.Running;if(selectAfterStop){Assert.True(w.MarkdownResults.SelectVersion(first.VersionId));w.ReplaceResult();w.ResetMarkdownReadingForTest();w.RefreshLabelForTest();}string label=w.ResultLabel;Assert.StartsWith("版本 1",label);Assert.DoesNotContain("已停止",label);
        w.TickForTest();Assert.Equal(first.VersionId,w.MarkdownResults.SelectedVersionId);Assert.Equal("completed old",w.MarkdownResults.Text);Assert.Equal("completed old",string.Concat(w.MarkdownModel.Blocks.Select(b=>b.Source)));Assert.Equal("completed old",Plain(w));Assert.Equal(label,w.ResultLabel);Assert.True(w.MarkdownResults.Versions.Single(v=>v.Identity==first).Complete);Assert.False(w.MarkdownResults.Versions.Single(v=>v.Identity==first).Stopped);Assert.True(w.MarkdownResults.Versions.Single(v=>v.Identity==second).Stopped);
    }
    [Fact]public async Task ActualPendingConfirmationStopsWithoutLaunchingOrCreatingAnotherVersion()
    {
        await using var rig=new RealCoordinatorRig();var w=rig.Window;await rig.Start();w.StopForTest();await rig.Flow.Running;Drain(w);var prior=w.MarkdownResults.Identity;int attempts=rig.ConnectionAttempts;var source=new string('a',12001);
        var snapshot=new SelectionSourceSnapshot(new(17,92823,132000,@"C:\Synthetic\fixture-editor.exe"),source,source,[new(1,2,20,22)],[7,8],rig.Settings.Version,source.Length,true);var offer=rig.Flow.Prepare(snapshot);Assert.NotNull(offer);Assert.Equal(TextSubmission.ConfirmationRequired,rig.Flow.Submit(offer));Assert.True(w.MarkdownGate.HasRunningGeneration);Assert.Equal(prior,w.MarkdownResults.Identity);
        w.StopForTest();Assert.False(w.MarkdownGate.HasRunningGeneration);Assert.Equal(TextSubmission.Rejected,rig.Flow.Submit(offer,true));Assert.Equal(prior,w.MarkdownResults.Identity);Assert.Single(w.MarkdownResults.Versions);Assert.Equal(attempts,rig.ConnectionAttempts);Assert.True(w.FlushPending);w.TickForTest();
    }
    [Fact]public void ConnectionTestIsNotGenerationAndStopDoesNotCancelIt()
    {
        var w=Window();var settings=new SettingsSnapshot(new(){ActiveProfileId="synthetic",Profiles=[new(){Id="synthetic",BaseUrl="https://public.invalid/v1/",Model="TEST_ONLY",Authentication=ApiAuthentication.None}]},"synthetic",SettingsLoadState.Current);w.MarkdownGate.ObserveSettingsMetadata(settings);Assert.True(w.MarkdownGate.TryGetProfileBinding("synthetic",out var binding));Assert.True(w.MarkdownGate.TryStartConnectionTest(binding,new(new Uri("https://public.invalid")),w.MarkdownGate.Snapshot.Epoch,out var id,out var ct));long epoch=w.MarkdownGate.Snapshot.Epoch;
        w.StopForTest();Assert.Equal(epoch,w.MarkdownGate.Snapshot.Epoch);Assert.True(w.MarkdownGate.IsConnectionTest(id));Assert.False(ct.IsCancellationRequested);Assert.False(w.FlushPending);Assert.False(w.Dirty);Assert.Null(w.ButtonTip);w.MarkdownGate.Disable();
    }
    [Fact]public async Task ReaderInputAfterStopFlushWinsOverQueuedOuterRestore()
    {
        var w=Window();await Begin(w,"```\nx\n```");Drain(w);w.DrainResultDispatchForTest();w.ReaderScroll(145);w.StopForTest();w.TickForTest();w.ReaderScroll(275);w.DrainResultDispatchForTest();Assert.Equal(275,w.ScrollOffset);Assert.False(w.Following);
    }
}
