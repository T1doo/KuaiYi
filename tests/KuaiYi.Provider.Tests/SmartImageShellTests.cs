using KuaiYi.Core;
using KuaiYi.Imaging;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed partial class ProductionScreenshotShellTests
{
    private sealed class SmartQueue:SynchronizationContext
    {
        private readonly Queue<Action> pending=[];
        public override void Post(SendOrPostCallback action,object? state){lock(pending)pending.Enqueue(()=>action(state));}
        internal void Drain(){while(true){Action next;lock(pending){if(!pending.TryDequeue(out next!))return;}next();}}
    }
    [Theory][InlineData("clear")][InlineData("replace")][InlineData("off")]
    public async Task SmartOriginalEntryCannotRetargetAfterItsFirstYield(string change)
    {
        await using var r=await Rig.Create();var w=r.Window;var queue=new SmartQueue();var previous=SynchronizationContext.Current;int calls=0;Task original;
        try{SynchronizationContext.SetSynchronizationContext(queue);original=w.TrackSmartForTest(()=>{calls++;return Task.CompletedTask;});}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
        Task<bool>? clear=null;
        try
        {
            if(change=="clear"){clear=w.ClearContentForTest();Assert.False(clear.IsCompleted);}
            if(change=="off")w.MarkdownGate.Disable();
            if(change=="replace")
            {
                var input=w.ImageInputForTest();using(input.Intent)using(input.Cancellation)
                {var replacement=new OwnedSourceImage(new ScreenshotPixels(new(0,0,8,8),Enumerable.Repeat((byte)255,256).ToArray()));Assert.True(input.Commit(replacement));w.EndRestoreForTest(input.Cancellation);}
            }
            queue.Drain();await original.WaitAsync(TimeSpan.FromSeconds(6));if(clear!=null)Assert.True(await clear.WaitAsync(TimeSpan.FromSeconds(6)));Assert.Equal(0,calls);Assert.Equal(0,r.Script.Requests);
        }
        finally{queue.Drain();await original.WaitAsync(TimeSpan.FromSeconds(6));if(clear!=null)await clear.WaitAsync(TimeSpan.FromSeconds(6));}
    }
    [Fact]public async Task SmartShellLocalApprovalStillNeedsIndependentSendConfirmation()
    {
        await using var r=await Rig.Create();var w=r.Window;using var approval=Assert.IsType<OcrImageApproval>(SmartOcrFixture.Approve(w.MarkdownGate,()=>r.Config,w.SelectedSourceVersionForTest!.Value,r.Image,(v,id)=>w.SelectedSourceVersionForTest==v&&w.SelectedImageForTest?.SourceId==id));int confirmations=0;
        Assert.False(await w.SendSmartForTest(approval,offer=>{confirmations++;Assert.Contains("分类请求仍发送当前图片",offer.ConfirmationText);return false;}));Assert.Equal(1,confirmations);Assert.Equal(2,approval.State);Assert.Equal(0,r.Script.Requests);Assert.Same(r.Image,w.SelectedImageForTest);
    }
    [Fact]public async Task SmartShellClearJoinsOriginalParentAndLocalWorkerBeforeReturning()
    {
        await using var r=await Rig.Create();var w=r.Window;var version=w.SelectedSourceVersionForTest!.Value;var worker=new SmartOcrFixture.Worker{Hold=true};
        await using var local=new OcrOnlyCoordinator(w.MarkdownGate,()=>r.Config,(v,id)=>w.SelectedSourceVersionForTest==v&&w.SelectedImageForTest?.SourceId==id,new Immediate(),()=>worker);
        w.OcrCleanupForTest=()=>local.Clear();Task? clear=null;
        var original=w.TrackSmartForTest(async()=>{Assert.True(local.Start(version,r.Image,"en-US"));await local.Running;});
        try
        {
            await worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(6));clear=w.ClearContentForTest();Assert.False(clear.IsCompleted);Assert.False(original.IsCompleted);Assert.True(worker.Cancellation.IsCancellationRequested);Assert.Contains(worker.Loan.ToArray(),b=>b!=0);Assert.False(local.Start(version,r.Image,"en-US"));
            worker.Release.TrySetResult();await clear.WaitAsync(TimeSpan.FromSeconds(6));await original;Assert.True(worker.Disposed);Assert.True(worker.Loan.Span.IndexOfAnyExcept((byte)0)<0);Assert.Null(local.Preview);Assert.Equal(0,r.Script.Requests);
        }
        finally{worker.Release.TrySetResult();await original.WaitAsync(TimeSpan.FromSeconds(6));if(clear!=null)await clear.WaitAsync(TimeSpan.FromSeconds(6));w.OcrCleanupForTest=null;}
    }
    [Fact]public async Task SmartShellJoinsItsOfferTaskWhenBeginSynchronouslyStartsAnotherImage()
    {
        await using var r=await Rig.Create();var w=r.Window;await r.Flow.DisposeAsync();bool replace=true;KuaiYi.App.ProductionScreenshotOffer? seen=null;Task? second=null;
        r.Flow=w.ConfigureScreenshotForTest(r.Config,r.Provider,new Immediate(),observe:update=>
        {
            if(update.Kind!=KuaiYi.App.ProductionScreenshotEvent.Begin||!replace)return;replace=false;var input=w.ImageInputForTest();using(input.Intent)using(input.Cancellation)
            {var image=new OwnedSourceImage(new ScreenshotPixels(new(0,0,8,8),Enumerable.Repeat((byte)255,256).ToArray()));Assert.True(input.Commit(image));w.EndRestoreForTest(input.Cancellation);}
            Assert.True(w.SendScreenshotForTest(KuaiYi.Providers.ImageOutputMode.ExplainImage,_=>true));second=r.Flow.Running;
        });
        using var approval=Assert.IsType<OcrImageApproval>(SmartOcrFixture.Approve(w.MarkdownGate,()=>r.Config,w.SelectedSourceVersionForTest!.Value,r.Image,(v,id)=>w.SelectedSourceVersionForTest==v&&w.SelectedImageForTest?.SourceId==id));var queue=new SmartQueue();var previous=SynchronizationContext.Current;Task<bool> smart;
        try{SynchronizationContext.SetSynchronizationContext(queue);smart=w.SendSmartForTest(approval,offer=>{seen=offer;return true;});}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
        using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(!smart.IsCompleted){guard.Token.ThrowIfCancellationRequested();queue.Drain();await Task.Yield();}
        Assert.True(await smart);Assert.NotNull(seen);Assert.NotNull(second);Assert.NotSame(seen.OriginalTask,second);Assert.True(seen.OriginalTask.IsCompleted);Assert.False(second.IsCompleted);Assert.Equal(2,approval.State);
    }

}
