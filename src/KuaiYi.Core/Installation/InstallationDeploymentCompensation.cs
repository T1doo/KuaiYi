using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

internal enum InstallationCompensationStatus { Compensated,Unconfirmed }
internal enum InstallationCompensationStage { Ticket,Plan,Verified,HostReceipt,Edge,Chrome,Active,Current,Closing,Retired,Closed,BeforeArchive }
internal sealed record InstallationCompensationOutcome(InstallationCompensationStatus Status,IReadOnlyList<Exception> Errors,IReadOnlyList<Exception> OriginalSessionErrors);
internal sealed class InstallationCompensationFaults
{
    internal Action<InstallationCompensationStage>? Stage {get;init;}
    internal Action<int>? BeforeMutation {get;init;}
    internal Action<string,string,bool>? Publish {get;init;}
    internal Action<string,string>? Retire {get;init;}
    internal Action? BeforeExecutionClose {get;set;}
    internal Action<string,string>? Archive {get;init;}
}

internal sealed partial class InstallationDeploymentSession
{
    private InstallationDeploymentCompensation? compensation;
    internal InstallationDeploymentCompensation BeginCompensation(CancellationToken cancellation=default,InstallationCompensationFaults? faults=null,InstallationPreparationFaults? ioFaults=null,InstallationAdmissionFaults? admissionFaults=null)
    {
        lock(sync)
        {
            // Memory-only qualification before publication. All new exact reads belong to the new original Task.
            scope.RequireOriginalSession(this);
            if(compensation!=null||accepted!=null||qualificationUnknown||Completion is not{IsCompletedSuccessfully:true}||Completion.Result.Status!=InstallationDeploymentSessionStatus.RollbackRequired)
                throw new InstallationRecoveryException("One original failed, settled deployment session is required for compensation.");
            var before=JsonSerializer.Deserialize<InstallationDeploymentBefore>(records[Path.Combine(Root,"deployment-before-"+scope.Operation+".json")])??throw new InstallationRecoveryException("Missing original Before.");
            var value=new InstallationDeploymentCompensation(this,controller,prepared.Plan,prepared.Sha256,before,registry,records,launch,cancellation,faults,ioFaults,admissionFaults);
            compensation=value;value.Start();return value;
        }
    }
    internal void RequireOriginalCompensation(InstallationDeploymentCompensation value)
    {lock(sync){scope.RequireOriginalSession(this);if(!ReferenceEquals(compensation,value)||accepted!=null||Completion is not{IsCompletedSuccessfully:true}||Completion.Result.Status!=InstallationDeploymentSessionStatus.RollbackRequired)throw new InstallationRecoveryException("Not the original failed session compensation.");}}
    internal void ReleaseCompensatedRetention(InstallationDeploymentCompensation value)
    {RequireOriginalCompensation(value);lock(Retained)Retained.Remove(this);}
}

