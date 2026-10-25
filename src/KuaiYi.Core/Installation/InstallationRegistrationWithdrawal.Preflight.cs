namespace KuaiYi.Core.Installation;

internal enum InstallationMaintenanceIntent { Uninstall,Repair }
internal enum InstallationMaintenanceStatus { ObservedReadOnly,Refused,Cancelled,Unconfirmed }
internal enum InstallationMaintenanceAction { WithdrawSevenReferences,StopOwnedProcesses,RemovePayloadAndTemporaryFiles,RemoveShellRegistration,RemoveIdentityPackage,RemoveSettingsAndKeys,RestorePayload,PublishRegistrations,RepairShellRegistration,RepairIdentityPackage }
internal enum InstallationMaintenanceAuthorization { NotAuthorized }
internal sealed record InstallationMaintenanceStep(InstallationMaintenanceAction Action,InstallationMaintenanceAuthorization Authorization);
// Historical observations only: neither a receipt nor this result is an executable capability.
internal sealed record InstallationMaintenanceObservation(InstalledVersion Generation,Guid Bundle,string BootstrapRevision,string GenerationSourceSha256,string BootstrapSourceSha256);
internal sealed record InstallationMaintenanceOutcome(string Operation,InstallationMaintenanceIntent Intent,InstallationMaintenanceStatus Status,
    InstallationMaintenanceObservation? Observation,bool CleanupConfirmed,IReadOnlyList<InstallationMaintenanceStep> RemainingSteps,IReadOnlyList<Exception> Errors);

internal sealed partial class InstallationRegistrationWithdrawal
{
    private readonly InstallationMaintenanceIntent? inspection;
    private readonly Task<InstallationMaintenanceOutcome>? preflightWork;
    private InstallationMaintenanceObservation? observation;
    internal static InstallationRegistrationWithdrawal ForMaintenancePreflight(ReadOnlyMemory<byte> originalEmbeddedPins,string root,string data,
        IInstallationStartupRegistry registry,InstallationMaintenanceIntent intent,CancellationToken cancellation=default,InstallationWithdrawalFaults? faults=null)
    {
        if(intent is not (InstallationMaintenanceIntent.Uninstall or InstallationMaintenanceIntent.Repair))throw new ArgumentOutOfRangeException(nameof(intent));
        return new(originalEmbeddedPins,root,data,registry,cancellation,faults,intent);
    }
    internal Task<InstallationMaintenanceOutcome> StartPreflight()
    {
        lock(sync)
        {
            if(inspection==null)throw new InvalidOperationException("Withdrawal cannot be reinterpreted as a read-only preflight.");
            StartOriginal();return preflightWork!;
        }
    }
    private async Task<InstallationMaintenanceOutcome> ReportPreflightAsync()
    {
        var value=await OriginalWork.ConfigureAwait(false);
        var status=value.Status switch
        {
            InstallationWithdrawalStatus.PreflightObservedReadOnly=>InstallationMaintenanceStatus.ObservedReadOnly,
            InstallationWithdrawalStatus.PreflightCancelled=>InstallationMaintenanceStatus.Cancelled,
            InstallationWithdrawalStatus.Unconfirmed=>InstallationMaintenanceStatus.Unconfirmed,
            _=>InstallationMaintenanceStatus.Refused
        };
        InstallationMaintenanceAction[] steps=inspection==InstallationMaintenanceIntent.Uninstall
            ?[InstallationMaintenanceAction.WithdrawSevenReferences,InstallationMaintenanceAction.StopOwnedProcesses,InstallationMaintenanceAction.RemovePayloadAndTemporaryFiles,
                InstallationMaintenanceAction.RemoveShellRegistration,InstallationMaintenanceAction.RemoveIdentityPackage,InstallationMaintenanceAction.RemoveSettingsAndKeys]
            :[InstallationMaintenanceAction.StopOwnedProcesses,InstallationMaintenanceAction.RestorePayload,InstallationMaintenanceAction.PublishRegistrations,
                InstallationMaintenanceAction.RepairShellRegistration,InstallationMaintenanceAction.RepairIdentityPackage];
        return new(Operation,inspection!.Value,status,status==InstallationMaintenanceStatus.ObservedReadOnly?observation:null,value.CleanupConfirmed,
            Array.AsReadOnly(steps.Select(action=>new InstallationMaintenanceStep(action,InstallationMaintenanceAuthorization.NotAuthorized)).ToArray()),value.Errors);
    }
    private async Task<InstallationWithdrawalOutcome> RunPreflightAsync()
    {
        List<Exception> errors=[];bool observed=false,cancelled=false;
        try
        {
            pins=InstallationInstallerPins.Read(embedded);CheckCancellation();
            // The original existing-only admission provides a quiescent observation, never a publication channel.
            store.AcquireAdmission(admission,mutation:true,existing:true,requireStopped:true,keepSetup:true);
            io.RequireExactMetadata(Path.Combine(root,"owner.txt"),InstallationStore.InitialOwnerBytes,4096);
            InstallationPaths.Check(data);if(!Directory.Exists(data))throw new InstallationRecoveryException("Original user data directory absent.");
            var before=ReadBindings(await ReadSlots(initial:true).ConfigureAwait(false));CheckCancellation();
            var selected=Qualify(before);CheckCancellation();
            // No absent-current history, disk pin or matching path is promoted into a repair/deletion source.
            if(ReadBindings(await ReadSlots(preparation:true).ConfigureAwait(false))!=before)throw new InstallationRecoveryException("Read-only maintenance observations changed.");
            CheckCancellation();faults?.BeforeClosing?.Invoke();
            lock(sync){CheckCancellation();closing=true;}
            observation=new(selected.Selected,selected.Bundle,selected.BootstrapRevision,pins.Generation,pins.Bootstrap);observed=true;
        }
        catch(Exception error)
        {
            if(!io.CleanupConfirmed||!RegistryCleanupConfirmed||io.StorageUnconfirmed||(registryStarted&&registry.ConflictObserved))unknown=true;
            lock(sync)cancelled=error is OperationCanceledException&&(abort||cancellation.IsCancellationRequested);
            errors.Add(error);
        }
        lock(sync)closing=true;
        try{faults?.AfterClosing?.Invoke();}catch(Exception error){unknown=true;errors.Add(error);}
        // Reuse the same original IO/registry/admission settlement. UNKNOWN never becomes a replay instruction.
        await CloseOriginals(errors).ConfigureAwait(false);
        if(unknown||io.StorageUnconfirmed||!ResourcesConfirmed)return new(InstallationWithdrawalStatus.Unconfirmed,ResourcesConfirmed,errors.ToArray());
        lock(Retained)Retained.Remove(this);
        return new(observed?InstallationWithdrawalStatus.PreflightObservedReadOnly:cancelled?InstallationWithdrawalStatus.PreflightCancelled:InstallationWithdrawalStatus.Refused,true,errors.ToArray());
    }
}
