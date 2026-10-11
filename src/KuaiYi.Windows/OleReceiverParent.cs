using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using KuaiYi.OleTransfer;
namespace KuaiYi.Windows;
public static class OleReceiverParent
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct ProcessEntry
    {
        public uint Size,Usage,Id;public nuint Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string File;
    }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags,uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Process32FirstW(SafeFileHandle snapshot,ref ProcessEntry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Process32NextW(SafeFileHandle snapshot,ref ProcessEntry entry);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    public static Process Verify(OleReceiverHello job)
    {
        OleReceiverWire.Validate(job);Process? parent=null;
        try
        {
            if(!string.Equals(Environment.ProcessPath,Path.Combine(AppContext.BaseDirectory,"KuaiYi.OleReceiver.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("OLE parent role rejected.");
            uint observed=0;using(var snapshot=CreateToolhelp32Snapshot(2,0))
            {
                if(snapshot.IsInvalid)throw new InvalidOperationException("OLE parent role rejected.");
                var entry=new ProcessEntry{Size=(uint)Marshal.SizeOf<ProcessEntry>(),File=""};
                if(!Process32FirstW(snapshot,ref entry))throw new InvalidOperationException("OLE parent role rejected.");
                do{if(entry.Id==Environment.ProcessId){observed=entry.Parent;break;}}while(Process32NextW(snapshot,ref entry));
            }
            if(observed==0||observed!=job.AppProcessId)throw new InvalidOperationException("OLE parent role rejected.");
            if(GetWindowThreadProcessId((nint)job.Hwnd,out uint owner)==0||owner!=observed)throw new InvalidOperationException("OLE target HWND owner rejected.");
            parent=Process.GetProcessById(checked((int)observed));_ =parent.Handle; // Retain actual OS handle; never used for termination.
            using var self=Process.GetCurrentProcess();
            if(parent.HasExited||parent.StartTime>self.StartTime||parent.SessionId!=self.SessionId||!string.Equals(parent.MainModule?.FileName,Path.Combine(AppContext.BaseDirectory,"KuaiYi.App.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("OLE parent role rejected.");
            var held=parent;parent=null;return held;
        }
        catch(InvalidOperationException){throw;}
        catch(Exception){throw new InvalidOperationException("OLE parent role rejected.");}
        finally{parent?.Dispose();}
    }
}
