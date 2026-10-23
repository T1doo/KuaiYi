namespace KuaiYi.GrowthHarness;

// Test-tool ownership, not a replacement for the production Shell close controller.
internal sealed class OverlayProbeLifetime
{
    private readonly TaskCompletionSource start=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal List<ProbeFailure> Failures{get;}=[];
    internal Task Original{get;}
    internal bool CloseConfirmed{get;private set;}
    internal bool CleanupConfirmed{get;private set;}
    internal OverlayProbeLifetime(Func<Task> body,Func<Task<bool>> close,Action cleanup)
        =>Original=Run(body,close,cleanup);
    internal void Release()=>start.SetResult();
    internal void Record(string stage,Exception error)=>Failures.Add(new(stage,error.ToString()));
    private async Task Run(Func<Task> body,Func<Task<bool>> close,Action cleanup)
    {
        await start.Task;
        try{await body();}catch(Exception error){Record("Body",error);}
        try{CloseConfirmed=await close();if(!CloseConfirmed)Record("Close",new InvalidOperationException("Original close unconfirmed; state retained."));}
        catch(Exception error){Record("Close",error);}
        if(!CloseConfirmed)return;
        try{cleanup();CleanupConfirmed=true;}catch(Exception error){Record("Cleanup",error);}
    }
    // Observe both original tasks independently. A failed first task cannot erase the second failure.
    internal async Task<bool> JoinClose(Task original,Task closed)
    {
        bool ok=true;
        try{await original;}catch(Exception error){ok=false;Record("OriginalCloseTask",error);}
        try{await closed;}catch(Exception error){ok=false;Record("ClosedEvent",error);}
        return ok;
    }
}
internal sealed record ProbeFailure(string Stage,string Error);
