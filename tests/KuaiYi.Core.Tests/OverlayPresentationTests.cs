using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OverlayPresentationTests
{
    private static readonly LayoutPreferences Preferences=new(420,640);
    private static MonitorGeometry Monitor(double scale=1)=>new("primary",new(-800,-600,1200,1000),new(scale));
    private static OverlayFrames Frames(double scale=1)
    {var monitor=Monitor(scale);int size=monitor.Dpi.CeilingPixels(48);return new(new(-750,-550,-750+size,-550+size),monitor);}
    private static void Settle(OverlayFrames f){for(int i=0;i<500&&f.NeedsFrame;i++)f.Tick(TimeSpan.FromMilliseconds(16));Assert.False(f.NeedsFrame);}
    private static RuntimeGate Gate()
    {
        var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(new(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]},"public-version",SettingsLoadState.Current));var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));return gate;
    }
    private static RequestIdentity Start(RuntimeGate gate,Guid? session=null)
    {Assert.True(gate.TryGetProfileBinding("public",out var binding));Assert.True(gate.TryStartProfileRequest(binding,session??Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _));return id;}
    [Theory][InlineData(1.0)][InlineData(1.25)][InlineData(1.5)][InlineData(2.0)]
    public void ActualPointerDpiRebaseKeepsGestureAndReleasedInsteadOfClick(double scale)
    {
        var gesture=new OverlayPointerGesture();var frames=Frames();Assert.Equal(OverlayPointerAction.Pressed,gesture.Down(frames.Anchor.Center,frames.Anchor,true));
        Assert.Equal(OverlayPointerAction.Moved,gesture.Move(new(-700,-500),out var desired,4,4,new(1)));frames.BeginMove();frames.Move(desired,Monitor(scale));var cursor=frames.Anchor.Center;
        Assert.True(gesture.Rebase(cursor,frames.Anchor,true));Assert.True(gesture.Active);Assert.True(gesture.HasMoved);
        var legal=frames.Anchor;Assert.Equal(OverlayPointerAction.Moved,gesture.Move(new(cursor.X+20,cursor.Y+10),out desired,4,4,new(scale)));Assert.Equal(new PixelPoint(legal.Left+20,legal.Top+10),desired);
        frames.Move(desired,Monitor(scale));Assert.Equal(OverlayPointerAction.Released,gesture.Up());Assert.False(gesture.Active);Assert.Equal(OverlayPointerAction.Ignored,gesture.Up());
        var saved=OverlayPositionMapper.Capture(frames.Anchor,frames.Monitor);Assert.Equal(frames.Anchor,OverlayPositionMapper.Restore(saved,[frames.Monitor],frames.Monitor.Id));
    }
    [Fact]public void DpiGeometryChangeDuringPressCannotBecomeActivationClickAndCancelledPointerCannotRebase()
    {
        var gesture=new OverlayPointerGesture();var f=Frames();gesture.Down(f.Anchor.Center,f.Anchor,true);Assert.True(gesture.Rebase(f.Anchor.Center,f.Anchor,true));Assert.Equal(OverlayPointerAction.Released,gesture.Up());
        gesture.Down(f.Anchor.Center,f.Anchor,true);gesture.Cancel();Assert.False(gesture.Rebase(f.Anchor.Center,f.Anchor,true));Assert.Equal(OverlayPointerAction.Ignored,gesture.Up());
    }
    [Theory][InlineData("lostCapture")][InlineData("removedMonitor")][InlineData("lock")]
    public void CancelAndTopologyRemovalSettleWithoutClickOrOutOfBounds(string reason)
    {
        var gate=Gate();var gesture=new OverlayPointerGesture();var frames=Frames();frames.Open(320,Preferences);Settle(frames);gesture.Down(frames.Anchor.Center,frames.Anchor,true);frames.BeginMove();frames.Move(new(1100,900),Monitor());gesture.Rebase(frames.Anchor.Center,frames.Anchor,true);
        gesture.Cancel();if(reason=="lock")gate.SetSessionLocked(true);else frames.Reanchor(OverlayPositionMapper.Restore(null,[Monitor()],"primary"),Monitor(),Preferences);Settle(frames);
        Assert.Equal(OverlayPointerAction.Ignored,gesture.Up());Assert.True(frames.Current.Contains(frames.Anchor));Assert.True(frames.Monitor.WorkArea.Inset(frames.Monitor.Dpi.CeilingPixels(12)).Contains(frames.Current));Assert.Equal(reason=="lock"?Activation.Off:Activation.On,gate.Snapshot.Activation);
    }
    [Fact]public void InitialOpenInterruptionAndCollapseReverseFromActualDisplayedEdgesWithoutJump()
    {
        var f=Frames();var anchor=f.Anchor;f.Open(280,Preferences);Assert.Equal(anchor,f.Current);Assert.Equal(anchor,f.Tick(TimeSpan.Zero));var partial=f.Tick(TimeSpan.FromMilliseconds(40));Assert.NotEqual(anchor,partial);
        f.Collapse();Assert.Equal(partial,f.Tick(TimeSpan.Zero));f.Tick(TimeSpan.FromMilliseconds(35));var closing=f.Current;f.Open(300,Preferences);Assert.Equal(closing,f.Tick(TimeSpan.Zero));Settle(f);Assert.Equal(anchor,f.Anchor);f.Collapse();Settle(f);Assert.Equal(anchor,f.Current);
    }
    [Fact]public void OpacityIsContinuousWhenOpeningReversesOrExpandedDragResumes()
    {
        var f=Frames();f.Open(300,Preferences);Assert.Equal(0,f.ContentOpacity);f.Tick(TimeSpan.FromMilliseconds(20));Assert.Equal(.25,f.ContentOpacity);f.Collapse();Assert.Equal(.25,f.ContentOpacity);f.Tick(TimeSpan.FromMilliseconds(20));double closing=f.ContentOpacity;f.Open(300,Preferences);Assert.Equal(closing,f.ContentOpacity);Settle(f);Assert.Equal(1,f.ContentOpacity);f.BeginMove();f.Move(new(100,100),Monitor());Assert.Equal(1,f.ContentOpacity);f.EndMove(Preferences);Assert.Equal(1,f.ContentOpacity);
    }
    [Fact]public void DpiRebasePreservesOriginalGrabFractionEvenAfterWorkAreaClampsButtonAwayFromCursor()
    {
        var gesture=new OverlayPointerGesture();gesture.Down(new(-730,-520),new(-750,-550,-702,-502),true);gesture.Rebase(new(1100,900),new(200,100,248,148),true);
        Assert.True(gesture.TryDpiDesiredAnchor(new(-500,-300),new(2),out var desired));Assert.Equal(new PixelPoint(-540,-360),desired);gesture.Cancel();Assert.False(gesture.TryDpiDesiredAnchor(new(0,0),new(1),out _));
    }
    [Fact]public void DirectionWidthAndAnchorStayFrozenAcrossBurstMeasurementsAndTargetHeightNeverShrinks()
    {
        var f=Frames();f.Open(140,Preferences);var direction=f.Plan!.Direction;int width=f.Plan.Target.Width;f.Grow(600);Settle(f);var tall=f.Current;f.Grow(150);Settle(f);Assert.Equal(tall,f.Current);Assert.Equal(direction,f.Plan.Direction);Assert.Equal(width,f.Current.Width);Assert.Equal(f.Plan.Anchor,f.Anchor);
        f.Grow(1000000);Settle(f);Assert.Equal(f.Plan.MaximumHeightPixels,f.Current.Height);Assert.True(f.Monitor.WorkArea.Contains(f.Current));
    }
    [Theory][InlineData(1.0)][InlineData(1.25)][InlineData(1.5)][InlineData(2.0)]
    public void ExpandedDragFreezesDimensionsAndCachesGrowthUntilReleaseWithDpiSafetyPriority(double dpi)
    {
        var f=Frames();f.Open(300,Preferences);Settle(f);var current=f.Current;var oldAnchor=f.Anchor;f.BeginMove();var direction=f.ContentDirection;double textWidth=f.TextWidthDip;
        f.Grow(620);Assert.Equal(current,f.Tick(TimeSpan.FromSeconds(2)));f.Move(new(1100,900),Monitor(dpi));var moving=f.Current;Assert.True(moving.Contains(f.Anchor));Assert.True(f.Monitor.WorkArea.Inset(f.Monitor.Dpi.CeilingPixels(12)).Contains(moving));Assert.Equal(direction,f.ContentDirection);Assert.Equal(textWidth,f.TextWidthDip);
        f.Grow(800);Assert.Equal(moving,f.Tick(TimeSpan.FromSeconds(1)));Assert.Equal(OverlayTransition.Moving,f.Transition);f.EndMove(Preferences,600);Settle(f);Assert.True(f.Current.Contains(f.Anchor));Assert.NotEqual(oldAnchor,f.Anchor);Assert.Equal(Math.Min(600,f.Plan!.MaximumHeightPixels),f.Current.Height);
    }
    [Fact]public void SmallerDisplayClipsFrozenCardImmediatelyButNeverLosesButtonOrRequiresOverflow()
    {
        var f=Frames();f.Open(600,Preferences);Settle(f);f.BeginMove();var tiny=new MonitorGeometry("tiny",new(0,0,150,150),new(2));f.Move(new(140,140),tiny);Assert.True(tiny.WorkArea.Inset(24).Contains(f.Current));Assert.True(f.Current.Contains(f.Anchor));Assert.Equal(96,f.Anchor.Width);f.EndMove(Preferences);Settle(f);Assert.True(tiny.WorkArea.Inset(24).Contains(f.Current));
    }
    [Fact]public void ReducedMotionSettlesImmediatelyAndToolbarNeverExceeds220Dip()
    {
        var f=Frames(2);f.OpenToolbar();f.Tick(TimeSpan.Zero,true);Assert.Equal(OverlayTransition.Expanded,f.Transition);Assert.True(f.Current.Width<=440);Assert.Equal(f.Anchor.Height,f.Current.Height);Assert.False(f.NeedsFrame);
        f.Collapse();f.Tick(TimeSpan.Zero,true);Assert.Equal(f.Anchor,f.Current);Assert.False(f.NeedsFrame);f.Open(600,Preferences);f.Tick(TimeSpan.Zero,true);Assert.False(f.NeedsFrame);
    }
    [Fact]public void CachedOffReopenAndManualCollapseDoNotAcquireTextOrStartRequests()
    {
        var gate=Gate();var id=Start(gate);var cache=new OverlayResults(gate);Assert.True(cache.Begin(id));Assert.True(cache.Append(id,"公开测试一"));Assert.True(cache.TryAutoReveal(id));cache.Collapse();Assert.True(cache.Append(id,"公开测试二"));Assert.False(cache.TryAutoReveal(id));Assert.True(cache.Complete(id));Assert.True(cache.Reopen());
        gate.Disable();for(int i=0;i<3;i++){Assert.True(cache.Reopen());Assert.Equal("公开测试一公开测试二",cache.Text);Assert.Equal(Activation.Off,gate.Snapshot.Activation);Assert.False(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));Assert.False(gate.CanSend(id,new(new Uri("https://public.invalid"))));}
        gate.Disable(hide:true);Assert.False(cache.Reopen());gate.SetSessionLocked(true);Assert.False(cache.Reopen());cache.Clear();Assert.False(cache.HasText);Assert.Empty(cache.Versions);
    }
    [Fact]public void SessionKeepsAtMostThreeVersionsAndOnlyValidatedNewSourceClearsOldCache()
    {
        var gate=Gate();var cache=new OverlayResults(gate);var session=Guid.NewGuid();
        for(int i=0;i<5;i++){var id=Start(gate,session);Assert.True(cache.Begin(id));Assert.True(cache.Append(id,"public"+i));Assert.True(cache.Complete(id));}Assert.Equal(3,cache.Versions.Count);Assert.Equal(new[]{"public2","public3","public4"},cache.Versions.Select(v=>v.Text).ToArray());
        Assert.False(cache.Begin(new(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),0)));Assert.Equal("public4",cache.Text);var next=Start(gate);Assert.True(cache.Begin(next));Assert.False(cache.HasText);Assert.Empty(cache.Versions);
    }
    [Theory][InlineData("off")][InlineData("stop")][InlineData("lock")][InlineData("replacement")]
    public void LateRequestDeltaCannotTouchOldOrNewContent(string change)
    {
        var gate=Gate();var id=Start(gate);var cache=new OverlayResults(gate);cache.Begin(id);cache.Append(id,"public-original");
        if(change=="off")gate.Disable();else if(change=="stop")gate.StopGeneration();else if(change=="lock")gate.SetSessionLocked(true);else{var next=Start(gate);cache.Begin(next);cache.Append(next,"public-new");}
        string before=cache.Text;Assert.False(cache.Append(id,"late"));Assert.False(cache.Complete(id));Assert.Equal(before,cache.Text);Assert.False(cache.TryAutoReveal(id));
    }
    [Fact]public void ResultLimitFailsBeforeMutatingMemoryAndNoDeltaAfterCompletion()
    {
        var gate=Gate();var id=Start(gate);var cache=new OverlayResults(gate);cache.Begin(id);Assert.True(cache.Append(id,new string('a',OverlayResults.MaximumCharacters)));Assert.Throws<InvalidOperationException>(()=>cache.Append(id,"x"));Assert.Equal(OverlayResults.MaximumCharacters,cache.Text.Length);Assert.True(cache.Complete(id));Assert.False(cache.Append(id,"late"));
    }
    [Fact]public void TwoThousandEventSequencesPreserveFrameBoundsThroughOpenMoveDpiGrowCloseReverse()
    {
        var random=new Random(0x504432);double[] scales=[1,1.25,1.5,2];
        for(int i=0;i<2000;i++)
        {
            var dpi=new DpiScale(scales[random.Next(scales.Length)]);int x=random.Next(-5000,5000),y=random.Next(-3000,3000);var m=new MonitorGeometry("public"+i,new(x,y,x+random.Next(300,2200),y+random.Next(300,1600)),dpi);var a=OverlayPositionMapper.Restore(null,[m],m.Id);var f=new OverlayFrames(a,m);f.Open(random.Next(50,1200),Preferences);
            for(int step=0;step<30;step++){if(step==3)f.Grow(1600);if(step==9){f.BeginMove();f.Move(new(m.WorkArea.Right-10,m.WorkArea.Bottom-10),m with{Dpi=new(scales[random.Next(scales.Length)])});}if(step==12)f.EndMove(Preferences);if(step==20)f.Collapse();if(step==23)f.Open(500,Preferences);f.Tick(TimeSpan.FromMilliseconds(16));Assert.True(f.Current.Contains(f.Anchor));Assert.True(f.Monitor.WorkArea.Inset(f.Monitor.Dpi.CeilingPixels(12)).Contains(f.Current));}Settle(f);
        }
    }
    [Fact]public void HoverAndDelayedWaitingCannotOverrideManualCollapseOrHiddenLock()
    {
        var gate=Gate();var id=Start(gate);gate.Collapse();Assert.True(gate.TryPeek(true));Assert.True(gate.TryPeek(false));Assert.False(gate.TryRevealDelta(id));
        gate.Disable(hide:true);Assert.False(gate.TryPeek(true));Assert.False(gate.TryReopen(true));gate.SetSessionLocked(true);Assert.False(gate.TryPeek(true));Assert.False(gate.TryRevealDelta(id));
    }
    [Fact]public void WaitingToolbarTransformsIntoBodyWithoutChangingAnchorAndCollapseInterruptsContinuously()
    {
        var f=Frames();f.OpenToolbar();Settle(f);var anchor=f.Anchor;var toolbar=f.Current;Assert.Equal(anchor.Height,toolbar.Height);
        f.Open(250,Preferences);Assert.Equal(toolbar,f.Tick(TimeSpan.Zero));Settle(f);Assert.Equal(anchor,f.Anchor);Assert.True(f.Current.Height>anchor.Height);f.Collapse();Settle(f);Assert.Equal(anchor,f.Current);
    }
    [Fact]public void DuplicateBeginDoesNotResetContentAndReplacedPartialVersionIsMarkedStopped()
    {
        var gate=Gate();var cache=new OverlayResults(gate);var id=Start(gate);cache.Begin(id);cache.Append(id,"public partial");Assert.False(cache.Begin(id));Assert.Equal("public partial",cache.Text);
        var next=Start(gate,id.SessionId);Assert.True(cache.Begin(next));Assert.True(Assert.Single(cache.Versions).Stopped);Assert.False(cache.HasText);Assert.False(cache.Append(id,"late"));
    }
    [Fact]public void EmptyDeltaDoesNotBecomeFirstBodyOrResetWaitingEligibility()
    {
        var gate=Gate();var cache=new OverlayResults(gate);var id=Start(gate);cache.Begin(id);Assert.False(cache.Append(id,""));Assert.False(cache.HasText);Assert.False(cache.TryAutoReveal(id));Assert.True(gate.CanApply(id));Assert.True(cache.Append(id,"public first body"));Assert.True(cache.TryAutoReveal(id));
    }
    private sealed class QueuedDestination:SynchronizationContext
    {private readonly Queue<(SendOrPostCallback Callback,object? State)> work=new();public override void Post(SendOrPostCallback callback,object? state)=>work.Enqueue((callback,state));public void Drain(){while(work.TryDequeue(out var item))item.Callback(item.State);}}
    [Theory][InlineData("off")][InlineData("lock")][InlineData("replace")]
    public async Task ActualIdentityReceiverFinalDestinationGateRejectsQueuedPresentationUpdates(string reason)
    {
        var gate=Gate();var cache=new OverlayResults(gate);var id=Start(gate);cache.Begin(id);cache.Append(id,"public original");var destination=new QueuedDestination();var receiver=new IdentityEventReceiver<OverlayResultEvent>(gate,destination);int applied=0;
        var pending=receiver.ReceiveAsync(id,new(OverlayResultEventKind.Delta,"late"),v=>{applied++;cache.Append(id,v.Text);});
        if(reason=="off")gate.Disable();else if(reason=="lock")gate.SetSessionLocked(true);else{var next=Start(gate);cache.Begin(next);cache.Append(next,"public replacement");}
        string before=cache.Text;destination.Drain();Assert.False(await pending);Assert.Equal(0,applied);Assert.Equal(before,cache.Text);
    }
    private sealed class NoKeys:ISecretProtector{public byte[] Protect(ReadOnlySpan<byte> input)=>throw new InvalidOperationException();public byte[] Unprotect(ReadOnlySpan<byte> input)=>throw new InvalidOperationException();}
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    [Fact]public async Task DpiDragReleaseSavesProductionSettingsWhileOriginalBoundOperationKeepsRunning()
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-overlay-tests-"+Guid.NewGuid().ToString("N"));var store=new SettingsStore(Path.Combine(root,"config"));var coordinator=new SettingsCoordinator(store,new SecretFileStore(Path.Combine(root,"secrets"),new NoKeys(),new DirectoryPolicy()));
        try
        {
            using(var draft=await coordinator.OpenDraftAsync()){draft.Update(s=>s with{ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]});await coordinator.SaveAsync(draft);}
            using var shell=new ShellLifecycle(coordinator);await shell.InitializeAsync();Assert.True(shell.TryPrepareEnable(out var intent,out _));Assert.True(shell.ConfirmEnable(intent!,true));var gate=shell.Gate;var id=Start(gate);Assert.True(gate.TryLinkRequestCancellation(id,default,out var linked));using var token=linked!;var pending=Task.Delay(Timeout.Infinite,token.Token);
            var frame=Frames();frame.Open(350,Preferences);Settle(frame);var gesture=new OverlayPointerGesture();gesture.Down(frame.Anchor.Center,frame.Anchor,true);gate.SetSuspension(Suspension.Moving,true);frame.BeginMove();frame.Move(new(500,100),Monitor(2));gesture.Rebase(frame.Anchor.Center,frame.Anchor,true);Assert.Equal(OverlayPointerAction.Released,gesture.Up());gate.SetSuspension(Suspension.Moving,false);frame.EndMove(Preferences,400);Settle(frame);
            var saved=OverlayPositionMapper.Capture(frame.Anchor,frame.Monitor);await shell.SavePositionAsync(saved);Assert.Equal(saved,(await store.LoadAsync()).Data.Overlay.LastPosition);Assert.Equal(frame.Anchor,OverlayPositionMapper.Restore(saved,[frame.Monitor],frame.Monitor.Id));Assert.Equal(Activation.On,gate.Snapshot.Activation);Assert.True(gate.CanApply(id));Assert.False(pending.IsCompleted);Assert.False(token.IsCancellationRequested);
            gate.StopGeneration();await Assert.ThrowsAnyAsync<OperationCanceledException>(async()=>await pending);
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
