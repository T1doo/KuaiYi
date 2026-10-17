using System.Runtime.InteropServices;
using System.Text;
using KuaiYi.Core.Settings;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.Core.Diagnostics;

public static class DiagnosticReportFiles
{
    private static string LocalPath(string path)
    {
        if(path.Length is <1 or >4096||!Path.IsPathFullyQualified(path)||path.StartsWith(@"\\",StringComparison.Ordinal)||path.StartsWith("//",StringComparison.Ordinal))throw new IOException("Local report file required.");
        if(OperatingSystem.IsWindows()&&(path.Length<3||!char.IsAsciiLetter(path[0])||path[1]!=':'||path[2]!='\\'||path.AsSpan(2).Contains(':')))throw new IOException("Local report file required.");
        string full=Path.GetFullPath(path);
        if(OperatingSystem.IsWindows()&&new DriveInfo(Path.GetPathRoot(full)!).DriveType==DriveType.Network)throw new IOException("Local report file required.");
        for(string? directory=Path.GetDirectoryName(full);directory!=null;directory=Path.GetDirectoryName(directory))SettingsStore.RejectLink(directory);
        return full;
    }
    private static void RequireOpenedPath(FileStream stream,string expected)
    {
        _=LocalPath(expected);SettingsStore.RejectLink(expected);string actual;
        if(OperatingSystem.IsWindows())
        {
            if(GetFileType(stream.SafeFileHandle)!=1||!GetFileInformationByHandle(stream.SafeFileHandle,out var info)||(info.Attributes&(0x10u|0x400u))!=0)throw new IOException("Report ownership unavailable.");
            var path=new StringBuilder(32768);uint length=GetFinalPathNameByHandleW(stream.SafeFileHandle,path,(uint)path.Capacity,0);
            if(length is 0 or >=32768||!path.ToString().StartsWith(@"\\?\",StringComparison.Ordinal))throw new IOException("Report ownership unavailable.");
            actual=path.ToString()[4..];_=LocalPath(actual);
        }
        else if(OperatingSystem.IsLinux())actual=File.ResolveLinkTarget("/proc/self/fd/"+stream.SafeFileHandle.DangerousGetHandle().ToInt64(),true)?.FullName??throw new IOException("Report ownership unavailable.");
        else throw new IOException("Report ownership unavailable.");
        if(!string.Equals(actual,expected,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new IOException("Report ownership changed.");
    }
    public static async Task<IPreparedDiagnosticExport> PrepareNewAsync(string path,DiagnosticReport report,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();string full=LocalPath(path);
        string temporary=Path.Combine(Path.GetDirectoryName(full)!,".kuaiyi-diagnostic-"+Guid.NewGuid().ToString("N")+".tmp");
        var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.Read|FileShare.Delete,4096,FileOptions.Asynchronous);var prepared=new Prepared(stream,temporary,full);
        try
        {
            RequireOpenedPath(stream,temporary);await stream.WriteAsync(Encoding.UTF8.GetBytes(report.Text),cancellation).ConfigureAwait(false);await stream.FlushAsync(cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();return prepared;
        }
        catch{prepared.Dispose();throw;}
    }
    private sealed class Prepared(FileStream stream,string temporary,string target):IPreparedDiagnosticExport
    {
        private bool disposed,published;
        public void Publish(){ObjectDisposedException.ThrowIf(disposed,this);if(published)throw new InvalidOperationException("Already published.");_=LocalPath(target);RequireOpenedPath(stream,temporary);File.Move(temporary,target,false);published=true;}
        public void Dispose(){if(disposed)return;disposed=true;try{if(!published){RequireOpenedPath(stream,temporary);File.Delete(temporary);}}finally{stream.Dispose();}}
    }
    [StructLayout(LayoutKind.Sequential)]private struct Information{public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Creation,Access,Write;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
    [DllImport("kernel32.dll",ExactSpelling=true)]private static extern uint GetFileType(SafeFileHandle handle);
    [DllImport("kernel32.dll",ExactSpelling=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetFileInformationByHandle(SafeFileHandle handle,out Information info);
    [DllImport("kernel32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle,StringBuilder path,uint length,uint flags);
}
