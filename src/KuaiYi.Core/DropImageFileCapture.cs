namespace KuaiYi.Core;
public sealed partial class DropCaptureCoordinator
{
    private sealed class PendingFilePath(string? path)
    {internal bool HasValue=>Volatile.Read(ref path)!=null;internal string? Take()=>Interlocked.Exchange(ref path,null);internal void Clear()=>Interlocked.Exchange(ref path,null);}
    private readonly HashSet<PendingFilePath> queuedFiles=[];
    internal int PendingFileCountForTest{get{lock(sync)return queuedFiles.Count(x=>x.HasValue);}}
    private void ClearQueuedFiles(){foreach(var item in queuedFiles)item.Clear();}
    /// <summary>Transfers only a validated copied path on the original Drop admission. The callback must install, not await, its independent file task.</summary>
    public Task ReceiveImageFileAsync(DropCaptureLease expected,Guid transaction,ReadOnlyMemory<byte> bytes,Func<Action,bool> receiverAdmission,Action<string> acceptedFile,CancellationToken cancellation=default,Func<bool>? receiverCurrent=null)
    {
        lock(sync)
        {
            if(cancellation.IsCancellationRequested||!Current(expected)||drop is not {} work||work.Transaction!=transaction||work.Generation!=generation||!SourceCurrent(work.Source))return Task.CompletedTask;
            drop=null;pending?.Cancel();var token=pending=CancellationTokenSource.CreateLinkedTokenSource(cancellation);var task=DeliverImageFile(expected,work,bytes,receiverAdmission,acceptedFile,token,receiverCurrent??(static()=>true));operations.Add(task);_=Observe(task,token);return task;
        }
    }
    private async Task DeliverImageFile(DropCaptureLease expected,DropWork work,ReadOnlyMemory<byte> bytes,Func<Action,bool> receiverAdmission,Action<string> acceptedFile,CancellationTokenSource cancellation,Func<bool> receiverCurrent)
    {
        PendingFilePath? owner=null;
        bool CurrentWork()=>SourceCurrent(work.Source)&&!cancellation.IsCancellationRequested&&generation==work.Generation&&geometry?.Anchor==work.Target.Anchor&&Current(expected);
        bool Timely()=>clock.GetElapsedTime(work.Started)<TimeSpan.FromMilliseconds(1500);
        try
        {
            if(!CurrentWork())return;string? path=null;DropCaptureStatus? failure=null;
            try{if(!Timely())failure=DropCaptureStatus.Timeout;else path=OleImageFileDrop.DecodeTransport(bytes.Span);}
            catch(Exception error)when(error is InvalidDataException or System.Text.DecoderFallbackException){failure=DropCaptureStatus.Unsupported;}
            owner=new(path);path=null;bytes=default;lock(sync)queuedFiles.Add(owner);using var release=cancellation.Token.Register(owner.Clear);var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var payloadOwner=owner;
            destination.Post(_=>
            {
                try
                {
                    lock(sync)
                    {
                        if(!CurrentWork())return;
                        receiverAdmission(()=>gate.TryCommitCapture(expected.Ticket,()=>CurrentWork()&&Timely()&&receiverCurrent(),()=>
                        {string? value=payloadOwner.Take();if(value==null){failed(failure??DropCaptureStatus.Unsupported);return;}acceptedFile(value);}));
                    }
                }
                catch(Exception error){done.TrySetException(error);}
                finally{payloadOwner.Clear();done.TrySetResult();}
            },null);
            await done.Task.WaitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
        finally{if(owner!=null){owner.Clear();lock(sync)queuedFiles.Remove(owner);}}
    }
}
