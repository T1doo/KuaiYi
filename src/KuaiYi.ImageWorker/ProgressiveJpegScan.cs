using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;

// Conservative 8-bit Huffman SOF2 subset. This verifies complete entropy/progression before native decode.
// It keeps nonzero AC masks, not reconstructed pixels. Unsupported limits never grant native permission.
internal static class ProgressiveJpegScan
{
    internal const int ScanLimit=64,SegmentLimit=1024,MaskByteLimit=16*1024*1024;
    internal const long WorkLimit=256_000_000;
    private sealed class Component(byte id,int h,int v,int quantization)
    {
        internal readonly byte Id=id;internal readonly int H=h,V=v,Quantization=quantization;
        internal readonly int[] Precision=Enumerable.Repeat(-1,64).ToArray();
        internal int Columns,Rows,Stride,PaddedRows,Predictor;internal ulong[] Nonzero=[];
    }
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
            foreach(byte value in values)if(ac?(value&15)>10:value>11)Bad();
        }
        internal int Read(ref Bits bits)
        {
            int code=0;
            for(int length=1;length<=16;length++){code=(code<<1)|bits.Bit();if(code>=minimum[length]&&code<=maximum[length])return values[start[length]+code-minimum[length]];}
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
                if(current==255&&(position>=bytes.Length||bytes[position++]!=0))Bad();remaining=8;
            }
            return(current>>--remaining)&1;
        }
        internal int Read(int count){int value=0;for(int i=0;i<count;i++)value=(value<<1)|Bit();return value;}
        internal int Extend(int count){if(count==0)return 0;int value=Read(count);return value<(1<<(count-1))?value-((1<<count)-1):value;}
        internal int Marker()
        {
            if(remaining>0&&(current&((1<<remaining)-1))!=(1<<remaining)-1)Bad();remaining=0;
            if(position>=bytes.Length||bytes[position++]!=255)Bad();while(position<bytes.Length&&bytes[position]==255)position++;
            if(position>=bytes.Length)Bad();return bytes[position++];
        }
    }
    private sealed record ScanComponent(Component Component,Huffman? Dc,Huffman? Ac);
    // Dispatch before interpreting any DHT: progressive AC tables may legally precede SOF2.
    internal static bool AppliesTo(ReadOnlySpan<byte> bytes,CancellationToken cancellation)
    {
        int offset=2,segments=0;
        while(offset<bytes.Length)
        {
            cancellation.ThrowIfCancellationRequested();if(bytes[offset++]!=255)Bad();while(offset<bytes.Length&&bytes[offset]==255)offset++;
            if(offset>=bytes.Length)Bad();int marker=bytes[offset++];if(++segments>SegmentLimit)return false; // No SOF2 grant; old baseline validator retains its original contract.
            if(marker is 192 or 193 or 194 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207)return marker==194;
            if(marker==218)return false;if(bytes.Length-offset<2)Bad();int length=BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);if(length<2||length>bytes.Length-offset)Bad();offset+=length;
        }
        Bad();return false;
    }
    internal static void Validate(ReadOnlySpan<byte> bytes,ImageSize size,CancellationToken cancellation,Action<ulong[]>? maskAllocatedForTest=null,Action? scanCompletedForTest=null)
    {
        List<Component> components=[];var dc=new Huffman?[4];var ac=new Huffman?[4];var quantization=new bool[4];
        int offset=2,restart=0,scans=0,segments=0,columns=0,rows=0;long work=0;int? pending=null;
        try
        {
            if(bytes.Length<4||bytes[0]!=255||bytes[1]!=216)Bad();ImagePolicy.ValidateDimensions(size.Width,size.Height);
            while(pending!=null||offset<bytes.Length)
            {
                cancellation.ThrowIfCancellationRequested();int marker;
                if(pending is {} saved){marker=saved;pending=null;}
                else
                {
                    if(bytes[offset++]!=255)Bad();while(offset<bytes.Length&&bytes[offset]==255)offset++;
                    if(offset>=bytes.Length)Bad();marker=bytes[offset++];
                }
                if(marker==217)
                {
                    if(offset!=bytes.Length||scans==0||components.Count==0||components.Any(c=>c.Precision.Any(p=>p!=0)))Bad();return;
                }
                if(marker is 0 or 216 or >=208 and <=215)Bad();
                if(++segments>SegmentLimit)Unsupported();if(bytes.Length-offset<2)Bad();int length=BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
                if(length<2||length>bytes.Length-offset)Bad();var segment=bytes.Slice(offset+2,length-2);offset+=length;
                if(marker==194)
                {
                    if(components.Count!=0||segment.Length<6)Bad();if(segment[0]!=8)Unsupported();int count=segment[5];if(count is not(1 or 3))Unsupported();
                    if(segment.Length!=6+3*count||BinaryPrimitives.ReadUInt16BigEndian(segment[1..])!=size.Height||BinaryPrimitives.ReadUInt16BigEndian(segment[3..])!=size.Width)Bad();
                    for(int i=0;i<count;i++)
                    {
                        int at=6+i*3,h=segment[at+1]>>4,v=segment[at+1]&15;if(h is <1 or >2||v is <1 or >2||segment[at+2]>3)Unsupported();
                        byte id=segment[at];if(components.Any(c=>c.Id==id))Bad();components.Add(new(id,h,v,segment[at+2]));
                    }
                    if(count==1&&(components[0].H!=1||components[0].V!=1)||components.Sum(c=>c.H*c.V)>10)Unsupported();
                    int maxH=components.Max(c=>c.H),maxV=components.Max(c=>c.V);columns=checked(((int)size.Width+8*maxH-1)/(8*maxH));rows=checked(((int)size.Height+8*maxV-1)/(8*maxV));long maskBytes=0;
                    foreach(var c in components)
                    {
                        c.Columns=checked(((int)size.Width*c.H+8*maxH-1)/(8*maxH));c.Rows=checked(((int)size.Height*c.V+8*maxV-1)/(8*maxV));
                        c.Stride=checked(columns*c.H);c.PaddedRows=checked(rows*c.V);maskBytes=checked(maskBytes+checked((long)c.Stride*c.PaddedRows*8));
                    }
                    if(maskBytes>MaskByteLimit)Unsupported();
                    foreach(var c in components){cancellation.ThrowIfCancellationRequested();c.Nonzero=new ulong[checked(c.Stride*c.PaddedRows)];maskAllocatedForTest?.Invoke(c.Nonzero);}
                }
                else if(marker is 192 or 193 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207 or 204)Unsupported();
                else if(marker==196)
                {
                    if(segment.IsEmpty)Bad();int at=0;
                    while(at<segment.Length)
                    {
                        cancellation.ThrowIfCancellationRequested();if(segment.Length-at<17)Bad();int kind=segment[at]>>4,id=segment[at++]&15;if(kind>1||id>3)Bad();
                        var counts=segment.Slice(at,16);at+=16;int count=0;foreach(byte n in counts)count+=n;if(count>256||count>segment.Length-at)Bad();
                        var table=new Huffman(counts,segment.Slice(at,count),kind==1);at+=count;if(kind==0)dc[id]=table;else ac[id]=table;
                    }
                }
                else if(marker==219)
                {
                    if(scans!=0)Unsupported();if(segment.IsEmpty)Bad();int at=0;
                    while(at<segment.Length)
                    {
                        cancellation.ThrowIfCancellationRequested();int precision=segment[at]>>4,id=segment[at++]&15;if(id>3)Bad();if(precision!=0)Unsupported();if(segment.Length-at<64)Bad();
                        for(int i=0;i<64;i++)if(segment[at+i]==0)Bad();at+=64;quantization[id]=true;
                    }
                }
                else if(marker==221){if(segment.Length!=2)Bad();restart=BinaryPrimitives.ReadUInt16BigEndian(segment);}
                else if(marker==218)
                {
                    if(++scans>ScanLimit)Unsupported();if(components.Count==0||segment.Length<6)Bad();int count=segment[0];if(count<1||count>components.Count||segment.Length!=4+2*count)Bad();
                    int ss=segment[^3],se=segment[^2],ah=segment[^1]>>4,al=segment[^1]&15;
                    if(ss>se||se>63||ss==0&&se!=0||ss!=0&&count!=1||ah!=0&&al!=ah-1)Bad();if(ah>10||al>10)Unsupported();
                    List<ScanComponent> selected=[];
                    for(int i=0;i<count;i++)
                    {
                        byte id=segment[1+2*i];var c=components.FirstOrDefault(c=>c.Id==id);int d=segment[2+2*i]>>4,a=segment[2+2*i]&15;
                        if(c==null||selected.Any(s=>s.Component.Id==id)||d>3||a>3)Bad();if(!quantization[c!.Quantization]||ss!=0&&c.Precision[0]<0)Bad();
                        for(int k=ss;k<=se;k++)if(ah==0?c.Precision[k]!=-1:c.Precision[k]!=ah)Bad();
                        if(ss==0&&ah==0&&dc[d]==null||ss!=0&&ac[a]==null)Bad();selected.Add(new(c,dc[d],ac[a]));c.Predictor=0;
                    }
                    long blocks=count==1?checked((long)selected[0].Component.Columns*selected[0].Component.Rows):checked((long)columns*rows*selected.Sum(s=>s.Component.H*s.Component.V));
                    work=checked(work+checked(blocks*(se-ss+1)));if(work>WorkLimit)Unsupported();
                    var bits=new Bits(bytes,offset);int eob=0,nextRestart=0;int mcus=count==1?checked(selected[0].Component.Columns*selected[0].Component.Rows):checked(columns*rows);
                    for(int mcu=0;mcu<mcus;mcu++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        foreach(var s in selected)
                        {
                            var c=s.Component;int blockCount=count==1?1:c.H*c.V;
                            for(int b=0;b<blockCount;b++)
                            {
                                int index=count==1?(mcu/c.Columns)*c.Stride+mcu%c.Columns:((mcu/columns)*c.V+b/c.H)*c.Stride+(mcu%columns)*c.H+b%c.H;
                                if(ss==0)
                                {
                                    if(ah==0)
                                    {
                                        int difference=bits.Extend(s.Dc!.Read(ref bits));c.Predictor=checked(c.Predictor+difference);long coefficient=(long)c.Predictor<<al;
                                        if(coefficient is <-1024 or >1023)Bad();
                                    }
                                    else _=bits.Bit();
                                }
                                else if(ah==0)InitialAc(ref bits,c,index,s.Ac!,ss,se,al,ref eob);
                                else RefineAc(ref bits,c,index,s.Ac!,ss,se,ref eob);
                            }
                        }
                        if(restart>0&&(mcu+1)%restart==0&&mcu+1<mcus)
                        {
                            if(eob!=0||bits.Marker()!=208+nextRestart)Bad();nextRestart=(nextRestart+1)&7;foreach(var s in selected)s.Component.Predictor=0;
                        }
                    }
                    if(eob!=0)Bad();pending=bits.Marker();offset=bits.Position;
                    foreach(var s in selected)for(int k=ss;k<=se;k++)s.Component.Precision[k]=al;scanCompletedForTest?.Invoke();
                }
                else if(marker is not(254 or >=224 and <=239))Unsupported();
            }
            Bad();
        }
        finally{foreach(var c in components)CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(c.Nonzero.AsSpan()));}
    }
    private static void InitialAc(ref Bits bits,Component c,int index,Huffman table,int ss,int se,int al,ref int eob)
    {
        if(eob>0){eob--;return;}int k=ss;
        while(k<=se)
        {
            int symbol=table.Read(ref bits),run=symbol>>4,size=symbol&15;
            if(size==0)
            {
                if(run==15){k+=16;if(k>se+1)Bad();continue;}
                eob=(1<<run)+bits.Read(run)-1;return;
            }
            if(size+al>10)Bad();k+=run;if(k>se)Bad();int value=bits.Extend(size);if(value==0)Bad();c.Nonzero[index]|=1UL<<k;k++;
        }
    }
    private static void RefineAc(ref Bits bits,Component c,int index,Huffman table,int ss,int se,ref int eob)
    {
        int k=ss;
        if(eob==0)
        {
            while(k<=se)
            {
                int symbol=table.Read(ref bits),run=symbol>>4,size=symbol&15;if(size>1)Bad();
                if(size==0&&run!=15){eob=(1<<run)+bits.Read(run);break;}
                if(size==1)_=bits.Bit(); // Sign of a newly nonzero coefficient; only its nonzero mask is needed.
                while(k<=se)
                {
                    if((c.Nonzero[index]&(1UL<<k))!=0)_=bits.Bit();
                    else if(--run<0)break;
                    k++;
                }
                if(k>se)Bad();if(size==1)c.Nonzero[index]|=1UL<<k;k++;
            }
        }
        if(eob>0)
        {if(c.Nonzero[index]!=0)for(;k<=se;k++)if((c.Nonzero[index]&(1UL<<k))!=0)_=bits.Bit();eob--;}
    }
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
    private static void Unsupported()=>throw new ImageJobException(ImageFailure.UnsupportedImage);
}
