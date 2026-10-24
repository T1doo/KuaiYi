using System.Diagnostics;
using System.Text.Json;
namespace KuaiYi.Core.Installation;

// Session health is a later operation, never another step in deployment finalization.
internal enum InstallationDeploymentSessionStatus { CleanExit,ActivationBlocked,RollbackRequired,Unconfirmed }
internal sealed record InstallationDeploymentReady(string Operation,string PreparedSha256,InstalledVersion Generation,Guid Launch,string Proof);
internal sealed record InstallationDeploymentSessionOutcome(InstallationDeploymentSessionStatus Status,InstallationDeploymentReady? Ready,IReadOnlyList<Exception> Errors);

internal sealed partial class InstallationDeploymentScope
{
    private InstallationDeploymentSession? healthSession;
    internal InstallationDeploymentSession BeginCommittedSession(InstallationUpgradeCoordinator controller,
        Func<InstallationControlledLaunch,CancellationToken,Task> publishReady,CancellationToken cancellation=default,InstallationPreparationFaults? readFaults=null,InstallationAdmissionFaults? admissionFaults=null)
    {
        ArgumentNullException.ThrowIfNull(controller);ArgumentNullException.ThrowIfNull(publishReady);
        lock(sync)
        {
            // Neither an archive on disk nor a receipt-uncertain return can reconstruct this private authority.
            if(healthSession!=null||finalization is not{IsCompletedSuccessfully:true}||finalization.Result.Status!=InstallationDeploymentStatus.Committed||
                qualifiedPrepared==null||snapshotOwner is not IInstallationDeploymentPublicationRegistry registry||!CleanupConfirmed)
                throw new InstallationRecoveryException("One original, fully committed deployment is required for session activation.");
            var value=new InstallationDeploymentSession(this,controller,root,userData,qualifiedPrepared,registry,
                new Dictionary<string,byte[]>{
                    [Path.Combine(root,"deployment-committed-"+Operation+".json")]=TerminalBytes(generationRetention),
                    [backup]=ticket.ToArray(),[beforePath]=before!.ToArray(),[preparedContext!.Path]=JsonSerializer.SerializeToUtf8Bytes(qualifiedPrepared.Plan)
                },publishReady,cancellation,readFaults,admissionFaults);
            healthSession=value;value.Start();return value;
        }
    }
    internal void RequireOriginalSession(InstallationDeploymentSession value)
    {lock(sync){startupSources?.Require(this);if(!ReferenceEquals(healthSession,value)||finalization is not{IsCompletedSuccessfully:true}||finalization.Result.Status!=InstallationDeploymentStatus.Committed)throw new InstallationRecoveryException("Not the original committed session authority.");}}
}

