using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

// Independent diagnostic lease. Never changes the lifecycle operation envelope.
public sealed class KuaiYiConfigureRootLease : IDisposable
{
    private IntPtr root;
    public long Handle { get { if(root==IntPtr.Zero)throw new IOException();return root.ToInt64(); } }
    public int SourceProcess { get; private set; }
    public uint Volume { get; private set; }
    public ulong Index { get; private set; }
    private KuaiYiConfigureRootLease() { }

    public static KuaiYiConfigureRootLease Open(string path)
    {
        // Only the freshly created caller-owned local output directory is accepted.
        if(string.IsNullOrEmpty(path)||path.Length>4096||path.IndexOf('\0')>=0||
           path.Length<3||!char.IsAsciiLetter(path[0])||path[1]!=':'||path[2]!='\\'||
           !string.Equals(Path.GetFullPath(path),path,StringComparison.OrdinalIgnoreCase))throw new IOException();
        var lease=new KuaiYiConfigureRootLease();IntPtr buffer=IntPtr.Zero,text=IntPtr.Zero;
        try
        {
            string name="\\??\\"+path;buffer=Marshal.StringToHGlobalUni(name);
            var unicode=new UnicodeString{Length=checked((ushort)(name.Length*2)),MaximumLength=checked((ushort)(name.Length*2+2)),Buffer=buffer};
            text=Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());Marshal.StructureToPtr(unicode,text,false);
            var attributes=new ObjectAttributes{Length=(uint)Marshal.SizeOf<ObjectAttributes>(),ObjectName=text,Attributes=0x1040};
            // FILE_OPEN only; no creation, content access, delete sharing or inheritance.
            int status=NtCreateFile(out lease.root,0x1000a0,ref attributes,out IoStatus io,IntPtr.Zero,0,3,1,0x200020,IntPtr.Zero,0);
            if(status!=0||lease.root==IntPtr.Zero||io.Information.ToUInt64()!=1)throw new IOException();
            if(!GetHandleInformation(lease.root,out uint flags)||(flags&1)!=0||GetFileType(lease.root)!=1||
               !GetFileInformationByHandle(lease.root,out FileInformation info))throw new IOException();
            if((info.Attributes&0x10)==0||(info.Attributes&(0x400|0x1000|0x40))!=0||info.Volume==0)throw new IOException();
            lease.Volume=info.Volume;lease.Index=((ulong)info.IndexHigh<<32)|info.IndexLow;
            if(lease.Index==0)throw new IOException();
            using(var current=Process.GetCurrentProcess())lease.SourceProcess=current.Id;
            return lease;
        }
        catch {lease.Dispose();throw;}
        finally {if(text!=IntPtr.Zero)Marshal.FreeHGlobal(text);if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);}
    }
    public void Dispose()
    {
        if(root==IntPtr.Zero)return;
        if(!CloseHandle(root))throw new IOException();
        root=IntPtr.Zero;GC.SuppressFinalize(this);
    }
    ~KuaiYiConfigureRootLease(){if(root!=IntPtr.Zero)CloseHandle(root);}
    [StructLayout(LayoutKind.Sequential)] private struct UnicodeString{public ushort Length,MaximumLength;public IntPtr Buffer;}
    [StructLayout(LayoutKind.Sequential)] private struct ObjectAttributes{public uint Length;public IntPtr RootDirectory,ObjectName;public uint Attributes;public IntPtr SecurityDescriptor,SecurityQualityOfService;}
    [StructLayout(LayoutKind.Sequential)] private struct IoStatus{public IntPtr Status;public UIntPtr Information;}
    [StructLayout(LayoutKind.Sequential)] private struct FileInformation{public uint Attributes,CreateLow,CreateHigh,AccessLow,AccessHigh,WriteLow,WriteHigh,Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
    [DllImport("ntdll.dll")] private static extern int NtCreateFile(out IntPtr handle,uint access,ref ObjectAttributes attributes,out IoStatus io,IntPtr allocation,uint fileAttributes,uint sharing,uint disposition,uint options,IntPtr ea,uint eaLength);
    [DllImport("kernel32.dll")] private static extern bool GetHandleInformation(IntPtr handle,out uint flags);
    [DllImport("kernel32.dll")] private static extern uint GetFileType(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool GetFileInformationByHandle(IntPtr handle,out FileInformation info);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}
