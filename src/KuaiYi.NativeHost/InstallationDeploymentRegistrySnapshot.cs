using KuaiYi.Core.Installation;
namespace KuaiYi.NativeHost;

// Observation only. These slots do not establish ownership or authorize a registration write.
internal sealed class InstallationDeploymentRegistrySnapshot(IHostRegistrationStore registry):IInstallationDeploymentPublicationRegistry
{
    private readonly object sync=new();
    private Task<InstallationDeploymentRegistrationSnapshot>? capture;
    private Task? verification;
    private Task<InstallationDeploymentRegistrationSnapshot>? preparation;
    private Task? publication;
    private bool conflictObserved;
    public bool ConflictObserved {get{lock(sync)return conflictObserved;}}
    private InstallationDeploymentRegistrationSnapshot? observed;
    public bool CleanupConfirmed
    {get{lock(sync)return registry.CleanupConfirmed&&(capture==null||capture.IsCompleted)&&(verification==null||verification.IsCompleted)&&(preparation==null||preparation.IsCompleted)&&(publication==null||publication.IsCompleted);}}
    public Task<InstallationDeploymentRegistrationSnapshot> ReadForPreparationAsync()
    {
        lock(sync)
        {
            if(capture is not{IsCompletedSuccessfully:true}||observed==null||preparation!=null||verification!=null)throw new InvalidOperationException("Original registry preparation read is not available.");
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            preparation=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);var now=ReadSlots();if(now!=observed)throw new HostRegistrationConflictException();return now;});begin.SetResult();return preparation;
        }
    }
    public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync()
    {
        lock(sync)
        {
            if(capture!=null)throw new InvalidOperationException("Registry snapshot is one-shot.");
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            capture=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);observed=ReadSlots();return observed;});
            begin.SetResult();return capture;
        }
    }
    public Task VerifyAsync()
    {
        lock(sync)
        {
            if(capture is not {IsCompletedSuccessfully:true}||observed==null||verification!=null)throw new InvalidOperationException("Registry snapshot is not ready or was already verified.");
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            verification=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);if(ReadSlots()!=observed)throw new HostRegistrationConflictException();});
            begin.SetResult();return verification;
        }
    }
    private InstallationDeploymentRegistrationSnapshot ReadSlots()
    {
        if(!registry.CleanupConfirmed)throw new IOException("Original registry resources are unsettled.");
        var chrome=registry.Read(IntegrationBrowser.Chrome);
        if(!registry.CleanupConfirmed)throw new IOException("Original Chrome read is unsettled; Edge read refused.");
        var edge=registry.Read(IntegrationBrowser.Edge);
        if(!registry.CleanupConfirmed)throw new IOException("Original Edge read is unsettled.");
        return new(new(chrome.KeyExists,chrome.Value,chrome.ForeignContent),new(edge.KeyExists,edge.Value,edge.ForeignContent));
    }
    public Task<InstallationDeploymentRegistrationSnapshot> ReadPublishedAsync()=>StartPublication(ReadSlots);
    public Task WriteDefaultAsync(bool chrome,InstallationDeploymentRegistrationSlot expected,string? value)=>StartPublication(()=>
    {registry.Write(chrome?IntegrationBrowser.Chrome:IntegrationBrowser.Edge,new(expected.KeyExists,expected.Value,expected.ForeignContent),value);if(!registry.CleanupConfirmed)throw new IOException("Original publication write is unsettled.");return true;});
    private Task<T> StartPublication<T>(Func<T> action)
    {
        lock(sync)
        {
            if(conflictObserved||preparation is not{IsCompletedSuccessfully:true}||verification!=null||publication is{IsCompleted:false}||!registry.CleanupConfirmed)throw new InvalidOperationException("Original registry publication is not ready or remains unsettled.");
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var task=Task.Run(async()=>
            {
                await begin.Task.ConfigureAwait(false);
                try{return action();}
                catch(Exception error)
                {
                    // The original compare observation outlives both external restoration and successful close.
                    // Windows raw-key access preserves body+close errors as original Aggregate leaves.
                    if(error is HostRegistrationConflictException||error is AggregateException many&&many.Flatten().InnerExceptions.Any(e=>e is HostRegistrationConflictException))
                        lock(sync)conflictObserved=true;
                    throw;
                }
            });publication=task;begin.SetResult();return task;
        }
    }
    public Task CloseOriginalsAsync()
    {
        lock(sync)
        {
            if(capture is {IsCompleted:false}||verification is {IsCompleted:false}||preparation is{IsCompleted:false}||publication is{IsCompleted:false})throw new InvalidOperationException("Join the original registry task before closing.");
            registry.FinalizeCleanup();
            if(!registry.CleanupConfirmed)throw new IOException("Original registry resources remain unsettled.");
            return Task.CompletedTask;
        }
    }
}
