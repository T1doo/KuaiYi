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
    private bool stopRequested, disposed;
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
                if (!disposed && HasPendingLifetimeWork) return stopRequested ? HostExitState.ExitUnconfirmed : HostExitState.Running;
                return termination?.State ?? (startTask == null ? null : HostExitState.ExitConfirmed);
            }
        }
    }
    public bool ObserveExit()
    {
        lock (ownershipSync) return disposed || IsSettledAndExited();
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
            if(startTask!=null)throw new InvalidOperationException("Single owned child per session.");
            var pending=startTask=Task.Run(()=>StartOwned(info,token));startupObserver=ObserveStartupAsync(pending);return pending;
        }
    }
    public Task<HostExitState> StopAsync()=>StopOwnedAsync();
    public Task StartupSettlement {get{lock(ownershipSync)return startTask??Task.CompletedTask;}}
    public async ValueTask<bool> TryFinalizeAsync(bool retry=false)
    {
        if(retry){lock(ownershipSync)if(stopTask is {IsCompleted:true})stopTask=null;}
        try{await StopOwnedAsync().ConfigureAwait(false);}
        catch(Exception error){lock(ownershipSync)FinalizationFailureCode=error.GetType().Name;return false;}
        lock(ownershipSync)
        {
            if(disposed)return true;if(!IsSettledAndExited())return false;
            try{owned?.Dispose();owned=null;disposed=true;_ =startupObserver;return true;}
            catch(Exception error){FinalizationFailureCode=error.GetType().Name;return false;}
        }
    }
    public string? FinalizationFailureCode {get;private set;}
    public static ProcessStartInfo Info(string executable,string? assembly)
    {
        var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        if(assembly!=null)info.ArgumentList.Add(assembly);info.ArgumentList.Add("--private-stdio");
        var retained=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string key in new[]{"SystemRoot","WINDIR","TEMP","TMP","PATH","DOTNET_ROOT","DOTNET_ROOT_X64"})if(info.Environment.TryGetValue(key,out var value)&&value!=null)retained[key]=value;
        info.Environment.Clear();foreach(var pair in retained)info.Environment[pair.Key]=pair.Value;return info;
    }
}
