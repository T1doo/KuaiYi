using System.ComponentModel;
namespace KuaiYi.App;

// Explicit WPF-only call ports. ClosingShell itself is linked from production unchanged.
// No HWND/STA/Dispatcher behavior is claimed by this selected synthetic child.
internal sealed partial class ShellWindow(Func<Task> settle,Action started,Action<Exception> failed,Action closed)
{
    private bool opening=false,exiting=false,allowClose=false,exitRequested=false;
    private SettingsPort? settings=null;
    private readonly ButtonPort button=new();
    private readonly DispatcherPort Dispatcher=new();
    private readonly LifecyclePort lifecycle=new();
    internal bool ExitRequested=>exitRequested;
    internal bool Exiting=>exiting;
    internal void Close(){var e=new CancelEventArgs();ClosingShell(this,e);if(!e.Cancel)closed();}
    private void QueuePositionSave(){ /* No settings storage is substituted for an exit receipt. */ }
    private static void PrepareSettingsExit(Action request)=>request();
    private async Task SettleExitContent(List<Exception>? admissionErrors=null){started();await Task.Yield();await settle();if(admissionErrors is {Count:>0})throw new AggregateException(admissionErrors);}
    private void Report(string _,Exception error)=>failed(error);
    private Task<bool> DispatchCloseAttempt()=>Task.Run(TryCloseAttemptAsync);
    private Task CommitShellCloseAsync()=>Dispatcher.InvokeAsync(Close);
    private sealed class SettingsPort{internal void RequestCloseWhenSettled()=>throw new InvalidOperationException("Settings UI is outside this fixture.");}
    private sealed class ButtonPort{internal bool IsEnabled{get;set;}=true;}
    private sealed class LifecyclePort{private readonly KuaiYi.Core.RuntimeGate gate=new();internal void Hide()=>gate.Disable(hide:true);}
    private sealed class DispatcherPort{internal Task InvokeAsync(Action action)=>Task.Run(action);}
}
