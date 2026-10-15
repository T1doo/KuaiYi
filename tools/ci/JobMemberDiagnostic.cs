using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

// Independent, opt-in CI diagnostic. No call site in the lifecycle runner.
// Only a caller-owned job handle in THIS process is accepted; never a foreign integer.
public static class KuaiYiJobMemberDiagnostic
{
    public const int Capacity=8, ListBytes=72, ImageCharacters=1024, ReserveMilliseconds=250;
    public enum Status { NotAttempted,Unsupported,Observed,Partial,ListUnavailable,CapacityExceeded,InvalidList,SkippedBudget,ReaderFailure,HandleCloseUnknown }
    public enum Verification { Unknown,No,Yes }
    public enum ImageKind { Unknown,MSBuild,PdbServer,Compiler,Linker,CMake,Other }
    public sealed class Observation
    {
        public Status State { get; internal set; }=Status.NotAttempted;
        public int Listed { get; internal set; }=-1;
        public int MSBuild { get; internal set; }
        public int PdbServer { get; internal set; }
        public int Compiler { get; internal set; }
        public int Linker { get; internal set; }
        public int CMake { get; internal set; }
        public int Other { get; internal set; }
        public int OpenUnavailable { get; internal set; }
        public int MembershipUnconfirmed { get; internal set; }
        public int RunningUnconfirmed { get; internal set; }
        public int ImageUnavailable { get; internal set; }
        public bool DiagnosticHandlesClosed { get; internal set; }=true;
        internal void ClearFacts()
        {
            Listed=-1;MSBuild=PdbServer=Compiler=Linker=CMake=Other=0;
            OpenUnavailable=MembershipUnconfirmed=RunningUnconfirmed=ImageUnavailable=0;
        }
        // Closed finite schema only. No original strings, process IDs, handles or errors.
        public string PublicJson()
        {
            var b=new StringBuilder("{\"Status\":\"").Append(State.ToString()).Append("\"");
            string[] names={"Listed","MSBuild","PdbServer","Compiler","Linker","CMake","Other","OpenUnavailable","MembershipUnconfirmed","RunningUnconfirmed","ImageUnavailable"};
            int[] values={Listed,MSBuild,PdbServer,Compiler,Linker,CMake,Other,OpenUnavailable,MembershipUnconfirmed,RunningUnconfirmed,ImageUnavailable};
            for(int i=0;i<names.Length;i++)b.Append(",\"").Append(names[i]).Append("\":").Append(values[i].ToString(CultureInfo.InvariantCulture));
            return b.Append(",\"DiagnosticHandlesClosed\":").Append(DiagnosticHandlesClosed?"true":"false").Append('}').ToString();
        }
    }
    public sealed class MemberList
    {
        public bool Success;
        public int Error;
        public uint WrittenBytes;
        public byte[] Buffer;
    }
    // Test seam uses the same complete state machine as the Windows implementation.
    public interface IProbe
    {
        MemberList ReadList(IntPtr queryJob);
        IntPtr Open(uint id);
        Verification IsMember(IntPtr process,IntPtr queryJob);
        Verification IsRunning(IntPtr process);
        string Image(IntPtr process);
        bool Close(IntPtr handle);
    }
    private sealed class BudgetExhausted : Exception { }
    private static bool Valid(IntPtr value){return value!=IntPtr.Zero&&value!=new IntPtr(-1);}
    private static void Guard(Func<long> remaining){if(remaining()<=ReserveMilliseconds)throw new BudgetExhausted();}
    private static bool Close(IProbe api,IntPtr handle){try{return api.Close(handle);}catch{return false;}}

