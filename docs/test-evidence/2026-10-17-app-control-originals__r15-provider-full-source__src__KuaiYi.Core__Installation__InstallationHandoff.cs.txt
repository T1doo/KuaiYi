using System.Diagnostics;
using System.Text.Json;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

/// <summary>Private control protocol for the exact child the controller started. No PID adoption.
/// CompleteControlAsync must close stdin and cancel/join the original stdout/stderr readers and cancellation work.
/// It is distinct from SettleAsync, which still requires actual child exit.</summary>
public interface IInstallationHandoffProtocol:IInstallationApplicationProtocol
{
    Task PrepareHandoffAsync(Process owned,InstallationHandoffOffer offer,CancellationToken cancellation);
    Task AcceptHandoffAsync(Process owned,InstallationHandoffOffer offer,CancellationToken cancellation);
    Task CompleteControlAsync(Process owned,InstallationHandoffOffer offer,CancellationToken cancellation);
}
public enum InstallationHandoffStatus { None,Committed,CommittedUnconfirmed,CommitUncertain }

/// <summary>A bounded correlation offer, never a credential or authority to control another process.</summary>
public sealed record InstallationHandoffOffer(Guid Transfer,Guid Receiver,Guid Launch,string Root,string UserData,
    InstalledVersion Version,string OwnerRecord,string CurrentRecord,string UpgradeRecord)
{
    public string Encode()=>Convert.ToBase64String(Bytes());
    internal byte[] Bytes()=>JsonSerializer.SerializeToUtf8Bytes(this);
    public static InstallationHandoffOffer Decode(string encoded)
    {
        if(encoded.Length>65536)throw new InvalidDataException("Handoff offer too large.");
        byte[] bytes=Convert.FromBase64String(encoded);
        var offer=JsonSerializer.Deserialize<InstallationHandoffOffer>(bytes)??throw new InvalidDataException("Missing handoff offer.");
        if(bytes.Length>49152||!bytes.AsSpan().SequenceEqual(offer.Bytes()))throw new InvalidDataException("Noncanonical or unknown handoff offer retained.");
        offer.Validate();return offer;
    }
    internal void Validate()
    {
        if(Transfer==Guid.Empty||Receiver==Guid.Empty||Launch==Guid.Empty||Root!=InstallationPaths.Root(Root)||UserData!=InstallationPaths.Root(UserData)||Version==null)throw new InvalidDataException("Invalid handoff identity.");
        if(Convert.FromBase64String(OwnerRecord).Length>1024||Convert.FromBase64String(CurrentRecord).Length>4096||Convert.FromBase64String(UpgradeRecord).Length>16384)throw new InvalidDataException("Handoff records too large.");
    }
    internal byte[] Ledger(string phase)=>JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",phase,offer=this});
    internal string PathFor(string name)=>Path.Combine(Root,name);
    internal void RequireRecords()
    {
        Validate();Exact("execution-owner.json",Convert.FromBase64String(OwnerRecord),1024);
        Exact("current.json",Convert.FromBase64String(CurrentRecord),4096);
        Exact("upgrade-pending.json",Convert.FromBase64String(UpgradeRecord),16384);
        if(new InstallationStore(Root,UserData,()=>false).ReadActive()!=Version)throw new InstallationRecoveryException("Selected handoff generation changed.");
    }
    internal void Exact(string name,byte[] expected,int limit=65536)
    {if(!InstallationPackage.ReadDocument(PathFor(name),limit).AsSpan().SequenceEqual(expected))throw new InstallationRecoveryException("Handoff record changed; retained without overwrite.");}
}

