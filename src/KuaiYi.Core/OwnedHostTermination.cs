using System.Diagnostics;

namespace KuaiYi.Core;

public enum HostExitState { Running, TerminationRequested, ExitConfirmed, TerminationFailed, ExitUnconfirmed, OwnershipTransferred }
public interface IOwnedHostLifetime
{
    bool HasExited { get; }
    void RequestTermination();
    Task WaitForExitAsync(CancellationToken cancellation);
}
/// <summary>Wrap only the Process returned by this caller's Start. Never opens or kills a PID supplied by another process.</summary>
public sealed class StartedHostLifetime(Process owned) : IOwnedHostLifetime
{
    public bool HasExited => owned.HasExited;
    public void RequestTermination() => owned.Kill(); // Only this held child, not unverified descendants.
    public Task WaitForExitAsync(CancellationToken cancellation) => owned.WaitForExitAsync(cancellation);
}
public sealed class OwnedHostTermination(IOwnedHostLifetime owned)
{
    private readonly object sync = new();
    private Task<HostExitState>? pending;
    private Task? exitWait;
    private bool waitStarted;
    public HostExitState State { get; private set; } = HostExitState.Running;
    public string? FailureCode { get; private set; }
    public bool CanRestart => State == HostExitState.ExitConfirmed;
    public Task<HostExitState> StopAsync(TimeSpan budget)
    {
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(budget));
        lock(sync)
        {
            if (pending is { IsCompleted: false }) return pending;
            return pending = StopCore(budget);
        }
    }
    public bool ObserveExit()
    {lock(sync)return ObserveExitCore();}
    private bool ObserveExitCore()
    {
        // Actual exit cannot settle an original observer which can still use the held Process.
        if(waitStarted && exitWait is not {IsCompleted:true})return false;
        if(exitWait is {IsFaulted:true})FailureCode=exitWait.Exception!.GetBaseException().GetType().Name;
        else if(exitWait is {IsCanceled:true})FailureCode=nameof(TaskCanceledException);
        if (CanRestart) return true;
        try { if (!owned.HasExited) return false; State = HostExitState.ExitConfirmed; return true; }
        catch (Exception error) { FailureCode = error.GetType().Name; State = HostExitState.TerminationFailed; return false; }
    }
    private async Task<HostExitState> StopCore(TimeSpan budget)
    {
        Task original;
        lock(sync)
        {
            if (!CanRestart) { FailureCode = null; State = HostExitState.Running; }
            if (ObserveExitCore()) return State;
            if (State == HostExitState.TerminationFailed) return State;
            if(!waitStarted)
            {
                // Publish the pending gate before invoking either externally supplied lifetime method.
                waitStarted=true;
                try { owned.RequestTermination(); State = HostExitState.TerminationRequested; }
                catch (Exception error) { waitStarted=false;FailureCode=error.GetType().Name;if(!ObserveExitCore())State=HostExitState.TerminationFailed;return State; }
                try { exitWait=owned.WaitForExitAsync(CancellationToken.None); }
                catch(Exception error){exitWait=Task.FromException(error);}
            }
            if(exitWait==null)return State=HostExitState.ExitUnconfirmed; // Reentrant call before original task publication.
            original=exitWait;
        }
        try
        {
            // Only this caller's wait is bounded. The one original observation remains owned until it settles.
            await original.WaitAsync(budget).ConfigureAwait(false);
            lock(sync)if (!ObserveExitCore()) State = HostExitState.ExitUnconfirmed;
        }
        catch (TimeoutException) { lock(sync)if (!ObserveExitCore()) State = HostExitState.ExitUnconfirmed; }
        catch (Exception error) { lock(sync){FailureCode=error.GetType().Name;if(!ObserveExitCore())State=HostExitState.TerminationFailed;} }
        lock(sync)return State;
    }
}
