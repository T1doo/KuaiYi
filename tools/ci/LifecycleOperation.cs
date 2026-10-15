using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// Windows CI safety envelope only. No COM, target queries or production references.
public static class KuaiYiLifecycleOperation
{
    public enum OperationStage { Start,CreateJob,VerifyJobInheritance,SetJobLimits,OpenOutput,OpenInput,PrepareProcess,CreateProcess,AssignJob,VerifyJobMembership,ResumeThread,WaitRoot,ReadExit,WaitJobEmpty,FlushOutput,Completed }
    public enum FailureKind { None,Win32,MissingLibrary,MissingEntry,Argument,Io,ManagedOther,Timeout }
    public enum NativeError { None,Zero,FileNotFound,PathNotFound,AccessDenied,InvalidHandle,InvalidParameter,BadImage,EnvVarMissing,DirectoryInvalid,ElevationRequired,Other }
    public enum JobState { Unknown,Empty,NonEmpty }
    public enum WaitOutcome { Completed,Timeout,Failed,Unexpected }
    public enum OperationReason { Completed,OperationFailure,OperationTimeout,OperationCleanupFailure }
    // Fixed-size copies of EXISTING successful accounting queries; never a new probe.
    public readonly struct AccountingSnapshot
    {
        public bool Known { get; }
        public uint? ActiveProcesses { get; }
        public uint? TotalProcesses { get; }
        internal AccountingSnapshot(uint active,uint total){Known=true;ActiveProcesses=active;TotalProcesses=total;}
    }
    public sealed class AccountingDiagnostic
    {
        public AccountingSnapshot FirstWait { get; private set; }
        public AccountingSnapshot LastPreRescue { get; private set; }
        private bool seen,recordFirst;
        internal void BeginQuery(){recordFirst=!seen;seen=true;LastPreRescue=default;}
        internal void RecordSuccess(uint active,uint total)
        {var snapshot=new AccountingSnapshot(active,total);if(recordFirst)FirstWait=snapshot;LastPreRescue=snapshot;}
    }
    public sealed class Receipt
    {
        public readonly AccountingDiagnostic Accounting=new AccountingDiagnostic();
        public string Status="Failure";public string Exit="Unknown";
        public bool RootExitConfirmed,JobEmpty,RescueUsed,CleanupConfirmed;
        public bool ProcessCreated,Resumed;
        public JobState PreRescueJobState=JobState.Unknown;
        public bool BudgetExpired;
        public OperationStage Stage=OperationStage.Start;
        public FailureKind Failure=FailureKind.None;
        public NativeError Error=NativeError.None;
    }
    // Finite projection only: never copy exception messages, paths, command lines or Data.
    public static void RecordFailure(Receipt receipt,Exception error)
    {
        receipt.Error=NativeError.None;
        if(error is Win32Exception native){receipt.Failure=FailureKind.Win32;receipt.Error=native.NativeErrorCode switch {0=>NativeError.Zero,2=>NativeError.FileNotFound,3=>NativeError.PathNotFound,5=>NativeError.AccessDenied,6=>NativeError.InvalidHandle,87=>NativeError.InvalidParameter,193=>NativeError.BadImage,203=>NativeError.EnvVarMissing,267=>NativeError.DirectoryInvalid,740=>NativeError.ElevationRequired,_=>NativeError.Other};}
        else receipt.Failure=error is TimeoutException?FailureKind.Timeout:error is DllNotFoundException?FailureKind.MissingLibrary:error is EntryPointNotFoundException?FailureKind.MissingEntry:error is ArgumentException?FailureKind.Argument:error is IOException?FailureKind.Io:FailureKind.ManagedOther;
    }
    public static WaitOutcome ClassifyWait(uint value,bool expired){return value==uint.MaxValue?WaitOutcome.Failed:value==258?WaitOutcome.Timeout:value!=0?WaitOutcome.Unexpected:expired?WaitOutcome.Timeout:WaitOutcome.Completed;}
    public static OperationReason GetReason(Receipt r)
    {
        if(r.Status=="CleanupFailure"||!r.CleanupConfirmed)return OperationReason.OperationCleanupFailure;
        if(r.Status=="Timeout"&&r.Failure==FailureKind.Timeout)return OperationReason.OperationTimeout;
        if(r.Status=="Completed"&&r.ProcessCreated&&r.Resumed&&r.RootExitConfirmed&&r.JobEmpty&&!r.RescueUsed&&r.Failure==FailureKind.None)return OperationReason.Completed;
        return OperationReason.OperationFailure;
    }
    // Diagnostic snapshots only. Cleanup must never overwrite these pre-rescue observations.
    public static void RecordJobObservation(Receipt r,bool empty){r.PreRescueJobState=empty?JobState.Empty:JobState.NonEmpty;}
    public static bool CompleteJobWait(Receipt r,bool empty,bool expired)
    {
        RecordJobObservation(r,empty);r.BudgetExpired=expired;r.JobEmpty=empty;
        return !empty||expired;
    }
    private static bool ObserveJobEmpty(IntPtr job,Receipt r)
    {
        r.PreRescueJobState=JobState.Unknown;
        bool empty=Empty(job,r.Accounting);RecordJobObservation(r,empty);return empty;
    }
    private static Win32Exception NativeFailure(){return new Win32Exception(Marshal.GetLastWin32Error());}
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
            r.Stage=OperationStage.CreateJob;job=CreateJobObjectW(IntPtr.Zero,null);if(job==IntPtr.Zero)throw NativeFailure();
            r.Stage=OperationStage.VerifyJobInheritance;
            if(!SetHandleInformation(job,1,0))throw NativeFailure();
            if(!GetHandleInformation(job,out uint inheritance))throw NativeFailure();
            if((inheritance&1)!=0)throw new IOException();
            var limits=new ExtendedLimits();limits.Basic.Flags=0x2000;
            r.Stage=OperationStage.SetJobLimits;
            if(!SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits))))throw NativeFailure();
            var security=new SecurityAttributes{Length=Marshal.SizeOf(typeof(SecurityAttributes)),Inherit=1};
            r.Stage=OperationStage.OpenOutput;
            output=CreateFileW(log,0x40000000,1,ref security,2,0x80,IntPtr.Zero);
            if(output==new IntPtr(-1))throw NativeFailure();
            r.Stage=OperationStage.OpenInput;
            input=CreateFileW("NUL",0x80000000,3,ref security,3,0x80,IntPtr.Zero);
            if(input==new IntPtr(-1))throw NativeFailure();
            r.Stage=OperationStage.PrepareProcess;
            var startup=new StartupInfo{Size=Marshal.SizeOf(typeof(StartupInfo)),Flags=0x100,Input=input,Output=output,Error=output};
            var command=new StringBuilder(Quote(executable));foreach(string arg in arguments)command.Append(' ').Append(Quote(arg));
            IntPtr environment=Marshal.StringToHGlobalUni(ChildEnvironment());
            // Capture last error before FreeHGlobal or any other native operation can replace it.
            int creationError=0;r.Stage=OperationStage.CreateProcess;
            try{created=CreateProcessW(executable,command,IntPtr.Zero,IntPtr.Zero,true,0x08000004|0x400,environment,directory,ref startup,out child);if(!created)creationError=Marshal.GetLastWin32Error();r.ProcessCreated=created;}finally{Marshal.FreeHGlobal(environment);}
            if(!created)throw new Win32Exception(creationError);
            // Suspended creation binds the complete operation graph before any code can spawn children.
            r.Stage=OperationStage.AssignJob;if(!AssignProcessToJobObject(job,child.Process))throw NativeFailure();
            r.Stage=OperationStage.VerifyJobMembership;if(!IsProcessInJob(child.Process,job,out bool member))throw NativeFailure();if(!member)throw new IOException();
            r.Stage=OperationStage.ResumeThread;r.BudgetExpired=clock.ElapsedMilliseconds>=30000;if(r.BudgetExpired)throw new TimeoutException();if(ResumeThread(child.Thread)==uint.MaxValue)throw NativeFailure();r.Resumed=true;
            r.Stage=OperationStage.WaitRoot;
            uint wait=WaitForSingleObject(child.Process,(uint)Math.Max(0,30000-clock.ElapsedMilliseconds));
            int waitError=wait==uint.MaxValue?Marshal.GetLastWin32Error():0;
            r.BudgetExpired=clock.ElapsedMilliseconds>=30000;
            WaitOutcome outcome=ClassifyWait(wait,r.BudgetExpired);
            if(outcome==WaitOutcome.Failed)throw new Win32Exception(waitError);
            if(outcome==WaitOutcome.Unexpected)throw new IOException();
            if(outcome==WaitOutcome.Timeout){r.Status="Timeout";r.Failure=FailureKind.Timeout;r.RescueUsed=true;}
            else
            {
                r.RootExitConfirmed=true;
                r.Stage=OperationStage.ReadExit;if(!GetExitCodeProcess(child.Process,out uint code))throw NativeFailure();
                r.Exit=code<=3?code.ToString(System.Globalization.CultureInfo.InvariantCulture):"Other";
                r.Stage=OperationStage.WaitJobEmpty;while(!ObserveJobEmpty(job,r)&&clock.ElapsedMilliseconds<30000)Thread.Sleep(10);
                bool empty=ObserveJobEmpty(job,r);
                if(CompleteJobWait(r,empty,clock.ElapsedMilliseconds>=30000)){r.Status="Timeout";r.Failure=FailureKind.Timeout;r.RescueUsed=true;}else r.Status="Completed";
            }
        }
        catch(Exception error){RecordFailure(r,error);r.Status=r.Failure==FailureKind.Timeout?"Timeout":"Failure";r.RescueUsed=created;}
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
                if(output!=IntPtr.Zero&&output!=new IntPtr(-1))if(!FlushFileBuffers(output)){if(r.Failure==FailureKind.None){r.Stage=OperationStage.FlushOutput;RecordFailure(r,NativeFailure());}r.Status="CleanupFailure";r.CleanupConfirmed=false;}
            }
            catch(Exception error){if(r.Failure==FailureKind.None)RecordFailure(r,error);r.Status="CleanupFailure";r.CleanupConfirmed=false;}
            if(child.Thread!=IntPtr.Zero)CloseHandle(child.Thread);if(child.Process!=IntPtr.Zero)CloseHandle(child.Process);
            if(input!=IntPtr.Zero&&input!=new IntPtr(-1))CloseHandle(input);if(output!=IntPtr.Zero&&output!=new IntPtr(-1))CloseHandle(output);
            if(job!=IntPtr.Zero)CloseHandle(job);
            if(r.Status=="Completed")r.Stage=OperationStage.Completed;
        }
        return r;
    }
    private static bool Empty(IntPtr job,AccountingDiagnostic diagnostic=null)
    {
        var data=new Accounting();if(diagnostic!=null)diagnostic.BeginQuery();
        if(!QueryInformationJobObject(job,1,ref data,(uint)Marshal.SizeOf(typeof(Accounting)),IntPtr.Zero))throw NativeFailure();
        if(diagnostic!=null)diagnostic.RecordSuccess(data.Active,data.Total);
        return data.Active==0;
    }
    private static string ChildEnvironment()
    {
        var values=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string name in new[]{"SystemRoot","WINDIR","SystemDrive","TEMP","TMP","PATH","COMSPEC","ProgramFiles","ProgramFiles(x86)","ProgramW6432","USERPROFILE","LOCALAPPDATA","APPDATA","DOTNET_ROOT","DOTNET_ROOT_X64"}){string value=Environment.GetEnvironmentVariable(name);if(value!=null)values[name]=value;}
        values["DOTNET_CLI_TELEMETRY_OPTOUT"]="1";values["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"]="1";
        // Configure-time compiler probes invoke MSBuild too; do not retain reusable build nodes.
        values["MSBUILDDISABLENODEREUSE"]="1";
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
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool FlushFileBuffers(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr CreateFileW(string path,uint access,uint share,ref SecurityAttributes security,uint creation,uint flags,IntPtr template);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CreateProcessW(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo child);
}
