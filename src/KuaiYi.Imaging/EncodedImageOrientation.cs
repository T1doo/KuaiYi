using System.Buffers.Binary;
namespace KuaiYi.Imaging;

/// <summary>Bounded JPEG APP1/IFD0 Orientation only. Never follows GPS, thumbnail or nested IFDs.</summary>
public static class EncodedImageOrientation
{
    public static ImageOrientation Read(ReadOnlySpan<byte> bytes,EncodedRasterFormat format)
    {
        // PNG eXIf is explicitly unsupported even if a native codec ignores that chunk.
        // Walk lengths only here; the isolated worker separately verifies the container CRCs.
        if(format==EncodedRasterFormat.Png)
        {
            int at=8,chunks=0;
            while(bytes.Length-at>=12)
            {
                if(++chunks>256)throw new ImageJobException(ImageFailure.UnsupportedImage);
                uint count=BinaryPrimitives.ReadUInt32BigEndian(bytes[at..]);if(count>(uint)(bytes.Length-at-12))Bad();
                var type=bytes.Slice(at+4,4);if(type.SequenceEqual("eXIf"u8))throw new ImageJobException(ImageFailure.UnsupportedImage);
                at+=checked((int)count+12);if(type.SequenceEqual("IEND"u8)){if(at!=bytes.Length)Bad();return ImageOrientation.ExifTopLeft;}
            }
            Bad();
        }
        int offset=2,segments=0;bool exif=false;var orientation=ImageOrientation.ExifTopLeft;
        while(offset<bytes.Length-2)
        {
            if(++segments>256)throw new ImageJobException(ImageFailure.UnsupportedImage);
            if(bytes[offset++]!=255)Bad();while(offset<bytes.Length&&bytes[offset]==255)offset++;
            if(offset>=bytes.Length)Bad();int marker=bytes[offset++];if(bytes.Length-offset<2)Bad();
            int length=BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);if(length<2||length>bytes.Length-offset)Bad();
            if(marker==218)return orientation;
            var segment=bytes.Slice(offset+2,length-2);offset+=length;
            if(marker!=225||!segment.StartsWith("Exif\0\0"u8))continue;
            if(exif)Bad();exif=true;orientation=ReadTiff(segment[6..]);
        }
        Bad();return default;
    }
    private static ImageOrientation ReadTiff(ReadOnlySpan<byte> tiff)
    {
        if(tiff.Length<8)Bad();bool little=tiff[..2].SequenceEqual("II"u8);
        if(!little&&!tiff[..2].SequenceEqual("MM"u8)||U16(tiff[2..],little)!=42)Bad();
        uint position=U32(tiff[4..],little);if(position<8||position>(uint)(tiff.Length-2))Bad();int at=(int)position,count=U16(tiff[at..],little);
        if(count>256)throw new ImageJobException(ImageFailure.UnsupportedImage);
        if(2L+12L*count+4>tiff.Length-at)Bad();var orientation=ImageOrientation.ExifTopLeft;bool seen=false;
        for(int i=0;i<count;i++)
        {
            var entry=tiff.Slice(at+2+12*i,12);if(U16(entry,little)!=0x112)continue;
            if(seen||U16(entry[2..],little)!=3||U32(entry[4..],little)!=1)Bad();seen=true;
            int value=U16(entry[8..],little);if(value is <1 or >8)Bad();orientation=(ImageOrientation)value;
        }
        return orientation;
    }
    private static ushort U16(ReadOnlySpan<byte> b,bool little)=>little?BinaryPrimitives.ReadUInt16LittleEndian(b):BinaryPrimitives.ReadUInt16BigEndian(b);
    private static uint U32(ReadOnlySpan<byte> b,bool little)=>little?BinaryPrimitives.ReadUInt32LittleEndian(b):BinaryPrimitives.ReadUInt32BigEndian(b);
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
}
