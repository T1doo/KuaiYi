using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// Owned synthetic resources only. Never called by the original lifecycle runner.
public static class KuaiYiJobMemberWindowsFixture
{
    public struct Child { public IntPtr Process,Thread;public uint Id;public int Job;public bool Bound; }
    public sealed class Rejection
    {
        public KuaiYiJobMemberDiagnostic.Observation Observation;
        public int Lists,Opens,MembershipChecks,RunningChecks,ImageReads,Closes;
        public bool HandlesClosed;
    }
    public interface IFixtureApi
    {
        bool Supported { get; }
        IntPtr NewJob();bool LimitJob(IntPtr job);
        bool CreateSuspended(string executable,out Child child);
        bool Bind(IntPtr job,IntPtr process);bool Member(IntPtr job,IntPtr process);
        uint Active(IntPtr job);bool LimitsIntact(IntPtr job);
        KuaiYiJobMemberDiagnostic.Observation Observe(IntPtr job);
        Rejection RejectOwnedNonMember(IntPtr job,uint otherOwnedId);
        bool TerminateJob(IntPtr job);bool TerminateRoot(IntPtr process);bool RootRunning(IntPtr process);bool RootExited(IntPtr process);
        bool Close(IntPtr handle);void BeginCleanup();bool CleanupExpired { get; }void Pause();
    }
    public sealed class Result
    {
        public string Status { get; internal set; }="Fail";
        public string Stage { get; internal set; }="Initialize";
        public int ExitCode { get; internal set; }=3;
        public int Created { get; internal set; }
        public bool RootExitsConfirmed { get; internal set; }
        public bool JobsEmpty { get; internal set; }
        public bool FixtureHandlesClosed { get; internal set; }=true;
        public bool QueryHandlesClosed { get; internal set; }=true;
        public bool CleanupConfirmed { get; internal set; }
        public bool CleanupFailed { get; internal set; }
        public bool BudgetExpired { get; internal set; }
        public bool CleanupBudgetExpired { get; internal set; }
        public bool DirectTerminationUsed { get; internal set; }
        public string[] Checks { get; internal set; }=Array.Empty<string>();
        public KuaiYiJobMemberDiagnostic.Observation NativeObservation { get; internal set; }
        public Rejection NonMember { get; internal set; }
        public string PublicJson()
        {
            bool publish=Status=="Pass"&&CleanupConfirmed&&!BudgetExpired;
            var b=new StringBuilder("{\"Kind\":\"JobMemberWindowsFixture\",\"Scope\":\"SyntheticOwnedJobsOnly\",\"Status\":\"").Append(Status).Append("\",\"Stage\":\"").Append(Stage).Append("\",\"Checks\":[");
            for(int i=0;i<Checks.Length;i++){if(i>0)b.Append(',');b.Append('"').Append(Checks[i]).Append('"');}
            b.Append("],\"Created\":").Append(Created).Append(",\"ExitCode\":").Append(ExitCode);
            string[] names={"RootExitsConfirmed","JobsEmpty","FixtureHandlesClosed","QueryHandlesClosed","CleanupConfirmed","CleanupFailed","BudgetExpired","CleanupBudgetExpired","DirectTerminationUsed"};
            bool[] flags={RootExitsConfirmed,JobsEmpty,FixtureHandlesClosed,QueryHandlesClosed,CleanupConfirmed,CleanupFailed,BudgetExpired,CleanupBudgetExpired,DirectTerminationUsed};
            for(int i=0;i<names.Length;i++)b.Append(",\"").Append(names[i]).Append("\":").Append(flags[i]?"true":"false");
            b.Append(",\"NativeDiagnosticStatus\":\"").Append(NativeObservation==null?"NotRun":NativeObservation.State.ToString()).Append('"');
            b.Append(",\"NonMemberDiagnosticStatus\":\"").Append(NonMember==null||NonMember.Observation==null?"NotRun":NonMember.Observation.State.ToString()).Append('"');
            b.Append(",\"NativeObservation\":").Append(publish&&NativeObservation!=null?NativeObservation.PublicJson():"null");
            b.Append(",\"NonMemberObservation\":").Append(publish&&NonMember!=null&&NonMember.Observation!=null?NonMember.Observation.PublicJson():"null");
            if(publish&&NonMember!=null)
                b.Append(",\"NonMemberCounters\":{\"Lists\":").Append(NonMember.Lists).Append(",\"Opens\":").Append(NonMember.Opens).Append(",\"MembershipChecks\":").Append(NonMember.MembershipChecks).Append(",\"RunningChecks\":").Append(NonMember.RunningChecks).Append(",\"ImageReads\":").Append(NonMember.ImageReads).Append(",\"Closes\":").Append(NonMember.Closes).Append('}');
            else b.Append(",\"NonMemberCounters\":null");
            return b.Append(",\"PidReuseScope\":\"SubstitutedOwnedNonMember\",\"CMake\":\"NotRun\",\"FourCases\":\"NotRun\",\"ProductionReuse\":\"Off\",\"ModelCalls\":0}").ToString();
        }
    }
    private static bool Valid(IntPtr h){return h!=IntPtr.Zero&&h!=new IntPtr(-1);}
    private static void Require(bool condition){if(!condition)throw new InvalidOperationException();}
    private static void Check(Result result,List<string> checks,string name,bool condition)
    {result.Stage=name;Require(condition);checks.Add(name);}
    private static void Guard(Result result,Func<long> elapsed)
    {if(elapsed()>=29750){result.BudgetExpired=true;throw new TimeoutException();}}
    private static bool TryClose(IFixtureApi api,IntPtr handle)
    {if(!Valid(handle))return true;try{return api.Close(handle);}catch{return false;}}
    private static bool RootsRunning(IFixtureApi api,Child[] children,int count)
    {for(int i=0;i<count;i++)if(!api.RootRunning(children[i].Process))return false;return true;}
    public static bool IsRunningWait(uint outcome){return outcome==258;}
    private static int Classes(KuaiYiJobMemberDiagnostic.Observation row)
    {return row.MSBuild+row.PdbServer+row.Compiler+row.Linker+row.CMake+row.Other;}

