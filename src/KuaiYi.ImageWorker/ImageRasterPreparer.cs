using System.Security.Cryptography;
using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;

internal static class ImageRasterPreparer
{
    // Decode owns source; this method never consumes or mutates it. Only destination ownership transfers.
    internal static DecodedRaster Prepare(ReadOnlyMemory<byte> source,ImageDecodeJob job,EncodedRasterFormat format,ImagePreparedInfo prepared,CancellationToken cancellation,Action<byte[]>? allocated=null)
    {
        if(job.PrepareLongEdge is not {} limit)throw new InvalidDataException("Preparation options required.");
        var transform=ImageTransform.PlanEncoded(new((uint)prepared.SourceWidth,(uint)prepared.SourceHeight),prepared.SourceOrientation,limit);
        if(source.Length!=ImageDecodePolicy.RasterBytes(prepared.SourceWidth,prepared.SourceHeight))throw new InvalidDataException("Source raster length mismatch.");
        cancellation.ThrowIfCancellationRequested();int width=(int)transform.NormalizedSize.Width,height=(int)transform.NormalizedSize.Height,ow=(int)transform.OrientedSize.Width,oh=(int)transform.OrientedSize.Height;
        byte[]? output=new byte[ImageDecodePolicy.RasterBytes(width,height)];
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
                        int index=SourceIndex(sx,sy,prepared);var pixel=source.Span.Slice(index,4);long alpha=pixel[3],white=255*(255-alpha);
                        blue+=(pixel[0]*alpha+white)*weight;green+=(pixel[1]*alpha+white)*weight;red+=(pixel[2]*alpha+white)*weight;
                    }
                    int to=(y*width+x)*4;output[to]=(byte)((blue+denominator/2)/denominator);output[to+1]=(byte)((green+denominator/2)/denominator);output[to+2]=(byte)((red+denominator/2)/denominator);output[to+3]=255;
                }
            }
            cancellation.ThrowIfCancellationRequested();var metadata=new ImageDecodeReply(job,ImageFailure.None,format,width,height,width*4,output.Length,ImageDecodePolicy.Hash(output),"BGRA8888-opaque-white",1,1,prepared);
            var raster=new DecodedRaster(metadata,output);output=null;return raster;
        }
        finally{if(output!=null)CryptographicOperations.ZeroMemory(output);}
    }
    private static int SourceIndex(int x,int y,ImagePreparedInfo p)
    {
        int w=p.SourceWidth,h=p.SourceHeight;var (sx,sy)=p.SourceOrientation switch
        {ImageOrientation.ExifTopRight=>(w-1-x,y),ImageOrientation.ExifBottomRight=>(w-1-x,h-1-y),ImageOrientation.ExifBottomLeft=>(x,h-1-y),ImageOrientation.ExifLeftTop=>(y,x),ImageOrientation.ExifRightTop=>(y,h-1-x),ImageOrientation.ExifRightBottom=>(w-1-y,h-1-x),ImageOrientation.ExifLeftBottom=>(w-1-y,x),_=>(x,y)};
        return checked((sy*w+sx)*4);
    }
}
