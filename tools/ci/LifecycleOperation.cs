using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// Windows CI safety envelope only. No COM, target queries or production references.
public static class KuaiYiLifecycleOperation
{
    public sealed class Receipt
    {
        public string Status="Failure";public string Exit="Unknown";
        public bool RootExitConfirmed,JobEmpty,RescueUsed,CleanupConfirmed;
    }
    public static string Quote(string value)
    {
        var text=new StringBuilder("\"");int slashes=0;
        foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){text.Append('\\',slashes*2+1);text.Append(c);}else{text.Append('\\',slashes);text.Append(c);}slashes=0;}
        text.Append('\\',slashes*2);text.Append('"');return text.ToString();
    }
    public static Receipt Run(string executable,string[] arguments,string directory,string log)
    {
        var r=new Receipt();var clock=Stopwatch.StartNew();IntPtr job=IntPtr.Zero,output=IntPtr.Zero,input=IntPtr.Zero;ProcessInfo child=new ProcessInfo();bool created=false;
        try
        {
            job=CreateJobObjectW(IntPtr.Zero,null);if(job==IntPtr.Zero)throw new IOException();
            if(!SetHandleInformation(job,1,0)||!GetHandleInformation(job,out uint inheritance)||(inheritance&1)!=0)throw new IOException();
            var limits=new ExtendedLimits();limits.Basic.Flags=0x2000;
            if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits))))throw new IOException();
            var security=new SecurityAttributes{Length=Marshal.SizeOf(typeof(SecurityAttributes)),Inherit=1};
            output=CreateFileW(log,0x40000000,1,ref security,2,0x80,IntPtr.Zero);
            input=CreateFileW("NUL",0x80000000,3,ref security,3,0x80,IntPtr.Zero);
            if(output==new IntPtr(-1)||input==new IntPtr(-1))throw new IOException();
            var startup=new StartupInfo{Size=Marshal.SizeOf(typeof(StartupInfo)),Flags=0x100,Input=input,Output=output,Error=output};
            var command=new StringBuilder(Quote(executable));foreach(string arg in arguments)command.Append(' ').Append(Quote(arg));
            IntPtr environment=Marshal.StringToHGlobalUni(ChildEnvironment());
            try{created=CreateProcessW(executable,command,IntPtr.Zero,IntPtr.Zero,true,0x08000004|0x400,environment,directory,ref startup,out child);}finally{Marshal.FreeHGlobal(environment);}
            if(!created)throw new IOException();
            // Suspended creation binds the complete operation graph before any code can spawn children.
            if(!AssignProcessToJobObject(job,child.Process)||!IsProcessInJob(child.Process,job,out bool member)||!member)throw new IOException();
            if(clock.ElapsedMilliseconds>=30000||ResumeThread(child.Thread)==uint.MaxValue)throw new IOException();
            uint wait=WaitForSingleObject(child.Process,(uint)Math.Max(0,30000-clock.ElapsedMilliseconds));
            if(wait!=0||clock.ElapsedMilliseconds>=30000){r.Status="Timeout";r.RescueUsed=true;}
            else
            {
                r.RootExitConfirmed=true;
                if(!GetExitCodeProcess(child.Process,out uint code))throw new IOException();
                r.Exit=code<=3?code.ToString(System.Globalization.CultureInfo.InvariantCulture):"Other";
                while(!Empty(job)&&clock.ElapsedMilliseconds<30000)Thread.Sleep(10);
                r.JobEmpty=Empty(job);
                if(!r.JobEmpty||clock.ElapsedMilliseconds>=30000){r.Status="Timeout";r.RescueUsed=true;}else r.Status="Completed";
            }
        }
        catch{r.Status="Failure";r.RescueUsed=created;}
        finally
        {
            try
            {
                if(created&&(r.RescueUsed||!r.RootExitConfirmed||!r.JobEmpty))
                {
                    r.RescueUsed=true;
                    // This is unplanned rescue, never a passing parent-death or watchdog observation.
                    if(job!=IntPtr.Zero)TerminateJobObject(job,3);
                    if(WaitForSingleObject(child.Process,0)!=0)TerminateProcess(child.Process,3);
                    r.RootExitConfirmed=WaitForSingleObject(child.Process,2000)==0;
                    var cleanup=Stopwatch.StartNew();while(job!=IntPtr.Zero&&!Empty(job)&&cleanup.ElapsedMilliseconds<2000)Thread.Sleep(10);
                    r.JobEmpty=job!=IntPtr.Zero&&Empty(job);
                }
                r.CleanupConfirmed=created?r.RootExitConfirmed&&r.JobEmpty:true;
                if(!r.CleanupConfirmed)r.Status="CleanupFailure";
                if(output!=IntPtr.Zero&&output!=new IntPtr(-1))if(!FlushFileBuffers(output)){r.Status="CleanupFailure";r.CleanupConfirmed=false;}
            }
            catch{r.Status="CleanupFailure";r.CleanupConfirmed=false;}
            if(child.Thread!=IntPtr.Zero)CloseHandle(child.Thread);if(child.Process!=IntPtr.Zero)CloseHandle(child.Process);
            if(input!=IntPtr.Zero&&input!=new IntPtr(-1))CloseHandle(input);if(output!=IntPtr.Zero&&output!=new IntPtr(-1))CloseHandle(output);
            if(job!=IntPtr.Zero)CloseHandle(job);
        }
        return r;
    }
    private static bool Empty(IntPtr job){var data=new Accounting();if(!QueryInformationJobObject(job,1,ref data,(uint)Marshal.SizeOf(typeof(Accounting)),IntPtr.Zero))throw new IOException();return data.Active==0;}
    private static string ChildEnvironment()
    {
        var values=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string name in new[]{"SystemRoot","WINDIR","SystemDrive","TEMP","TMP","PATH","COMSPEC","ProgramFiles","ProgramFiles(x86)","ProgramW6432","USERPROFILE","LOCALAPPDATA","APPDATA","DOTNET_ROOT","DOTNET_ROOT_X64"}){string value=Environment.GetEnvironmentVariable(name);if(value!=null)values[name]=value;}
        values["DOTNET_CLI_TELEMETRY_OPTOUT"]="1";values["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"]="1";
        var block=new StringBuilder();foreach(var pair in values)block.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');return block.Append('\0').ToString();
    }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes{public int Length;public IntPtr Descriptor;public int Inherit;}
    [StructLayout(LayoutKind.Sequential)] private struct BasicLimits{public long ProcessTime,JobTime;public uint Flags;public UIntPtr Minimum,Maximum;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)] private struct IoCounters{public ulong Reads,Writes,Others,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)] private struct ExtendedLimits{public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
    [StructLayout(LayoutKind.Sequential)] private struct Accounting{public long User,Kernel,PeriodUser,PeriodKernel;public uint PageFaults,Total,Active,Terminated;}
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] private struct StartupInfo{public int Size;public string Reserved,Desktop,Title;public uint X,Y,XSize,YSize,XChars,YChars,Fill,Flags;public ushort Show,ReservedSize;public IntPtr ReservedData,Input,Output,Error;}
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInfo{public IntPtr Process,Thread;public uint Pid,Tid;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateJobObjectW(IntPtr attributes,string name);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool SetHandleInformation(IntPtr handle,uint mask,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetHandleInformation(IntPtr handle,out uint flags);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool SetInformationJobObject(IntPtr job,int kind,ref ExtendedLimits value,uint size);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool QueryInformationJobObject(IntPtr job,int kind,ref Accounting value,uint size,IntPtr returned);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool IsProcessInJob(IntPtr process,IntPtr job,out bool member);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool TerminateJobObject(IntPtr job,uint code);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool TerminateProcess(IntPtr process,uint code);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GetExitCodeProcess(IntPtr process,out uint code);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern uint ResumeThread(IntPtr thread);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll")] private static extern bool FlushFileBuffers(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateFileW(string path,uint access,uint share,ref SecurityAttributes security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CreateProcessW(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo child);
}
