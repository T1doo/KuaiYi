using System.Buffers.Binary;
using System.Security.Cryptography;
namespace KuaiYi.Core;

/// <summary>One CF_DIB subset, wrapped as an inert BMP. No file or desktop coordinate authority.</summary>
public static class OleBitmapDrop
{
    public const int TransportLimit=1024*1024,AllocationLimit=TransportLimit,HeaderSize=40;
    public readonly record struct Layout(int Width,int Height,int Stride,int PixelBytes)
    {public int DibLength=>checked(HeaderSize+PixelBytes);public int BmpLength=>checked(14+DibLength);}
    private static uint U(ReadOnlySpan<byte> header,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(at,4));
    public static Layout Inspect(ReadOnlySpan<byte> header,ulong allocation)
    {
        if(header.Length!=40||allocation<40||allocation>AllocationLimit)throw new InvalidDataException("DIB allocation limit.");
        // No palette, masks, profile, compression or alternate header in this slice.
        if(U(header,0)!=40||BinaryPrimitives.ReadUInt16LittleEndian(header[12..])!=1||BinaryPrimitives.ReadUInt16LittleEndian(header[14..])!=24||U(header,16)!=0||U(header,32)!=0||U(header,36)!=0)throw new InvalidDataException("Unsupported DIB layout.");
        int width=BinaryPrimitives.ReadInt32LittleEndian(header[4..]),signedHeight=BinaryPrimitives.ReadInt32LittleEndian(header[8..]);
        if(width is <1 or >16384||signedHeight==0||signedHeight==int.MinValue||Math.Abs((long)signedHeight)>16384)throw new InvalidDataException("DIB dimensions.");
        int height=Math.Abs(signedHeight);long stride=checked(((long)width*3+3)&~3L),pixels=checked(stride*height);
        if((long)width*height>24_000_000||54+pixels>TransportLimit||40+pixels>(long)allocation||U(header,20)!=0&&U(header,20)!=pixels)throw new InvalidDataException("DIB pixels incomplete or oversized.");
        return new(width,height,(int)stride,(int)pixels);
    }
    public static void ValidateBmp(ReadOnlySpan<byte> bmp)
    {
        if(bmp.Length<54||bmp.Length>TransportLimit||bmp[0]!='B'||bmp[1]!='M'||BinaryPrimitives.ReadUInt32LittleEndian(bmp[2..])!=bmp.Length||BinaryPrimitives.ReadUInt32LittleEndian(bmp[6..])!=0||BinaryPrimitives.ReadUInt32LittleEndian(bmp[10..])!=54)throw new InvalidDataException("Invalid bitmap envelope.");
        var layout=Inspect(bmp.Slice(14,40),(ulong)(bmp.Length-14));if(layout.BmpLength!=bmp.Length)throw new InvalidDataException("Bitmap trailing data.");
        for(int y=0;y<layout.Height;y++)foreach(byte padding in bmp.Slice(54+y*layout.Stride+layout.Width*3,layout.Stride-layout.Width*3))if(padding!=0)throw new InvalidDataException("Bitmap row padding.");
    }
    public static void WriteFileHeader(Span<byte> bmp)
    {bmp[..14].Clear();bmp[0]=(byte)'B';bmp[1]=(byte)'M';BinaryPrimitives.WriteUInt32LittleEndian(bmp[2..],(uint)bmp.Length);BinaryPrimitives.WriteUInt32LittleEndian(bmp[10..],54);}
}

/// <summary>Independent copied bytes. Dispose clears pixels, headers and padding.</summary>
public sealed class OwnedOleBitmap:IDisposable
{
    private byte[]? bytes;
    public OwnedOleBitmap(ReadOnlySpan<byte> source){OleBitmapDrop.ValidateBmp(source);bytes=source.ToArray();}
    public ReadOnlyMemory<byte> Bytes=>Volatile.Read(ref bytes)??throw new ObjectDisposedException(nameof(OwnedOleBitmap));
    public void Dispose(){var held=Interlocked.Exchange(ref bytes,null);if(held!=null)CryptographicOperations.ZeroMemory(held);}
}
