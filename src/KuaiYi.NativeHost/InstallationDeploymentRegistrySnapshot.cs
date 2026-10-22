using KuaiYi.Core.Installation;
namespace KuaiYi.NativeHost;

// Observation only. These slots do not establish ownership or authorize a registration write.
internal sealed class InstallationDeploymentRegistrySnapshot(IHostRegistrationStore registry):IInstallationDeploymentSnapshotRead
{
    private readonly object sync=new();
    private Task<InstallationDeploymentRegistrationSnapshot>? capture;
    private Task? verification;
    private InstallationDeploymentRegistrationSnapshot? observed;
    public bool CleanupConfirmed
    {get{lock(sync)return registry.CleanupConfirmed&&(capture==null||capture.IsCompleted)&&(verification==null||verification.IsCompleted);}}
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
    public Task CloseOriginalsAsync()
    {
        lock(sync)
        {
            if(capture is {IsCompleted:false}||verification is {IsCompleted:false})throw new InvalidOperationException("Join the original registry task before closing.");
            registry.FinalizeCleanup();
            if(!registry.CleanupConfirmed)throw new IOException("Original registry resources remain unsettled.");
            return Task.CompletedTask;
        }
    }
}
