using System.Diagnostics;
using System.Runtime.InteropServices;
using KuaiYi.Core;
namespace KuaiYi.Windows;
public static class SelectionDesktop
{
    [DllImport("user32.dll")]private static extern nint WindowFromPoint(NativeDesktop.Point point);
    [DllImport("user32.dll")]private static extern nint GetAncestor(nint window,uint flags);
    [DllImport("user32.dll")]public static extern uint GetDoubleClickTime();
    [DllImport("user32.dll")]public static extern int GetSystemMetrics(int index);
    public static SelectionWindow? Resolve(long foreground,PixelPoint? hit=null,bool requireForeground=true)
    {
        nint window=new(foreground);if(window==0||!NativeDesktop.IsWindow(window)||requireForeground&&NativeDesktop.GetForegroundWindow()!=window||hit is {} point&&GetAncestor(WindowFromPoint(new(){X=point.X,Y=point.Y}),2)!=window)return null;
        if(NativeDesktop.GetWindowThreadProcessId(window,out uint pid)==0||pid==0||pid==Environment.ProcessId)return null;
        try{using var process=Process.GetProcessById(checked((int)pid));using var self=Process.GetCurrentProcess();if(process.HasExited||process.SessionId!=self.SessionId||NativeDesktop.IntegrityLevel(pid)>NativeDesktop.IntegrityLevel((uint)Environment.ProcessId))return null;string? path=process.MainModule?.FileName;if(string.IsNullOrEmpty(path))return null;return new(foreground,pid,process.StartTime.ToUniversalTime().Ticks,path);}
        catch(Exception error)when(error is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException or UnauthorizedAccessException){return null;}
    }
    public static bool IsDropSourceCurrent(SelectionWindow source)=>IsCurrent(source)&&ClipboardNative.TrustedFocus(source,ClipboardNative.Focus(source.Handle));
    public static bool IsCurrent(SelectionWindow source)=>Resolve(source.Handle)==source;
    public static bool IsRetainedSourceCurrent(SelectionWindow source)=>Resolve(source.Handle,requireForeground:false)==source;
}
