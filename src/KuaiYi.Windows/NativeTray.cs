using System.ComponentModel;
using System.Runtime.InteropServices;
namespace KuaiYi.Windows;
public sealed class NativeTray:IDisposable
{
    public const int CallbackMessage=0x8000+41;
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Data
    {
        public uint Size;public nint Window;public uint Id,Flags,Callback;public nint Icon;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Tip;public uint State,StateMask;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=256)]public string Info;public uint Version;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=64)]public string Title;public uint InfoFlags;public Guid Guid;public nint BalloonIcon;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool Shell_NotifyIconW(uint message,ref Data data);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]private static extern nint LoadIconW(nint instance,nint icon);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]public static extern uint RegisterWindowMessageW(string name);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PostMessageW(nint hwnd,uint message,nint w,nint l);
    [DllImport("user32.dll",SetLastError=true)]private static extern nint CreatePopupMenu();
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool AppendMenuW(nint menu,uint flags,nuint id,string title);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint TrackPopupMenuEx(nint menu,uint flags,int x,int y,nint window,nint parameters);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool DestroyMenu(nint menu);
    private Data data;private bool added;private bool disposed;
    public uint TaskbarCreated{get;}=RegisterWindowMessageW("TaskbarCreated");
    public NativeTray(nint window)
    {
        data=new(){Size=(uint)Marshal.SizeOf<Data>(),Window=window,Id=1,Flags=1|2|4|128,Callback=CallbackMessage,Icon=LoadIconW(0,new nint(32512)),Tip="快译 · 未启用",Info="",Title=""};
        if(data.Icon==0||TaskbarCreated==0)throw new Win32Exception(Marshal.GetLastPInvokeError());
        try{Restore();}catch{if(added)Shell_NotifyIconW(2,ref data);throw;}
    }
    public void Restore()
    {
        ObjectDisposedException.ThrowIf(disposed,this);if(!Shell_NotifyIconW(0,ref data))throw new Win32Exception(Marshal.GetLastPInvokeError());added=true;
        data.Version=4;if(!Shell_NotifyIconW(4,ref data))throw new Win32Exception(Marshal.GetLastPInvokeError());
    }
    public void Update(bool enabled)
    {ObjectDisposedException.ThrowIf(disposed,this);data.Tip=enabled?"快译 · 已启用":"快译 · 未启用";if(!Shell_NotifyIconW(1,ref data))throw new Win32Exception(Marshal.GetLastPInvokeError());}
    public uint Menu(bool enabled,bool visible)
    {
        nint menu=CreatePopupMenu();if(menu==0)throw new Win32Exception(Marshal.GetLastPInvokeError());
        try
        {
            foreach(var item in new[]{(1u,enabled?"停用翻译":"启用翻译"),(2u,visible?"隐藏浮窗（同时停用）":"显示浮窗"),(3u,"设置"),(4u,"聚焦浮窗"),(5u,"退出")})
                if(!AppendMenuW(menu,item.Item1==1&&enabled?8u:0u,item.Item1,item.Item2))throw new Win32Exception(Marshal.GetLastPInvokeError());
            if(!NativeDesktop.GetCursorPos(out var p))throw new Win32Exception(Marshal.GetLastPInvokeError());
            SetForegroundWindow(data.Window); // Deliberate tray invocation only; never for automatic output.
            uint result=TrackPopupMenuEx(menu,0x100|2,p.X,p.Y,data.Window,0);PostMessageW(data.Window,0,0,0);return result;
        }
        finally{if(!DestroyMenu(menu))throw new Win32Exception(Marshal.GetLastPInvokeError());}
    }
    public static uint ShowMessage(string scope)
    {uint message=RegisterWindowMessageW(scope+".Show");if(message==0)throw new Win32Exception(Marshal.GetLastPInvokeError());return message;}
    public static void ShowExisting(string scope)
    {if(!PostMessageW(new nint(0xffff),ShowMessage(scope),0,0))throw new Win32Exception(Marshal.GetLastPInvokeError());}
    public void Dispose(){if(disposed)return;disposed=true;if(added&&!Shell_NotifyIconW(2,ref data))throw new Win32Exception(Marshal.GetLastPInvokeError());added=false;}
}
