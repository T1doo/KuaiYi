using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Imaging;
namespace KuaiYi.NativeHost;

/// <summary>Local current-user/session pipe, pinned installed App role; never launches App or downloads URLs.</summary>
public sealed class InstalledDesktopChannel(InstalledHostPolicy policy,Action? requireCurrent=null) : IDesktopConfirmationChannel
{
    private NamedPipeClientStream? pipe;
    private DesktopPipeChannel? channel;
    public ValueTask<BrowserGrant> OfferAsync(Guid operation,CancellationToken cancellation)=>OfferWithConfirmationAsync(operation,_=>ValueTask.CompletedTask,cancellation);
    public async ValueTask<BrowserGrant> OfferWithConfirmationAsync(Guid operation,Func<CancellationToken,ValueTask> waiting,CancellationToken cancellation)
    {
        requireCurrent?.Invoke();
        if(!OperatingSystem.IsWindows())throw new BrowserException(BrowserFailure.UnsupportedPlatform);
        pipe=new(".",policy.PipeName,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        try{await pipe.ConnectAsync(1500,cancellation).ConfigureAwait(false);}catch(TimeoutException){throw new BrowserException(BrowserFailure.DesktopUnavailable);}catch(IOException){throw new BrowserException(BrowserFailure.DesktopUnavailable);}
        VerifyServer(pipe,policy);requireCurrent?.Invoke();
        channel=new(pipe,policy.Origin,requireCurrent:requireCurrent);
        var grant=await channel.OfferWithConfirmationAsync(operation,waiting,cancellation);
        requireCurrent?.Invoke();return grant;
    }
    public ValueTask SubmitAsync(BrowserGrant grant,ReceivedImage image,ImageSize size,CancellationToken cancellation)
        {requireCurrent?.Invoke();return (channel??throw new BrowserException(BrowserFailure.DesktopUnavailable)).SubmitAsync(grant,image,size,cancellation);}
    public ValueTask CancelAsync(BrowserGrant grant,CancellationToken cancellation)
        =>channel!=null&&pipe?.IsConnected==true?channel.CancelAsync(grant,cancellation):ValueTask.CompletedTask;
    [SupportedOSPlatform("windows")]
    private static void VerifyServer(NamedPipeClientStream stream,InstalledHostPolicy policy)
    {
        if(!GetNamedPipeServerProcessId(stream.SafePipeHandle,out uint pid)||pid==0||pid>int.MaxValue)throw new BrowserException(BrowserFailure.DesktopUntrusted);
        try
        {
            using var server=Process.GetProcessById((int)pid);using var current=Process.GetCurrentProcess();
            if(server.SessionId!=current.SessionId||server.MainModule?.FileName is not{} path||!string.Equals(path,policy.AppPath,StringComparison.OrdinalIgnoreCase)||!string.Equals(InstalledHostPolicy.Digest(path),policy.AppSha256,StringComparison.OrdinalIgnoreCase))throw new BrowserException(BrowserFailure.DesktopUntrusted);
            policy.VerifyFiles();
        }
        catch(Exception e)when(e is InvalidOperationException or System.ComponentModel.Win32Exception or IOException){throw new BrowserException(BrowserFailure.DesktopUntrusted);}
    }
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(Microsoft.Win32.SafeHandles.SafePipeHandle pipe,out uint processId);
    public ValueTask DisposeAsync(){pipe?.Dispose();pipe=null;return ValueTask.CompletedTask;}
}
