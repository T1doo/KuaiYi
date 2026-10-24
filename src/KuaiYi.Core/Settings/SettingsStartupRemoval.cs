namespace KuaiYi.Core.Settings;

// Internal only: this slice can revoke a qualified existing startup registration, never enable it.
// The public Save methods and SettingsJson's false-only admission are unchanged.
internal interface ISettingsStartupRemoval
{
    Guid Request { get; }
    bool SaveStarted { get; }
    void SettingsSaveStarted();
    Task ApplyAsync(CancellationToken cancellation);
    Task CommitAsync(SettingsSnapshot saved);
    Task AbortAsync();
}
internal interface ISettingsStartupRemovalParticipant
{
    Task<ISettingsStartupRemoval> ReserveAsync(string basisVersion,CancellationToken cancellation);
}
public sealed partial class SettingsCoordinator
{
    internal async Task<SettingsSnapshot> SaveWithStartupRemovalAsync(SettingsDraft draft,ISettingsStartupRemovalParticipant participant,
        ISettingsHotkeyParticipant? hotkeys=null,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if(draft.Data.LaunchAtSignIn)throw new SettingsValidationException("launchAtSignIn");
        // Reserve the original controller before Settings.serial or secret-transaction.lock.
        var original=await participant.ReserveAsync(draft.Basis.Version,cancellation).ConfigureAwait(false);
        try{return await SaveCore(draft,SettingsRecovery.None,cancellation,hotkeys,original).ConfigureAwait(false);}
        catch(Exception error)
        {
            // Also covers cancellation before Serialized starts. Abort is idempotent only on this exact local object.
            try{if(!original.SaveStarted)await original.AbortAsync().ConfigureAwait(false);}
            catch(Exception cleanup){throw new AggregateException("Original startup removal remains unconfirmed.",error,cleanup);}
            throw;
        }
    }
}
