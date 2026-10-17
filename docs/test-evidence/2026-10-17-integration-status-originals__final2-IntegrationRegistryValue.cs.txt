using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.NativeHost;

/// <summary>Fixed-size reads; never reallocates to an untrusted registry-reported length.</summary>
public static class IntegrationRegistryValue
{
    public const int Limit=65536;
    [SupportedOSPlatform("windows")]
    public static object? Read(RegistryKey? key,string? name)
    {
        if(key==null)return null;byte[] bytes=new byte[Limit];uint length=Limit;
        int error=RegQueryValueEx(key.Handle,name,0,out uint type,bytes,ref length);
        return Decode(error,type,bytes,length);
    }
    public static object? Decode(int error,uint type,byte[] bytes,uint length)
    {
        if(error==2)return null;
        if(error==5)throw new UnauthorizedAccessException();
        if(error==234)throw new InvalidDataException();
        if(error!=0)throw new IOException();
        if(length>Limit||length>bytes.Length)throw new InvalidDataException();
        if(type==4&&length==4)return BinaryPrimitives.ReadInt32LittleEndian(bytes);
        if(type!=1||length<2||length%2!=0||bytes[length-1]!=0||bytes[length-2]!=0)throw new InvalidDataException();
        string value;
        try{value=new UnicodeEncoding(false,false,true).GetString(bytes,0,(int)length-2);}
        catch(DecoderFallbackException){throw new InvalidDataException();}
        if(value.Contains('\0'))throw new InvalidDataException();return value;
    }
    [SupportedOSPlatform("windows")]
    public static string[] Names(RegistryKey key)
    {
        int count=key.ValueCount;if(count>64||key.SubKeyCount!=0)throw new InvalidDataException();var names=new string[count];
        for(uint index=0;index<count;index++)
        {
            var text=new StringBuilder(3);uint length=3;
            int error=RegEnumValue(key.Handle,index,text,ref length,0,0,0,0);
            if(error==5)throw new UnauthorizedAccessException();if(error==234)throw new InvalidDataException();if(error!=0)throw new IOException();
            names[index]=text.ToString();
        }
        if(key.ValueCount!=count)throw new IOException();return names;
    }
    [DllImport("advapi32.dll",EntryPoint="RegQueryValueExW",CharSet=CharSet.Unicode)]
    private static extern int RegQueryValueEx(SafeRegistryHandle key,string? name,nint reserved,out uint type,[Out] byte[] data,ref uint length);
    [DllImport("advapi32.dll",EntryPoint="RegEnumValueW",CharSet=CharSet.Unicode)]
    private static extern int RegEnumValue(SafeRegistryHandle key,uint index,StringBuilder name,ref uint length,nint reserved,nint type,nint data,nint dataLength);
}
