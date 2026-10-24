using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Settings;

internal sealed record SettingsStartupIntent(Guid Request,Guid WriteId,string BasisVersion,string Sha256);
internal sealed record SettingsStartupLocalSettlement(Guid Request,Guid WriteId,string Sha256,string Outcome,string ConfigVersion,bool CleanupConfirmed);
internal sealed record SettingsStartupSaveOutcome(SettingsSnapshot Snapshot,Guid Request,string State="PublishedAwaitingOriginalControllerSettlement");
internal sealed record SettingsStartupIntentReferences(ImmutableArray<string> NewReferences,ImmutableArray<string> RetiredReferences);

// Internal only: this slice can revoke a qualified existing startup registration, never enable it.
// The public Save methods and SettingsJson's false-only admission are unchanged.
internal interface ISettingsStartupRemoval
{
    Guid Request { get; }
    bool SaveStarted { get; }
    void SettingsSaveStarted();
    void BindIntent(string directory,SettingsStartupIntent intent);
    Task ApplyAsync(CancellationToken cancellation);
    Task CommitAsync(SettingsSnapshot saved);
    Task AbortAsync();
    Task LocalSettledAsync(string outcome,string configVersion,bool cleanupConfirmed);
}
internal interface ISettingsStartupRemovalParticipant
{
    Task<ISettingsStartupRemoval> ReserveAsync(string basisVersion,CancellationToken cancellation);
}
public sealed partial class SettingsCoordinator
{
    internal async Task<SettingsStartupSaveOutcome> SaveWithStartupRemovalAsync(SettingsDraft draft,ISettingsStartupRemovalParticipant participant,
        ISettingsHotkeyParticipant? hotkeys=null,CancellationToken cancellation=default)
    {
        ArgumentNullException.ThrowIfNull(participant);
        if(draft.Data.LaunchAtSignIn)throw new SettingsValidationException("launchAtSignIn");
        // Reserve the original controller before Settings.serial or secret-transaction.lock.
        var original=await participant.ReserveAsync(draft.Basis.Version,cancellation).ConfigureAwait(false);
        try{return new(await SaveCore(draft,SettingsRecovery.None,cancellation,hotkeys,original).ConfigureAwait(false),original.Request);}
        catch(Exception error)
        {
            // Also covers cancellation before Serialized starts. Abort is idempotent only on this exact local object.
            try{if(!original.SaveStarted)await original.AbortAsync().ConfigureAwait(false);}
            catch(Exception cleanup){throw new AggregateException("Original startup removal remains unconfirmed.",error,cleanup);}
            throw;
        }
    }

    internal static SettingsStartupIntentReferences ReadOriginalStartupIntent(byte[] bytes,SettingsStartupIntent expected)
    {
        if(bytes.Length is <2 or >16384||expected.Request==Guid.Empty||expected.WriteId==Guid.Empty||
            Convert.ToHexString(SHA256.HashData(bytes))!=expected.Sha256)throw new SettingsTransactionUnconfirmedException();
        using(var doc=JsonDocument.Parse(bytes,new(){MaxDepth=16}))SettingsJson.RejectDuplicates(doc.RootElement);
        var value=JsonSerializer.Deserialize<SecretTransaction>(bytes,SettingsJson.Options)??throw new SettingsTransactionUnconfirmedException();
        if(value.SchemaVersion!=3||value.StartupRequest!=expected.Request||value.Id!=expected.WriteId||value.BasisVersion!=expected.BasisVersion||
            value.NewReferences.IsDefault||value.RetiredReferences.IsDefault||value.NewReferences.Length>16||value.RetiredReferences.Length>16||
            value.NewReferences.Concat(value.RetiredReferences).Any(r=>!SecretFileStore.ValidReference(r))||value.DeletedProfiles.IsDefault||value.RevokedBindings.IsDefault||
            value.DeletedProfiles.Length>16||value.RevokedBindings.Length>16||value.DeletedProfiles.Any(p=>!ValidProfileId(p))||
            value.RevokedBindings.Any(b=>b==null||!ValidProfileId(b.ProfileId)||b.PreviousSecretReference!=null&&!SecretFileStore.ValidReference(b.PreviousSecretReference))||
            !bytes.AsSpan().SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(value,SettingsJson.Options)))throw new SettingsTransactionUnconfirmedException();
        return new(value.NewReferences,value.RetiredReferences);
    }
}
