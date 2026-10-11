using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using KuaiYi.Core;
namespace KuaiYi.Windows;
/// <summary>Sole selection hook owns a dedicated pumping thread. Callback writes bounded down/up metadata only.</summary>
public sealed class SelectionMouseHook:IAsyncDisposable
{
    [StructLayout(LayoutKind.Sequential)]private struct MouseData{public NativeDesktop.Point Point;public uint Data,Flags,Time;public nuint Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct Message{public nint Window;public uint Code;public nuint W;public nint L;public uint Time;public NativeDesktop.Point Point;public uint Private;}
    private delegate nint HookCallback(int code,nint message,nint data);
    [DllImport("user32.dll",SetLastError=true)]private static extern nint SetWindowsHookExW(int id,HookCallback callback,nint module,uint thread);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")]private static extern nint CallNextHookEx(nint hook,int code,nint message,nint data);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern nint GetModuleHandleW(string? name);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll",SetLastError=true)]private static extern int GetMessageW(out Message message,nint window,uint first,uint last);
    [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PeekMessageW(out Message message,nint window,uint first,uint last,uint remove);
    [DllImport("user32.dll",SetLastError=true)][return:MarshalAs(UnmanagedType.Bool)]private static extern bool PostThreadMessageW(uint thread,uint message,nuint w,nint l);
    private readonly Channel<SelectionRawInput> queue=Channel.CreateBounded<SelectionRawInput>(new BoundedChannelOptions(64){SingleWriter=true,SingleReader=true,FullMode=BoundedChannelFullMode.Wait,AllowSynchronousContinuations=false});
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously),exited=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly HookCallback callback;private readonly Thread thread;private nint handle;private uint threadId;private long lost;private int stopping;
    public SelectionInputIdentity Identity{get;}
    public Task Ready=>ready.Task;public ChannelReader<SelectionRawInput> Events=>queue.Reader;public long LossRevision=>Interlocked.Read(ref lost);
    public bool IsReleased=>exited.Task.IsCompleted&&handle==0;
    public bool IsFaulted=>exited.Task.IsFaulted;
    public SelectionMouseHook(SelectionInputIdentity identity){Identity=identity;callback=OnHook;thread=new(Run){IsBackground=true,Name="KuaiYi selection mouse pump"};thread.SetApartmentState(ApartmentState.MTA);thread.Start();}
    private nint OnHook(int code,nint message,nint data)
    {
        if(code>=0&&Volatile.Read(ref stopping)==0&&(message==0x201||message==0x202))
        {
            var value=Marshal.PtrToStructure<MouseData>(data);
            long now=Environment.TickCount64,time=now-unchecked((uint)now-value.Time);nint foreground=NativeDesktop.GetForegroundWindow();NativeDesktop.GetWindowThreadProcessId(foreground,out uint pid);
            var record=new SelectionRawInput(message==0x201,new(value.Point.X,value.Point.Y),time,(value.Flags&3)!=0,foreground.ToInt64(),pid,DateTime.UtcNow.Ticks-(now-time)*TimeSpan.TicksPerMillisecond,LossRevision,Identity);
            if(!queue.Writer.TryWrite(record))Interlocked.Increment(ref lost);
        }
        return CallNextHookEx(handle,code,message,data);
    }
    private void Run()
    {
        Exception? failure=null;
        try
        {
            threadId=GetCurrentThreadId();PeekMessageW(out _,0,0,0,0);handle=SetWindowsHookExW(14,callback,GetModuleHandleW(null),0);
            if(handle==0)throw new Win32Exception(Marshal.GetLastWin32Error());ready.TrySetResult();
            while(Volatile.Read(ref stopping)==0){int result=GetMessageW(out _,0,0,0);if(result==0)break;if(result<0)throw new Win32Exception(Marshal.GetLastWin32Error());}
        }
        catch(Exception error){failure=error;ready.TrySetException(error);}
        finally
        {
            Interlocked.Exchange(ref stopping,1);
            if(handle!=0&&!UnhookWindowsHookEx(handle)){failure=new Win32Exception(Marshal.GetLastWin32Error());GCHandle.Alloc(callback); /* Failed native registration stays rooted and inactive. */}
            else handle=0;
            queue.Writer.TryComplete(failure);if(failure==null)exited.TrySetResult();else exited.TrySetException(failure);
        }
    }
    public void Stop(){Interlocked.Exchange(ref stopping,1);if(threadId!=0&&ready.Task.IsCompletedSuccessfully&&!exited.Task.IsCompleted&&!PostThreadMessageW(threadId,0x12,0,0)&&!exited.Task.IsCompleted)throw new Win32Exception(Marshal.GetLastWin32Error());}
    public async ValueTask DisposeAsync(){try{await ready.Task.ConfigureAwait(false);}catch{await exited.Task.ConfigureAwait(false);throw;}Stop();await exited.Task.ConfigureAwait(false);GC.KeepAlive(callback);}
}
