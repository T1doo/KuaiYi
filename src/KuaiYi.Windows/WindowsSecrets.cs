using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using KuaiYi.Core.Settings;
namespace KuaiYi.Windows;
public unsafe sealed class CurrentUserDpapiProtector:ISecretProtector
{
    [StructLayout(LayoutKind.Sequential)]private struct Blob{public int Length;public nint Data;}
    [DllImport("crypt32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptProtectData(ref Blob input,nint description,nint entropy,nint reserved,nint prompt,uint flags,out Blob output);
    [DllImport("crypt32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CryptUnprotectData(ref Blob input,nint description,nint entropy,nint reserved,nint prompt,uint flags,out Blob output);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
    public byte[] Protect(ReadOnlySpan<byte> plain)=>Transform(plain,true);
    public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>Transform(encrypted,false);
    private static byte[] Transform(ReadOnlySpan<byte> input,bool protect)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        fixed(byte* bits=input)
        {
            var source=new Blob{Length=input.Length,Data=(nint)bits};Blob output;
            // CRYPTPROTECT_UI_FORBIDDEN=1; no LOCAL_MACHINE flag, no UI prompt or supplied credential.
            bool success=protect?CryptProtectData(ref source,0,0,0,0,1,out output):CryptUnprotectData(ref source,0,0,0,0,1,out output);
            if(!success)throw new CryptographicException("DPAPI operation failed.",new Win32Exception(Marshal.GetLastPInvokeError()));
            try{if(output.Length is <1 or >32768||output.Data==0)throw new CryptographicException("Invalid DPAPI output.");return new ReadOnlySpan<byte>((void*)output.Data,output.Length).ToArray();}
            finally{if(output.Data!=0){if(output.Length is >0 and <=32768)CryptographicOperations.ZeroMemory(new Span<byte>((void*)output.Data,output.Length));LocalFree(output.Data);}}
        }
    }
}
public sealed class CurrentUserSecretDirectory:ISecretDirectoryPolicy
{
    [StructLayout(LayoutKind.Sequential)]private struct SecurityAttributes{public int Length;public nint Descriptor;public int InheritHandle;}
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string text,uint revision,out nint descriptor,out uint size);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool CreateDirectory(string path,ref SecurityAttributes attributes);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetFileSecurity(string path,uint information,nint descriptor);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
    public void EnsurePrivate(string directory)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        string parent=Path.GetDirectoryName(directory)??throw new IOException("Invalid private product directory.");Directory.CreateDirectory(parent);
        using var identity=WindowsIdentity.GetCurrent();string sid=identity.User?.Value??throw new IOException("Missing current-user SID.");
        if(!ConvertStringSecurityDescriptorToSecurityDescriptor("D:P(A;OICI;FA;;;"+sid+")",1,out nint descriptor,out _))throw new Win32Exception(Marshal.GetLastPInvokeError());
        try
        {
            var attributes=new SecurityAttributes{Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=descriptor};
            if(!CreateDirectory(directory,ref attributes)&&Marshal.GetLastPInvokeError()!=183)throw new Win32Exception(Marshal.GetLastPInvokeError());
            SettingsPathGuard(directory);
            if(!SetFileSecurity(directory,0x80000004,descriptor))throw new Win32Exception(Marshal.GetLastPInvokeError()); // Protected DACL, inheritable by newly created secret blobs.
        }
        finally{LocalFree(descriptor);}
    }
    private static void SettingsPathGuard(string path){if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Private product directory is a reparse point.");}
}
