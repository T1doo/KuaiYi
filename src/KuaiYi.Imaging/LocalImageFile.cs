using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
namespace KuaiYi.Imaging;

public static class LocalImageFile
{
    public static Task<byte[]> ReadPngAsync(string path,CancellationToken cancellation=default)=>ReadAsync(path,0,cancellation);
    public static Task<byte[]> ReadGifAsync(string path,CancellationToken cancellation=default)=>ReadAsync(path,1,cancellation);
    public static Task<byte[]> ReadEncodedAsync(string path,CancellationToken cancellation=default)=>ReadAsync(path,2,cancellation);
    private static async Task<byte[]> ReadAsync(string path,int kind,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows()) throw new ImageJobException(ImageFailure.UnsupportedPlatform);
        // No UNC, device namespace, ADS, drive-relative path or remote URI. Final opened target checked again.
        if (path.Length < 3 || !char.IsAsciiLetter(path[0]) || path[1] != ':' || path[2] != '\\' || path.AsSpan(2).Contains(':')) throw new ImageJobException(ImageFailure.InvalidImage);
        if (GetDriveTypeW(path[..3]) is not (2 or 3 or 5 or 6)) throw new ImageJobException(ImageFailure.InvalidImage);
        using SafeFileHandle handle = CreateFileW(path, 0x80000000, 1, 0, 3, 0x40000000 | 0x00200000 | 0x08000000, 0);
        if (handle.IsInvalid || GetFileType(handle) != 1 || !GetFileInformationByHandle(handle, out var info) || (info.Attributes & (0x10u | 0x400u)) != 0) throw new ImageJobException(ImageFailure.InvalidImage);
        var finalPath = new StringBuilder(32768);
        uint length = GetFinalPathNameByHandleW(handle, finalPath, (uint)finalPath.Capacity, 0);
        string resolved = finalPath.ToString();
        if (length is 0 or >= 32768 || resolved.Length < 7 || !resolved.StartsWith("\\\\?\\", StringComparison.Ordinal) || !char.IsAsciiLetter(resolved[4]) || resolved[5] != ':' || resolved[6] != '\\') throw new ImageJobException(ImageFailure.InvalidImage);
        if (GetDriveTypeW(resolved.Substring(4,3)) is not (2 or 3 or 5 or 6)) throw new ImageJobException(ImageFailure.InvalidImage);
        ulong size = ((ulong)info.SizeHigh << 32) | info.SizeLow;
        cancellation.ThrowIfCancellationRequested();
        if (size == 0 || size > ImagePolicy.ByteLimit) throw new ImageJobException(ImageFailure.ImageTooLarge);
        using var stream = new FileStream(handle, FileAccess.Read, 4096, isAsync: true);
        byte[] bytes = new byte[(int)size];
        try { await stream.ReadExactlyAsync(bytes, cancellation); if(kind==1)_=GifDecodePolicy.Inspect(bytes,cancellation);else if(kind==2)_=ImageDecodePolicy.Inspect(bytes,cancellation);else ImagePolicy.InspectPng(bytes); return bytes; }
        catch { System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes); throw; }
    }
    [StructLayout(LayoutKind.Sequential)] private struct Information { public uint Attributes; public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write; public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)] private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint creation, uint flags, nint template);
    [DllImport("kernel32.dll", ExactSpelling = true)] private static extern uint GetFileType(SafeFileHandle handle);
    [DllImport("kernel32.dll", ExactSpelling = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Information info);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, StringBuilder path, uint length, uint flags);
    [DllImport("kernel32.dll", ExactSpelling = true, CharSet = CharSet.Unicode)] private static extern uint GetDriveTypeW(string root);
}
