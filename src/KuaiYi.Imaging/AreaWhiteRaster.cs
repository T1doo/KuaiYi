using System.Security.Cryptography;
namespace KuaiYi.Imaging;

/// <summary>Bounded area average of straight BGRA over white. Borrows source; transfers only a new owned destination.</summary>
internal static class AreaWhiteRaster
{
    internal static byte[] Prepare(ReadOnlyMemory<byte> source,ImageTransform transform,CancellationToken cancellation,Action<byte[]>? allocated=null)
    {
        if(source.Length!=checked((int)((long)transform.OriginalSize.Width*transform.OriginalSize.Height*4)))throw new InvalidDataException("Source raster length mismatch.");
        cancellation.ThrowIfCancellationRequested();int width=(int)transform.NormalizedSize.Width,height=(int)transform.NormalizedSize.Height,ow=(int)transform.OrientedSize.Width,oh=(int)transform.OrientedSize.Height;
        byte[]? output=new byte[checked(width*height*4)];
        try
        {
            allocated?.Invoke(output);int work=0;long denominator=checked((long)ow*oh*255);
            for(int y=0;y<height;y++)
            {
                cancellation.ThrowIfCancellationRequested();long top=(long)y*oh,bottom=(long)(y+1)*oh;int firstY=(int)(top/height),lastY=(int)((bottom-1)/height);
                for(int x=0;x<width;x++)
                {
                    long left=(long)x*ow,right=(long)(x+1)*ow,blue=0,green=0,red=0;int firstX=(int)(left/width),lastX=(int)((right-1)/width);
                    for(int sy=firstY;sy<=lastY;sy++)for(int sx=firstX;sx<=lastX;sx++)
                    {
                        if((work++&4095)==0)cancellation.ThrowIfCancellationRequested();
                        long weight=(Math.Min(right,(long)(sx+1)*width)-Math.Max(left,(long)sx*width))*(Math.Min(bottom,(long)(sy+1)*height)-Math.Max(top,(long)sy*height));
                        int index=SourceIndex(sx,sy,transform);var pixel=source.Span.Slice(index,4);long alpha=pixel[3],white=255*(255-alpha);
                        blue+=(pixel[0]*alpha+white)*weight;green+=(pixel[1]*alpha+white)*weight;red+=(pixel[2]*alpha+white)*weight;
                    }
                    int to=(y*width+x)*4;output[to]=(byte)((blue+denominator/2)/denominator);output[to+1]=(byte)((green+denominator/2)/denominator);output[to+2]=(byte)((red+denominator/2)/denominator);output[to+3]=255;
                }
            }
            cancellation.ThrowIfCancellationRequested();var delivery=output;output=null;return delivery;
        }
        finally{if(output!=null)CryptographicOperations.ZeroMemory(output);}
    }
    private static int SourceIndex(int x,int y,ImageTransform p)
    {
        int w=(int)p.OriginalSize.Width,h=(int)p.OriginalSize.Height;var (sx,sy)=p.Orientation switch
        {ImageOrientation.ExifTopRight=>(w-1-x,y),ImageOrientation.ExifBottomRight=>(w-1-x,h-1-y),ImageOrientation.ExifBottomLeft=>(x,h-1-y),ImageOrientation.ExifLeftTop=>(y,x),ImageOrientation.ExifRightTop=>(y,h-1-x),ImageOrientation.ExifRightBottom=>(w-1-y,h-1-x),ImageOrientation.ExifLeftBottom=>(w-1-y,x),_=>(x,y)};
        return checked((sy*w+sx)*4);
    }
}
