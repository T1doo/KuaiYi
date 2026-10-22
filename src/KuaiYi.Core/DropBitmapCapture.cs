namespace KuaiYi.Core;
public sealed partial class DropCaptureCoordinator
{
    private readonly HashSet<OwnedOleBitmap> queuedBitmaps=[];
    private void ClearQueuedBitmaps(){foreach(var item in queuedBitmaps)item.Dispose();}
    public bool IsLeaseCurrent(DropCaptureLease expected){lock(sync)return Current(expected);}
    public DropCaptureLease? OpenBitmapPreview(Guid receiver,long wireEpoch,DropTargetGeometry target)
    {
        lock(sync)
        {
            InvalidateLocked(false);if(disposed||receiver==Guid.Empty||wireEpoch<0||wireEpoch>long.MaxValue/2||!target.Valid||!visible()||settings() is not {} current||current.ReadOnly||current.RequiresRecovery||!gate.TryBeginLocalBitmapDrop(out var ticket))return null;
            config=current;geometry=target;return lease=new(receiver,wireEpoch,default,current.Version,ticket);
        }
    }
    // Callback consumes the independent owner unconditionally, including when it rejects admission.
    public Task ReceiveBitmapAsync(DropCaptureLease expected,Guid transaction,ReadOnlyMemory<byte> bytes,Func<Action,bool> receiverAdmission,Action<OwnedOleBitmap> accept,CancellationToken cancellation=default,Func<bool>? receiverCurrent=null)
    {
        lock(sync)
        {
            if(cancellation.IsCancellationRequested||!Current(expected)||drop is not {} work||work.Transaction!=transaction||work.Generation!=generation||!SourceCurrent(work.Source))return Task.CompletedTask;
            drop=null;pending?.Cancel();var token=pending=CancellationTokenSource.CreateLinkedTokenSource(cancellation);var task=DeliverBitmap(expected,work,bytes,receiverAdmission,accept,token,receiverCurrent??(static()=>true));operations.Add(task);_=Observe(task,token);return task;
        }
    }
    private async Task DeliverBitmap(DropCaptureLease expected,DropWork work,ReadOnlyMemory<byte> bytes,Func<Action,bool> receiverAdmission,Action<OwnedOleBitmap> accept,CancellationTokenSource cancellation,Func<bool> receiverCurrent)
    {
        OwnedOleBitmap? owner=null;
        bool CurrentWork()=>SourceCurrent(work.Source)&&!cancellation.IsCancellationRequested&&generation==work.Generation&&geometry?.Anchor==work.Target.Anchor&&Current(expected);
        bool Timely()=>clock.GetElapsedTime(work.Started)<TimeSpan.FromMilliseconds(1500);
        try
        {
            if(!CurrentWork()||!Timely())return;owner=new(bytes.Span);bytes=default;lock(sync)queuedBitmaps.Add(owner);
            var registeredOwner=owner;using var release=cancellation.Token.Register(()=>{lock(sync){if(queuedBitmaps.Contains(registeredOwner))registeredOwner.Dispose();}});var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var copied=owner;
            destination.Post(_=>
            {
                bool transferred=false;
                try
                {
                    lock(sync)
                    {
                        if(!CurrentWork())return;
                        receiverAdmission(()=>
                        {
                            bool Check()=>CurrentWork()&&Timely()&&receiverCurrent();
                            void Commit(){queuedBitmaps.Remove(copied);release.Unregister();transferred=true;accept(copied);}
                            if(expected.BitmapPreview is {} local)gate.TryCommitLocalBitmapDrop(local,Check,Commit);else gate.TryCommitCapture(expected.Ticket,Check,Commit);
                        });
                    }
                }
                catch(Exception error){done.TrySetException(error);}
                finally{if(!transferred)copied.Dispose();done.TrySetResult();}
            },null);
            await done.Task.WaitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){}
        finally{if(owner!=null)lock(sync){if(queuedBitmaps.Remove(owner))owner.Dispose();}}
    }
}