// New original task/journal/IO ledger. Never reopens the deployment Scope or borrows old Prepared mutation authority.
internal sealed class InstallationDeploymentCompensation
{
    private static readonly HashSet<InstallationDeploymentCompensation> Retained=[];
    private readonly InstallationDeploymentSession session;
    private readonly InstallationUpgradeCoordinator controller;
    private readonly InstallationDeploymentPlan target;
    private readonly InstallationDeploymentBefore before;
    private readonly IInstallationDeploymentPublicationRegistry registry;
    private readonly Dictionary<string,byte[]> history;
    private readonly CancellationToken cancellation;
    private readonly InstallationCompensationFaults? faults;
    private readonly InstallationAdmission admission;
    private readonly InstallationPreparationIO io;
    private readonly TaskCompletionSource start=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly string root,data,pending,planPath,terminalPath,ticketBackup,archive;
    private readonly string preparedSha;
    private readonly Guid launch;
    private readonly byte[] planBytes;
    private readonly object settlementSync=new();
    private readonly List<Task<IReadOnlyList<Exception>>> settlements=[];
    private Task<IReadOnlyList<Exception>>? settlement;
    private byte[]? ticket;
    private InstallationExecutionOwnership? execution;
    private InstallationExecutionOwnership.Retirement? retirement;
    private bool gateHeld,closing,unknown,ticketAttempted,ticketComplete,planComplete;
    private int prefix,attempt;
    private readonly Dictionary<string,byte[]> remnants=[];
    internal string Operation{get;}=Guid.NewGuid().ToString("N");
    internal string RetiredPath{get;}
    internal Task<InstallationCompensationOutcome> Completion{get;}
    internal Task? OriginalSettlement{get{lock(settlementSync)return settlement;}}
    internal bool PendingAttempted=>ticketAttempted;
    internal bool CleanupConfirmed{get{lock(settlementSync)return Completion.IsCompleted&&settlements.All(t=>t.IsCompleted)&&io.CleanupConfirmed&&registry.CleanupConfirmed&&admission.CleanupConfirmed&&!gateHeld&&(execution==null||retirement is{CleanupConfirmed:true});}}
    internal bool OriginalExecutionLeaseRetained=>execution!=null&&(retirement==null||!retirement.CleanupConfirmed);
    internal InstallationDeploymentCompensation(InstallationDeploymentSession session,InstallationUpgradeCoordinator controller,InstallationDeploymentPlan target,string preparedSha,
        InstallationDeploymentBefore before,IInstallationDeploymentPublicationRegistry registry,Dictionary<string,byte[]> history,Guid launch,CancellationToken cancellation,
        InstallationCompensationFaults? faults,InstallationPreparationFaults? ioFaults,InstallationAdmissionFaults? admissionFaults)
    {
        this.session=session;this.controller=controller;this.target=target;this.preparedSha=preparedSha;this.before=before;this.registry=registry;
        this.history=history.ToDictionary(x=>x.Key,x=>x.Value.ToArray());this.launch=launch;this.cancellation=cancellation;this.faults=faults;root=session.Root;data=session.UserData;
        pending=Path.Combine(root,"upgrade-pending.json");planPath=Path.Combine(root,"compensation-plan-"+Operation+".json");terminalPath=Path.Combine(root,"compensation-terminal-"+Operation+".tmp");
        ticketBackup=Path.Combine(root,"compensation-ticket-"+Operation+".json");archive=Path.Combine(root,"compensation-finished-"+Operation+".json");RetiredPath=Path.Combine(root,"compensation-execution-"+Operation+".json");
        // The exact execution bytes are filled after the new Task obtains the original object, before persistence.
        planBytes=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=3,kind="KuaiYiCompensationRestorePlan",operation=Operation,originalOperation=target.Operation,preparedSha256=preparedSha,failedLaunch=launch,target,before,originalRecords=this.history.ToDictionary(x=>x.Key,x=>Hash(x.Value))});
        if(planBytes.Length>131072)throw new InvalidDataException("Compensation plan exceeds its actual encoded bound.");
        admission=InstallationAdmission.CreateOwner(root,admissionFaults);
        io=InstallationPreparationIO.ReadOnly(root).CreatePublicationChannel(root,new[]{pending,planPath,terminalPath,ticketBackup}.Concat(new[]{1,4,5}.SelectMany(n=>new[]{Temporary(n),Removed(n)})),ioFaults);
        Completion=Task.Run(RunAsync);
    }
    internal void Start(){lock(Retained)Retained.Add(this);start.SetResult();}
    internal void OwnControllerGate(InstallationUpgradeCoordinator actual)
    {if(!ReferenceEquals(controller,actual)||gateHeld)throw new InstallationRecoveryException("Wrong original compensation gate.");gateHeld=true;}
    internal void OwnControllerGateForRelease(InstallationUpgradeCoordinator actual)
    {if(!ReferenceEquals(controller,actual)||!gateHeld)throw new InstallationRecoveryException("Wrong original compensation gate release.");}
    internal void RequireOriginalExecution(InstallationExecutionOwnership value)
    {session.RequireOriginalCompensation(this);controller.RequireCompensationBinding(this);if(!ReferenceEquals(value,execution)||!closing)throw new InstallationRecoveryException("Wrong original execution retirement authority.");admission.RequireRetirementCleanup(root);}
    internal void RequireRetirement(InstallationExecutionOwnership.Retirement value,InstallationExecutionOwnership original)
    {session.RequireOriginalCompensation(this);controller.RequireCompensationBinding(this);if(!closing||!ReferenceEquals(original,execution)||!ReferenceEquals(value,retirement))throw new InstallationRecoveryException("Wrong original Closing retirement owner.");}
    private string Temporary(int step)=>Path.Combine(root,$"compensation-{Operation}-{step}.tmp");
    private string Removed(int step)=>Path.Combine(root,$"compensation-{Operation}-{step}-removed.json");
    private string Record(int step)=>step switch{1=>Path.Combine(root,"host-registration.json"),4=>InstallationBootstrapDeployment.ActivePath(root),5=>Path.Combine(root,"current.json"),_=>throw new InvalidOperationException()};
    private byte[] After(int step)=>Convert.FromBase64String(step switch{1=>target.Host.Receipt,4=>target.Bootstrap.Receipt,5=>target.TargetCurrent,_=>throw new InvalidOperationException()});
    private byte[]? Before(int step)=>Decode(step switch{1=>before.HostReceipt,4=>before.BootstrapActive,5=>before.Current,_=>throw new InvalidOperationException()});
    private static byte[]? Decode(string? value)=>value==null?null:Convert.FromBase64String(value);
    private static string Hash(byte[] value)=>Convert.ToHexString(SHA256.HashData(value));
    private void Optional(string path,byte[]? expected,int maximum)
    {if(expected==null)io.RequireAbsent(path);else io.RequireExactMetadata(path,expected,maximum);}
    private void RequireJournal(bool mutation)
    {
        if(registry.ConflictObserved)unknown=true;
        if(unknown||io.StorageUnconfirmed||!io.CleanupConfirmed||!registry.CleanupConfirmed)throw new InstallationRecoveryException("Original compensation resources or ownership remain unconfirmed.");
        session.RequireOriginalCompensation(this);controller.RequireCompensationBinding(this);
        if(mutation){if(closing)throw new InstallationRecoveryException("Compensation mutation is sealed.");admission.RequireMutation(root);execution!.RequireCompensation(root,io);}
        if(ticketComplete)io.RequireExactMetadata(pending,ticket!,16384);if(planComplete)io.RequireExactMetadata(planPath,planBytes,131072);
        foreach(var record in history)io.RequireExactMetadata(record.Key,record.Value,131072);
    }
    private void VerifyPayloads()
    {
        var(planned,old)=InstallationStore.PlanCurrent(root,Decode(before.Current),target.Generation,CancellationToken.None,io);
        if(!planned.AsSpan().SequenceEqual(Decode(target.TargetCurrent)))throw new InstallationRecoveryException("Original current restore lineage differs.");
        var g=InstallationPackage.ReadInstalled(target.Generation.DirectoryPath,CancellationToken.None,expectedDigest:target.Generation.CatalogSha256,io:io);
        if(g.Schema!=3||g.SourceCatalogSha256!=target.GenerationSourceSha256||InstallationBootstrapPackage.Read(target.Bootstrap.CanonicalDirectory,target.BootstrapSourceSha256,target.Generation,CancellationToken.None,io).Revision!=target.BootstrapRevision)throw new InstallationRecoveryException("Committed G/B qualification differs.");
        if(before.BootstrapActive!=null)
        {
            var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(Decode(before.BootstrapOwner)??throw new InstallationRecoveryException("Old B owner is missing."));
            if(InstallationBootstrapDeployment.RequireReceipt(root,data,owner,Decode(before.BootstrapActive)!,CancellationToken.None,io).Generation!=old)throw new InstallationRecoveryException("Old B/Core/generation restore closure differs.");
        }
        if(before.HostReceipt!=null)
        {
            var receipt=InstallationBootstrapDeployment.Decode<InstallationHostOwnership>(Decode(before.HostReceipt)!);
            if(receipt!=target.Host.Before||receipt.Version!=old||receipt.Manifest!=Path.Combine(old!.DirectoryPath,InstallationPayload.HostManifestName))throw new InstallationRecoveryException("Old Host restore ownership differs.");
        }
        else if(target.Host.Before!=null)throw new InstallationRecoveryException("Old Host lineage differs.");
    }
    private static InstallationDeploymentRegistrationSlot Restored(InstallationDeploymentRegistrationSlot slot)=>slot.KeyExists?slot:new(true,null);
    private async Task VerifyPrefix(bool mutation)
    {
        try
        {
            RequireJournal(mutation);VerifyPayloads();
            io.RequireExactMetadata(InstallationBootstrapDeployment.OwnerPath(root),Convert.FromBase64String(target.Bootstrap.Owner),4096);
            foreach(int step in new[]{1,4,5})Optional(Record(step),prefix>=step?Before(step):After(step),step==5?4096:8192);
            var slots=await ReadSlots().ConfigureAwait(false);
            if(slots.Edge!=(prefix>=2?Restored(before.Registry.Edge):target.Host.Targets.Edge)||slots.Chrome!=(prefix>=3?Restored(before.Registry.Chrome):target.Host.Targets.Chrome))throw new InstallationRecoveryException("Compensation prefix is foreign or partial.");
            foreach(var row in remnants)io.RequireExactMetadata(row.Key,row.Value,8192);
            RequireJournal(mutation);
        }
        catch{unknown=true;throw;}
    }
    private async Task<InstallationDeploymentRegistrationSnapshot> ReadSlots()
    {
        var errors=new List<Exception>();InstallationDeploymentRegistrationSnapshot? result=null;
        try{result=await registry.ReadPublishedAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(!registry.CleanupConfirmed){unknown=true;errors.Add(new IOException("Original compensation registry close is unconfirmed."));}
        InstallationErrors.Throw(errors);return result!;
    }
    private void RestoreRecord(int step)
    {
        var expected=After(step);var restored=Before(step);string path=Record(step),temporary=restored==null?Removed(step):Temporary(step);
        io.RequireExactMetadata(path,expected,step==5?4096:8192);io.RequireAbsent(temporary);
        if(restored!=null){io.WritePublicationMetadata(temporary,restored,step==5?4096:8192);io.RequireExactMetadata(temporary,restored,8192);remnants.Add(temporary,restored);}
        RequireJournal(true);io.RequireExactMetadata(path,expected,8192);cancellation.ThrowIfCancellationRequested();
        if(faults?.Publish!=null)faults.Publish(restored==null?path:temporary,restored==null?temporary:path,restored!=null);
        else if(restored==null)File.Move(path,temporary);else File.Replace(temporary,path,null,false);
        if(restored==null)remnants.Add(temporary,expected);else remnants.Remove(temporary);
    }
    private async Task Restore()
    {
        await VerifyPrefix(true).ConfigureAwait(false);faults?.Stage?.Invoke(InstallationCompensationStage.Verified);
        for(int step=1;step<=5;step++)
        {
            cancellation.ThrowIfCancellationRequested();await VerifyPrefix(true).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();attempt=step;faults?.BeforeMutation?.Invoke(step);cancellation.ThrowIfCancellationRequested();
            if(step is 2 or 3)
            {bool chrome=step==3;await registry.WriteDefaultAsync(chrome,chrome?target.Host.Targets.Chrome:target.Host.Targets.Edge,(chrome?before.Registry.Chrome:before.Registry.Edge).Value).ConfigureAwait(false);await registry.CloseOriginalsAsync().ConfigureAwait(false);}
            else RestoreRecord(step);
            prefix=step;attempt=0;await VerifyPrefix(true).ConfigureAwait(false);faults?.Stage?.Invoke((InstallationCompensationStage)((int)InstallationCompensationStage.HostReceipt+step-1));
        }
    }
    private async Task<InstallationCompensationOutcome> RunAsync()
    {
        await start.Task.ConfigureAwait(false);var errors=new List<Exception>();
        try
        {
            cancellation.ThrowIfCancellationRequested();execution=controller.EnterCompensation(this,session);execution.RequireCompensation(root,io);
            var store=new InstallationStore(root,data,()=>true);store.AcquireAdmission(admission,execution,mutation:true,existing:true,keepSetup:true,executionIO:io);
            ticket=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=3,kind="KuaiYiPostCommitCompensationTicket",operation=Operation,originalOperation=target.Operation,preparedSha256=preparedSha,beforeSha256=target.BeforeSha256,committedSha256=Hash(history[Path.Combine(root,"deployment-committed-"+target.Operation+".json")]),planSha256=Hash(planBytes),failedLaunch=launch,executionSha256=Hash(execution.RecordBytes),retiredPath=RetiredPath});
            if(ticket.Length>16384)throw new InvalidDataException("Compensation ticket exceeds bound.");
            cancellation.ThrowIfCancellationRequested();io.RequireAbsent(pending);ticketAttempted=true;io.WritePublicationMetadata(pending,ticket,16384);io.RequireExactMetadata(pending,ticket,16384);ticketComplete=true;faults?.Stage?.Invoke(InstallationCompensationStage.Ticket);
            cancellation.ThrowIfCancellationRequested();io.RequireAbsent(planPath);io.WritePublicationMetadata(planPath,planBytes,131072);io.RequireExactMetadata(planPath,planBytes,131072);planComplete=true;faults?.Stage?.Invoke(InstallationCompensationStage.Plan);cancellation.ThrowIfCancellationRequested();admission.ReleaseSetup();
            await Restore().ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();closing=true;faults?.Stage?.Invoke(InstallationCompensationStage.Closing);
            retirement=execution.PrepareRetirement(this,io);retirement.Move(io,faults?.Retire);faults?.Stage?.Invoke(InstallationCompensationStage.Retired);
            await VerifyPrefix(false).ConfigureAwait(false);admission.RequireRetirementCleanup(root);retirement.Verify(io,true);
        }
        catch(Exception error){errors.Add(error);}
        // Finalization owns original registry work and every new Closing IO operation, not a stale clean snapshot.
        try{io.CloseOriginals();}catch(Exception error){errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        try{admission.Dispose();}catch(Exception error){errors.Add(error);}
        if(errors.Count==0&&closing&&prefix==5&&attempt==0&&io.CleanupConfirmed&&registry.CleanupConfirmed&&admission.CleanupConfirmed)
        {
            try{retirement!.CloseLease(faults?.BeforeExecutionClose);}
            catch(Exception error){errors.Add(error);}
        }
        if(errors.Count==0&&retirement is{CleanupConfirmed:true})
        {
            try
            {
                // Fresh reads after lease close still belong to this prepublished original Task/ledger.
                await VerifyPrefix(false).ConfigureAwait(false);retirement.Verify(io,false);controller.SealCompensatedExecution(this,session,execution!);faults?.Stage?.Invoke(InstallationCompensationStage.Closed);
            }
            catch(Exception error){errors.Add(error);}
        }
        try{io.CloseOriginals();}catch(Exception error){errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
        if(gateHeld)try{controller.ReleaseCompensationGate(this);gateHeld=false;}catch(Exception error){errors.Add(error);}
        if(errors.Count==0&&!unknown&&closing&&prefix==5&&attempt==0&&retirement is{CleanupConfirmed:true}&&io.CleanupConfirmed&&registry.CleanupConfirmed&&admission.CleanupConfirmed&&!gateHeld)
        {
            try
            {
                byte[] terminal=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=3,kind="KuaiYiPostCommitCompensationTerminal",operation=Operation,originalOperation=target.Operation,preparedSha256=preparedSha,planSha256=Hash(planBytes),ticketSha256=Hash(ticket!),failedLaunch=launch,retiredExecution=RetiredPath,executionSha256=Hash(execution!.RecordBytes),retainedBootstrapOwner=target.Bootstrap.Owner,retainedBundle=target.Bootstrap.CanonicalDirectory,registry=new{Chrome=Restored(before.Registry.Chrome),Edge=Restored(before.Registry.Edge)}});
                if(terminal.Length>16384)throw new InvalidDataException("Compensation terminal exceeds bound.");
                io.RequireAbsent(terminalPath);io.RequireAbsent(ticketBackup);io.RequireAbsent(archive);io.RequireExactMetadata(pending,ticket!,16384);
                io.WritePublicationMetadata(terminalPath,terminal,16384);io.RequireExactMetadata(terminalPath,terminal,16384);File.Replace(terminalPath,pending,ticketBackup,false);
                io.RequireExactMetadata(pending,terminal,16384);io.RequireExactMetadata(ticketBackup,ticket!,16384);
                if(!io.CleanupConfirmed||!registry.CleanupConfirmed)throw new IOException("Terminal original IO is unconfirmed.");
                faults?.Stage?.Invoke(InstallationCompensationStage.BeforeArchive);
                var result=new InstallationCompensationOutcome(InstallationCompensationStatus.Compensated,[],session.Completion.Result.Errors);
                session.ReleaseCompensatedRetention(this);
                if(faults?.Archive!=null)faults.Archive(pending,archive);else File.Move(pending,archive);
                // No new IO/Dispose/serial release after the unique successful archive Move.
                lock(Retained)Retained.Remove(this);return result;
            }
            catch(Exception error){errors.Add(error);}
        }
        // No replay, early abort archive, stale-owner adoption or mutation after an unknown observation.
        try{io.CloseOriginals();}catch(Exception error){errors.Add(error);}
        return new(InstallationCompensationStatus.Unconfirmed,errors.ToArray(),session.Completion.Result.Errors);
    }
    // Explicit same-owner close retry only. Never reads, rewrites, replays or upgrades the historical outcome.
    internal Task<IReadOnlyList<Exception>> SettleOriginalsAsync()
    {
        lock(settlementSync)
        {
            if(!Completion.IsCompleted)throw new InstallationBusyException();
            if(Completion.Result.Status==InstallationCompensationStatus.Compensated)return Task.FromResult<IReadOnlyList<Exception>>([]);
            if(settlement is{IsCompleted:false})return settlement;
            if(settlement is{IsCompletedSuccessfully:true}&&settlement.Result.Count==0)return settlement;
            var begin=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            settlement=Task.Run(async()=>
            {
                await begin.Task.ConfigureAwait(false);var errors=new List<Exception>();
                try{io.CloseOriginals();}catch(Exception error){errors.Add(error);}
                try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){errors.Add(error);}
                try{admission.Dispose();}catch(Exception error){errors.Add(error);}
                if(retirement is{MoveConfirmed:true,CleanupConfirmed:false})try{retirement.CloseLease(faults?.BeforeExecutionClose);}catch(Exception error){errors.Add(error);}
                return (IReadOnlyList<Exception>)errors.ToArray();
            });settlements.Add(settlement);begin.SetResult();return settlement;
        }
    }
}

