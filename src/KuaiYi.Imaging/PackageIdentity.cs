using System.Runtime.InteropServices;
using System.Text;
namespace KuaiYi.Imaging;

public sealed record IdentityDiagnostic(bool Windows, bool Present, string? FullName, string? ApplicationUserModelId, ImageFailure Failure);
public static class PackageIdentity
{
    public const string DevelopmentName = "KuaiYi.Development";
    public const string DevelopmentPublisher = "CN=KuaiYi Development";
    public static IdentityDiagnostic Read(string applicationId)
    {
        if (!OperatingSystem.IsWindows()) return new(false, false, null, null, ImageFailure.UnsupportedPlatform);
        uint count = 0;
        int error = GetCurrentPackageFullName(ref count, null);
        if (error == 15700) return new(true, false, null, null, ImageFailure.IdentityMissing);
        if (error != 122 || count is 0 or > 4096) return new(true, false, null, null, ImageFailure.NativeFailure);
        var fullName = new StringBuilder((int)count);
        if (GetCurrentPackageFullName(ref count, fullName) != 0) return new(true, false, null, null, ImageFailure.NativeFailure);
        count = 0;
        error = GetCurrentApplicationUserModelId(ref count, null);
        if (error != 122 || count is 0 or > 4096) return new(true, true, fullName.ToString(), null, ImageFailure.IdentityMismatch);
        var aumid = new StringBuilder((int)count);
        if (GetCurrentApplicationUserModelId(ref count, aumid) != 0) return new(true, true, fullName.ToString(), null, ImageFailure.IdentityMismatch);
        string full = fullName.ToString(), id = aumid.ToString();
        string? publisher = ReadPublisher();
        // Runtime names are diagnostic evidence, never inferred from parent process identity.
        bool match = publisher == DevelopmentPublisher && full.StartsWith(DevelopmentName + "_", StringComparison.Ordinal) &&
            id.StartsWith(DevelopmentName + "_", StringComparison.Ordinal) && id.EndsWith("!" + applicationId, StringComparison.Ordinal);
        return new(true, true, full, id, match ? ImageFailure.None : ImageFailure.IdentityMismatch);
    }
    private static string? ReadPublisher()
    {
        uint count = 0;
        if (GetCurrentPackageId(ref count, 0) != 122 || count is 0 or > 16384) return null;
        nint buffer = Marshal.AllocHGlobal((int)count);
        try
        {
            if (GetCurrentPackageId(ref count, buffer) != 0) return null;
            var package = Marshal.PtrToStructure<PackageId>(buffer);
            return Marshal.PtrToStringUni(package.Publisher);
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PackageId { public uint Reserved, Architecture; public ulong Version; public nint Name, Publisher, ResourceId, PublisherId; }
    [DllImport("kernel32.dll")] private static extern int GetCurrentPackageId(ref uint length, nint id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, StringBuilder? name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentApplicationUserModelId(ref uint length, StringBuilder? id);
}
