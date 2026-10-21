using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using KuaiYi.Core;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.NativeHost;

/// <summary>App's installed receiving role. Construction requires verified installation configuration;
/// production callers never supply peer claims. Does not register/install/configure anything.</summary>
public sealed class InstalledBrowserReceiver(InstalledReceiverPolicy policy,RuntimeGate gate,ILocalImageConfirmation confirmation,LocalImageInbox inbox,Func<BrowserReceiveCompletion,Task>? settled=null)
{
    private readonly object sync=new();private CancellationTokenSource? current;private Task currentSettlement=Task.CompletedTask;
    public string? FailureCode {get;private set;}
    public void Revoke(){CancellationTokenSource? held;lock(sync)held=current;try{held?.Cancel();}catch(ObjectDisposedException){ /* The captured iteration has already disposed its cancellation owner. */ }}
    public Task RevokeAsync()
    {
        CancellationTokenSource? held;Task original;lock(sync){held=current;original=currentSettlement;}
        Exception? failure=null;try{held?.Cancel();}catch(ObjectDisposedException)when(original.IsCompleted){}catch(Exception error){failure=error;}
        return failure==null?original:Settle(original,failure);
    }
    private static async Task Settle(Task original,Exception? failure)
    {await original.ConfigureAwait(false);if(failure!=null)throw new InvalidOperationException("Receive cancellation failed.",failure);}
    public async Task RunAsync(CancellationToken cancellation)
    {
        if(!OperatingSystem.IsWindows()){FailureCode="UnsupportedPlatform";return;}
        while(!cancellation.IsCancellationRequested)
        {
            var connection=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
            var finished=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock(sync){current=connection;currentSettlement=finished.Task;}
            try
            {
                await using var pipe=CreatePipe(policy.Role.PipeName);
                await pipe.WaitForConnectionAsync(connection.Token).ConfigureAwait(false);
                using var client=new WindowsNativePeer(pipe,policy);
                var receiving=new DesktopReceiverSession(gate,confirmation,inbox,policy.Origins,client.Identity,client.Verify);
                if(settled==null)await receiving.RunAsync(pipe,connection.Token).ConfigureAwait(false);
                else await receiving.RunAndNotifyAsync(pipe,settled,connection.Token).ConfigureAwait(false);FailureCode=receiving.FailureCode;
            }
            catch(OperationCanceledException)when(connection.IsCancellationRequested){FailureCode="Cancelled";}
            catch(Exception e)when(e is IOException or Win32Exception or UnauthorizedAccessException or BrowserException)
            {
                FailureCode=e is BrowserException b?b.Code.ToString():e.GetType().Name;
                // Bind/security failures stop rather than spin or fall back to an unauthenticated pipe.
                return;
            }
            finally{lock(sync){if(ReferenceEquals(current,connection))current=null;}connection.Dispose();finished.TrySetResult();}
        }
    }
    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreatePipe(string name)
    {
        using var user=WindowsIdentity.GetCurrent();string sid=user.User?.Value??throw new BrowserException(BrowserFailure.DesktopUntrusted);
        if(!ConvertStringSecurityDescriptorToSecurityDescriptor($"D:P(A;;GA;;;{sid})",1,out var descriptor,out _))throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var attributes=new SecurityAttributes{Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=descriptor,Inherit=false};
            // Duplex | overlapped | first instance. Byte mode | PIPE_REJECT_REMOTE_CLIENTS.
            var handle=CreateNamedPipe("\\\\.\\pipe\\"+name,0x40080003,8,1,65536,65536,0,ref attributes);
            if(handle.IsInvalid){handle.Dispose();throw new Win32Exception(Marshal.GetLastWin32Error());}
            try{return new(PipeDirection.InOut,true,false,handle);}catch{handle.Dispose();throw;}
        }
        finally{LocalFree(descriptor);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct SecurityAttributes{public int Length;public IntPtr Descriptor;[MarshalAs(UnmanagedType.Bool)]public bool Inherit;}
    [DllImport("advapi32.dll",EntryPoint="ConvertStringSecurityDescriptorToSecurityDescriptorW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl,uint revision,out IntPtr descriptor,out uint size);
    [DllImport("kernel32.dll",EntryPoint="CreateNamedPipeW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateNamedPipe(string name,uint openMode,uint pipeMode,uint instances,uint outputSize,uint inputSize,uint timeout,ref SecurityAttributes attributes);
    [DllImport("kernel32.dll")]private static extern IntPtr LocalFree(IntPtr pointer);
}
