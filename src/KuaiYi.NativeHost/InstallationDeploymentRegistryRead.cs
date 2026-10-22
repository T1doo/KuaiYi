using KuaiYi.Core.Installation;
namespace KuaiYi.NativeHost;

// Read-only finalization adapter, not a registration entry or a participant commit.
// Construct/register this owner before Closing; the original registry store retains every failed raw close.
internal sealed class InstallationDeploymentRegistryRead(IHostRegistrationStore registry,HostRegistrationSlot chrome,HostRegistrationSlot edge):IInstallationDeploymentFinalRead
{
    private Task? read;
    public bool CleanupConfirmed=>registry.CleanupConfirmed&&(read==null||read.IsCompleted);
    public Task VerifyAsync()
    {
        if(read!=null)throw new InvalidOperationException("Registry verification is one-shot.");
        var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        read=Task.Run(async()=>
        {
            await begin.Task.ConfigureAwait(false);
            if(!registry.CleanupConfirmed)throw new IOException("Original registry resources are unsettled.");
            if(registry.Read(IntegrationBrowser.Chrome)!=chrome)throw new HostRegistrationConflictException();
            if(!registry.CleanupConfirmed)throw new IOException("Original registry read is unsettled.");
            if(registry.Read(IntegrationBrowser.Edge)!=edge)throw new HostRegistrationConflictException();
        });
        begin.SetResult();return read;
    }
    public Task CloseOriginalsAsync()
    {
        if(read is {IsCompleted:false})throw new InvalidOperationException("Join the original registry read before closing.");
        registry.FinalizeCleanup();
        if(!registry.CleanupConfirmed)throw new IOException("Original registry resources remain unsettled.");
        return Task.CompletedTask;
    }
}
