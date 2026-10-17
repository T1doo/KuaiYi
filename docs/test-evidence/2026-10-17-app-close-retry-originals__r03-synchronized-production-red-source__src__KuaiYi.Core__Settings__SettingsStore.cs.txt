using System.Diagnostics;
using System.Security.Cryptography;
namespace KuaiYi.Core.Settings;
public enum SettingsLoadState{Defaults,Current,Migrated,RecoverableBackup,RecoverableDefaults,ReadOnlyUnknown}
public enum SettingsRecovery{None,UseBackup,UseDefaults}
public enum SettingsWriteStage{BeforePublish,AfterPublish}
public sealed record SettingsSnapshot(AppSettings Data,string Version,SettingsLoadState State)
{public bool RequiresRecovery=>State is SettingsLoadState.RecoverableBackup or SettingsLoadState.RecoverableDefaults;public bool ReadOnly=>State==SettingsLoadState.ReadOnlyUnknown;}
public sealed class SettingsConflictException():Exception("Settings changed; reload the draft.");
public sealed class SettingsRecoveryRequiredException():Exception("Explicit settings recovery is required.");
public sealed class SettingsBusyException():Exception("Settings are in use.");
public sealed class SettingsStore(string directory)
{
    public string DirectoryPath{get;}=Path.GetFullPath(directory);
    public string PrimaryPath=>Path.Combine(DirectoryPath,"settings.json");
    public string BackupPath=>Path.Combine(DirectoryPath,"settings.backup.json");
    internal Action<SettingsWriteStage>? WriteStageForTest{get;init;}
    public Task<SettingsSnapshot> LoadAsync(CancellationToken cancellation=default)=>Task.Run(()=>Locked(Load,cancellation),cancellation);
    public Task<SettingsSnapshot> SaveAsync(SettingsSnapshot basis,AppSettings settings,SettingsRecovery recovery=SettingsRecovery.None,CancellationToken cancellation=default)=>Task.Run(()=>Locked(()=>
    {
        var current=Load();if(current.ReadOnly)throw new UnknownSettingsSchemaException(-1);
        if(current.Version!=basis.Version)throw new SettingsConflictException();
        if(current.RequiresRecovery&&(recovery==SettingsRecovery.None||recovery==SettingsRecovery.UseBackup&&current.State!=SettingsLoadState.RecoverableBackup))throw new SettingsRecoveryRequiredException();
        var data=settings with{SchemaVersion=AppSettings.CurrentSchema};byte[] bytes=SettingsJson.Write(data);
        string temp=Path.Combine(DirectoryPath,"settings.tmp-"+Guid.NewGuid().ToString("N"));
        try
        {
            WriteFlushed(temp,bytes);WriteStageForTest?.Invoke(SettingsWriteStage.BeforePublish);cancellation.ThrowIfCancellationRequested();
            // Keep corrupt/legacy bytes for review. A valid recovery backup is never replaced by corrupt primary.
            if(current.RequiresRecovery&&File.Exists(PrimaryPath))WriteFlushed(Path.Combine(DirectoryPath,"settings.corrupt-"+Guid.NewGuid().ToString("N")+".json"),Read(PrimaryPath).Bytes!);
            if(!current.RequiresRecovery&&File.Exists(BackupPath)){var prior=Read(BackupPath);if(prior.Data==null&&prior.Bytes!=null)WriteFlushed(Path.Combine(DirectoryPath,"settings.corrupt-backup-"+Guid.NewGuid().ToString("N")+".json"),prior.Bytes);}
            if(File.Exists(PrimaryPath))File.Replace(temp,PrimaryPath,current.RequiresRecovery?null:BackupPath,false);
            else File.Move(temp,PrimaryPath);
            WriteStageForTest?.Invoke(SettingsWriteStage.AfterPublish);
            // Commit has happened. Cancellation after this point cannot truthfully report rollback.
            return Load();
        }
        finally{CryptographicOperations.ZeroMemory(bytes);if(File.Exists(temp))File.Delete(temp);}
    },cancellation),cancellation);
    private SettingsSnapshot Load()
    {
        var primary=Read(PrimaryPath);var backup=Read(BackupPath);
        string version=Digest(primary.Bytes)+":"+Digest(backup.Bytes);
        if(primary.Unknown||backup.Unknown)return new(new(),version,SettingsLoadState.ReadOnlyUnknown);
        if(primary.Data!=null)return new(primary.Data,version,primary.Migrated?SettingsLoadState.Migrated:SettingsLoadState.Current);
        if(primary.Bytes==null&&backup.Bytes==null)return new(new(),version,SettingsLoadState.Defaults);
        if(backup.Data!=null)return new(backup.Data,version,SettingsLoadState.RecoverableBackup);
        return new(new(),version,SettingsLoadState.RecoverableDefaults);
    }
    private static string Digest(byte[]? bytes)=>bytes==null?"absent":Convert.ToHexString(SHA256.HashData(bytes));
    private static (byte[]? Bytes,AppSettings? Data,bool Migrated,bool Unknown) Read(string path)
    {
        if(!File.Exists(path)){if(Directory.Exists(path))throw new IOException("Settings path is a directory.");return(null,null,false,false);}
        RejectLink(path);using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(file.Length>SettingsJson.MaximumBytes)throw new SettingsValidationException("fileSize");
        byte[] bytes=new byte[(int)file.Length];file.ReadExactly(bytes);
        try{return(bytes,SettingsJson.Read(bytes,out bool migrated),migrated,false);}
        catch(UnknownSettingsSchemaException){return(bytes,null,false,true);}
        catch(Exception error)when(error is SettingsValidationException or System.Text.Json.JsonException or ArgumentException or OverflowException){return(bytes,null,false,false);}
    }
    private T Locked<T>(Func<T> action,CancellationToken cancellation)
    {
        Directory.CreateDirectory(DirectoryPath);RejectLink(DirectoryPath);string path=Path.Combine(DirectoryPath,"settings.lock");var timer=Stopwatch.StartNew();
        while(true)
        {
            cancellation.ThrowIfCancellationRequested();if(File.Exists(path))RejectLink(path);
            FileStream lease;
            try{lease=new(path,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
            catch(IOException){if(timer.Elapsed>=TimeSpan.FromSeconds(2))throw new SettingsBusyException();cancellation.WaitHandle.WaitOne(20);continue;}
            using(lease){cancellation.ThrowIfCancellationRequested();return action();}
        }
    }
    internal static void WriteFlushed(string path,ReadOnlySpan<byte> bytes)
    {bool created=false;try{using var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None);created=true;file.Write(bytes);file.Flush(true);}catch{if(created)File.Delete(path);throw;}}
    internal static void RejectLink(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Product storage must not be a reparse point.");}
}
