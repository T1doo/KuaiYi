using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using KuaiYi.Contracts;
using KuaiYi.Core.Installation;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.App;

internal enum InstallationImageDelivery { NoEndpoint,Rejected,PreviewReady }
internal static class WindowsInstallationImagePipe
{
    // Explicit read/write/attributes/synchronize, WITHOUT FILE_CREATE_PIPE_INSTANCE (same bit as APPEND_DATA).
    private const uint ClientAccess=0x0012019b;
    private static string Name(string root,InstallationPeerToken own)=>"KuaiYi.Image.v1."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToUpperInvariant()+"|"+own.LogonSid+"|"+own.Session)));
    internal static async Task RunAsync(InstallationUpgradeCoordinator.LiveImageSession live,CancellationToken cancellation)
    {
        var context=new InstallationImageContext(live.Proof.Version.DirectoryPath,live.Proof.UserData);var own=WindowsInstallationImagePeer.Current(context.Image);
        await live.VerifyAsync(cancellation).ConfigureAwait(false);await using var pipe=CreateServer(Name(context.Root,own),own.LogonSid);
        while(!cancellation.IsCancellationRequested&&live.MemoryCurrent)
        {
            try
            {
                await pipe.WaitForConnectionAsync(cancellation).ConfigureAwait(false);live.RequireRecords();
                using var peer=new WindowsInstallationImagePeer(pipe,true,context.Image,own);
                WindowsInstallationImagePeer.RequireOwnedChild(live.OriginalChild,context.Image,own);
                await InstallationImagePipeConnection.ServeAsync(pipe,()=>{peer.RequireCurrent();live.RequireRecords();context.RequireCurrent();WindowsInstallationImagePeer.RequireOwnedChild(live.OriginalChild,context.Image,own);},live.VerifyAsync,live.Client,cancellation).ConfigureAwait(false);
            }
            catch(OperationCanceledException)when(cancellation.IsCancellationRequested){return;}
            catch(Exception error)when(error is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException or OperationCanceledException or InvalidOperationException)
            {
                // One failed connection cannot acquire another request or close somebody else's App.
                if(live.Client.Busy)throw new IOException("Original warm request settlement unconfirmed.",error);
                if(!live.MemoryCurrent)return;
            }
            finally{if(pipe.IsConnected)pipe.Disconnect();} // Reuse this original first-instance handle; no name reclamation loop.
        }
    }
    internal static async Task<InstallationImageDelivery> TrySubmitAsync(string directory,string userData,string path)
    {
        var context=new InstallationImageContext(directory,userData);var own=WindowsInstallationImagePeer.Current(context.Image);
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await context.VerifyAsync(timeout.Token).ConfigureAwait(false);
        var handle=CreateFile("\\\\.\\pipe\\"+Name(context.Root,own),ClientAccess,0,0,3,0x40110000,0);
        if(handle.IsInvalid){int code=Marshal.GetLastWin32Error();handle.Dispose();if(code==2)return InstallationImageDelivery.NoEndpoint;throw new Win32Exception(code);}
        await using var pipe=new NamedPipeClientStream(PipeDirection.InOut,true,true,handle);using var peer=new WindowsInstallationImagePeer(pipe,false,context.Image,own);
        var hello=new InstallationImageHello(InstallationWarmIdentity.NewNonce());Guid correlation=Guid.NewGuid();
        await InstallationImagePipeWire.WriteAsync(pipe,IpcMessageType.Ping,correlation,hello,timeout.Token).ConfigureAwait(false);
        var challengeFrame=await InstallationImagePipeWire.ReadAsync(pipe,IpcMessageType.Ping,timeout.Token).ConfigureAwait(false);var challenge=InstallationImagePipeWire.Decode<InstallationImageChallenge>(challengeFrame);
        var grant=InstallationWarmImageWire.Decode<InstallationWarmGrant>(challenge.Grant);
        if(challenge.Hello!=correlation||grant.Identity.Request!=challengeFrame.RequestId||grant.Identity.ClientNonce!=hello.ClientNonce)throw new InvalidDataException("Warm challenge correlation differs.");
        var proof=context.RequireGrant(grant);await context.VerifyAsync(timeout.Token).ConfigureAwait(false);peer.RequireCurrent();proof.RequireImageRecords();
        await InstallationImagePipeWire.WriteAsync(pipe,IpcMessageType.SubmitImagePath,grant.Identity.Request,new InstallationImageSubmit(challenge.Grant,path),timeout.Token).ConfigureAwait(false);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));var resultFrame=await InstallationImagePipeWire.ReadAsync(pipe,IpcMessageType.SubmitImagePath,timeout.Token).ConfigureAwait(false);
        var result=InstallationWarmImageWire.Decode<InstallationWarmResult>(InstallationImagePipeWire.Decode<string>(resultFrame));peer.RequireCurrent();context.RequireGrant(grant);
        if(resultFrame.RequestId!=grant.Identity.Request||result.Identity!=grant.Identity||result.Outcome==InstallationWarmOutcome.PreviewReady&&result.SourceVersion==Guid.Empty)throw new InvalidDataException("Warm terminal identity differs.");
        return result.Outcome==InstallationWarmOutcome.PreviewReady?InstallationImageDelivery.PreviewReady:InstallationImageDelivery.Rejected;
    }
    private static NamedPipeServerStream CreateServer(string name,string logon)
    {
        if(!ConvertStringSecurityDescriptorToSecurityDescriptor($"D:P(A;;0x{ClientAccess:x};;;{logon})",1,out nint descriptor,out _))throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            var attributes=new SecurityAttributes{Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=descriptor};var handle=CreateNamedPipe("\\\\.\\pipe\\"+name,0x40080003,8,1,65536,65536,0,ref attributes);
            if(handle.IsInvalid){int code=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(code);}try{return new(PipeDirection.InOut,true,false,handle);}catch{handle.Dispose();throw;}
        }
        finally{LocalFree(descriptor);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct SecurityAttributes{public int Length;public nint Descriptor;[MarshalAs(UnmanagedType.Bool)]public bool Inherit;}
    [DllImport("advapi32.dll",EntryPoint="ConvertStringSecurityDescriptorToSecurityDescriptorW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl,uint revision,out nint descriptor,out uint size);
    [DllImport("kernel32.dll",EntryPoint="CreateNamedPipeW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateNamedPipe(string name,uint openMode,uint mode,uint instances,uint outputSize,uint inputSize,uint timeout,ref SecurityAttributes attributes);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateFile(string name,uint access,uint share,nint attributes,uint creation,uint flags,nint template);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
}

internal sealed partial class InstallationSessionEntry
{
    static partial void SelectWindowsImageEndpoint(IInstallationApplicationProtocol protocol,ref Func<InstallationUpgradeCoordinator.LiveImageSession,CancellationToken,Task>? endpoint)
    {if(protocol is InstallationApplicationClient)endpoint=WindowsInstallationImagePipe.RunAsync;}
}
