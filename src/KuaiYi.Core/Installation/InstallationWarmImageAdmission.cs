namespace KuaiYi.Core.Installation;

// One in-memory connection attempt. This is NOT a verified peer or a transferable permission.
internal sealed class InstallationWarmConnection
{
    internal readonly Guid Id=Guid.NewGuid();
    internal readonly string Nonce;
    internal InstallationWarmConnection(string? nonce=null)
    {if(nonce!=null&&!InstallationWarmIdentity.Nonce(nonce))throw new InvalidDataException("Invalid connection nonce.");Nonce=nonce??InstallationWarmIdentity.NewNonce();}
    private int consumed;
    internal void Consume(){if(Interlocked.Exchange(ref consumed,1)!=0)throw new InvalidOperationException("Connection attempt already consumed.");}
}
internal enum InstallationWarmPhase { Reserved,Preparing,AppOwned,Revoked }
internal sealed class InstallationWarmLease
{
    internal InstallationWarmGrant Grant;
    internal bool RemoteGrantBound,PreviewConfirmed;
    internal readonly CancellationTokenSource Stop=new();
    internal readonly List<Task> Originals=[];
    internal readonly List<ITimer> Timers=[];
    internal readonly List<CancellationTokenRegistration> Registrations=[];
    internal readonly TaskCompletionSource CancelJoined=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task? OriginalCancellation,Retirement,FinalWrite;
    internal bool CancelStarted,Sealed;
    internal Action? BeforeCancellationForTest;
    internal long Started;
    internal InstallationWarmPhase Phase;
    internal InstallationWarmAccepted? Accepted;
    internal InstallationWarmLease(InstallationWarmGrant grant,long started){Grant=grant;Started=started;}
    internal CancellationToken Token=>Stop.Token;
}

