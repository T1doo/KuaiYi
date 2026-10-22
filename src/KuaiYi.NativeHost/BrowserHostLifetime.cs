using KuaiYi.Core.Installation;
namespace KuaiYi.NativeHost;

internal sealed class BrowserHostLifetime
{
    private readonly InstallationBrowserHostEntry entry;
    private BrowserHostLifetime(InstallationBrowserHostEntry entry){this.entry=entry;}
    internal static BrowserHostLifetime Enter(string executableDirectory)=>new(InstallationBrowserHostEntry.Enter(executableDirectory));
    internal void RequireCurrent()=>entry.RequireCurrent();
}
