using System.Diagnostics;
namespace KuaiYi.Core.Installation;

internal enum InstallationRestoredSessionStatus { CleanExit,ActivationBlocked,Failed,Unconfirmed }
internal sealed record InstallationRestoredReady(string CompensationOperation,string DeploymentOperation,InstalledVersion Generation,Guid Launch,string Proof);
internal sealed record InstallationRestoredSessionOutcome(InstallationRestoredSessionStatus Status,InstallationRestoredReady? Ready,IReadOnlyList<Exception> Errors);

internal sealed partial class InstallationDeploymentCompensation
{
    private bool restorationAttempted;
    private InstallationRestoredSession? restoredSession;
    internal InstallationRestoredSession? BeginRestoredSession(InstallationUpgradeCoordinator next,
        Func<InstallationControlledLaunch,CancellationToken,Task> publishReady,CancellationToken cancellation=default,
        InstallationPreparationFaults? readFaults=null,InstallationAdmissionFaults? admissionFaults=null,InstallationExecutionFaults? executionFaults=null)
    {
        ArgumentNullException.ThrowIfNull(next);ArgumentNullException.ThrowIfNull(publishReady);
        lock(settlementSync)
        {
            if(restorationAttempted||Completion is not{IsCompletedSuccessfully:true}||Completion.Result.Status!=InstallationCompensationStatus.Compensated||!CleanupConfirmed||terminalBytes==null)
                throw new InstallationRecoveryException("One original, fully settled compensation is required for a new restored session.");
            next.RequireFreshRestoredController(root,data);
            if(ReferenceEquals(next,controller))throw new InstallationRecoveryException("The failed controller cannot be reused.");
            restorationAttempted=true;
            if(before.Current==null)return null; // No fabricated previous version, task, child or READY.
            var exact=history.ToDictionary(x=>x.Key,x=>x.Value.ToArray());
            foreach(var item in remnants)exact.Add(item.Key,item.Value.ToArray());
            exact.Add(archive,terminalBytes.ToArray());exact.Add(planPath,planBytes.ToArray());exact.Add(ticketBackup,ticket!.ToArray());exact.Add(RetiredPath,execution!.RecordBytes);
            var value=new InstallationRestoredSession(this,next,root,data,target,before,registry,exact,publishReady,cancellation,readFaults,admissionFaults,executionFaults);
            restoredSession=value;value.Start();return value;
        }
    }
    internal void RequireRestoredSession(InstallationRestoredSession original)
    {
        lock(settlementSync)
            if(!ReferenceEquals(restoredSession,original)||Completion is not{IsCompletedSuccessfully:true}||Completion.Result.Status!=InstallationCompensationStatus.Compensated||
                !closing||unknown||gateHeld||!io.CleanupConfirmed||!admission.CleanupConfirmed||retirement is not{CleanupConfirmed:true})
                throw new InstallationRecoveryException("Not the original settled compensation's restored-session authority.");
    }
}

