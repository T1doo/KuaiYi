using System.Buffers.Binary;
namespace KuaiYi.Imaging;

/// <summary>Product subset: BM file + 40-byte INFOHEADER, 24bpp BI_RGB, no palette/gap/trailer.</summary>
internal static class BmpDecodePolicy
{
    internal static ImageSize Inspect(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length>ImagePolicy.ByteLimit)throw new ImageJobException(ImageFailure.ImageTooLarge);
        if(bytes.Length<2||bytes[0]!=66||bytes[1]!=77)throw new ImageJobException(ImageFailure.UnsupportedImage);
        if(bytes.Length<18)Bad();if(U32(bytes,14)!=40)Unsupported();if(bytes.Length<54)Bad();
        if(U16(bytes,6)!=0||U16(bytes,8)!=0||U32(bytes,2)!=(uint)bytes.Length)Bad();
        int width=BinaryPrimitives.ReadInt32LittleEndian(bytes[18..]),signedHeight=BinaryPrimitives.ReadInt32LittleEndian(bytes[22..]);
        if(width<=0||signedHeight==0||signedHeight==int.MinValue)Bad();long height=signedHeight<0?checked(-(long)signedHeight):signedHeight;
        ImagePolicy.ValidateDimensions((uint)width,checked((uint)height));if(U16(bytes,26)!=1)Bad();
        if(U16(bytes,28)!=24||U32(bytes,30)!=0||U32(bytes,46)!=0||U32(bytes,50)!=0)Unsupported();
        uint offset=U32(bytes,10);if(offset<54||offset>(uint)bytes.Length)Bad();if(offset!=54)Unsupported();
        long stride=checked((checked((long)width*24)+31)&~31L)/8,payload=checked(stride*height),total=checked(54L+payload);
        if(total!=bytes.Length||U32(bytes,34)!=0&&U32(bytes,34)!=payload)Bad();
        return new((uint)width,(uint)height);
    }
    private static ushort U16(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt16LittleEndian(b[at..]);
    private static uint U32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
    private static void Unsupported()=>throw new ImageJobException(ImageFailure.UnsupportedImage);
}
