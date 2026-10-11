using System.Runtime.InteropServices;
using System.Windows.Interop;
namespace KuaiYi.App;
/// <summary>Explicit registration only. New combo succeeds before removing old; no global keyboard hook.</summary>
internal sealed class ScreenshotHotkey(HwndSource source,Action invoke,Action topologyChanged):IDisposable
{
    private int? registered;
    private HwndSourceHook? hook;
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
    [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnregisterHotKey(nint hwnd,int id);
    public bool Set(uint modifiers,uint key)
    {
        if(key!=0&&(key is <0x41 or >0x5a||(modifiers&3)==0||modifiers>15))return false; // P0 Ctrl/Alt + A-Z subset, no F12/system-only keys.
        int candidate=registered==0x3000?0x3001:0x3000;
        if(key!=0&&!RegisterHotKey(source.Handle,candidate,modifiers|0x4000,key))return false;
        if(registered is {} old&&!UnregisterHotKey(source.Handle,old)){if(key!=0&&!UnregisterHotKey(source.Handle,candidate))throw new InvalidOperationException("Hotkey rollback failed.");return false;}
        registered=key==0?null:candidate;
        if(hook==null){hook=Hook;source.AddHook(hook);}return true;
    }
    private nint Hook(nint hwnd,int msg,nint w,nint l,ref bool handled){if(msg is 0x007e or 0x02e0)topologyChanged();if(msg==0x0312&&registered==w.ToInt32()){handled=true;invoke();}return 0;}
    public void Dispose(){if(registered is {} old&&!UnregisterHotKey(source.Handle,old))throw new InvalidOperationException("Hotkey cleanup failed.");registered=null;if(hook!=null)source.RemoveHook(hook);hook=null;}
}