// Non-serializable, one-shot authority rooted in the original successful scope and exact coordinator.
// RollbackRequired is a retained obligation, not a completed rollback or a successful installation.
internal sealed partial class InstallationDeploymentSession
{
    private static readonly HashSet<InstallationDeploymentSession> Retained=[];
    private readonly object sync=new();
    private readonly InstallationDeploymentScope scope;
    private readonly InstallationUpgradeCoordinator controller;
    private readonly InstallationDeploymentPrepared prepared;
    private readonly IInstallationDeploymentPublicationRegistry registry;
    private readonly Dictionary<string,byte[]> records;
    private readonly Func<InstallationControlledLaunch,CancellationToken,Task> publishReady;
    private readonly CancellationToken cancellation;
    private readonly InstallationPreparationIO io;
    private readonly TaskCompletionSource start=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<InstallationDeploymentReady> ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private InstallationDeploymentReady? accepted;
    private Guid launch;
    private bool revoked,qualificationUnknown;
    internal string Root{get;}
    internal string UserData{get;}
    internal InstallationAdmissionFaults? AdmissionFaults{get;}
    internal InstallationStableSelection Selection{get;}
    internal Task<InstallationDeploymentReady> Ready=>ready.Task;
    internal Task<InstallationDeploymentSessionOutcome> Completion{get;}
    // Settled work excludes the deliberately live recovery execution lease.
    internal bool OriginalWorkPending=>!Completion.IsCompleted||controller.DeploymentOriginalWorkPending(this);
    internal bool SessionWorkSettled=>Completion.IsCompleted&&io.CleanupConfirmed&&registry.CleanupConfirmed&&controller.DeploymentCleanupConfirmed(this);
    internal bool OriginalFailedSessionRetired
    {get{lock(sync)return accepted==null&&Completion is{IsCompletedSuccessfully:true}&&Completion.Result.Status==InstallationDeploymentSessionStatus.RollbackRequired&&controller.DeploymentRetirementConfirmed(this);}}
    internal InstallationDeploymentSession(InstallationDeploymentScope scope,InstallationUpgradeCoordinator controller,string root,string data,
        InstallationDeploymentPrepared prepared,IInstallationDeploymentPublicationRegistry registry,Dictionary<string,byte[]> records,
        Func<InstallationControlledLaunch,CancellationToken,Task> publishReady,CancellationToken cancellation,InstallationPreparationFaults? readFaults,InstallationAdmissionFaults? admissionFaults)
    {
        this.scope=scope;this.controller=controller;Root=root;UserData=data;this.prepared=prepared;this.registry=registry;this.records=records;
        this.publishReady=publishReady;this.cancellation=cancellation;io=InstallationPreparationIO.ReadOnly(root,readFaults);AdmissionFaults=admissionFaults;
        var p=prepared.Plan;Selection=new(Guid.NewGuid(),root,data,p.Generation,p.TargetCurrent,new(p.Bootstrap.Owner,p.Bootstrap.Receipt));
        Completion=Task.Run(RunAsync);
    }
    internal void Start()=>start.SetResult();
    private async Task<InstallationDeploymentSessionOutcome> RunAsync()
    {
        await start.Task.ConfigureAwait(false);var errors=new List<Exception>();
        using var revoke=cancellation.Register(()=>{lock(sync)if(accepted==null)revoked=true;});
        try
        {
            scope.RequireOriginalSession(this);
            await controller.RunDeploymentSessionAsync(this,publishReady,cancellation).ConfigureAwait(false);
        }
        catch(Exception error){errors.Add(error);}
        // This original task owns every later read and close, independently of the completed deployment finalizer.
        try{io.CloseOriginals();}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        if(!io.CleanupConfirmed||!registry.CleanupConfirmed)qualificationUnknown=true;
        bool own=controller.DeploymentOwnsExecution(this),everOwned=controller.DeploymentClaimedExecution(this),settled=controller.DeploymentCleanupConfirmed(this);
        InstallationDeploymentReady? observed;lock(sync){revoked=true;observed=accepted;}
        var status=errors.Count==0&&observed!=null?InstallationDeploymentSessionStatus.CleanExit:
            qualificationUnknown||!settled?InstallationDeploymentSessionStatus.Unconfirmed:
            !everOwned?InstallationDeploymentSessionStatus.ActivationBlocked:
            observed==null&&controller.DeploymentRetirementConfirmed(this)?InstallationDeploymentSessionStatus.RollbackRequired:InstallationDeploymentSessionStatus.Unconfirmed;
        if(own||qualificationUnknown||!settled)lock(Retained)Retained.Add(this);
        if(observed==null){ready.TrySetException(errors.Count==1?errors[0]:new AggregateException(errors));_ =ready.Task.Exception;}
        return new(status,observed,errors.ToArray());
    }
    internal async Task BeforeStartAsync(InstallationUpgradeCoordinator actual,InstallationAdmission admission,InstalledVersion selected,Guid originalLaunch,CancellationToken stop)
    {
        RequireController(actual);admission.RequireRoot(Root);admission.Require(Root);
        if(actual.UserDataForController!=UserData||selected!=Selection.Version||originalLaunch==Guid.Empty)throw new InstallationRecoveryException("Committed deployment controller or generation differs.");
        lock(sync){if(launch!=Guid.Empty||revoked)throw new OperationCanceledException("Committed session was revoked.",stop);launch=originalLaunch;}
        stop.ThrowIfCancellationRequested();await VerifyCommittedAsync().ConfigureAwait(false);stop.ThrowIfCancellationRequested();
    }
    internal async Task AcceptReadyAsync(InstallationUpgradeCoordinator actual,InstallationControlledLaunch proof,Process original,CancellationToken stop)
    {
        RequireController(actual);
        if(proof.Launch!=launch||proof.Version!=Selection.Version||proof.Root!=Root||proof.UserData!=UserData||proof.UpgradeRecord!=null)throw new InstallationRecoveryException("READY is not from the original deployment session.");
        await VerifyCommittedAsync().ConfigureAwait(false);proof.RequireImageRecords();
        lock(sync)
        {
            stop.ThrowIfCancellationRequested();cancellation.ThrowIfCancellationRequested();
            if(revoked||accepted!=null||original.HasExited)throw new InstallationRecoveryException("Late, duplicate or exited session READY was refused.");
            accepted=new(scope.Operation,prepared.Sha256,proof.Version,proof.Launch,proof.Fingerprint);ready.SetResult(accepted);
        }
    }
    internal void RequireController(InstallationUpgradeCoordinator actual)
    {scope.RequireOriginalSession(this);if(!ReferenceEquals(actual,controller))throw new InstallationRecoveryException("Different deployment controller.");}
    private async Task VerifyCommittedAsync()
    {
        var errors=new List<Exception>();
        try
        {
            scope.RequireOriginalSession(this);if(registry.ConflictObserved)throw new InstallationRecoveryException("Original registry authority is foreign.");
            foreach(var pair in records)io.RequireExactMetadata(pair.Key,pair.Value,131072);
            var p=prepared.Plan;
            io.RequireAbsent(Path.Combine(Root,"upgrade-pending.json"));io.RequireAbsent(InstallationBootstrapDeployment.PendingPath(Root));io.RequireAbsent(Path.Combine(Root,"host-registration-pending.json"));
            io.RequireExactMetadata(Path.Combine(Root,"current.json"),Convert.FromBase64String(p.TargetCurrent),4096);
            io.RequireExactMetadata(InstallationBootstrapDeployment.OwnerPath(Root),Convert.FromBase64String(p.Bootstrap.Owner),4096);
            io.RequireExactMetadata(InstallationBootstrapDeployment.ActivePath(Root),Convert.FromBase64String(p.Bootstrap.Receipt),8192);
            io.RequireExactMetadata(Path.Combine(Root,"host-registration.json"),Convert.FromBase64String(p.Host.Receipt),8192);
            if(p.Startup is {} startup)
            {if(startup.TargetOwner==null)io.RequireAbsent(InstallationStartupRegistration.OwnerPath(Root));else io.RequireExactMetadata(InstallationStartupRegistration.OwnerPath(Root),Convert.FromBase64String(startup.TargetOwner),InstallationStartupRegistration.OwnerLimit);}
            var g=InstallationPackage.ReadInstalled(p.Generation.DirectoryPath,CancellationToken.None,expectedDigest:p.Generation.CatalogSha256,io:io);
            if(g.Schema!=3||g.SourceCatalogSha256!=p.GenerationSourceSha256||InstallationBootstrapPackage.Read(p.Bootstrap.CanonicalDirectory,p.BootstrapSourceSha256,p.Generation,CancellationToken.None,io).Revision!=p.BootstrapRevision)throw new InstallationRecoveryException("Committed payload or bootstrap differs.");
            var actual=await registry.ReadPublishedAsync().ConfigureAwait(false);actual=await InstallationStartupRegistration.CombineReadAsync(registry,actual,p.Startup!=null).ConfigureAwait(false);
            if(actual!=p.Host.Targets with{Startup=p.Startup?.Target})throw new InstallationRecoveryException("Committed Host or startup registration differs.");
        }
        catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        try{io.CloseOriginals();}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        try{await registry.CloseOriginalsAsync().ConfigureAwait(false);}catch(Exception error){qualificationUnknown=true;errors.Add(error);}
        InstallationErrors.Throw(errors);
    }
}

