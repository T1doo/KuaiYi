using System.Runtime.InteropServices;

namespace KuaiYi.Windows;

public readonly record struct WindowBackdropResult(bool Requested,bool Accepted,int HResult);

// Presentation only. Never changes activation, position, hit regions or system settings.
public static class NativeWindowBackdrop
{
    [StructLayout(LayoutKind.Sequential)]private struct Margins{public int Left,Right,Top,Bottom;}
    [DllImport("dwmapi.dll")]private static extern int DwmSetWindowAttribute(nint window,int attribute,ref int value,int size);
    [DllImport("dwmapi.dll")]private static extern int DwmExtendFrameIntoClientArea(nint window,ref Margins margins);
    [DllImport("dwmapi.dll")]private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)]out bool enabled);
    public static WindowBackdropResult Apply(nint window,bool enable,bool dark)
    {
        if(window==0||!OperatingSystem.IsWindowsVersionAtLeast(10,0,22621))return new(enable,false,unchecked((int)0x80004001));
        int composition=DwmIsCompositionEnabled(out bool composed);
        if(composition<0||!composed)return new(enable,false,composition);
        int nonClient=enable?2:1;int policy=DwmSetWindowAttribute(window,2,ref nonClient,sizeof(int));
        if(policy<0)return new(enable,false,policy);
        int mode=dark?1:0;_ =DwmSetWindowAttribute(window,20,ref mode,sizeof(int));
        int material=enable?3:1;
        int hr=DwmSetWindowAttribute(window,38,ref material,sizeof(int));
        if(hr<0)return new(enable,false,hr);
        var margins=enable?new Margins{Left=-1,Right=-1,Top=-1,Bottom=-1}:new Margins();
        hr=DwmExtendFrameIntoClientArea(window,ref margins);
        if(hr<0){material=1;_ =DwmSetWindowAttribute(window,38,ref material,sizeof(int));}
        return new(enable,enable&&hr>=0,hr);
    }
}
