using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;

// Read-only inventory. Never creates a process, adjusts a token/privilege, or changes UAC.
// An eligible shape is a prerequisite only: this does not authenticate a production pipe.
public static class KuaiYiWarmImageWindowsFacts
{
    public sealed class TokenFact
    {
        public int Integrity, ElevationType, Session, TokenType;
        public bool Elevated, AppContainer, UiAccess, LogonSidPresent, AuthenticationIdPresent;
        public bool CandidateShapeEligible;
    }
    public sealed class Fact
    {
        public TokenFact Current, Linked;
        public string LinkedStatus;
        public int LinkedWin32Error;
        public bool SameUser, SameLogon, SameAuthentication, SameSession;
        public bool LinkedCandidateForSameSession;
        public bool SeImpersonatePresent, SeImpersonateEnabled;
        // No token/SID/LUID/handle or privilege values are emitted.
    }
    private sealed class Snapshot
    {
        public TokenFact Fact;
        public string User, Logon;
        public long Authentication;
    }
    [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern bool OpenProcessToken(IntPtr process, uint access, out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll", SetLastError=true)]
    private static extern bool GetTokenInformation(SafeAccessTokenHandle token, int kind, IntPtr buffer, int size, out int needed);
    [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern bool LookupPrivilegeValue(string system,string name,out long luid);
    private static T Info<T>(SafeAccessTokenHandle token, int kind, Func<IntPtr,int,T> read)
    {
        GetTokenInformation(token,kind,IntPtr.Zero,0,out int size);
        if(size<4||size>65536)throw new Win32Exception(Marshal.GetLastWin32Error());
        IntPtr buffer=Marshal.AllocHGlobal(size);
        try
        {
            if(!GetTokenInformation(token,kind,buffer,size,out int actual))throw new Win32Exception(Marshal.GetLastWin32Error());
            if(actual<4||actual>size)throw new InvalidOperationException("Token information size refused.");
            return read(buffer,actual);
        }
        finally{Marshal.FreeHGlobal(buffer);}
    }
    private static string Sid(IntPtr buffer,int size,IntPtr pointer)
    {
        long offset=pointer.ToInt64()-buffer.ToInt64();
        if(offset<0||offset>size-8)throw new InvalidOperationException("Token SID bounds refused.");
        int length=8+4*Marshal.ReadByte(pointer,1);
        if(offset>size-length)throw new InvalidOperationException("Token SID length refused.");
        return new SecurityIdentifier(pointer).Value;
    }
    private static IntPtr Pointer(IntPtr buffer,int size)
    {if(size<IntPtr.Size)throw new InvalidOperationException("Token pointer truncated.");return Marshal.ReadIntPtr(buffer);}
    private static Snapshot Read(SafeAccessTokenHandle token)
    {
        var result=new Snapshot{Fact=new TokenFact()};
        result.User=Info(token,1,(p,n)=>Sid(p,n,Pointer(p,n)));
        result.Logon=Info(token,28,(p,n)=>
        {
            int offset=IntPtr.Size==8?8:4;
            if(n<offset+IntPtr.Size+4||Marshal.ReadInt32(p)!=1||((uint)Marshal.ReadInt32(p,offset+IntPtr.Size)&0xc0000000)!=0xc0000000)
                throw new InvalidOperationException("Unique logon SID unavailable.");
            return Sid(p,n,Marshal.ReadIntPtr(p,offset));
        });
        result.Authentication=Info(token,10,(p,n)=>n>=56?Marshal.ReadInt64(p,8):throw new InvalidOperationException("Token statistics truncated."));
        result.Fact.Session=Info(token,12,(p,n)=>Marshal.ReadInt32(p));
        string level=Info(token,25,(p,n)=>Sid(p,n,Pointer(p,n)));
        string[] parts=level.Split('-');
        if(!level.StartsWith("S-1-16-",StringComparison.Ordinal)||!int.TryParse(parts[parts.Length-1],out result.Fact.Integrity))
            throw new InvalidOperationException("Integrity SID unavailable.");
        result.Fact.ElevationType=Info(token,18,(p,n)=>Marshal.ReadInt32(p));
        result.Fact.TokenType=Info(token,8,(p,n)=>Marshal.ReadInt32(p));
        result.Fact.Elevated=Info(token,20,(p,n)=>Marshal.ReadInt32(p)!=0);
        result.Fact.AppContainer=Info(token,29,(p,n)=>Marshal.ReadInt32(p)!=0);
        result.Fact.UiAccess=Info(token,26,(p,n)=>Marshal.ReadInt32(p)!=0);
        result.Fact.LogonSidPresent=result.Logon.StartsWith("S-1-5-5-",StringComparison.Ordinal);
        result.Fact.AuthenticationIdPresent=result.Authentication!=0;
        result.Fact.CandidateShapeEligible=result.User.StartsWith("S-1-",StringComparison.Ordinal)&&result.Fact.LogonSidPresent&&
            result.Fact.AuthenticationIdPresent&&result.Fact.Session>=0&&result.Fact.Integrity==8192&&
            !result.Fact.Elevated&&!result.Fact.AppContainer&&!result.Fact.UiAccess&&result.Fact.TokenType==1;
        return result;
    }
    public static Fact Capture()
    {
        SafeAccessTokenHandle token;
        if(!OpenProcessToken(GetCurrentProcess(),8,out token))throw new Win32Exception(Marshal.GetLastWin32Error());
        using(token)
        {
            Snapshot current=Read(token);
            var result=new Fact{Current=current.Fact,LinkedStatus="NotQueried"};
            long impersonate;
            if(!LookupPrivilegeValue(null,"SeImpersonatePrivilege",out impersonate))throw new Win32Exception(Marshal.GetLastWin32Error());
            Info(token,3,(p,n)=>
            {
                int count=Marshal.ReadInt32(p);
                if(count<0||count>(n-4)/12)throw new InvalidOperationException("Token privilege array truncated.");
                for(int i=0;i<count;i++)if(Marshal.ReadInt64(p,4+i*12)==impersonate)
                {result.SeImpersonatePresent=true;result.SeImpersonateEnabled=(Marshal.ReadInt32(p,12+i*12)&2)!=0;}
                return true;
            });
            IntPtr linked;
            try{linked=Info(token,19,(p,n)=>n>=IntPtr.Size?Marshal.ReadIntPtr(p):throw new InvalidOperationException("Linked token truncated."));}
            catch(Win32Exception error){result.LinkedStatus="Unavailable";result.LinkedWin32Error=error.NativeErrorCode;return result;}
            using(var held=new SafeAccessTokenHandle(linked))
            {
                if(held.IsInvalid)throw new InvalidOperationException("Invalid linked token handle.");
                Snapshot candidate=Read(held);result.Linked=candidate.Fact;result.LinkedStatus="Observed_ReadOnly";
                result.SameUser=current.User==candidate.User;result.SameLogon=current.Logon==candidate.Logon;
                result.SameAuthentication=current.Authentication==candidate.Authentication;
                result.SameSession=current.Fact.Session==candidate.Fact.Session;
                result.LinkedCandidateForSameSession=candidate.Fact.CandidateShapeEligible&&result.SameUser&&result.SameLogon&&result.SameAuthentication&&result.SameSession;
            }
            return result;
        }
    }
}
