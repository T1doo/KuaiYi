using System.ComponentModel;
using System.Runtime.InteropServices;
using KuaiYi.Core;
namespace KuaiYi.Windows;
public sealed record MonitorTopology(IReadOnlyList<MonitorGeometry> Monitors,string PrimaryId)
{
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Info
    {public int Size;public NativeDesktop.Rect Monitor,Work;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Device;}
    private delegate bool Enumerate(nint monitor,nint dc,nint rectangle,nint state);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool EnumDisplayMonitors(nint dc,nint clip,Enumerate callback,nint data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetMonitorInfoW(nint monitor,ref Info info);
    public static MonitorTopology Read()
    {
        var monitors=new List<MonitorGeometry>();string? primary=null;Exception? failure=null;
        bool result=EnumDisplayMonitors(0,0,(monitor,_,_,_)=>
        {
            try
            {
                var info=new Info{Size=Marshal.SizeOf<Info>(),Device=""};if(!GetMonitorInfoW(monitor,ref info))throw new Win32Exception(Marshal.GetLastPInvokeError());
                var work=info.Work.ToPixels();if(work.Width<=0||work.Height<=0)return true;
                var geometry=NativeDesktop.MonitorAt(work.Center) with{Id=info.Device};monitors.Add(geometry);if((info.Flags&1)!=0)primary=info.Device;return true;
            }
            catch(Exception error){failure=error;return false;}
        },0);
        if(failure!=null)throw new InvalidOperationException("Monitor topology unavailable.",failure);
        if(!result)throw new Win32Exception(Marshal.GetLastPInvokeError());if(monitors.Count==0)throw new InvalidOperationException("No active work areas.");
        return new(monitors,primary??monitors[0].Id);
    }
    public MonitorGeometry At(PixelRect anchor)=>Monitors.OrderByDescending(m=>m.WorkArea.OverlapArea(anchor)).First();
}
