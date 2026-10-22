namespace KuaiYi.Core.Installation;

internal enum InstallationOperationPhase { Preparing,Publishing,Committed,NewSession,Compensating,Restored,PreviousSession,ResultPublished }
internal enum InstallationOperationStatus { NotCommitted,CommittedNotReady,NewVersionReady,RestoredNoPrevious,RestoredPreviousNotReady,PreviousVersionReady,Unconfirmed }
internal enum InstallationOperationSession { New,Previous }
internal sealed record InstallationOperationOutcome(InstallationOperationStatus Status,InstallationOperationPhase StoppedAt,string? DeploymentOperation,
    InstallationDeploymentOutcome? Deployment,InstallationDeploymentReady? NewReady,InstallationCompensationOutcome? Compensation,InstallationRestoredReady? PreviousReady,IReadOnlyList<Exception> Errors);
internal sealed record InstallationOperationSettlement(InstallationOperationOutcome Result,InstallationDeploymentSessionOutcome? NewSession,
    InstallationRestoredSessionOutcome? PreviousSession,bool CleanupConfirmed,IReadOnlyList<Exception> Errors);
internal sealed class InstallationOperationFaults
{
    internal InstallationDeploymentFaults? Deployment {get;init;}
    internal InstallationPreparationFaults? Generation {get;init;}
    internal InstallationPublicationFaults? Publication {get;init;}
    internal InstallationCompensationFaults? Compensation {get;init;}
    internal Action<InstallationOperationPhase>? Phase {get;init;}
    internal InstallationPreparationFaults? Initialization {get;init;}
}

