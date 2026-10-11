using KuaiYi.Core.Settings;
namespace KuaiYi.Core;
public sealed record SelectionInputDelivery(bool Admitted,Task Capture);
/// <summary>The same production bridge validates channel input before resolution and again on the UI destination.</summary>
public sealed class SelectionInputBridge(RuntimeGate gate,Func<SettingsSnapshot?> settings,Func<Guid?> currentHook,
    Func<SelectionRawInput,SelectionWindow?> resolve,Func<SelectionWindow,bool> sourceCurrent,
    SelectionCaptureCoordinator coordinator,SynchronizationContext destination)
{
    private static readonly SelectionInputDelivery Rejected=new(false,Task.CompletedTask);
    public bool IsCurrent(SelectionRawInput input)=>input.Foreground!=0&&input.ProcessId!=0&&input.CapturedUtcTicks>0&&input.Identity.HookId!=Guid.Empty&&currentHook()==input.Identity.HookId&&input.Identity.SettingsVersion==settings()?.Version&&input.Identity.Ticket.Mode==TriggerMode.Selection&&gate.IsCaptureCurrent(input.Identity.Ticket);
    public Task<SelectionInputDelivery> ReceiveAsync(SelectionRawInput input)
    {
        if(!IsCurrent(input))return Task.FromResult(Rejected);
        var source=input.Injected?null:resolve(input);
        if(!input.Injected&&(source==null||source.Handle!=input.Foreground||source.ProcessId!=input.ProcessId||source.ProcessStartUtcTicks>input.CapturedUtcTicks)||!IsCurrent(input))return Task.FromResult(Rejected);
        var completion=new TaskCompletionSource<SelectionInputDelivery>(TaskCreationOptions.RunContinuationsAsynchronously);
        try{destination.Post(_=>
        {
            try
            {
                if(!IsCurrent(input)){completion.TrySetResult(Rejected);return;}
                if(input.Injected){coordinator.CancelPending();completion.TrySetResult(Rejected);return;}
                if(!sourceCurrent(source!)){completion.TrySetResult(Rejected);return;}
                var capture=coordinator.InputAsync(new(input.Down,input.Point,input.TimeMs,false,source!,input.Identity));completion.TrySetResult(new(true,capture));
            }
            catch(Exception error){completion.TrySetException(error);}
        },null);}catch(Exception error){completion.TrySetException(error);}
        return completion.Task;
    }
}
