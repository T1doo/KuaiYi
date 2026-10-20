using System.Buffers.Binary;
namespace KuaiYi.Imaging;

public enum ImageFailure { None, UnsupportedPlatform, IdentityMissing, IdentityMismatch, NativeUnavailable, LanguageUnavailable, InvalidImage, ImageTooLarge, OcrDimensionExceeded, Cancelled, NativeFailure, ProtocolFailure, Timeout, ExitUnconfirmed, SettlementUnconfirmed, UnsupportedImage }
public sealed class ImageJobException(ImageFailure code,Exception? innerException=null) : Exception(code.ToString(),innerException) { public ImageFailure Code { get; } = code; }
public readonly record struct ImageSize(uint Width, uint Height);
public static class ImagePolicy
{
    public const int ByteLimit = 20 * 1024 * 1024;
    public const uint SideLimit = 16384;
    public const ulong PixelLimit = 24_000_000;
    public const int ResultLimit = 1024 * 1024;
    // P0 minimum accepts only genuine PNG header; complete SkiaSharp multi-format normalization belongs to P6.
    public static ImageSize InspectPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length > ByteLimit) throw new ImageJobException(ImageFailure.ImageTooLarge);
        if (bytes.Length < 33 || !bytes[..8].SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) ||
            BinaryPrimitives.ReadUInt32BigEndian(bytes[8..]) != 13 || !bytes.Slice(12,4).SequenceEqual("IHDR"u8))
            throw new ImageJobException(ImageFailure.InvalidImage);
        uint width = BinaryPrimitives.ReadUInt32BigEndian(bytes[16..]);
        uint height = BinaryPrimitives.ReadUInt32BigEndian(bytes[20..]);
        ValidateDimensions(width, height);
        return new(width, height);
    }
    public static void ValidateDimensions(uint width, uint height)
    {
        if (width == 0 || height == 0) throw new ImageJobException(ImageFailure.InvalidImage);
        if (width > SideLimit || height > SideLimit || (ulong)width * height > PixelLimit)
            throw new ImageJobException(ImageFailure.ImageTooLarge);
    }
    public static bool SupportedShellExtension(string extension) => extension.ToLowerInvariant() is ".png" or ".jpg" or ".jpeg" or ".webp" or ".bmp" or ".gif";
}
