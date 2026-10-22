using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Runtime.ExceptionServices;
namespace KuaiYi.Core.Installation;

// Cooperative lifecycle fence, never a replacement for the real connected peer, origin or image grant.
// NativeHost does not create it, inherit handles, discover processes or infer an owner from a PID.
internal sealed class InstallationHostActivity:IDisposable
{
    private readonly string root;
    private FileStream? lease;
    private static readonly HashSet<InstallationHostActivity> Retained=[];
    private InstallationHostActivity(string root){this.root=root;}
    internal static InstallationHostActivity CreateOwner(string root)=>new(root);
    internal static string PathFor(string root)=>Path.Combine(root,"host-activity.lock");
    internal static void Initialize(string root,bool newStore)
    {
        string path=PathFor(root);InstallationPaths.Check(path);
        if(Directory.Exists(path))throw new InstallationRecoveryException("Unknown host activity directory retained.");
        if(File.Exists(path))return;
        // An older store may have hosts that never participated. No automatic migration can prove their exit.
        if(!newStore)throw new InstallationRecoveryException("Existing installation lacks host lifecycle admission; explicit quiescence outside this protocol is required.");
        var owner=CreateOwner(root);
        try{owner.lease=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);owner.Dispose();}
        catch(Exception primary){var errors=new List<Exception>{primary};try{owner.Dispose();}catch(Exception close){errors.Add(close);}InstallationErrors.Throw(errors);throw;}
    }
    internal static InstallationHostActivity Open(string root,bool exclusive)
    {
        var owner=CreateOwner(root);
        try{owner.Acquire(exclusive);return owner;}
        catch(Exception primary){var errors=new List<Exception>{primary};try{owner.Dispose();}catch(Exception close){errors.Add(close);}InstallationErrors.Throw(errors);throw;}
    }
    internal void Acquire(bool exclusive)
    {
        if(lease!=null)throw new InvalidOperationException("Host activity already owned.");
        string path=PathFor(root);InstallationPaths.Check(path);
        try{lease=new(path,FileMode.Open,exclusive?FileAccess.ReadWrite:FileAccess.Read,exclusive?FileShare.None:FileShare.Read);}
        catch(IOException error)when(error is not FileNotFoundException and not DirectoryNotFoundException&&(error.HResult&0xffff) is 11 or 32 or 33){throw new InstallationBusyException();}
        if(lease.Length!=0)throw new InstallationRecoveryException("Unknown host activity bytes retained.");
    }
    internal void Require(string expected){if(root!=expected||lease==null||lease.Length!=0)throw new InstallationRecoveryException("Host activity lease is unconfirmed.");}
    public void Dispose()
    {
        try{lease?.Dispose();lease=null;}
        finally{lock(Retained){if(lease==null)Retained.Remove(this);else Retained.Add(this);}}
    }
}
public sealed partial class InstallationStore
{
    // Host entry never initializes a store, lock, owner or session. Acquire setup only for bounded ingress work.
    internal static FileStream OpenExistingSetup(string root,Action<FileStream>? own=null)
    {
        InstallationPaths.Check(root);string marker=Path.Combine(root,"owner.txt");
        if(!InstallationPackage.ReadDocument(marker,128).AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(Owner)))throw new InstallationRecoveryException("Unknown installation owner retained.");
        string path=Path.Combine(root,"installation.lock");InstallationPaths.Check(path);FileStream lease;
        try{lease=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}catch(IOException error)when(error is not FileNotFoundException and not DirectoryNotFoundException&&(error.HResult&0xffff) is 11 or 32 or 33){throw new InstallationBusyException();}
        if(own!=null){own(lease);if(lease.Length!=0)throw new InstallationRecoveryException("Unknown setup lock bytes retained.");return lease;}
        try{if(lease.Length!=0)throw new InstallationRecoveryException("Unknown setup lock bytes retained.");return lease;}
        catch(Exception primary){InstallationAdmission.CloseUnpublished(lease,primary);throw;}
    }
    internal InstallationAdmission ReenterHostMutation()
    {
        var owner=InstallationAdmission.CreateOwner(directory);
        try{_=Lock(owner.OwnSetup);owner.Acquire(true,false);owner.ReleaseSetup();return owner;}catch(Exception error){owner.Fail(error);throw;}
    }
}
// Only the controller holding the exact Start-returned child may publish/retire this normal-session record.
// Fixed record is evidence of that session, not an external request capability or an authorization token.
internal sealed class InstallationBrowserHostSession
{
    private readonly InstallationControlledLaunch proof;
    private readonly InstallationAdmission admission;
    private readonly InstallationPreparationIO? io;
    private bool retired,closingWritten;
    private readonly byte[] closing;
    private static readonly List<FileStream> UnclosedRecords=[];
    private InstallationBrowserHostSession(InstallationControlledLaunch proof,InstallationAdmission admission,InstallationPreparationIO? io)
    {this.proof=proof;this.admission=admission;this.io=io;closing=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",launch=proof.Launch.ToString("N"),phase="Closing"});}
    internal static string PathFor(string root)=>Path.Combine(root,"browser-host-session.json");
    internal static string ClosingPath(InstallationControlledLaunch proof)=>Path.Combine(proof.Root,$"browser-host-closing-{proof.Launch:N}.json");
    internal static void RequireNotClosing(InstallationControlledLaunch proof)
    {string path=ClosingPath(proof);InstallationPaths.Check(path);if(File.Exists(path)||Directory.Exists(path))throw new InstallationRecoveryException("Original browser host session is closing.");}
    private static void WriteNewRecord(string path,byte[] bytes,InstallationPreparationIO? io=null)
    {
        if(io!=null){io.WritePublicationMetadata(path,bytes,65536);return;}
        InstallationPaths.Check(path);var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);List<Exception> errors=[];
        try{file.Write(bytes);file.Flush(true);}catch(Exception error){errors.Add(error);}
        try{file.Dispose();}catch(Exception error){errors.Add(error);lock(UnclosedRecords)UnclosedRecords.Add(file);}
        // Partial records are retained, never mistaken for a completed publication or automatically deleted.
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException(errors);
    }
    internal static InstallationBrowserHostSession Publish(InstallationControlledLaunch proof,InstallationAdmission admission,Process original,InstallationPreparationIO? io=null)
    {
        admission.Require(proof.Root);proof.RequireImageRecords(io);
        if(original.HasExited)throw new InstallationRecoveryException("Original session child has exited.");
        var session=new InstallationBrowserHostSession(proof,admission,io);
        admission.Require(proof.Root);proof.RequireImageRecords(io);RequireNotClosing(proof);
        string path=PathFor(proof.Root);InstallationPaths.Check(path);
        string prepared=Path.Combine(proof.Root,$"browser-host-prepared-{proof.Launch:N}.json");
        WriteNewRecord(prepared,proof.Bytes(),io);
        admission.Require(proof.Root);proof.RequireImageRecords(io);RequireNotClosing(proof);
        if(original.HasExited)throw new InstallationRecoveryException("Original session child has exited.");
        // The original role admission already excludes every cooperating writer. Publish only fully closed bytes.
        if(io!=null)io.MovePublicationRecord(prepared,path);else File.Move(prepared,path);return session;
    }
    internal void Retire()
    {
        if(retired)return;
        // Cleanup of this exact session never authorizes work under a replaced execution-owner record.
        admission.RequireCleanupLease(proof.Root);
        if(!closingWritten){WriteNewRecord(ClosingPath(proof),closing,io);closingWritten=true;}
        else InstallationControlledLaunch.Exact(ClosingPath(proof),closing,io:io);
        InstallationControlledLaunch.Exact(PathFor(proof.Root),proof.Bytes(),io:io);
        string destination=Path.Combine(proof.Root,$"browser-host-finished-{proof.Launch:N}.json");InstallationPaths.Check(destination);
        if(io!=null)io.MovePublicationRecord(PathFor(proof.Root),destination);else File.Move(PathFor(proof.Root),destination);retired=true;
    }
}
internal sealed class InstallationBrowserHostEntry
{
    // Deliberately rooted until OS teardown, including when BrowserSession or its cancellation waiter returns first.
    // There is no successful-entry Dispose: only the original process's actual exit releases its shared activity handle.
    private static readonly List<InstallationBrowserHostEntry> ProcessLifetime=[];
    private readonly InstallationHostActivity activity;
    private readonly InstallationControlledLaunch proof;
    private readonly byte[] sessionBytes;
    private int invalid;
    private InstallationBrowserHostEntry(InstallationHostActivity activity,InstallationControlledLaunch proof,byte[] bytes){this.activity=activity;this.proof=proof;sessionBytes=bytes;}
    internal static InstallationBrowserHostEntry Enter(string executableDirectory)
    {
        string selected=InstallationPaths.Root(executableDirectory);
        string root=InstallationNormalEntry.FindInstallationRoot(selected)??throw new InstallationRecoveryException("Browser host requires an installed generation.");
        InstallationHostActivity? activity=null;
        try
        {
            InstallationBrowserHostEntry entry;
            using(var setup=InstallationStore.OpenExistingSetup(root))
            {
                activity=InstallationHostActivity.Open(root,false);
                byte[] bytes=InstallationPackage.ReadDocument(InstallationBrowserHostSession.PathFor(root),49152);
                var proof=InstallationControlledLaunch.Decode(Convert.ToBase64String(bytes));
                if(proof.Root!=root||proof.Version.DirectoryPath!=selected||proof.UpgradeRecord!=null)throw new InstallationRecoveryException("Browser host session generation mismatch.");
                entry=new(activity,proof,bytes);entry.RequireCurrent();
            }
            // Full package verification is outside the short setup section, protected against mutation by activity.
            entry.proof.RequireRecords();entry.RequireCurrent();
            lock(ProcessLifetime)ProcessLifetime.Add(entry);activity=null;return entry;
        }
        finally{activity?.Dispose();}
    }
    internal void RequireCurrent()
    {
        if(Volatile.Read(ref invalid)!=0)throw new InstallationRecoveryException("Browser host session is no longer current.");
        try
        {
            activity.Require(proof.Root);InstallationBrowserHostSession.RequireNotClosing(proof);InstallationControlledLaunch.Exact(InstallationBrowserHostSession.PathFor(proof.Root),sessionBytes,49152);
            proof.RequireImageRecords();
        }
        catch{Interlocked.Exchange(ref invalid,1);throw;}
    }
}
