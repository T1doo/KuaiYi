using System.Diagnostics;
namespace KuaiYi.Core.Installation;

public sealed partial class InstallationUpgradeCoordinator
{
    // No public claims/Process/PID factory. Only the normal original session path invokes this hook after READY/cold registration.
    internal Func<LiveImageSession,CancellationToken,Task>? WarmImageEndpoint{get;init;}
    internal sealed class LiveImageSession
    {
        private readonly InstallationUpgradeCoordinator owner;
        private readonly Role role;
        private readonly InstallationAdmission admission;
        private readonly InstallationExecutionOwnership execution;
        private readonly InstallationApplicationClient protocol;
        private readonly CancellationTokenSource stop=new();
        private readonly TaskCompletionSource cancelled=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int closed;
        private Task? originalCancellation;
        internal InstallationControlledLaunch Proof{get;}
        internal Process OriginalChild{get;}
        internal InstallationWarmImageClient Client{get;}
        internal CancellationToken Token=>stop.Token;
        internal LiveImageSession(InstallationUpgradeCoordinator owner)
        {
            this.owner=owner;
            if(!owner.sessionAttempted||owner.disposed||owner.pending!=null||owner.handoff!=null||owner.roles.Count!=1||owner.protocol is not InstallationApplicationClient original)
                throw new InstallationRecoveryException("No original normal installation session.");
            role=owner.roles[0];admission=owner.admission??throw new InstallationBusyException();execution=owner.execution??throw new InstallationBusyException();protocol=original;
            if(!role.RetainTasks||role.Name!="KuaiYi.App.exe"||role.Process==null||role.Startup is not {IsCompletedSuccessfully:true}||!ReferenceEquals(role.Startup.Result,role.Process)||role.LaunchProof is not {UpgradeRecord:null} proof)
                throw new InstallationRecoveryException("Original healthy App role unavailable.");
            OriginalChild=role.Process;Proof=proof;RequireRecords();Client=protocol.AttachWarm(OriginalChild,TimeProvider.System);Client.Quiescing=Quiesce;
        }
        internal bool MemoryCurrent=>Volatile.Read(ref closed)==0&&!owner.disposed&&ReferenceEquals(owner.admission,admission)&&ReferenceEquals(owner.execution,execution)&&ReferenceEquals(role.Process,OriginalChild)&&!role.Transferred&&!role.Finalized;
        internal void RequireRecords()
        {
            if(!MemoryCurrent||OriginalChild.HasExited)throw new InstallationBusyException();
            admission.Require(execution.DirectoryPath);execution.Require(Proof.Root);Proof.RequireImageRecords();
            if(!MemoryCurrent||OriginalChild.HasExited)throw new InstallationBusyException();
        }
        internal async Task VerifyAsync(CancellationToken cancellation)
        {
            RequireRecords();await Task.Run(()=>Proof.RequireRecords(cancellation),CancellationToken.None).ConfigureAwait(false);
            cancellation.ThrowIfCancellationRequested();RequireRecords();
        }
        internal void Quiesce()
        {
            if(Interlocked.Exchange(ref closed,1)!=0)return;
            try{originalCancellation=stop.CancelAsync();_=JoinCancellation();}catch(Exception error){cancelled.TrySetException(error);}
        }
        private async Task JoinCancellation()
        {try{await originalCancellation!.ConfigureAwait(false);cancelled.TrySetResult();}catch(Exception error){cancelled.TrySetException(error);}}
        internal async Task RunEndpointAsync(Func<LiveImageSession,CancellationToken,Task> endpoint,CancellationToken cancellation)
        {
            List<Exception> errors=[];using var registration=cancellation.Register(Quiesce);
            try{await endpoint(this,Token).ConfigureAwait(false);}catch(OperationCanceledException error)when(Token.IsCancellationRequested&&error.CancellationToken==Token){}
            catch(Exception error){errors.Add(error);}
            Quiesce();try{await cancelled.Task.ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
            if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if(errors.Count>1)throw new AggregateException("Original installed image endpoint unconfirmed.",errors);
        }
    }
}
