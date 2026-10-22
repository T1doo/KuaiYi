using System.Diagnostics;
namespace KuaiYi.Core;
/// <summary>Shared owned-child lifetime: startup settlement and actual exit must both complete before release/restart.</summary>
public sealed class OwnedProcessSession(IOwnedProcessStarter? processStarter=null)
{
    internal Func<Process,IOwnedHostLifetime>? LifetimeForTest {get;set;}
    private readonly object ownershipSync = new();
    private Process? owned;
    private OwnedHostTermination? termination;
    private Task<Process>? startTask;
    private Task? startupObserver;
    private Task<HostExitState>? stopTask;
    private Task<IReadOnlyList<Exception>>? originalExitFence;
    private bool stopRequested, disposed,transferred;
    public string? StartupFailureCode { get; private set; }
    internal Action<Process>? OwnershipPublishedForTest { get; set; } // Test-only scheduler barrier; never configured by App.
    internal Task StartupSettlementForTest { get { lock (ownershipSync) return startTask ?? Task.CompletedTask; } }
    // Called only under ownershipSync. Publication of a Process is not completion of its creating task.
    private bool HasPendingLifetimeWork => startTask is { IsCompleted: false } || stopTask is { IsCompleted: false };
    private bool IsSettledAndExited() => !HasPendingLifetimeWork && (termination == null || termination.ObserveExit());
    public HostExitState? ExitState
    {
        get
        {
            lock (ownershipSync)
            {
                if(transferred)return HostExitState.OwnershipTransferred;
                if (!disposed && HasPendingLifetimeWork) return stopRequested ? HostExitState.ExitUnconfirmed : HostExitState.Running;
                return termination?.State ?? (startTask == null ? null : HostExitState.ExitConfirmed);
            }
        }
    }
    public bool ObserveExit()
    {
        lock (ownershipSync) return !transferred&&(disposed || IsSettledAndExited());
    }
    private Process StartOwned(ProcessStartInfo info, CancellationToken cancellation)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            Process child = (processStarter ?? SystemOwnedProcessStarter.Instance).Start(info);
            lock (ownershipSync) { owned = child; termination = new(LifetimeForTest?.Invoke(child)??new StartedHostLifetime(child)); }
            OwnershipPublishedForTest?.Invoke(child);
            return child;
        }
        catch (Exception error) { lock (ownershipSync) StartupFailureCode = error.GetType().Name; throw; }
    }
    private async Task ObserveStartupAsync(Task<Process> pending)
    {
        try { await pending.ConfigureAwait(false); }
        catch (Exception error) { lock (ownershipSync) StartupFailureCode = error.GetType().Name; return; }
        bool stop; lock (ownershipSync) stop = stopRequested;
        if (stop) await StopOwnedAsync().ConfigureAwait(false); // A cancelled waiter never abandons a later-created child.
    }
    private Task<HostExitState> StopOwnedAsync()
    {
        lock (ownershipSync)
        {
            if(transferred)return Task.FromResult(HostExitState.OwnershipTransferred);
            stopRequested = true;
            if (disposed) return Task.FromResult(HostExitState.ExitConfirmed);
            if (termination == null) return Task.FromResult(startTask == null || startTask.IsCompleted ? HostExitState.ExitConfirmed : HostExitState.ExitUnconfirmed);
            return stopTask ??= termination.StopAsync(TimeSpan.FromSeconds(2));
        }
    }

    public Task<Process> Start(ProcessStartInfo info,CancellationToken token)
    {
        lock(ownershipSync)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(transferred)throw new InvalidOperationException("Process ownership was transferred.");
            if(startTask!=null)throw new InvalidOperationException("Single owned child per session.");
            var pending=startTask=Task.Run(()=>StartOwned(info,token));startupObserver=ObserveStartupAsync(pending);return pending;
        }
    }
    public Task<HostExitState> StopAsync()=>StopOwnedAsync();
    public Task StartupSettlement {get{lock(ownershipSync)return startTask??Task.CompletedTask;}}
    internal Task StartupObservation {get{lock(ownershipSync)return startupObserver??Task.CompletedTask;}}
    // Read-only process-exit fence for a completed installer session producer. Never retries Stop/Finalize or releases ownership.
    internal Task<IReadOnlyList<Exception>> JoinOriginalExitAsync()
    {
        lock(ownershipSync)return originalExitFence??=Task.Run(async()=>
        {
            var errors=new List<Exception>();
            async Task Join(Task? original){if(original!=null)try{await original.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}}
            Task? start,observer;lock(ownershipSync){start=startTask;observer=startupObserver;}
            await Join(start).ConfigureAwait(false);await Join(observer).ConfigureAwait(false);
            Task? stop,exit;Process? child;bool closed;
            lock(ownershipSync){stop=stopTask;exit=termination?.OriginalExitObservation;child=owned;closed=disposed;}
            await Join(stop).ConfigureAwait(false);await Join(exit).ConfigureAwait(false);
            if(!closed&&child!=null)
            {
                try
                {
                    // This exact prepublished fence owns even a new passive OS observation after a failed Stop.
                    if(!child.HasExited)await child.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
                    if(!child.HasExited)throw new IOException("Original owned child exit remains unconfirmed.");
                }
                catch(Exception error)
                {
                    errors.Add(error);
                    // Cannot prove actual exit. Keep this original caller alive, with no timer, retry or new termination.
                    await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task.ConfigureAwait(false);
                }
            }
            return (IReadOnlyList<Exception>)errors;
        });
    }
    public async ValueTask<bool> TryFinalizeAsync(bool retry=false)
    {
        if(retry){lock(ownershipSync)if(stopTask is {IsCompleted:true})stopTask=null;}
        try{await StopOwnedAsync().ConfigureAwait(false);}
        catch(Exception error){lock(ownershipSync)FinalizationFailureCode=error.GetType().Name;return false;}
        lock(ownershipSync)
        {
            if(transferred)return false;
            if(disposed)return true;if(!IsSettledAndExited())return false;
            try{owned?.Dispose();owned=null;disposed=true;_ =startupObserver;return true;}
            catch(Exception error){FinalizationFailureCode=error.GetType().Name;return false;}
        }
    }
    public string? FinalizationFailureCode {get;private set;}
    internal void ReleaseToReceiver(Process expected,Installation.InstallationHandoffTransaction proof)
    {
        lock(ownershipSync)
        {
            if(transferred)return;
            if(!proof.Committed||!ReferenceEquals(owned,expected)||HasPendingLifetimeWork||stopRequested||disposed)throw new InvalidOperationException("Exact committed child ownership required.");
            proof.Offer.RequireRecords();proof.Offer.Exact("handoff-pending.json",proof.Offer.Ledger("Committed"));
            // Caller has joined the original control readers. Releasing this local handle does not assert OS exit.
            owned!.Dispose();owned=null;termination=null;transferred=true;
        }
    }
    public static ProcessStartInfo Info(string executable,string? assembly)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        if(assembly!=null)info.ArgumentList.Add(assembly);info.ArgumentList.Add("--private-stdio");
        var retained=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string key in new[]{"SystemRoot","WINDIR","TEMP","TMP","PATH","DOTNET_ROOT","DOTNET_ROOT_X64"})if(info.Environment.TryGetValue(key,out var value)&&value!=null)retained[key]=value;
        info.Environment.Clear();foreach(var pair in retained)info.Environment[pair.Key]=pair.Value;return info;
    }
}