    public static ImageKind Classify(string image)
    {
        if(string.IsNullOrEmpty(image)||image.Length>=ImageCharacters)return ImageKind.Unknown;
        foreach(char c in image)if(char.IsControl(c))return ImageKind.Unknown;
        bool drive=image.Length>3&&char.IsAsciiLetter(image[0])&&image[1]==':'&&image[2]=='\\';
        bool extended=image.Length>7&&image.StartsWith("\\\\?\\",StringComparison.Ordinal)&&char.IsAsciiLetter(image[4])&&image[5]==':'&&image[6]=='\\';
        if(!drive&&!extended)return ImageKind.Unknown;
        string name=image.Substring(image.LastIndexOf('\\')+1);
        if(name.Length==0||name.IndexOf('/')>=0||name.IndexOf('"')>=0)return ImageKind.Unknown;
        if(name.Equals("MSBuild.exe",StringComparison.OrdinalIgnoreCase))return ImageKind.MSBuild;
        if(name.Equals("mspdbsrv.exe",StringComparison.OrdinalIgnoreCase))return ImageKind.PdbServer;
        if(name.Equals("cl.exe",StringComparison.OrdinalIgnoreCase))return ImageKind.Compiler;
        if(name.Equals("link.exe",StringComparison.OrdinalIgnoreCase))return ImageKind.Linker;
        if(name.Equals("cmake.exe",StringComparison.OrdinalIgnoreCase))return ImageKind.CMake;
        return ImageKind.Other;
    }
    private static Status Parse(MemberList list,out uint[] ids)
    {
        ids=Array.Empty<uint>();
        if(list==null)return Status.ListUnavailable;
        if(!list.Success)return list.Error==234?Status.CapacityExceeded:Status.ListUnavailable;
        if(list.Buffer==null||list.Buffer.Length!=ListBytes||list.WrittenBytes<8||list.WrittenBytes>ListBytes)return Status.InvalidList;
        uint assigned=BitConverter.ToUInt32(list.Buffer,0),count=BitConverter.ToUInt32(list.Buffer,4);
        if(assigned<count)return Status.InvalidList;
        if(assigned>Capacity||count<assigned)return Status.CapacityExceeded;
        if(list.WrittenBytes<8+count*8)return Status.InvalidList;
        var values=new uint[count];var unique=new HashSet<uint>();
        for(int i=0;i<count;i++)
        {
            ulong value=BitConverter.ToUInt64(list.Buffer,8+i*8);
            if(value==0||value>uint.MaxValue||!unique.Add((uint)value))return Status.InvalidList;
            values[i]=(uint)value;
        }
        ids=values;return Status.Observed;
    }
    public static Observation Capture(IntPtr ownedJob,Func<long> remaining,IProbe api)
    {
        var result=new Observation();
        if(!Valid(ownedJob)||remaining==null||api==null)return result;
        try
        {
            // Borrowed only: never duplicate, close, inherit or change this job.
            Guard(remaining);MemberList list=api.ReadList(ownedJob);
            result.State=Parse(list,out uint[] ids);
            if(result.State!=Status.Observed)return result;
            result.Listed=ids.Length;
            foreach(uint id in ids)
            {
                if(!result.DiagnosticHandlesClosed)break;
                Guard(remaining);IntPtr process=api.Open(id);
                if(!Valid(process)){result.OpenUnavailable++;continue;}
                if(process==ownedJob){result.ClearFacts();result.State=Status.ReaderFailure;return result;}
                try
                {
                    Guard(remaining);
                    if(api.IsMember(process,ownedJob)!=Verification.Yes){result.MembershipUnconfirmed++;continue;}
                    Guard(remaining);
                    if(api.IsRunning(process)!=Verification.Yes){result.RunningUnconfirmed++;continue;}
                    Guard(remaining);ImageKind image=Classify(api.Image(process));
                    if(image==ImageKind.Unknown){result.ImageUnavailable++;continue;}
                    // Held process handle prevents PID reuse between these checks.
                    Guard(remaining);
                    if(api.IsMember(process,ownedJob)!=Verification.Yes){result.MembershipUnconfirmed++;continue;}
                    Guard(remaining);
                    if(api.IsRunning(process)!=Verification.Yes){result.RunningUnconfirmed++;continue;}
                    switch(image)
                    {
                        case ImageKind.MSBuild:result.MSBuild++;break;
                        case ImageKind.PdbServer:result.PdbServer++;break;
                        case ImageKind.Compiler:result.Compiler++;break;
                        case ImageKind.Linker:result.Linker++;break;
                        case ImageKind.CMake:result.CMake++;break;
                        case ImageKind.Other:result.Other++;break;
                    }
                }
                finally {if(!Close(api,process))result.DiagnosticHandlesClosed=false;}
                if(!result.DiagnosticHandlesClosed)break;
            }
            if(result.DiagnosticHandlesClosed)Guard(remaining);
            if(result.OpenUnavailable+result.MembershipUnconfirmed+result.RunningUnconfirmed+result.ImageUnavailable>0)result.State=Status.Partial;
        }
        catch(BudgetExhausted){result.ClearFacts();result.State=Status.SkippedBudget;}
        catch{result.ClearFacts();result.State=Status.ReaderFailure;}
        finally
        {
            if(!result.DiagnosticHandlesClosed){result.ClearFacts();result.State=Status.HandleCloseUnknown;}
        }
        return result;
    }
    // Must receive the ORIGINAL operation clock. Never grants a new 30-second budget.
    // Synchronous APIs have no hard cancellation guarantee; not integrated into Run.
    public static bool SupportsNativeLayout(bool windows,int pointerBytes,Architecture process,Architecture operatingSystem)
    {return windows&&pointerBytes==8&&process==Architecture.X64&&operatingSystem==Architecture.X64;}
    public static Observation CaptureWindows(IntPtr ownedJob,Stopwatch operationClock)
    {
        if(!Valid(ownedJob)||operationClock==null||!operationClock.IsRunning)return new Observation();
        if(!SupportsNativeLayout(RuntimeInformation.IsOSPlatform(OSPlatform.Windows),IntPtr.Size,RuntimeInformation.ProcessArchitecture,RuntimeInformation.OSArchitecture))
            return new Observation{State=Status.Unsupported};
        return Capture(ownedJob,()=>30000-operationClock.ElapsedMilliseconds,new WindowsProbe());
    }
    private sealed class WindowsProbe : IProbe
    {
        public MemberList ReadList(IntPtr queryJob)
        {
            var bytes=new byte[ListBytes];
            bool success=QueryInformationJobObject(queryJob,3,bytes,ListBytes,out uint written);
            int error=success?0:Marshal.GetLastWin32Error();
            return new MemberList{Success=success,Error=error,WrittenBytes=written,Buffer=bytes};
        }
        public IntPtr Open(uint id){return OpenProcess(0x101000,false,id);}
        public Verification IsMember(IntPtr process,IntPtr queryJob)
        {return IsProcessInJob(process,queryJob,out bool member)?(member?Verification.Yes:Verification.No):Verification.Unknown;}
        public Verification IsRunning(IntPtr process)
        {uint wait=WaitForSingleObject(process,0);return wait==258?Verification.Yes:wait==0?Verification.No:Verification.Unknown;}
        public string Image(IntPtr process)
        {
            var text=new StringBuilder(ImageCharacters);uint size=ImageCharacters;
            if(!QueryFullProcessImageNameW(process,0,text,ref size)||size==0||size>=ImageCharacters)return null;
            string image=text.ToString();return image.Length==size?image:null;
        }
        public bool Close(IntPtr handle){return CloseHandle(handle);}
    }
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool QueryInformationJobObject(IntPtr job,int kind,[Out] byte[] value,uint size,out uint written);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern IntPtr OpenProcess(uint access,bool inherit,uint id);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool IsProcessInJob(IntPtr process,IntPtr job,out bool member);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern uint WaitForSingleObject(IntPtr process,uint milliseconds);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool QueryFullProcessImageNameW(IntPtr process,uint flags,StringBuilder image,ref uint size);
    [DllImport("kernel32.dll",SetLastError=true)] private static extern bool CloseHandle(IntPtr handle);
}
