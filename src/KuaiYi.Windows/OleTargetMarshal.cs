using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using KuaiYi.OleTransfer;
namespace KuaiYi.Windows;
public static class OleTargetMarshal
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]public delegate int Callback(int kind,nint borrowed,uint keys,OlePoint point,ref uint effect);
    [DllImport("KuaiYi.OleNativeBridge.dll",ExactSpelling=true,CallingConvention=CallingConvention.Cdecl)]private static extern int KY_CreateDropTarget(Callback callback,out nint target);
    [DllImport("ole32.dll")]private static extern int CreateStreamOnHGlobal(nint memory,[MarshalAs(UnmanagedType.Bool)]bool delete,out nint stream);
    [DllImport("ole32.dll")]private static extern int GetHGlobalFromStream(nint stream,out nint memory);
    [DllImport("ole32.dll")]private static extern int CoMarshalInterface(nint stream,ref Guid iid,nint target,uint context,nint reserved,uint flags);
    [DllImport("ole32.dll")]private static extern int CoUnmarshalInterface(nint stream,ref Guid iid,out nint target);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll")]private static extern nint GlobalFree(nint memory);
    [DllImport("ole32.dll",EntryPoint="RegisterDragDrop")]public static extern int RegisterPointer(nint hwnd,nint nativeTarget);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]private delegate int StreamPosition(nint stream,long offset,uint origin,out ulong position);
    public sealed class Export:IDisposable
    {
        private readonly int thread=Environment.CurrentManagedThreadId;
        private readonly Callback callback;private nint target,stream;private bool marshaled;
        private GCHandle callbackRoot;private bool nativeCreated;
        public byte[] Packet {get;}
        public Export(Callback callback)
        {
            this.callback=callback;
            callbackRoot=GCHandle.Alloc(callback);
            try
            {
                Marshal.ThrowExceptionForHR(KY_CreateDropTarget(callback,out target));nativeCreated=true;Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(0,true,out stream));
                Guid id=OleReceiverWire.DropTargetId;Marshal.ThrowExceptionForHR(CoMarshalInterface(stream,ref id,target,0,0,1));marshaled=true; // LOCAL=0, TABLESTRONG=1.
                Marshal.ThrowExceptionForHR(GetHGlobalFromStream(stream,out nint memory));
                var seek=Marshal.GetDelegateForFunctionPointer<StreamPosition>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(stream),5*nint.Size));Marshal.ThrowExceptionForHR(seek(stream,0,1,out ulong size));
                if(size is <72 or >OleReceiverWire.ExportLimit||size>OleNative.GlobalSize(memory))throw new InvalidDataException("Invalid owned marshal length.");
                nint bits=OleNative.GlobalLock(memory);if(bits==0)throw new IOException("Cannot lock own marshal stream.");
                try{Packet=new byte[(int)size];Marshal.Copy(bits,Packet,0,Packet.Length);OleReceiverWire.ValidateExport(Packet);}
                finally{OleNative.GlobalUnlock(memory);}
            }
            catch{Dispose();throw;}
        }
        public void Dispose()
        {
            if(Environment.CurrentManagedThreadId!=thread)throw new InvalidOperationException("Marshal export crossed STA.");
            var scope=new KuaiYi.Core.OleCleanupScope();
            if(target!=0){nint owned=target;target=0;scope.Own(()=>{if(Marshal.Release(owned)==0&&callbackRoot.IsAllocated)callbackRoot.Free();});}
            if(stream!=0){nint owned=stream;stream=0;scope.Own(()=>Marshal.Release(owned));if(marshaled){marshaled=false;scope.Own(()=>OleNative.ReleaseUnusedMarshalPacket(owned));}}
            try{scope.Dispose();}finally{if(!nativeCreated&&callbackRoot.IsAllocated)callbackRoot.Free();if(Packet!=null)CryptographicOperations.ZeroMemory(Packet);GC.KeepAlive(callback);}
            // If external standard-marshaler refs remain, keep the callback pinned until this one-shot receiver exits.
        }
    }
    public static nint Import(byte[] trustedPacket)
    {
        OleReceiverWire.ValidateExport(trustedPacket);nint memory=GlobalAlloc(2,(nuint)trustedPacket.Length);if(memory==0)throw new OutOfMemoryException();nint stream=0;
        try
        {
            nint bits=OleNative.GlobalLock(memory);if(bits==0)throw new IOException("Cannot lock own import stream.");try{Marshal.Copy(trustedPacket,0,bits,trustedPacket.Length);}finally{OleNative.GlobalUnlock(memory);}
            Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(memory,true,out stream));memory=0;Guid id=OleReceiverWire.DropTargetId;Marshal.ThrowExceptionForHR(CoUnmarshalInterface(stream,ref id,out nint target));return target;
        }
        finally{if(stream!=0)Marshal.Release(stream);if(memory!=0)GlobalFree(memory);}
    }
}
