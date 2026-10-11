using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using KuaiYi.Imaging;
namespace KuaiYi.Windows;
/// <summary>Metadata gate only. Successful visibility/affinity checks do not prove Windows pixel exclusion.</summary>
public static class CaptureBoundary
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct ProcessEntry
    {
        public uint Size,Usage,Id;public nuint Heap;public uint Module,Threads,Parent;public int Priority;public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string File;
    }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern SafeFileHandle CreateToolhelp32Snapshot(uint flags,uint process);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Process32FirstW(SafeFileHandle snapshot,ref ProcessEntry entry);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Process32NextW(SafeFileHandle snapshot,ref ProcessEntry entry);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetWindowDisplayAffinity(nint window,out uint affinity);
    private delegate bool WindowCallback(nint window,nint data);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool EnumWindows(WindowCallback callback,nint data);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]private static extern nint GetWindowLongPtr(nint window,int index);
    public static IReadOnlyList<long> VisibleWindowsForProcess(int processId)
    {
        var windows=new List<long>();
        if(!EnumWindows((window,_)=>{if(IsWindowVisible(window)&&GetWindowThreadProcessId(window,out uint owner)!=0&&owner==processId)windows.Add(window.ToInt64());return true;},0))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);
        return windows;
    }
    public static Process VerifyActualParent(CaptureJob job)
    {
        CaptureProtocol.Validate(job);Process? parent=null;
        try
        {
            if(!string.Equals(Environment.ProcessPath,Path.Combine(AppContext.BaseDirectory,"KuaiYi.CaptureHost.exe"),StringComparison.OrdinalIgnoreCase))throw new CaptureException(CaptureFailure.ParentRoleRejected);
            uint observed=0;using(var snapshot=CreateToolhelp32Snapshot(2,0))
            {
                if(snapshot.IsInvalid)throw new CaptureException(CaptureFailure.ParentRoleRejected);
                var entry=new ProcessEntry{Size=(uint)Marshal.SizeOf<ProcessEntry>(),File=""};
                if(!Process32FirstW(snapshot,ref entry))throw new CaptureException(CaptureFailure.ParentRoleRejected);
                do{if(entry.Id==Environment.ProcessId){observed=entry.Parent;break;}}while(Process32NextW(snapshot,ref entry));
            }
            if(observed==0||observed!=job.AppProcessId)throw new CaptureException(CaptureFailure.ParentRoleRejected);
            parent=Process.GetProcessById(checked((int)observed));_ =parent.Handle; // Retain actual OS handle; never used for termination.
            using var self=Process.GetCurrentProcess();
            if(parent.HasExited||parent.StartTime>self.StartTime||parent.SessionId!=self.SessionId||!string.Equals(parent.MainModule?.FileName,Path.Combine(AppContext.BaseDirectory,"KuaiYi.App.exe"),StringComparison.OrdinalIgnoreCase))throw new CaptureException(CaptureFailure.ParentRoleRejected);
            var held=parent;parent=null;return held;
        }
        catch(CaptureException){throw;}
        catch(Exception){throw new CaptureException(CaptureFailure.ParentRoleRejected);}
        finally{parent?.Dispose();}
    }
    public static void VerifyHidden(CaptureJob job,Process heldParent)
    {
        if(heldParent.HasExited||heldParent.Id!=job.AppProcessId||VisibleWindowsForProcess(heldParent.Id).Count!=0||!HiddenForProcess(job.HiddenWindows,heldParent.Id,true))throw new CaptureException(CaptureFailure.HiddenWindowNotVerified);
    }
    public static bool HiddenForProcess(long[] windows,int processId,bool requireAffinity)
    {
        return windows.Length is >=1 and <=16&&windows.All(handle=>
        {
            nint window=checked((nint)handle);
            return IsWindow(window)&&!IsWindowVisible(window)&&GetWindowThreadProcessId(window,out uint owner)!=0&&owner==processId&&(!requireAffinity||(GetWindowLongPtr(window,-20).ToInt64()&0x80000)!=0&&GetWindowDisplayAffinity(window,out uint affinity)&&affinity==0x11);
        });
    }
}