// A separate health operation. No recovered marker, archive, old Prepared or failed launch can construct its authority.
internal sealed class InstallationRestoredSession
{
    private static readonly HashSet<InstallationRestoredSession> Retained=[];
    private readonly object sync=new();
    private readonly InstallationDeploymentCompensation compensation;
    private readonly InstallationUpgradeCoordinator controller;
    private readonly InstallationDeploymentPlan target;
    private readonly InstallationDeploymentBefore before;
    private readonly IInstallationDeploymentPublicationRegistry registry;
    private readonly Dictionary<string,byte[]> records;
    private readonly Func<InstallationControlledLaunch,CancellationToken,Task> publishReady;
    private readonly CancellationToken cancellation;
    private readonly InstallationPreparationIO io;
    private InstallationPreparationIO? launchIO;
    private readonly TaskCompletionSource start=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<InstallationRestoredReady> ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private InstallationRestoredReady? accepted;
    private InstallationStableSelection? selection;
    private Guid launch;
    private bool revoked,qualificationUnknown;
    internal string Root{get;}
    internal string UserData{get;}
    internal InstallationAdmissionFaults? AdmissionFaults{get;}
    internal InstallationExecutionFaults? ExecutionFaults{get;}
    internal InstallationPreparationIO ExecutionIO=>io;
    internal InstallationPreparationIO LaunchIO=>launchIO??throw new InstallationRecoveryException("Original launch IO is not registered.");
    internal InstallationStableSelection Selection=>selection??throw new InstallationRecoveryException("Restored generation is not qualified.");
    internal Task<InstallationRestoredReady> Ready=>ready.Task;
    internal Task<InstallationRestoredSessionOutcome> Completion{get;}
    internal bool OriginalWorkPending=>!Completion.IsCompleted||controller.RestoredOriginalWorkPending(this);
    internal bool SessionWorkSettled=>Completion.IsCompleted&&io.CleanupConfirmed&&registry.CleanupConfirmed&&controller.RestoredCleanupConfirmed(this);
    internal InstallationRestoredSession(InstallationDeploymentCompensation compensation,InstallationUpgradeCoordinator controller,string root,string data,
        InstallationDeploymentPlan target,InstallationDeploymentBefore before,IInstallationDeploymentPublicationRegistry registry,Dictionary<string,byte[]> records,
        Func<InstallationControlledLaunch,CancellationToken,Task> publishReady,CancellationToken cancellation,InstallationPreparationFaults? readFaults,
        InstallationAdmissionFaults? admissionFaults,InstallationExecutionFaults? executionFaults)
    {
        this.compensation=compensation;this.controller=controller;Root=root;UserData=data;this.target=target;this.before=before;this.registry=registry;this.records=records;
        this.publishReady=publishReady;this.cancellation=cancellation;AdmissionFaults=admissionFaults;ExecutionFaults=executionFaults;
        io=InstallationPreparationIO.ReadOnly(root,readFaults).CreatePublicationChannel(root,[InstallationExecutionOwnership.RecordPath(root)],readFaults);
        Completion=Task.Run(RunAsync);
    }
    internal void Start()=>start.SetResult();
    private async Task<InstallationRestoredSessionOutcome> RunAsync()
    {
        await start.Task.ConfigureAwait(false);var errors=new List<Exception>();
        using var revoke=cancellation.Register(()=>{lock(sync)if(accepted==null)revoked=true;});
        try
        {
            RequireController(controller);cancellation.ThrowIfCancellationRequested();
            var old=await VerifyRestoredAsync().ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
            selection=new(Guid.NewGuid(),Root,UserData,old,before.Current!,before.BootstrapActive==null?null:new(target.Bootstrap.Owner,before.BootstrapActive));
            await controller.RunRestoredSessionAsync(this,publishReady,cancellation).ConfigureAwait(false);
        }
        catch(Exception error){errors.Add(error);}
        try{io.CloseOriginals();}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        if(io.StorageUnconfirmed||!io.CleanupConfirmed||!registry.CleanupConfirmed)qualificationUnknown=true;
        bool owns=controller.RestoredOwnsExecution(this),settled=controller.RestoredCleanupConfirmed(this);
        InstallationRestoredReady? observed;lock(sync){revoked=true;observed=accepted;}
        var status=errors.Count==0&&observed!=null?InstallationRestoredSessionStatus.CleanExit:
            qualificationUnknown||io.StorageUnconfirmed||!settled||controller.RestoredAcquisitionUnknown(this)?InstallationRestoredSessionStatus.Unconfirmed:
            !owns?InstallationRestoredSessionStatus.ActivationBlocked:InstallationRestoredSessionStatus.Failed;
        if(owns||qualificationUnknown||!settled)lock(Retained)Retained.Add(this);
        if(observed==null){ready.TrySetException(errors.Count==1?errors[0]:new AggregateException(errors));_=ready.Task.Exception;}
        return new(status,observed,errors.ToArray());
    }
    internal void RequireController(InstallationUpgradeCoordinator actual)
    {compensation.RequireRestoredSession(this);if(!ReferenceEquals(actual,controller))throw new InstallationRecoveryException("Different restored-session controller.");}
    internal async Task BeforeStartAsync(InstallationUpgradeCoordinator actual,InstallationAdmission admission,InstalledVersion selected,Guid originalLaunch,CancellationToken stop)
    {
        RequireController(actual);admission.RequireRoot(Root);admission.Require(Root);
        if(actual.UserDataForController!=UserData||selected!=Selection.Version||originalLaunch==Guid.Empty)throw new InstallationRecoveryException("Restored controller or generation differs.");
        lock(sync){if(launch!=Guid.Empty||revoked)throw new OperationCanceledException("Restored session was revoked.",stop);launch=originalLaunch;}
        launchIO=io.CreateSessionChannel(Root,new[]{"prepared","accepted","finished"}.Select(phase=>Path.Combine(Root,$"launch-{launch:N}-{phase}.json")).Concat(new[]{Path.Combine(Root,$"browser-host-prepared-{launch:N}.json"),InstallationBrowserHostSession.PathFor(Root),Path.Combine(Root,$"browser-host-closing-{launch:N}.json"),Path.Combine(Root,$"browser-host-finished-{launch:N}.json")}));
        stop.ThrowIfCancellationRequested();await VerifyRestoredAsync().ConfigureAwait(false);stop.ThrowIfCancellationRequested();
    }
    internal async Task AcceptReadyAsync(InstallationUpgradeCoordinator actual,InstallationControlledLaunch proof,Process original,CancellationToken stop)
    {
        RequireController(actual);
        if(proof.Launch!=launch||proof.Version!=Selection.Version||proof.Root!=Root||proof.UserData!=UserData||proof.UpgradeRecord!=null)throw new InstallationRecoveryException("READY is not from the new original restored session.");
        await VerifyRestoredAsync().ConfigureAwait(false);proof.RequireImageRecords(LaunchIO);
        lock(sync)
        {
            stop.ThrowIfCancellationRequested();cancellation.ThrowIfCancellationRequested();
            if(revoked||accepted!=null||original.HasExited)throw new InstallationRecoveryException("Late, duplicate or exited restored READY refused.");
            accepted=new(compensation.Operation,target.Operation,proof.Version,proof.Launch,proof.Fingerprint);ready.SetResult(accepted);
        }
    }
    private InstalledVersion VerifyFiles()
    {
        InstalledVersion? old;
        compensation.RequireRestoredSession(this);
        if(qualificationUnknown||registry.ConflictObserved)throw new InstallationRecoveryException("Original restored-session qualification is unknown.");
        foreach(var pair in records)io.RequireExactMetadata(pair.Key,pair.Value,131072);
        foreach(var name in new[]{"upgrade-pending.json","bootstrap-deployment-pending.json","host-registration-pending.json","handoff-pending.json"})io.RequireAbsent(Path.Combine(Root,name));
        io.RequireExactMetadata(Path.Combine(Root,"current.json"),Convert.FromBase64String(before.Current!),4096);
        io.RequireExactMetadata(InstallationBootstrapDeployment.OwnerPath(Root),Convert.FromBase64String(target.Bootstrap.Owner),4096);io.RequireDirectory(target.Bootstrap.CanonicalDirectory);
        Optional(InstallationBootstrapDeployment.ActivePath(Root),before.BootstrapActive,8192);Optional(Path.Combine(Root,"host-registration.json"),before.HostReceipt,8192);
        var(next,previous)=InstallationStore.PlanCurrent(Root,Convert.FromBase64String(before.Current!),target.Generation,CancellationToken.None,io);old=previous??throw new InstallationRecoveryException("No previous generation.");
        if(Convert.ToBase64String(next)!=target.TargetCurrent||selection!=null&&selection.Version!=old)throw new InstallationRecoveryException("Restored current lineage changed.");
        if(before.BootstrapActive!=null)
        {
            var owner=InstallationBootstrapDeployment.Decode<InstallationBootstrapDeployment.StoreOwner>(Convert.FromBase64String(target.Bootstrap.Owner));
            if(InstallationBootstrapDeployment.RequireReceipt(Root,UserData,owner,Convert.FromBase64String(before.BootstrapActive),CancellationToken.None,io).Generation!=old)throw new InstallationRecoveryException("Restored B/Core lineage changed.");
        }
        if(target.Host.Before is{} host&&(host.Version!=old||host.Manifest!=Path.Combine(old.DirectoryPath,InstallationPayload.HostManifestName)))throw new InstallationRecoveryException("Restored Host lineage changed.");
        return old;
    }
    private async Task<InstalledVersion> VerifyRestoredAsync()
    {
        var errors=new List<Exception>();InstalledVersion? old=null;
        try
        {
            old=VerifyFiles();
            InstallationDeploymentRegistrationSlot Restored(InstallationDeploymentRegistrationSlot slot)=>slot.KeyExists?slot:new(true,null);
            var expected=new InstallationDeploymentRegistrationSnapshot(Restored(before.Registry.Chrome),Restored(before.Registry.Edge));
            if(await registry.ReadPublishedAsync().ConfigureAwait(false)!=expected)throw new InstallationRecoveryException("Restored Host defaults changed.");
        }
        catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        if(!registry.CleanupConfirmed){qualificationUnknown=true;errors.Add(new IOException("Original restored registry close is unconfirmed."));}
        if(errors.Count==0)try{old=VerifyFiles();}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        // No more registry await/close after this final complete file closure. Every fresh read uses the same ledger.
        try{io.CloseOriginals();}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        if(!io.CleanupConfirmed)qualificationUnknown=true;
        InstallationErrors.Throw(errors);return old!;
    }
    private void Optional(string path,string? expected,int maximum)
    {if(expected==null)io.RequireAbsent(path);else io.RequireExactMetadata(path,Convert.FromBase64String(expected),maximum);}
}

