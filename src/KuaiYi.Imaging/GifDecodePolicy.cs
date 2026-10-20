using System.Buffers.Binary;
namespace KuaiYi.Imaging;

public sealed record GifFirstFrameInfo(int SourceFrames,int Left,int Top,int Width,int Height);
public sealed record GifInspection(ImageSize Size,GifFirstFrameInfo FirstFrame,int FirstImageEnd,int FirstDataOffset,int PaletteEntries);
/// <summary>Strict GIF product subset. Scan all container structure; never expand later frames.</summary>
public static class GifDecodePolicy
{
    public const int FrameLimit=256,BlockLimit=4096,SubBlockLimit=131072;
    public static GifInspection Inspect(ReadOnlySpan<byte> bytes,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();if(bytes.Length>ImagePolicy.ByteLimit)throw new ImageJobException(ImageFailure.ImageTooLarge);
        if(bytes.Length<13)Bad();bool old=bytes[..6].SequenceEqual("GIF87a"u8);if(!old&&!bytes[..6].SequenceEqual("GIF89a"u8))Bad();
        int width=U16(bytes[6..]),height=U16(bytes[8..]);ImagePolicy.ValidateDimensions((uint)width,(uint)height);
        if(bytes[12]!=0)Unsupported();int at=13,global=(bytes[10]&128)!=0?2<<(bytes[10]&7):0;
        Take(bytes,ref at,global*3);if(global!=0?bytes[11]>=global:bytes[11]!=0)Bad();
        int frames=0,blocks=0,subblocks=0,firstEnd=0,firstData=0,firstPalette=0;bool gce=false,loop=false;int transparent=-1;GifFirstFrameInfo? first=null;
        while(at<bytes.Length)
        {
            cancellation.ThrowIfCancellationRequested();if(++blocks>BlockLimit)Unsupported();byte kind=bytes[at++];
            if(kind==0x3b)
            {if(at!=bytes.Length||frames==0||gce)Bad();return new(new((uint)width,(uint)height),first! with{SourceFrames=frames},firstEnd,firstData,firstPalette);}
            if(kind==0x21)
            {
                if(old)Unsupported();Need(bytes,at,1);byte label=bytes[at++];
                if(label==0xf9)
                {
                    Need(bytes,at,6);if(gce||bytes[at]!=4||bytes[at+5]!=0)Bad();byte flags=bytes[at+1];
                    if((flags&0xe0)!=0)Bad();if((flags&2)!=0||((flags>>2)&7)>3)Unsupported();gce=true;transparent=(flags&1)!=0?bytes[at+4]:-1;at+=6;
                }
                else if(label==0xfe)SkipSubBlocks(bytes,ref at,ref subblocks,cancellation,false);
                else if(label==0xff)
                {
                    Need(bytes,at,17);if(loop||bytes[at]!=11)Unsupported();var name=bytes.Slice(at+1,11);
                    if(!name.SequenceEqual("NETSCAPE2.0"u8)&&!name.SequenceEqual("ANIMEXTS1.0"u8))Unsupported();
                    if(bytes[at+12]!=3||bytes[at+13]!=1||bytes[at+16]!=0)Bad();loop=true;at+=17;
                }
                else Unsupported();
                continue;
            }
            if(kind!=0x2c)Bad();if(++frames>FrameLimit)Unsupported();Need(bytes,at,9);
            int left=U16(bytes[at..]),top=U16(bytes[(at+2)..]),w=U16(bytes[(at+4)..]),h=U16(bytes[(at+6)..]);byte packed=bytes[at+8];at+=9;
            if(w==0||h==0||(long)left+w>width||(long)top+h>height||(packed&0x18)!=0)Bad();
            int palette=(packed&128)!=0?2<<(packed&7):global;if(palette==0)Bad();if((packed&128)!=0)Take(bytes,ref at,palette*3);
            if(transparent>=palette)Bad();Need(bytes,at,1);int dataOffset=at;int minimum=bytes[at++];if(minimum is <2 or >8)Bad();
            SkipSubBlocks(bytes,ref at,ref subblocks,cancellation,true);
            if(frames==1){first=new(1,left,top,w,h);firstEnd=at;firstData=dataOffset;firstPalette=palette;}
            gce=false;transparent=-1;
        }
        Bad();return null!;
    }
    private static void SkipSubBlocks(ReadOnlySpan<byte> bytes,ref int at,ref int blocks,CancellationToken cancellation,bool requireData)
    {
        bool data=false;
        while(true){cancellation.ThrowIfCancellationRequested();if(++blocks>SubBlockLimit)Unsupported();Need(bytes,at,1);int n=bytes[at++];if(n==0){if(requireData&&!data)Bad();return;}Take(bytes,ref at,n);data=true;}
    }
    private static int U16(ReadOnlySpan<byte> b)=>BinaryPrimitives.ReadUInt16LittleEndian(b);
    private static void Need(ReadOnlySpan<byte> bytes,int at,int count){if(at<0||count<0||(long)at+count>bytes.Length)Bad();}
    private static void Take(ReadOnlySpan<byte> bytes,ref int at,int count){Need(bytes,at,count);at=checked(at+count);}
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
    private static void Unsupported()=>throw new ImageJobException(ImageFailure.UnsupportedImage);
}
