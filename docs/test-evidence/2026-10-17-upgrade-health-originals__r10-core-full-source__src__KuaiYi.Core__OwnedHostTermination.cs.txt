using System.Diagnostics;

namespace KuaiYi.Core;

public enum HostExitState { Running, TerminationRequested, ExitConfirmed, TerminationFailed, ExitUnconfirmed }
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
    private Task<HostExitState>? pending;
    public HostExitState State { get; private set; } = HostExitState.Running;
    public string? FailureCode { get; private set; }
    public bool CanRestart => State == HostExitState.ExitConfirmed;
    public Task<HostExitState> StopAsync(TimeSpan budget)
    {
        if (budget <= TimeSpan.Zero || budget > TimeSpan.FromSeconds(10)) throw new ArgumentOutOfRangeException(nameof(budget));
        if (pending is { IsCompleted: false }) return pending;
        return pending = StopCore(budget);
    }
    public bool ObserveExit()
    {
        if (CanRestart) return true;
        try { if (!owned.HasExited) return false; State = HostExitState.ExitConfirmed; return true; }
        catch (Exception error) { FailureCode = error.GetType().Name; State = HostExitState.TerminationFailed; return false; }
    }
    private async Task<HostExitState> StopCore(TimeSpan budget)
    {
        if (!CanRestart) { FailureCode = null; State = HostExitState.Running; }
        if (ObserveExit()) return State;
        if (State == HostExitState.TerminationFailed) return State;
        try { owned.RequestTermination(); State = HostExitState.TerminationRequested; }
        catch (Exception error) { FailureCode = error.GetType().Name; if (!ObserveExit()) State = HostExitState.TerminationFailed; return State; }
        using var cancellation = new CancellationTokenSource();
        try
        {
            await owned.WaitForExitAsync(cancellation.Token).WaitAsync(budget).ConfigureAwait(false);
            if (!ObserveExit()) State = HostExitState.ExitUnconfirmed;
        }
        catch (TimeoutException) { if (!ObserveExit()) State = HostExitState.ExitUnconfirmed; }
        catch (Exception error) { FailureCode = error.GetType().Name; if (!ObserveExit()) State = HostExitState.TerminationFailed; }
        finally { cancellation.Cancel(); }
        return State;
    }
}
