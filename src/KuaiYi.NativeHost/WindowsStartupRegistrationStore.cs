using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using KuaiYi.Core.Installation;
namespace KuaiYi.NativeHost;

// Deliberately specific to one current-user Run value. No enumeration, key deletion or ACL operation.
internal interface IStartupRegistrationStore
{
    bool CleanupConfirmed{get;}
    InstallationStartupValue Read();
    void Write(InstallationStartupValue expected,InstallationStartupValue next);
    void FinalizeCleanup();
}
internal interface IStartupRegistryNative
{
    int Open(int access,ref nint handle);
    int Create(int access,ref nint handle);
    int Query(nint handle,byte[] buffer,out uint type,ref uint bytes);
    int Set(nint handle,byte[] bytes);
    int Delete(nint handle);
    int Flush(nint handle);
    int Close(nint handle);
}
internal sealed class WindowsStartupRegistrationStore:IStartupRegistrationStore
{
    internal const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    internal const string ValueName="KuaiYi";
    internal const int ReadAccess=0x0101,WriteAccess=0x0103,CommandLimit=260;
    internal const int ReadByteLimit=8194;
    private readonly IStartupRegistryNative native;
    private readonly object sync=new();
    private sealed class OriginalKey {internal nint Handle;}
    private readonly List<OriginalKey> retained=[];
    internal WindowsStartupRegistrationStore():this(new Native()){}
    internal WindowsStartupRegistrationStore(IStartupRegistryNative native){this.native=native;}
    public bool CleanupConfirmed{get{lock(sync)return retained.Count==0;}}
    public InstallationStartupValue Read()=>Access(null,null);
    public void Write(InstallationStartupValue expected,InstallationStartupValue next)
    {
        InstallationStartupRegistration.Validate(expected);InstallationStartupRegistration.Validate(next);
        try
        {
            if(expected.Exists)_=new UnicodeEncoding(false,false,true).GetByteCount(expected.Value!);
            if(next.Exists)_=new UnicodeEncoding(false,false,true).GetByteCount(next.Value!);
        }
        catch(EncoderFallbackException){throw new InstallationRecoveryException("Startup value cannot be encoded exactly.");}
        if(next.Exists&&next.Value!.Length>CommandLimit)throw new InstallationRecoveryException("Run command exceeds the Windows supported bound.");
        _=Access(expected,next);
    }
    private InstallationStartupValue Access(InstallationStartupValue? expected,InstallationStartupValue? next)
    {
        lock(sync)
        {
            if(retained.Count!=0)throw new IOException("Original startup registry handles remain unsettled.");
            // The original owner exists before even a partially successful acquisition can throw.
            var owner=new OriginalKey();List<Exception> errors=[];InstallationStartupValue result=new(false);
            try
            {
                int code=native.Open(expected==null?ReadAccess:WriteAccess,ref owner.Handle);
                if(code!=0&&code!=2)throw new Win32Exception(code);
                if(code==0&&owner.Handle==0)throw new IOException("Successful startup acquisition returned no original handle.");
                if(code==2&&owner.Handle!=0)throw new IOException("Failed startup acquisition returned an unconfirmed handle.");
                if(code==2&&expected is{Exists:false}&&next is{Exists:true})
                {
                    code=native.Create(WriteAccess,ref owner.Handle);
                    if(code!=0)throw new Win32Exception(code);
                    if(owner.Handle==0)throw new IOException("Successful startup creation returned no original handle.");
                }
                result=owner.Handle==0?new(false):ReadNamed(owner.Handle);
                if(expected!=null)
                {
                    // Only the named value participates. A concurrently created value is foreign even in a new key.
                    if(result!=expected)throw new HostRegistrationConflictException();
                    if(next!=result)
                    {
                        if(owner.Handle==0)throw new IOException("Startup mutation has no original key handle.");
                        code=next!.Exists?native.Set(owner.Handle,Encoding.Unicode.GetBytes(next.Value+"\0")):native.Delete(owner.Handle);
                        if(code!=0)throw new Win32Exception(code);
                        code=native.Flush(owner.Handle);if(code!=0)throw new Win32Exception(code);
                    }
                }
            }
            catch(Exception error){errors.Add(error);}
            try{Close(owner);}catch(Exception error){retained.Add(owner);errors.Add(error);}
            HostRegistrationTransaction.Throw(errors);return result;
        }
    }
    private InstallationStartupValue ReadNamed(nint handle)
    {
        byte[] buffer=new byte[ReadByteLimit];uint bytes=(uint)buffer.Length;
        int code=native.Query(handle,buffer,out uint type,ref bytes);
        if(code==2)return new(false); // On this error the size output is undefined; do not inspect it.
        if(code==234)return new(true,"UNSUPPORTED_OVERSIZE");
        if(code!=0)throw new Win32Exception(code);
        if(type!=1)return new(true,"REG_TYPE_"+type);
        if(bytes<2||bytes>buffer.Length||(bytes&1)!=0)return new(true,"UNSUPPORTED_REG_SZ");
        string value;
        try{value=new UnicodeEncoding(false,false,true).GetString(buffer,0,(int)bytes);}
        catch(DecoderFallbackException){return new(true,"UNSUPPORTED_REG_SZ");}
        // No expansion or repair of malformed stored bytes: compare only canonical, bounded REG_SZ.
        if(value[^1]!='\0'||value.AsSpan(0,value.Length-1).Contains('\0'))return new(true,"UNSUPPORTED_REG_SZ");
        return new(true,"REG_SZ",value[..^1]);
    }
    private void Close(OriginalKey owner)
    {
        if(owner.Handle==0)return;
        int code=native.Close(owner.Handle);if(code!=0)throw new Win32Exception(code);owner.Handle=0;
    }
    public void FinalizeCleanup()
    {
        lock(sync)
        {
            List<Exception> errors=[];
            foreach(var owner in retained.ToArray())try{Close(owner);retained.Remove(owner);}catch(Exception error){errors.Add(error);}
            HostRegistrationTransaction.Throw(errors);
        }
    }
    private sealed class Native:IStartupRegistryNative
    {
        private static nint CurrentUser=>unchecked((nint)(int)0x80000001);
        public int Open(int access,ref nint handle)
        {if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();return RegOpenKeyEx(CurrentUser,KeyPath,0,access,out handle);}
        public int Create(int access,ref nint handle)=>RegCreateKeyEx(CurrentUser,KeyPath,0,null,0,access,0,out handle,out _);
        public int Query(nint handle,byte[] buffer,out uint type,ref uint bytes)=>RegQueryValueEx(handle,ValueName,0,out type,buffer,ref bytes);
        public int Set(nint handle,byte[] bytes)=>RegSetValueEx(handle,ValueName,0,1,bytes,(uint)bytes.Length);
        public int Delete(nint handle)=>RegDeleteValue(handle,ValueName);
        public int Flush(nint handle)=>RegFlushKey(handle);
        public int Close(nint handle)=>RegCloseKey(handle);
    }
    [DllImport("advapi32.dll",EntryPoint="RegOpenKeyExW",CharSet=CharSet.Unicode,ExactSpelling=true)]
    private static extern int RegOpenKeyEx(nint key,string path,uint options,int access,out nint handle);
    [DllImport("advapi32.dll",EntryPoint="RegCreateKeyExW",CharSet=CharSet.Unicode,ExactSpelling=true)]
    private static extern int RegCreateKeyEx(nint key,string path,uint reserved,string? keyClass,uint options,int access,nint security,out nint handle,out uint disposition);
    [DllImport("advapi32.dll",EntryPoint="RegQueryValueExW",CharSet=CharSet.Unicode,ExactSpelling=true)]
    private static extern int RegQueryValueEx(nint key,string name,nint reserved,out uint type,[Out]byte[] buffer,ref uint bytes);
    [DllImport("advapi32.dll",EntryPoint="RegSetValueExW",CharSet=CharSet.Unicode,ExactSpelling=true)]
    private static extern int RegSetValueEx(nint key,string name,uint reserved,uint type,byte[] bytes,uint count);
    [DllImport("advapi32.dll",EntryPoint="RegDeleteValueW",CharSet=CharSet.Unicode,ExactSpelling=true)]
    private static extern int RegDeleteValue(nint key,string name);
    [DllImport("advapi32.dll",ExactSpelling=true)]private static extern int RegFlushKey(nint key);
    [DllImport("advapi32.dll",ExactSpelling=true)]private static extern int RegCloseKey(nint key);
}