public sealed partial class InstallationUpgradeCoordinator
{
    internal Task<int> RunDeploymentSessionAsync(InstallationDeploymentSession value,Func<InstallationControlledLaunch,CancellationToken,Task> published,CancellationToken cancellation)
    {
        value.RequireController(this);
        if(installationRoot!=value.Root||UserData!=value.UserData)throw new InstallationRecoveryException("Deployment session roots differ from the original controller.");
        return RunSession(value.Selection.Version.DirectoryPath,value.Selection,published,cancellation,null,value);
    }
    internal bool DeploymentOwnsExecution(InstallationDeploymentSession original)=>ReferenceEquals(deploymentSession,original)&&execution!=null;
    internal bool DeploymentClaimedExecution(InstallationDeploymentSession original)=>ReferenceEquals(deploymentSession,original)&&roles.Count!=0;
    internal bool DeploymentOriginalWorkPending(InstallationDeploymentSession original)
        =>ReferenceEquals(deploymentSession,original)&&(roles.Any(r=>r.Startup is{IsCompleted:false}||!r.TasksCompleted||!r.Lifetime.StartupObservation.IsCompleted||!r.Lifetime.StartupSettlement.IsCompleted)||stablePublicationOriginal is{IsCompleted:false});
    internal bool DeploymentCleanupConfirmed(InstallationDeploymentSession original)
        =>!ReferenceEquals(deploymentSession,original)||(admission==null||admission.CleanupConfirmed)&&retainedBrowserSession==null&&roles.All(r=>r.Finalized&&r.Exited);
    internal bool DeploymentRetirementConfirmed(InstallationDeploymentSession original)
    {
        if(!DeploymentOwnsExecution(original)||roles.Count!=1||!DeploymentCleanupConfirmed(original))return false;
        try{execution!.Require(original.Root);return roles[0].LaunchFinished||roles[0].LaunchProof==null&&roles[0].Startup==null;}catch{return false;}
    }
}
