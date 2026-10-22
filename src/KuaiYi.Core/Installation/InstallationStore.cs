using System.Text;
using System.Text.Json;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

public sealed record InstalledVersion(string Version,string DirectoryPath,string CatalogSha256);
public sealed class InstallationBusyException():IOException("Product roles are not confirmed stopped, or installation is busy.");
public sealed class InstallationRecoveryException(string message):IOException(message);
public sealed class InstallationRollbackException(Exception operation,Exception rollback):IOException("Installation failed and rollback is unconfirmed; all versions retained.",new AggregateException(operation,rollback));
internal enum InstallationStage { PackageVerified,PayloadMaterialized,InstalledCatalogVerified,VersionPublished,BeforePointerPublish,PointerPublished,RollbackPublished }
/// <summary>File transaction only. The future installer must keep product roles stopped throughout; this library never registers,
/// signs, starts, stops or deletes applications. ReadActive consumes the atomic version record, not a Windows launch registration.</summary>
public sealed partial class InstallationStore
{
    private sealed record Reference(string Id,string CatalogSha256);
    private sealed record Pointer(Reference Active,Reference? Previous);
    private const string Owner="KuaiYi installation file store v1\n";
    private readonly string directory,userData;private readonly Func<bool> rolesStopped;
    internal Action<InstallationStage>? StageForTest {get;init;}
    public InstallationStore(string directory,string userDataDirectory,Func<bool> rolesStopped)
    {
        this.directory=InstallationPaths.Root(directory);userData=InstallationPaths.Root(userDataDirectory);this.rolesStopped=rolesStopped??throw new ArgumentNullException(nameof(rolesStopped));
        if(InstallationPaths.Contains(this.directory,userData)||InstallationPaths.Contains(userData,this.directory))throw new InvalidDataException("Application files and user data must be separate.");
    }
    public Task<InstalledVersion> ApplyAsync(string packageDirectory,string expectedCatalogSha256,CancellationToken cancellation=default)=>Task.Run(()=>Apply(packageDirectory,expectedCatalogSha256,cancellation),cancellation);
    internal Task<InstalledVersion> ApplyForUpgradeAsync(string packageDirectory,string hash,InstallationAdmission admission,Func<InstalledVersion,CancellationToken,Task> health,CancellationToken cancellation)
        =>Task.Run(()=>Apply(packageDirectory,hash,cancellation,admission,health),cancellation);
    private InstalledVersion Apply(string packageDirectory,string expectedCatalogSha256,CancellationToken cancellation,InstallationAdmission? admission=null,Func<InstalledVersion,CancellationToken,Task>? health=null)
    {
        string source=InstallationPaths.Root(packageDirectory);if(InstallationPaths.Contains(directory,source)||InstallationPaths.Contains(source,directory)||InstallationPaths.Contains(userData,source)||InstallationPaths.Contains(source,userData))throw new InvalidDataException("Package source must be separate.");
        if(!InstallationPackage.HashValid(expectedCatalogSha256))throw new InvalidDataException("Expected package catalog digest required.");
        RequireStopped();var package=InstallationPackage.ReadSource(source,expectedCatalogSha256,cancellation);if(!string.Equals(package.CatalogSha256,expectedCatalogSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Package catalog digest mismatch.");StageForTest?.Invoke(InstallationStage.PackageVerified);cancellation.ThrowIfCancellationRequested();RequireStopped();
        using var ownAdmission=admission==null?EnterAdmission(mutation:true):null;admission?.RequireMutation(directory);
        using var lease=Lock();byte[]? before=PointerBytes();Pointer? prior=before==null?null:Parse(before);if(prior!=null)_=Verify(prior.Active,cancellation);
        string id=Guid.NewGuid().ToString("N"),staging=Path.Combine(directory,"staging-"+id),versions=Path.Combine(directory,"versions"),destination=Path.Combine(versions,id);
        if(Directory.Exists(staging)||File.Exists(staging)||Directory.Exists(destination)||File.Exists(destination))throw new InstallationRecoveryException("Unexpected version path retained.");
        Directory.CreateDirectory(staging);InstallationPackage installed;
        if(package.Schema==2)
        {
            package.CopyTo(source,staging,cancellation,InstallationPayload.SourceCatalogName);
            InstallationPayload.Materialize(package,staging,destination,cancellation);StageForTest?.Invoke(InstallationStage.PayloadMaterialized);
            installed=InstallationPackage.ReadInstalled(staging,cancellation,destination);StageForTest?.Invoke(InstallationStage.InstalledCatalogVerified);
        }
        else{package.CopyTo(source,staging,cancellation);installed=package;}
        Directory.CreateDirectory(versions);InstallationPaths.Check(versions);Directory.Move(staging,destination);StageForTest?.Invoke(InstallationStage.VersionPublished);
        var reference=new Reference(id,installed.CatalogSha256);var next=new Pointer(reference,prior?.Active);byte[] nextBytes=Encode(next);bool published=false;
        try
        {
            StageForTest?.Invoke(InstallationStage.BeforePointerPublish);_=Verify(reference,cancellation);RequireStopped();cancellation.ThrowIfCancellationRequested();Publish(nextBytes,before);published=true;StageForTest?.Invoke(InstallationStage.PointerPublished);
            // Ordinary Apply commits here. The upgrade coordinator retains the admission and writer leases through health.
            if(health!=null)health(Verify(reference,CancellationToken.None),cancellation).GetAwaiter().GetResult();
            RequirePointer(nextBytes);return Verify(reference,CancellationToken.None);
        }
        catch(Exception operation)
        {
            if(!published)throw;
            try
            {
                RestoreRecord(before,nextBytes);
                StageForTest?.Invoke(InstallationStage.RollbackPublished);
            }
            catch(Exception rollback){throw new InstallationRollbackException(operation,rollback);}
            throw;
        }
    }
    public InstalledVersion? ReadActive(CancellationToken cancellation=default)
    {if(!Directory.Exists(directory))return null;RequireOwned();byte[]? bytes=PointerBytes();return bytes==null?null:Verify(Parse(bytes).Active,cancellation);}
    public Task<InstalledVersion?> RecoverAsync(CancellationToken cancellation=default)=>Task.Run<InstalledVersion?>(()=>
    {
        RequireStopped();using var admission=EnterAdmission(mutation:true);using var lease=Lock();byte[]? bytes=PointerBytes();if(bytes==null)return null;var pointer=Parse(bytes);
        try{return Verify(pointer.Active,cancellation);}
        catch(Exception error)when(error is IOException or InvalidDataException or JsonException)
        {
            cancellation.ThrowIfCancellationRequested();if(pointer.Previous==null)throw new InstallationRecoveryException("No verified previous version; files retained.");var previous=Verify(pointer.Previous,cancellation);RequireStopped();cancellation.ThrowIfCancellationRequested();Publish(Encode(new(pointer.Previous,null)),bytes);return previous;
        }
    },cancellation);
    /// <summary>Cooperative launch admission, held from pointer read through actual startup settlement.
    /// Existing Windows entry points are not yet wired to this gate. Pending or unknown upgrade records fail closed.</summary>
    public IDisposable EnterRoleAdmission()=>EnterAdmission();
    internal InstallationAdmission EnterAdmission(InstallationExecutionOwnership? execution=null,bool mutation=false,bool existing=false)
    {
        var owner=InstallationAdmission.CreateOwner(directory);
        try{AcquireAdmission(owner,execution,mutation,existing);return owner;}catch(Exception error){owner.Fail(error);throw;}
    }
    internal void AcquireAdmission(InstallationAdmission owner,InstallationExecutionOwnership? execution=null,bool mutation=true,bool existing=true,bool requireStopped=false,bool keepSetup=false)
    {
        owner.RequireRoot(directory);
        if(requireStopped)RequireStopped();
        _=existing?OpenExistingSetup(directory,owner.OwnSetup):Lock(owner.OwnSetup);
        if(existing)InstallationHostActivity.Initialize(directory,false);owner.Acquire(mutation,existing);
        if(execution!=null)owner.Attach(execution);
        else if(File.Exists(InstallationExecutionOwnership.RecordPath(directory))||Directory.Exists(InstallationExecutionOwnership.RecordPath(directory)))throw new InstallationRecoveryException("Existing execution owner retained; launch and mutation blocked.");
        if(File.Exists(InstallationBrowserHostSession.PathFor(directory))||Directory.Exists(InstallationBrowserHostSession.PathFor(directory))||File.Exists(PendingPath)||Directory.Exists(PendingPath)||File.Exists(Path.Combine(directory,"handoff-pending.json"))||Directory.Exists(Path.Combine(directory,"handoff-pending.json"))||
            File.Exists(Path.Combine(directory,"host-registration-pending.json"))||Directory.Exists(Path.Combine(directory,"host-registration-pending.json"))||
            File.Exists(InstallationBootstrapDeployment.PendingPath(directory))||Directory.Exists(InstallationBootstrapDeployment.PendingPath(directory)))throw new InstallationRecoveryException("Pending or unknown upgrade/handoff/registration retained; launch and mutation blocked.");
        if(!keepSetup)owner.ReleaseSetup();
    }
    internal string PendingPath=>Path.Combine(directory,"upgrade-pending.json");
    internal InstallationExecutionOwnership ClaimExecution(InstallationAdmission admission)
    {admission.Require(directory);RequireOwned();var execution=new InstallationExecutionOwnership(directory);admission.Attach(execution);return execution;}
    internal InstallationAdmission ReenterUpgrade(byte[] expected,InstallationExecutionOwnership execution)
    {
        var admission=InstallationAdmission.CreateOwner(directory);
        try{_=Lock(admission.OwnSetup);admission.Acquire(true,false);admission.Attach(execution);RequireUpgradeRecord(expected);admission.ReleaseSetup();return admission;}
        catch(Exception error){admission.Fail(error);throw;}
    }
    internal void FinishUpgrade(InstallationAdmission admission,byte[] expected,Guid operation,string outcome)
    {
        admission.RequireMutation(directory);RequireOwned();RequireUpgradeRecord(expected);
        File.Move(PendingPath,Path.Combine(directory,"upgrade-finished-"+operation.ToString("N")+"-"+outcome+".json"));
    }
    private void RequireUpgradeRecord(byte[] expected)
    {if(!InstallationPackage.ReadDocument(PendingPath,16384).AsSpan().SequenceEqual(expected))throw new InstallationRecoveryException("Upgrade record changed; retained without overwrite.");}
    internal byte[]? SnapshotRecord(InstallationAdmission admission){admission.Require(directory);RequireOwned();return PointerBytes();}
    internal void RestoreForUpgrade(InstallationAdmission admission,byte[]? before,byte[] expected)
    {admission.RequireMutation(directory);using var lease=Lock();RestoreRecord(before,expected);}
    private void RestoreRecord(byte[]? before,byte[] expected)
    {
        RequireStopped();if(before!=null)_=Verify(Parse(before).Active,CancellationToken.None);
        if(before!=null)Publish(before,expected);
        else{RequirePointer(expected);File.Move(CurrentPath,Path.Combine(directory,"failed-current-"+Guid.NewGuid().ToString("N")+".json"));}
    }
    private InstalledVersion Verify(Reference reference,CancellationToken cancellation)
    {
        string root=Path.Combine(directory,"versions",reference.Id);var package=InstallationPackage.ReadInstalled(root,cancellation,expectedDigest:reference.CatalogSha256);if(!string.Equals(package.CatalogSha256,reference.CatalogSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Version catalog hash mismatch.");return new(package.Manifest.Version,root,package.CatalogSha256);
    }
    private string CurrentPath=>Path.Combine(directory,"current.json");
    private byte[]? PointerBytes(){InstallationPaths.Check(CurrentPath);if(Directory.Exists(CurrentPath))throw new InstallationRecoveryException("Version record is a directory.");return File.Exists(CurrentPath)?InstallationPackage.ReadDocument(CurrentPath,4096):null;}
    private void RequirePointer(byte[]? expected)
    {var actual=PointerBytes();if(expected==null?actual!=null:actual==null||!expected.AsSpan().SequenceEqual(actual))throw new InstallationRecoveryException("Version record changed; retained without overwrite.");}
    private void Publish(byte[] bytes,byte[]? expected)
    {
        RequireOwned();RequirePointer(expected);string temporary=Path.Combine(directory,"current-"+Guid.NewGuid().ToString("N")+".tmp");SettingsStore.WriteFlushed(temporary,bytes);InstallationPaths.Check(directory);RequirePointer(expected);
        if(expected==null)File.Move(temporary,CurrentPath);else File.Replace(temporary,CurrentPath,null,false);
    }
    private FileStream Lock(Action<FileStream>? own=null)
    {
        InstallationPaths.Check(directory);Directory.CreateDirectory(directory);bool createdOwner=false;string marker=Path.Combine(directory,"owner.txt");
        if(!File.Exists(marker))
        {
            if(Directory.EnumerateFileSystemEntries(directory).Any())throw new InstallationRecoveryException("Unknown installation directory retained.");SettingsStore.WriteFlushed(marker,Encoding.UTF8.GetBytes(Owner));createdOwner=true;
        }
        RequireOwned();string path=Path.Combine(directory,"installation.lock");InstallationPaths.Check(path);FileStream lease;
        try{lease=new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){throw new InstallationBusyException();}
        if(own!=null){own(lease);if(lease.Length!=0)throw new InstallationRecoveryException("Unknown lock bytes retained.");InstallationHostActivity.Initialize(directory,createdOwner);return lease;}
        try{if(lease.Length!=0)throw new InstallationRecoveryException("Unknown lock bytes retained.");InstallationHostActivity.Initialize(directory,createdOwner);return lease;}
        catch(Exception primary){InstallationAdmission.CloseUnpublished(lease,primary);throw;}
    }
    private void RequireOwned()
    {InstallationPaths.Check(directory);string marker=Path.Combine(directory,"owner.txt");if(!File.Exists(marker)||!InstallationPackage.ReadDocument(marker,128).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Owner)))throw new InstallationRecoveryException("Unknown installation owner retained.");}
    private void RequireStopped(){if(!rolesStopped())throw new InstallationBusyException();}
    private static byte[] Encode(Pointer pointer)=>JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,active=Write(pointer.Active),previous=pointer.Previous==null?null:Write(pointer.Previous)});
    internal static (byte[] Target,InstalledVersion? Before) PlanCurrent(string root,byte[]? before,InstalledVersion next,CancellationToken cancellation,InstallationPreparationIO io)
    {
        string id=Path.GetFileName(next.DirectoryPath);if(!Guid.TryParseExact(id,"N",out _)||next.DirectoryPath!=Path.Combine(root,"versions",id))throw new InstallationRecoveryException("Next generation is outside the original root.");
        var prior=before==null?null:Parse(before);InstalledVersion? old=null;
        if(prior!=null)
        {
            string directory=Path.Combine(root,"versions",prior.Active.Id);var package=InstallationPackage.ReadInstalled(directory,cancellation,expectedDigest:prior.Active.CatalogSha256,io:io);
            if(package.Schema!=3)throw new InstallationRecoveryException("Previous current is not a qualified v3 payload.");old=new(package.Manifest.Version,directory,package.CatalogSha256);
        }
        byte[] target=Encode(new(new(id,next.CatalogSha256),prior?.Active));if(target.Length>4096)throw new InvalidDataException("Target current exceeds consumer bound.");return(target,old);
    }
    private static object Write(Reference reference)=>new{id=reference.Id,catalogSha256=reference.CatalogSha256};
    private static Pointer Parse(byte[] bytes)
    {
        using var document=JsonDocument.Parse(bytes,new(){MaxDepth=4});var root=document.RootElement;InstallationPackage.Fields(root,"schemaVersion","active","previous");if(root.GetProperty("schemaVersion").GetInt32()!=1)throw new InstallationRecoveryException("Unsupported installation record retained.");return new(Read(root.GetProperty("active")),root.GetProperty("previous").ValueKind==JsonValueKind.Null?null:Read(root.GetProperty("previous")));
    }
    private static Reference Read(JsonElement item)
    {
        InstallationPackage.Fields(item,"id","catalogSha256");string id=item.GetProperty("id").GetString()??"",hash=item.GetProperty("catalogSha256").GetString()??"";if(!Guid.TryParseExact(id,"N",out _)||!InstallationPackage.HashValid(hash))throw new InstallationRecoveryException("Invalid installation reference retained.");return new(id,hash);
    }
}
