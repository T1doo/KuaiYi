using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.NativeHost;

/// <summary>Unwired installer adapter. Fixed HKCU keys/default REG_SZ only, no policies, ACLs or key deletion.
/// Read/check/write is cooperative, never atomic compare-exchange or protection against same-user races.</summary>
internal sealed class WindowsHostRegistrationStore:IHostRegistrationStore
{
    private sealed class OriginalKey(nint raw)
    {
        private nint original=raw;
        private SafeRegistryHandle? wrapper;
        internal RegistryKey? Key;
        [SupportedOSPlatform("windows")]
        internal void Initialize(){wrapper=new(original,ownsHandle:false);Key=RegistryKey.FromHandle(wrapper,RegistryView.Registry32);}
        [SupportedOSPlatform("windows")]
        internal void Close()
        {
            List<Exception> errors=[];
            if(Key!=null)try{Key.Dispose();Key=null;}catch(Exception error){errors.Add(error);}
            if(wrapper!=null)try{wrapper.Dispose();wrapper=null;}catch(Exception error){errors.Add(error);}
            // CLR IsClosed does not prove ReleaseHandle succeeded. Own the exact raw handle
            // and check RegCloseKey's result; non-owning wrappers never close it for us.
            if(original!=0)
            {
                int code=RegCloseKey(original);if(code==0)original=0;else errors.Add(new Win32Exception(code));
            }
            HostRegistrationTransaction.Throw(errors);
        }
    }
    private readonly List<OriginalKey> retained=[];
    public bool CleanupConfirmed {get{lock(retained)return retained.Count==0;}}
    public void FinalizeCleanup()
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        lock(retained)
        {
            List<Exception> errors=[];
            foreach(var owner in retained.ToArray())try{owner.Close();retained.Remove(owner);}catch(Exception error){errors.Add(error);}
            HostRegistrationTransaction.Throw(errors);
        }
    }
    internal static string Key(IntegrationBrowser browser)=>browser switch
    {
        IntegrationBrowser.Chrome=>@"Software\Google\Chrome\NativeMessagingHosts\"+InstalledHostPolicy.HostName,
        IntegrationBrowser.Edge=>@"Software\Microsoft\Edge\NativeMessagingHosts\"+InstalledHostPolicy.HostName,
        _=>throw new ArgumentOutOfRangeException(nameof(browser))
    };
    public HostRegistrationSlot Read(IntegrationBrowser browser)
    {if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();return Access(browser,null,false,ReadKey);}
    public void Write(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(value!=null&&(value.Length is <1 or >32768||value.Contains('\0')||!Path.IsPathFullyQualified(value)))throw new InvalidDataException("Invalid host manifest path.");
        WriteWindows(browser,expected,value);
    }
    [SupportedOSPlatform("windows")]
    private void WriteWindows(IntegrationBrowser browser,HostRegistrationSlot expected,string? value)
    {
        Access(browser,expected,value!=null,key=>
        {
            var actual=ReadKey(key);var compare=expected;
            if(value!=null&&!expected.KeyExists)compare=expected with{KeyExists=true}; // A created empty key is retained, never owned/deleted as a key.
            if(actual!=compare)throw new HostRegistrationConflictException();
            if(value!=null)key!.SetValue("",value,RegistryValueKind.String);else key?.DeleteValue("",false);
            key?.Flush();return true;
        });
    }
    [SupportedOSPlatform("windows")]
    private static HostRegistrationSlot ReadKey(RegistryKey? key)
    {
        if(key==null)return new(false,null);
        int count=key.ValueCount;bool extra=key.SubKeyCount!=0||count>1;
        object? value=IntegrationRegistryValue.Read(key,null);
        return new(true,value as string,extra||value!=null&&value is not string||value==null&&count!=0);
    }
    [SupportedOSPlatform("windows")]
    private T Access<T>(IntegrationBrowser browser,HostRegistrationSlot? expected,bool create,Func<RegistryKey?,T> action)
    {
        lock(retained)
        {
            if(retained.Count!=0)throw new IOException("Original registry handle owners remain unsettled.");
            OriginalKey? owner=null;T result=default!;List<Exception> errors=[];
            try
            {
                const int read=0x20019,set=2,view32=0x200;
                int access=read|view32|(expected==null?0:set);
                nint currentUser=unchecked((nint)(int)0x80000001);
                int code=RegOpenKeyEx(currentUser,Key(browser),0,access,out nint opened);
                if(code==0){owner=new(opened);owner.Initialize();}
                else if(code!=2)throw new Win32Exception(code);
                if(expected!=null&&ReadKey(owner?.Key)!=expected)throw new HostRegistrationConflictException();
                if(owner==null&&create)
                {
                    code=RegCreateKeyEx(currentUser,Key(browser),0,null,0,access,0,out opened,out _);
                    if(code!=0)throw new Win32Exception(code);owner=new(opened);owner.Initialize();
                }
                result=action(owner?.Key);
            }
            catch(Exception error){errors.Add(error);}
            finally{if(owner!=null)try{owner.Close();}catch(Exception error){retained.Add(owner);errors.Add(error);}}
            HostRegistrationTransaction.Throw(errors);return result;
        }
    }
    [DllImport("advapi32.dll",EntryPoint="RegOpenKeyExW",CharSet=CharSet.Unicode)]
    private static extern int RegOpenKeyEx(nint key,string name,uint options,int access,out nint result);
    [DllImport("advapi32.dll",EntryPoint="RegCreateKeyExW",CharSet=CharSet.Unicode)]
    private static extern int RegCreateKeyEx(nint key,string name,uint reserved,string? keyClass,uint options,int access,nint security,out nint result,out uint disposition);
    [DllImport("advapi32.dll")]
    private static extern int RegCloseKey(nint key);
}
