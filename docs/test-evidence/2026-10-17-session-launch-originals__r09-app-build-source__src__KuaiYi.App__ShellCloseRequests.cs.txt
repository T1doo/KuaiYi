namespace KuaiYi.App;

// One Shell's original close attempt and at most one explicitly requested retry.
// No idle worker, process discovery, automatic failure retry or exit receipt is created here.
internal sealed class ShellCloseRequests(Func<Task<bool>> attempt)
{
    private readonly object sync=new();
    private Task? current;
    private bool pending,closed;
    internal Task? Current{get{lock(sync)return current;}}
    internal bool Pending{get{lock(sync)return pending;}}
    internal void Request(Action? admitted=null)
    {
        lock(sync)
        {
            if(closed)return;pending=true;var begin=StartIfReady();
            try{admitted?.Invoke();}
            catch(Exception error){begin?.TrySetException(error);throw;}
            finally{begin?.TrySetResult();}
        }
    }
    private TaskCompletionSource? StartIfReady()
    {
        if(closed||!pending||current is {IsCompleted:false})return null;
        pending=false;
        var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Task original=Run(begin.Task);current=original; // Publish before any UI/cleanup callback can reenter.
        _=original.ContinueWith(t=>
        {
            _=t.Exception;
            lock(sync)StartIfReady()?.TrySetResult(); // Only the completed original task can hand off a retained retry.
        },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default);
        return begin;
    }
    private async Task Run(Task begin)
    {
        await begin.ConfigureAwait(false);
        bool succeeded=await attempt().ConfigureAwait(false);
        if(succeeded)lock(sync){closed=true;pending=false;}
    }
}
