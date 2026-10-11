using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Interop;
using KuaiYi.Core;
namespace KuaiYi.Windows;
/// <summary>App owns the sole mouse Raw Input registration. No keyboard registration, trajectory history or OLE inference.</summary>
public sealed class OverlayRawMouse:IDisposable
{
    [StructLayout(LayoutKind.Sequential)]private struct Device{public ushort Page,Usage;public uint Flags;public nint Hwnd;}
    [StructLayout(LayoutKind.Sequential)]private struct Header{public uint Type,Size;public nint Device,Parameter;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseDown{public NativeDesktop.Point Point;public uint Data,Flags,Time;public nuint Extra;}
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate nint MouseHook(int code,nint message,nint data);
    [DllImport("user32.dll",ExactSpelling=true,SetLastError=true)]private static extern nint SetWindowsHookExW(int kind,MouseHook callback,nint module,uint thread);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]private static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("kernel32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern nint GetModuleHandleW(string? module);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterRawInputDevices(Device[] devices,uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRegisteredRawInputDevices([Out]Device[]? devices,ref uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRawInputData(nint raw,uint command,[Out]byte[] data,ref uint bytes,uint headerSize);
    [DllImport("user32.dll")]private static extern nint WindowFromPoint(NativeDesktop.Point point);
    [DllImport("user32.dll")]private static extern nint GetAncestor(nint hwnd,uint flags);
    private readonly HwndSource source;private readonly Action<ushort,PixelPoint,bool> input;private readonly Action<string> failure;private readonly HwndSourceHook hook;
    private bool disposed,failed;
    private readonly MouseHook downHook;private nint downHandle;private GCHandle downRoot;
    public bool IsReady=>!disposed&&!failed&&downHandle!=0;
    public OverlayRawMouse(HwndSource source,Action<ushort,PixelPoint,bool> input,Action<string> failure)
    {
        this.source=source;this.input=input;this.failure=failure;hook=Hook;downHook=FreshDown;
        if(Registered().Any(d=>d.Page==1&&d.Usage==2))throw new InvalidOperationException("App mouse Raw Input already has an owner.");
        if(!RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=0x100,Hwnd=source.Handle}],1,(uint)Marshal.SizeOf<Device>()))throw new Win32Exception(Marshal.GetLastWin32Error());
        source.AddHook(hook);
        downRoot=GCHandle.Alloc(downHook);
        downHandle=SetWindowsHookExW(14,downHook,GetModuleHandleW(null),0); // WH_MOUSE_LL; callback runs on this pumping App thread, no injection into source.
        if(downHandle==0){downRoot.Free();SignalFailure("MouseDownHookUnavailable");} // Retain raw registration owner for actual teardown/retry.
    }
    private nint FreshDown(int code,nint message,nint data)
    {
        try
        {
            if(code>=0&&!disposed&&!failed&&(message==0x201||message==0x204))
            {
                var down=Marshal.PtrToStructure<MouseDown>(data);
                if((down.Flags&1)==0) // Only physical mouse scope; injected events have no matching Raw Input guarantee.
                {
                    var point=new PixelPoint(down.Point.X,down.Point.Y);bool hit=GetAncestor(WindowFromPoint(down.Point),2)==source.Handle;
                    if(hit)input(message==0x201?(ushort)1:(ushort)4,point,true); // Original down coordinates; never sample the later cursor for identity.
                }
            }
        }
        catch(Exception error){SignalFailure(error.GetType().Name);}
        return CallNextHookEx(downHandle,code,message,data); // Do not consume or rewrite the external source's event.
    }
    private void SignalFailure(string code)
    {
        failed=true;
        try{failure(code);}catch(Exception error){Console.Error.WriteLine("Overlay input failure reporting: "+error.GetType().Name);} // App callback revokes before any layout/report work; never throw across native hook ABI.
    }
    private static Device[] Registered()
    {
        uint count=0,size=(uint)Marshal.SizeOf<Device>();if(GetRegisteredRawInputDevices(null,ref count,size)==uint.MaxValue||count>256)throw new Win32Exception(Marshal.GetLastWin32Error());
        var devices=new Device[count];if(count!=0&&GetRegisteredRawInputDevices(devices,ref count,size)==uint.MaxValue)throw new Win32Exception(Marshal.GetLastWin32Error());return devices;
    }
    private nint Hook(nint hwnd,int message,nint wparam,nint lparam,ref bool handled)
    {
        if(message!=0xff||disposed||failed)return 0;byte[] bytes=new byte[64];
        try
        {
            uint count=64,header=(uint)Marshal.SizeOf<Header>();uint read=GetRawInputData(lparam,0x10000003,bytes,ref count,header);
            if(read==uint.MaxValue||read<header+24||read>64)throw new Win32Exception(Marshal.GetLastWin32Error());
            if(BitConverter.ToUInt32(bytes)!=0)return 0; // Mouse only. Do not decode keyboard/HID.
            ushort flags=(ushort)(BitConverter.ToUInt16(bytes,(int)header+4)&~5);var point=ScreenshotDesktop.Cursor(); // Down identity comes only from event-coordinate hook; Raw Input handles movement/up.
            bool actualHit=GetAncestor(WindowFromPoint(new(){X=point.X,Y=point.Y}),2)==source.Handle;
            input(flags,point,actualHit);
            // Leave WM_INPUT unhandled so DefWindowProc performs required foreground raw-input cleanup.
        }
        catch(Exception error){SignalFailure(error.GetType().Name);}
        finally{CryptographicOperations.ZeroMemory(bytes);}
        return 0;
    }
    public void Dispose()
    {
        if(disposed)return;
        failed=true; // Stop callbacks before attempting teardown; an unknown hook stays rooted and inactive.
        if(downHandle!=0)
        {
            if(!UnhookWindowsHookEx(downHandle))throw new Win32Exception(Marshal.GetLastWin32Error());
            downHandle=0;if(downRoot.IsAllocated)downRoot.Free();
        }
        if(Registered().Any(d=>d.Page==1&&d.Usage==2&&d.Hwnd==source.Handle))
            if(!RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=1,Hwnd=0}],1,(uint)Marshal.SizeOf<Device>()))throw new Win32Exception(Marshal.GetLastWin32Error());
        source.RemoveHook(hook);disposed=true;
    }
}
