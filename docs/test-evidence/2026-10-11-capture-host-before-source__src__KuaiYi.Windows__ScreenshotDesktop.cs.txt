using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using KuaiYi.Core;
using KuaiYi.Imaging;
using Microsoft.Win32.SafeHandles;
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.System32)]
namespace KuaiYi.Windows;

public enum ScreenshotBackend { WindowsGraphicsCapture, GdiCompatibility }
public sealed record ScreenshotDisplay(nint Handle,ScreenshotMonitor Monitor);
public static class ScreenshotDesktop
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect {public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct Info {public int Size;public Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
    private delegate bool MonitorCallback(nint monitor,nint dc,nint rect,nint data);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool EnumDisplayMonitors(nint dc,nint clip,MonitorCallback callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetMonitorInfoW(nint monitor,ref Info info);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetPhysicalCursorPos(out NativeDesktop.Point point);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetWindowDisplayAffinity(nint window,uint affinity);
    [DllImport("dwmapi.dll")] private static extern int DwmFlush();
    [DllImport("KuaiYi.CaptureNative.dll",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)] private static extern int KY_CaptureMonitor(nint monitor,int width,int height,SafeWaitHandle stop,[Out]byte[] output,int length);
    [DllImport("KuaiYi.CaptureNative.dll",CallingConvention=CallingConvention.Cdecl,ExactSpelling=true)] private static extern int KY_CaptureGdi(int left,int top,int width,int height,SafeWaitHandle stop,[Out]byte[] output,int length);
    public static IReadOnlyList<ScreenshotDisplay> Displays()
    {
        var result=new List<ScreenshotDisplay>();Exception? failure=null;
        if(!EnumDisplayMonitors(0,0,(monitor,_,_,_)=>
        {
            try
            {
                var info=new Info{Size=Marshal.SizeOf<Info>(),Device=""};if(!GetMonitorInfoW(monitor,ref info))throw new Win32Exception(Marshal.GetLastWin32Error());
                var bounds=new PixelRect(info.Monitor.Left,info.Monitor.Top,info.Monitor.Right,info.Monitor.Bottom);
                result.Add(new(monitor,new(info.Device,bounds,NativeDesktop.MonitorAt(bounds.Center).Dpi)));return true;
            }
            catch(Exception e){failure=e;return false;}
        },0))throw failure??new Win32Exception(Marshal.GetLastWin32Error());
        if(failure!=null)throw failure;_ = new ScreenshotTopology(result.Select(d=>d.Monitor));return result;
    }
    public static PixelPoint Cursor(){if(!GetPhysicalCursorPos(out var p))throw new Win32Exception(Marshal.GetLastWin32Error());return new(p.X,p.Y);}
    public static bool ExcludeWindow(nint window)=>SetWindowDisplayAffinity(window,0x11); // Supplemental only; callers always hide before capture.
    public static void FlushHiddenWindows(){int hr=DwmFlush();if(hr<0)Marshal.ThrowExceptionForHR(hr);}
    public static Task<ScreenshotSnapshot> CaptureAsync(IReadOnlyList<ScreenshotDisplay> displays,ScreenshotBackend backend,CancellationToken cancellation)=>Task.Run(()=>Capture(displays,backend,cancellation));
    private static ScreenshotSnapshot Capture(IReadOnlyList<ScreenshotDisplay> displays,ScreenshotBackend backend,CancellationToken cancellation)
    {
        var topology=new ScreenshotTopology(displays.Select(d=>d.Monitor));var tiles=new List<ScreenshotPixels>();
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(5));using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,limit.Token);
        using var stop=new EventWaitHandle(false,EventResetMode.ManualReset);using var registration=linked.Token.Register(()=>stop.Set());
        try
        {
            foreach(var display in displays)
            {
                linked.Token.ThrowIfCancellationRequested();var b=display.Monitor.Bounds;byte[]? bytes=new byte[checked(b.Width*b.Height*4)];
                try
                {
                    int code=backend switch
                    {
                        ScreenshotBackend.WindowsGraphicsCapture=>KY_CaptureMonitor(display.Handle,b.Width,b.Height,stop.SafeWaitHandle,bytes,bytes.Length),
                        ScreenshotBackend.GdiCompatibility=>KY_CaptureGdi(b.Left,b.Top,b.Width,b.Height,stop.SafeWaitHandle,bytes,bytes.Length),
                        _=>throw new ArgumentOutOfRangeException(nameof(backend))
                    };
                    linked.Token.ThrowIfCancellationRequested();
                    if(code!=0)throw new ScreenshotException(code switch{3=>ScreenshotFailure.Timeout,4=>ScreenshotFailure.TopologyChanged,_=>ScreenshotFailure.CaptureUnavailable});
                    tiles.Add(ScreenshotPixels.FromCapturedBuffer(b,bytes,linked.Token));bytes=null;
                }
                finally{if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);}
            }
            linked.Token.ThrowIfCancellationRequested();if(!topology.Matches(new(Displays().Select(d=>d.Monitor))))throw new ScreenshotException(ScreenshotFailure.TopologyChanged);
            var snapshot=new ScreenshotSnapshot(topology,tiles);tiles.Clear();return snapshot;
        }
        catch(OperationCanceledException)when(limit.IsCancellationRequested&&!cancellation.IsCancellationRequested){throw new ScreenshotException(ScreenshotFailure.Timeout);}
        catch(Exception e)when(e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException){throw new ScreenshotException(ScreenshotFailure.CaptureUnavailable);}
        finally{foreach(var tile in tiles)tile.Dispose();}
    }
}
