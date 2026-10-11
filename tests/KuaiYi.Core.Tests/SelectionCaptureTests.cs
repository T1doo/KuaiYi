using System.Collections.Immutable;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionCaptureTests
{
    private static readonly SelectionWindow Source=new(123,456,789,"public.exe");
    private static SelectionCaptureResult Result(string text="public selection")=>new(SelectionCaptureStatus.Success,text,[new(0,0,100,20)],[12,4],new(0,0,500,200));
    private static SettingsSnapshot Config(string version="public-settings",AppSettings? data=null)=>new(data??new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]},version,SettingsLoadState.Current);
    private static RuntimeGate Gate()
    {var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(Config());var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));return gate;}
    private sealed class Platform:ISelectionCapturePlatform
    {
        public bool Current=true;public int Calls;public Func<CancellationToken,Task<SelectionCaptureResult>> Read=_=>Task.FromResult(Result());
        public bool IsCurrent(SelectionWindow window)=>Current&&window==Source;
        public Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation){Calls++;return Read(cancellation);}
    }
    private sealed class Destination:SynchronizationContext
    {private readonly Queue<(SendOrPostCallback,object?)> queue=[];public override void Post(SendOrPostCallback callback,object? state){lock(queue)queue.Enqueue((callback,state));}public int Count{get{lock(queue)return queue.Count;}}public void Drain(){while(true){(SendOrPostCallback callback,object? state) next;lock(queue){if(!queue.TryDequeue(out next))return;}next.callback(next.state);}}}
    private static async Task Queued(Destination destination){using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(5));while(destination.Count==0)await Task.Delay(1,guard.Token);}
    private static readonly Guid TestHook=Guid.Parse("b4bbf568-2832-48a5-b155-7a78c6314992");
    private static Task Drag(SelectionCaptureCoordinator coordinator,long tick=1000){var frozen=coordinator.FreezeInputIdentity(TestHook);_ =coordinator.InputAsync(new(true,new(3,3),tick,false,Source,frozen));return coordinator.InputAsync(new(false,new(70,3),tick+10,false,Source,frozen));}
    [Theory][InlineData("off-on")][InlineData("drop-selection")][InlineData("pause-resume")]
    public async Task RawQueuedInputBeforeLifecycleRoundtripCannotStartBroker(string transition)
    {
        var gate=Gate();var platform=new Platform();var destination=new Destination();
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>Config(),platform,new(4,4,500,4,4),999,destination,(_,_)=>{},_=>{},(_,_)=>Task.CompletedTask);
        var frozen=coordinator.FreezeInputIdentity(TestHook);Assert.NotNull(frozen);var down=new SelectionPointerEvent(true,new(3,3),1000,false,Source,frozen);var up=new SelectionPointerEvent(false,new(70,3),1010,false,Source,frozen);
        if(transition=="off-on"){gate.Disable();Assert.True(gate.TryEnable(new(new Uri("https://public.invalid")),true));}else if(transition=="drop-selection"){gate.SetMode(TriggerMode.Drop);gate.SetMode(TriggerMode.Selection);}else{gate.SetSuspension(Suspension.Moving,true);gate.SetSuspension(Suspension.Moving,false);}
        Assert.NotEqual(frozen.Ticket.Epoch,gate.Snapshot.Epoch);
        await coordinator.InputAsync(down);var pending=coordinator.InputAsync(up);destination.Drain();await pending;Assert.Equal(0,platform.Calls);
    }
    [Theory][InlineData("off")][InlineData("drop")][InlineData("settings")][InlineData("moving")][InlineData("capture")][InlineData("lock")][InlineData("excluded")]
    public async Task IneligibleInputNeverCallsPlatformOrRetainsCandidate(string reason)
    {
        var gate=Gate();var platform=new Platform();var config=Config();int accepted=0;
        if(reason=="off")gate.Disable();else if(reason=="drop")gate.SetMode(TriggerMode.Drop);else if(reason=="lock")gate.SetSessionLocked(true);else if(reason=="excluded")config=Config(data:config.Data with{Privacy=new(){BlockedApplications=["PUBLIC.EXE"]}});else gate.SetSuspension(reason switch{"settings"=>Suspension.Settings,"moving"=>Suspension.Moving,_=>Suspension.Capture},true);
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>config,platform,new(4,4,500,4,4),999,new Destination(),(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask);
        await Drag(coordinator);Assert.Equal(0,platform.Calls);Assert.Equal(0,accepted);
    }
    [Theory][InlineData(1,false)][InlineData(2,true)][InlineData(3,true)]
    public void SingleClickIsNotSelectionButDoubleAndTripleClickKeepPhysicalEvidence(int clicks,bool expected)
    {
        var tracker=new SelectionGestureTracker(4,4,500,4,4);SelectionGesture? found=null;
        for(int i=0;i<clicks;i++){tracker.Accept(new(true,new(10,10),1000+i*100,false,Source));found=tracker.Accept(new(false,new(10,10),1020+i*100,false,Source));}
        Assert.Equal(expected,found!=null);if(found!=null)Assert.Equal(clicks,found.ClickCount);
    }
    [Theory][InlineData("injected")][InlineData("window")][InlineData("time")]
    public void BrokenInputIdentityCannotProduceGesture(string reason)
    {var tracker=new SelectionGestureTracker(4,4,500,4,4);tracker.Accept(new(true,new(1,1),1000,false,Source));Assert.Null(tracker.Accept(new(false,new(70,1),reason=="time"?999:1010,reason=="injected",reason=="window"?Source with{ProcessStartUtcTicks=790}:Source)));}
    [Theory][InlineData("off")][InlineData("mode")][InlineData("source")][InlineData("config")][InlineData("exclude")][InlineData("lock")][InlineData("new-down")]
    public async Task DestinationQueueRechecksEligibilityAfterActualRead(string change)
    {
        var gate=Gate();var platform=new Platform();var config=Config();var destination=new Destination();int accepted=0;
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>config,platform,new(4,4,500,4,4),999,destination,(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask);var task=Drag(coordinator);await Queued(destination);
        switch(change){case "off":gate.Disable();break;case "mode":gate.SetMode(TriggerMode.Drop);break;case "source":platform.Current=false;break;case "config":config=Config("new-settings");break;case "exclude":config=Config("new-settings",config.Data with{Privacy=new(){BlockedApplications=["public"]}});break;case "lock":gate.SetSessionLocked(true);break;default:await coordinator.InputAsync(new(true,new(20,3),1100,false,Source,coordinator.FreezeInputIdentity(TestHook)));break;}
        destination.Drain();await task;Assert.Equal(1,platform.Calls);Assert.Equal(0,accepted);
    }
    [Fact]public async Task NewDownCancelsStableWaitingAndOnlyLatestSelectionIsAccepted()
    {
        var gate=Gate();var platform=new Platform();var destination=new Destination();var waits=new List<TaskCompletionSource>();var accepted=new List<SelectionSourceSnapshot>();
        Task Delay(TimeSpan period,CancellationToken token){Assert.Equal(180,period.TotalMilliseconds);var wait=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);waits.Add(wait);return wait.Task.WaitAsync(token);}
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>Config(),platform,new(4,4,500,4,4),999,destination,(s,_)=>accepted.Add(s),_=>{},Delay);
        var first=Drag(coordinator);Assert.Equal(0,platform.Calls);var second=Drag(coordinator,1200);waits[0].SetResult();await first;Assert.Equal(0,platform.Calls);waits[1].SetResult();await Queued(destination);destination.Drain();await second;Assert.Single(accepted);Assert.Equal(1,platform.Calls);
    }
    [Fact]public async Task LateUncooperativePlatformCannotReplaceNewerSourceAndShutdownCancelsQueuedCommit()
    {
        var gate=Gate();var platform=new Platform();var destination=new Destination();var late=new TaskCompletionSource<SelectionCaptureResult>(TaskCreationOptions.RunContinuationsAsynchronously);int accepted=0;
        platform.Read=_=>late.Task;var coordinator=new SelectionCaptureCoordinator(gate,()=>Config(),platform,new(4,4,500,4,4),999,destination,(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask);var first=Drag(coordinator);platform.Read=_=>Task.FromResult(Result("public newer"));var second=Drag(coordinator,1200);await Queued(destination);destination.Drain();await second;late.SetResult(Result("public obsolete"));await first;Assert.Equal(1,accepted);
        var queued=Drag(coordinator,1400);await Queued(destination);await coordinator.DisposeAsync();destination.Drain();await queued;Assert.Equal(1,accepted);
    }
    [Fact]public async Task DuplicateSnapshotReturnsMemoryReopenHintAndExplicitSelectionWorksInDropMode()
    {
        var gate=Gate();var platform=new Platform();var destination=new Destination();var flags=new List<bool>();await using var coordinator=new SelectionCaptureCoordinator(gate,()=>Config(),platform,new(4,4,500,4,4),999,destination,(_,same)=>flags.Add(same),_=>{},(_,_)=>Task.CompletedTask);
        for(int i=0;i<2;i++){var task=Drag(coordinator,1000+i*2000);await Queued(destination);destination.Drain();await task;}Assert.Equal(new[]{false,true},flags);gate.SetMode(TriggerMode.Drop);var explicitTask=coordinator.ExplicitAsync(Source,new(70,3));await Queued(destination);destination.Drain();await explicitTask;Assert.Equal(3,platform.Calls);Assert.Equal(3,flags.Count);
    }
    [Theory][InlineData("space")][InlineData("high")][InlineData("low")][InlineData("high-a")]
    public void EmptyOrMalformedUnicodeCannotBecomeSnapshot(string kind)
    {string text=kind switch{"high"=>"\ud800","low"=>"\udc00","high-a"=>"\ud800a",_=>" "};Assert.Null(SelectionTextPolicy.Snapshot(new(Source,new(3,3),new(70,3),0,1),Result(text),Config()));}
    [Fact]public void OneScalarAndCodeIndentationArePreservedAndSoftLimitDoesNotAuthorizeSend()
    {
        var gesture=new SelectionGesture(Source,new(3,3),new(70,3),0,1);Assert.Equal(1,SelectionTextPolicy.Snapshot(gesture,Result("a"),Config())!.Scalars);
        Assert.Equal("a\n  code\tvalue",SelectionTextPolicy.Snapshot(gesture,Result("a\r\n  code\tvalue\u200b"),Config())!.Text);
        Assert.True(SelectionTextPolicy.Snapshot(gesture,Result(new string('a',12001)),Config())!.RequiresConfirmation);Assert.Null(SelectionTextPolicy.Snapshot(gesture,Result(new string('a',50001)),Config()));Assert.Equal(50000,SelectionTextPolicy.Snapshot(gesture,Result(new string('a',50000)),Config())!.Scalars);
    }
    [Theory][InlineData("bounds")][InlineData("empty-ranges")][InlineData("runtime")]
    public void UnrelatedRetainedSelectionCannotPassAutomaticGestureAssociation(string change)
    {
        var result=Result();result=change switch{"bounds"=>result with{Ranges=[new(300,150,400,180)]},"empty-ranges"=>result with{Ranges=[]},_=>result with{RuntimeId=[]}};Assert.Null(SelectionTextPolicy.Snapshot(new(Source,new(3,3),new(70,3),0,1),result,Config()));
    }
    [Theory][InlineData("C:\\Apps\\Public.exe","public")][InlineData("C:\\Apps\\Public.exe","PUBLIC.EXE")][InlineData("C:\\Apps\\Public.exe","c:\\apps\\public.exe")]
    public void WindowsApplicationExclusionsMatchNameFileAndFullPathOnPortableCore(string executable,string blocked)
    {Assert.True(SelectionTextPolicy.Excluded(Source with{Executable=executable},Config().Data with{Privacy=new(){BlockedApplications=[blocked]}},999));}
}
