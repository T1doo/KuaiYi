using System.Diagnostics;
namespace KuaiYi.Imaging;
/// <summary>Production implementation starts the actual owned child. Tests may delay this boundary while still creating a real child.</summary>
public interface IImageProcessStarter : KuaiYi.Core.IOwnedProcessStarter { }
public sealed class SystemImageProcessStarter : IImageProcessStarter
{
    public static SystemImageProcessStarter Instance { get; } = new();
    private SystemImageProcessStarter() { }
    public Process Start(ProcessStartInfo info) => Process.Start(info) ?? throw new IOException("Worker start failed.");
}
