using System.Text;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class DropCaptureTests
{
    private static readonly SelectionWindow Source=new(123,456,789,"public.exe");
    private static readonly DropTargetGeometry Target=new(new(0,0,48,48),new(0,0,48,48),24);
    private static readonly PixelPoint Point=new(24,24);
    private sealed class Clock:TimeProvider{public long Now=10000;public override long TimestampFrequency=>1000;public override long GetTimestamp()=>Now;}
    private sealed class Destination:SynchronizationContext
    {private readonly Queue<Action> items=[];public override void Post(SendOrPostCallback callback,object? state)=>items.Enqueue(()=>callback(state));public int Count=>items.Count;public void Drain(){while(items.TryDequeue(out var item))item();}}
    private sealed class Harness:IAsyncDisposable
    {
        public readonly RuntimeGate Gate=new(requireProfileBinding:true);public SettingsSnapshot Settings=new(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]},"public-v1",SettingsLoadState.Current);
        public bool Visible=true,Current=true,Native=true;public Action? DuringSource;public readonly Destination Queue=new();public readonly Clock Time=new();public readonly List<(SelectionSourceSnapshot Source,bool Duplicate)> Results=[];public readonly List<DropCaptureStatus> Failures=[];public DropCaptureCoordinator Coordinator;public DropCaptureLease Lease;
        public Harness(){Gate.ObserveSettingsMetadata(Settings);Gate.SetMode(TriggerMode.Drop);var origin=new EndpointOrigin(new Uri("https://public.invalid"));Gate.ConsentTo(origin);Assert.True(Gate.TryEnable(origin,true));Coordinator=new(Gate,()=>Settings,s=>{DuringSource?.Invoke();return Current&&s==Source;},()=>Visible,999,Queue,(s,d)=>Results.Add((s,d)),Failures.Add,Time);Lease=Coordinator.Open(Guid.NewGuid(),Gate.Snapshot.Epoch,Target)!;Assert.NotNull(Lease);}
        public Guid Begin(){Guid id=Guid.NewGuid();Assert.True(Coordinator.Enter(Lease,id,Source,Point,OleEffect.Copy));Assert.True(Coordinator.Begin(Lease,id,Source,Point,OleEffect.Copy));return id;}
        public Task Receive(Guid id,string text="public 文本 😀",CancellationToken cancellation=default)=>Coordinator.ReceiveAsync(Lease,id,Encoding.UTF8.GetBytes(text),action=>{if(!Native)return false;action();return true;},cancellation,()=>Native);
        public async Task Deliver(string text="public 文本 😀"){var task=Receive(Begin(),text);Assert.Equal(1,Queue.Count);Queue.Drain();await task;}
        public ValueTask DisposeAsync()=>Coordinator.DisposeAsync();
    }
    [Theory][InlineData("off")][InlineData("selection")][InlineData("hidden")][InlineData("moving")][InlineData("settings")][InlineData("capture")][InlineData("locked")][InlineData("local-only")][InlineData("read-only")][InlineData("recovery")]
    public async Task IneligibleModeCannotOpenReceiverLease(string reason)
    {
        await using var h=new Harness();switch(reason){case "off":h.Gate.Disable();break;case "selection":h.Gate.SetMode(TriggerMode.Selection);break;case "hidden":h.Visible=false;break;case "moving":h.Gate.SetSuspension(Suspension.Moving,true);break;case "settings":h.Gate.SetSuspension(Suspension.Settings,true);break;case "capture":h.Gate.SetSuspension(Suspension.Capture,true);break;case "locked":h.Gate.SetSessionLocked(true);break;case "local-only":h.Gate.Disable();Assert.True(h.Gate.TryEnableLocalOperation(true));break;case "read-only":h.Settings=h.Settings with{State=SettingsLoadState.ReadOnlyUnknown};break;case "recovery":h.Settings=h.Settings with{State=SettingsLoadState.RecoverableDefaults};break;}
        Assert.Null(h.Coordinator.Open(Guid.NewGuid(),h.Gate.Snapshot.Epoch,Target));Assert.Empty(h.Results);Assert.Equal(0,h.Queue.Count);
    }
    [Theory][InlineData("move")][InlineData("none")][InlineData("corner")][InlineData("edge")][InlineData("outside")][InlineData("source")][InlineData("own")][InlineData("terminal")][InlineData("excluded")][InlineData("empty-id")]
    public async Task HoverNeverAcceptsUntrustedOrMoveOnlyData(string reason)
    {
        await using var h=new Harness();var source=reason switch{"own"=>Source with{ProcessId=999},"terminal"=>Source with{Executable="powershell.exe"},_=>Source};if(reason=="source")h.Current=false;if(reason=="excluded")h.Settings=h.Settings with{Version="public-excluded",Data=h.Settings.Data with{Privacy=new(){BlockedApplications=["public.exe"]}}};
        var point=reason switch{"corner"=>new PixelPoint(0,0),"edge"=>new(48,24),"outside"=>new(-1,24),_=>Point};var effect=reason switch{"move"=>OleEffect.Move,"none"=>OleEffect.None,_=>OleEffect.Copy};Assert.False(h.Coordinator.Enter(h.Lease,reason=="empty-id"?Guid.Empty:Guid.NewGuid(),source,point,effect));Assert.Equal(0,h.Queue.Count);Assert.Empty(h.Results);
    }
    [Fact]public async Task ActualDropFreezesIdentityAndPreservesRawUnicodeWithoutUiaEvidence()
    {await using var h=new Harness();Guid id=h.Begin();string raw="a\r\n  code\t中😀\u200b\ufeff\u0001";var task=h.Receive(id,raw);h.Queue.Drain();await task;var value=Assert.Single(h.Results).Source;Assert.Equal(raw,value.RawText);Assert.Equal("a\n  code\t中😀",value.Text);Assert.Equal(value.Text.EnumerateRunes().Count(),value.Scalars);Assert.Equal(Source,value.Window);Assert.Equal(h.Settings.Version,value.SettingsVersion);Assert.Equal(h.Lease.Receiver,value.DropReceiver);Assert.Equal(id,value.DropTransaction);Assert.Equal(SelectionSourceKind.OleDrop,value.SourceKind);Assert.Empty(value.RuntimeId);Assert.Empty(value.Ranges);Assert.False(value.RequiresConfirmation);}
    [Fact]public async Task HoverLeaveDoesNotReadAndDropNeedsSameEnteredTransactionSourceAndPoint()
    {await using var h=new Harness();Guid id=Guid.NewGuid();Assert.False(h.Coordinator.Begin(h.Lease,id,Source,Point,OleEffect.Copy));Assert.True(h.Coordinator.Enter(h.Lease,id,Source,Point,OleEffect.Copy));Assert.False(h.Coordinator.Begin(h.Lease,Guid.NewGuid(),Source,Point,OleEffect.Copy));Assert.False(h.Coordinator.Begin(h.Lease,id,Source with{ProcessStartUtcTicks=790},Point,OleEffect.Copy));Assert.False(h.Coordinator.Begin(h.Lease,id,Source,new(48,24),OleEffect.Copy));h.Coordinator.Leave(h.Lease,id);Assert.False(h.Coordinator.Begin(h.Lease,id,Source,Point,OleEffect.Copy));await h.Receive(id);Assert.Empty(h.Results);Assert.Equal(0,h.Queue.Count);}
    [Fact]public async Task SameTransactionIsExactlyOnceAndRepeatedTextOnlyReopensMemoryHint()
    {await using var h=new Harness();Guid id=h.Begin();var task=h.Receive(id);await h.Receive(id);h.Queue.Drain();await task;Assert.Single(h.Results);Assert.False(h.Coordinator.Enter(h.Lease,id,Source,Point,OleEffect.Copy));await h.Deliver();Assert.Equal(new[]{false,true},h.Results.Select(r=>r.Duplicate));}
    [Fact]public async Task ReceiverIdentityCannotBorrowFreshEpochOrVersion()
    {await using var h=new Harness();Guid id=h.Begin();await h.Coordinator.ReceiveAsync(h.Lease with{Receiver=Guid.NewGuid()},id,Encoding.UTF8.GetBytes("public"),a=>{a();return true;});Assert.Equal(0,h.Queue.Count);h.Gate.Disable();Assert.True(h.Gate.TryEnable(new(new Uri("https://public.invalid")),true));Assert.False(h.Coordinator.Enter(h.Lease,Guid.NewGuid(),Source,Point,OleEffect.Copy));Assert.Null(h.Coordinator.Open(Guid.Empty,1,Target));}
    [Theory][InlineData("off")][InlineData("off-on")][InlineData("mode")][InlineData("pause-resume")][InlineData("source")][InlineData("hidden")][InlineData("config")][InlineData("native")][InlineData("anchor")][InlineData("cancel")][InlineData("invalidate")][InlineData("new-hover")][InlineData("new-receiver")]
    public async Task QueuedDropRechecksAllOwnershipAndAuthorizationAtDestination(string change)
    {
        await using var h=new Harness();using var cancellation=new CancellationTokenSource();Guid id=h.Begin();var task=h.Receive(id,cancellation:cancellation.Token);Assert.Equal(1,h.Coordinator.PendingSourceCountForTest);
        switch(change){case "off":h.Gate.Disable();break;case "off-on":h.Gate.Disable();Assert.True(h.Gate.TryEnable(new(new Uri("https://public.invalid")),true));break;case "mode":h.Gate.SetMode(TriggerMode.Selection);break;case "pause-resume":h.Gate.SetSuspension(Suspension.Moving,true);h.Gate.SetSuspension(Suspension.Moving,false);break;case "source":h.Current=false;break;case "hidden":h.Visible=false;break;case "config":h.Settings=h.Settings with{Version="public-v2"};break;case "native":h.Native=false;break;case "anchor":h.Coordinator.UpdateGeometry(new(new(10,0,58,48),new(10,0,58,48),24));break;case "cancel":cancellation.Cancel();break;case "invalidate":h.Coordinator.Invalidate();break;case "new-hover":Assert.True(h.Coordinator.Enter(h.Lease,Guid.NewGuid(),Source,Point,OleEffect.Copy));break;case "new-receiver":Assert.NotNull(h.Coordinator.Open(Guid.NewGuid(),h.Gate.Snapshot.Epoch,Target));break;}
        if(change is "anchor" or "cancel" or "invalidate" or "new-hover" or "new-receiver")Assert.Equal(0,h.Coordinator.PendingSourceCountForTest);h.Queue.Drain();await task;Assert.Empty(h.Results);Assert.Empty(h.Failures);Assert.Equal(0,h.Coordinator.PendingSourceCountForTest);
    }
    [Theory][InlineData(1499,true)][InlineData(1500,false)][InlineData(1501,false)]
    public async Task DestinationHasAbsoluteDropDeadline(int elapsed,bool accepted)
    {await using var h=new Harness();var task=h.Receive(h.Begin());h.Time.Now+=elapsed;h.Queue.Drain();await task;Assert.Equal(accepted,h.Results.Count==1);}
    [Fact]public async Task NativeDeadlineIsRecheckedAfterFinalSourceValidation()
    {await using var h=new Harness();var task=h.Receive(h.Begin());int calls=0;h.DuringSource=()=>{if(++calls>=2)h.Native=false;};h.Queue.Drain();await task;Assert.Empty(h.Results);Assert.True(calls>=2);}
    [Theory][InlineData("gate")][InlineData("time")][InlineData("config")][InlineData("cancel")]
    public async Task FinalSourceValidationCannotMutateAuthorizationBeforeCommit(string change)
    {await using var h=new Harness();using var cancel=new CancellationTokenSource();var task=h.Receive(h.Begin(),cancellation:cancel.Token);h.DuringSource=()=>{switch(change){case "gate":h.Gate.Disable();break;case "time":h.Time.Now+=1500;break;case "config":h.Settings=h.Settings with{Version="changed"};break;default:cancel.Cancel();break;}};h.Queue.Drain();await task;Assert.Empty(h.Results);}
    [Fact]public async Task GeometryCanGrowThenCollapseWithoutDiscardingAcceptedDropPoint()
    {await using var h=new Harness();var expanded=new DropTargetGeometry(Target.Anchor,new(0,0,96,48),16);h.Coordinator.UpdateGeometry(expanded);Guid id=Guid.NewGuid();Assert.True(h.Coordinator.Enter(h.Lease,id,Source,new(75,24),OleEffect.Copy));Assert.True(h.Coordinator.Begin(h.Lease,id,Source,new(75,24),OleEffect.Copy));h.Coordinator.UpdateGeometry(Target);var task=h.Receive(id);h.Queue.Drain();await task;Assert.Single(h.Results);}
    [Fact]public async Task SeenTransactionBoundRevokesInsteadOfEvictingReplayProtection()
    {await using var h=new Harness();Guid first=Guid.NewGuid();Assert.True(h.Coordinator.Enter(h.Lease,first,Source,Point,OleEffect.Copy));for(int i=1;i<256;i++)Assert.True(h.Coordinator.Enter(h.Lease,Guid.NewGuid(),Source,Point,OleEffect.Copy));Assert.False(h.Coordinator.Enter(h.Lease,Guid.NewGuid(),Source,Point,OleEffect.Copy));Assert.Null(h.Coordinator.Lease);Assert.False(h.Coordinator.Enter(h.Lease,first,Source,Point,OleEffect.Copy));}
    [Theory][InlineData("empty")][InlineData("url")][InlineData("custom-url")][InlineData("cleaned-url")][InlineData("hard-limit")][InlineData("bad-utf8")]
    public async Task RejectedDropCannotReplacePreviousSource(string reason)
    {await using var h=new Harness();await h.Deliver("public previous");Guid id=h.Begin();string text=reason switch{"empty"=>" ","url"=>"https://public.invalid/path","custom-url"=>"javascript:alert(1)","cleaned-url"=>"htt\u0001ps://public.invalid","hard-limit"=>new string('a',50001),_=>"public"};var task=reason=="bad-utf8"?h.Coordinator.ReceiveAsync(h.Lease,id,new byte[]{0xff},a=>{a();return true;}):h.Receive(id,text);h.Queue.Drain();await task;Assert.Single(h.Failures);Assert.Equal("public previous",Assert.Single(h.Results).Source.Text);}
    [Theory][InlineData(1,false)][InlineData(12000,false)][InlineData(12001,true)][InlineData(50000,true)]
    public async Task ScalarLimitsUseUnicodeRunesAndSoftLimitRequiresConfirmation(int scalars,bool confirm)
    {await using var h=new Harness();await h.Deliver(string.Concat(Enumerable.Repeat("😀",scalars)));var result=Assert.Single(h.Results).Source;Assert.Equal(scalars,result.Scalars);Assert.Equal(confirm,result.RequiresConfirmation);}
    [Theory][InlineData("high")][InlineData("low")][InlineData("high-a")]
    public void SnapshotNeverAcceptsUnpairedSurrogates(string kind)
    {string raw=kind switch{"high"=>"\ud800","low"=>"\udc00",_=>"\ud800a"};var settings=new SettingsSnapshot(new(),"public",SettingsLoadState.Current);Assert.Null(SelectionTextPolicy.DropSnapshot(Source,raw,Guid.NewGuid(),Guid.NewGuid(),settings));}
    [Fact]public async Task ShutdownClearsQueuedSourceWithoutWaitingForDestinationPump()
    {var h=new Harness();var task=h.Receive(h.Begin());Assert.Equal(1,h.Coordinator.PendingSourceCountForTest);await h.DisposeAsync();Assert.Equal(0,h.Coordinator.PendingSourceCountForTest);h.Queue.Drain();await task;Assert.Empty(h.Results);}
}
