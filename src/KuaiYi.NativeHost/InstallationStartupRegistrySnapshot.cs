using KuaiYi.Core.Installation;
namespace KuaiYi.NativeHost;

// Same transaction owner for original Chrome/Edge tasks and the single named startup value.
// None of these observations grants source/installation authority; Core's original scope still does that.
internal sealed class InstallationStartupRegistrySnapshot(IInstallationDeploymentPublicationRegistry host,IStartupRegistrationStore startup):IInstallationStartupRegistry
{
    private readonly object sync=new();
    private Task? original,capture,preparation,verification;
    private bool conflict;
    public bool ConflictObserved{get{lock(sync)return conflict||host.ConflictObserved;}}
    public bool CleanupConfirmed{get{lock(sync)return (original==null||original.IsCompleted)&&host.CleanupConfirmed&&startup.CleanupConfirmed;}}
    public Task<InstallationDeploymentRegistrationSnapshot> CaptureAsync()=>Start(()=>host.CaptureAsync(),task=>capture=task);
    public Task<InstallationDeploymentRegistrationSnapshot> ReadForPreparationAsync()=>Start(()=>host.ReadForPreparationAsync(),task=>preparation=task);
    public Task VerifyAsync()=>Start(async()=>{await host.VerifyAsync().ConfigureAwait(false);return true;},task=>verification=task);
    public Task<InstallationDeploymentRegistrationSnapshot> ReadPublishedAsync()=>Start(()=>host.ReadPublishedAsync());
    public Task WriteDefaultAsync(bool chrome,InstallationDeploymentRegistrationSlot expected,string? value)=>
        Start(async()=>{await host.WriteDefaultAsync(chrome,expected,value).ConfigureAwait(false);return true;});
    public Task<InstallationStartupValue> ReadStartupAsync()
    {
        lock(sync)
        {
            if(capture is not{IsCompletedSuccessfully:true})throw new InvalidOperationException("Original host capture is required before a startup observation.");
            return Start(()=>Task.FromResult(startup.Read()));
        }
    }
    public Task WriteStartupAsync(InstallationStartupValue expected,InstallationStartupValue next)
    {
        lock(sync)
        {
            if(preparation is not{IsCompletedSuccessfully:true}||verification!=null)throw new InvalidOperationException("Original startup publication is not prepared.");
            return Start(()=>{startup.Write(expected,next);return Task.FromResult(true);});
        }
    }
    private Task<T> Start<T>(Func<Task<T>> action,Action<Task>? register=null)
    {
        lock(sync)
        {
            if(original is{IsCompleted:false}||conflict||host.ConflictObserved||!host.CleanupConfirmed||!startup.CleanupConfirmed)
                throw new InvalidOperationException("Original registration work is busy, foreign or unsettled.");
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var task=Task.Run(async()=>
            {
                await begin.Task.ConfigureAwait(false);
                try{return await action().ConfigureAwait(false);}
                catch(Exception error)
                {
                    if(error is HostRegistrationConflictException||error is AggregateException many&&many.Flatten().InnerExceptions.Any(e=>e is HostRegistrationConflictException))
                        lock(sync)conflict=true;
                    throw;
                }
            });
            original=task;register?.Invoke(task);begin.SetResult();return task;
        }
    }
    public Task CloseOriginalsAsync()
    {
        lock(sync)
        {
            if(original is{IsCompleted:false})throw new InvalidOperationException("Join the exact registration task before closing original handles.");
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var task=Task.Run(async()=>
            {
                await begin.Task.ConfigureAwait(false);List<Exception> errors=[];
                try{await host.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
                try{startup.FinalizeCleanup();}catch(Exception error){errors.Add(error);}
                if(errors.Count==0&&(!host.CleanupConfirmed||!startup.CleanupConfirmed))errors.Add(new IOException("Original registration handle cleanup remains unconfirmed."));
                HostRegistrationTransaction.Throw(errors);
            });original=task;begin.SetResult();return task;
        }
    }
}
