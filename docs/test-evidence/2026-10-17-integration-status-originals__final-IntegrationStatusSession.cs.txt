namespace KuaiYi.Core.Integration;

public enum IntegrationRefreshStatus { Published, Cancelled, Busy, Failed }
/// <summary>One actual read at a time, including cancellation callback settlement. The UI pulls a typed snapshot.</summary>
public sealed class IntegrationStatusSession:IDisposable
{
    private sealed class Operation(CancellationToken caller)
    {public readonly CancellationTokenSource Source=new();public readonly CancellationToken Caller=caller;public bool Valid=true,Finishing;public Task? Cancellation;}
    private readonly object sync=new();private Operation? pending;private bool disposed;
    private IntegrationSnapshot? snapshot;private bool cancellationFailed;
    public bool CancellationFailed {get{lock(sync)return cancellationFailed;}}
    public bool Busy {get{lock(sync)return pending!=null;}}
    public IntegrationSnapshot? Snapshot {get{lock(sync)return snapshot;}}
    public void Invalidate(){lock(sync){snapshot=null;Cancel(pending);}}
    private void Cancel(Operation? owned)
    {
        if(owned==null)return;owned.Valid=false;
        if(owned.Finishing||owned.Cancellation!=null)return;
        // No reader cancellation callback runs under the gate or on the WPF thread.
        owned.Cancellation=Task.Run(async()=>{try{await owned.Source.CancelAsync().ConfigureAwait(false);}catch(Exception){lock(sync)cancellationFailed=true;}});
    }
    public async Task<IntegrationRefreshStatus> RefreshAsync(Func<CancellationToken,Task<IntegrationSnapshot>> read,CancellationToken cancellation=default)
    {
        Operation owned;
        lock(sync)
        {
            if(disposed||cancellation.IsCancellationRequested)return IntegrationRefreshStatus.Cancelled;
            if(pending!=null)return IntegrationRefreshStatus.Busy;
            snapshot=null;cancellationFailed=false;owned=pending=new(cancellation);
        }
        var bridge=cancellation.Register(()=>{lock(sync)Cancel(owned);});
        var result=IntegrationRefreshStatus.Cancelled;
        try
        {
            var value=await Task.Run(()=>read(owned.Source.Token),CancellationToken.None).ConfigureAwait(false);
            lock(sync)
            {
                if(!disposed&&owned.Valid&&!owned.Caller.IsCancellationRequested&&!owned.Source.IsCancellationRequested)
                {snapshot=value;result=IntegrationRefreshStatus.Published;}
            }
        }
        catch(OperationCanceledException)when(!owned.Valid||owned.Caller.IsCancellationRequested||owned.Source.IsCancellationRequested){result=IntegrationRefreshStatus.Cancelled;}
        catch(Exception){result=IntegrationRefreshStatus.Failed;}
        finally
        {
            Task settlement;lock(sync){owned.Finishing=true;settlement=owned.Cancellation??Task.CompletedTask;}
            await settlement.ConfigureAwait(false);await bridge.DisposeAsync().ConfigureAwait(false);
            lock(sync){if(pending==owned)pending=null;owned.Source.Dispose();}
        }
        return result;
    }
    public void Dispose(){lock(sync){disposed=true;snapshot=null;Cancel(pending);}}
}
