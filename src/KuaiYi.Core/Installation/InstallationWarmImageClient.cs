using System.Diagnostics;
namespace KuaiYi.Core.Installation;

// Private inherited-channel actor. External Windows ingress additionally requires the original live session and real peers.
internal sealed class InstallationWarmImageClient(InstallationControlledLaunch proof,TimeProvider clock,Func<string,CancellationToken,Task> send)
{
    internal sealed class Request(InstallationWarmLease lease)
    {
        internal readonly InstallationWarmLease Lease=lease;
        internal readonly TaskCompletionSource<InstallationWarmGrant?> Granted=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly TaskCompletionSource<InstallationWarmResult> Result=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool GrantSeen,ResultSeen,Submitted,Cancelled;
        internal Task? CancelWrite;
        internal Task<bool>? Finish;
    }
    private readonly object sync=new();
    private readonly InstallationWarmImageAdmission admission=new(proof,clock);
    private Request? current;
    private bool closed;
    internal Action? Quiescing; // Installed live capability only; synchronous lock-free seal, never IO or joins.
    internal bool IsOpen{get{lock(sync)return !closed;}}
    internal bool Busy=>admission.Busy;
    internal Request? Active{get{lock(sync)return current;}}
    internal async Task<Request> ReserveAsync(InstallationWarmConnection connection)
    {
        Request request;Task write;
        lock(sync)
        {
            if(closed)throw new InvalidOperationException("Warm channel closed.");
            request=new(admission.Reserve(connection,0));current=request;
            _=admission.RunOwned(request.Lease,_=>request.Granted.Task);
            _=admission.RunOwned(request.Lease,_=>request.Result.Task);
            write=admission.RunOwned(request.Lease,ct=>send("WARM_RESERVE|"+InstallationWarmImageWire.Encode(request.Lease.Grant.Identity),ct));
            admission.OwnCancellation(request.Lease,()=>{_=Cancel(request);});
        }
        await write.ConfigureAwait(false);var grant=await request.Granted.Task.ConfigureAwait(false);
        if(grant==null){if(request.Finish!=null)await request.Finish.ConfigureAwait(false);throw new IOException("Warm reservation rejected; do not replay or fall back.");}
        admission.BindRemoteGrant(request.Lease,grant);return request;
    }
    internal async Task<InstallationWarmResult> CommitAsync(Request request,string path)
    {
        Task? write=null;
        lock(sync)
        {
            Require(request);
            if(request.Submitted||request.Cancelled)throw new InvalidOperationException("Warm attempt consumed.");
            var submit=new InstallationWarmSubmit(request.Lease.Grant,path);
            if(admission.Commit(request.Lease,submit))
            {request.Submitted=true;write=admission.RunOwned(request.Lease,ct=>send("WARM_COMMIT|"+InstallationWarmImageWire.Encode(submit),ct));}
        }
        if(write==null){_=Cancel(request);throw new OperationCanceledException(request.Lease.Token);}
        await write.ConfigureAwait(false);var result=await request.Result.Task.ConfigureAwait(false);
        lock(sync){if(request.Cancelled)throw new IOException("Warm outcome is uncertain after cancellation; do not replay.");}
        return result;
    }
    internal Task Cancel(Request request,bool sendCancel=true)
    {
        lock(sync)
        {
            Require(request);if(request.ResultSeen)return Task.CompletedTask;request.Cancelled=true;
            Task cancellation=admission.Revoke(request.Lease); // Sets state/token before waiting for any writer.
            request.Granted.TrySetCanceled(request.Lease.Token);
            if(sendCancel&&!closed&&request.CancelWrite==null&&!request.ResultSeen)
                request.CancelWrite=admission.RunOwned(request.Lease,_=>send("WARM_CANCEL|"+InstallationWarmImageWire.Encode(request.Lease.Grant.Identity),CancellationToken.None),true);
            return cancellation;
        }
    }
    internal void Quiesce()
    {
        lock(sync){closed=true;Quiescing?.Invoke();if(current is {} request&&!request.ResultSeen)_=Cancel(request,false);}
    }
    internal void Receive(string line)
    {
        lock(sync)
        {
            if(line==$"WARM_QUIESCE|{proof.Launch:N}|{proof.Fingerprint}"){Quiesce();return;}
            var request=current??throw new InvalidDataException("Unexpected warm receipt.");
            if(line.StartsWith("WARM_REJECTED|",StringComparison.Ordinal))
            {
                var identity=InstallationWarmImageWire.Decode<InstallationWarmIdentity>(line[14..]);identity.Require(proof);
                if(request.GrantSeen||request.ResultSeen||request.Submitted||identity!=request.Lease.Grant.Identity)throw new InvalidDataException("Invalid warm busy receipt.");
                request.ResultSeen=true;_=admission.Revoke(request.Lease);
                request.Finish=admission.RetireAsync(request.Lease,()=>ValueTask.FromResult(true),_=>Task.CompletedTask);
                request.Granted.TrySetResult(null);request.Result.TrySetResult(new(identity,InstallationWarmOutcome.Rejected,Guid.Empty,0));return;
            }
            if(line.StartsWith("WARM_GRANTED|",StringComparison.Ordinal))
            {
                var grant=InstallationWarmImageWire.Decode<InstallationWarmGrant>(line[13..]);grant.Identity.Require(proof);
                if(request.GrantSeen||request.ResultSeen||grant.Identity!=request.Lease.Grant.Identity||!InstallationWarmIdentity.Nonce(grant.Nonce)||grant.Epoch<0)throw new InvalidDataException("Invalid warm grant receipt.");
                request.GrantSeen=true;request.Granted.TrySetResult(grant);return;
            }
            if(line.StartsWith("WARM_RESULT|",StringComparison.Ordinal))
            {
                var result=InstallationWarmImageWire.Decode<InstallationWarmResult>(line[12..]);result.Identity.Require(proof);
                if(request.ResultSeen||result.Identity!=request.Lease.Grant.Identity||!Enum.IsDefined(result.Outcome)||
                    result.Outcome==InstallationWarmOutcome.PreviewReady&&(!request.Submitted||result.Source==Guid.Empty||result.Version<=0)||
                    result.Outcome==InstallationWarmOutcome.Rejected&&(result.Source!=Guid.Empty||result.Version!=0||result.SourceVersion!=Guid.Empty))throw new InvalidDataException("Invalid warm terminal receipt.");
                request.ResultSeen=true;
                if(result.Outcome==InstallationWarmOutcome.PreviewReady)
                {
                    // The original remote receipt still settles. A lost local deadline race cannot authorize success,
                    // nor does it grant authority to roll back the remote App-owned source.
                    if(!admission.TryAccept(request.Lease,()=>new(result.Source,result.Version,result.SourceVersion)))request.Cancelled=true;
                }
                else _=admission.Revoke(request.Lease);
                request.Finish=admission.RetireAsync(request.Lease,()=>ValueTask.FromResult(true),_=>Task.CompletedTask);
                if(!request.GrantSeen)request.Granted.TrySetResult(null);
                request.Result.TrySetResult(result);return;
            }
            throw new InvalidDataException("Unknown warm receipt phase.");
        }
    }
    internal void Disconnected(Exception error)
    {
        lock(sync)
        {
            closed=true;Quiescing?.Invoke();if(current is not {} request||request.ResultSeen)return;
            _=admission.Revoke(request.Lease);
            request.Finish=admission.RetireAsync(request.Lease,()=>ValueTask.FromResult(true),_=>Task.CompletedTask);
            request.Granted.TrySetException(error);request.Result.TrySetException(error);
        }
    }
    internal async Task JoinAsync()
    {
        Task<bool>? finish;lock(sync){finish=current?.Finish;}
        if(finish!=null)await finish.ConfigureAwait(false);
    }
    private void Require(Request request)
    {if(!ReferenceEquals(current,request))throw new InvalidOperationException("Not the original warm attempt.");}
}
