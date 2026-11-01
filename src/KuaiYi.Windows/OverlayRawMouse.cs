using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using System.Windows.Interop;
using KuaiYi.Core;
namespace KuaiYi.Windows;
/// <summary>App owns the sole mouse Raw Input registration. No keyboard registration, trajectory history or OLE inference.</summary>
public sealed class OverlayRawMouse:IAsyncDisposable
{
    [StructLayout(LayoutKind.Sequential)]private struct Device{public ushort Page,Usage;public uint Flags;public nint Hwnd;}
    [StructLayout(LayoutKind.Sequential)]private struct Header{public uint Type,Size;public nint Device,Parameter;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseDown{public NativeDesktop.Point Point;public uint Data,Flags,Time;public nuint Extra;}
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]private delegate nint MouseHook(int code,nint message,nint data);
    [DllImport("user32.dll",ExactSpelling=true,SetLastError=true)]private static extern nint SetWindowsHookExW(int kind,MouseHook callback,nint module,uint thread);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]private static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("kernel32.dll",ExactSpelling=true,CharSet=CharSet.Unicode)]private static extern nint GetModuleHandleW(string? module);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool RegisterRawInputDevices(Device[] devices,uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRegisteredRawInputDevices([Out]Device[]? devices,ref uint count,uint size);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint GetRawInputData(nint raw,uint command,[Out]byte[] data,ref uint bytes,uint headerSize);
    [DllImport("user32.dll")]private static extern nint WindowFromPoint(NativeDesktop.Point point);
    [DllImport("user32.dll")]private static extern nint GetAncestor(nint hwnd,uint flags);
    [StructLayout(LayoutKind.Sequential)]private struct Message{public nint Window;public uint Code;public nuint W;public nint L;public uint Time;public NativeDesktop.Point Point;public uint Private;}
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",SetLastError=true)]private static extern int GetMessageW(out Message message,nint window,uint first,uint last);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PeekMessageW(out Message message,nint window,uint first,uint last,uint remove);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PostThreadMessageW(uint thread,uint message,nuint w,nint l);
    [DllImport("user32.dll")]private static extern int GetMessageTime();
    private readonly record struct Sample(ushort Flags,PixelPoint Point,bool Hit,long Time,long Revision,long Capture);
    private readonly Channel<Sample> queue=Channel.CreateBounded<Sample>(new BoundedChannelOptions(64){SingleWriter=false,SingleReader=true,FullMode=BoundedChannelFullMode.Wait,AllowSynchronousContinuations=false});
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously),exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly OverlayMouseSequence sequence=new();private Task delivery=Task.CompletedTask;private readonly nint target;
    private readonly OverlayMouseTracking tracking=new();
    private int stopping;private long revision;private uint pumpId;private string? failureCode;private bool rawReleased,rawRegistered,hookAdded,started;
    private readonly HwndSource source;private readonly Action<ushort,PixelPoint,bool> input;private readonly Action<string> failure;private readonly Func<PixelPoint,bool> gestureHit;private readonly HwndSourceHook hook;
    private volatile bool disposed,failed;
    private readonly MouseHook downHook;private nint downHandle;private GCHandle downRoot;
    public Task Readiness=>Task.WhenAny(ready.Task,exited.Task);
    public bool IsReady=>!disposed&&!failed&&ready.Task.IsCompletedSuccessfully&&Volatile.Read(ref stopping)==0;
    public OverlayRawMouse(HwndSource source,Action<ushort,PixelPoint,bool> input,Action<string> failure,Func<PixelPoint,bool> gestureHit)
    {
        this.source=source;target=source.Handle;this.input=input;this.failure=failure;this.gestureHit=gestureHit;hook=Hook;downHook=FreshDown;
    }
    public void Start()
    {
        if(started)throw new InvalidOperationException("Original mouse owner is one-shot.");started=true;
        // Caller has retained this owner before any native side effect.
        try
        {
            if(Registered().Any(d=>d.Page==1&&d.Usage==2))throw new InvalidOperationException("App mouse Raw Input already has an owner.");
            if(!RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=0x100,Hwnd=target}],1,(uint)Marshal.SizeOf<Device>()))throw new Win32Exception(Marshal.GetLastWin32Error());
            rawRegistered=true;source.AddHook(hook);hookAdded=true;
            delivery=DeliverAsync();
            var thread=new Thread(Pump){IsBackground=true,Name="KuaiYi overlay physical-down pump"};thread.SetApartmentState(ApartmentState.MTA);thread.Start();
        }
        catch(Exception error){SignalFailure(error.GetType().Name);exited.TrySetResult();throw;}
    }
    private void Pump()
    {
        Exception? cleanupError=null;
        try
        {
            pumpId=GetCurrentThreadId();PeekMessageW(out _,0,0,0,0);downRoot=GCHandle.Alloc(downHook);
            downHandle=SetWindowsHookExW(14,downHook,GetModuleHandleW(null),0);
            if(downHandle==0)throw new Win32Exception(Marshal.GetLastWin32Error());ready.TrySetResult();
            while(Volatile.Read(ref stopping)==0){int result=GetMessageW(out _,0,0,0);if(result==0){if(Volatile.Read(ref stopping)==0)SignalFailure("MousePumpUnexpectedExit");break;}if(result<0)throw new Win32Exception(Marshal.GetLastWin32Error());}
        }
        catch(Exception caught){SignalFailure(caught.GetType().Name);}
        finally
        {
            Interlocked.Exchange(ref stopping,1);
            if(downHandle!=0&&!UnhookWindowsHookEx(downHandle)){cleanupError=new Win32Exception(Marshal.GetLastWin32Error());SignalFailure("MouseUnhookUnconfirmed");} // Keep the inactive delegate rooted on UNKNOWN.
            else{downHandle=0;if(downRoot.IsAllocated)downRoot.Free();}
            queue.Writer.TryComplete();if(cleanupError==null)exited.TrySetResult();else exited.TrySetException(cleanupError);
        }
    }
    private static long Tick(uint time){long now=Environment.TickCount64;return now-unchecked((uint)now-time);}
    private void Offer(Sample sample)
    {
        // Movement is replaceable by the final up position; reserve four slots for physical edges.
        if(sample.Flags==0&&queue.Reader.Count>=60)return;
        if(!queue.Writer.TryWrite(sample))SignalFailure("MouseInputOverflow");
    }
    private async Task DeliverAsync()
    {
        try
        {
            await foreach(var sample in queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                if(failed||Volatile.Read(ref stopping)!=0)continue;
                await source.Dispatcher.InvokeAsync(()=>
                {
                    if(failed||disposed||Volatile.Read(ref stopping)!=0||source.IsDisposed||source.Handle!=target||sample.Revision!=Volatile.Read(ref revision))return;
                    if(!sequence.Accept(sample.Flags,sample.Hit&&gestureHit(sample.Point),sample.Time,Environment.TickCount64,sample.Capture)){if(sequence.OrderUnconfirmed)SignalFailure("MouseCaptureOrderUnknown");return;}
                    input(sample.Flags,sample.Point,sample.Hit);
                }).Task.ConfigureAwait(false);
            }
        }
        catch(Exception error){SignalFailure(error.GetType().Name);}
        if(failureCode is {} code&&!source.Dispatcher.HasShutdownStarted)
            await source.Dispatcher.InvokeAsync(()=>failure(code)).Task.ConfigureAwait(false);
    }
    private nint FreshDown(int code,nint message,nint data)
    {
        try
        {
            if(code>=0&&!disposed&&!failed&&Volatile.Read(ref stopping)==0&&(message==0x201||message==0x204))
            {
                var down=Marshal.PtrToStructure<MouseDown>(data);
                if((down.Flags&3)==0) // Only physical mouse scope; injected events have no matching Raw Input guarantee.
                {
                    var point=new PixelPoint(down.Point.X,down.Point.Y);bool hit=GetAncestor(WindowFromPoint(down.Point),2)==target;
                    if(hit){long time=Tick(down.Time),capture=message==0x201?tracking.Begin(time):0;Offer(new(message==0x201?(ushort)1:(ushort)4,point,true,time,Volatile.Read(ref revision),capture));} // Original down coordinates; never sample the later cursor for identity.
                }
            }
        }
        catch(Exception error){SignalFailure(error.GetType().Name);}
        return CallNextHookEx(downHandle,code,message,data); // Do not consume or rewrite the external source's event.
    }
    private void SignalFailure(string code)
    {
        failed=true;Interlocked.CompareExchange(ref failureCode,code,null);queue.Writer.TryComplete();
        Interlocked.Exchange(ref stopping,1);if(pumpId!=0)PostThreadMessageW(pumpId,0x12,0,0);
    }
    private static Device[] Registered()
    {
        uint count=0,size=(uint)Marshal.SizeOf<Device>();if(GetRegisteredRawInputDevices(null,ref count,size)==uint.MaxValue||count>256)throw new Win32Exception(Marshal.GetLastWin32Error());
        var devices=new Device[count];if(count!=0&&GetRegisteredRawInputDevices(devices,ref count,size)==uint.MaxValue)throw new Win32Exception(Marshal.GetLastWin32Error());return devices;
    }
    private nint Hook(nint hwnd,int message,nint wparam,nint lparam,ref bool handled)
    {
        if(message!=0xff||disposed||failed||!tracking.Active)return 0;byte[] bytes=new byte[64];
        try
        {
            uint count=64,header=(uint)Marshal.SizeOf<Header>();uint read=GetRawInputData(lparam,0x10000003,bytes,ref count,header);
            if(read==uint.MaxValue||read<header+24||read>64)throw new Win32Exception(Marshal.GetLastWin32Error());
            if(BitConverter.ToUInt32(bytes)!=0)return 0; // Mouse only. Do not decode keyboard/HID.
            ushort flags=(ushort)(BitConverter.ToUInt16(bytes,(int)header+4)&~5);var point=ScreenshotDesktop.Cursor(); // Down identity comes only from event-coordinate hook; Raw Input handles movement/up.
            bool actualHit=GetAncestor(WindowFromPoint(new(){X=point.X,Y=point.Y}),2)==source.Handle;
            long time=Tick(unchecked((uint)GetMessageTime()));
            if(tracking.Read(time,(flags&2)!=0,out long capture,out bool ambiguous))Offer(new(flags,point,actualHit,time,Volatile.Read(ref revision),capture));
            else if(ambiguous)SignalFailure("MouseEdgeOrderUnknown");
            // Leave WM_INPUT unhandled so DefWindowProc performs required foreground raw-input cleanup.
        }
        catch(Exception error){SignalFailure(error.GetType().Name);}
        finally{CryptographicOperations.ZeroMemory(bytes);}
        return 0;
    }
    public void CancelGesture(){Interlocked.Increment(ref revision);tracking.Cancel();sequence.Cancel();}
    public async ValueTask DisposeAsync()
    {
        if(disposed)return;failed=true;Interlocked.Exchange(ref stopping,1);List<Exception> errors=[];
        // Every original resource gets its own settlement even when another close is unconfirmed.
        try
        {
            if(rawRegistered)
            {
                if(!Registered().Any(d=>d.Page==1&&d.Usage==2&&d.Hwnd==target))throw new InvalidOperationException("Original mouse registration changed; cleanup unconfirmed.");
                if(!RegisterRawInputDevices([new(){Page=1,Usage=2,Flags=1,Hwnd=0}],1,(uint)Marshal.SizeOf<Device>()))throw new Win32Exception(Marshal.GetLastWin32Error());
                rawRegistered=false;
            }
            rawReleased=true;
        }
        catch(Exception error){errors.Add(error);}
        try{if(hookAdded){source.RemoveHook(hook);hookAdded=false;}}catch(Exception error){errors.Add(error);}
        try{if(pumpId!=0&&!exited.Task.IsCompleted&&!PostThreadMessageW(pumpId,0x12,0,0)&&!exited.Task.IsCompleted)throw new Win32Exception(Marshal.GetLastWin32Error());}catch(Exception error){errors.Add(error);}
        try{if(started)await exited.Task.WaitAsync(TimeSpan.FromSeconds(2));}catch(Exception error){errors.Add(error);}
        queue.Writer.TryComplete();
        try{await delivery.WaitAsync(TimeSpan.FromSeconds(2));}catch(Exception error){errors.Add(error);}
        GC.KeepAlive(downHook);
        if(errors.Count>0)throw new AggregateException("Original overlay input cleanup unconfirmed.",errors);
        disposed=rawReleased&&!hookAdded;
    }
}
