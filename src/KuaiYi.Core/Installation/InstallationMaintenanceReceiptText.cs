namespace KuaiYi.Core.Installation;
internal static class InstallationMaintenanceReceiptText
{
    internal static IReadOnlyList<string> Lines(InstallationMaintenanceOutcome result)
    {
        List<string> lines=["MAINTENANCE_RESULT|"+result.Intent+"|"+result.Status];
        if(result.ExecutedSteps is {Count:>0} executed)foreach(var step in executed)lines.Add("MAINTENANCE_EFFECT|"+step);
        else if(result.Progress is not{TicketAttempted:true})lines.Add("MAINTENANCE_NOT_EXECUTED|PreserveUserData");
        if(result.Progress is {} p)lines.Add($"MAINTENANCE_PROGRESS|{p.TicketAttempted}|{p.ReferencePrefix}|{p.ReferenceAttempt}|{p.FileMutationAttempted}|{p.Irrevocable}|{p.StorageUnconfirmed}|{p.CandidatePreparationAttempted}");
        if(result.RecoveryOperation is {} recovery)lines.Add("MAINTENANCE_RECOVERY|"+recovery);
        foreach(var step in result.RemainingSteps)lines.Add("MAINTENANCE_PENDING|"+step.Action+"|"+step.Authorization);
        lines.Add("MAINTENANCE_SETTLED|"+(result.CleanupConfirmed?"Confirmed":"Unconfirmed"));return lines.AsReadOnly();
    }
    internal static int ExitCode(InstallationMaintenanceOutcome result,bool cancellationFailed=false)
    {
        if(!result.CleanupConfirmed||cancellationFailed||result.Status==InstallationMaintenanceStatus.Unconfirmed)return 3;
        return result.Status switch
        {
            InstallationMaintenanceStatus.ObservedReadOnly or InstallationMaintenanceStatus.FilesRetired or InstallationMaintenanceStatus.PayloadRepaired or InstallationMaintenanceStatus.ReferencesWithdrawn=>4,
            InstallationMaintenanceStatus.Cancelled=>7,
            _=>6
        }; // No whole-maintenance-success code until all remaining platform/data actions have an executable contract.
    }

}
