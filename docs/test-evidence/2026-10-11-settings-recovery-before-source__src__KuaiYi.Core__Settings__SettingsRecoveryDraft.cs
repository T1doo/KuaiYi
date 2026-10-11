namespace KuaiYi.Core.Settings;
/// <summary>Recovery selection and its displayed draft share one owner; never touches storage on selection.</summary>
public sealed class SettingsRecoveryDraft(SettingsDraft initial):IDisposable
{
    public SettingsDraft Draft{get;}=initial;
    public SettingsRecovery Choice{get;private set;}
    public bool IsSaving{get;private set;}
    public bool Select(SettingsRecovery choice)
    {
        Choice=choice;
        if(choice==SettingsRecovery.UseDefaults)Draft.Update(_=>new());
        return true;
    }
    public async Task<SettingsSnapshot> SaveAsync(SettingsCoordinator coordinator,CancellationToken cancellation=default)
    {IsSaving=true;try{return await coordinator.SaveAsync(Draft,Choice,cancellation).ConfigureAwait(false);}finally{IsSaving=false;}}
    public void Dispose(){if(!Draft.IsClosed)Draft.Dispose();}
}
