using System.Runtime.InteropServices;
namespace KuaiYi.Windows;

public readonly record struct SystemTextScale(double Factor,bool Known,int HResult);
/// <summary>Read-only UISettings/IUISettings2 ABI; no WinRT package, process or registry writes.</summary>
public static class NativeTextScale
{
    [DllImport("combase.dll",ExactSpelling=true)]private static extern int RoInitialize(uint type);
    [DllImport("combase.dll",ExactSpelling=true)]private static extern void RoUninitialize();
    [DllImport("combase.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern int WindowsCreateString(string value,uint length,out nint text);
    [DllImport("combase.dll",ExactSpelling=true)]private static extern int WindowsDeleteString(nint text);
    [DllImport("combase.dll",ExactSpelling=true)]private static extern int RoActivateInstance(nint name,out nint instance);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int ReadScale(nint self,out double factor);
    public static SystemTextScale Read()
    {
        if(!OperatingSystem.IsWindows())return new(1,false,unchecked((int)0x80004001));
        nint name=0,instance=0,settings=0;bool initialized=false;
        try
        {
            int hr=RoInitialize(Thread.CurrentThread.GetApartmentState()==ApartmentState.STA?0u:1u);
            if(hr<0)return new(1,false,hr);initialized=true;
            const string runtimeClass="Windows.UI.ViewManagement.UISettings";
            hr=WindowsCreateString(runtimeClass,(uint)runtimeClass.Length,out name);if(hr<0)return new(1,false,hr);
            hr=RoActivateInstance(name,out instance);if(hr<0||instance==0)return new(1,false,hr);
            var iid=new Guid("bad82401-2721-44f9-bb91-2bb228be442f");
            hr=Marshal.QueryInterface(instance,ref iid,out settings);if(hr<0||settings==0)return new(1,false,hr);
            // IInspectable = IUnknown's 3 entries + GetIids/GetRuntimeClassName/GetTrustLevel.
            nint getter=Marshal.ReadIntPtr(Marshal.ReadIntPtr(settings),6*IntPtr.Size);
            hr=Marshal.GetDelegateForFunctionPointer<ReadScale>(getter)(settings,out double factor);
            return hr>=0&&double.IsFinite(factor)&&factor>=1&&factor<=2.25
                ?new(factor,true,hr):new(1,false,hr<0?hr:unchecked((int)0x80070057));
        }
        catch(Exception error)when(error is DllNotFoundException or EntryPointNotFoundException or COMException)
        {return new(1,false,error.HResult);}
        finally
        {
            if(settings!=0)Marshal.Release(settings);if(instance!=0)Marshal.Release(instance);
            if(name!=0)WindowsDeleteString(name);if(initialized)RoUninitialize();
        }
    }
}
