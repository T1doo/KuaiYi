using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using KuaiYi.Core.Installation;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.App;

// Two-way verification of this original connected handle. A retained peer handle is never a kill/adoption capability.
internal sealed class WindowsInstallationImagePeer:IDisposable
{
    private readonly PipeStream pipe;
    private readonly bool server;
    private readonly string image;
    private readonly InstallationPeerToken own;
    private readonly SafeProcessHandle process;
    private readonly uint pid;
    private readonly long creation;
    internal WindowsInstallationImagePeer(PipeStream pipe,bool server,string expectedImage,InstallationPeerToken own)
    {
        this.pipe=pipe;this.server=server;image=expectedImage;this.own=own;pid=PeerId();process=OpenProcess(0x00101000,false,pid);
        try
        {
            if(process.IsInvalid||!GetProcessTimes(process,out creation,out _,out _,out _))throw Refused();RequireCurrent();
        }
        catch{process.Dispose();throw;}
    }
    internal static InstallationPeerToken Current(string expectedImage)
    {
        using var self=Process.GetCurrentProcess();RequireImage(self.SafeHandle,expectedImage);var token=ReadToken(self.SafeHandle);
        InstallationPeerPolicy.RequireSameSession(token,token);return token;
    }
    internal static void RequireOwnedChild(Process child,string image,InstallationPeerToken own)
    {if(child.HasExited)throw Refused();RequireImage(child.SafeHandle,image);InstallationPeerPolicy.RequireSameSession(own,ReadToken(child.SafeHandle));if(child.HasExited)throw Refused();}
    internal void RequireCurrent()
    {
        if(!pipe.IsConnected||WaitForSingleObject(process,0)!=258||PeerId()!=pid||!GetProcessTimes(process,out long now,out _,out _,out _)||now!=creation||!ProcessIdToSessionId(pid,out uint session)||session!=own.Session)throw Refused();
        bool success=server?GetNamedPipeClientSessionId(pipe.SafePipeHandle,out uint pipeSession):GetNamedPipeServerSessionId(pipe.SafePipeHandle,out pipeSession);
        if(!success||pipeSession!=session)throw Refused();RequireImage(process,image);InstallationPeerPolicy.RequireSameSession(own,ReadToken(process));
    }
    private uint PeerId()
    {
        bool success=server?GetNamedPipeClientProcessId(pipe.SafePipeHandle,out uint peer):GetNamedPipeServerProcessId(pipe.SafePipeHandle,out peer);
        if(!success||peer==0||peer>int.MaxValue)throw Refused();return peer;
    }
    private static void RequireImage(SafeProcessHandle process,string expected)
    {
        InstallationPaths.Check(expected);var name=new StringBuilder(32768);int length=name.Capacity;
        if(!QueryFullProcessImageName(process,0,name,ref length)||!string.Equals(Path.GetFullPath(name.ToString()),Path.GetFullPath(expected),StringComparison.OrdinalIgnoreCase))throw Refused();
    }
    private static InstallationPeerToken ReadToken(SafeProcessHandle process)
    {
        if(!OpenProcessToken(process,8,out var token))throw Refused();
        using(token)
        {
            string user=Info(token,1,(p,n)=>Sid(p,n,Marshal.ReadIntPtr(p)));
            string logon=Info(token,28,(p,n)=>
            {
                int offset=IntPtr.Size==8?8:4;if(n<offset+IntPtr.Size+4||Marshal.ReadInt32(p)!=1||((uint)Marshal.ReadInt32(p,offset+IntPtr.Size)&0xc0000000)!=0xc0000000)throw Refused();
                return Sid(p,n,Marshal.ReadIntPtr(p,offset));
            });
            long authentication=Info(token,10,(p,n)=>n>=56?Marshal.ReadInt64(p,8):throw Refused());
            int session=Info(token,12,(p,n)=>n>=4?Marshal.ReadInt32(p):throw Refused());
            string level=Info(token,25,(p,n)=>Sid(p,n,Marshal.ReadIntPtr(p)));
            int integrity=level=="S-1-16-8192"?8192:-1;
            bool Flag(int kind)=>Info(token,kind,(p,n)=>n>=4?Marshal.ReadInt32(p)!=0:throw Refused());
            return new(user,logon,authentication,session,integrity,Flag(20),Flag(29),Flag(26));
        }
    }
    private static string Sid(nint buffer,int size,nint pointer)
    {
        if(pointer<buffer||pointer>buffer+size-8)throw Refused();int length=8+Marshal.ReadByte(pointer,1)*4;
        if(length>size||pointer>buffer+size-length)throw Refused();return new SecurityIdentifier(pointer).Value;
    }
    private static T Info<T>(SafeAccessTokenHandle token,int kind,Func<nint,int,T> read)
    {
        _=GetTokenInformation(token,kind,0,0,out uint required);if(required is <4 or >65536)throw Refused();nint buffer=Marshal.AllocHGlobal((int)required);
        try{if(!GetTokenInformation(token,kind,buffer,required,out uint actual)||actual>required)throw Refused();return read(buffer,(int)actual);}
        finally{Marshal.FreeHGlobal(buffer);}
    }
    private static UnauthorizedAccessException Refused()=>new("Connected installation peer identity unconfirmed.");
    public void Dispose()=>process.Dispose();
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe,out uint session);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeServerSessionId(SafePipeHandle pipe,out uint session);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ProcessIdToSessionId(uint pid,out uint session);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern SafeProcessHandle OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetProcessTimes(SafeProcessHandle process,out long creation,out long exit,out long kernel,out long user);
    [DllImport("kernel32.dll")]private static extern uint WaitForSingleObject(SafeProcessHandle process,uint milliseconds);
    [DllImport("kernel32.dll",EntryPoint="QueryFullProcessImageNameW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool QueryFullProcessImageName(SafeProcessHandle process,uint flags,StringBuilder image,ref int length);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool OpenProcessToken(SafeProcessHandle process,uint access,out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetTokenInformation(SafeAccessTokenHandle token,int kind,nint buffer,uint length,out uint required);
}
