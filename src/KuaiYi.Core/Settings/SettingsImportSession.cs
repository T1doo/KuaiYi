namespace KuaiYi.Core.Settings;

public enum SettingsImportStatus{Preview,Cancelled,Busy,Failed}
public sealed record SettingsImportResult(SettingsImportStatus Status,SettingsImportPlan? Preview=null,Exception? Failure=null);
/// <summary>Own one read and its cancellation through settlement. Only an exact, confirmed preview can replace a still-pristine draft.</summary>
public sealed class SettingsImportSession:IDisposable
{
    private sealed class Operation(CancellationToken caller)
    {public readonly CancellationTokenSource Source=new();public readonly CancellationToken Caller=caller;public Task? Cancellation;public Exception? Failure;public bool Finishing,Withdrawn;}
    private readonly object sync=new();private bool disposed;private Operation? pending;private SettingsImportPlan? preview;private SettingsImportBasis? basis;private CancellationToken caller;
    public bool Busy{get{lock(sync)return pending!=null;}}
    public SettingsImportPlan? Preview{get{lock(sync)return !caller.IsCancellationRequested?preview:null;}}
    public void Invalidate(){lock(sync){preview=null;basis=null;RequestCancellation(pending);}}
    private void RequestCancellation(Operation? operation)
    {
        if(operation!=null)operation.Withdrawn=true;
        if(operation is not {Finishing:false,Cancellation:null})return;
        operation.Cancellation=Task.Run(async()=>{try{await operation.Source.CancelAsync().ConfigureAwait(false);}catch(Exception error){lock(sync)operation.Failure=error;}});
    }
    public async Task<SettingsImportResult> ReadAsync(SettingsImportBasis target,Func<CancellationToken,Task<SettingsImportData>> read,CancellationToken cancellation=default)
    {
        Operation owned;
        lock(sync)
        {
            if(disposed||cancellation.IsCancellationRequested)return new(SettingsImportStatus.Cancelled);
            if(pending!=null)return new(SettingsImportStatus.Busy);
            preview=null;basis=null;owned=pending=new(cancellation);
        }
        using var bridge=cancellation.Register(()=>{lock(sync){if(ReferenceEquals(pending,owned)){preview=null;basis=null;}RequestCancellation(owned);}});
        var result=new SettingsImportResult(SettingsImportStatus.Cancelled);
        try
        {
            owned.Source.Token.ThrowIfCancellationRequested();var value=await read(owned.Source.Token).ConfigureAwait(false);owned.Source.Token.ThrowIfCancellationRequested();
            var plan=SettingsImportDocument.Merge(target,value);
            lock(sync)
            {
                if(!disposed&&!owned.Withdrawn&&!owned.Caller.IsCancellationRequested&&!owned.Source.IsCancellationRequested)
                {preview=plan;basis=target;caller=cancellation;result=new(SettingsImportStatus.Preview,plan);}
            }
        }
        catch(OperationCanceledException)when(owned.Caller.IsCancellationRequested||owned.Source.IsCancellationRequested){ }
        catch(Exception error){result=new(SettingsImportStatus.Failed,Failure:error);}
        finally
        {
            Task settlement;lock(sync){owned.Finishing=true;settlement=owned.Cancellation??Task.CompletedTask;}
            await settlement.ConfigureAwait(false);
            lock(sync)
            {
                if(owned.Failure is {} error){preview=null;basis=null;result=new(SettingsImportStatus.Failed,Failure:result.Failure==null?error:new AggregateException(result.Failure,error));}
                if(result.Status==SettingsImportStatus.Preview&&(disposed||owned.Withdrawn||owned.Caller.IsCancellationRequested||!ReferenceEquals(preview,result.Preview)))result=new(SettingsImportStatus.Cancelled);
                if(ReferenceEquals(pending,owned))pending=null;owned.Source.Dispose();
            }
        }
        return result;
    }
    public bool Apply(SettingsImportPlan value,bool confirmed,string currentSavedVersion)
    {
        lock(sync)
        {
            if(!confirmed||disposed||pending!=null||!ReferenceEquals(preview,value)||basis==null||caller.IsCancellationRequested||basis.SavedVersion!=currentSavedVersion)return false;
            bool applied=basis.Owner.TryApplyImport(basis,value);preview=null;basis=null;return applied;
        }
    }
    public void Dispose(){lock(sync){disposed=true;preview=null;basis=null;RequestCancellation(pending);}}
}
public static class SettingsImportFiles
{
    public static Task<SettingsImportData> ReadAsync(string path,CancellationToken cancellation=default)
        =>DisplayPreferencesFiles.ReadDocumentAsync(path,SettingsJson.MaximumBytes,SettingsImportDocument.ReadAsync,cancellation);
}
