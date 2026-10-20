using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;

/// <summary>Validate first-frame symbols/count/EOI with a fixed4096-entry dictionary, without a second raster.</summary>
internal static class GifFirstFrameLzw
{
    internal static void Validate(ReadOnlySpan<byte> bytes,GifInspection gif,CancellationToken cancellation)
    {
        var reader=new Bits(bytes.Slice(gif.FirstDataOffset+1,gif.FirstImageEnd-gif.FirstDataOffset-1));int minimum=bytes[gif.FirstDataOffset],clear=1<<minimum,end=clear+1,next=end+1,width=minimum+1,previous=-1;long produced=0,target=(long)gif.FirstFrame.Width*gif.FirstFrame.Height;
        Span<int> lengths=stackalloc int[4096];Span<int> maximum=stackalloc int[4096];
        for(int i=0;i<clear;i++){lengths[i]=1;maximum[i]=i;}
        if(reader.Read(width)!=clear)Bad();
        while(true)
        {
            cancellation.ThrowIfCancellationRequested();int code=reader.Read(width);if(code<0)Bad();
            if(code==clear){next=end+1;width=minimum+1;previous=-1;continue;}
            if(code==end){if(produced!=target||!reader.ExactZeroPadding())Bad();return;}
            int length,max;
            if(code<clear){length=1;max=code;}
            else if(code<next&&code>end){length=lengths[code];max=maximum[code];}
            else if(code==next&&previous>=0&&next<4096){length=checked(lengths[previous]+1);max=maximum[previous];}
            else{Bad();return;}
            if(max>=gif.PaletteEntries||length<=0||(produced+=length)>target)Bad();
            if(previous>=0&&next<4096)
            {
                lengths[next]=checked(lengths[previous]+1);maximum[next]=Math.Max(maximum[previous],max); // Conservative: max includes the entire current expansion.
                // Exact palette validity of current was already checked, so this upper bound is sufficient.
                next++;if(next==(1<<width)&&width<12)width++;
            }
            previous=code;
        }
    }
    private static void Bad()=>throw new ImageJobException(ImageFailure.InvalidImage);
    private ref struct Bits(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> data=data;private int at,remaining,count;private uint buffer;
        internal int Read(int width)
        {
            while(count<width)
            {
                if(remaining==0){if(at>=data.Length)return -1;remaining=data[at++];if(remaining==0)return -1;}
                if(at>=data.Length)return -1;buffer|=(uint)data[at++]<<count;remaining--;count+=8;
            }
            int value=(int)(buffer&((1u<<width)-1));buffer>>=width;count-=width;return value;
        }
        internal bool ExactZeroPadding()=>count<8&&buffer==0&&remaining==0&&at==data.Length-1&&data[at]==0;
    }
}
