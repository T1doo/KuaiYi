using System.Threading.Channels;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class SelectionInputBridgeTests
{
    private static readonly SelectionWindow Source=new(123,456,789,"public.exe");
    private static SettingsSnapshot Config(string version="public-settings")=>new(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]},version,SettingsLoadState.Current);
    private static RuntimeGate Gate(){var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(Config());var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));return gate;}
    private sealed class Platform:ISelectionCapturePlatform
    {public int Calls;public bool IsCurrent(SelectionWindow source)=>source==Source;public Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation){Calls++;return Task.FromResult(new SelectionCaptureResult(SelectionCaptureStatus.Success,"public selection",[new(0,0,100,20)],[12,4],new(0,0,500,200)));}}
    private sealed class Queue:SynchronizationContext
    {private readonly System.Collections.Generic.Queue<(SendOrPostCallback,object?)> callbacks=[];public override void Post(SendOrPostCallback callback,object? state){lock(callbacks)callbacks.Enqueue((callback,state));}public void Drain(){while(true){(SendOrPostCallback callback,object? state) next;lock(callbacks)if(!callbacks.TryDequeue(out next))return;next.callback(next.state);}}}
    private static void Transition(RuntimeGate gate,string kind)
    {switch(kind){case "off-on":gate.Disable();Assert.True(gate.TryEnable(new(new Uri("https://public.invalid")),true));break;case "drop-selection":gate.SetMode(TriggerMode.Drop);gate.SetMode(TriggerMode.Selection);break;default:gate.SetSuspension(Suspension.Moving,true);gate.SetSuspension(Suspension.Moving,false);break;}}
    private static SelectionRawInput Raw(bool down,SelectionInputIdentity identity)=>new(down,new(down?3:70,3),down?1000:1010,false,Source.Handle,Source.ProcessId,1000,0,identity);
    [Theory][InlineData("off-on","channel")][InlineData("drop-selection","channel")][InlineData("pause-resume","channel")][InlineData("off-on","dispatcher")][InlineData("drop-selection","dispatcher")][InlineData("pause-resume","dispatcher")]
    public async Task ProductionRawBridgeRejectsBothLevelsOfAccumulationBeforeBroker(string kind,string level)
    {
        var gate=Gate();var platform=new Platform();var destination=new Queue();Guid? hook=Guid.NewGuid();int resolutions=0,accepted=0;
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>Config(),platform,new(4,4,500,4,4),999,destination,(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask);
        var stamp=coordinator.FreezeInputIdentity(hook.Value)!;var bridge=new SelectionInputBridge(gate,()=>Config(),()=>hook,_=>{resolutions++;return Source;},platform.IsCurrent,coordinator,destination);
        var channel=Channel.CreateBounded<SelectionRawInput>(64);Assert.True(channel.Writer.TryWrite(Raw(true,stamp)));Assert.True(channel.Writer.TryWrite(Raw(false,stamp)));
        if(level=="channel")Transition(gate,kind);
        var down=bridge.ReceiveAsync(await channel.Reader.ReadAsync());var up=bridge.ReceiveAsync(await channel.Reader.ReadAsync());
        if(level=="dispatcher")Transition(gate,kind);destination.Drain();Assert.False((await down).Admitted);Assert.False((await up).Admitted);Assert.Equal(0,platform.Calls);Assert.Equal(0,accepted);Assert.Equal(level=="channel"?0:2,resolutions);
    }
    [Theory][InlineData("pid")][InlineData("reused-process")][InlineData("hook")][InlineData("config")][InlineData("source")]
    public async Task FrozenProcessTimeHookAndConfigCannotBeReboundAtDispatcher(string reason)
    {
        var gate=Gate();var platform=new Platform();var destination=new Queue();var config=Config();Guid? hook=Guid.NewGuid();var source=Source;bool current=true;
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>config,platform,new(4,4,500,4,4),999,destination,(_,_)=>{},_=>{},(_,_)=>Task.CompletedTask);
        var stamp=coordinator.FreezeInputIdentity(hook.Value)!;var bridge=new SelectionInputBridge(gate,()=>config,()=>hook,_=>source,_=>current,coordinator,destination);
        if(reason=="pid")source=Source with{ProcessId=457};if(reason=="reused-process")source=Source with{ProcessStartUtcTicks=1001};
        var input=bridge.ReceiveAsync(Raw(true,stamp));if(reason=="hook")hook=Guid.NewGuid();if(reason=="config")config=Config("new-settings");if(reason=="source")current=false;
        destination.Drain();Assert.False((await input).Admitted);Assert.Equal(0,platform.Calls);
    }
    [Fact]public async Task FreshGestureSurvivesOldQueuedInputAndBridgeDeliversActualCoordinatorWork()
    {
        var gate=Gate();var platform=new Platform();var destination=new Queue();Guid? hook=Guid.NewGuid();int accepted=0;
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>Config(),platform,new(4,4,500,4,4),999,destination,(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask);var old=coordinator.FreezeInputIdentity(hook.Value)!;Transition(gate,"off-on");hook=Guid.NewGuid();var fresh=coordinator.FreezeInputIdentity(hook.Value)!;
        var bridge=new SelectionInputBridge(gate,()=>Config(),()=>hook,_=>Source,platform.IsCurrent,coordinator,destination);
        var down=bridge.ReceiveAsync(Raw(true,fresh));destination.Drain();Assert.True((await down).Admitted);Assert.False((await bridge.ReceiveAsync(Raw(false,old))).Admitted);Assert.False((await bridge.ReceiveAsync(Raw(false,old) with{Injected=true})).Admitted);
        var up=bridge.ReceiveAsync(Raw(false,fresh));destination.Drain();Assert.True((await up).Admitted);destination.Drain();await (await up).Capture;Assert.Equal(1,platform.Calls);Assert.Equal(1,accepted);
    }
    [Fact]public async Task CurrentInjectedEventClearsPhysicalGestureWithoutResolvingOrCapturing()
    {
        var gate=Gate();var platform=new Platform();var destination=new Queue();Guid? hook=Guid.NewGuid();int resolutions=0;
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>Config(),platform,new(4,4,500,4,4),999,destination,(_,_)=>{},_=>{},(_,_)=>Task.CompletedTask);
        var stamp=coordinator.FreezeInputIdentity(hook.Value)!;var bridge=new SelectionInputBridge(gate,()=>Config(),()=>hook,_=>{resolutions++;return Source;},platform.IsCurrent,coordinator,destination);
        var down=bridge.ReceiveAsync(Raw(true,stamp));destination.Drain();Assert.True((await down).Admitted);
        var injected=bridge.ReceiveAsync(Raw(false,stamp) with{Injected=true});destination.Drain();Assert.False((await injected).Admitted);Assert.Equal(1,resolutions);
        var up=bridge.ReceiveAsync(Raw(false,stamp));destination.Drain();await (await up).Capture;Assert.Equal(0,platform.Calls);
    }
}
