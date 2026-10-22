using System.Text.Json;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core.Installation;

// Product-local cooperative fence. No PID, OS privilege, or ownership is inferred from a pathname.
internal sealed class InstallationAdmission:IDisposable
{
    private readonly string directory;
    private FileStream? lease;
    private InstallationExecutionOwnership? execution;
    private InstallationHostActivity? mutation;
    internal InstallationAdmission(string directory,bool mutation=false,bool existing=false)
    {
        this.directory=directory;
        try
        {
            this.mutation=mutation?InstallationHostActivity.Open(directory,true):null;
            string path=Path.Combine(directory,"upgrade.lock");InstallationPaths.Check(path);
            try{lease=new(path,existing?FileMode.Open:FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException){throw new InstallationBusyException();}
            if(lease.Length!=0)throw new InstallationRecoveryException("Unknown upgrade lock bytes retained.");
        }
        catch{Dispose();throw;}
    }

    internal void RequireCleanupLease(string expected)
    {if(lease==null||directory!=expected)throw new InstallationRecoveryException("Upgrade admission is no longer held.");InstallationPaths.Check(directory);}
    internal void Require(string expected){RequireCleanupLease(expected);execution?.Require(directory);}
    internal void RequireMutation(string expected){Require(expected);(mutation??throw new InstallationRecoveryException("Mutation requires the exclusive host fence.")).Require(expected);}
    internal void Attach(InstallationExecutionOwnership owner){owner.Require(directory);execution=owner;}
    public void Dispose(){lease?.Dispose();lease=null;mutation?.Dispose();mutation=null;}
}
// Before the first actual Start, hold an OS-released lease AND a persistent, exact-byte owned record.
// A fresh controller cannot clear a crashed controller's record or infer ownership from a PID.
internal sealed class InstallationExecutionOwnership:IDisposable
{
    private readonly string directory;
    private readonly Guid id=Guid.NewGuid();
    private readonly byte[] record;
    private FileStream? lease;
    internal static string RecordPath(string directory)=>Path.Combine(directory,"execution-owner.json");
    internal string DirectoryPath=>directory;
    internal byte[] RecordBytes=>record.ToArray();
    internal InstallationExecutionOwnership(string directory)
    {
        this.directory=directory;record=JsonSerializer.SerializeToUtf8Bytes(new{schemaVersion=1,product="KuaiYi",owner=id.ToString("N"),phase="Owned"});
        string path=Path.Combine(directory,"execution-owner.lock");InstallationPaths.Check(path);
        try{lease=new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}catch(IOException){throw new InstallationBusyException();}
        try
        {
            if(lease.Length!=0)throw new InstallationRecoveryException("Unknown execution lock bytes retained.");
            string marker=RecordPath(directory);InstallationPaths.Check(marker);
            if(File.Exists(marker)||Directory.Exists(marker))throw new InstallationRecoveryException("Existing execution owner retained; no process ownership inferred.");
            SettingsStore.WriteFlushed(marker,record);
        }
        catch{Dispose();throw;}
    }
    internal void Require(string expected)
    {
        if(lease==null||directory!=expected||!InstallationPackage.ReadDocument(RecordPath(directory),1024).AsSpan().SequenceEqual(record))throw new InstallationRecoveryException("Execution ownership is unconfirmed; files retained.");
    }
    internal void Finish()
    {Require(directory);File.Move(RecordPath(directory),Path.Combine(directory,"execution-finished-"+id.ToString("N")+".json"));}
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
    public void Dispose(){lease?.Dispose();lease=null;}
}
