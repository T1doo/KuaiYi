namespace KuaiYi.Core.Installation;

// Internal stage-A opt-in, reached only by the synthetic installation fixture. The App does not attach it.
internal sealed class InstallationWarmImageControl
{
    private readonly InstallationControlledLaunch proof;
    private readonly Func<long> epoch;
    private readonly Func<InstallationWarmImageAdmission,InstallationWarmLease,string,CancellationToken,Task> prepare;
    private readonly Func<InstallationWarmLease,ValueTask<bool>> finalize;
    private readonly Func<string,CancellationToken,Task> send;
    private readonly object sync=new();
    private InstallationWarmLease? current;
    private Task<bool> completion=Task.FromResult(true);
    private Task quiesceWrite=Task.CompletedTask,busyWrite=Task.CompletedTask;
    private TaskCompletionSource changed=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool quiesced;
    private long retiredOrdinal;
    internal readonly InstallationWarmImageAdmission Admission;
    internal bool EverReserved{get;private set;}
    internal InstallationWarmImageControl(InstallationControlledLaunch proof,TimeProvider clock,Func<long> epoch,
        Func<InstallationWarmImageAdmission,InstallationWarmLease,string,CancellationToken,Task> prepare,
        Func<InstallationWarmLease,ValueTask<bool>> finalize,Func<string,CancellationToken,Task> send)
    {this.proof=proof;this.epoch=epoch;this.prepare=prepare;this.finalize=finalize;this.send=send;Admission=new(proof,clock);}
    internal bool Handle(string line)
    {lock(sync)return HandleCore(line);}
    private bool HandleCore(string line)
    {
        if(line.StartsWith("WARM_RESERVE|",StringComparison.Ordinal))
        {
            if(quiesced||completion.IsFaulted||completion.IsCanceled)throw new InvalidDataException("Warm request unavailable.");
            var identity=InstallationWarmImageWire.Decode<InstallationWarmIdentity>(line[13..]);
            if(Admission.Busy||!completion.IsCompletedSuccessfully)
            {
                Admission.BurnBusy(identity);
                if(!busyWrite.IsCompletedSuccessfully)throw new InvalidDataException("Warm busy response already pending.");
                var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                busyWrite=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);await send("WARM_REJECTED|"+InstallationWarmImageWire.Encode(identity),CancellationToken.None).ConfigureAwait(false);});begin.TrySetResult();return true;
            }
            var lease=Admission.ReceiveReserve(identity,epoch());current=lease;EverReserved=true;
            _=Admission.RunOwned(lease,ct=>send("WARM_GRANTED|"+InstallationWarmImageWire.Encode(lease.Grant),ct));
            Admission.OwnCancellation(lease,()=>
            {
                lock(sync)if(ReferenceEquals(current,lease)&&completion.IsCompletedSuccessfully)Complete(lease,Task.CompletedTask);
            });return true;
        }
        if(line.StartsWith("WARM_COMMIT|",StringComparison.Ordinal))
        {
            var submit=InstallationWarmImageWire.Decode<InstallationWarmSubmit>(line[12..]);var lease=Require(submit.Grant.Identity);
            if(!Admission.Commit(lease,submit)){Complete(lease,Task.CompletedTask);return true;}
            Task work=Admission.RunOwned(lease,ct=>prepare(Admission,lease,submit.Path,ct));Complete(lease,work);return true;
        }
        if(line.StartsWith("WARM_CANCEL|",StringComparison.Ordinal))
        {
            var identity=InstallationWarmImageWire.Decode<InstallationWarmIdentity>(line[12..]);identity.Require(proof);
            if((current==null||current.Grant.Identity!=identity)&&identity.Ordinal<=Admission.HighWater)return true; // A lost terminal receipt never gives a late cancel authority over a new source.
            var lease=Require(identity);
            _=Admission.Revoke(lease);
            if(completion.IsCompletedSuccessfully)Complete(lease,Task.CompletedTask);return true;
        }
        return false;
    }
    private InstallationWarmLease Require(InstallationWarmIdentity identity)
    {
        identity.Require(proof);
        if(current is not {} lease||lease.Grant.Identity!=identity)throw new InvalidDataException("Warm request identity mismatch.");return lease;
    }
    private void Complete(InstallationWarmLease lease,Task work)
    {
        completion=work.ContinueWith(async _=>
        {
            // RetireAsync observes this exact work task, including its original failure, after cancellation starts.
            if(Admission.Phase(lease)==InstallationWarmPhase.Preparing)_=Admission.Revoke(lease);
            bool retired=await Admission.RetireAsync(lease,()=>finalize(lease),result=>send("WARM_RESULT|"+InstallationWarmImageWire.Encode(result),CancellationToken.None)).ConfigureAwait(false);
            if(retired)lock(sync){retiredOrdinal=lease.Grant.Identity.Ordinal;if(ReferenceEquals(current,lease))current=null;}
            return retired;
        },CancellationToken.None,TaskContinuationOptions.ExecuteSynchronously,TaskScheduler.Default).Unwrap();
        changed.TrySetResult();changed=new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    internal void Quiesce(){lock(sync)QuiesceCore();}
    private void QuiesceCore()
    {
        if(quiesced)return;quiesced=true;
        _=Admission.Quiesce(); // Raw revocation must happen before any wait on the writer.
        var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        quiesceWrite=Task.Run(async()=>{await begin.Task.ConfigureAwait(false);await send($"WARM_QUIESCE|{proof.Launch:N}|{proof.Fingerprint}",CancellationToken.None).ConfigureAwait(false);});begin.TrySetResult();
        if(current is {} lease&&completion.IsCompletedSuccessfully)Complete(lease,Task.CompletedTask);
    }
    internal bool RetryCleanup()
    {
        lock(sync)
        {
            if(current==null||!completion.IsCompletedSuccessfully||completion.Result)return false;
            Complete(current,Task.CompletedTask);return true;
        }
    }
    internal Task<bool> OriginalCompletion{get{lock(sync)return completion;}}
    internal Task NextCompletionChange{get{lock(sync)return changed.Task;}}
    internal async Task SettleAsync()
    {
        Quiesce();List<Exception> errors=[];
        try
        {
            while(true)
            {
                Task<bool> original;Task change;lock(sync){original=completion;change=changed.Task;}
                if(await original.ConfigureAwait(false))break;
                await change.ConfigureAwait(false); // Only an explicit same-owner retry may advance a false cleanup result.
            }
        }
        catch(Exception error){errors.Add(error);}
        Task notice,busy;lock(sync){notice=quiesceWrite;busy=busyWrite;}
        foreach(var original in new[]{notice,busy})try{await original.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("Original warm control unconfirmed.",errors);
    }
}
