using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using KuaiYi.App;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.GrowthHarness;

// Component calls on real held handles; never opens a production endpoint or manufactures an identity.
internal static class WarmWindowsPipeProbe
{
    private const BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    private static object? Call(Type type,string name,params object?[] args)
    {
        try{return type.GetMethod(name,Static)!.Invoke(null,args);}
        catch(TargetInvocationException error)when(error.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
    }
    internal static readonly Dictionary<string,object?> Observation=[];
    private static SafePipeHandle? originalServer;
    private static NamedPipeServerStream? serverOwner;
    internal static bool CleanupConfirmed {get;private set;}
    internal static async Task<object> Run()
    {
        Observation.Clear();originalServer=null;serverOwner=null;CleanupConfirmed=false;
        object? result=null;List<Exception> errors=[];
        try{result=await RunCore();}catch(Exception error){errors.Add(error);}
        bool disposeSucceeded=serverOwner==null;
        Observation["ServerCreated"]=serverOwner!=null;
        if(serverOwner!=null)
        {
            Task? disposal=null;
            try{disposal=serverOwner.DisposeAsync().AsTask();await disposal;disposeSucceeded=disposal.IsCompletedSuccessfully;}
            catch(Exception error){errors.Add(error);}
            Observation["OriginalServerDisposeStatus"]=disposal?.Status.ToString()??"ThrewBeforeReturningTask";
        }
        // The ORIGINAL dispose must succeed, the ORIGINAL wait must be joined, and the ORIGINAL handle must close.
        bool closed=serverOwner==null||originalServer is {IsClosed:true};
        Observation["OriginalServerHandleClosed"]=serverOwner==null?(bool?)null:closed;
        CleanupConfirmed=closed&&disposeSucceeded&&(!Observation.ContainsKey("OriginalWaitStarted")||Observation.GetValueOrDefault("OriginalWaitJoined") is true);
        Observation["CleanupConfirmed"]=CleanupConfirmed;
        if(errors.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors.Count>1)throw new AggregateException("Original pipe body and disposal failures.",errors);
        return result!;
    }
    private static async Task<object> RunCore()
    {
        using var self=Process.GetCurrentProcess();string image=Environment.ProcessPath??throw new InvalidOperationException("SelfImageMissing");
        object own=Call(typeof(WindowsInstallationImagePeer),"ReadToken",self.SafeHandle)!;
        bool elevated=(bool)own.GetType().GetProperty("Elevated")!.GetValue(own)!;
        if(!elevated)throw new InvalidOperationException("ExpectedActualElevatedRunner");
        bool highRejected=false;try{Call(typeof(WindowsInstallationImagePeer),"Current",image);}
        catch(UnauthorizedAccessException){highRejected=true;}
        Observation["ActualElevated"]=elevated;Observation["ProductionHighRejected"]=highRejected;
        if(!highRejected)throw new InvalidOperationException("ProductionHighGateAccepted");
        string logon=(string)own.GetType().GetProperty("LogonSid")!.GetValue(own)!;
        string name="KuaiYi.WarmComponentProbe."+Guid.NewGuid().ToString("N");
        uint rights=(uint)typeof(WindowsInstallationImagePipe).GetField("ClientAccess",Static)!.GetRawConstantValue()!;
        if(rights!=0x12019b||(rights&4)!=0)throw new InvalidOperationException("ProductionClientMaskChanged");
        var server=(NamedPipeServerStream)Call(typeof(WindowsInstallationImagePipe),"CreateServer",name,logon)!;serverOwner=server;
        originalServer=server.SafePipeHandle;
        uint query=GetSecurityInfo(server.SafePipeHandle,6,4,out _,out _,out _,out _,out nint descriptor);
        if(query!=0)throw new Win32Exception((int)query);
        bool exactDacl;
        Exception? body=null;
        try
        {
            uint length=GetSecurityDescriptorLength(descriptor);if(length is <20 or >65536)throw new InvalidDataException("DescriptorBound");
            byte[] bytes=new byte[length];Marshal.Copy(descriptor,bytes,0,bytes.Length);var parsed=new RawSecurityDescriptor(bytes,0);
            exactDacl=parsed.ControlFlags.HasFlag(ControlFlags.DiscretionaryAclProtected)&&parsed.DiscretionaryAcl is {Count:1} acl&&
                acl[0] is CommonAce ace&&ace.AceQualifier==AceQualifier.AccessAllowed&&ace.AceFlags==AceFlags.None&&
                ace.AccessMask==(int)rights&&ace.SecurityIdentifier.Value==logon;
        }
        finally{LocalFree(descriptor);}
        Observation["ExactProtectedSingleLogonDacl"]=exactDacl;Observation["ClientMask"]=rights;
        if(!exactDacl)throw new InvalidDataException("ProductionDaclMismatch");
        int secondBind=0;
        try{using var unexpected=(NamedPipeServerStream)Call(typeof(WindowsInstallationImagePipe),"CreateServer",name,logon)!;throw new InvalidOperationException("SecondFirstInstanceAccepted");}
        catch(Win32Exception error){secondBind=error.NativeErrorCode;}
        Observation["SecondBindWin32"]=secondBind;
        if(secondBind!=5)throw new InvalidOperationException("SecondFirstInstanceExpectedAccessDenied");
        using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Task connecting=server.WaitForConnectionAsync(deadline.Token);Observation["OriginalWaitStarted"]=true;
        try
        {
            using var denied=CreateFile("\\\\.\\pipe\\"+name,rights|4,0,0,3,0x40110000,0);int deniedCode=Marshal.GetLastWin32Error();
            Observation["ClientTrialRequestedMask"]=rights|4;Observation["ClientTrialHandleInvalid"]=denied.IsInvalid;
            Observation["ClientTrialWin32OnFailure"]=denied.IsInvalid?(int?)deniedCode:null;
            Observation["ClientTrialAssertion"]="RequiresInvalidHandleAndWin32_5";
            if(!denied.IsInvalid||deniedCode!=5){var error=new InvalidOperationException("CreateInstanceRightNotDenied");error.Data["ProbeCode"]="CreateInstanceRightNotDenied";throw error;}
            using var handle=CreateFile("\\\\.\\pipe\\"+name,rights,0,0,3,0x40110000,0);int allowedCode=Marshal.GetLastWin32Error();
            if(handle.IsInvalid){handle.Dispose();throw new Win32Exception(allowedCode);}
            await using var client=new NamedPipeClientStream(PipeDirection.InOut,true,true,handle);
            await connecting;
            if(!GetNamedPipeClientProcessId(server.SafePipeHandle,out uint clientPid)||!GetNamedPipeServerProcessId(client.SafePipeHandle,out uint serverPid)||
                clientPid!=self.Id||serverPid!=self.Id||!GetNamedPipeClientSessionId(server.SafePipeHandle,out uint clientSession)||
                !GetNamedPipeServerSessionId(client.SafePipeHandle,out uint serverSession)||clientSession!=self.SessionId||serverSession!=self.SessionId)
                throw new InvalidOperationException("ActualSameProcessPeerBindingFailed");
            bool Rejected(PipeStream pipe,bool isServer,string expected)
            {
                try
                {
                    using var unexpected=(IDisposable)Activator.CreateInstance(typeof(WindowsInstallationImagePeer),BindingFlags.Instance|BindingFlags.NonPublic,null,[pipe,isServer,expected,own],null)!;
                    throw new InvalidOperationException("ActualHighPeerAccepted");
                }
                catch(TargetInvocationException error)when(error.InnerException is UnauthorizedAccessException){return true;}
            }
            bool serverReject=Rejected(server,true,image),clientReject=Rejected(client,false,image);
            bool wrongImageReject=Rejected(server,true,Path.Combine(AppContext.BaseDirectory,"KuaiYi.App.exe"));
            return new{ActualElevated=elevated,ProductionHighRejected=highRejected,SameProcessFixture=true,ActualPeerPidSessionMatched=true,
                ExactProtectedSingleLogonDacl=exactDacl,ClientMask=rights,SecondBindWin32=secondBind,CreateInstanceWin32=deniedCode,
                ServerHighRejected=serverReject,ClientHighRejected=clientReject,WrongImagePlusHighRejected=wrongImageReject,PositiveWarmAuthentication="NotRun"};
        }
        catch(Exception error){body=error;throw;}
        finally
        {
            // Join the original wait even when a preceding native check failed.
            List<Exception> errors=[];try{await deadline.CancelAsync();}catch(Exception error){errors.Add(error);}
            try{await connecting;}catch(OperationCanceledException)when(deadline.IsCancellationRequested){}catch(Exception error){errors.Add(error);}
            Observation["OriginalWaitStatus"]=connecting.Status.ToString();Observation["OriginalWaitJoined"]=connecting.IsCompleted&&errors.Count==0;
            if(errors.Count>0){if(body!=null)errors.Insert(0,body);throw new AggregateException("Original pipe wait settlement failed.",errors);}
        }
    }
    [DllImport("advapi32.dll",SetLastError=true)]private static extern uint GetSecurityInfo(SafePipeHandle handle,int type,uint info,out nint owner,out nint group,out nint dacl,out nint sacl,out nint descriptor);
    [DllImport("advapi32.dll")]private static extern uint GetSecurityDescriptorLength(nint descriptor);
    [DllImport("kernel32.dll")]private static extern nint LocalFree(nint memory);
    [DllImport("kernel32.dll",EntryPoint="CreateFileW",CharSet=CharSet.Unicode,SetLastError=true)]private static extern SafePipeHandle CreateFile(string name,uint access,uint share,nint attributes,uint creation,uint flags,nint template);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out uint pid);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe,out uint session);
    [DllImport("kernel32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetNamedPipeServerSessionId(SafePipeHandle pipe,out uint session);
}
