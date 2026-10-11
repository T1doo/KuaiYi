using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using KuaiYi.Core;
internal static class Program
{
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnregisterHotKey(nint window,int id);
    [STAThread]private static int Main(string[] args)
    {
        if(!OperatingSystem.IsWindows()){Console.Error.WriteLine("Windows interactive session required; no registration attempted.");return 2;}
        if(args.Length!=1){Console.Error.WriteLine("Usage: KuaiYi.HotkeyOccupier.exe Ctrl+Alt+Q (press Enter to release). Use public test combination only.");return 2;}
        HotkeyChord chord;try{chord=HotkeyPolicy.Parse(args[0]);}catch(HotkeyValidationException error){Console.Error.WriteLine(error.Problem);return 2;}
        using var window=new HwndSource(new HwndSourceParameters("KuaiYi.HotkeyOccupier"){ParentWindow=new(-3),WindowStyle=0,Width=0,Height=0});
        if(!RegisterHotKey(window.Handle,1,(uint)chord.Modifiers|0x4000,chord.Key)){Console.Error.WriteLine("RegisterHotKey failed · Win32 "+Marshal.GetLastPInvokeError());return 3;}
        Console.WriteLine("Registered public test chord: "+chord.Text+". Enter releases only this tool's registration.");var dispatcher=Dispatcher.CurrentDispatcher;
        _=Task.Run(()=>{Console.ReadLine();dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);});
        int result=0;try{Dispatcher.Run();}finally{if(!UnregisterHotKey(window.Handle,1)){Console.Error.WriteLine("UnregisterHotKey failed · Win32 "+Marshal.GetLastPInvokeError());result=4;}}return result;
    }
}
