using System.Security.Cryptography;
namespace KuaiYi.Imaging;
public sealed partial class ImageWorkerClient
{
    internal Action<byte[]>? FileAllocationForTest{get;init;}
    public Task<OwnedFileImage> PrepareFileAsync(ReadOnlyMemory<byte> encoded,ImageDecodeJob job,CancellationToken cancellation=default)
    {
        if(job.PrepareLongEdge!=null)throw new InvalidDataException("File preparation requires a raw source job.");
        return DecodeCoreAsync(encoded,job with{PrepareLongEdge=ImageTransform.LongEdgeLimit},cancellation,(raster,token)=>Task.Run(()=>
        {
            byte[]? copy=null,pngCopy=null;ScreenshotPng? png=null;PreparedImagePng? request=null;OwnedFileImage? owner=null;
            try
            {
                token.ThrowIfCancellationRequested();request=ImageRequestPngEncoder.Encode(raster,4*1024*1024,token);
                copy=new byte[raster.Bytes.Length];for(int at=0;at<copy.Length;){token.ThrowIfCancellationRequested();int n=Math.Min(65536,copy.Length-at);raster.Bytes.Span.Slice(at,n).CopyTo(copy.AsSpan(at,n));at+=n;}
                FileAllocationForTest?.Invoke(copy);token.ThrowIfCancellationRequested();pngCopy=request.Bytes.ToArray();FileAllocationForTest?.Invoke(pngCopy);token.ThrowIfCancellationRequested();
                png=new(pngCopy);pngCopy=null;var m=raster.Metadata;
                owner=new(new(m.Format!.Value,m.Job.ByteLength,m.Job.Sha256,raster.Transform!,m.Job.PrepareLongEdge!.Value,m.GifInfo),copy,png);copy=null;png=null;
                token.ThrowIfCancellationRequested();var result=owner;owner=null;return result;
            }
            finally{owner?.Dispose();request?.Dispose();png?.Dispose();if(copy!=null)CryptographicOperations.ZeroMemory(copy);if(pngCopy!=null)CryptographicOperations.ZeroMemory(pngCopy);}
        }),totalDeadline:true);
    }
}
