using System.Security.Cryptography;
using KuaiYi.Imaging;
namespace KuaiYi.ImageWorker;

internal static class ImageRasterPreparer
{
    // Decode owns source; this method never consumes or mutates it. Only destination ownership transfers.
    internal static DecodedRaster Prepare(ReadOnlyMemory<byte> source,ImageDecodeJob job,EncodedRasterFormat format,ImagePreparedInfo prepared,CancellationToken cancellation,Action<byte[]>? allocated=null,GifFirstFrameInfo? gifInfo=null)
    {
        if(job.PrepareLongEdge is not {} limit)throw new InvalidDataException("Preparation options required.");
        var transform=ImageTransform.PlanEncoded(new((uint)prepared.SourceWidth,(uint)prepared.SourceHeight),prepared.SourceOrientation,limit);
        int width=(int)transform.NormalizedSize.Width,height=(int)transform.NormalizedSize.Height;
        byte[]? output=AreaWhiteRaster.Prepare(source,transform,cancellation,allocated);
        try
        {
            cancellation.ThrowIfCancellationRequested();var metadata=new ImageDecodeReply(job,ImageFailure.None,format,width,height,width*4,output.Length,ImageDecodePolicy.Hash(output),"BGRA8888-opaque-white",1,1,prepared,gifInfo);
            var raster=new DecodedRaster(metadata,output);output=null;return raster;
        }
        finally{if(output!=null)CryptographicOperations.ZeroMemory(output);}
    }
}