public sealed partial class InstallationUpgradeCoordinator
{
    internal void RequireFreshRestoredController(string root,string data)
    {
        if(installationRoot!=root||UserData!=data||disposed||sessionAttempted||execution!=null||admission!=null||roles.Count!=0||pending!=null||handoff!=null)
            throw new InstallationRecoveryException("A fresh controller for the exact restored roots is required.");
    }
    internal Task<int> RunRestoredSessionAsync(InstallationRestoredSession original,Func<InstallationControlledLaunch,CancellationToken,Task> published,CancellationToken cancellation)
    {original.RequireController(this);return RunSession(original.Selection.Version.DirectoryPath,original.Selection,published,cancellation,null,restored:original);}
    internal bool RestoredOwnsExecution(InstallationRestoredSession original)=>ReferenceEquals(restoredSession,original)&&execution!=null;
    internal bool RestoredAcquisitionUnknown(InstallationRestoredSession original)=>ReferenceEquals(restoredSession,original)&&execution is{Acquired:false}||ReferenceEquals(restoredSession,original)&&execution is{LeaseCloseUnconfirmed:true};
    internal bool RestoredOriginalWorkPending(InstallationRestoredSession original)
        =>ReferenceEquals(restoredSession,original)&&(roles.Any(r=>r.Startup is{IsCompleted:false}||!r.TasksCompleted||!r.Lifetime.StartupObservation.IsCompleted||!r.Lifetime.StartupSettlement.IsCompleted)||stablePublicationOriginal is{IsCompleted:false});
    internal bool RestoredCleanupConfirmed(InstallationRestoredSession original)
        =>!ReferenceEquals(restoredSession,original)||(admission==null||admission.CleanupConfirmed)&&retainedBrowserSession==null&&roles.All(r=>r.Finalized&&r.Exited);
}
