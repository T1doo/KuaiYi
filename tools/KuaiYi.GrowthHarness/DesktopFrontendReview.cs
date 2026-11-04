using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using KuaiYi.App;
using KuaiYi.Core.Settings;

namespace KuaiYi.GrowthHarness;

// Explicit interactive fixture: the actual settings class, disposable configuration,
// no provider, no keys, no capture. It never opens the user's production settings.
internal static class DesktopFrontendReview
{
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW",SetLastError=true)]private static extern nint GetWindowLongPtr(nint window,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)]private static extern nint SetWindowLongPtr(nint window,int index,nint value);
    internal static void ExposeInspectionWindow(Window window)
    {
        nint hwnd=new WindowInteropHelper(window).Handle;
        long style=GetWindowLongPtr(hwnd,-20).ToInt64();
        long adapted=(style&~(0x80L|0x08000000L))|0x40000L;
        if(adapted==style)return;
        Marshal.SetLastPInvokeError(0);
        if(SetWindowLongPtr(hwnd,-20,new nint(adapted))==0&&Marshal.GetLastPInvokeError()!=0)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastPInvokeError());
        window.Title="快译 · 公开外观验收（工具窗枚举适配，非原生焦点验收）";
    }
    internal static void InstallInspectionWindow(Window window)
    {
        ExposeInspectionWindow(window);
        HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).AddHook((nint hwnd,int message,nint w,nint l,ref bool handled)=>
        {if(message==0x007d&&w==-20)ExposeInspectionWindow(window);return 0;});
    }
    internal static int Run()
    {
        string root=Path.Combine(Path.GetTempPath(),"KuaiYi-frontend-review-"+Guid.NewGuid().ToString("N"));
        var app=new Application();
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        var coordinator=new SettingsCoordinator(new SettingsStore(Path.Combine(root,"config")),new SecretFileStore(Path.Combine(root,"secrets"),new NoKeys(),new DirectoryPolicy()));
        var draft=coordinator.OpenDraftAsync().GetAwaiter().GetResult();
        draft.Update(s=>s with{ActiveProfileId="public",Profiles=[new(){Id="public",Name="公开外观测试 · 无密钥",Model="public-fixture",BaseUrl="https://public.invalid",Authentication=ApiAuthentication.None}]});
        var window=new SettingsWindow(coordinator,draft){Title="快译设置 · 公开前端验收（不发送请求）",ShowInTaskbar=true};
        try{return app.Run(window);}
        finally{draft.Dispose();if(Directory.Exists(root))Directory.Delete(root,true);}
    }
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoKeys:ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plain)=>throw new InvalidOperationException("Public review does not save keys.");
        public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>throw new InvalidOperationException("Public review does not read keys.");
    }
}
