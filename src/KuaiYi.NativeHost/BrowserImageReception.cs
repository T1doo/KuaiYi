using KuaiYi.Core;
using System.Runtime.ExceptionServices;
namespace KuaiYi.NativeHost;

public readonly record struct BrowserImageSourceState(long Epoch,Activation Activation,string SettingsVersion,long ImageRevision,long ResultRevision,bool Available);
/// <summary>Local transfer authority is separate from the Shell's activation/profile/send gate.
/// Every UI callback is dispatched to the original destination; original tasks remain held until completion.</summary>
public sealed class BrowserImageReception:ILocalImageConfirmation,IDisposable
{
    private sealed record Admission(LocalImageConfirmation Request,BrowserImageSourceState State,long Revision);
    private readonly SynchronizationContext destination;private readonly Func<BrowserImageSourceState> state;
    private readonly Func<bool> available;private readonly ILocalImageConfirmation prompt;private readonly Func<ReadOnlyMemory<byte>,BrowserImagePreparationPermit,Task> prepare;
    private Admission? admission;private long revision;private bool disposed;private BrowserImagePreparationPermit? preparing;private int preparationReturned;
    public RuntimeGate TransferGate{get;}=new();
    public LocalImageInbox Inbox{get;}
    public bool Busy=>Volatile.Read(ref admission)!=null;
    public bool CleanupPending=>Volatile.Read(ref preparing)!=null&&Volatile.Read(ref preparationReturned)==1;
    public bool Current=>Volatile.Read(ref admission) is {} value&&Matches(value);
    public BrowserImageReception(SynchronizationContext destination,Func<BrowserImageSourceState> state,Func<bool> available,ILocalImageConfirmation prompt,Func<ReadOnlyMemory<byte>,BrowserImagePreparationPermit,Task> prepare)
    {this.destination=destination;this.state=state;this.available=available;this.prompt=prompt;this.prepare=prepare;Inbox=new(TransferGate,destination);}
    public async Task<bool> ConfirmAsync(LocalImageConfirmation request,CancellationToken cancellation)
    {
        var held=await OnDestination(()=>
        {
            if(Volatile.Read(ref disposed)||Volatile.Read(ref admission)!=null||Volatile.Read(ref preparing)!=null)return null;long command=Interlocked.Read(ref revision);
            bool canStart=available();var snapshot=state();
            if(!canStart||!snapshot.Available||Volatile.Read(ref disposed)||Interlocked.Read(ref revision)!=command||Volatile.Read(ref admission)!=null)return null;
            var value=new Admission(request,snapshot,Interlocked.Increment(ref revision));Volatile.Write(ref admission,value);return value;
        }).ConfigureAwait(false);
        if(held==null)return false;bool accepted=false;
        try
        {
            accepted=await prompt.ConfirmAsync(request,cancellation).ConfigureAwait(false);
            return accepted=await OnDestination(()=>accepted&&!cancellation.IsCancellationRequested&&ReferenceEquals(admission,held)&&Matches(held)).ConfigureAwait(false);
        }
        finally{if(!accepted)await OnDestination(()=>{if(ReferenceEquals(Interlocked.CompareExchange(ref admission,null,held),held)){Interlocked.Increment(ref revision);}return true;}).ConfigureAwait(false);}
    }
    public async Task CompleteAsync(BrowserReceiveCompletion completion)
    {
        LocalAcceptedImage? receipt=null;BrowserImagePreparationPermit? permit=null;Exception? failure=null;
        try
        {
            var original=await OnDestination(()=>
            {
                var held=admission;
                if(held==null||held.Request.ConnectionId!=completion.ConnectionId)return Task.CompletedTask;
                bool eligible=Matches(held);
                if(!ReferenceEquals(admission,held))return Task.CompletedTask;
                if(eligible&&completion.CommittedIdentity is {} identity)receipt=Inbox.TakeCurrent(identity);
                if(!ReferenceEquals(Interlocked.CompareExchange(ref admission,null,held),held))return Task.CompletedTask;
                if(completion.CommittedIdentity is {} finished){Inbox.Clear(finished);TransferGate.StopRequest(finished);}
                // A cancellation callback can reenter Shell state. Claiming alone is not decode/send authority.
                if(receipt==null||!eligible)return Task.CompletedTask;
                permit=new(held.State,()=>!Volatile.Read(ref disposed)&&held.Revision==Interlocked.Read(ref revision));
                Volatile.Write(ref preparationReturned,0);Volatile.Write(ref preparing,permit);
                if(!Matches(held)||!permit.IsCurrent)return Task.CompletedTask;
                return prepare(receipt.Bytes,permit);
            }).ConfigureAwait(false);
            await original.ConfigureAwait(false);
        }
        catch(Exception error){failure=error;}
        finally
        {
            if(permit!=null)Volatile.Write(ref preparationReturned,1);
            try{if(permit!=null)await permit.DisposeAsync().ConfigureAwait(false);}
            catch(Exception error){failure=failure==null?error:new AggregateException("Preparation and cancellation settlement failed.",failure,error);}
            finally{if(permit!=null)Interlocked.CompareExchange(ref preparing,null,permit);receipt?.Dispose();}
        }
        if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
    }
    private bool Matches(Admission held){var current=state();return !Volatile.Read(ref disposed)&&held.Revision==Interlocked.Read(ref revision)&&current==held.State&&held.State.Available;}
    public void Revoke()
    {
        Interlocked.Increment(ref revision);Interlocked.Exchange(ref admission,null);
        try{Volatile.Read(ref preparing)?.Revoke();}
        finally{try{TransferGate.Disable();}finally{Inbox.Clear();}}
    }
    public void Dispose(){Volatile.Write(ref disposed,true);try{Revoke();}finally{Inbox.Dispose();}}
    private Task<T> OnDestination<T>(Func<T> action)
    {
        var original=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        try{destination.Post(_=>{try{original.TrySetResult(action());}catch(Exception error){original.TrySetException(error);}},null);}catch(Exception error){original.TrySetException(error);}
        return original.Task;
    }
}
