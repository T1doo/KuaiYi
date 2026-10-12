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
        OleStartupStage stage=OleStartupStage.Entry;
        try
        {
            OleStartupDiagnostics.Emit(stage);
            stage=OleStartupStage.Apartment;OleStartupDiagnostics.Emit(stage);
            using var apartment=new OleApartment();var dispatcher=Dispatcher.CurrentDispatcher;
            Task<int> run=Task.Run(()=>OleReceiverServer.RunAsync(Console.OpenStandardInput(),Console.OpenStandardOutput(),Create,diagnostic:OleStartupDiagnostics.Emit));
            _ =run.ContinueWith(_=>dispatcher.BeginInvokeShutdown(DispatcherPriority.Send),TaskScheduler.Default);
            stage=OleStartupStage.Dispatcher;OleStartupDiagnostics.Emit(stage);
            Dispatcher.Run();return run.GetAwaiter().GetResult();
            async Task<IOleReceiverBackend> Create(OleReceiverHello hello,Action<IpcFrame> send)=>await dispatcher.InvokeAsync(()=>new ReceiverHost(hello,send));
        }
        catch(Exception error){OleStartupDiagnostics.Emit(stage,error);return 3;}
    }
}
