namespace KuaiYi.Core.Settings;

public enum DisplayPreferencesPreviewKind{Import,Export}
public sealed class DisplayPreferencesPreview
{
    public DisplayPreferences Value{get;}public DisplayPreferencesPreviewKind Kind{get;}
    internal object Ticket{get;}internal DisplayPreferencesDraftSnapshot? Basis{get;}internal CancellationToken Caller{get;}
    internal DisplayPreferencesPreview(object ticket,DisplayPreferences value,DisplayPreferencesPreviewKind kind,DisplayPreferencesDraftSnapshot? basis,CancellationToken caller)
    {Ticket=ticket;Value=value;Kind=kind;Basis=basis;Caller=caller;}
}
public enum DisplayPreferencesExportStatus{Published,Cancelled,Failed,Busy,PublishedCleanupUnconfirmed}
public sealed record DisplayPreferencesExportResult(DisplayPreferencesExportStatus Status,DisplayPreferencesIssue? Issue=null);
/// <summary>One frozen preview, explicit apply/publish and latest operation ownership. No configuration or secret store access.</summary>
public sealed class DisplayPreferencesTransfer:IDisposable
{
    private readonly object sync=new();private object? ticket;private DisplayPreferencesPreview? preview;private IoScope? io;private bool disposed,exporting;
    private sealed class IoScope:IDisposable
    {
        private readonly object sync=new();private CancellationTokenSource? source;
        public CancellationToken Token{get;}
        public IoScope(CancellationToken first,CancellationToken second=default){source=CancellationTokenSource.CreateLinkedTokenSource(first,second);Token=source.Token;}
        public void Cancel(){lock(sync)source?.Cancel();}
        public void Dispose(){lock(sync){source?.Dispose();source=null;}}
    }
    public DisplayPreferencesPreview? Preview{get{lock(sync)return preview!=null&&Current(preview)?preview:null;}}
    public bool IsCurrent(DisplayPreferencesPreview value){lock(sync)return Current(value);}
    private bool Current(DisplayPreferencesPreview value)=>!disposed&&ReferenceEquals(preview,value)&&ReferenceEquals(ticket,value.Ticket)&&!value.Caller.IsCancellationRequested;
    public void Cancel()
    {IoScope? previous;lock(sync){ticket=null;preview=null;previous=io;io=null;exporting=false;}previous?.Cancel();}
    public async Task<DisplayPreferencesPreview?> ImportAsync(DisplayPreferencesDraftSnapshot basis,Func<CancellationToken,Task<DisplayPreferences>> read,CancellationToken cancellation=default)
    {
        IoScope scope;object command=new();IoScope? previous;
        lock(sync){ObjectDisposedException.ThrowIf(disposed,this);scope=new(cancellation);previous=io;io=scope;ticket=command;preview=null;exporting=false;}
        try
        {
            previous?.Cancel();
            scope.Token.ThrowIfCancellationRequested();var value=await read(scope.Token).ConfigureAwait(false);scope.Token.ThrowIfCancellationRequested();DisplayPreferencesDocument.Validate(value);
            lock(sync){if(disposed||!ReferenceEquals(ticket,command)||scope.Token.IsCancellationRequested)return null;return preview=new(command,value,DisplayPreferencesPreviewKind.Import,basis,cancellation);}
        }
        catch(OperationCanceledException)when(scope.Token.IsCancellationRequested){return null;}
        finally{lock(sync)if(ReferenceEquals(io,scope))io=null;scope.Dispose();}
    }
    public DisplayPreferencesPreview PrepareExport(DisplayPreferences value,CancellationToken cancellation=default)
    {
        DisplayPreferencesDocument.Validate(value);cancellation.ThrowIfCancellationRequested();IoScope? previous;DisplayPreferencesPreview result;
        lock(sync){ObjectDisposedException.ThrowIf(disposed,this);previous=io;io=null;ticket=new object();exporting=false;preview=result=new(ticket,value,DisplayPreferencesPreviewKind.Export,null,cancellation);}previous?.Cancel();return result;
    }
    public bool ApplyImport(DisplayPreferencesPreview value,SettingsDraft draft)
    {
        lock(sync)
        {
            if(!Current(value)||value.Kind!=DisplayPreferencesPreviewKind.Import||value.Basis==null)return false;
            bool applied;
            try{applied=draft.TryApplyDisplayPreferences(value.Basis,value.Value);}
            catch(InvalidOperationException){applied=false;}catch(UnknownSettingsSchemaException){applied=false;}
            ticket=null;preview=null;return applied;
        }
    }
    public async Task<DisplayPreferencesExportResult> ExportAsync(DisplayPreferencesPreview value,Func<ReadOnlyMemory<byte>,CancellationToken,Task<IPreparedDisplayPreferencesExport>> prepare,CancellationToken cancellation=default)
    {
        IoScope scope;
        lock(sync)
        {
            if(!Current(value)||value.Kind!=DisplayPreferencesPreviewKind.Export)return new(DisplayPreferencesExportStatus.Cancelled);
            if(exporting)return new(DisplayPreferencesExportStatus.Busy);
            scope=new(value.Caller,cancellation);io=scope;exporting=true;
        }
        IPreparedDisplayPreferencesExport? prepared=null;bool published=false;var result=new DisplayPreferencesExportResult(DisplayPreferencesExportStatus.Cancelled);
        try
        {
            scope.Token.ThrowIfCancellationRequested();prepared=await prepare(DisplayPreferencesDocument.Write(value.Value),scope.Token).ConfigureAwait(false);
            lock(sync)
            {
                if(Current(value)&&!scope.Token.IsCancellationRequested)
                {
                    // Publication is the commit point. Later cancellation must not report an undone export.
                    prepared.Publish();published=true;if(ReferenceEquals(ticket,value.Ticket)){ticket=null;preview=null;}result=new(DisplayPreferencesExportStatus.Published);
                }
            }
        }
        catch(OperationCanceledException)when(scope.Token.IsCancellationRequested){result=new(DisplayPreferencesExportStatus.Cancelled);}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or DisplayPreferencesException or ArgumentException or NotSupportedException)
        {result=new(DisplayPreferencesExportStatus.Failed,error is DisplayPreferencesException known?known.Issue:DisplayPreferencesIssue.File);}
        finally
        {
            try{prepared?.Dispose();}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or DisplayPreferencesException or ArgumentException or NotSupportedException){result=new(published?DisplayPreferencesExportStatus.PublishedCleanupUnconfirmed:DisplayPreferencesExportStatus.Failed,DisplayPreferencesIssue.Cleanup);}
            lock(sync){if(ReferenceEquals(io,scope)){io=null;exporting=false;}}scope.Dispose();
        }
        return result;
    }
    public void Dispose(){lock(sync){if(disposed)return;disposed=true;}Cancel();}
}
