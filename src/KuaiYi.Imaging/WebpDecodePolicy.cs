using System.Buffers.Binary;
namespace KuaiYi.Imaging;

/// <summary>Static even-payload VP8/VP8L, optional minimal VP8X. Metadata, ALPH and animation are unsupported.</summary>
internal static class WebpDecodePolicy
{
    internal static ImageSize Inspect(ReadOnlySpan<byte> bytes,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        if(bytes.Length>ImagePolicy.ByteLimit)throw new ImageJobException(ImageFailure.ImageTooLarge);
        if(bytes.Length<4||!bytes[..4].SequenceEqual("RIFF"u8))Unsupported();
        if(bytes.Length<12)Bad();if(!bytes.Slice(8,4).SequenceEqual("WEBP"u8))Unsupported();
        if((bytes.Length&1)!=0||checked((long)U32(bytes,4)+8)!=bytes.Length)Bad();
        int at=12,chunks=0;ImageSize? canvas=null,image=null;byte flags=0;bool lossless=false,imagePadding=false;
        while(at<bytes.Length)
        {
            cancellation.ThrowIfCancellationRequested();if(++chunks>256)Unsupported();
            if(bytes.Length-at<8)Bad();uint count=U32(bytes,at+4);long end=checked((long)at+8+count+(count&1));
            if(end>bytes.Length)Bad();int length=checked((int)count);var type=bytes.Slice(at,4);var payload=bytes.Slice(at+8,length);
            if((count&1)!=0&&bytes[(int)end-1]!=0)Bad();
            if(type.SequenceEqual("VP8X"u8))
            {
                if(chunks!=1||canvas!=null||image!=null||length!=10)Bad();flags=payload[0];
                if((flags&0xc1)!=0||payload[1]!=0||payload[2]!=0||payload[3]!=0)Bad();
                if((flags&~0x10)!=0)Unsupported();
                canvas=new(U24(payload,4)+1,U24(payload,7)+1);ImagePolicy.ValidateDimensions(canvas.Value.Width,canvas.Value.Height);
            }
            else if(type.SequenceEqual("VP8 "u8)||type.SequenceEqual("VP8L"u8))
            {
                if(image!=null)Bad();lossless=type.SequenceEqual("VP8L"u8);
                image=lossless?Lossless(payload):Lossy(payload);imagePadding=(count&1)!=0;
            }
            else Unsupported(); // Includes ANIM/ANMF/ALPH/ICCP/EXIF/XMP, even without their VP8X flags.
            at=checked((int)end);
        }
        if(image==null||canvas is {} c&&c!=image.Value)Bad();
        if(!lossless&&(flags&0x10)!=0)Unsupported(); // VP8 alpha needs ALPH, which this slice never decodes.
        // Fixed native demux includes RIFF padding in the decoder fragment. A real truncated
        // VP8L borrowed that byte and returned Success with changed pixels. Until a bounded
        // native payload API is available, reject legal odd payloads too; never supply that byte.
        if(imagePadding)Unsupported();
        return image!.Value;
    }
    private static ImageSize Lossless(ReadOnlySpan<byte> payload)
    {
        if(payload.Length<5||payload[0]!=0x2f)Bad();uint bits=U32(payload,1);
        if((bits>>29)!=0)Unsupported();
        // Bit28 is only an alpha hint. Never discard actual native alpha based on it.
        var size=new ImageSize((bits&0x3fff)+1,((bits>>14)&0x3fff)+1);ImagePolicy.ValidateDimensions(size.Width,size.Height);return size;
    }
    private static ImageSize Lossy(ReadOnlySpan<byte> payload)
    {
        if(payload.Length<10)Bad();uint tag=U24(payload,0);
        if((tag&1)!=0||((tag>>1)&7)>3)Unsupported();if((tag&16)==0||!payload.Slice(3,3).SequenceEqual(new byte[]{0x9d,1,0x2a}))Bad();
        uint partition=tag>>5;if(partition==0||partition>(uint)(payload.Length-10))Bad();
        ushort width=BinaryPrimitives.ReadUInt16LittleEndian(payload[6..]),height=BinaryPrimitives.ReadUInt16LittleEndian(payload[8..]);
        if((width&0xc000)!=0||(height&0xc000)!=0)Unsupported();
        var size=new ImageSize((uint)(width&0x3fff),(uint)(height&0x3fff));ImagePolicy.ValidateDimensions(size.Width,size.Height);return size;
    }
    private static uint U24(ReadOnlySpan<byte> b,int at)=>(uint)(b[at]|b[at+1]<<8|b[at+2]<<16);
    private static uint U32(ReadOnlySpan<byte> b,int at)=>BinaryPrimitives.ReadUInt32LittleEndian(b[at..]);
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
    private static void Unsupported()=>throw new ImageJobException(ImageFailure.UnsupportedImage);
}
