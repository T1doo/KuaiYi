using System.Buffers.Binary;
using System.Security.Cryptography;
namespace KuaiYi.Imaging;

/// <summary>Bounded JPEG APP1 and PNG eXIf/IFD0 Orientation. No nested IFD traversal.</summary>
public static class EncodedImageOrientation
{
    public static ImageOrientation Read(ReadOnlySpan<byte> bytes,EncodedRasterFormat format)
        =>Read(bytes,format,default);
    public static ImageOrientation Read(ReadOnlySpan<byte> bytes,EncodedRasterFormat format,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if(format==EncodedRasterFormat.Png)return ReadPng(bytes,cancellation,out _,out _);
        int offset=2,segments=0;bool exif=false;var orientation=ImageOrientation.ExifTopLeft;
        while(offset<bytes.Length-2)
        {
            cancellation.ThrowIfCancellationRequested();if(++segments>256)throw new ImageJobException(ImageFailure.UnsupportedImage);
            if(bytes[offset++]!=255)Bad();while(offset<bytes.Length&&bytes[offset]==255)offset++;
            if(offset>=bytes.Length)Bad();int marker=bytes[offset++];if(bytes.Length-offset<2)Bad();
            int length=BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);if(length<2||length>bytes.Length-offset)Bad();
            if(marker==218)return orientation;
            var segment=bytes.Slice(offset+2,length-2);offset+=length;
            if(marker!=225||!segment.StartsWith("Exif\0\0"u8))continue;
            if(exif)Bad();exif=true;orientation=ReadTiff(segment[6..],null,cancellation);
        }
        Bad();return default;
    }
    private static ImageOrientation ReadTiff(ReadOnlySpan<byte> tiff,ImageSize? pngSize,CancellationToken cancellation)
    {
        if(tiff.Length<8)Bad();bool little=tiff[..2].SequenceEqual("II"u8);
        if(!little&&!tiff[..2].SequenceEqual("MM"u8)||U16(tiff[2..],little)!=42)Bad();
        uint position=U32(tiff[4..],little);if(position<8||position>(uint)(tiff.Length-2)||pngSize!=null&&(position&1)!=0)Bad();int at=(int)position,count=U16(tiff[at..],little);
        if(count>256)throw new ImageJobException(ImageFailure.UnsupportedImage);
        long directoryEnd=checked((long)at+2+12L*count+4);if(directoryEnd>tiff.Length)Bad();
        // PNG is deliberately one IFD0: reject every nextIFD, including cycles, without traversal.
        if(pngSize!=null&&U32(tiff.Slice(at+2+12*count),little)!=0)throw new ImageJobException(ImageFailure.UnsupportedImage);
        var orientation=ImageOrientation.ExifTopLeft;bool seen=false,widthSeen=false,heightSeen=false;
        for(int i=0;i<count;i++)
        {
            cancellation.ThrowIfCancellationRequested();var entry=tiff.Slice(at+2+12*i,12);ushort tag=U16(entry,little);
            if(pngSize is {} size)
            {
                ValidatePngEntry(entry,little,at,directoryEnd,tiff.Length);
                if(tag is 0x14a or 0x8769 or 0x8825 or 0xa005)throw new ImageJobException(ImageFailure.UnsupportedImage);
                if(tag is 0x100 or 0x101)
                {
                    ushort type=U16(entry[2..],little);if(type is not(3 or 4)||U32(entry[4..],little)!=1)Bad();
                    uint declared=type==3?U16(entry[8..],little):U32(entry[8..],little);
                    if(tag==0x100){if(widthSeen||declared!=size.Width)Bad();widthSeen=true;}
                    else{if(heightSeen||declared!=size.Height)Bad();heightSeen=true;}
                }
            }
            if(tag!=0x112)continue;
            if(seen||U16(entry[2..],little)!=3||U32(entry[4..],little)!=1)Bad();seen=true;
            int value=U16(entry[8..],little);if(value is <1 or >8)Bad();orientation=(ImageOrientation)value;
        }
        return orientation;
    }
    // Parent scans bounded structure and eXIf only; the Worker independently checks all chunk CRCs.
    private static ImageOrientation ReadPng(ReadOnlySpan<byte> bytes,CancellationToken cancellation,out int exifOffset,out int exifBytes)
    {
        exifOffset=-1;exifBytes=0;var size=ImagePolicy.InspectPng(bytes);int at=8,chunks=0;bool first=true,idat=false,afterIdat=false,exif=false,palette=false;var orientation=ImageOrientation.ExifTopLeft;
        while(bytes.Length-at>=12)
        {
            cancellation.ThrowIfCancellationRequested();if(++chunks>256)throw new ImageJobException(ImageFailure.UnsupportedImage);
            uint count=BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);if(count>(uint)(bytes.Length-at-12))Bad();int length=(int)count;var type=bytes.Slice(at+4,4);
            foreach(byte letter in type)if(letter is not(>=65 and <=90 or >=97 and <=122))Bad();if((type[2]&32)!=0)Bad();
            if(first?(!type.SequenceEqual("IHDR"u8)||length!=13):type.SequenceEqual("IHDR"u8))Bad();first=false;
            if(type.SequenceEqual("acTL"u8)||type.SequenceEqual("fcTL"u8)||type.SequenceEqual("fdAT"u8))throw new ImageJobException(ImageFailure.UnsupportedImage);
            if(type.SequenceEqual("IDAT"u8)){if(afterIdat)Bad();idat=true;}else if(idat)afterIdat=true;
            if(type.SequenceEqual("PLTE"u8)){if(palette||idat)Bad();palette=true;}
            if(type.SequenceEqual("eXIf"u8))
            {
                if(exif||idat)Bad();exif=true;
                // Product subset, not the PNG format maximum: no larger than a JPEG-compatible Exif profile.
                if(length>65527)throw new ImageJobException(ImageFailure.UnsupportedImage);
                ImageDecodePolicy.ValidatePngChunkCrc(bytes.Slice(at+4,length+4),BinaryPrimitives.ReadUInt32BigEndian(bytes[(at+8+length)..]),cancellation);
                orientation=ReadTiff(bytes.Slice(at+8,length),size,cancellation);exifOffset=at;exifBytes=length+12;
            }
            if((type[0]&32)==0&&!type.SequenceEqual("IHDR"u8)&&!type.SequenceEqual("PLTE"u8)&&!type.SequenceEqual("IDAT"u8)&&!type.SequenceEqual("IEND"u8))throw new ImageJobException(ImageFailure.UnsupportedImage);
            at+=checked(length+12);
            if(type.SequenceEqual("IEND"u8)){if(length!=0||at!=bytes.Length||!idat)Bad();return orientation;}
        }
        Bad();return default;
    }
    // Only after the Worker has checked the original job/hash and every container CRC.
    // This helper independently validates metadata too. Native receives one exact copy with eXIf removed;
    // product sourceOrientation is applied once by the area kernel, not attributed to native support.
    internal static byte[] CopyPngForNative(ReadOnlySpan<byte> bytes,bool allowOrientation,out ImageOrientation sourceOrientation,CancellationToken cancellation,Action<byte[]>? allocated=null)
    {
        sourceOrientation=ReadPng(bytes,cancellation,out int offset,out int removed);
        if(!allowOrientation&&sourceOrientation!=ImageOrientation.ExifTopLeft)throw new ImageJobException(ImageFailure.UnsupportedImage);
        byte[]? copy=new byte[bytes.Length-removed];
        try
        {
            int before=offset<0?bytes.Length:offset;
            for(int at=0;at<before;){cancellation.ThrowIfCancellationRequested();int count=Math.Min(65536,before-at);bytes.Slice(at,count).CopyTo(copy.AsSpan(at,count));at+=count;}
            for(int from=before+removed,to=before;from<bytes.Length;){cancellation.ThrowIfCancellationRequested();int count=Math.Min(65536,bytes.Length-from);bytes.Slice(from,count).CopyTo(copy.AsSpan(to,count));from+=count;to+=count;}
            allocated?.Invoke(copy);cancellation.ThrowIfCancellationRequested();var delivery=copy;copy=null;return delivery;
        }
        finally{if(copy!=null)CryptographicOperations.ZeroMemory(copy);}
    }
    private static void ValidatePngEntry(ReadOnlySpan<byte> entry,bool little,int directoryStart,long directoryEnd,int length)
    {
        int unit=U16(entry[2..],little) switch{1 or 2 or 6 or 7=>1,3 or 8=>2,4 or 9 or 11=>4,5 or 10 or 12=>8,_=>throw new ImageJobException(ImageFailure.UnsupportedImage)};
        uint count=U32(entry[4..],little);if(count==0)Bad();long bytes=checked((long)count*unit);if(bytes<=4)return;
        long offset=U32(entry[8..],little),end=checked(offset+bytes);
        if(offset<8||end>length||offset<directoryEnd&&end>directoryStart)Bad();
    }
    private static ushort U16(ReadOnlySpan<byte> b,bool little)=>little?BinaryPrimitives.ReadUInt16LittleEndian(b):BinaryPrimitives.ReadUInt16BigEndian(b);
    private static uint U32(ReadOnlySpan<byte> b,bool little)=>little?BinaryPrimitives.ReadUInt32LittleEndian(b):BinaryPrimitives.ReadUInt32BigEndian(b);
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
}
