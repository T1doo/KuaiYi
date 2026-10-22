namespace KuaiYi.Imaging;

/// <summary>Browser MIME is a declaration, never decode authority. Inspect the original
/// bounded bytes at both receiving commits; dimensions describe the raw source canvas.</summary>
public static class BrowserEncodedImagePolicy
{
    public static string Mime(EncodedRasterFormat format)=>format switch
    {
        EncodedRasterFormat.Png=>"image/png",EncodedRasterFormat.Jpeg=>"image/jpeg",
        EncodedRasterFormat.Bmp=>"image/bmp",EncodedRasterFormat.Webp=>"image/webp",
        EncodedRasterFormat.Gif=>"image/gif",_=>throw new ImageJobException(ImageFailure.UnsupportedImage)
    };
    public static ImageSize Inspect(ReadOnlySpan<byte> bytes,string declaredMime,CancellationToken cancellation=default)
    {
        cancellation.ThrowIfCancellationRequested();
        var source=ImageDecodePolicy.Inspect(bytes,cancellation);
        if(Mime(source.Format)!=declaredMime)throw new ImageJobException(ImageFailure.InvalidImage);
        cancellation.ThrowIfCancellationRequested();return source.Size;
    }
}
