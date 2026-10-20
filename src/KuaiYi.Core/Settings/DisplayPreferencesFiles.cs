using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.Core.Settings;

public interface IPreparedDisplayPreferencesExport:IDisposable{void Publish();}
public static class DisplayPreferencesFiles
{
    private static string LocalPath(string path)
    {
        if(path.Length is <1 or >4096||!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\",StringComparison.Ordinal)||path.StartsWith("//",StringComparison.Ordinal))throw new DisplayPreferencesException(DisplayPreferencesIssue.File);
        if(OperatingSystem.IsWindows()&&(path.Length<3||!char.IsAsciiLetter(path[0])||path[1]!=':'||path[2]!='\\'||path.AsSpan(2).Contains(':')))throw new DisplayPreferencesException(DisplayPreferencesIssue.File);
        string full=Path.GetFullPath(path);
        if(OperatingSystem.IsWindows()&&new DriveInfo(Path.GetPathRoot(full)!).DriveType==DriveType.Network)throw new DisplayPreferencesException(DisplayPreferencesIssue.File);
        for(string? directory=Path.GetDirectoryName(full);directory!=null;directory=Path.GetDirectoryName(directory))SettingsStore.RejectLink(directory);
        return full;
    }
    private static void RequireOpenedPath(FileStream stream,string expected)
    {
        _=LocalPath(expected);SettingsStore.RejectLink(expected);string actual;
        if(OperatingSystem.IsWindows())
        {
            if(GetFileType(stream.SafeFileHandle)!=1||!GetFileInformationByHandle(stream.SafeFileHandle,out var info)||(info.Attributes&(0x10u|0x400u))!=0)throw new IOException("Display file ownership unavailable.");
            var path=new StringBuilder(32768);uint length=GetFinalPathNameByHandleW(stream.SafeFileHandle,path,(uint)path.Capacity,0);
            if(length is 0 or >=32768||!path.ToString().StartsWith(@"\\?\",StringComparison.Ordinal))throw new IOException("Display file ownership unavailable.");
            actual=path.ToString()[4..];_=LocalPath(actual);
        }
        else if(OperatingSystem.IsLinux())
            actual=File.ResolveLinkTarget("/proc/self/fd/"+stream.SafeFileHandle.DangerousGetHandle().ToInt64(),returnFinalTarget:true)?.FullName??throw new IOException("Display file ownership unavailable.");
        else throw new IOException("Display file ownership unavailable.");
        if(!string.Equals(actual,expected,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new IOException("Display file ownership changed.");
    }
    public static Task<DisplayPreferences> ReadAsync(string path,CancellationToken cancellation=default)
        =>ReadDocumentAsync(path,DisplayPreferencesDocument.MaximumBytes,DisplayPreferencesDocument.ReadAsync,cancellation);
    internal static async Task<T> ReadDocumentAsync<T>(string path,int maximumBytes,Func<Stream,CancellationToken,Task<T>> read,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();string full=LocalPath(path);
        var attributes=File.GetAttributes(full);if((attributes&(FileAttributes.ReparsePoint|FileAttributes.Directory|FileAttributes.Device))!=0)throw new DisplayPreferencesException(DisplayPreferencesIssue.File);
        if(new FileInfo(full).Length<2||new FileInfo(full).Length>maximumBytes)throw new DisplayPreferencesException(DisplayPreferencesIssue.FileSize);
        await using var file=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.Read,4096,FileOptions.Asynchronous);
        RequireOpenedPath(file,full);var value=await read(file,cancellation).ConfigureAwait(false);RequireOpenedPath(file,full);cancellation.ThrowIfCancellationRequested();return value;
    }
    public static Task<IPreparedDisplayPreferencesExport> PrepareNewAsync(string path,ReadOnlyMemory<byte> document,CancellationToken cancellation=default)
        =>PrepareDocumentAsync(path,document,bytes=>{_=DisplayPreferencesDocument.Read(bytes);},".kuaiyi-display-",cancellation);
    internal static async Task<IPreparedDisplayPreferencesExport> PrepareDocumentAsync(string path,ReadOnlyMemory<byte> document,Action<ReadOnlyMemory<byte>> validate,string prefix,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();string full=LocalPath(path);validate(document);
        string temporary=Path.Combine(Path.GetDirectoryName(full)!,prefix+Guid.NewGuid().ToString("N")+".tmp");
        // Creation proves ownership. Never remove an existing file when creation fails.
        var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.Read|FileShare.Delete,4096,FileOptions.Asynchronous);
        var prepared=new Prepared(stream,temporary,full);
        try{RequireOpenedPath(stream,temporary);await stream.WriteAsync(document,cancellation).ConfigureAwait(false);await stream.FlushAsync(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return prepared;}
        catch
        {
            try{prepared.Dispose();}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException or DisplayPreferencesException or ArgumentException or NotSupportedException){throw new DisplayPreferencesException(DisplayPreferencesIssue.Cleanup);}
            throw;
        }
    }
    private sealed class Prepared(FileStream stream,string temporary,string target):IPreparedDisplayPreferencesExport
    {
        private bool published,disposed;
        public void Publish()
        {ObjectDisposedException.ThrowIf(disposed,this);if(published)throw new InvalidOperationException("Already published.");_=LocalPath(target);RequireOpenedPath(stream,temporary);File.Move(temporary,target,overwrite:false);published=true;}
        public void Dispose()
        {if(disposed)return;disposed=true;try{if(!published){RequireOpenedPath(stream,temporary);File.Delete(temporary);}}finally{stream.Dispose();}}
    }
    [StructLayout(LayoutKind.Sequential)]private struct Information{public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
    [DllImport("kernel32.dll",ExactSpelling=true)]private static extern uint GetFileType(SafeFileHandle handle);
    [DllImport("kernel32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Information info);
    [DllImport("kernel32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
}