public sealed partial class InstallationUpgradeCoordinator
{
    private InstallationDeploymentCompensation? compensationOwner;
    internal InstallationExecutionOwnership EnterCompensation(InstallationDeploymentCompensation original,InstallationDeploymentSession source)
    {
        source.RequireOriginalCompensation(original);
        if(!serial.Wait(0))throw new InstallationBusyException();original.OwnControllerGate(this);
        if(disposed||compensationOwner!=null||!ReferenceEquals(deploymentSession,source)||execution==null||roles.Count!=1||!DeploymentCleanupConfirmed(source)||!roles[0].LaunchFinished&&roles[0].LaunchProof!=null)throw new InstallationRecoveryException("Original failed execution is not available for compensation.");
        compensationOwner=original;return execution;
    }
    internal void RequireCompensationBinding(InstallationDeploymentCompensation original)
    {if(!ReferenceEquals(compensationOwner,original))throw new InstallationRecoveryException("Wrong original compensation controller.");}
    internal void SealCompensatedExecution(InstallationDeploymentCompensation original,InstallationDeploymentSession source,InstallationExecutionOwnership held)
    {
        RequireCompensationBinding(original);source.RequireOriginalCompensation(original);
        if(!ReferenceEquals(execution,held)||!ReferenceEquals(deploymentSession,source)||roles.Any(r=>!r.Finalized||!r.TasksCompleted))throw new InstallationRecoveryException("Original recovery execution cannot be sealed.");
        execution=null;disposed=true;
    }
    internal void ReleaseCompensationGate(InstallationDeploymentCompensation original)
    {original.OwnControllerGateForRelease(this);serial.Release();}
}