// Only the already-held admission and exact records authorize this narrow transition. Ordinary store gates stay closed.
internal sealed class InstallationHandoffTransaction(InstallationHandoffOffer offer)
{
    internal InstallationHandoffOffer Offer{get;}=offer;
    internal bool CommitAttempted{get;private set;}
    internal bool Committed{get;private set;}
    internal void Prepare()
    {Offer.RequireRecords();InstallationPaths.Check(Offer.PathFor("handoff-pending.json"));SettingsStore.WriteFlushed(Offer.PathFor("handoff-pending.json"),Offer.Ledger("Prepared"));}
    internal void Commit(InstallationExecutionOwnership execution)
    {
        Offer.RequireRecords();Offer.Exact("handoff-pending.json",Offer.Ledger("Prepared"));
        // The receiver must actually hold the same OS lease, not merely print an ACK.
        execution.RequireReceiverLease();
        string temporary=Offer.PathFor("handoff-"+Offer.Transfer.ToString("N")+".tmp");
        SettingsStore.WriteFlushed(temporary,Offer.Ledger("Committed"));
        Offer.RequireRecords();Offer.Exact("handoff-pending.json",Offer.Ledger("Prepared"));
        CommitAttempted=true;
        try{File.Replace(temporary,Offer.PathFor("handoff-pending.json"),null,false);Committed=true;}
        catch
        {
            // A failed syscall is not proof that publication did not happen. Only exact known Prepared permits rollback.
            if(Is("Committed"))Committed=true;
            else if(Is("Prepared"))CommitAttempted=false;
            throw;
        }
    }
    private bool Is(string phase)
    {try{Offer.Exact("handoff-pending.json",Offer.Ledger(phase));return true;}catch(IOException){return false;}}
    internal void Reclaim(InstallationExecutionOwnership execution)
    {
        if(CommitAttempted)throw new InstallationRecoveryException("Committed or uncertain handoff cannot be reclaimed.");
        Offer.RequireRecords();Offer.Exact("handoff-pending.json",Offer.Ledger("Prepared"));
        execution.Resume(); // Caller has already confirmed actual exit and original IO/task settlement of its exact child.
        Offer.RequireRecords();Offer.Exact("handoff-pending.json",Offer.Ledger("Prepared"));
        File.Move(Offer.PathFor("handoff-pending.json"),Offer.PathFor("handoff-aborted-"+Offer.Transfer.ToString("N")+".json"));
    }
}

/// <summary>Receiver-side lifetime. Only the selected executable's own process may prepare and acquire.
/// After commit keep this object rooted and its OS lease until process exit. There is deliberately no committed-release API:
/// a process cannot prove its own exit. Persistent records remain, so a fresh controller never auto-adopts a stale runtime.</summary>
public sealed class InstallationRuntimeOwnership
{
    private readonly InstallationHandoffOffer offer;
    private FileStream? lease;
    private bool committed;
    private bool abortRequested;
    private InstallationRuntimeOwnership(InstallationHandoffOffer offer){this.offer=offer;}
    public static InstallationRuntimeOwnership Prepare(InstallationHandoffOffer offer,Guid ownLaunch)
    {
        if(offer.Launch!=ownLaunch||Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)!=offer.Version.DirectoryPath)throw new InstallationRecoveryException("Receiver is not the selected launched generation.");
        offer.RequireRecords();offer.Exact("handoff-pending.json",offer.Ledger("Prepared"));return new(offer);
    }
    public void Acquire()
    {
        if(lease!=null||abortRequested)throw new InvalidOperationException("Receiver lease already acquired or abort requested.");
        offer.RequireRecords();offer.Exact("handoff-pending.json",offer.Ledger("Prepared"));
        string path=offer.PathFor("execution-owner.lock");InstallationPaths.Check(path);
        var held=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        try{if(held.Length!=0)throw new InstallationRecoveryException("Unknown execution lock bytes retained.");offer.RequireRecords();offer.Exact("handoff-pending.json",offer.Ledger("Prepared"));lease=held;}
        catch{held.Dispose();throw;}
    }
    public void ConfirmCommitted()
    {
        if(lease==null||abortRequested)throw new InstallationRecoveryException("Receiver does not hold an accepted execution lease.");
        offer.RequireRecords();offer.Exact("handoff-pending.json",offer.Ledger("Committed"));committed=true;
    }
    public void AbortBeforeCommit()
    {
        if(committed)throw new InstallationRecoveryException("Committed receiver must keep its lifetime lease until OS exit.");
        offer.Exact("handoff-pending.json",offer.Ledger("Prepared"));abortRequested=true;
        // Even a Prepared read can race a parent commit. Only OS exit may release this lease.
    }
}
