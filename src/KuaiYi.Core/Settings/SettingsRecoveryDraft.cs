namespace KuaiYi.Core.Settings;
/// <summary>Owns recovery drafts and selection until the original save settles; selection never touches storage.</summary>
public sealed class SettingsRecoveryDraft:IDisposable
{
    private readonly object sync=new();
    private readonly SettingsDraft original;
    private SettingsDraft? defaults;
    private SettingsDraft active;
    private SettingsRecovery choice;
    private bool saving,disposed;
    public SettingsRecoveryDraft(SettingsDraft initial){original=active=initial;}
    public SettingsDraft Draft{get{lock(sync)return active;}}
    public SettingsRecovery Choice{get{lock(sync)return choice;}}
    public bool IsSaving{get{lock(sync)return saving;}}
    public bool Select(SettingsRecovery selection)
    {
        lock(sync)
        {
            // Late/programmatic events after admission or commit cannot change either display or payload.
            if(saving||disposed||active.IsClosed||original.Basis.ReadOnly)return false;
            if(!Enum.IsDefined(selection))throw new SettingsValidationException("recoveryChoice");
            if(selection!=SettingsRecovery.None&&!original.Basis.RequiresRecovery||selection==SettingsRecovery.UseBackup&&original.Basis.State!=SettingsLoadState.RecoverableBackup)throw new SettingsRecoveryRequiredException();
            if(selection==SettingsRecovery.UseDefaults)
            {
                if(defaults==null){defaults=new(original.Basis);defaults.Update(_=>new());}
                active=defaults;
            }
            else active=original; // None removes authorization to save, but still displays the original recovery draft.
            choice=selection;return true;
        }
    }
    public Task<SettingsSnapshot> SaveAsync(SettingsCoordinator coordinator,CancellationToken cancellation=default,ISettingsHotkeyParticipant? hotkeys=null)
    {
        SettingsDraft selected;SettingsRecovery selection;
        lock(sync)
        {
            if(saving||disposed||active.IsClosed)throw new InvalidOperationException("Recovery draft is closed or saving.");
            selected=active;selection=choice;saving=true;
        }
        return SaveOriginal();
        async Task<SettingsSnapshot> SaveOriginal()
        {try{return hotkeys==null?await coordinator.SaveAsync(selected,selection,cancellation).ConfigureAwait(false):await coordinator.SaveWithHotkeysAsync(selected,hotkeys,selection,cancellation).ConfigureAwait(false);}finally{lock(sync)saving=false;}}
    }
    public void Dispose()
    {
        lock(sync)
        {
            if(saving)throw new InvalidOperationException("Original recovery save is still running.");
            if(disposed)return;disposed=true;if(!original.IsClosed)original.Dispose();if(defaults is {IsClosed:false})defaults.Dispose();
        }
    }
}
