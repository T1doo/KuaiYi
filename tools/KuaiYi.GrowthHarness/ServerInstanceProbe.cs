using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using KuaiYi.App;
using Microsoft.Win32.SafeHandles;
using static KuaiYi.GrowthHarness.WarmWindowsPipeAccessProbe;
namespace KuaiYi.GrowthHarness;

// Own short-lived fixtures only: unchanged logon DACL, maxInstances=2, ordinary inherited-token children.
internal static class ServerInstanceProbe
{
    private const uint Rights=0x12019b,Mode=8,FirstOpen=0x40080003,SecondOpen=0x40000003;
    private sealed record Identity(string UserDigest,string LogonDigest,string OwnerDigest,string AuthenticationDigest,int Session,int ProductionIntegrity,bool Elevated,bool AppContainer,bool UiAccess);
    private sealed record Ready(string Kind,string Nonce,int Pid,Identity Token,bool ClientHeld);
    private static readonly List<object> errors=[],cases=[];
    private static readonly List<Process> retainedChildren=[];
    private static readonly TaskCompletionSource failedOwnerFence=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static string evidenceOutput="";
    private static Dictionary<string,object?>? liveState;
    private static bool Context()=>OperatingSystem.IsWindows()&&IntPtr.Size==8&&Environment.GetEnvironmentVariable("GITHUB_ACTIONS")=="true"&&Environment.GetEnvironmentVariable("GITHUB_REPOSITORY")=="T1doo/KuaiYi"&&Environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT")=="1";
    private static string Digest(string salt,string value)=>Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(salt+"|"+value)));
    private static Identity ReadIdentity(SafeProcessHandle process,string salt,out string logon,out string owner)
    {
        object token=Call(typeof(WindowsInstallationImagePeer),"ReadToken",process)!;
        T Field<T>(string name)=>(T)token.GetType().GetProperty(name)!.GetValue(token)!;
        logon=Field<string>("LogonSid");owner=TokenOwner(process);
        return new(Digest(salt,Field<string>("UserSid")),Digest(salt,logon),Digest(salt,owner),Digest(salt,Field<long>("AuthenticationId").ToString(System.Globalization.CultureInfo.InvariantCulture)),Field<int>("Session"),Field<int>("Integrity"),Field<bool>("Elevated"),Field<bool>("AppContainer"),Field<bool>("UiAccess"));
    }
    internal static int Run(string result)
    {
        if(!Context())return 2;errors.Clear();cases.Clear();bool claimed=false;Dictionary<string,object?> state=[];
        string output=Path.GetFullPath(result);
        try
        {
            Need(!output.StartsWith("\\\\",StringComparison.Ordinal),"LocalResultRequired");
            for(string? p=Path.GetDirectoryName(output);p!=null;p=Path.GetDirectoryName(p))Need((File.GetAttributes(p)&FileAttributes.ReparsePoint)==0,"LinkedResultRefused");
            using(var file=new FileStream(output,FileMode.CreateNew,FileAccess.Write,FileShare.Read))file.Flush(true);claimed=true;
            evidenceOutput=output;liveState=state;RunCore(state).GetAwaiter().GetResult();
        }
        catch(Exception error){errors.Add(Error(error));}
        if(!claimed)return 3;
        try{File.WriteAllText(output,JsonSerializer.Serialize(new{Kind="OperationalExtraServerInstanceReview",Status=errors.Count==0?"Pass_LimitedTwoDeniedAttempts":"Fail",Errors=errors,Cases=cases,State=state,
            HistoricalClientBit4Red="Retained_NotBackfilledOrDeclaredHarmless",CrossIdentity="NotRun",PositiveWarm="Blocked_NoMediumEntry",Images="NotRun",AppUi="NotRun",Worker="NotRun",ModelCalls=0,Package="Hold",ProductionReuse="Off"},new JsonSerializerOptions{WriteIndented=true}));}
        catch(Exception error){errors.Add(Error(error));Console.Error.WriteLine("KUAIYI_SERVER_EVIDENCE_FAILURE "+JsonSerializer.Serialize(new{Status="Fail",Errors=errors}));}
        return errors.Count==0?0:3;
    }
    private static async Task RunCore(Dictionary<string,object?> state)
    {
        using var self=Process.GetCurrentProcess();string nonce=Guid.NewGuid().ToString("N"),name="KuaiYi.ServerInstanceProbe."+nonce;
        Identity own=ReadIdentity(self.SafeHandle,nonce,out string logon,out string owner);state["ParentToken"]=own;state["IdentityPurpose"]="ReadOnlyCorrelation_NotProductionAuthorization";
        Need(own.Elevated,"ActualElevatedRunnerRequired");
        SafePipeHandle? firstHandle=null;NamedPipeServerStream? first=null;Task? connection=null;
        CancellationTokenSource? deadline=null;bool childSettled=true;List<Exception> body=[],cleanup=[];
        try
        {
            firstHandle=Create(name,logon,true,out int code);state["FirstCreateWin32OnFailure"]=firstHandle.IsInvalid?(int?)code:null;Need(!firstHandle.IsInvalid,"FirstServerCreateFailed");
            first=new NamedPipeServerStream(PipeDirection.InOut,true,false,firstHandle);
            state["FirstOpenMode"]=FirstOpen;state["SecondOpenMode"]=SecondOpen;state["PipeMode"]=Mode;state["MaximumInstances"]=2;state["DaclMask"]=Rights;
            InspectFirst(firstHandle,owner,logon);state["PipeOwnerMatchesOriginalTokenOwner"]=true;state["ExactProtectedSingleLogonDacl"]=true;Need(!first.IsConnected,"UnexpectedInitialClient");
            childSettled=await Attempt("without-client",name,nonce,own,first,firstHandle,false);
            Need(childSettled,"OriginalFirstChildSettlementUnconfirmed");
            Need(!first.IsConnected,"NoClientConditionChanged");
            deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));connection=first.WaitForConnectionAsync(deadline.Token);
            state["HeldClientRequestedAccess"]=Rights|4;state["HeldClientOwner"]="SecondOriginalChild_AlsoCallsCreateNamedPipe";
            state["HeldClientGrantedAccess"]="NotRemeasured_Prior9d90a1fEvidenceRetained";
            childSettled=await Attempt("with-held-bit4-client",name,nonce,own,first,firstHandle,true,connection);
            Need(childSettled,"OriginalSecondChildSettlementUnconfirmed");
            InspectFirst(firstHandle,owner,logon);state["FirstOriginalHandleHeldAcrossBoth"]=true;
        }
        catch(Exception error){body.Add(error);}
        finally
        {
            // Child attempts retain their original Process/IO tasks; never start another phase if they failed settlement.
            bool serverDisposed=false,serverHandleDisposed=false,waitJoined=false;
            try{if(deadline!=null)await deadline.CancelAsync();}catch(Exception e){cleanup.Add(e);}
            try{if(connection!=null)await connection;waitJoined=true;}catch(OperationCanceledException)when(deadline?.IsCancellationRequested==true){waitJoined=true;}catch(Exception e){cleanup.Add(e);}
            try{deadline?.Dispose();}catch(Exception e){cleanup.Add(e);}
            state["OriginalConnectionStatus"]=connection?.Status.ToString()??"NotStarted";state["OriginalConnectionJoined"]=waitJoined;
            try{if(first!=null){Task task=first.DisposeAsync().AsTask();await task;state["OriginalServerDispose"]=task.Status.ToString();}serverDisposed=true;}catch(Exception e){cleanup.Add(e);}
            try{firstHandle?.Dispose();serverHandleDisposed=true;}catch(Exception e){cleanup.Add(e);}
            state["OriginalFirstServerClosed"]=firstHandle?.IsClosed;
            bool settled=childSettled&&serverDisposed&&serverHandleDisposed&&waitJoined&&cleanup.Count==0;
            state["CleanupConfirmed"]=settled;state["BodyErrors"]=body.Select(Error).ToArray();state["CleanupErrors"]=cleanup.Select(Error).ToArray();errors.AddRange(body.Select(Error));errors.AddRange(cleanup.Select(Error));
            if(!settled&&body.Count==0&&cleanup.Count==0)Add(errors,"OriginalFixtureSettlementUnconfirmed");
        }
    }
    private static async Task<bool> Attempt(string phase,string name,string nonce,Identity own,NamedPipeServerStream first,SafePipeHandle original,bool connected,Task? originalConnection=null)
    {
        Dictionary<string,object?> row=new(){["Phase"]=phase,["FirstOriginalHandleHeldBefore"]=!original.IsClosed,["FirstConnectedBefore"]=first.IsConnected};cases.Add(row);
        Need(ReferenceEquals(first.SafePipeHandle,original)&&!original.IsClosed&&!first.IsConnected,"OriginalFirstHandleOrClientStateChanged");
        var child=new Process{StartInfo=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true}};
        child.StartInfo.ArgumentList.Add("--ci-server-instance-child");child.StartInfo.ArgumentList.Add(name);child.StartInfo.ArgumentList.Add(nonce);child.StartInfo.ArgumentList.Add(connected?"held-client":"no-client");
        List<Exception> body=[],cleanup=[];Task? exit=null,write=null,flush=null;Task<string>? ready=null,tail=null,stderr=null;bool started=false,exitJoined=false;
        try
        {
            Need(child.Start(),"OrdinaryChildStartFailed");started=true;exit=child.WaitForExitAsync();_=child.SafeHandle;row["ChildPid"]=child.Id;row["Launch"]="OrdinaryProcessStart_CurrentToken_NoCredentialOrTokenChange";
            stderr=ReadBounded(child.StandardError,false);ready=ReadBounded(child.StandardOutput,true);
            var hello=JsonSerializer.Deserialize<Ready>(await ready.WaitAsync(TimeSpan.FromSeconds(5)))??throw new InvalidDataException("ReadyMissing");
            Need(hello.Kind=="Ready"&&hello.Nonce==nonce&&hello.Pid==child.Id&&hello.ClientHeld==connected,"OriginalChildReadyMismatch");
            Identity observed=ReadIdentity(child.SafeHandle,nonce,out _,out _);row["OriginalChildTokenObserved"]=observed;row["ChildSelfToken"]=hello.Token;
            Need(observed==own&&hello.Token==own,"OriginalChildUserLogonOwnerSessionTokenMismatch");row["OriginalTokenIdentityMatched"]=true;
            if(connected)
            {
                Need(originalConnection!=null,"OriginalConnectionMissing");await originalConnection!.WaitAsync(TimeSpan.FromSeconds(5));
                Need(GetNamedPipeClientProcessId(original,out uint clientPid)&&clientPid==child.Id,"OriginalChildHeldClientPidMismatch");row["ActualHeldClientPid"]=clientPid;
            }
            Need(!original.IsClosed&&ReferenceEquals(first.SafePipeHandle,original)&&first.IsConnected==connected,"FirstNotHeldAtGo");row["FirstConnectedAtGo"]=first.IsConnected;
            tail=ReadBounded(child.StandardOutput,false);
            write=child.StandardInput.BaseStream.WriteAsync(new byte[]{(byte)'G'}).AsTask();await write.WaitAsync(TimeSpan.FromSeconds(5));
            flush=child.StandardInput.BaseStream.FlushAsync();await flush.WaitAsync(TimeSpan.FromSeconds(5));
            await exit.WaitAsync(TimeSpan.FromSeconds(10));exitJoined=true;row["OriginalChildExitCode"]=child.ExitCode;
            string rest=await tail.WaitAsync(TimeSpan.FromSeconds(5));string err=await stderr.WaitAsync(TimeSpan.FromSeconds(5));Need(err.Length==0,"ChildStderrNonempty");
            using var doc=JsonDocument.Parse(rest);JsonElement reply=doc.RootElement;row["ChildReply"]=reply.Clone();
            Need(reply.GetProperty("Kind").GetString()=="ServerInstanceReply"&&reply.GetProperty("Pid").GetInt32()==child.Id,"OriginalChildReplyMismatch");
            Need(JsonSerializer.Deserialize<Identity>(reply.GetProperty("Token"))==own,"ChildTokenChangedBeforeAttempt");
            bool invalid=reply.GetProperty("HandleInvalid").GetBoolean();
            if(!invalid)Add(errors,"AdditionalServerHandleObtained_SecurityRed");
            else if(reply.GetProperty("Win32").GetInt32()!=5)Add(errors,"ServerAttemptOtherError_Uninformative");
            Need(child.ExitCode==0&&invalid&&reply.GetProperty("Win32").GetInt32()==5&&reply.GetProperty("CleanupConfirmed").GetBoolean()&&reply.GetProperty("Errors").GetArrayLength()==0,"ChildDidNotConfirmWin32FiveAndCleanup");row["DeniedWin32Five"]=true;
        }
        catch(Exception error){body.Add(error);}
        finally
        {
            // Close the raw stdin pipe without waiting on a text-writer lock; observe original write/flush tasks separately.
            if(started)
            {
                try{child.StandardInput.BaseStream.Dispose();row["OriginalStdinClosed"]=true;}catch(Exception e){cleanup.Add(e);}
                if(exit==null)try{exit=child.WaitForExitAsync();}catch(Exception e){cleanup.Add(e);}
                if(!exitJoined)try{if(!child.HasExited){child.Kill();row["OwnedChildKillRequested"]=true;}}catch(Exception e){cleanup.Add(e);}
                if(exit!=null)try{await exit.WaitAsync(TimeSpan.FromSeconds(5));exitJoined=true;}catch(Exception e){cleanup.Add(e);}
                // Final fences remain on the ORIGINAL tasks after bounded observation/owned kill.
                // They retain the first server/client until settlement; the external 90s gate cannot report success on a hang.
                if(exit is {IsCompleted:false})try{await exit;exitJoined=true;}catch(Exception e){cleanup.Add(e);}
                foreach(Task? task in new Task?[]{write,flush,ready})if(task!=null)
                {
                    try{await task.WaitAsync(TimeSpan.FromSeconds(5));}catch(Exception e){cleanup.Add(e);}
                    if(!task.IsCompleted)try{await task;}catch(Exception e){cleanup.Add(e);}
                }
                if(tail==null&&ready is {IsCompleted:true})tail=ReadBounded(child.StandardOutput,false);
                foreach(Task? task in new Task?[]{tail,stderr})if(task!=null)
                {
                    try{await task.WaitAsync(TimeSpan.FromSeconds(5));}catch(Exception e){cleanup.Add(e);}
                    if(!task.IsCompleted)try{await task;}catch(Exception e){cleanup.Add(e);}
                }
                if(new Task?[]{ready,tail,stderr}.All(t=>t==null||t.IsCompleted))
                {
                    try{child.StandardOutput.Dispose();row["OriginalStdoutDisposed"]=true;}catch(Exception e){cleanup.Add(e);}
                    try{child.StandardError.Dispose();row["OriginalStderrDisposed"]=true;}catch(Exception e){cleanup.Add(e);}
                }
            }
            bool io=(ready?.IsCompletedSuccessfully??!started)&&(tail?.IsCompletedSuccessfully??!started)&&(stderr?.IsCompletedSuccessfully??!started)&&(write?.IsCompletedSuccessfully??true)&&(flush?.IsCompletedSuccessfully??true);
            bool allTerminal=new Task?[]{exit,write,flush,ready,tail,stderr}.All(t=>t==null||t.IsCompleted);
            if(!started||exitJoined&&allTerminal)try{child.Dispose();row["OriginalProcessDisposed"]=true;}catch(Exception e){cleanup.Add(e);}
            else retainedChildren.Add(child); // Retain the ORIGINAL owner when exit/IO could not be confirmed.
            bool settled=(!started||exitJoined)&&io&&cleanup.Count==0;
            row["OriginalExitJoined"]=exitJoined;row["OriginalExitStatus"]=exit?.Status.ToString()??"NotStarted";row["OriginalReadyStatus"]=ready?.Status.ToString()??"NotStarted";row["OriginalStdoutStatus"]=tail?.Status.ToString()??"NotStarted";row["OriginalStderrStatus"]=stderr?.Status.ToString()??"NotStarted";row["OriginalWriteStatus"]=write?.Status.ToString()??"NotStarted";row["OriginalFlushStatus"]=flush?.Status.ToString()??"NotStarted";
            row["FirstOriginalHandleHeldAfter"]=!original.IsClosed&&ReferenceEquals(first.SafePipeHandle,original);row["CleanupConfirmed"]=settled;row["BodyErrors"]=body.Select(Error).ToArray();row["CleanupErrors"]=cleanup.Select(Error).ToArray();errors.AddRange(body.Select(Error));errors.AddRange(cleanup.Select(Error));
            if(started&&!exitJoined)
            {
                // A faulted ORIGINAL exit observation is not replaced with a second wait or HasExited proof.
                // Keep this Process plus the caller's original first server/client alive until the outer failure gate.
                row["RetainedOriginalOwners"]=true;
                try{File.WriteAllText(evidenceOutput,JsonSerializer.Serialize(new{Kind="OperationalExtraServerInstanceReview",Status="Fail_CleanupUnconfirmed",Errors=errors,Cases=cases,State=liveState,Reason="OriginalExitObservationUnconfirmed_OwnersRetained"},new JsonSerializerOptions{WriteIndented=true}));}
                catch(Exception e){Console.Error.WriteLine(JsonSerializer.Serialize(Error(e)));}
                await failedOwnerFence.Task;
            }
        }
        return row.GetValueOrDefault("CleanupConfirmed") is true;
    }
    internal static int Child(string name,string nonce,string condition)
    {
        if(!Context()||nonce.Length!=32||!Guid.TryParseExact(nonce,"N",out _)||name!="KuaiYi.ServerInstanceProbe."+nonce||condition is not ("held-client" or "no-client"))return 2;
        using var self=Process.GetCurrentProcess();List<object> faults=[];Dictionary<string,object?> measurement=[];SafePipeHandle? handle=null,clientHandle=null;Identity? own=null;bool? invalid=null;int? code=null;bool closed=false,clientClosed=false;Task<int>? gate=null;Stream? input=null;
        try
        {
            own=ReadIdentity(self.SafeHandle,nonce,out string logon,out _);
            if(condition=="held-client")
            {
                clientHandle=CreateFile("\\\\.\\pipe\\"+name,Rights|4,0,0,3,0x40110000,0);int clientNative=Marshal.GetLastWin32Error();measurement["HeldClientRequestedAccess"]=Rights|4;measurement["HeldClientInvalid"]=clientHandle.IsInvalid;measurement["HeldClientWin32OnFailure"]=clientHandle.IsInvalid?(int?)clientNative:null;
                Need(!clientHandle.IsInvalid,"OriginalChildBit4ClientOpenFailed");
                Need(GetNamedPipeInfo(clientHandle,out uint flags,out _,out _,out uint max)&&(flags&1)==0&&max==2,"OriginalChildClientEndpointUnconfirmed");measurement["HeldClientEndpoint"]="Client";measurement["HeldClientFlags"]=flags;measurement["HeldClientGrantedAccess"]="NotRemeasured";
            }
            Console.WriteLine(JsonSerializer.Serialize(new Ready("Ready",nonce,self.Id,own,clientHandle!=null)));Console.Out.Flush();
            input=Console.OpenStandardInput();byte[] go=new byte[1];gate=input.ReadAsync(go).AsTask();Need(gate.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult()==1&&go[0]=='G',"OriginalGoMissing");
            Need(ReadIdentity(self.SafeHandle,nonce,out _,out _)==own,"OriginalTokenChanged");
            Need(condition=="no-client"||clientHandle is {IsClosed:false,IsInvalid:false},"OriginalClientNotHeldAtServerAttempt");
            handle=Create(name,logon,false,out int native);invalid=handle.IsInvalid;code=invalid.Value?native:null;
            measurement["OriginalClientHeldThroughNativeAttempt"]=condition=="no-client"?(bool?)null:clientHandle is {IsClosed:false,IsInvalid:false};
            if(!invalid.Value){Measure(handle,measurement);Add(faults,"AdditionalServerHandleObtained_SecurityRed");}
            else if(native!=5)Add(faults,"OtherNativeError_Uninformative");
        }
        catch(Exception e){faults.Add(Error(e));}
        finally
        {
            try{handle?.Dispose();closed=handle==null||handle.IsClosed;}catch(Exception e){faults.Add(Error(e));}
            try{clientHandle?.Dispose();clientClosed=clientHandle==null||clientHandle.IsClosed;}catch(Exception e){faults.Add(Error(e));}
            try{input?.Dispose();}catch(Exception e){faults.Add(Error(e));}
            if(gate!=null)
            {
                try{gate.WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();}catch(Exception e){faults.Add(Error(e));}
                if(!gate.IsCompleted)try{gate.GetAwaiter().GetResult();}catch(Exception e){faults.Add(Error(e));}
            }
        }
        bool settled=closed&&clientClosed&&(gate?.IsCompletedSuccessfully??false);Console.WriteLine(JsonSerializer.Serialize(new{Kind="ServerInstanceReply",Pid=self.Id,Token=own,HandleInvalid=invalid,Win32=code,OpenMode=SecondOpen,PipeMode=Mode,MaximumInstances=2,EndpointRequested="Server_CreateNamedPipe",Measurement=measurement,OriginalHeldClientClosed=clientHandle?.IsClosed,OriginalServerHandleClosed=handle?.IsClosed,OriginalGoReadStatus=gate?.Status.ToString(),CleanupConfirmed=settled,Errors=faults}));Console.Out.Flush();
        return faults.Count==0&&invalid==true&&code==5&&settled?0:3;
    }
    private static async Task<string> ReadBounded(StreamReader reader,bool line)
    {
        char[] buffer=new char[line?1:4096];var text=new StringBuilder();int count;
        while((count=await reader.ReadAsync(buffer))>0){if(line&&buffer[0]=='\n')break;text.Append(buffer,0,count);Need(text.Length<=65536,"ChildOutputBoundExceeded");}
        return text.ToString();
    }
    private static SafePipeHandle Create(string name,string logon,bool first,out int error)
    {
        if(!ConvertStringSecurityDescriptorToSecurityDescriptor($"D:P(A;;0x{Rights:x};;;{logon})",1,out nint descriptor,out _))throw new Win32Exception(Marshal.GetLastWin32Error());
        try{var attributes=new SecurityAttributes{Length=Marshal.SizeOf<SecurityAttributes>(),Descriptor=descriptor};var result=CreateNamedPipe("\\\\.\\pipe\\"+name,first?FirstOpen:SecondOpen,Mode,2,65536,65536,0,ref attributes);error=Marshal.GetLastWin32Error();return result;}
        finally{LocalFree(descriptor);}
    }
    private static void InspectFirst(SafePipeHandle handle,string owner,string logon)
    {
        Need(!handle.IsClosed&&GetNamedPipeInfo(handle,out uint flags,out _,out _,out uint max)&&(flags&1)==1&&max==2,"OriginalFirstServerEndpointUnconfirmed");
        uint code=GetSecurityInfo(handle,6,5,out _,out _,out _,out _,out nint descriptor);if(code!=0)throw new Win32Exception((int)code);
        try{uint n=GetSecurityDescriptorLength(descriptor);Need(n is >=20 and <=65536,"DescriptorBound");byte[] bytes=new byte[n];Marshal.Copy(descriptor,bytes,0,bytes.Length);var parsed=new RawSecurityDescriptor(bytes,0);Need(parsed.Owner?.Value==owner&&parsed.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected)&&parsed.DiscretionaryAcl is {Count:1} acl&&acl[0] is CommonAce ace&&ace.AceFlags==AceFlags.None&&ace.AceQualifier==AceQualifier.AccessAllowed&&ace.AccessMask==(int)Rights&&ace.SecurityIdentifier.Value==logon,"OriginalPipeOwnerOrLogonDaclMismatch");}
        finally{LocalFree(descriptor);}
    }
    private static string TokenOwner(SafeProcessHandle process)
    {
        if(!OpenProcessToken(process,8,out var token))throw new Win32Exception(Marshal.GetLastWin32Error());using(token)
        {
            GetTokenInformation(token,4,0,0,out uint required);Need(required is >=12 and <=65536,"OwnerTokenBound");nint buffer=Marshal.AllocHGlobal((int)required);
            try{if(!GetTokenInformation(token,4,buffer,required,out uint actual))throw new Win32Exception(Marshal.GetLastWin32Error());Need(actual<=required&&actual>=12,"OwnerTokenLength");nint sid=Marshal.ReadIntPtr(buffer);Need(sid>=buffer&&sid<=buffer+actual-8,"OwnerSidPointer");int size=8+Marshal.ReadByte(sid,1)*4;Need(size<=actual&&sid<=buffer+actual-size,"OwnerSidBound");return new SecurityIdentifier(sid).Value;}
            finally{Marshal.FreeHGlobal(buffer);}
        }
    }
    private static void Add(List<object> list,string code){try{Need(false,code);}catch(Exception e){list.Add(Error(e));}}
    [StructLayout(LayoutKind.Sequential)]private struct SecurityAttributes{public int Length;public nint Descriptor;[MarshalAs(UnmanagedType.Bool)]public bool Inherit;}
    [DllImport("advapi32.dll",EntryPoint="ConvertStringSecurityDescriptorToSecurityDescriptorW",CharSet=CharSet.Unicode,SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool ConvertStringSecurityDescriptorToSecurityDescriptor(string sddl,uint revision,out nint descriptor,out uint size);
    [DllImport("kernel32.dll",EntryPoint="CreateNamedPipeW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateNamedPipe(string name,uint openMode,uint mode,uint instances,uint output,uint input,uint timeout,ref SecurityAttributes attributes);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateFile(string name,uint access,uint share,nint attributes,uint creation,uint flags,nint template);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeInfo(SafePipeHandle pipe,out uint flags,out uint output,out uint input,out uint maximum);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("advapi32.dll")]private static extern uint GetSecurityInfo(SafePipeHandle handle,int type,uint info,out nint owner,out nint group,out nint dacl,out nint sacl,out nint descriptor);
    [DllImport("advapi32.dll")]private static extern uint GetSecurityDescriptorLength(nint descriptor);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool OpenProcessToken(SafeProcessHandle process,uint access,out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetTokenInformation(SafeAccessTokenHandle token,int kind,nint buffer,uint length,out uint returned);
}
