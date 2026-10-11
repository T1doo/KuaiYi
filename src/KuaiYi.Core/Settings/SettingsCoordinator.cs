using System.Collections.Immutable;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
namespace KuaiYi.Core.Settings;
public sealed class SettingsTransactionUnconfirmedException():Exception("Settings/secret transaction needs recovery; original references retained.");
public sealed record SettingsChange(ImmutableArray<string> DeletedProfiles,ImmutableArray<string> DeletedSecrets)
{public bool HasDeletion=>DeletedProfiles.Length>0||DeletedSecrets.Length>0;}
public sealed class SettingsCoordinator(SettingsStore store,SecretFileStore secrets)
{
    private readonly SemaphoreSlim serial=new(1);private SettingsSnapshot? effective;
    public event Action<SettingsSnapshot,SettingsChange>? Committed;
    public SettingsSnapshot? Effective=>Volatile.Read(ref effective);
    public Task<SettingsDraft> OpenDraftAsync(CancellationToken cancellation=default)=>Serialized(async()=>
    {
        using var lease=await TransactionLease(cancellation);var snapshot=await store.LoadAsync(cancellation);if(!snapshot.ReadOnly)await Recover(snapshot,cancellation);Volatile.Write(ref effective,snapshot);return new SettingsDraft(snapshot);
    },cancellation);
    public Task<SettingsSnapshot> SaveAsync(SettingsDraft draft,SettingsRecovery recovery=SettingsRecovery.None,CancellationToken cancellation=default)=>Serialized(async()=>
    {
        var captured=draft.BeginSave();bool committed=false;var staged=new List<string>();Guid id=Guid.NewGuid();string journal=JournalPath(id);bool journalWritten=false;
        try
        {
            using var lease=await TransactionLease(cancellation);var current=await store.LoadAsync(cancellation);await Recover(current,cancellation);
            if(current.Version!=draft.Basis.Version)throw new SettingsConflictException();if(current.ReadOnly)throw new UnknownSettingsSchemaException(-1);
            if(current.RequiresRecovery&&recovery==SettingsRecovery.None)throw new SettingsRecoveryRequiredException();
            SettingsJson.Validate(captured.Data);
            if(!captured.Data.Hotkeys.OrderBy(p=>p.Key).SequenceEqual(draft.Basis.Data.Hotkeys.OrderBy(p=>p.Key))||!captured.Data.Privacy.ClipboardAllowedApplications.SequenceEqual(draft.Basis.Data.Privacy.ClipboardAllowedApplications)||!captured.Data.Privacy.ConsentedEndpointOrigins.SequenceEqual(draft.Basis.Data.Privacy.ConsentedEndpointOrigins))throw new SettingsValidationException("deferredAuthorizationOrHotkeyTransaction");
            foreach(var p in captured.Data.Profiles)if(p.SecretRef!=null&&secrets.IsRevoked(p.SecretRef))throw new SettingsValidationException("revokedSecretRef");
            var data=captured.Data with{WriteId=id};var replacements=new Dictionary<string,string>();
            foreach(var key in captured.Keys){if(!data.Profiles.Any(p=>p.Id==key.Key))continue;string reference=await secrets.SaveNewAsync(key.Value,cancellation);staged.Add(reference);replacements[key.Key]=reference;}
            data=data with{Profiles=data.Profiles.Select(p=>replacements.TryGetValue(p.Id,out var reference)?p with{SecretRef=reference}:p).ToImmutableArray()};
            var remainingReferences=data.Profiles.Where(p=>p.SecretRef!=null).Select(p=>p.SecretRef!).ToHashSet(StringComparer.Ordinal);
            var retired=draft.Basis.Data.Profiles.Where(p=>p.SecretRef!=null&&!remainingReferences.Contains(p.SecretRef)&&(captured.Cleared.Contains(p.Id)||!data.Profiles.Any(n=>n.Id==p.Id))).Select(p=>p.SecretRef!).Distinct().ToImmutableArray();
            var transaction=new SecretTransaction{Id=id,BasisVersion=draft.Basis.Version,NewReferences=staged.ToImmutableArray(),RetiredReferences=retired};
            byte[] metadata=JsonSerializer.SerializeToUtf8Bytes(transaction,SettingsJson.Options);try{SettingsStore.WriteFlushed(journal,metadata);journalWritten=true;}finally{CryptographicOperations.ZeroMemory(metadata);}
            SettingsSnapshot saved;
            try{saved=await store.SaveAsync(draft.Basis,data,recovery,cancellation);}
            catch(Exception error)
            {
                if(error is SettingsConflictException or UnknownSettingsSchemaException or SettingsRecoveryRequiredException or OperationCanceledException){foreach(var reference in staged)await secrets.DeleteUnpublishedAsync(reference);File.Delete(journal);journalWritten=false;throw;}
                // An error after atomic publication must not be represented as a rollback.
                var observed=await store.LoadAsync();
                if(!observed.ReadOnly&&!observed.RequiresRecovery&&observed.Data.WriteId==id)saved=observed;
                else if(observed.Version==draft.Basis.Version){foreach(var reference in staged)await secrets.DeleteUnpublishedAsync(reference);File.Delete(journal);journalWritten=false;throw;}
                else throw new SettingsTransactionUnconfirmedException();
            }
            committed=true;Volatile.Write(ref effective,saved); // Whole immutable configuration changes only after disk publication.
            Committed?.Invoke(saved,new(draft.Basis.Data.Profiles.Where(p=>!data.Profiles.Any(n=>n.Id==p.Id)).Select(p=>p.Id).ToImmutableArray(),retired));
            // No cancellation after commit: finish durable deletion, or retain journal for recovery.
            foreach(var reference in retired)await secrets.RevokeAsync(reference);
            File.Delete(journal);journalWritten=false;return saved;
        }
        catch
        {
            if(!journalWritten&&!committed)foreach(var reference in staged)await secrets.DeleteUnpublishedAsync(reference);
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
            if(transaction.SchemaVersion!=1||transaction.Id==Guid.Empty||Path.GetFileName(path)!=Path.GetFileName(JournalPath(transaction.Id))||transaction.NewReferences.IsDefault||transaction.RetiredReferences.IsDefault||transaction.NewReferences.Length>16||transaction.RetiredReferences.Length>16||transaction.NewReferences.Concat(transaction.RetiredReferences).Any(r=>!SecretFileStore.ValidReference(r)))throw new SettingsTransactionUnconfirmedException();
            if(snapshot.ReadOnly||snapshot.RequiresRecovery)throw new SettingsTransactionUnconfirmedException();
            if(snapshot.Data.WriteId==transaction.Id)
            {
                // Prior committed journals may predate shared-reference retirement rules. The saved configuration proves these references are still live.
                var live=snapshot.Data.Profiles.Where(p=>p.SecretRef!=null).Select(p=>p.SecretRef!).ToHashSet(StringComparer.Ordinal);
                foreach(string reference in transaction.RetiredReferences)if(!live.Contains(reference))await secrets.RevokeAsync(reference);
            }
            else if(snapshot.Version==transaction.BasisVersion)foreach(string reference in transaction.NewReferences)await secrets.DeleteUnpublishedAsync(reference);
            else throw new SettingsTransactionUnconfirmedException(); // Cannot prove either outcome; never delete old references or guess.
            File.Delete(path);
        }
    }
    private string JournalPath(Guid id)=>Path.Combine(store.DirectoryPath,"secret-txn-"+id.ToString("N")+".json");
    private Task<T> Serialized<T>(Func<Task<T>> action,CancellationToken cancellation)=>Task.Run(async()=>{await serial.WaitAsync(cancellation).ConfigureAwait(false);try{return await action().ConfigureAwait(false);}finally{serial.Release();}},cancellation);
    private Task<FileStream> TransactionLease(CancellationToken cancellation)=>Task.Run(()=>
    {
        Directory.CreateDirectory(store.DirectoryPath);SettingsStore.RejectLink(store.DirectoryPath);string path=Path.Combine(store.DirectoryPath,"secret-transaction.lock");var timer=Stopwatch.StartNew();
        while(true){cancellation.ThrowIfCancellationRequested();if(File.Exists(path))SettingsStore.RejectLink(path);try{return new FileStream(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){if(timer.Elapsed>TimeSpan.FromSeconds(5))throw new SettingsBusyException();cancellation.WaitHandle.WaitOne(20);}}
    },cancellation);
    private sealed record SecretTransaction
    {
        public int SchemaVersion{get;init;}=1;public Guid Id{get;init;}public string BasisVersion{get;init;}="";
        public ImmutableArray<string> NewReferences{get;init;}=[];public ImmutableArray<string> RetiredReferences{get;init;}=[];
    }
}
