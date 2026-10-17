using System.Diagnostics;
namespace KuaiYi.Core;
public interface IOwnedProcessStarter { Process Start(ProcessStartInfo info); }
public sealed class SystemOwnedProcessStarter : IOwnedProcessStarter
{
    public static SystemOwnedProcessStarter Instance {get;}=new();
    private SystemOwnedProcessStarter(){}
    public Process Start(ProcessStartInfo info)=>Process.Start(info)??throw new IOException("Owned child start failed.");
}