    public static Result Run(IFixtureApi api,string executable,Func<long> elapsed)
    {
        var result=new Result();var checks=new List<string>();var children=new Child[9];var jobs=new IntPtr[2];
        if(api==null||elapsed==null)return result;
        try
        {
            result.Stage="HostedWindowsRequired";
            if(!api.Supported){result.Status="Blocked";result.ExitCode=2;return result;}
            Guard(result,elapsed);result.Stage="CreateJobs";
            for(int i=0;i<2;i++){jobs[i]=api.NewJob();Require(Valid(jobs[i]));Require(api.LimitJob(jobs[i]));}
            Check(result,checks,"TwoOwnedJobs",true);
            result.Stage="BindChildren";
            for(int i=0;i<9;i++)
            {
                Guard(result,elapsed);Require(api.CreateSuspended(executable,out Child child));
                children[i]=child;children[i].Job=i<8?0:1;result.Created++;
                Require(Valid(child.Process)&&Valid(child.Thread)&&child.Id!=0);
                Guard(result,elapsed);Require(api.Bind(jobs[children[i].Job],child.Process));
                Require(api.Member(jobs[children[i].Job],child.Process));children[i].Bound=true;
            }
            // No ResumeThread call: primary threads stay initially suspended until owned cleanup.
            Check(result,checks,"NineBoundInitiallySuspended",result.Created==9);
            Guard(result,elapsed);result.Stage="PrimaryJobAccounting";uint countA=api.Active(jobs[0]);
            Guard(result,elapsed);result.Stage="OtherJobAccounting";uint countB=api.Active(jobs[1]);
            Guard(result,elapsed);
            Check(result,checks,"OwnedJobCounts",countA==8&&countB==1);
            Guard(result,elapsed);Check(result,checks,"JobLimitsBeforeProbe",api.LimitsIntact(jobs[0])&&api.LimitsIntact(jobs[1]));
            Guard(result,elapsed);Check(result,checks,"OwnedRootsLiveBeforeProbe",RootsRunning(api,children,result.Created));
            Guard(result,elapsed);result.Stage="NativeCapture";result.NativeObservation=api.Observe(jobs[0]);
            result.QueryHandlesClosed=result.NativeObservation!=null&&result.NativeObservation.DiagnosticHandlesClosed;
            Guard(result,elapsed);
            Check(result,checks,"NativeEightMemberAbi",result.NativeObservation!=null&&result.NativeObservation.State==KuaiYiJobMemberDiagnostic.Status.Observed&&result.NativeObservation.Listed==8&&result.NativeObservation.Other==8&&Classes(result.NativeObservation)==8&&result.QueryHandlesClosed);
            Guard(result,elapsed);result.Stage="NonMemberRejection";result.NonMember=api.RejectOwnedNonMember(jobs[0],children[8].Id);
            Rejection n=result.NonMember;
            result.QueryHandlesClosed=result.QueryHandlesClosed&&n!=null&&n.HandlesClosed&&n.Observation!=null&&n.Observation.DiagnosticHandlesClosed;
            Guard(result,elapsed);
            Check(result,checks,"OwnedPidReplacementRejected",n!=null&&n.Observation!=null&&n.Observation.State==KuaiYiJobMemberDiagnostic.Status.Partial&&n.Observation.Listed==1&&n.Observation.MembershipUnconfirmed==1&&Classes(n.Observation)==0);
            Check(result,checks,"NonMemberZeroImageRead",n.Lists==1&&n.Opens==1&&n.MembershipChecks==1&&n.RunningChecks==0&&n.ImageReads==0&&n.Closes==1&&result.QueryHandlesClosed);
            Guard(result,elapsed);result.Stage="PreservedPrimaryJobAccounting";countA=api.Active(jobs[0]);
            Guard(result,elapsed);result.Stage="PreservedOtherJobAccounting";countB=api.Active(jobs[1]);
            Guard(result,elapsed);
            Check(result,checks,"BorrowedJobsPreserved",countA==8&&countB==1&&api.LimitsIntact(jobs[0])&&api.LimitsIntact(jobs[1]));
            Guard(result,elapsed);Check(result,checks,"OwnedRootsLiveAfterProbe",RootsRunning(api,children,result.Created));
            Guard(result,elapsed);result.Status="Pass";result.Stage="Completed";result.ExitCode=0;
        }
        catch { /* Finite stage remains; no paths, command strings, IDs or exceptions. */ }
        finally
        {
            if(result.Created>0||Valid(jobs[0])||Valid(jobs[1]))
            {
                var killed=new bool[2];
                try{api.BeginCleanup();}catch{result.CleanupFailed=true;}
                for(int i=0;i<2;i++)if(Valid(jobs[i]))try{killed[i]=api.TerminateJob(jobs[i]);if(!killed[i])result.CleanupFailed=true;}catch{result.CleanupFailed=true;}
                for(int i=0;i<result.Created;i++)
                    if(!children[i].Bound||!killed[children[i].Job])
                        try{result.DirectTerminationUsed=true;if(!api.TerminateRoot(children[i].Process))result.CleanupFailed=true;}catch{result.CleanupFailed=true;}
                try
                {
                    while(true)
                    {
                        bool roots=true,empty=true;
                        for(int i=0;i<result.Created;i++)roots=api.RootExited(children[i].Process)&&roots;
                        for(int i=0;i<2;i++)if(Valid(jobs[i]))empty=(api.Active(jobs[i])==0)&&empty;
                        result.RootExitsConfirmed=roots;result.JobsEmpty=empty;
                        result.CleanupBudgetExpired=api.CleanupExpired;
                        if((roots&&empty)||result.CleanupBudgetExpired)break;
                        api.Pause();
                    }
                }
                catch{result.CleanupFailed=true;}
                for(int i=0;i<result.Created;i++)
                {
                    if(!TryClose(api,children[i].Thread))result.FixtureHandlesClosed=false;
                    if(!TryClose(api,children[i].Process))result.FixtureHandlesClosed=false;
                }
                for(int i=0;i<2;i++)if(!TryClose(api,jobs[i]))result.FixtureHandlesClosed=false;
                try{result.CleanupBudgetExpired=result.CleanupBudgetExpired||api.CleanupExpired;}catch{result.CleanupFailed=true;}
                result.CleanupConfirmed=result.RootExitsConfirmed&&result.JobsEmpty&&result.FixtureHandlesClosed&&result.QueryHandlesClosed&&!result.CleanupFailed&&!result.CleanupBudgetExpired;
                if(!result.CleanupConfirmed){result.CleanupFailed=true;result.Status="Fail";result.ExitCode=3;if(result.Stage=="Completed")result.Stage="FixtureCleanup";}
                else checks.Add("OwnedCleanupConfirmed");
            }
            result.Checks=checks.ToArray();
        }
        return result;
    }
    public static Result RunWindows(string executable)
    {
        var clock=Stopwatch.StartNew();return Run(new WindowsApi(clock),executable,()=>clock.ElapsedMilliseconds);
    }
    public static string ChildEnvironment()
    {
        var values=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string name in new[]{"SystemRoot","WINDIR","SystemDrive","TEMP","TMP","PATH","ProgramFiles","ProgramFiles(x86)","ProgramW6432"})
        {string value=Environment.GetEnvironmentVariable(name);if(value!=null)values[name]=value;}
        values["POWERSHELL_TELEMETRY_OPTOUT"]="1";values["DOTNET_CLI_TELEMETRY_OPTOUT"]="1";values["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"]="1";
        var text=new StringBuilder();foreach(var pair in values)text.Append(pair.Key).Append('=').Append(pair.Value).Append('\0');return text.Append('\0').ToString();
    }
    public static string Quote(string value)
    {
        var b=new StringBuilder("\"");int slashes=0;
        foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='"')b.Append('\\',slashes*2+1).Append(c);else b.Append('\\',slashes).Append(c);slashes=0;}
        return b.Append('\\',slashes*2).Append('"').ToString();
    }
    private sealed class WindowsApi : IFixtureApi
    {
        private readonly Stopwatch clock;private Stopwatch cleanup;
        internal WindowsApi(Stopwatch source){clock=source;}
        public bool Supported=>KuaiYiJobMemberDiagnostic.SupportsNativeLayout(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),IntPtr.Size,RuntimeInformation.ProcessArchitecture,RuntimeInformation.OSArchitecture);
        public IntPtr NewJob(){return CreateJobObjectW(IntPtr.Zero,null);}
        public bool LimitJob(IntPtr job)
        {
            var limits=new ExtendedLimits();limits.Basic.Flags=0x2000;
            return SetHandleInformation(job,1,0)&&GetHandleInformation(job,out uint flags)&&(flags&1)==0&&SetInformationJobObject(job,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>());
        }
        public bool CreateSuspended(string executable,out Child child)
        {
            child=new Child();
            if(string.IsNullOrEmpty(executable)||executable.Length>4096||!Path.IsPathFullyQualified(executable)||!string.Equals(Path.GetFileName(executable),"pwsh.exe",StringComparison.OrdinalIgnoreCase))return false;
            var startup=new StartupInfo{Size=Marshal.SizeOf<StartupInfo>()};
            string code=Convert.ToBase64String(Encoding.Unicode.GetBytes("[Threading.Thread]::Sleep(60000)"));
            var command=new StringBuilder(Quote(executable)+" -NoLogo -NoProfile -NonInteractive -EncodedCommand "+code);
            IntPtr environment=Marshal.StringToHGlobalUni(ChildEnvironment());
            try
            {
                if(!CreateProcessW(executable,command,IntPtr.Zero,IntPtr.Zero,false,0x08000404,environment,null,ref startup,out ProcessInfo info))return false;
                child=new Child{Process=info.Process,Thread=info.Thread,Id=info.Pid};return true;
            }
            finally{Marshal.FreeHGlobal(environment);}
        }
        public bool Bind(IntPtr job,IntPtr process){return AssignProcessToJobObject(job,process);}
        public bool Member(IntPtr job,IntPtr process){return IsProcessInJob(process,job,out bool member)&&member;}
        public uint Active(IntPtr job){var info=new Accounting();if(!QueryAccounting(job,1,ref info,(uint)Marshal.SizeOf<Accounting>(),IntPtr.Zero))throw new InvalidOperationException();return info.Active;}
        public bool LimitsIntact(IntPtr job)
        {var limits=new ExtendedLimits();return GetHandleInformation(job,out uint flags)&&flags==0&&QueryLimits(job,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>(),IntPtr.Zero)&&limits.Basic.Flags==0x2000;}
        public KuaiYiJobMemberDiagnostic.Observation Observe(IntPtr job){return KuaiYiJobMemberDiagnostic.CaptureWindows(job,clock);}
        public Rejection RejectOwnedNonMember(IntPtr job,uint otherOwnedId)
        {
            var probe=new OwnedNonMemberProbe(job,otherOwnedId);
            probe.Result.Observation=KuaiYiJobMemberDiagnostic.Capture(job,()=>30000-clock.ElapsedMilliseconds,probe);
            return probe.Result;
        }
        public bool TerminateJob(IntPtr job){return TerminateJobObject(job,3);}
        public bool TerminateRoot(IntPtr process){return WaitForSingleObject(process,0)==0||TerminateProcess(process,3);}
        public bool RootRunning(IntPtr process){return IsRunningWait(WaitForSingleObject(process,0));}
        public bool RootExited(IntPtr process){return WaitForSingleObject(process,0)==0;}
        public bool Close(IntPtr handle){return CloseHandle(handle);}
        public void BeginCleanup(){cleanup=Stopwatch.StartNew();}
        public bool CleanupExpired=>cleanup==null||cleanup.ElapsedMilliseconds>=5000;
        public void Pause(){Thread.Sleep(10);}
    }
    private sealed class OwnedNonMemberProbe : KuaiYiJobMemberDiagnostic.IProbe
    {
        private readonly IntPtr job;private readonly uint ownedId;private IntPtr opened;
        internal readonly Rejection Result=new Rejection{HandlesClosed=true};
        internal OwnedNonMemberProbe(IntPtr source,uint id){job=source;ownedId=id;}
        public KuaiYiJobMemberDiagnostic.MemberList ReadList(IntPtr source)
        {
            Result.Lists++;Require(source==job);var bytes=new byte[72];
            Array.Copy(BitConverter.GetBytes((uint)1),0,bytes,0,4);Array.Copy(BitConverter.GetBytes((uint)1),0,bytes,4,4);Array.Copy(BitConverter.GetBytes((ulong)ownedId),0,bytes,8,8);
            return new KuaiYiJobMemberDiagnostic.MemberList{Success=true,WrittenBytes=16,Buffer=bytes};
        }
        public IntPtr Open(uint id)
        {Result.Opens++;Require(id==ownedId);opened=OpenProcess(0x101000,false,id);if(Valid(opened))Result.HandlesClosed=false;return opened;}
        public KuaiYiJobMemberDiagnostic.Verification IsMember(IntPtr process,IntPtr source)
        {Result.MembershipChecks++;Require(source==job&&process==opened);return IsProcessInJob(process,source,out bool member)?(member?KuaiYiJobMemberDiagnostic.Verification.Yes:KuaiYiJobMemberDiagnostic.Verification.No):KuaiYiJobMemberDiagnostic.Verification.Unknown;}
        public KuaiYiJobMemberDiagnostic.Verification IsRunning(IntPtr process){Result.RunningChecks++;throw new InvalidOperationException();}
        public string Image(IntPtr process){Result.ImageReads++;throw new InvalidOperationException();}
        public bool Close(IntPtr process)
        {Result.Closes++;Require(process==opened);bool closed=CloseHandle(process);Result.HandlesClosed=closed;if(closed)opened=IntPtr.Zero;return closed;}
    }
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
    [DllImport("kernel32.dll",EntryPoint="QueryInformationJobObject",SetLastError=true)] private static extern bool QueryAccounting(IntPtr job,int kind,ref Accounting value,uint size,IntPtr returned);
    [DllImport("kernel32.dll",EntryPoint="QueryInformationJobObject",SetLastError=true)] private static extern bool QueryLimits(IntPtr job,int kind,ref ExtendedLimits value,uint size,IntPtr returned);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool CreateProcessW(string application,StringBuilder command,IntPtr processAttributes,IntPtr threadAttributes,bool inherit,uint flags,IntPtr environment,string directory,ref StartupInfo startup,out ProcessInfo child);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool IsProcessInJob(IntPtr process,IntPtr job,out bool member);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool TerminateJobObject(IntPtr job,uint code);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool TerminateProcess(IntPtr process,uint code);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,uint id);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool CloseHandle(IntPtr handle);
}
