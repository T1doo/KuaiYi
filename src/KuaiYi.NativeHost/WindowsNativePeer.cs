using System.ComponentModel;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.NativeHost;

public sealed class InstalledReceiverPolicy
{
    internal InstalledReceiverPolicy(InstalledHostPolicy role,IReadOnlyList<string> origins){Role=role;Origins=origins;}
    internal InstalledHostPolicy Role {get;}
    internal IReadOnlyList<string> Origins {get;}
}
/// <summary>Retains the actual client process handle. Not proof of a browser parent or extension intent.</summary>
[SupportedOSPlatform("windows")]
internal sealed class WindowsNativePeer:IDisposable
{
    private readonly NamedPipeServerStream pipe;private readonly InstalledReceiverPolicy policy;
    private readonly SafeProcessHandle process;
    internal VerifiedNativeClient Identity {get;}
    internal WindowsNativePeer(NamedPipeServerStream stream,InstalledReceiverPolicy configuration)
    {
        pipe=stream;policy=configuration;
        if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out uint pid)||pid==0||pid>int.MaxValue)throw new BrowserException(BrowserFailure.DesktopUntrusted);
        process=OpenProcess(0x00101000,false,pid); // QUERY_LIMITED_INFORMATION | SYNCHRONIZE, never elevate.
        try
        {
            if(process.IsInvalid||!GetProcessTimes(process,out long creation,out _,out _,out _))throw new BrowserException(BrowserFailure.DesktopUntrusted);
            using var current=Process.GetCurrentProcess();using var user=WindowsIdentity.GetCurrent();
            Identity=new((int)pid,current.SessionId,user.User?.Value??"",Path.Combine(Path.GetDirectoryName(policy.Role.AppPath)!,"KuaiYi.NativeHost.exe"),creation);
            if(!Verify())throw new BrowserException(BrowserFailure.DesktopUntrusted);
        }
        catch{process.Dispose();throw;}
    }
    internal bool Verify()
    {
        try
        {
            if(WaitForSingleObject(process,0)!=258||!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out uint pid)||pid!=Identity.ProcessId||
                !GetNamedPipeClientSessionId(pipe.SafePipeHandle,out uint session)||session!=Identity.SessionId||
                !ProcessIdToSessionId(pid,out uint actualSession)||actualSession!=session||
                !GetProcessTimes(process,out long creation,out _,out _,out _)||creation!=Identity.StartTime)return false;
            var name=new StringBuilder(32768);int length=name.Capacity;
            if(!QueryFullProcessImageName(process,0,name,ref length)||!string.Equals(name.ToString(),Identity.Path,StringComparison.OrdinalIgnoreCase))return false;
            if(!OpenProcessToken(process,8,out var token))return false;
            using(token)using(var user=new WindowsIdentity(token.DangerousGetHandle()))
                if(user.User?.Value!=Identity.UserSid)return false;
            policy.Role.VerifyFiles();return true;
        }
        catch(Exception e)when(e is Win32Exception or IOException or UnauthorizedAccessException or InvalidOperationException or BrowserException){return false;}
    }
    public void Dispose()=>process.Dispose();
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe,out uint session);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)] private static extern bool ProcessIdToSessionId(uint pid,out uint session);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern SafeProcessHandle OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetProcessTimes(SafeProcessHandle process,out long creation,out long exit,out long kernel,out long user);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern uint WaitForSingleObject(SafeProcessHandle process,uint milliseconds);
    [DllImport("kernel32.dll",EntryPoint="QueryFullProcessImageNameW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)] private static extern bool QueryFullProcessImageName(SafeProcessHandle process,uint flags,StringBuilder name,ref int length);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)] private static extern bool OpenProcessToken(SafeProcessHandle process,uint access,out SafeAccessTokenHandle token);
}
