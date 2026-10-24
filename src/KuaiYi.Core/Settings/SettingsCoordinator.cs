using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Runtime.ExceptionServices;
namespace KuaiYi.Core.Settings;
public sealed class SettingsTransactionUnconfirmedException():Exception("Settings/secret transaction needs recovery; original references retained.");
public sealed record RevokedProfileBinding(string ProfileId,string? PreviousSecretReference);
public sealed record SettingsChange(ImmutableArray<string> DeletedProfiles,ImmutableArray<string> DeletedSecrets)
{public ImmutableArray<RevokedProfileBinding> RevokedProfileBindings{get;init;}=[];public bool ClipboardPermissionsChanged{get;init;}public bool HasBindingRevocation=>RevokedProfileBindings.Length>0;public bool HasDeletion=>DeletedProfiles.Length>0||DeletedSecrets.Length>0||HasBindingRevocation;}
public sealed partial class SettingsCoordinator(SettingsStore store,SecretFileStore secrets)
{
    /// <summary>Only an admitted request can open its exact encrypted binding. Never used on startup or Off.</summary>
    public async Task<SecretLease> OpenRequestSecretAsync(RuntimeGate gate,RequestIdentity identity,RequestProfileBinding binding,EndpointOrigin origin,CancellationToken cancellation=default)
    {
        bool Current()=>gate.IsRequestProfileBindingCurrent(identity,binding)&&gate.CanSend(identity,origin)&&Effective?.Data.Profiles.Any(p=>p.Id==binding.ProfileId&&p.SecretRef==binding.SecretReference)==true;
        if(binding.SecretReference==null||!Current())throw new InvalidOperationException("Request secret admission rejected.");
        var lease=await secrets.OpenAsync(binding.SecretReference,cancellation).ConfigureAwait(false);
        if(!Current()||cancellation.IsCancellationRequested){lease.Dispose();throw new OperationCanceledException("Request secret admission expired.",cancellation);}
        return lease;
    }
    private readonly SemaphoreSlim serial=new(1);private SettingsSnapshot? effective;
    public event Action<SettingsSnapshot,SettingsChange>? Committed;
    public SettingsSnapshot? Effective=>Volatile.Read(ref effective);
    internal KuaiYi.Core.Installation.InstallationPreparationFaults? StartupJournalFaultsForTest{get;init;}
    /// <summary>Only the separate explicit application confirmation UI calls this immediate transaction; ordinary drafts cannot grant permission.</summary>
    public Task<SettingsSnapshot> SetClipboardPermissionAsync(string executable,bool enabled,CancellationToken cancellation=default)=>Serialized(async()=>
    {
        string application=ClipboardApplicationConsent.Normalize(executable);using var lease=await TransactionLease(cancellation);var current=await store.LoadAsync(cancellation);await Recover(current,cancellation);
        if(current.ReadOnly)throw new UnknownSettingsSchemaException(-1);if(current.RequiresRecovery)throw new SettingsRecoveryRequiredException();
        bool Same(string value){try{return string.Equals(ClipboardApplicationConsent.Normalize(value),application,StringComparison.OrdinalIgnoreCase);}catch(InvalidDataException){return false;}}
        var applications=current.Data.Privacy.ClipboardAllowedApplications.Where(p=>!Same(p)).ToImmutableArray();if(enabled)applications=applications.Add(application);
        Guid id=Guid.NewGuid();string journal=JournalPath(id);var data=current.Data with{WriteId=id,Privacy=current.Data.Privacy with{ClipboardAllowedApplications=applications}};SettingsJson.Validate(data);
        byte[] metadata=JsonSerializer.SerializeToUtf8Bytes(new SecretTransaction{Id=id,BasisVersion=current.Version,ClipboardPermissionsChanged=true},SettingsJson.Options);try{SettingsStore.WriteFlushed(journal,metadata);}finally{CryptographicOperations.ZeroMemory(metadata);}
        SettingsSnapshot saved;
        try{saved=await store.SaveAsync(current,data,cancellation:cancellation);}
        catch(Exception error)
        {
            if(error is SettingsConflictException or UnknownSettingsSchemaException or SettingsRecoveryRequiredException or OperationCanceledException){File.Delete(journal);throw;}
            var observed=await store.LoadAsync();if(!observed.ReadOnly&&!observed.RequiresRecovery&&observed.Data.WriteId==id)saved=observed;else if(observed.Version==current.Version){File.Delete(journal);throw;}else throw new SettingsTransactionUnconfirmedException();
        }
        Volatile.Write(ref effective,saved);PublishCommitted(saved,new([],[]){ClipboardPermissionsChanged=true});File.Delete(journal);return saved;
    },cancellation);
    public Task<SettingsDraft> OpenDraftAsync(CancellationToken cancellation=default)=>Serialized(async()=>
    {
        using var lease=await TransactionLease(cancellation);var snapshot=await store.LoadAsync(cancellation);if(!snapshot.ReadOnly)await Recover(snapshot,cancellation);Volatile.Write(ref effective,snapshot);return new SettingsDraft(snapshot);
    },cancellation);
    public Task<SettingsSnapshot> SaveAsync(SettingsDraft draft,SettingsRecovery recovery=SettingsRecovery.None,CancellationToken cancellation=default)=>SaveCore(draft,recovery,cancellation,null);
    public Task<SettingsSnapshot> SaveWithHotkeysAsync(SettingsDraft draft,ISettingsHotkeyParticipant hotkeys,SettingsRecovery recovery=SettingsRecovery.None,CancellationToken cancellation=default)=>SaveCore(draft,recovery,cancellation,hotkeys??throw new ArgumentNullException(nameof(hotkeys)));
    private Task<SettingsSnapshot> SaveCore(SettingsDraft draft,SettingsRecovery recovery,CancellationToken cancellation,ISettingsHotkeyParticipant? hotkeys,ISettingsStartupRemoval? startup=null)=>Serialized(async()=>
    {
        var captured=draft.BeginSave();startup?.SettingsSaveStarted();ISettingsHotkeyTransaction? hotkeyTransaction=null;bool committed=false,publicationUnconfirmed=false;var staged=new List<string>();Guid id=Guid.NewGuid();string journal=JournalPath(id);bool journalWritten=false;SettingsStartupJournal? startupJournal=null;
        try
        {
            using var lease=await TransactionLease(cancellation);var current=await store.LoadAsync(cancellation);await Recover(current,cancellation);
            if(current.Version!=draft.Basis.Version)throw new SettingsConflictException();if(current.ReadOnly)throw new UnknownSettingsSchemaException(-1);
            if(current.RequiresRecovery&&recovery==SettingsRecovery.None)throw new SettingsRecoveryRequiredException();
            SettingsJson.Validate(captured.Data);
            if(hotkeys==null&&!captured.Data.Hotkeys.OrderBy(p=>p.Key).SequenceEqual(draft.Basis.Data.Hotkeys.OrderBy(p=>p.Key))||!captured.Data.Privacy.ClipboardAllowedApplications.SequenceEqual(draft.Basis.Data.Privacy.ClipboardAllowedApplications)||!captured.Data.Privacy.ConsentedEndpointOrigins.SequenceEqual(draft.Basis.Data.Privacy.ConsentedEndpointOrigins))throw new SettingsValidationException("deferredAuthorizationOrHotkeyTransaction");
            if(hotkeys!=null&&!captured.Data.Hotkeys.OrderBy(p=>p.Key).SequenceEqual(draft.Basis.Data.Hotkeys.OrderBy(p=>p.Key)))hotkeyTransaction=await hotkeys.PrepareAsync(captured.Data.Hotkeys,cancellation);
            foreach(var p in captured.Data.Profiles)if(p.SecretRef!=null&&secrets.IsRevoked(p.SecretRef))throw new SettingsValidationException("revokedSecretRef");
            var data=captured.Data with{WriteId=id};var replacements=new Dictionary<string,string>();
            foreach(var key in captured.Keys){if(!data.Profiles.Any(p=>p.Id==key.Key))continue;string reference=await secrets.SaveNewAsync(key.Value,cancellation);staged.Add(reference);replacements[key.Key]=reference;}
            data=data with{Profiles=data.Profiles.Select(p=>replacements.TryGetValue(p.Id,out var reference)?p with{SecretRef=reference}:p).ToImmutableArray()};
            var remainingReferences=data.Profiles.Where(p=>p.SecretRef!=null).Select(p=>p.SecretRef!).ToHashSet(StringComparer.Ordinal);
            var retired=draft.Basis.Data.Profiles.Where(p=>p.SecretRef!=null&&!remainingReferences.Contains(p.SecretRef)&&(captured.Cleared.Contains(p.Id)||!data.Profiles.Any(n=>n.Id==p.Id))).Select(p=>p.SecretRef!).Distinct().ToImmutableArray();
            var change=ChangeFrom(draft.Basis.Data,data,retired);
            var transaction=new SecretTransaction{SchemaVersion=startup==null?2:3,StartupRequest=startup?.Request,Id=id,BasisVersion=draft.Basis.Version,NewReferences=staged.ToImmutableArray(),RetiredReferences=retired,DeletedProfiles=change.DeletedProfiles,RevokedBindings=change.RevokedProfileBindings};
            byte[] metadata=JsonSerializer.SerializeToUtf8Bytes(transaction,SettingsJson.Options);
            try
            {
                if(startup==null){SettingsStore.WriteFlushed(journal,metadata);journalWritten=true;}
                else
                {
                    startup.BindIntent(store.DirectoryPath,new(startup.Request,id,draft.Basis.Version,Convert.ToHexString(SHA256.HashData(metadata))));
                    startupJournal=new(store.DirectoryPath,journal,metadata,StartupJournalFaultsForTest);journalWritten=true;startupJournal.Write();
                }
            }
            finally{CryptographicOperations.ZeroMemory(metadata);}
            if(startup!=null)await startup.ApplyAsync(cancellation).ConfigureAwait(false);
            SettingsSnapshot saved;
            try{saved=await store.SaveAsync(draft.Basis,data,recovery,cancellation);}
            catch(Exception error)
            {
                if(error is SettingsConflictException or UnknownSettingsSchemaException or SettingsRecoveryRequiredException){if(startup==null){foreach(var reference in staged)await secrets.DeleteUnpublishedAsync(reference);File.Delete(journal);journalWritten=false;}throw;}
                // An error after atomic publication must not be represented as a rollback.
                SettingsSnapshot observed;try{observed=await store.LoadAsync();}catch(Exception probe){publicationUnconfirmed=true;throw new AggregateException("Settings publication unconfirmed; hotkey transaction retained.",error,probe);}
                if(!observed.ReadOnly&&!observed.RequiresRecovery&&observed.Data.WriteId==id)saved=observed;
                else if(observed.Version==draft.Basis.Version){if(startup==null){foreach(var reference in staged)await secrets.DeleteUnpublishedAsync(reference);File.Delete(journal);journalWritten=false;}throw;}
                else{publicationUnconfirmed=true;throw new SettingsTransactionUnconfirmedException();}
            }
            committed=true;Volatile.Write(ref effective,saved); // Whole immutable configuration changes only after disk publication.
            List<Exception> publicationErrors=[];
            Task? originalHotkeyCommit=null,originalStartupCommit=null;
            if(startup!=null)try{originalStartupCommit=startup.CommitAsync(saved);}catch(Exception error){publicationErrors.Add(error);}
            if(hotkeyTransaction!=null)try{originalHotkeyCommit=hotkeyTransaction.CommitAsync(saved);}catch(Exception error){publicationErrors.Add(error);}
            // Disk/effective already changed. Every safety subscriber must receive revocation even when native commit/fence failed.
            try{PublishCommitted(saved,change);}catch(Exception error){publicationErrors.Add(error);}
            if(originalHotkeyCommit!=null)try{await originalHotkeyCommit;}catch(Exception error){publicationErrors.Add(error);}
            if(originalStartupCommit!=null)try{await originalStartupCommit;}catch(Exception error){publicationErrors.Add(error);}
            if(startup!=null)
            {
                // Local revocation must not be skipped because a remote receipt/transport is uncertain.
                foreach(var reference in retired)try{await secrets.RevokeAsync(reference);}catch(Exception error){publicationErrors.Add(error);}
                try{startupJournal!.RequireSettledIntent();}catch(Exception error){publicationErrors.Add(error);}
                try{await startup.LocalSettledAsync("COMMITTED",saved.Version,publicationErrors.Count==0).ConfigureAwait(false);}catch(Exception error){publicationErrors.Add(error);}
            }
            if(publicationErrors.Count==1)ExceptionDispatchInfo.Capture(publicationErrors[0]).Throw();
            if(publicationErrors.Count>1)throw new AggregateException("Configuration committed; hotkey settlement or safety notification failed. Recovery journal retained.",publicationErrors);
            // No cancellation after commit: finish durable deletion, or retain journal for recovery.
            if(startup==null){foreach(var reference in retired)await secrets.RevokeAsync(reference);File.Delete(journal);journalWritten=false;}
            // A receipt is not the remote send Task's settlement. Only the original controller may retire v3 after exit/IO.
            return saved;
        }
        catch(Exception error)
        {
            var failures=new List<Exception>{error};
            if(!journalWritten&&!committed)foreach(var reference in staged)
                try{await secrets.DeleteUnpublishedAsync(reference);}catch(Exception cleanup){failures.Add(cleanup);}
            if(!committed&&!publicationUnconfirmed)
            {
                if(hotkeyTransaction!=null)try{await hotkeyTransaction.RollbackAsync();}catch(Exception rollback){failures.Add(rollback);}
                if(startup!=null)try{await startup.AbortAsync().ConfigureAwait(false);}catch(Exception rollback){failures.Add(rollback);}
                // Even a visible ABORT_OK leaves v3 pending until the original controller's post-exit settlement.
                if(startup!=null&&journalWritten&&failures.Count==1)
                {
                    try
                    {
                        startupJournal!.RequireSettledIntent();
                        foreach(var reference in staged)try{await secrets.DeleteUnpublishedAsync(reference);}catch(Exception cleanup){failures.Add(cleanup);}
                    }
                    catch(Exception cleanup){failures.Add(cleanup);}
                }
                if(startup!=null&&journalWritten)
                    try{await startup.LocalSettledAsync("ABORTED",draft.Basis.Version,failures.Count==1).ConfigureAwait(false);}catch(Exception notice){failures.Add(notice);}
            }
            if(failures.Count>1)throw new AggregateException("Settings save or original participant settlement failed; recovery intent retained.",failures);
            throw;
        }
        finally{foreach(var value in captured.Keys.Values)CryptographicOperations.ZeroMemory(value);draft.EndSave(committed);}
    },cancellation);
    private async Task Recover(SettingsSnapshot snapshot,CancellationToken cancellation)
    {
        var files=Directory.GetFiles(store.DirectoryPath,"secret-txn-*.json");if(files.Length>32)throw new SettingsTransactionUnconfirmedException();
        foreach(string path in files)
        {
            cancellation.ThrowIfCancellationRequested();SettingsStore.RejectLink(path);var info=new FileInfo(path);if(info.Length is <2 or >16384)throw new SettingsTransactionUnconfirmedException();
            byte[] bytes;using(var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)){if(file.Length is <2 or >16384)throw new SettingsTransactionUnconfirmedException();bytes=new byte[(int)file.Length];file.ReadExactly(bytes);}
            using(var document=JsonDocument.Parse(bytes,new(){MaxDepth=16}))SettingsJson.RejectDuplicates(document.RootElement);
            var transaction=JsonSerializer.Deserialize<SecretTransaction>(bytes,SettingsJson.Options)??throw new SettingsTransactionUnconfirmedException();
            if(transaction.SchemaVersion is not (1 or 2)||transaction.StartupRequest!=null||transaction.Id==Guid.Empty||Path.GetFileName(path)!=Path.GetFileName(JournalPath(transaction.Id))||transaction.NewReferences.IsDefault||transaction.RetiredReferences.IsDefault||transaction.NewReferences.Length>16||transaction.RetiredReferences.Length>16||transaction.NewReferences.Concat(transaction.RetiredReferences).Any(r=>!SecretFileStore.ValidReference(r))||transaction.DeletedProfiles.IsDefault||transaction.RevokedBindings.IsDefault||transaction.DeletedProfiles.Length>16||transaction.RevokedBindings.Length>16||transaction.DeletedProfiles.Any(p=>!ValidProfileId(p))||transaction.RevokedBindings.Any(b=>b==null||!ValidProfileId(b.ProfileId)||b.PreviousSecretReference!=null&&!SecretFileStore.ValidReference(b.PreviousSecretReference)))throw new SettingsTransactionUnconfirmedException();
            if(snapshot.ReadOnly||snapshot.RequiresRecovery)throw new SettingsTransactionUnconfirmedException();
            if(snapshot.Data.WriteId==transaction.Id)
            {
                // Prior committed journals may predate shared-reference retirement rules. The saved configuration proves these references are still live.
                var live=snapshot.Data.Profiles.Where(p=>p.SecretRef!=null).Select(p=>p.SecretRef!).ToHashSet(StringComparer.Ordinal);
                var retired=transaction.RetiredReferences.Where(r=>!live.Contains(r)).Distinct().ToImmutableArray();
                var change=new SettingsChange(transaction.DeletedProfiles,retired){RevokedProfileBindings=transaction.RevokedBindings,ClipboardPermissionsChanged=transaction.ClipboardPermissionsChanged};
                if(transaction.SchemaVersion==1&&effective is {ReadOnly:false,RequiresRecovery:false} previous&&previous.Data.WriteId!=snapshot.Data.WriteId)change=ChangeFrom(previous.Data,snapshot.Data,retired);
                Volatile.Write(ref effective,snapshot);PublishCommitted(snapshot,change);
                foreach(string reference in retired)await secrets.RevokeAsync(reference);
            }
            else if(snapshot.Version==transaction.BasisVersion)foreach(string reference in transaction.NewReferences)await secrets.DeleteUnpublishedAsync(reference);
            else throw new SettingsTransactionUnconfirmedException(); // Cannot prove either outcome; never delete old references or guess.
            File.Delete(path);
        }
    }
    private string JournalPath(Guid id)=>Path.Combine(store.DirectoryPath,"secret-txn-"+id.ToString("N")+".json");
    private static bool ValidProfileId(string? id)=>id is {Length:>0 and <=64}&&id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_');
    private static SettingsChange ChangeFrom(AppSettings before,AppSettings after,ImmutableArray<string> retired)
        =>new(before.Profiles.Where(p=>!after.Profiles.Any(n=>n.Id==p.Id)).Select(p=>p.Id).ToImmutableArray(),retired)
        {RevokedProfileBindings=before.Profiles.Where(p=>!after.Profiles.Any(n=>n.Id==p.Id&&n.SecretRef==p.SecretRef)).Select(p=>new RevokedProfileBinding(p.Id,p.SecretRef)).ToImmutableArray()};
    private void PublishCommitted(SettingsSnapshot snapshot,SettingsChange change)
    {
        List<Exception>? errors=null;
        foreach(var callback in Committed?.GetInvocationList()??[])
        {try{((Action<SettingsSnapshot,SettingsChange>)callback)(snapshot,change);}catch(Exception error){(errors??=[]).Add(error);}}
        // Every runtime subscriber gets the committed invalidation; errors still fail the save and retain its recovery journal.
        if(errors is {Count:1})ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors is {Count:>1})throw new AggregateException("Settings commit observers failed; journal retained.",errors);
    }
    private Task<T> Serialized<T>(Func<Task<T>> action,CancellationToken cancellation)=>Task.Run(async()=>{await serial.WaitAsync(cancellation).ConfigureAwait(false);try{return await action().ConfigureAwait(false);}finally{serial.Release();}},cancellation);
    private Task<FileStream> TransactionLease(CancellationToken cancellation)=>Task.Run(()=>
    {
        Directory.CreateDirectory(store.DirectoryPath);SettingsStore.RejectLink(store.DirectoryPath);string path=Path.Combine(store.DirectoryPath,"secret-transaction.lock");var timer=Stopwatch.StartNew();
        while(true){cancellation.ThrowIfCancellationRequested();if(File.Exists(path))SettingsStore.RejectLink(path);try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){if(timer.Elapsed>TimeSpan.FromSeconds(5))throw new SettingsBusyException();cancellation.WaitHandle.WaitOne(20);}}
    },cancellation);
    private sealed record SecretTransaction
    {
        public int SchemaVersion{get;init;}=2;[System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]public Guid? StartupRequest{get;init;}public Guid Id{get;init;}public string BasisVersion{get;init;}="";
        public ImmutableArray<string> NewReferences{get;init;}=[];public ImmutableArray<string> RetiredReferences{get;init;}=[];
        public ImmutableArray<string> DeletedProfiles{get;init;}=[];public ImmutableArray<RevokedProfileBinding> RevokedBindings{get;init;}=[];
        public bool ClipboardPermissionsChanged{get;init;}
    }
}
