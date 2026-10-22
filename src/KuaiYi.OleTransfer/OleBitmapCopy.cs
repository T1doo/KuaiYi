using System.Security.Cryptography;
using KuaiYi.Core;
namespace KuaiYi.OleTransfer;
/// <summary>Apartment-bound adapter owns the actual medium, including pUnkForRelease. Never frees the source HGLOBAL.</summary>
public interface IOleBitmapMedium
{
    ulong Size{get;}
    void Lock();
    void Copy(int offset,byte[] destination,int at,int count);
    void Unlock();
    void Release();
}
public static class OleBitmapCopy
{
    public static byte[] Read(IOleBitmapMedium medium,Func<bool> current)
    {
        byte[] first=new byte[40],last=new byte[40];byte[]? bmp=null;var cleanup=new OleCleanupScope();cleanup.Own(medium.Release);
        void Check(){if(!current())throw new OperationCanceledException();}
        try
        {
            cleanup.Run(()=>
            {
                Check();ulong size=medium.Size;if(size<40||size>OleBitmapDrop.AllocationLimit)throw new InvalidDataException("DIB allocation limit.");
                medium.Lock();cleanup.Own(medium.Unlock);Check();medium.Copy(0,first,0,40);Check();var layout=OleBitmapDrop.Inspect(first,size);
                bmp=new byte[layout.BmpLength];OleBitmapDrop.WriteFileHeader(bmp);first.CopyTo(bmp,14);
                for(int at=0;at<layout.PixelBytes;){Check();int n=Math.Min(65536,layout.PixelBytes-at);medium.Copy(40+at,bmp,54+at,n);at+=n;}
                Check();medium.Copy(0,last,0,40);if(!first.AsSpan().SequenceEqual(last))throw new InvalidDataException("DIB header changed.");
                for(int y=0;y<layout.Height;y++)bmp.AsSpan(54+y*layout.Stride+layout.Width*3,layout.Stride-layout.Width*3).Clear();
                Check();OleBitmapDrop.ValidateBmp(bmp);return true;
            });
            Check();var result=bmp!;bmp=null;return result;
        }
        finally{CryptographicOperations.ZeroMemory(first);CryptographicOperations.ZeroMemory(last);if(bmp!=null)CryptographicOperations.ZeroMemory(bmp);}
    }
}
