using System.IO;
using System.IO.MemoryMappedFiles;
using KuaiYi.OleTransfer;
namespace KuaiYi.Windows;
/// <summary>32-byte session gate, no content. App's aligned atomic store; receiver opens read-only and verifies scope/parent.</summary>
public unsafe sealed class OleSharedActivation:IOleActivationLease
{
    private readonly object sync=new();private readonly MemoryMappedFile map;private readonly MemoryMappedViewAccessor view;
    private byte* pointer;private bool disposed;public string Name{get;}
    public OleSharedActivation(Guid instance)
    {
        Name=OleReceiverWire.ActivationName(instance);map=MemoryMappedFile.CreateNew(Name,32,MemoryMappedFileAccess.ReadWrite);
        try{view=map.CreateViewAccessor(0,32,MemoryMappedFileAccess.ReadWrite);view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);view.Write(0,0x474f594bu);view.Write(4,Environment.ProcessId);view.WriteArray(8,instance.ToByteArray(),0,16);Set(0,false);}
        catch{if(pointer!=null&&view!=null){view.SafeMemoryMappedViewHandle.ReleasePointer();pointer=null;}view?.Dispose();map.Dispose();throw;}
    }
    public void Set(long epoch,bool enabled)
    {
        lock(sync)
        {
            if(disposed){if(enabled)throw new ObjectDisposedException(nameof(OleSharedActivation));return;}
            if(epoch<0||enabled&&epoch>long.MaxValue/2)throw new ArgumentOutOfRangeException(nameof(epoch));
            long stamp=enabled?(epoch<<1)|1:epoch>long.MaxValue/2?-2:epoch<<1;
            Volatile.Write(ref *(long*)(pointer+24),stamp);
        }
    }
    public void Dispose()
    {
        lock(sync){if(disposed)return;Set(long.MaxValue,false);disposed=true;view.SafeMemoryMappedViewHandle.ReleasePointer();pointer=null;view.Dispose();map.Dispose();}
    }
    public unsafe sealed class Reader:IDisposable
    {
        private readonly MemoryMappedFile map;private readonly MemoryMappedViewAccessor view;private byte* pointer;
        public Reader(OleReceiverHello hello)
        {
            OleReceiverWire.Validate(hello);map=MemoryMappedFile.OpenExisting(hello.ActivationName,MemoryMappedFileRights.Read);
            try
            {
                view=map.CreateViewAccessor(0,32,MemoryMappedFileAccess.Read);byte[] id=new byte[16];view.ReadArray(8,id,0,16);
                if(view.ReadUInt32(0)!=0x474f594b||view.ReadInt32(4)!=hello.AppProcessId||new Guid(id)!=hello.Instance)throw new InvalidDataException("Wrong activation owner/scope.");
                view.SafeMemoryMappedViewHandle.AcquirePointer(ref pointer);
            }
            catch{if(pointer!=null&&view!=null){view.SafeMemoryMappedViewHandle.ReleasePointer();pointer=null;}view?.Dispose();map.Dispose();throw;}
        }
        public bool Current(long epoch){if(pointer==null)return false;long stamp=Volatile.Read(ref *(long*)(pointer+24));return stamp>=0&&(stamp&1)==1&&(stamp>>1)==epoch;}
        public void Dispose(){if(pointer==null)return;view.SafeMemoryMappedViewHandle.ReleasePointer();pointer=null;view.Dispose();map.Dispose();}
    }
}
