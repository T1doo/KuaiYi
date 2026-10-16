using System.Collections.Concurrent;
using System.Threading.Channels;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.Provider.Tests;

// Only the destination scheduling is controlled. HTTP, GenerateEnvelopesAsync,
// Coordinator/Workflow, ProviderEventReceiver and canonical gate are production.
public sealed class QueuedConsumerDeadlineTests(PhaseHttpFixture fixture):IClassFixture<PhaseHttpFixture>
{
    private sealed record Pending(SendOrPostCallback Callback,object? State){public void Apply()=>Callback(State);}
    private sealed class Destination:SynchronizationContext
    {
        private readonly Channel<Pending> queue=Channel.CreateUnbounded<Pending>();
        public override void Post(SendOrPostCallback action,object? state){if(!queue.Writer.TryWrite(new(action,state)))throw new InvalidOperationException("Destination closed.");}
        public Task<Pending> Take()=>queue.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(6));
        public bool DrainOne(){if(!queue.Reader.TryRead(out var pending))return false;pending.Apply();return true;}
    }
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseClock Clock=new();public readonly RuntimeGate Gate=new(requireProfileBinding:true);
        public readonly ProviderClient Provider;public readonly SettingsSnapshot Settings;public readonly OverlayResults Results;
        public readonly Destination Queue=new();public readonly PhaseHttpFixture.Script Script;
        public readonly ConcurrentQueue<TextTranslationUpdate> TextUpdates=new();public readonly ConcurrentQueue<ApiTestUpdate> TestUpdates=new();
        public readonly TextTranslationCoordinator? Text;public readonly ApiConnectionTestWorkflow? Test;
        public readonly bool ConnectionTest;public Task Job=Task.CompletedTask;public int SecretReads;
        public Action? BeforeSettingsRead;
        public Rig(PhaseHttpFixture fixture,bool connectionTest,Protocol protocol,string mime,int total=5)
        {
            ConnectionTest=connectionTest;var run=Guid.NewGuid().ToString("N");Script=fixture.Add(run,mime);var prefix=new Uri(fixture.BaseUri,run+"/");
            Settings=new(new(){ActiveProfileId="p",Profiles=[new(){Id="p",Protocol=protocol,BaseUrl=prefix.AbsoluteUri,Model="PUBLIC",AllowLoopbackHttp=true,Authentication=ApiAuthentication.None,Streaming=mime!="application/json",TimeoutSeconds=total,Timeouts=new(){FirstResponseSeconds=2,IdleSeconds=2}}]},"queue-fixture",SettingsLoadState.Current);
            Gate.ObserveSettingsMetadata(Settings);Provider=new(clock:Clock,phaseClock:Clock);Results=new(Gate);
            if(connectionTest)Test=new(Gate,()=>{Interlocked.Exchange(ref BeforeSettingsRead,null)?.Invoke();return Settings;},Provider,Queue,(_,_,_)=>{SecretReads++;throw new InvalidOperationException("No key in this fixture.");});
            else{Gate.ConsentTo(new(prefix));Assert.True(Gate.TryEnable(new(prefix),true));Text=new(Gate,()=>Settings,(_,_)=>true,Provider,Queue,Results,TextUpdates.Enqueue,(_,_,_)=>{SecretReads++;throw new InvalidOperationException("No key in this fixture.");});}
        }
        public void Start(string source="PUBLIC_SOURCE")
        {
            if(Test!=null){var intent=Test.Prepare("p",out var reason);Assert.NotNull(intent);Assert.Equal("",reason);Job=Test.Start(intent!,true,TestUpdates.Enqueue)!;Assert.NotNull(Job);}
            else{var selected=new SelectionSourceSnapshot(new(18,93812,132000,@"C:\Fixtures\editor.exe"),source,source,[],[7,8],Settings.Version,source.Length,false);Assert.Equal(TextSubmission.Started,Text!.Submit(Text.Prepare(selected)!));Job=Text.Running;}
        }
        public async Task<Pending> Hold(int ordinal)
        {for(int i=0;;i++){var pending=await Queue.Take();if(i==ordinal)return pending;pending.Apply();}}
        public async Task Settle()
        {using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(!Job.IsCompleted){while(Queue.DrainOne()){}await Task.Delay(2,stop.Token);}await Job;while(Queue.DrainOne()){} }
        public void Invalidate(string action)
        {if(action=="off")Gate.Disable();else if(action=="cancel"){if(Test!=null)Test.Cancel();else Gate.StopGeneration();}else{Assert.True(Gate.TryGetProfileBinding("p",out var binding));if(Test!=null){Test.Cancel();Assert.True(Gate.TryStartConnectionTest(binding,new(new Uri(Settings.Data.Profiles[0].BaseUrl)),Gate.Snapshot.Epoch,out _,out _));}else Assert.True(Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out _));}}
        public async ValueTask DisposeAsync(){Gate.Disable();while(Queue.DrainOne()){}if(Text!=null)await Text.DisposeAsync();if(Test!=null)await Test.DisposeAsync();Provider.Dispose();}
    }
    private static string Json(Protocol protocol,string text="PUBLIC_BODY")=>protocol==Protocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\""+text+"\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2}}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\""+text+"\"}],\"usage\":{\"input_tokens\":1,\"output_tokens\":2},\"stop_reason\":\"end_turn\"}";
    private static string Sse(Protocol protocol,bool error=false)
    {
        string text=protocol==Protocol.OpenAIChat?"data: {\"choices\":[{\"index\":0,\"delta\":{\"content\":\"PUBLIC_BODY\"}}]}\n\n":"data: {\"type\":\"message_start\",\"message\":{}}\n\ndata: {\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"\"}}\n\ndata: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"PUBLIC_BODY\"}}\n\n";
        if(error)return text+"data: {PUBLIC_INVALID_JSON}\n\n";
        return text+(protocol==Protocol.OpenAIChat?"data: {\"choices\":[],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2}}\n\ndata: [DONE]\n\n":"data: {\"type\":\"content_block_stop\",\"index\":0}\n\ndata: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":2}}\n\ndata: {\"type\":\"message_stop\"}\n\n");
    }
    public static IEnumerable<object[]> DeadlineCases()
    {foreach(bool consumer in new[]{false,true})foreach(Protocol protocol in Enum.GetValues<Protocol>())foreach(string mime in new[]{"application/json","text/event-stream"})foreach(int ordinal in new[]{0,1,2,3})foreach(string kind in new[]{"total","phase"})yield return [consumer,protocol,mime,ordinal,kind];}
    [Theory][MemberData(nameof(DeadlineCases))]
    public async Task ActualQueuedApplyRejectsExpiredEventAndPreservesEarlierText(bool connectionTest,Protocol protocol,string mime,int ordinal,string kind)
    {
        await using var rig=new Rig(fixture,connectionTest,protocol,mime,kind=="phase"?120:5);rig.Start();
        await rig.Script.Write(mime=="application/json"?Json(protocol):Sse(protocol),true);var held=await rig.Hold(ordinal);
        rig.Clock.Advance(TimeSpan.FromSeconds(kind=="total"?5:2),dispatch:false);held.Apply();
        string reason=kind=="total"?"REQUEST_TIMEOUT":ordinal==0?"FIRST_RESPONSE_TIMEOUT":"READ_IDLE_TIMEOUT";
        if(connectionTest){var late=rig.TestUpdates.Last();Assert.Equal(ModelEventKind.Failed,late.Event.Kind);Assert.Equal(reason,late.Event.Reason);Assert.DoesNotContain(rig.TestUpdates,u=>u.Event.Kind==ModelEventKind.Completed);Assert.Equal(ordinal>1?"PUBLIC_BODY":"",string.Concat(rig.TestUpdates.Where(u=>u.Event.Kind==ModelEventKind.TextDelta).Select(u=>u.Event.Text)));}
        else{var version=Assert.Single(rig.Results.Versions);Assert.False(version.Complete);Assert.True(version.Stopped);Assert.Equal(ordinal>1?"PUBLIC_BODY":"",rig.Results.Text);Assert.Equal(reason,rig.TextUpdates.Last().Failure);Assert.DoesNotContain(rig.TextUpdates,u=>u.Presentation.Kind==OverlayResultEventKind.Complete);Assert.Equal(new TranslationUsage(ordinal>2&&!(protocol==Protocol.AnthropicMessages&&mime=="text/event-stream")?1:null,ordinal>2?2:null),version.Usage);}
        await rig.Settle();Assert.Equal(1,rig.Script.Requests);Assert.Equal(0,rig.SecretReads);
    }
    [Theory]
    [InlineData(false,Protocol.OpenAIChat,"total")][InlineData(true,Protocol.OpenAIChat,"total")][InlineData(false,Protocol.AnthropicMessages,"total")][InlineData(true,Protocol.AnthropicMessages,"total")]
    [InlineData(false,Protocol.OpenAIChat,"phase")][InlineData(true,Protocol.OpenAIChat,"phase")][InlineData(false,Protocol.AnthropicMessages,"phase")][InlineData(true,Protocol.AnthropicMessages,"phase")]
    public async Task QueuedProtocolErrorAfterPartialTextUsesCurrentDeadline(bool connectionTest,Protocol protocol,string kind)
    {
        await using var rig=new Rig(fixture,connectionTest,protocol,"text/event-stream",kind=="phase"?120:5);rig.Start();await rig.Script.Write(Sse(protocol,true),true);var held=await rig.Hold(2);
        rig.Clock.Advance(TimeSpan.FromSeconds(kind=="total"?5:2),dispatch:false);held.Apply();string reason=kind=="total"?"REQUEST_TIMEOUT":"READ_IDLE_TIMEOUT";
        if(connectionTest){Assert.Equal(reason,rig.TestUpdates.Last().Event.Reason);Assert.Equal("PUBLIC_BODY",string.Concat(rig.TestUpdates.Where(u=>u.Event.Kind==ModelEventKind.TextDelta).Select(u=>u.Event.Text)));}
        else{Assert.Equal(reason,rig.TextUpdates.Last().Failure);Assert.Equal("PUBLIC_BODY",rig.Results.Text);Assert.False(Assert.Single(rig.Results.Versions).Complete);}
        await rig.Settle();Assert.Equal(1,rig.Script.Requests);
    }
    public static IEnumerable<object[]> CancellationCases()
    {foreach(bool consumer in new[]{false,true})foreach(Protocol protocol in Enum.GetValues<Protocol>())foreach(string action in new[]{"cancel","off","replace"})foreach(bool error in new[]{false,true})yield return [consumer,protocol,action,error];}
    [Theory][MemberData(nameof(CancellationCases))]
    public async Task InvalidatedQueuedCompletionOrErrorCannotApply(bool connectionTest,Protocol protocol,string action,bool error)
    {
        await using var rig=new Rig(fixture,connectionTest,protocol,"text/event-stream");rig.Start();await rig.Script.Write(Sse(protocol,error),true);var held=await rig.Hold(error?2:3);
        int before=connectionTest?rig.TestUpdates.Count:rig.TextUpdates.Count;rig.Invalidate(action);held.Apply();await rig.Settle();
        Assert.Equal(before,connectionTest?rig.TestUpdates.Count:rig.TextUpdates.Count);Assert.Equal(1,rig.Script.Requests);Assert.Equal(0,rig.SecretReads);
    }
    [Theory][InlineData(false,Protocol.OpenAIChat,"application/json")][InlineData(true,Protocol.OpenAIChat,"application/json")][InlineData(false,Protocol.AnthropicMessages,"application/json")][InlineData(true,Protocol.AnthropicMessages,"application/json")]
    [InlineData(false,Protocol.OpenAIChat,"text/event-stream")][InlineData(true,Protocol.OpenAIChat,"text/event-stream")][InlineData(false,Protocol.AnthropicMessages,"text/event-stream")][InlineData(true,Protocol.AnthropicMessages,"text/event-stream")]
    public async Task OrderedActualQueueBeforeDeadlineCompletesNormally(bool connectionTest,Protocol protocol,string mime)
    {
        await using var rig=new Rig(fixture,connectionTest,protocol,mime);rig.Start();await rig.Script.Write(mime=="application/json"?Json(protocol):Sse(protocol),true);await rig.Settle();
        if(connectionTest)Assert.Equal(new[]{ModelEventKind.Started,ModelEventKind.TextDelta,ModelEventKind.Usage,ModelEventKind.Completed},rig.TestUpdates.Select(u=>u.Event.Kind));
        else{Assert.True(Assert.Single(rig.Results.Versions).Complete);Assert.Equal(new[]{OverlayResultEventKind.Begin,OverlayResultEventKind.Delta,OverlayResultEventKind.Usage,OverlayResultEventKind.Complete},rig.TextUpdates.Select(u=>u.Presentation.Kind));Assert.Equal(new TranslationUsage(protocol==Protocol.AnthropicMessages&&mime=="text/event-stream"?null:1,2),Assert.Single(rig.Results.Versions).Usage);}
        Assert.Equal(1,rig.Script.Requests);Assert.Equal(0,rig.SecretReads);
    }
    [Theory]
    [InlineData(false,Protocol.OpenAIChat,true)][InlineData(true,Protocol.OpenAIChat,true)][InlineData(false,Protocol.AnthropicMessages,true)][InlineData(true,Protocol.AnthropicMessages,true)]
    [InlineData(false,Protocol.OpenAIChat,false)][InlineData(true,Protocol.OpenAIChat,false)][InlineData(false,Protocol.AnthropicMessages,false)][InlineData(true,Protocol.AnthropicMessages,false)]
    public async Task OldQueuedCompletionCannotTerminateNewRealRequestInEitherDrainOrder(bool connectionTest,Protocol protocol,bool oldFirst)
    {
        await using var rig=new Rig(fixture,connectionTest,protocol,"application/json");rig.Start();await rig.Script.Write(Json(protocol),true);var held=await rig.Hold(3);
        var oldIdentity=connectionTest?rig.TestUpdates.First().Identity:rig.Results.Identity!.Value;var oldJob=rig.Job;
        if(connectionTest){rig.Test!.Cancel();await oldJob.WaitAsync(TimeSpan.FromSeconds(6));}
        rig.Start("PUBLIC_NEW_SOURCE");await rig.Script.Write(Json(protocol,"PUBLIC_NEW_BODY"),true);if(oldFirst)held.Apply();await rig.Settle();if(!oldFirst)held.Apply();await oldJob.WaitAsync(TimeSpan.FromSeconds(6));
        if(connectionTest){var completed=Assert.Single(rig.TestUpdates,u=>u.Event.Kind==ModelEventKind.Completed);Assert.NotEqual(oldIdentity,completed.Identity);Assert.DoesNotContain(rig.TestUpdates,u=>u.Identity==oldIdentity&&u.Event.Kind is ModelEventKind.Completed or ModelEventKind.Failed);Assert.Equal("PUBLIC_NEW_BODY",string.Concat(rig.TestUpdates.Where(u=>u.Identity==completed.Identity&&u.Event.Kind==ModelEventKind.TextDelta).Select(u=>u.Event.Text)));}
        else{Assert.Equal("PUBLIC_NEW_BODY",rig.Results.Text);Assert.True(rig.Results.Versions.Last().Complete);Assert.DoesNotContain(rig.TextUpdates,u=>u.Identity==oldIdentity&&u.Presentation.Kind is OverlayResultEventKind.Complete or OverlayResultEventKind.Stopped);Assert.False(rig.Gate.HasRunningGeneration);}
        Assert.Equal(2,rig.Script.Requests);Assert.Equal(0,rig.SecretReads);
    }
    [Theory][InlineData(false,Protocol.OpenAIChat)][InlineData(true,Protocol.OpenAIChat)][InlineData(false,Protocol.AnthropicMessages)][InlineData(true,Protocol.AnthropicMessages)]
    public async Task RealTimerCancellationWhileCompletionQueuedStillStopsWithoutSuccess(bool connectionTest,Protocol protocol)
    {
        await using var rig=new Rig(fixture,connectionTest,protocol,"application/json");rig.Start();await rig.Script.Write(Json(protocol),true);var held=await rig.Hold(3);rig.Clock.Advance(TimeSpan.FromSeconds(5));held.Apply();await rig.Settle();
        if(connectionTest){Assert.Equal(ModelEventKind.Failed,rig.TestUpdates.Last().Event.Kind);Assert.Equal("REQUEST_TIMEOUT",rig.TestUpdates.Last().Event.Reason);Assert.DoesNotContain(rig.TestUpdates,u=>u.Event.Kind==ModelEventKind.Completed);}
        else{Assert.False(Assert.Single(rig.Results.Versions).Complete);Assert.Equal("PUBLIC_BODY",rig.Results.Text);Assert.Equal("REQUEST_TIMEOUT",rig.TextUpdates.Last().Failure);}
    }
    [Theory][InlineData(Protocol.OpenAIChat,"total")][InlineData(Protocol.AnthropicMessages,"total")][InlineData(Protocol.OpenAIChat,"phase")][InlineData(Protocol.AnthropicMessages,"phase")]
    public async Task ActualCurrentCheckCannotCrossDeadlineBeforeFixedTestApply(Protocol protocol,string kind)
    {
        await using var rig=new Rig(fixture,true,protocol,"application/json",kind=="phase"?120:5);rig.Start();await rig.Script.Write(Json(protocol),true);var held=await rig.Hold(3);
        rig.BeforeSettingsRead=()=>rig.Clock.Advance(TimeSpan.FromSeconds(kind=="total"?5:2),dispatch:false);held.Apply();await rig.Settle();
        Assert.Equal(ModelEventKind.Failed,rig.TestUpdates.Last().Event.Kind);Assert.Equal(kind=="total"?"REQUEST_TIMEOUT":"READ_IDLE_TIMEOUT",rig.TestUpdates.Last().Event.Reason);Assert.DoesNotContain(rig.TestUpdates,u=>u.Event.Kind==ModelEventKind.Completed);
    }
    [Theory][InlineData(false,Protocol.OpenAIChat)][InlineData(true,Protocol.OpenAIChat)][InlineData(false,Protocol.AnthropicMessages)][InlineData(true,Protocol.AnthropicMessages)]
    public async Task RepeatedUsageQueuedLaterCannotRestartOriginalIdleOrigin(bool connectionTest,Protocol protocol)
    {
        await using var rig=new Rig(fixture,connectionTest,protocol,"text/event-stream",120);rig.Start();(await rig.Queue.Take()).Apply();
        string valid=Sse(protocol).Replace(protocol==Protocol.OpenAIChat?"data: [DONE]\n\n":"data: {\"type\":\"message_stop\"}\n\n","");await rig.Script.Write(valid);(await rig.Queue.Take()).Apply();(await rig.Queue.Take()).Apply();await rig.Clock.WaitDeadline(2);
        rig.Clock.Advance(TimeSpan.FromSeconds(1),dispatch:false);
        string repeated=protocol==Protocol.OpenAIChat?"data: {\"choices\":[],\"usage\":{\"prompt_tokens\":1,\"completion_tokens\":2}}\n\n":"data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":2}}\n\n";
        await rig.Script.Write(repeated);var held=await rig.Queue.Take();rig.Clock.Advance(TimeSpan.FromSeconds(1),dispatch:false);held.Apply();
        if(connectionTest){Assert.Equal(ModelEventKind.Failed,rig.TestUpdates.Last().Event.Kind);Assert.Equal("READ_IDLE_TIMEOUT",rig.TestUpdates.Last().Event.Reason);Assert.Equal(1,rig.TestUpdates.Count(u=>u.Event.Kind==ModelEventKind.Usage));}
        else{Assert.Equal("PUBLIC_BODY",rig.Results.Text);Assert.Equal("READ_IDLE_TIMEOUT",rig.TextUpdates.Last().Failure);Assert.True(Assert.Single(rig.Results.Versions).Stopped);}
        await rig.Settle();
    }
}
