using System.Buffers.Binary;
using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;

// Structural entropy validation only; native Skia still performs the actual pixel decode.
// Baseline8-bit, one complete interleaved scan only. Other legal JPEG coding modes are unsupported.
// This detects premature EOI which libjpeg may repair while still reporting pixel decode Success.
internal static class BaselineJpegScan
{
    private sealed record Component(byte Id,int H,int V,int Quantization);
    private sealed class Huffman
    {
        private readonly int[] minimum=new int[17],maximum=Enumerable.Repeat(-1,17).ToArray(),start=new int[17];private readonly byte[] values;
        internal Huffman(ReadOnlySpan<byte> counts,ReadOnlySpan<byte> symbols,bool ac)
        {
            values=symbols.ToArray();int code=0,index=0;
            for(int bits=1;bits<=16;bits++)
            {
                int count=counts[bits-1];if(count>0&&code+count-1>=(1<<bits)-1)Bad();
                minimum[bits]=code;maximum[bits]=count==0?-1:code+count-1;start[bits]=index;index+=count;code=(code+count)<<1;
            }
            if(index!=values.Length||index==0)Bad();
            foreach(byte value in values)if(ac?(value!=0&&value!=240&&((value&15) is 0 or >10)):value>11)Bad();
        }
        internal int Read(ref Bits reader)
        {
            int code=0;
            for(int length=1;length<=16;length++)
            {code=(code<<1)|reader.Bit();if(code>=minimum[length]&&code<=maximum[length])return values[start[length]+code-minimum[length]];}
            Bad();return 0;
        }
    }
    private ref struct Bits(ReadOnlySpan<byte> source,int offset)
    {
        private readonly ReadOnlySpan<byte> bytes=source;private int position=offset,current,remaining;
        internal readonly int Position=>position;
        internal int Bit()
        {
            if(remaining==0)
            {
                if(position>=bytes.Length)Bad();current=bytes[position++];
                if(current==255){if(position>=bytes.Length||bytes[position++]!=0)Bad();}
                remaining=8;
            }
            return(current>>--remaining)&1;
        }
        internal void Skip(int count){for(int i=0;i<count;i++)_=Bit();}
        internal void Align(){if(remaining>0&&(current&((1<<remaining)-1))!=(1<<remaining)-1)Bad();remaining=0;}
        internal int Marker()
        {
            Align();if(position>=bytes.Length||bytes[position++]!=255)Bad();
            while(position<bytes.Length&&bytes[position]==255)position++;
            if(position>=bytes.Length)Bad();return bytes[position++];
        }
    }
    internal static void Validate(ReadOnlySpan<byte> bytes,ImageSize size,CancellationToken cancellation)
    {
        var dc=new Huffman?[4];var ac=new Huffman?[4];var quantization=new bool[4];List<Component> components=[];int offset=2,restart=0;
        while(offset<bytes.Length-2)
        {
            cancellation.ThrowIfCancellationRequested();if(bytes[offset++]!=255)Bad();while(offset<bytes.Length&&bytes[offset]==255)offset++;if(offset>=bytes.Length)Bad();int marker=bytes[offset++];
            if(bytes.Length-offset<2)Bad();int length=BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);if(length<2||length>bytes.Length-offset)Bad();var segment=bytes.Slice(offset+2,length-2);offset+=length;
            if(marker==192)
            {
                if(components.Count!=0||segment.Length<6)Bad();if(segment[0]!=8)Unsupported();int count=segment[5];if(count is not(1 or 3))Unsupported();
                if(segment.Length!=6+3*count||BinaryPrimitives.ReadUInt16BigEndian(segment[1..])!=size.Height||BinaryPrimitives.ReadUInt16BigEndian(segment[3..])!=size.Width)Bad();
                for(int i=0;i<count;i++)
                {
                    int at=6+i*3,h=segment[at+1]>>4,v=segment[at+1]&15;
                    if(h is <1 or >2||v is <1 or >2||segment[at+2]>3)Unsupported();byte componentId=segment[at];if(components.Any(c=>c.Id==componentId))Bad();components.Add(new(componentId,h,v,segment[at+2]));
                }
                if(count==1&&(components[0].H!=1||components[0].V!=1)||components.Sum(c=>c.H*c.V)>10)Unsupported();
            }
            else if(marker is 193 or 194 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207||marker==204)Unsupported();
            else if(marker==196)
            {
                int at=0;
                while(at<segment.Length)
                {
                    cancellation.ThrowIfCancellationRequested();if(segment.Length-at<17)Bad();int kind=segment[at]>>4,id=segment[at++]&15;if(kind>1||id>3)Bad();var counts=segment.Slice(at,16);at+=16;int count=0;foreach(byte n in counts)count+=n;
                    if(count>256||count>segment.Length-at)Bad();var table=new Huffman(counts,segment.Slice(at,count),kind==1);at+=count;if(kind==0)dc[id]=table;else ac[id]=table;
                }
            }
            else if(marker==219)
            {
                int at=0;
                while(at<segment.Length)
                {
                    cancellation.ThrowIfCancellationRequested();int precision=segment[at]>>4,id=segment[at++]&15;
                    if(id>3)Bad();if(precision!=0)Unsupported();if(segment.Length-at<64)Bad();
                    for(int i=0;i<64;i++)if(segment[at+i]==0)Bad();at+=64;quantization[id]=true;
                }
                if(segment.IsEmpty)Bad();
            }
            else if(marker==221){if(segment.Length!=2)Bad();restart=BinaryPrimitives.ReadUInt16BigEndian(segment);}
            else if(marker==218)
            {
                if(components.Count==0||segment.Length<4||components.Any(c=>!quantization[c.Quantization]))Bad();int count=segment[0];if(count!=components.Count)Unsupported();if(segment.Length!=4+2*count||segment[^3]!=0||segment[^2]!=63||segment[^1]!=0)Unsupported();
                List<(Component Component,Huffman Dc,Huffman Ac)> scan=[];
                for(int i=0;i<count;i++)
                {
                    byte id=segment[1+2*i];var component=components.FirstOrDefault(c=>c.Id==id);int d=segment[2+2*i]>>4,a=segment[2+2*i]&15;
                    if(component==null||scan.Any(c=>c.Component.Id==id)||d>3||a>3||dc[d]==null||ac[a]==null)Bad();scan.Add((component!,dc[d]!,ac[a]!));
                }
                int maxH=components.Max(c=>c.H),maxV=components.Max(c=>c.V);int columns=checked(((int)size.Width+8*maxH-1)/(8*maxH)),rows=checked(((int)size.Height+8*maxV-1)/(8*maxV));int total=checked(columns*rows),nextRestart=0;
                var bits=new Bits(bytes,offset);
                for(int mcu=0;mcu<total;mcu++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    foreach(var entry in scan)for(int block=0;block<entry.Component.H*entry.Component.V;block++)
                    {
                        bits.Skip(entry.Dc.Read(ref bits));int coefficient=1;
                        while(coefficient<64)
                        {
                            int symbol=entry.Ac.Read(ref bits);if(symbol==0)break;if(symbol==240){coefficient+=16;if(coefficient>64)Bad();continue;}
                            coefficient+=symbol>>4;if(coefficient>=64)Bad();bits.Skip(symbol&15);coefficient++;
                        }
                    }
                    if(restart>0&&(mcu+1)%restart==0&&mcu+1<total){if(bits.Marker()!=208+nextRestart)Bad();nextRestart=(nextRestart+1)&7;}
                }
                int end=bits.Marker();if(end==218)Unsupported();if(end!=217||bits.Position!=bytes.Length)Bad();return;
            }
            else if(marker is not(254 or >=224 and <=239))Unsupported();
        }
        Bad();
    }
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
    private static void Unsupported()=>throw new ImageJobException(ImageFailure.UnsupportedImage);
}
