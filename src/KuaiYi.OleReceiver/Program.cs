using System.Windows.Threading;
using KuaiYi.Contracts;
using KuaiYi.OleTransfer;
using KuaiYi.Windows;
namespace KuaiYi.OleReceiver;
internal static class Program
{
    [STAThread]public static int Main(string[] args)
    {
        if(args.Length!=1||args[0]!="--private-stdio"||!OperatingSystem.IsWindows())return 2;
        using var apartment=new OleApartment();var dispatcher=Dispatcher.CurrentDispatcher;
        Task<int> run=Task.Run(()=>OleReceiverServer.RunAsync(Console.OpenStandardInput(),Console.OpenStandardOutput(),Create));
        _ =run.ContinueWith(_=>dispatcher.BeginInvokeShutdown(DispatcherPriority.Send),TaskScheduler.Default);
        Dispatcher.Run();return run.GetAwaiter().GetResult(); // Receiver STA only, after its pump exits; never App presentation.
        async Task<IOleReceiverBackend> Create(OleReceiverHello hello,Action<IpcFrame> send)=>await dispatcher.InvokeAsync(()=>new ReceiverHost(hello,send));
    }
}
