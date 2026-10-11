using System.IO;
using System.Collections.Immutable;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
namespace KuaiYi.Windows;
/// <summary>Owns a message-only HWND on a dedicated STA. Only this window's IDs are registered, drained or revoked.</summary>
public sealed class HotkeyHost:ISettingsHotkeyParticipant,IAsyncDisposable
{
    private sealed record State(Dispatcher Dispatcher,HwndSource Window,HotkeyRegistry Registry);
    private readonly TaskCompletionSource<State> ready=new(TaskCreationOptions.RunContinuationsAsynchronously);private readonly TaskCompletionSource exited=new(TaskCreationOptions.RunContinuationsAsynchronously);private readonly object sync=new();private readonly HashSet<Task> originals=[];private bool closing;private State? current;private readonly Thread thread;
    private readonly RuntimeGate gate;public event Action<HotkeyInputDispatch>? Pressed;public event Action<string>? Failed;
    public HotkeyHost(RuntimeGate gate)
    {
        this.gate=gate??throw new ArgumentNullException(nameof(gate));thread=new Thread(()=>
        {
            HwndSource? window=null;
            try
            {
                var dispatcher=Dispatcher.CurrentDispatcher;window=new(new HwndSourceParameters("KuaiYi.Hotkeys"){ParentWindow=new(-3),WindowStyle=0,Width=0,Height=0});var registry=new HotkeyRegistry(new NativeRegistrar(window));var state=new State(dispatcher,window,registry);current=state;window.AddHook(Hook);ready.TrySetResult(state);Dispatcher.Run();
            }
            catch(Exception error){ready.TrySetException(error);ReportFailure(error.GetType().Name);}
            finally{try{window?.Dispose();}catch(Exception error){ReportFailure(error.GetType().Name);}finally{exited.TrySetResult();}}
        }){IsBackground=true,Name="KuaiYi global hotkey message host"};thread.SetApartmentState(ApartmentState.STA);thread.Start();
    }
    public string? LastFailure {get;private set;}
    private void ReportFailure(string code)
    {
        LastFailure=code;foreach(var handler in Failed?.GetInvocationList()??[])try{((Action<string>)handler)(code);}catch(Exception error){LastFailure="failureObserver:"+error.GetType().Name;}
    }
    private nint Hook(nint hwnd,int message,nint w,nint l,ref bool handled)
    {
        if(message!=0x312)return 0;handled=true;try
        {
            uint packed=unchecked((uint)l.ToInt64());if((packed&0xffff&~7u)!=0)return 0;var chord=new HotkeyChord((HotkeyModifiers)(packed&7),packed>>16);var signal=HotkeyInputAdmission.Resolve(current!.Registry,gate,w.ToInt32(),chord,unchecked((uint)NativeRegistrar.GetMessageTime()));if(signal!=null)Pressed?.Invoke(signal);
        }
        catch(Exception error){ReportFailure(error.GetType().Name);}return 0;
    }
    private Task<T> Invoke<T>(Func<State,T> apply,bool settlement=false)
    {
        lock(sync){if(closing&&!settlement)throw new ObjectDisposedException(nameof(HotkeyHost));var task=Run();originals.Add(task);_ =Observe(task);return task;}
        async Task<T> Run(){var state=await ready.Task.ConfigureAwait(false);return await state.Dispatcher.InvokeAsync(()=>apply(state)).Task.ConfigureAwait(false);}
    }
    private async Task Observe(Task task){try{await task.ConfigureAwait(false);}catch(Exception error){ReportFailure(error.GetType().Name);}finally{lock(sync)originals.Remove(task);}}
    public async Task<ISettingsHotkeyTransaction> PrepareAsync(IReadOnlyDictionary<string,string?> hotkeys,CancellationToken cancellation)
    {cancellation.ThrowIfCancellationRequested();var values=hotkeys.ToImmutableDictionary();var ticket=await Invoke(s=>{cancellation.ThrowIfCancellationRequested();return s.Registry.Prepare(values);}).ConfigureAwait(false);return new Prepared(this,ticket);}
    private sealed class Prepared(HotkeyHost owner,HotkeyPrepared ticket):ISettingsHotkeyTransaction
    {public async Task CommitAsync(SettingsSnapshot saved)=>await owner.Invoke(s=>{s.Registry.Commit(ticket,saved.Version);return true;},settlement:true).ConfigureAwait(false);public async Task RollbackAsync()=>await owner.Invoke(s=>{s.Registry.Rollback(ticket);return true;},settlement:true).ConfigureAwait(false);}
    public async Task RestoreAsync(SettingsSnapshot saved)=>await Invoke(s=>{if(!saved.ReadOnly&&!saved.RequiresRecovery)s.Registry.Restore(saved.Data.Hotkeys,saved.Version);return true;}).ConfigureAwait(false);
    public async Task ReconcileAsync(SettingsSnapshot saved)=>await Invoke(s=>{s.Registry.Reconcile(saved.Data.Hotkeys,saved.Version);return true;}).ConfigureAwait(false);
    public async Task RecoverAsync(string version)=>await Invoke(s=>{s.Registry.Recover(version);return true;}).ConfigureAwait(false);
    public async Task SuspendAsync(bool value)=>await Invoke(s=>{s.Registry.Suspend(value);return true;}).ConfigureAwait(false);
    public bool IsCurrent(HotkeyInputDispatch dispatch)=>!exited.Task.IsCompleted&&current is {} state&&HotkeyInputAdmission.Current(state.Registry,gate,dispatch);
    public IReadOnlyList<HotkeyStatus> Status=>exited.Task.IsCompleted?[]:current?.Registry.Status??[];
    public async ValueTask DisposeAsync()
    {
        Task[] outstanding;lock(sync){closing=true;outstanding=originals.ToArray();}try{await Task.WhenAll(outstanding).ConfigureAwait(false);}catch(Exception error){ReportFailure("originalSettled:"+error.GetType().Name);}
        if(exited.Task.IsCompleted){await exited.Task.ConfigureAwait(false);if(!thread.Join(TimeSpan.FromSeconds(2)))throw new IOException("Hotkey thread exit unconfirmed.");return;}
        State state;try{state=await ready.Task.ConfigureAwait(false);}catch{await exited.Task.ConfigureAwait(false);if(!thread.Join(TimeSpan.FromSeconds(2)))throw new IOException("Hotkey startup thread exit unconfirmed.");return;}
        await state.Dispatcher.InvokeAsync(()=>state.Registry.Close());lock(sync)closing=true;await state.Dispatcher.InvokeAsync(()=>state.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Send));await exited.Task.ConfigureAwait(false);if(!await Task.Run(()=>thread.Join(TimeSpan.FromSeconds(2))).ConfigureAwait(false))throw new IOException("Hotkey thread exit unconfirmed.");
    }
    private sealed class NativeRegistrar(HwndSource window):IHotkeyRegistrar
    {
        [StructLayout(LayoutKind.Sequential)]private struct Message{public nint Hwnd;public uint Id;public nint W,L;public uint Time;public int X,Y;public uint Private;}
        [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterHotKey(nint hwnd,int id,uint modifiers,uint key);
        [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnregisterHotKey(nint hwnd,int id);
        [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PeekMessageW(out Message message,nint hwnd,uint first,uint last,uint remove);
        [DllImport("user32.dll")]internal static extern int GetMessageTime();
        [DllImport("kernel32.dll")]private static extern ulong GetTickCount64();
        [DllImport("user32.dll")]private static extern void PostQuitMessage(int code);
        public HotkeyRegistrationResult Register(int id,HotkeyChord chord){window.Dispatcher.VerifyAccess();bool success=RegisterHotKey(window.Handle,id,(uint)chord.Modifiers|0x4000,chord.Key);return new(success,success?0:Marshal.GetLastPInvokeError());}
        public HotkeyRegistrationResult Unregister(int id){window.Dispatcher.VerifyAccess();bool success=UnregisterHotKey(window.Handle,id);return new(success,success?0:Marshal.GetLastPInvokeError());}
        public uint FenceInput(){window.Dispatcher.VerifyAccess();while(PeekMessageW(out var message,window.Handle,0x312,0x312,1)){if(message.Id==0x12){PostQuitMessage(message.W.ToInt32());throw new IOException("Hotkey message host is quitting.");}}return unchecked((uint)GetTickCount64());}
    }
}