// One private inherited-channel slot. Real peer authentication is the App adapter prerequisite.
internal sealed class InstallationWarmImageAdmission(InstallationControlledLaunch proof,TimeProvider clock)
{
    private readonly object sync=new(); // The pure UI source commit takes this before Gate; callbacks here perform no IO.
    private InstallationWarmLease? slot;
    private long ordinal;
    private bool closed;
    internal bool Busy{get{lock(sync)return slot!=null;}}
    internal InstallationWarmLease Reserve(InstallationWarmConnection connection,long epoch)
    {
        connection.Consume();
        lock(sync)
        {
            if(closed||slot!=null)throw new InvalidOperationException("Warm admission unavailable.");
            if(ordinal==long.MaxValue)throw new InvalidOperationException("Warm ordinal exhausted.");
            var identity=new InstallationWarmIdentity(proof.Launch,proof.Fingerprint,connection.Id,Guid.NewGuid(),++ordinal,connection.Nonce,InstallationWarmIdentity.NewNonce());
            return ReserveCore(identity,epoch);
        }
    }
    internal InstallationWarmLease ReceiveReserve(InstallationWarmIdentity identity,long epoch)
    {
        identity.Require(proof);
        lock(sync)
        {
            if(closed||slot!=null||identity.Ordinal<=ordinal)throw new InvalidDataException("Stale or busy warm reservation.");
            ordinal=identity.Ordinal;return ReserveCore(identity,epoch);
        }
    }
    private InstallationWarmLease ReserveCore(InstallationWarmIdentity identity,long epoch)
    {
        identity.Require(proof);
        if(epoch<0)throw new InvalidDataException("Invalid UI epoch.");
        var lease=new InstallationWarmLease(new(identity,InstallationWarmIdentity.NewNonce(),epoch),clock.GetTimestamp());slot=lease;
        try{Arm(lease,TimeSpan.FromSeconds(10),InstallationWarmPhase.Reserved);return lease;}
        catch{slot=null;lease.Stop.Dispose();throw;}
    }
    private void Arm(InstallationWarmLease lease,TimeSpan due,InstallationWarmPhase phase)
        =>lease.Timers.Add(clock.CreateTimer(_=>
        { _=Revoke(lease,phase); },null,due,Timeout.InfiniteTimeSpan));
    internal void BindRemoteGrant(InstallationWarmLease lease,InstallationWarmGrant grant)
    {
        lock(sync)
        {
            Require(lease);
            if(lease.RemoteGrantBound||lease.Phase!=InstallationWarmPhase.Reserved||grant.Identity!=lease.Grant.Identity||!InstallationWarmIdentity.Nonce(grant.Nonce)||grant.Epoch<0)throw new InvalidDataException("Invalid remote warm grant.");
            lease.Grant=grant;lease.RemoteGrantBound=true;
        }
    }
    internal bool Commit(InstallationWarmLease lease,InstallationWarmSubmit submit)
    {
        OleImageFileDrop.ValidatePath(submit.Path);
        bool expired;
        lock(sync)
        {
            Require(lease);if(closed||submit.Grant!=lease.Grant||lease.Phase!=InstallationWarmPhase.Reserved)throw new InvalidDataException("Consumed or mismatched warm grant.");
            expired=clock.GetElapsedTime(lease.Started)>=TimeSpan.FromSeconds(10);
            if(!expired)
            {
                lease.Phase=InstallationWarmPhase.Preparing;lease.Started=clock.GetTimestamp();Arm(lease,TimeSpan.FromSeconds(15),InstallationWarmPhase.Preparing);return true;
            }
        }
        _=Revoke(lease);return false;
    }
    internal bool TryAccept(InstallationWarmLease lease,Func<InstallationWarmAccepted?> accept,bool previewPending=false)
    {try{return TryAcceptCore(lease,accept,previewPending);}catch{_=Revoke(lease);throw;}}
    private bool TryAcceptCore(InstallationWarmLease lease,Func<InstallationWarmAccepted?> accept,bool previewPending)
    {
        bool expired;
        lock(sync)
        {
            Require(lease);if(closed||lease.Phase!=InstallationWarmPhase.Preparing)return false;
            expired=clock.GetElapsedTime(lease.Started)>=TimeSpan.FromSeconds(15);
            if(!expired)
            {
                // Synchronous memory-only source/Gate swap. WPF, cancellation and disposal run after this lock is released.
                var accepted=accept();if(accepted is not {Source:var source,Version:>0}||source==Guid.Empty)return false;
                lease.Accepted=accepted;lease.PreviewConfirmed=!previewPending;lease.Phase=InstallationWarmPhase.AppOwned;return true;
            }
        }
        _=Revoke(lease);return false;
    }
    internal bool FreezeEpoch(InstallationWarmLease lease,long epoch)
    {lock(sync){Require(lease);if(closed||lease.Phase!=InstallationWarmPhase.Reserved||epoch<0||clock.GetElapsedTime(lease.Started)>=TimeSpan.FromSeconds(10))return false;lease.Grant=lease.Grant with{Epoch=epoch};return true;}}
    internal bool ConfirmPreview(InstallationWarmLease lease,Func<bool> current)
    {lock(sync){Require(lease);if(lease.Phase!=InstallationWarmPhase.AppOwned||!current())return false;lease.PreviewConfirmed=true;return true;}}
    internal Task Revoke(InstallationWarmLease lease,InstallationWarmPhase? expected=null)
    {
        bool start=false;
        lock(sync)
        {
            if(!ReferenceEquals(slot,lease)||lease.Phase==InstallationWarmPhase.AppOwned||expected!=null&&lease.Phase!=expected)return Task.CompletedTask;
            lease.Phase=InstallationWarmPhase.Revoked;
            if(!lease.CancelStarted){lease.CancelStarted=true;start=true;}
        }
        if(start)StartCancellation(lease);
        return lease.CancelJoined.Task;
    }
    private static void StartCancellation(InstallationWarmLease lease)
    {
        try{lease.BeforeCancellationForTest?.Invoke();lease.OriginalCancellation=lease.Stop.CancelAsync();_=JoinCancellation(lease);}
        catch(Exception error){lease.CancelJoined.TrySetException(error);}
    }
    private static async Task JoinCancellation(InstallationWarmLease lease)
    {try{await lease.OriginalCancellation!.ConfigureAwait(false);lease.CancelJoined.TrySetResult();}catch(Exception error){lease.CancelJoined.TrySetException(error);}}
    internal Task Quiesce()
    {
        InstallationWarmLease? held;bool start=false;
        lock(sync)
        {
            closed=true;held=slot;if(held==null||held.Phase==InstallationWarmPhase.AppOwned)return Task.CompletedTask;
            held.Phase=InstallationWarmPhase.Revoked;if(!held.CancelStarted){held.CancelStarted=true;start=true;}
        }
        if(start)StartCancellation(held);return held.CancelJoined.Task;
    }
    internal long HighWater{get{lock(sync)return ordinal;}}
    internal void BurnBusy(InstallationWarmIdentity identity)
    {identity.Require(proof);lock(sync){if(identity.Ordinal<=ordinal)throw new InvalidDataException("Replayed warm ordinal.");ordinal=identity.Ordinal;}}
    internal void OwnCancellation(InstallationWarmLease lease,Action callback)
    {
        var installed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock(sync){Require(lease);if(lease.Sealed||lease.Originals.Count>=8)throw new InvalidOperationException("Cancellation registration unavailable.");lease.Originals.Add(installed.Task);}
        try{var registration=lease.Token.Register(callback);lock(sync)lease.Registrations.Add(registration);installed.TrySetResult();}
        catch(Exception error){installed.TrySetException(error);throw;}
    }
    internal Task RunOwned(InstallationWarmLease lease,Func<CancellationToken,Task> run,bool control=false)
    {
        lock(sync)
        {
            Require(lease);if(lease.Sealed||!control&&lease.Phase is (InstallationWarmPhase.Revoked or InstallationWarmPhase.AppOwned)||lease.Originals.Count>=8)throw new InvalidOperationException("Warm work registration unavailable.");
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task original=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);await run(lease.Token).ConfigureAwait(false);});lease.Originals.Add(original);begin.TrySetResult();return original;
        }
    }
    internal InstallationWarmPhase Phase(InstallationWarmLease lease){lock(sync){Require(lease);return lease.Phase;}}
    internal Task<bool> RetireAsync(InstallationWarmLease lease,Func<ValueTask<bool>> finalize,Func<InstallationWarmResult,Task> write)
    {
        lock(sync)
        {
            Require(lease);if(lease.Phase is InstallationWarmPhase.Reserved or InstallationWarmPhase.Preparing)throw new InvalidOperationException("Warm request is not terminal.");
            if(lease.Retirement is Task<bool> prior&&(!prior.IsCompletedSuccessfully||prior.Result))return prior;
            lease.Sealed=true;var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var retirement=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);return await RetireCore(lease,finalize,write).ConfigureAwait(false);});lease.Retirement=retirement;begin.TrySetResult();return retirement;
        }
    }
    private async Task<bool> RetireCore(InstallationWarmLease lease,Func<ValueTask<bool>> finalize,Func<InstallationWarmResult,Task> write)
    {
        List<Exception> failures=[];
        foreach(var task in lease.Originals)try{await task.ConfigureAwait(false);}catch(OperationCanceledException error)when(lease.Stop.IsCancellationRequested&&error.CancellationToken==lease.Token){ /* Exact original cancelled task observed; never an unjoined waiter. */ }catch(Exception error){failures.Add(error);}
        foreach(var timer in lease.Timers)try{await timer.DisposeAsync().ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        foreach(var registration in lease.Registrations)try{await registration.DisposeAsync().ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        if(lease.CancelStarted)try{await lease.CancelJoined.Task.ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        bool confirmed=false;try{confirmed=await finalize().ConfigureAwait(false);}catch(Exception error){failures.Add(error);}
        if(failures.Count!=0)throw new AggregateException("Original warm work or cleanup failed; retained.",failures);
        if(!confirmed)return false;
        InstallationWarmResult result;
        lock(sync){Require(lease);bool ready=lease.Accepted!=null&&lease.PreviewConfirmed;result=new(lease.Grant.Identity,ready?InstallationWarmOutcome.PreviewReady:InstallationWarmOutcome.Rejected,ready?lease.Accepted!.Source:Guid.Empty,ready?lease.Accepted!.Version:0,ready?lease.Accepted!.SourceVersion:Guid.Empty);}
        // Original write/flush, including backpressure, must actually finish before this slot can retire.
        var writeBegin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lease.FinalWrite=Task.Run(async()=>{await writeBegin.Task.ConfigureAwait(false);await write(result).ConfigureAwait(false);});
        writeBegin.TrySetResult();await lease.FinalWrite.ConfigureAwait(false);
        lock(sync){Require(lease);slot=null;lease.Stop.Dispose();return true;}
    }
    private void Require(InstallationWarmLease lease)
    {if(!ReferenceEquals(slot,lease))throw new InvalidOperationException("Not the original warm slot.");}
}
