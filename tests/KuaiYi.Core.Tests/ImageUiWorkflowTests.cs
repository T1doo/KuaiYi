using System.Collections.Concurrent;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ImageUiWorkflowTests
{
    private sealed class QueueContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback,object? State)> queue = new();
        private readonly SemaphoreSlim posted = new(0);
        public bool Draining { get; private set; }
        public override void Post(SendOrPostCallback callback,object? state) { queue.Enqueue((callback,state));posted.Release(); }
        public async Task WaitPending() { await posted.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));posted.Release(); }
        public async Task DrainOne() { await posted.WaitAsync().WaitAsync(TimeSpan.FromSeconds(5));Assert.True(queue.TryDequeue(out var item));Draining=true;try{item.Callback(item.State);}finally{Draining=false;} }
    }
    private static (RuntimeGate Gate,RequestIdentity Identity) Prepare()
    {
        var gate=new RuntimeGate();Assert.True(gate.TryEnableLocalOperation(true));Assert.True(gate.TryStartRequest(Guid.NewGuid(),Guid.NewGuid(),null,out var identity,out _));return(gate,identity);
    }
    private static ImageReply PublicResult(RequestIdentity identity,string text) => new(new(identity.SessionId,1,identity.Epoch,"en-US",1),ImageFailure.None,new(1,1,"en-US",text,[]));
    private static ImageUiUpdate Update(ImageUiEventKind kind) => kind switch
    {
        ImageUiEventKind.Started=>ImageUiUpdate.Started(),ImageUiEventKind.Completed=>new(kind,"public completed","public old text"),
        ImageUiEventKind.Cancelled=>ImageUiUpdate.Cancelled(),ImageUiEventKind.ExitUnconfirmed=>ImageUiUpdate.Unconfirmed(),_=>ImageUiUpdate.Failed(new IOException("public fixture"))
    };
    public static IEnumerable<object[]> QueueCases() => from kind in Enum.GetValues<ImageUiEventKind>() from action in new[]{"off","replace","stop","lock"} from before in new[]{true,false} select new object[]{kind,action,before};
    [Theory] [MemberData(nameof(QueueCases))]
    public async Task EveryImageEventChecksFullGateAtActualDestination(ImageUiEventKind kind,string action,bool queuedBefore)
    {
        var(gate,a)=Prepare();var destination=new QueueContext();var workflow=new ImageUiWorkflow(gate,destination);int writes=0;string status="current status",text="current result";
        void Apply(ImageUiUpdate update){Assert.True(destination.Draining);writes++;status=update.Status;text=update.Text;}
        Task<bool>? pending=queuedBefore?workflow.PublishAsync(a,Update(kind),Apply):null;
        if(action=="off")gate.Disable();else if(action=="stop")gate.StopGeneration();else if(action=="lock")gate.SetSessionLocked(true);else Assert.True(gate.TryStartRequest(a.SessionId,Guid.NewGuid(),null,out _,out _));
        pending??=workflow.PublishAsync(a,Update(kind),Apply);await destination.DrainOne();Assert.False(await pending);Assert.Equal(0,writes);Assert.Equal("current status",status);Assert.Equal("current result",text);
    }
    [Theory] [InlineData("session")] [InlineData("version")] [InlineData("request")] [InlineData("epoch")]
    public async Task ForgedIdentityNeverAppliesImageError(string field)
    {
        var(gate,a)=Prepare();var destination=new QueueContext();var workflow=new ImageUiWorkflow(gate,destination);
        var forged=field switch{"session"=>a with{SessionId=Guid.NewGuid()},"version"=>a with{VersionId=Guid.NewGuid()},"request"=>a with{RequestId=Guid.NewGuid()},_=>a with{Epoch=a.Epoch+1}};
        int writes=0;var pending=workflow.PublishAsync(forged,ImageUiUpdate.Failed(new InvalidOperationException("fixture")),_=>writes++);await destination.DrainOne();Assert.False(await pending);Assert.Equal(0,writes);
    }
    [Theory] [InlineData(ImageUiEventKind.Started)] [InlineData(ImageUiEventKind.Completed)] [InlineData(ImageUiEventKind.Failed)] [InlineData(ImageUiEventKind.Cancelled)] [InlineData(ImageUiEventKind.ExitUnconfirmed)]
    public async Task CurrentEventsApplyOnlyOnDestinationAndExactlyOnce(ImageUiEventKind kind)
    {
        var(gate,a)=Prepare();var destination=new QueueContext();var workflow=new ImageUiWorkflow(gate,destination);int writes=0;
        var pending=workflow.PublishAsync(a,Update(kind),_=>{Assert.True(destination.Draining);writes++;});Assert.Equal(0,writes);await destination.DrainOne();Assert.True(await pending);Assert.Equal(1,writes);
    }
    public static IEnumerable<object[]> LateCases()=>from outcome in new[]{"success","failure","cancel"} from replace in new[]{false,true} from queued in new[]{false,true} select new object[]{outcome,replace,queued};
    [Theory] [MemberData(nameof(LateCases))]
    public async Task ActualWorkflowAThenOffOrBRejectsLateOutcome(string outcome,bool replace,bool queuedBeforeInvalidation)
    {
        var(gate,a)=Prepare();var destination=new QueueContext();var workflow=new ImageUiWorkflow(gate,destination);
        var source=new TaskCompletionSource<ImageReply>(TaskCreationOptions.RunContinuationsAsynchronously);var entered=new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        string status="initial",text="";int writes=0;
        void Apply(ImageUiUpdate update){Assert.True(destination.Draining);status=update.Status;text=update.Text;writes++;}
        var old=workflow.RunAsync(a,token=>{entered.SetResult(token);return source.Task;},Apply);await destination.DrainOne();var oldCancellation=await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        void FinishOld(){if(outcome=="success")source.SetResult(PublicResult(a,"public A"));else if(outcome=="failure")source.SetException(new IOException("public A failure"));else source.SetCanceled();}
        if(queuedBeforeInvalidation){FinishOld();await destination.WaitPending();}
        RequestIdentity b=default;Task<bool>? next=null;
        if(replace)
        {
            Assert.True(gate.TryStartRequest(a.SessionId,Guid.NewGuid(),null,out b,out _));status="public B starting";text="public B initial";
            next=workflow.RunAsync(b,_=>Task.FromResult(PublicResult(b,"public B result")),Apply);
        }
        else{gate.Disable();status="Off";text="";}
        Assert.True(oldCancellation.IsCancellationRequested);
        if(queuedBeforeInvalidation){await destination.DrainOne();Assert.False(await old);Assert.Equal(replace?"public B starting":"Off",status);Assert.Equal(replace?"public B initial":"",text);}
        if(replace){await destination.DrainOne();await destination.DrainOne();Assert.True(await next!);Assert.Equal("本地 OCR 完成",status);Assert.Equal("public B result",text);Assert.True(gate.CanApply(b));}
        int currentWrites=writes;string currentStatus=status,currentText=text;
        if(!queuedBeforeInvalidation){FinishOld();await destination.DrainOne();Assert.False(await old);}
        Assert.Equal(currentWrites,writes);Assert.Equal(currentStatus,status);Assert.Equal(currentText,text);
    }
    [Fact] public void LocalEnableDoesNotAuthorizeAnyEndpoint()
    {
        var(gate,a)=Prepare();var origin=new EndpointOrigin(new Uri("https://public-fixture.invalid"));Assert.False(gate.CanSend(a,origin));Assert.False(gate.TryBeginSend(a,origin,()=>Task.FromResult(1),out _));
    }
    [Fact] public void LocalEnableRespectsConfigurationAndSessionLock()
    {
        var gate=new RuntimeGate();Assert.False(gate.TryEnableLocalOperation(false));gate.SetSessionLocked(true);Assert.False(gate.TryEnableLocalOperation(true));Assert.Equal(Activation.Off,gate.Snapshot.Activation);
    }
    [Fact] public async Task DestinationCallbackFailuresAreNotSilenced()
    {
        var(gate,a)=Prepare();var destination=new QueueContext();var workflow=new ImageUiWorkflow(gate,destination);var pending=workflow.PublishAsync(a,ImageUiUpdate.Started(),_=>throw new InvalidOperationException("public callback fixture"));await destination.DrainOne();await Assert.ThrowsAsync<InvalidOperationException>(()=>pending);
    }
}
