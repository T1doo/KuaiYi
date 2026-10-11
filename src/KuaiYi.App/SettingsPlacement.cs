using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class SettingsWindow
{
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
    private bool placementPending;
    private void SetupPlacement()
    {
        SourceInitialized+=(_,_)=>{var source=(HwndSource)PresentationSource.FromVisual(this);source.AddHook((nint hwnd,int message,nint w,nint l,ref bool handled)=>{if(message is 0x02e0 or 0x007e or 0x001a)SchedulePlacement();return 0;});};
        Loaded+=(_,_)=>SchedulePlacement();
    }
    private void SchedulePlacement()
    {
        if(placementPending)return;placementPending=true;Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>
        {
            placementPending=false;if(!IsVisible)return;
            try
            {
                nint hwnd=new WindowInteropHelper(this).Handle;if(!NativeDesktop.GetWindowRect(hwnd,out var native))throw new Win32Exception(Marshal.GetLastPInvokeError());
                PixelRect rect=native.ToPixels();var monitor=NativeDesktop.MonitorAt(rect.Center);var work=monitor.WorkArea;double maxWidth=monitor.Dpi.Dips(work.Width),maxHeight=monitor.Dpi.Dips(work.Height);
                MinWidth=Math.Min(700,maxWidth);MinHeight=Math.Min(520,maxHeight);MaxWidth=maxWidth;MaxHeight=maxHeight;
                int width=Math.Min(rect.Width,work.Width),height=Math.Min(rect.Height,work.Height);int x=Math.Clamp(rect.Left,work.Left,work.Right-width),y=Math.Clamp(rect.Top,work.Top,work.Bottom-height);
                if(!SetWindowPos(hwnd,0,x,y,width,height,0x14))throw new Win32Exception(Marshal.GetLastPInvokeError()); // NOZORDER | NOACTIVATE; normal WPF window may still receive deliberate keyboard focus.
            }
            catch(Exception error){status.Text="设置窗口工作区校验未完成 · "+error.GetType().Name;}
        });
    }
}
