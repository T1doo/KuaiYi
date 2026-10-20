namespace KuaiYi.Imaging;

public sealed partial class ImageWorkerClient
{
    internal Func<DecodedRaster,int,CancellationToken,Task<PreparedImagePng>>? EncodePreparedForTest {get;init;}
    public Task<PreparedImagePng> PreparePngAsync(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,int maximumBytes=4*1024*1024,int maximumLongEdge=ImageTransform.LongEdgeLimit,CancellationToken cancellation=default)
    {
        ImageRequestPngEncoder.ValidateLimit(maximumBytes);
        if(job.PrepareLongEdge!=null)throw new InvalidDataException("PNG preparation requires a raw source job.");
        return DecodeCoreAsync(encoded,job with{PrepareLongEdge=maximumLongEdge},cancellation,async(raster,token)=>
        {
            // Await the same original CPU task even when cancellation/deadline occurs.
            // RunOnce retains its raster and active state until this task and all cleanup settle.
            var request=await (EncodePreparedForTest?.Invoke(raster,maximumBytes,token)??Task.Run(()=>ImageRequestPngEncoder.Encode(raster,maximumBytes,token))).ConfigureAwait(false);
            try
            {
                if(request.SourceMetadata!=raster.Metadata||request.MaximumBytes!=maximumBytes)throw new InvalidDataException("PNG projection identity/budget mismatch.");
                token.ThrowIfCancellationRequested();return request;
            }
            catch{request.Dispose();throw;}
        },totalDeadline:true);
    }
}
