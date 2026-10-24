using System.Text.Json;
using System.Runtime.ExceptionServices;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

internal sealed class InstallationAdmissionFaults
{
    internal Action<InstallationAdmission>? OwnerCreated {get;init;}
    internal Action? Acquired {get;init;}
    internal Action? BeforeClose {get;set;}
    internal Action<string>? AcquiredLease {get;init;}
    internal Action<string>? ClosingLease {get;set;}
}
internal static class InstallationErrors
{
    internal static void Throw(IReadOnlyList<Exception> errors)
    {
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException(errors);
    }
}
// The owner exists before setup/host/upgrade acquisition. Failed closes retain these exact resources.
internal sealed class InstallationAdmission:IDisposable
{
    private static readonly HashSet<InstallationAdmission> Retained=[];
    private static readonly HashSet<FileStream> UnclosedSetup=[];
    internal static void CloseUnpublished(FileStream original,Exception primary)
    {
        var errors=new List<Exception>{primary};
        try{original.Dispose();}catch(Exception close){lock(UnclosedSetup)UnclosedSetup.Add(original);errors.Add(close);}
        InstallationErrors.Throw(errors);
    }
    private readonly string directory;
    private readonly InstallationAdmissionFaults? faults;
    private FileStream? setup,lease;
    private InstallationExecutionOwnership? execution;
    private InstallationPreparationIO? executionIO;
    private InstallationHostActivity? mutation;
    private bool started,acquired;
    private InstallationAdmission(string directory,InstallationAdmissionFaults? faults)
    {this.directory=directory;this.faults=faults;faults?.OwnerCreated?.Invoke(this);}
    internal static InstallationAdmission CreateOwner(string directory,InstallationAdmissionFaults? faults=null)=>new(directory,faults);
    internal InstallationAdmission(string directory,bool mutation=false,bool existing=false,InstallationAdmissionFaults? faults=null):this(directory,faults)
    {
        try{Acquire(mutation,existing);}catch(Exception error){Fail(error);throw;}
    }
    internal bool CleanupConfirmed=>setup==null&&lease==null&&mutation==null;
    internal void RequireRoot(string expected)
    {if(directory!=expected)throw new InstallationRecoveryException("Admission belongs to another installation.");}
    internal void OwnSetup(FileStream value)
    {
        if(setup!=null)throw new InvalidOperationException("Setup already owned.");
        setup=value;faults?.AcquiredLease?.Invoke("setup");
    }
    internal void Acquire(bool exclusive,bool existing)
    {
        if(started)throw new InvalidOperationException("Admission acquisition is one-shot.");started=true;
        if(exclusive){mutation=InstallationHostActivity.CreateOwner(directory);mutation.Acquire(true);faults?.AcquiredLease?.Invoke("host");}
        string path=Path.Combine(directory,"upgrade.lock");InstallationPaths.Check(path);
        try{lease=new(path,existing?FileMode.Open:FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException){throw new InstallationBusyException();}
        faults?.AcquiredLease?.Invoke("upgrade");
        if(lease.Length!=0)throw new InstallationRecoveryException("Unknown upgrade lock bytes retained.");
        faults?.Acquired?.Invoke();acquired=true;
    }
    internal void ReleaseSetup()
    {if(setup!=null){faults?.ClosingLease?.Invoke("setup");setup.Dispose();setup=null;}}
    internal void Fail(Exception primary)
    {
        var errors=new List<Exception>{primary};try{Dispose();}catch(Exception close){errors.Add(close);}
        InstallationErrors.Throw(errors);
    }
    internal void RequireCleanupLease(string expected)
    {if(!acquired||lease==null||directory!=expected)throw new InstallationRecoveryException("Upgrade admission is no longer held.");InstallationPaths.Check(directory);}
    internal void Require(string expected){RequireCleanupLease(expected);if(executionIO!=null)execution?.RequireCompensation(directory,executionIO);else execution?.Require(directory);}
    internal void RequireObservedExecution(string expected,InstallationPreparationIO originalIo)
    {RequireCleanupLease(expected);(execution??throw new InstallationRecoveryException("Original execution is missing.")).RequireCompensation(directory,originalIo);}
    internal void RequireMutation(string expected){Require(expected);(mutation??throw new InstallationRecoveryException("Mutation requires the exclusive host fence.")).Require(expected);}
    internal void RequireRetirementCleanup(string expected){RequireCleanupLease(expected);(mutation??throw new InstallationRecoveryException("Retirement requires the original exclusive host fence.")).Require(expected);}
    internal void Attach(InstallationExecutionOwnership owner,InstallationPreparationIO? reader=null){if(reader==null)owner.Require(directory);else owner.RequireCompensation(directory,reader);execution=owner;executionIO=reader;}
    public void Dispose()
    {
        var errors=new List<Exception>();
        if(lease!=null)try{faults?.BeforeClose?.Invoke();faults?.ClosingLease?.Invoke("upgrade");lease.Dispose();lease=null;acquired=false;}catch(Exception error){errors.Add(error);}
        if(mutation!=null)try{faults?.ClosingLease?.Invoke("host");mutation.Dispose();mutation=null;}catch(Exception error){errors.Add(error);}
        // Setup stays held if any earlier close is unknown; no admission can pass the setup fence.
        if(lease==null&&mutation==null)try{ReleaseSetup();}catch(Exception error){errors.Add(error);}
        lock(Retained){if(CleanupConfirmed)Retained.Remove(this);else Retained.Add(this);}
        InstallationErrors.Throw(errors);
    }
}
// Before the first actual Start, hold an OS-released lease AND a persistent, exact-byte owned record.
// A fresh controller cannot clear a crashed controller's record or infer ownership from a PID.
internal sealed class InstallationExecutionFaults
{
    internal Action? Acquired {get;init;}
    internal Action? BeforeClose {get;set;}
}
internal sealed class InstallationExecutionOwnership:IDisposable
{
    private readonly string directory;
    private readonly Guid id=Guid.NewGuid();
    private readonly byte[] record;
    private FileStream? lease;
    private Retirement? retirement;
    private readonly InstallationPreparationIO? ownedIO;
    private readonly InstallationExecutionFaults? faults;
    private bool acquireAttempted;
    internal bool Acquired {get;private set;}
    internal bool LeaseCloseUnconfirmed {get;private set;}
    internal static string RecordPath(string directory)=>Path.Combine(directory,"execution-owner.json");
    internal string DirectoryPath=>directory;
    internal byte[] RecordBytes=>record.ToArray();
    private string FinishedPath=>Path.Combine(directory,"execution-finished-"+id.ToString("N")+".json");
    private InstallationExecutionOwnership(string directory,InstallationPreparationIO? io,InstallationExecutionFaults? faults)
    {this.directory=directory;ownedIO=io?.CreateSessionChannel(directory,[RecordPath(directory),FinishedPath]);this.faults=faults;record=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",owner=id.ToString("N"),phase="Owned"});}
    internal static InstallationExecutionOwnership CreateOwner(string directory,InstallationPreparationIO io,InstallationExecutionFaults? faults=null)=>new(directory,io,faults);
    internal InstallationExecutionOwnership(string directory):this(directory,null,null)
    {try{Acquire();}catch{Dispose();throw;}}
    // Restored sessions publish this object before entering here. Failed acquisition never drops its original lease.
    internal void Acquire()
    {
        if(acquireAttempted)throw new InstallationRecoveryException("Execution acquisition is one-shot.");acquireAttempted=true;
        string path=Path.Combine(directory,"execution-owner.lock");InstallationPaths.Check(path);
        try{lease=new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){throw new InstallationBusyException();}
        faults?.Acquired?.Invoke();
        if(lease.Length!=0)throw new InstallationRecoveryException("Unknown execution lock bytes retained.");
        string marker=RecordPath(directory);InstallationPaths.Check(marker);
        if(File.Exists(marker)||Directory.Exists(marker))throw new InstallationRecoveryException("Existing execution owner retained; no process ownership inferred.");
        if(ownedIO==null)SettingsStore.WriteFlushed(marker,record);
        else{ownedIO.WritePublicationMetadata(marker,record,1024);ownedIO.RequireExactMetadata(marker,record,1024);}
        Acquired=true;
    }
    internal void Require(string expected)
    {
        if(retirement!=null||!Acquired||lease==null||directory!=expected)throw new InstallationRecoveryException("Execution ownership is unconfirmed; files retained.");
        if(ownedIO!=null)ownedIO.RequireExactMetadata(RecordPath(directory),record,1024);
        else if(!InstallationPackage.ReadDocument(RecordPath(directory),1024).AsSpan().SequenceEqual(record))throw new InstallationRecoveryException("Execution ownership is unconfirmed; files retained.");
    }
    internal void RequireCompensation(string expected,InstallationPreparationIO io)
    {if(retirement!=null||lease==null||directory!=expected)throw new InstallationRecoveryException("Original live execution lease is unavailable.");io.RequireExactMetadata(RecordPath(directory),record,1024);}
    internal Retirement PrepareRetirement(InstallationDeploymentCompensation owner,InstallationPreparationIO io)
    {owner.RequireOriginalExecution(this);RequireCompensation(directory,io);if(retirement!=null)throw new InstallationRecoveryException("Retirement is one-shot.");return retirement=new(this,owner);}
    // This exact owner exists before Move. Once created, ordinary live mutation can never be regained.
    internal sealed class Retirement
    {
        private readonly InstallationExecutionOwnership original;
        private readonly InstallationDeploymentCompensation owner;
        private bool attempted,moved,moveConfirmed;
        internal Retirement(InstallationExecutionOwnership original,InstallationDeploymentCompensation owner){this.original=original;this.owner=owner;}
        internal bool CleanupConfirmed=>moveConfirmed&&original.lease==null;
        internal bool MoveConfirmed=>moveConfirmed;
        internal void Move(InstallationPreparationIO io,Action<string,string>? move)
        {
            if(attempted)throw new InstallationRecoveryException("Execution retirement cannot be replayed.");attempted=true;
            owner.RequireRetirement(this,original);io.RequireExactMetadata(RecordPath(original.directory),original.record,1024);io.RequireAbsent(owner.RetiredPath);
            if(move!=null)move(RecordPath(original.directory),owner.RetiredPath);else File.Move(RecordPath(original.directory),owner.RetiredPath);moved=true;Verify(io,true);moveConfirmed=true;
        }
        internal void Verify(InstallationPreparationIO io,bool held)
        {owner.RequireRetirement(this,original);if(!moved||held&&original.lease==null||!held&&original.lease!=null)throw new InstallationRecoveryException("Original execution retirement is unconfirmed.");io.RequireAbsent(RecordPath(original.directory));io.RequireExactMetadata(owner.RetiredPath,original.record,1024);}
        internal void CloseLease(Action? beforeClose)
        {owner.RequireRetirement(this,original);if(!moveConfirmed)throw new InstallationRecoveryException("Cannot release an unconfirmed execution retirement.");beforeClose?.Invoke();original.Dispose();}
    }
    internal void Finish()
    {Require(directory);if(ownedIO!=null)ownedIO.MovePublicationRecord(RecordPath(directory),FinishedPath);else File.Move(RecordPath(directory),FinishedPath);}
    internal void Suspend(){Require(directory);lease!.Dispose();lease=null;}
    internal void Resume()
    {
        if(lease!=null){Require(directory);return;}
        string path=Path.Combine(directory,"execution-owner.lock");InstallationPaths.Check(path);
        var held=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
        try{if(held.Length!=0)throw new InstallationRecoveryException("Unknown execution lock bytes retained.");lease=held;Require(directory);}
        catch{held.Dispose();lease=null;throw;}
    }
    internal void RequireReceiverLease()
    {
        if(lease!=null)throw new InstallationRecoveryException("Controller still owns the execution lease.");
        string path=Path.Combine(directory,"execution-owner.lock");InstallationPaths.Check(path);
        FileStream probe;
        try{probe=new(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None);}
        catch(IOException error)when(error is not FileNotFoundException and not DirectoryNotFoundException&&File.Exists(path)&&
            (error.HResult&0xffff) is 11 or 32 or 33){return;} // Only a sharing/lock conflict; an absent path or other IO error proves nothing.
        probe.Dispose();throw new InstallationRecoveryException("Receiver execution lease was not held.");
    }
    public void Dispose(){if(lease!=null)try{faults?.BeforeClose?.Invoke();lease.Dispose();lease=null;}catch{LeaseCloseUnconfirmed=true;throw;}}
}
