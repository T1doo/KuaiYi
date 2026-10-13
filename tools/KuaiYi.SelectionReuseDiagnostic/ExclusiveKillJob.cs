using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.SelectionReuseDiagnostic;

public sealed class JobBeforeTargetGate(bool killOnClose,bool parentExclusive,bool nonInheritable)
{
    private bool assigned;
    public bool CanSendFirstRequest=>killOnClose&&parentExclusive&&nonInheritable&&assigned;
    public void VerifyAssignment(bool assignmentSucceeded,bool exactMembership)
    {assigned=false;if(!killOnClose||!parentExclusive||!nonInheritable||!assignmentSucceeded||!exactMembership)throw new InvalidOperationException("Job prerequisite failed; no fallback.");assigned=true;}
}

// No named job, no inherited/duplicated child handle, no breakaway flags. Not executed on Linux.
internal sealed class ExclusiveKillJob : IDisposable
{
    private readonly SafeJobHandle job;private readonly JobBeforeTargetGate gate=new(true,true,true);
    private ExclusiveKillJob(SafeJobHandle job){this.job=job;}
    public static ExclusiveKillJob Create()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Native diagnostic requires Windows.");
        if(!Stopwatch.IsHighResolution||!QueryPerformanceFrequency(out long frequency)||frequency!=Stopwatch.Frequency)throw new InvalidOperationException("Shared QPC clock unavailable; no fallback.");
        var job=CreateJobObjectW(IntPtr.Zero,null);
        try
        {
            if(job.IsInvalid)throw new Win32Exception();
            if(!SetHandleInformation(job,1,0)||!GetHandleInformation(job,out uint flags)||(flags&1)!=0)throw new Win32Exception();
            var limits=new ExtendedLimits{Basic=new BasicLimits{Flags=0x2000}};
            if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>()))throw new Win32Exception();
            return new(job);
        }
        catch{job.Dispose();throw;}
    }
    public void AssignVerified(Process held)
    {
        bool assigned=AssignProcessToJobObject(job,held.SafeHandle);
        bool queried=IsProcessInJob(held.SafeHandle,job,out bool member);
        gate.VerifyAssignment(assigned,queried&&member);
        if(!gate.CanSendFirstRequest)throw new InvalidOperationException("Job not ready.");
    }
    public void Dispose()=>job.Dispose();
    private sealed class SafeJobHandle:SafeHandleZeroOrMinusOneIsInvalid
    {public SafeJobHandle():base(true){}protected override bool ReleaseHandle()=>CloseHandle(handle);}
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits
    {public long ProcessTime,JobTime;public uint Flags;public nuint Minimum,Maximum;public uint Active;public nuint Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)] private struct Counters
    {public ulong Reads,Writes,Others,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits
    {public BasicLimits Basic;public Counters Io;public nuint ProcessMemory,JobMemory,PeakProcess,PeakJob;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern SafeJobHandle CreateJobObjectW(IntPtr attributes,string? name);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetHandleInformation(SafeJobHandle handle,uint mask,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetHandleInformation(SafeJobHandle handle,out uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool SetInformationJobObject(SafeJobHandle handle,int kind,ref ExtendedLimits info,uint bytes);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool AssignProcessToJobObject(SafeJobHandle handle,SafeProcessHandle process);
    [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsProcessInJob(SafeProcessHandle process,SafeJobHandle job,out bool member);
    [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool QueryPerformanceFrequency(out long frequency);
}
