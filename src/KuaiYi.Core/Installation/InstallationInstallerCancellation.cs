namespace KuaiYi.Core.Installation;

// Console cancellation never terminates the installer. The caller seals this producer and joins its one original task.
internal sealed class InstallationInstallerCancellation
{
    private readonly object sync=new();
    private readonly CancellationTokenSource stop=new();
    private Task? original;
    private bool sealedProducer;
    internal CancellationToken Token=>stop.Token;
    internal Task Request()
    {
        lock(sync)
        {
            if(original!=null)return original;
            if(sealedProducer)return Task.CompletedTask;
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            original=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);await stop.CancelAsync().ConfigureAwait(false);});begin.SetResult();return original;
        }
    }
    internal async Task<IReadOnlyList<Exception>> SealAndJoinAsync()
    {
        Task? task;lock(sync){sealedProducer=true;task=original;}
        if(task!=null)try{await task.ConfigureAwait(false);}catch(Exception error){return [error];}
        return [];
    }
}