// One in-memory installer operation. Existing-only foundation; no Windows installer/registry entry or persisted resume.
internal sealed partial class InstallationOperation
{
    private static readonly HashSet<InstallationOperation> Retained=[];
    private readonly object sync=new();
    private readonly string root,data,generationSource,generationSha,bootstrapSource,bootstrapSha;
    private readonly IInstallationDeploymentPublicationRegistry registry;
    private readonly Func<InstallationOperationSession,Task<InstallationUpgradeCoordinator>> factory;
    private readonly Func<InstallationControlledLaunch,CancellationToken,Task> publishReady;
    private readonly CancellationToken cancellation;
    private readonly bool initializeMissingRoot;
    private readonly InstallationOperationFaults? faults;
    private readonly TaskCompletionSource begin=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<InstallationOperationOutcome> result=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<Task> controllerTasks=[];
    private readonly List<Exception> errors=[];
    private readonly HashSet<Exception> known=new(ReferenceEqualityComparer.Instance);
    private bool started,captureStarted,controllerUnknown;
    private InstallationOperationPhase phase;
    private InstallationDeploymentOutcome? deployment;
    private InstallationDeploymentBefore? before;
    private InstallationDeploymentPrepared? prepared;
    private InstallationDeploymentSessionOutcome? newOutcome;
    private InstallationRestoredSessionOutcome? previousOutcome;
    private InstallationDeploymentReady? newReady;
    private InstallationRestoredReady? previousReady;
    private InstallationCompensationOutcome? compensationOutcome;
    internal InstallationDeploymentScope? Scope {get;private set;}
    internal InstallationUpgradeCoordinator? NewController {get;private set;}
    internal InstallationUpgradeCoordinator? PreviousController {get;private set;}
    internal InstallationDeploymentSession? NewSession {get;private set;}
    internal InstallationDeploymentCompensation? Compensation {get;private set;}
    internal InstallationRestoredSession? PreviousSession {get;private set;}
    internal Task<InstallationOperationOutcome> Completion=>result.Task;
    internal Task<InstallationOperationSettlement> OriginalWork {get;}
    internal InstallationOperationPhase Phase{get{lock(sync)return phase;}}
    internal bool OriginalWorkPending=>!OriginalWork.IsCompleted||NewSession?.OriginalWorkPending==true||PreviousSession?.OriginalWorkPending==true||
        Scope is{Finalization.IsCompleted:false}||Compensation is{Completion.IsCompleted:false}||ControllerTasksPending();
    internal bool CleanupConfirmed=>OriginalWork.IsCompleted&&!OriginalWorkPending&&ResourcesConfirmed();
    internal InstallationOperation(string root,string data,string generationSource,string generationSha,string bootstrapSource,string bootstrapSha,
        IInstallationDeploymentPublicationRegistry registry,Func<InstallationOperationSession,Task<InstallationUpgradeCoordinator>> factory,
        Func<InstallationControlledLaunch,CancellationToken,Task> publishReady,CancellationToken cancellation=default,InstallationOperationFaults? faults=null,bool initializeMissingRoot=false)
    {
        this.root=InstallationPaths.Root(root);this.data=InstallationPaths.Root(data);this.generationSource=InstallationPaths.Root(generationSource);this.bootstrapSource=InstallationPaths.Root(bootstrapSource);
        if(!InstallationPackage.HashValid(generationSha)||!InstallationPackage.HashValid(bootstrapSha))throw new InvalidDataException("Both original source pins are required.");
        _=new InstallationStore(this.root,this.data,()=>false);this.generationSha=generationSha;this.bootstrapSha=bootstrapSha;
        this.registry=registry??throw new ArgumentNullException(nameof(registry));this.factory=factory??throw new ArgumentNullException(nameof(factory));
        this.initializeMissingRoot=initializeMissingRoot;this.publishReady=publishReady??throw new ArgumentNullException(nameof(publishReady));this.cancellation=cancellation;this.faults=faults;
        OriginalWork=Task.Run(RunAsync);
    }
    internal Task<InstallationOperationOutcome> Start()
    {
        lock(sync){if(started)throw new InvalidOperationException("One start per original installation operation.");started=true;lock(Retained)Retained.Add(this);begin.SetResult();return Completion;}
    }
    private void SetPhase(InstallationOperationPhase value)
    {lock(sync)phase=value;faults?.Phase?.Invoke(value);}
    private void Remember(Exception error)
    {
        if(error is AggregateException many){foreach(var item in many.Flatten().InnerExceptions)Remember(item);}
        else if(known.Add(error))errors.Add(error);
    }
    private void Remember(IEnumerable<Exception> values){foreach(var error in values)Remember(error);}
    private bool ControllerTasksPending(){lock(sync)return controllerTasks.Any(t=>!t.IsCompleted);}
    // The task is registered before even the synchronous part of a factory/Dispose callback can run.
    private Task<T> OwnControllerTask<T>(Func<Task<T>> action)
    {
        var gate=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var original=Task.Run(async()=>{await gate.Task.ConfigureAwait(false);return await action().ConfigureAwait(false);});
        lock(sync)controllerTasks.Add(original);gate.SetResult();return original;
    }
    private async Task<InstallationUpgradeCoordinator> CreateController(InstallationOperationSession kind)
    {
        InstallationUpgradeCoordinator value;
        try{value=await OwnControllerTask(()=>factory(kind)).ConfigureAwait(false)??throw new InstallationRecoveryException("Controller factory returned no original owner.");}
        catch{controllerUnknown=true;throw;}
        if(kind==InstallationOperationSession.New)NewController=value;else PreviousController=value;
        try{value.RequireFreshRestoredController(root,data);}catch{controllerUnknown=true;throw;}return value;
    }
    private async Task<bool> StopBeforeSession(InstallationUpgradeCoordinator value)
    {
        if(!cancellation.IsCancellationRequested)return false;
        try{await OwnControllerTask(()=>{value.DisposeUnusedOperationController(root,data);return Task.FromResult(true);}).ConfigureAwait(false);}catch{controllerUnknown=true;throw;}return true;
    }
    private async Task Deploy()
    {
        try
        {
            SetPhase(InstallationOperationPhase.Preparing);Scope=new(root,data,generationSha,bootstrapSha,cancellation,faults:faults?.Deployment,initializeMissingRoot:initializeMissingRoot,initializationFaults:faults?.Initialization);
            await Scope.AcquireAsync().ConfigureAwait(false);
            var capture=Scope.CaptureBeforeAsync(registry);captureStarted=true;before=await capture.ConfigureAwait(false);
            var generation=await Scope.PrepareGenerationAsync(generationSource,faults?.Generation).ConfigureAwait(false);
            var bootstrap=await Scope.PrepareBootstrapAsync(bootstrapSource,generation).ConfigureAwait(false);
            prepared=await Scope.SealPreparedAsync(generation,bootstrap).ConfigureAwait(false);
            SetPhase(InstallationOperationPhase.Publishing);deployment=await Scope.CommitPreparedAsync(prepared,faults?.Publication).ConfigureAwait(false);
        }
        catch(Exception error)
        {
            Remember(error);
            if(Scope!=null)
            {
                try
                {
                    if(!Scope.Finalization.IsCompleted)
                    {
                        if(prepared!=null)await Scope.AbortAfterPreparedAsync().ConfigureAwait(false);
                        else if(captureStarted)await Scope.AbortBeforePreparedAsync().ConfigureAwait(false);
                        else await Scope.AbortBeforeSnapshotAsync().ConfigureAwait(false);
                    }
                    deployment=await Scope.Finalization.ConfigureAwait(false);
                }
                catch(Exception settlement){Remember(settlement);}
            }
        }
        if(deployment!=null)Remember(deployment.Errors);
    }
    private InstallationOperationOutcome Publish(InstallationOperationStatus status)
    {
        var value=new InstallationOperationOutcome(status,Phase,Scope?.Operation,deployment,newReady,compensationOutcome,previousReady,errors.ToArray());
        lock(sync){if(result.Task.IsCompleted)throw new InvalidOperationException("Original operation already published its result.");phase=InstallationOperationPhase.ResultPublished;result.SetResult(value);}return value;
    }
    private async Task<InstallationOperationOutcome> ObserveHealth()
    {
        SetPhase(InstallationOperationPhase.Committed);
        if(cancellation.IsCancellationRequested)return Publish(InstallationOperationStatus.CommittedNotReady);
        var controller=await CreateController(InstallationOperationSession.New).ConfigureAwait(false);
        if(await StopBeforeSession(controller).ConfigureAwait(false))return Publish(InstallationOperationStatus.CommittedNotReady);
        SetPhase(InstallationOperationPhase.NewSession);NewSession=Scope!.BeginCommittedSession(controller,publishReady,cancellation);
        await Task.WhenAny(NewSession.Ready,NewSession.Completion).ConfigureAwait(false);
        if(NewSession.Ready.IsCompletedSuccessfully)
        {
            newReady=await NewSession.Ready.ConfigureAwait(false);var value=Publish(InstallationOperationStatus.NewVersionReady);
            newOutcome=await NewSession.Completion.ConfigureAwait(false);Remember(newOutcome.Errors);return value;
        }
        newOutcome=await NewSession.Completion.ConfigureAwait(false);Remember(newOutcome.Errors);
        // Completion can race the waiter; canonical success is the authority, never the WhenAny task's identity.
        if(NewSession.Ready.IsCompletedSuccessfully){newReady=await NewSession.Ready.ConfigureAwait(false);return Publish(InstallationOperationStatus.NewVersionReady);}
        if(newOutcome.Status==InstallationDeploymentSessionStatus.ActivationBlocked)return Publish(InstallationOperationStatus.CommittedNotReady);
        if(newOutcome.Status!=InstallationDeploymentSessionStatus.RollbackRequired||NewSession.OriginalWorkPending||!NewSession.OriginalFailedSessionRetired)
            return Publish(InstallationOperationStatus.Unconfirmed);
        SetPhase(InstallationOperationPhase.Compensating);
        Compensation=NewSession.BeginCompensation(CancellationToken.None,faults?.Compensation);compensationOutcome=await Compensation.Completion.ConfigureAwait(false);
        Remember(compensationOutcome.Errors);Remember(compensationOutcome.OriginalSessionErrors);
        if(compensationOutcome.Status!=InstallationCompensationStatus.Compensated||!Compensation.CleanupConfirmed)return Publish(InstallationOperationStatus.Unconfirmed);
        SetPhase(InstallationOperationPhase.Restored);
        if(before!.Current==null)return Publish(InstallationOperationStatus.RestoredNoPrevious);
        if(cancellation.IsCancellationRequested)return Publish(InstallationOperationStatus.RestoredPreviousNotReady);
        var previous=await CreateController(InstallationOperationSession.Previous).ConfigureAwait(false);
        if(await StopBeforeSession(previous).ConfigureAwait(false))return Publish(InstallationOperationStatus.RestoredPreviousNotReady);
        SetPhase(InstallationOperationPhase.PreviousSession);
        PreviousSession=Compensation.BeginRestoredSession(previous,publishReady,cancellation)??throw new InstallationRecoveryException("Original previous generation disappeared.");
        await Task.WhenAny(PreviousSession.Ready,PreviousSession.Completion).ConfigureAwait(false);
        if(PreviousSession.Ready.IsCompletedSuccessfully)
        {
            previousReady=await PreviousSession.Ready.ConfigureAwait(false);var value=Publish(InstallationOperationStatus.PreviousVersionReady);
            previousOutcome=await PreviousSession.Completion.ConfigureAwait(false);Remember(previousOutcome.Errors);return value;
        }
        previousOutcome=await PreviousSession.Completion.ConfigureAwait(false);Remember(previousOutcome.Errors);
        if(PreviousSession.Ready.IsCompletedSuccessfully){previousReady=await PreviousSession.Ready.ConfigureAwait(false);return Publish(InstallationOperationStatus.PreviousVersionReady);}
        return Publish(previousOutcome.Status==InstallationRestoredSessionStatus.Unconfirmed?InstallationOperationStatus.Unconfirmed:InstallationOperationStatus.RestoredPreviousNotReady);
    }
    private bool ResourcesConfirmed()=>!controllerUnknown&&!ControllerTasksPending()&&registry.CleanupConfirmed&&(Scope==null||Scope.CleanupConfirmed)&&
        (NewSession==null||NewSession.SessionWorkSettled&&NewController?.DeploymentOwnsExecution(NewSession)!=true)&&
        (Compensation==null||Compensation.CleanupConfirmed&&!Compensation.OriginalExecutionLeaseRetained)&&
        (PreviousSession==null||PreviousSession.SessionWorkSettled&&PreviousController?.RestoredOwnsExecution(PreviousSession)!=true);
    private async Task<InstallationOperationSettlement> RunAsync()
    {
        await begin.Task.ConfigureAwait(false);InstallationOperationOutcome? value=null;
        try
        {
            await Deploy().ConfigureAwait(false);
            if(deployment?.Status!=InstallationDeploymentStatus.Committed)
                value=Publish((deployment?.Status is InstallationDeploymentStatus.Refused or InstallationDeploymentStatus.AbortedBeforeSnapshot or InstallationDeploymentStatus.AbortedBeforePrepared or InstallationDeploymentStatus.AbortedAfterPrepared)&&ResourcesConfirmed()?InstallationOperationStatus.NotCommitted:InstallationOperationStatus.Unconfirmed);
            else value=await ObserveHealth().ConfigureAwait(false);
        }
        catch(Exception error){Remember(error);if(result.Task.IsCompletedSuccessfully)value=result.Task.Result;else value=Publish(InstallationOperationStatus.Unconfirmed);}
        bool clean=ResourcesConfirmed();if(clean&&value!.Status!=InstallationOperationStatus.Unconfirmed)lock(Retained)Retained.Remove(this);
        return new(value!,newOutcome,previousOutcome,clean,errors.ToArray());
    }
}

public sealed partial class InstallationUpgradeCoordinator
{
    // Only an unused controller can be closed here. Never finalize another operation's roles or execution.
    internal void DisposeUnusedOperationController(string root,string data)
    {
        if(!serial.Wait(0))throw new InstallationBusyException();
        try{RequireFreshRestoredController(root,data);disposed=true;}finally{serial.Release();}
    }
}
