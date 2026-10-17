using System.Collections.Immutable;
using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.Core.Diagnostics;
using KuaiYi.Providers;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private readonly DiagnosticLog diagnostics=new(typeof(ShellWindow).Assembly.GetName().Version??new Version(0,0));
    private (Guid Request,long Started)? diagnosticRequest;
    private DiagnosticEnvironment ReadDiagnosticEnvironment()
    {
        var monitors=MonitorTopology.Read().Monitors.Select(m=>new DiagnosticMonitor(m.WorkArea.Left,m.WorkArea.Top,m.WorkArea.Width,m.WorkArea.Height,m.Dpi.Factor*96,m.Dpi.Factor*96)).ToImmutableArray();
        static DiagnosticComponentVersion Component(DiagnosticComponent name,Type type)=>new(name,type.Assembly.GetName().Version??new Version(0,0));
        // Reading the report never launches a probe, capture, model request or package operation.
        return new(DiagnosticOs.Windows,Environment.OSVersion.Version,monitors,
            [Component(DiagnosticComponent.App,typeof(ShellWindow)),Component(DiagnosticComponent.Core,typeof(RuntimeGate)),Component(DiagnosticComponent.Providers,typeof(ProviderClient)),Component(DiagnosticComponent.NativeHost,typeof(KuaiYi.NativeHost.SelectionBrokerClient)),Component(DiagnosticComponent.Imaging,typeof(KuaiYi.Imaging.ImageWorkerClient))],
            Enum.GetValues<DiagnosticCapability>().Select(c=>new DiagnosticCapabilityResult(c,DiagnosticCapabilityState.NotRun)).ToImmutableArray());
    }
    private void ObserveDiagnosticTranslation(TextTranslationUpdate update)
    {
        if(update.Presentation.Kind==OverlayResultEventKind.Begin)
        {diagnosticRequest=(update.Identity.RequestId,Stopwatch.GetTimestamp());diagnostics.Record(DiagnosticEvent.TranslationStarted,requestId:update.Identity.RequestId);}
        else if(update.Presentation.Kind is OverlayResultEventKind.Complete or OverlayResultEventKind.Stopped)
        {
            long? elapsed=diagnosticRequest is {} start&&start.Request==update.Identity.RequestId?(long)Stopwatch.GetElapsedTime(start.Started).TotalMilliseconds:null;
            diagnostics.Record(DiagnosticEvent.TranslationFinished,update.Failure==null?DiagnosticError.None:DiagnosticError.Provider,update.Identity.RequestId,elapsedMilliseconds:elapsed);diagnosticRequest=null;
        }
        TranslationChanged(update);
    }
}
