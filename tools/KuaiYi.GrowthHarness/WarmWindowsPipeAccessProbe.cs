using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Text.Json;
using KuaiYi.App;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.GrowthHarness;

// Explicit CI evidence only. Original production pipe policy, real own elevated identity, no App/UI/Worker invocation.
internal static class WarmWindowsPipeAccessProbe
{
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private static readonly List<object> errors=[],fixtures=[];
    private static object? Call(Type type,string name,params object?[] args)
    {
        try{return type.GetMethod(name,Static)!.Invoke(null,args);}
        catch(TargetInvocationException error)when(error.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
    }
    private static void Need(bool value,string code){if(!value){var error=new InvalidOperationException(code);error.Data["ProbeCode"]=code;throw error;}}
    private static object Error(Exception error)=>new{Type=error.GetType().Name,error.HResult,Code=error.Data["ProbeCode"],Win32=error is Win32Exception native?(int?)native.NativeErrorCode:null,Stack=error.StackTrace,Inner=error is AggregateException many?many.InnerExceptions.Select(Error).ToArray():error.InnerException is {} inner?[Error(inner)]:Array.Empty<object>()};
    internal static int Run(string result)
    {
        if(!OperatingSystem.IsWindows()||IntPtr.Size!=8||Environment.GetEnvironmentVariable("GITHUB_ACTIONS")!="true"||Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")!="T1doo/KuaiYi"||Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT")!="1")return 2;
        errors.Clear();fixtures.Clear();bool claimed=false;string output=Path.GetFullPath(result);
        try
        {
            Need(Path.IsPathFullyQualified(output)&&!output.StartsWith("\\\\",StringComparison.Ordinal),"LocalAbsoluteResultRequired");
            for(string? current=Path.GetDirectoryName(output);current!=null;current=Path.GetDirectoryName(current))Need((File.GetAttributes(current)&FileAttributes.ReparsePoint)==0,"LinkedResultDirectoryRefused");
            using(var owner=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read))owner.Flush(true);claimed=true;
            using var self=Process.GetCurrentProcess();string image=Environment.ProcessPath??throw new InvalidOperationException("SelfImageMissing");
            object own=Call(typeof(WindowsInstallationImagePeer),"ReadToken",self.SafeHandle)!;
            Need((bool)own.GetType().GetProperty("Elevated")!.GetValue(own)!,"ActualElevatedRunnerRequired");
            uint rights=(uint)typeof(WindowsInstallationImagePipe).GetField("ClientAccess",Static)!.GetRawConstantValue()!;
            Need(rights==0x12019b&&(rights&4)==0,"OriginalClientMaskChanged");
            // A measurement failure/security red never masks the independent exact-mask fixture.
            bool settled=Fixture("client-bit4-granted-query",rights|4,rights,own,self,image,false).GetAwaiter().GetResult();
            if(settled)Fixture("exact-mask-and-real-bidirectional-elevated-peers",rights,rights,own,self,image,true).GetAwaiter().GetResult();
            else Need(false,"OriginalTrialCleanupUnconfirmed_NoNextFixture");
        }
        catch(Exception error){errors.Add(Error(error));}
        if(!claimed)return 3;
        try
        {
            File.WriteAllText(output,JsonSerializer.Serialize(new{Kind="RealOwnPipeGrantedAccessReview",Status=errors.Count==0?"Pass_MeasurementsOnly":"Fail",Errors=errors,Fixtures=fixtures,
                HistoricalValidBit4ClientHandle="Preserved_r02_NotA_ServerInstanceTest",ServerInstanceDaclRefusal="NotRun_RequiresSeparateReviewedTest",ProductionAclModified=false,
                SyntheticIdentity=false,PositiveWarm="Blocked",Images="NotRun_PreviousThreeCasesNotRepeated",AppUi="NotRun",Worker="NotRun",ModelCalls=0,Package="Hold",ProductionReuse="Off"},new JsonSerializerOptions{WriteIndented=true}));
        }
        catch(Exception error){errors.Add(Error(error));Console.Error.WriteLine("KUAIYI_PIPE_EVIDENCE_FAILURE "+JsonSerializer.Serialize(new{Status="Fail",Errors=errors}));}
        return errors.Count==0?0:3;
    }
    private static async Task<bool> Fixture(string kind,uint requested,uint rights,object own,Process self,string image,bool peers)
    {
        Dictionary<string,object?> row=new(){["Name"]=kind,["RequestedAccess"]=requested,["EndpointRequested"]="Client_CreateFile",["PeerFixture"]="RealSameProcessElevated_NotPositiveWarm"};
        fixtures.Add(row);List<Exception> body=[],cleanup=[];NamedPipeServerStream? server=null;SafePipeHandle? serverHandle=null,handle=null;
        NamedPipeClientStream? client=null;Task? connecting=null;using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        bool clientDisposed=false,handleDisposed=false,serverDisposed=false,waitJoined=false;
        try
        {
            string logon=(string)own.GetType().GetProperty("LogonSid")!.GetValue(own)!;
            string name="KuaiYi.PipeAccessProbe."+Guid.NewGuid().ToString("N");
            server=(NamedPipeServerStream)Call(typeof(WindowsInstallationImagePipe),"CreateServer",name,logon)!;serverHandle=server.SafePipeHandle;
            ExactOriginalDacl(serverHandle,logon,rights);row["ExactOriginalDaclStructure"]=true;row["DaclOperationalRefusal"]="NotTested";
            connecting=server.WaitForConnectionAsync(deadline.Token);
            handle=CreateFile("\\\\.\\pipe\\"+name,requested,0,0,3,0x40110000,0);int code=Marshal.GetLastWin32Error();
            row["HandleInvalid"]=handle.IsInvalid;row["CreateFileWin32OnFailure"]=handle.IsInvalid?(int?)code:null;
            Need(!handle.IsInvalid,"ClientOpenFailed_QueryUnconfirmed");
            // Query THIS original valid client handle while it is held. No duplicated/borrowed token/handle.
            uint? granted=Measure(handle,row);
            Capture(body,()=>Need(row.GetValueOrDefault("ActualEndpoint") is "Client","ActualClientEndpointUnconfirmed"));
            if(granted.HasValue)
            {
                row["Bit4Granted"]=(granted.Value&4)!=0;
                if((granted.Value&4)!=0){row["SecurityGate"]="Fail_ClientBit4Granted_ServerImplicationUnconfirmed";Capture(body,()=>Need(false,"ClientBit4Granted_SecurityReviewRequired"));}
                else row["SecurityGate"]="Measured_ClientBit4Absent_NoServerInstanceConclusion";
                if(peers)Capture(body,()=>Need(granted.Value==rights,"ExactMaskGrantedAccessDiffers"));
            }
            else Capture(body,()=>Need(false,"GrantedAccessQueryUnconfirmed"));
            // Query errors are retained, but must not prevent the original connection/peer negative checks.
            client=new NamedPipeClientStream(PipeDirection.InOut,true,true,handle);await connecting;
            row["OriginalConnectionSucceeded"]=connecting.IsCompletedSuccessfully;
            if(peers)
            {
                Capture(body,()=>
                {
                    Need(GetNamedPipeClientProcessId(serverHandle,out uint clientPid),"ClientPidQueryFailed");row["ActualClientPid"]=clientPid;
                    Need(GetNamedPipeServerProcessId(client.SafePipeHandle,out uint serverPid),"ServerPidQueryFailed");row["ActualServerPid"]=serverPid;
                    Need(GetNamedPipeClientSessionId(serverHandle,out uint clientSession),"ClientSessionQueryFailed");row["ActualClientSession"]=clientSession;
                    Need(GetNamedPipeServerSessionId(client.SafePipeHandle,out uint serverSession),"ServerSessionQueryFailed");row["ActualServerSession"]=serverSession;
                    Need(clientPid==self.Id&&serverPid==self.Id&&clientSession==self.SessionId&&serverSession==self.SessionId,"OriginalPeerPidSessionMismatch");row["ActualPeerPidSessionMatched"]=true;
                });
                void Reject(string key,PipeStream pipe,bool isServer,string expected)=>Capture(body,()=>
                {
                    bool rejected=false;
                    try{using var unexpected=(IDisposable)Activator.CreateInstance(typeof(WindowsInstallationImagePeer),BindingFlags.Instance|BindingFlags.NonPublic,null,[pipe,isServer,expected,own],null)!;}
                    catch(TargetInvocationException error)when(error.InnerException is UnauthorizedAccessException){rejected=true;}
                    row[key]=rejected;Need(rejected,key+"_UnexpectedAcceptance");
                });
                Reject("ServerElevatedRejected",server,true,image);Reject("ClientElevatedRejected",client,false,image);
                string wrong=Path.Combine(AppContext.BaseDirectory,"KuaiYi.App.dll");Need(File.Exists(wrong),"RealWrongImageFixtureMissing");
                Reject("ServerWrongImagePlusElevatedRejected",server,true,wrong);Reject("ClientWrongImagePlusElevatedRejected",client,false,wrong);
                row["WrongImageNegativeScope"]="CombinedWithElevated_NotIndependentImagePolicyProof";
            }
        }
        catch(Exception error){body.Add(error);}
        finally
        {
            // Every original operation is separately observed. IsClosed alone never confirms a throwing dispose.
            Task? clientDisposal=null;
            try{if(client!=null){clientDisposal=client.DisposeAsync().AsTask();await clientDisposal;}clientDisposed=true;}
            catch(Exception error){cleanup.Add(error);}
            row["OriginalClientDisposeStatus"]=client==null?"NotCreated":clientDisposal?.Status.ToString()??"ThrewBeforeReturningTask";
            // Even a throwing stream dispose cannot hide or skip settlement of the original SafeHandle.
            try{handle?.Dispose();handleDisposed=true;}catch(Exception error){cleanup.Add(error);}
            row["OriginalSafeHandleDisposeSucceeded"]=handleDisposed;
            try{await deadline.CancelAsync();}catch(Exception error){cleanup.Add(error);}
            try{if(connecting!=null)await connecting;waitJoined=true;}
            catch(OperationCanceledException)when(deadline.IsCancellationRequested){waitJoined=true;}
            catch(Exception error){cleanup.Add(error);}
            row["OriginalWaitStatus"]=connecting?.Status.ToString()??"NotStarted";row["OriginalWaitJoined"]=waitJoined;
            try{if(server!=null){Task dispose=server.DisposeAsync().AsTask();await dispose;row["OriginalServerDisposeStatus"]=dispose.Status.ToString();}serverDisposed=true;}
            catch(Exception error){cleanup.Add(error);}
            row["ServerCreated"]=server!=null;row["ClientCreated"]=handle!=null;
            row["OriginalClientHandleClosed"]=handle==null?(bool?)null:handle.IsClosed;
            row["OriginalServerHandleClosed"]=serverHandle==null?(bool?)null:serverHandle.IsClosed;
            bool settled=clientDisposed&&handleDisposed&&serverDisposed&&waitJoined&&(handle==null||handle.IsClosed)&&(server==null||serverHandle is {IsClosed:true})&&cleanup.Count==0;
            row["CleanupConfirmed"]=settled;row["BodyErrors"]=body.Select(Error).ToArray();row["CleanupErrors"]=cleanup.Select(Error).ToArray();
            errors.AddRange(body.Select(Error));errors.AddRange(cleanup.Select(Error));
            if(!settled&&cleanup.Count==0)CaptureAndRecord("OriginalFixtureCleanupUnconfirmed");
        }
        return row.GetValueOrDefault("CleanupConfirmed") is true;
    }
    private static void Capture(List<Exception> errors,Action action){try{action();}catch(Exception error){errors.Add(error);}}
    private static void CaptureAndRecord(string code){try{Need(false,code);}catch(Exception error){errors.Add(Error(error));}}
    private static uint? Measure(SafePipeHandle handle,Dictionary<string,object?> row)
    {
        try
        {
            bool pipe=GetNamedPipeInfo(handle,out uint flags,out _,out _,out uint maximum);int error=Marshal.GetLastWin32Error();
            row["EndpointQuerySucceeded"]=pipe;row["EndpointQueryWin32OnFailure"]=pipe?(int?)null:error;
            row["ActualEndpoint"]=pipe?(flags&1)==0?"Client":"Server":"Unconfirmed";row["PipeFlags"]=pipe?(uint?)flags:null;row["MaximumInstances"]=pipe?(uint?)maximum:null;
            nint library=NativeLibrary.Load("ntdll.dll",typeof(WarmWindowsPipeAccessProbe).Assembly,DllImportSearchPath.System32);
            try
            {
                var query=Marshal.GetDelegateForFunctionPointer<NtQueryObjectDelegate>(NativeLibrary.GetExport(library,"NtQueryObject"));
                const uint length=56; // PUBLIC_OBJECT_BASIC_INFORMATION: 14 ULONGs; GrantedAccess at offset 4.
                nint buffer=Marshal.AllocHGlobal((int)length);
                try
                {
                    Marshal.Copy(new byte[length],0,buffer,(int)length);
                    int status=query(handle,0,buffer,length,out uint returned);row["NtStatus"]=$"0x{unchecked((uint)status):x8}";row["ReturnLength"]=returned;row["ObjectInformationClass"]="ObjectBasicInformation";
                    bool confirmed=status>=0&&returned==length;row["GrantedAccessQueryConfirmed"]=confirmed;
                    uint? granted=confirmed?unchecked((uint)Marshal.ReadInt32(buffer,4)):null;row["GrantedAccess"]=granted;return granted;
                }
                finally{Marshal.FreeHGlobal(buffer);}
            }
            finally{NativeLibrary.Free(library);}
        }
        catch(Exception error){row["GrantedAccessQueryConfirmed"]=false;row["GrantedAccess"]=null;row["QueryError"]=Error(error);return null;}
    }
    private static void ExactOriginalDacl(SafePipeHandle handle,string logon,uint rights)
    {
        uint query=GetSecurityInfo(handle,6,4,out _,out _,out _,out _,out nint descriptor);if(query!=0)throw new Win32Exception((int)query);
        try
        {
            uint length=GetSecurityDescriptorLength(descriptor);Need(length is >=20 and <=65536,"DescriptorBound");byte[] bytes=new byte[length];Marshal.Copy(descriptor,bytes,0,bytes.Length);var parsed=new RawSecurityDescriptor(bytes,0);
            Need(parsed.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected)&&parsed.DiscretionaryAcl is {Count:1} acl&&acl[0] is CommonAce ace&&ace.AceQualifier==AceQualifier.AccessAllowed&&ace.AceFlags==AceFlags.None&&ace.AccessMask==(int)rights&&ace.SecurityIdentifier.Value==logon,"OriginalDaclStructureChanged");
        }
        finally{LocalFree(descriptor);}
    }
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate int NtQueryObjectDelegate(SafePipeHandle handle,int informationClass,nint information,uint length,out uint returned);
    [DllImport("advapi32.dll")]private static extern uint GetSecurityInfo(SafePipeHandle handle,int type,uint info,out nint owner,out nint group,out nint dacl,out nint sacl,out nint descriptor);
    [DllImport("advapi32.dll")]private static extern uint GetSecurityDescriptorLength(nint descriptor);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateFile(string name,uint access,uint share,nint attributes,uint creation,uint flags,nint template);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeInfo(SafePipeHandle pipe,out uint flags,out uint output,out uint input,out uint maximum);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe,out uint session);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeServerSessionId(SafePipeHandle pipe,out uint session);
}
