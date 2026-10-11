using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
[assembly: DefaultDllImportSearchPaths(DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.System32)]
namespace KuaiYi.Imaging;

public static class NativeOcr
{
    private static ImageFailure Map(int result) => result switch { 0 => ImageFailure.None, 1 => ImageFailure.IdentityMissing, 2 => ImageFailure.LanguageUnavailable, 3 => ImageFailure.InvalidImage, 4 => ImageFailure.ImageTooLarge, 5 => ImageFailure.OcrDimensionExceeded, 6 => ImageFailure.Cancelled, _ => ImageFailure.NativeFailure };
    public static WorkerDiagnostic Diagnose()
    {
        var identity = PackageIdentity.Read("ImageWorker");
        if (identity.Failure != ImageFailure.None) return new(identity, [], 0, identity.Failure);
        try
        {
            int result = KY_OcrDiagnose(out var pointer, out int length);
            try
            {
                if (result != 0) return new(identity, [], 0, Map(result));
                var capabilities = Read<Capabilities>(pointer, length);
                if (capabilities.Languages == null || capabilities.MaxImageDimension == 0 || capabilities.Languages.Any(tag => string.IsNullOrWhiteSpace(tag) || tag.Length > 64))
                    return new(identity, [], 0, ImageFailure.NativeFailure);
                return new(identity, capabilities.Languages, capabilities.MaxImageDimension, ImageFailure.None);
            }
            finally { KY_OcrFree(pointer, length); }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { return new(identity, [], 0, ImageFailure.NativeUnavailable); }
        catch (Exception e) when (e is ImageJobException or JsonException or InvalidDataException) { return new(identity, [], 0, ImageFailure.NativeFailure); }
    }
    public static OcrOutput Recognize(ReadOnlyMemory<byte> png, string language, CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        ImagePolicy.InspectPng(png.Span);
        var diagnostic = Diagnose();
        OcrCapabilitiesPolicy.Require(diagnostic, language, ImagePolicy.InspectPng(png.Span));
        using var stop = new EventWaitHandle(false, EventResetMode.ManualReset);
        using var registration = cancellation.Register(() => stop.Set());
        byte[] input = png.ToArray();
        try
        {
            int status = KY_OcrRecognize(input, input.Length, language, stop.SafeWaitHandle, out var pointer, out int length);
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (status != 0) throw new ImageJobException(Map(status));
                return Read<OcrOutput>(pointer, length);
            }
            finally { KY_OcrFree(pointer, length); }
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException) { throw new ImageJobException(ImageFailure.NativeUnavailable); }
        catch (Exception e) when (e is JsonException or InvalidDataException) { throw new ImageJobException(ImageFailure.NativeFailure); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(input); }
    }
    private sealed record Capabilities(string[] Languages, uint MaxImageDimension);
    private static T Read<T>(nint pointer, int length)
    {
        if (pointer == 0 || length is <= 0 or > ImagePolicy.ResultLimit) throw new ImageJobException(ImageFailure.NativeFailure);
        byte[] bytes = new byte[length];
        try { Marshal.Copy(pointer, bytes, 0, length); return ImageJson.Read<T>(bytes); }
        finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); }
    }
    [DllImport("KuaiYi.OcrNative.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] private static extern int KY_OcrDiagnose(out nint bytes, out int length);
    [DllImport("KuaiYi.OcrNative.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern int KY_OcrRecognize(byte[] bytes, int length, string language, SafeWaitHandle stop, out nint output, out int outputLength);
    [DllImport("KuaiYi.OcrNative.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)] private static extern void KY_OcrFree(nint bytes, int length);
}
