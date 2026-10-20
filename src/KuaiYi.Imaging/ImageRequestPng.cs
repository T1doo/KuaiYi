namespace KuaiYi.Imaging;

/// <summary>Only the bounded encoder constructs this owner. Numeric source mapping is local metadata.</summary>
public sealed class PreparedImagePng:IDisposable
{
    private ScreenshotPng? png;
    public ImageDecodeReply SourceMetadata {get;}
    public ImageTransform Transform {get;}
    public int MaximumBytes {get;}
    public int ByteLength {get;}
    public string Sha256 {get;}
    public string ContentType=>"image/png";
    public ReadOnlyMemory<byte> Bytes=>(png??throw new ObjectDisposedException(nameof(PreparedImagePng))).Bytes;
    internal PreparedImagePng(ImageDecodeReply source,ScreenshotPng owned,int maximumBytes)
    {
        try
        {
            ImageRequestPngEncoder.ValidateLimit(maximumBytes);ImageDecodePolicy.ValidateReply(source);
            if(source.Failure!=ImageFailure.None||source.PreparedInfo==null||source.Job.PrepareLongEdge==null)throw new InvalidDataException("Prepared source required.");
            var size=ImagePolicy.InspectPng(owned.Bytes.Span);
            if(size.Width!=source.Width||size.Height!=source.Height||owned.Bytes.Length>maximumBytes)throw new InvalidDataException("Encoded request does not match its plan/budget.");
            SourceMetadata=source;Transform=ImageTransform.PlanEncoded(new((uint)source.PreparedInfo.SourceWidth,(uint)source.PreparedInfo.SourceHeight),source.PreparedInfo.SourceOrientation,source.Job.PrepareLongEdge.Value);
            MaximumBytes=maximumBytes;ByteLength=owned.Bytes.Length;Sha256=ImageDecodePolicy.Hash(owned.Bytes.Span);png=owned;
        }
        catch{owned.Dispose();throw;}
    }
    public void Dispose()=>Interlocked.Exchange(ref png,null)?.Dispose();
}

internal static class ImageRequestPngEncoder
{
    internal const int MinimumBytes=65536,MaximumBytes=4*1024*1024;
    internal static void ValidateLimit(int maximumBytes)
    {if(maximumBytes is <MinimumBytes or >MaximumBytes)throw new ArgumentOutOfRangeException(nameof(maximumBytes));}
    // Borrow the source under the caller's original-task lease. No resize, lossy fallback or retry.
    internal static PreparedImagePng Encode(DecodedRaster source,int maximumBytes,CancellationToken cancellation,Action<byte[]>? allocated=null)
    {
        ValidateLimit(maximumBytes);cancellation.ThrowIfCancellationRequested();var metadata=source.Metadata;ImageDecodePolicy.ValidateReply(metadata);
        if(metadata.PreparedInfo==null||metadata.Job.PrepareLongEdge==null)throw new InvalidDataException("Prepared source required.");
        var bytes=source.Bytes;
        if(bytes.Length!=metadata.ByteLength||ImageDecodePolicy.Hash(bytes.Span)!=metadata.Sha256)throw new InvalidDataException("Prepared source integrity mismatch.");
        for(int i=3;i<bytes.Length;i+=4){if((i&65535)==3)cancellation.ThrowIfCancellationRequested();if(bytes.Span[i]!=255)throw new InvalidDataException("Prepared source is not opaque.");}
        ScreenshotPng? png=null;PreparedImagePng? request=null;
        try
        {
            png=ScreenshotPngEncoder.EncodeBgra(bytes,metadata.Width,metadata.Height,cancellation,maximumBytes,allocated);
            cancellation.ThrowIfCancellationRequested();request=new(metadata,png,maximumBytes);png=null;cancellation.ThrowIfCancellationRequested();
            var delivery=request;request=null;return delivery;
        }
        catch(ScreenshotException error)when(error.Code==ScreenshotFailure.RegionTooLarge)
        {cancellation.ThrowIfCancellationRequested();throw new ScreenshotNormalizationException();}
        finally{try{request?.Dispose();}finally{png?.Dispose();}}
    }
}
