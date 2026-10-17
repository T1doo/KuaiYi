namespace KuaiYi.Core.Diagnostics;

public enum DiagnosticExportStatus { Published, Cancelled, Busy, Failed, PublishedCleanupUnconfirmed }
public interface IPreparedDiagnosticExport:IDisposable {void Publish();}
/// <summary>Only the exact displayed report can be published. Invalidation and publication share one gate.</summary>
public sealed class DiagnosticReportSession:IDisposable
{
    private sealed class Operation(CancellationToken caller)
    {public readonly CancellationTokenSource Source=new();public readonly CancellationToken Caller=caller;public Task? Cancellation;public bool Finishing;}
    private readonly object sync=new();private DiagnosticReport? preview;private bool disposed;private Operation? pending;
    private DiagnosticError? cancellationFailure;
    public DiagnosticError? CancellationFailure{get{lock(sync)return cancellationFailure;}}
    public DiagnosticReport? Preview{get{lock(sync)return preview;}}
    public bool Busy{get{lock(sync)return pending!=null;}}
    public DiagnosticReport Prepare(DiagnosticLog log,DiagnosticEnvironment environment)
    {lock(sync){ObjectDisposedException.ThrowIf(disposed,this);if(pending!=null)throw new InvalidOperationException("Diagnostic export pending.");return preview=log.CreateReport(environment);}}
    public void Invalidate(){lock(sync){preview=null;RequestCancellation(pending);}}
    private void RequestCancellation(Operation? operation)
    {
        if(operation is not {Finishing:false,Cancellation:null})return;
        // Never run caller callbacks on the UI thread or under the publication gate. Keep ownership until they settle.
        operation.Cancellation=Task.Run(async()=>
        {try{await operation.Source.CancelAsync().ConfigureAwait(false);}catch(Exception error){lock(sync)cancellationFailure=DiagnosticLog.Classify(error);}});
    }
    public async Task<DiagnosticExportStatus> ExportAsync(DiagnosticReport report,Func<DiagnosticReport,CancellationToken,Task<IPreparedDiagnosticExport>> prepare,CancellationToken cancellation=default)
    {
        Operation owned;
        lock(sync)
        {
            if(disposed||preview!=report||cancellation.IsCancellationRequested)return DiagnosticExportStatus.Cancelled;
            if(pending!=null)return DiagnosticExportStatus.Busy;
            owned=pending=new(cancellation);
        }
        var bridge=cancellation.Register(()=>{lock(sync)RequestCancellation(owned);});
        IPreparedDiagnosticExport? file=null;var status=DiagnosticExportStatus.Cancelled;
        try
        {
            file=await prepare(report,owned.Source.Token).ConfigureAwait(false);
            lock(sync)
            {
                if(!disposed&&preview==report&&!owned.Caller.IsCancellationRequested&&!owned.Source.IsCancellationRequested)
                {file.Publish();status=DiagnosticExportStatus.Published;}
            }
        }
        catch(OperationCanceledException)when(owned.Caller.IsCancellationRequested||owned.Source.IsCancellationRequested){status=DiagnosticExportStatus.Cancelled;}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){status=DiagnosticExportStatus.Failed;}
        finally
        {
            Task settlement;lock(sync){owned.Finishing=true;settlement=owned.Cancellation??Task.CompletedTask;}
            await settlement.ConfigureAwait(false);
            try{file?.Dispose();}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){status=status==DiagnosticExportStatus.Published?DiagnosticExportStatus.PublishedCleanupUnconfirmed:DiagnosticExportStatus.Failed;}
            await bridge.DisposeAsync().ConfigureAwait(false);
            lock(sync){if(pending==owned)pending=null;owned.Source.Dispose();}
        }
        return status;
    }
    public void Dispose(){lock(sync){disposed=true;preview=null;RequestCancellation(pending);}}
}
