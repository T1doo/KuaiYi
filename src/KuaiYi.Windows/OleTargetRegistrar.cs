using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Threading;
using KuaiYi.OleTransfer;
namespace KuaiYi.Windows;
public enum OleRegistrarStage { UNKNOWN, Apartment, Import, Register, Pump, Revoke, Release, Uninitialize, Complete }
public sealed class OleTargetRegistrar(KuaiYi.Core.OleWindowOwnership ownership):IOleTargetRegistrar
{
    private readonly object sync=new();private Task? original;private Dispatcher? dispatcher;private volatile bool stopping;private bool revoked=true;private int started;
    public string? FailureCode{get;private set;}
    public int RegistrationThreadId{get;private set;}
    public uint NativeRegistrationThreadId{get;private set;}
    private int diagnosticStage,diagnosticFailureStage;
    public OleRegistrarStage DiagnosticStage=>(OleRegistrarStage)Volatile.Read(ref diagnosticStage);
    public OleRegistrarStage DiagnosticFailureStage=>(OleRegistrarStage)Volatile.Read(ref diagnosticFailureStage);
    public TaskStatus? DiagnosticTaskStatus{get{lock(sync)return original?.Status;}}
    private void At(OleRegistrarStage stage)=>Volatile.Write(ref diagnosticStage,(int)stage);
    private void Failed()=>Interlocked.CompareExchange(ref diagnosticFailureStage,Volatile.Read(ref diagnosticStage),0);
    private void Cleanup(OleRegistrarStage stage,Action work){At(stage);try{work();}catch{Failed();throw;}}
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint hwnd,out uint pid);
    public bool IsSettledAndRevoked{get{lock(sync)return original is not {IsCompleted:false}&&revoked;}}
    public Task RegisterAsync(long hwnd,byte[] ownedPacket,CancellationToken cancellation)
    {
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(Interlocked.Exchange(ref started,1)!=0){CryptographicOperations.ZeroMemory(ownedPacket);throw new InvalidOperationException("One registration per registrar.");}
        KuaiYi.Core.OleWindowOwnership.Lease reservation;
        try{reservation=ownership.Reserve(hwnd,done.Task);}
        catch{CryptographicOperations.ZeroMemory(ownedPacket);throw;}
        lock(sync){original=done.Task;revoked=false;}
        var thread=new Thread(()=>
        {
            var scope=new KuaiYi.Core.OleCleanupScope();
            try
            {
                cancellation.ThrowIfCancellationRequested();RegistrationThreadId=Environment.CurrentManagedThreadId;NativeRegistrationThreadId=NativeDesktop.GetCurrentThreadId();
                At(OleRegistrarStage.Apartment);var apartment=new OleApartment();scope.Own(()=>Cleanup(OleRegistrarStage.Uninitialize,apartment.Dispose));dispatcher=Dispatcher.CurrentDispatcher;
                if(GetWindowThreadProcessId((nint)hwnd,out uint process)==0||process!=Environment.ProcessId)throw new InvalidOperationException("Target HWND must belong to this App.");
                At(OleRegistrarStage.Import);nint target=OleTargetMarshal.Import(ownedPacket);scope.Own(()=>Cleanup(OleRegistrarStage.Release,()=>Marshal.Release(target)));
                At(OleRegistrarStage.Register);Marshal.ThrowExceptionForHR(OleTargetMarshal.RegisterPointer((nint)hwnd,target));scope.Own(()=>Cleanup(OleRegistrarStage.Revoke,()=>Marshal.ThrowExceptionForHR(OleNative.RevokeDragDrop((nint)hwnd))));
                CryptographicOperations.ZeroMemory(ownedPacket);
                if(stopping||cancellation.IsCancellationRequested)throw new OperationCanceledException();
                At(OleRegistrarStage.Pump);ready.SetResult();using var revoke=cancellation.Register(RequestStop);Dispatcher.Run();
            }
            catch(Exception error){Failed();FailureCode=error.GetType().Name;ready.TrySetException(error);}
            finally
            {
                CryptographicOperations.ZeroMemory(ownedPacket);
                try{scope.Dispose();lock(sync)revoked=scope.CleanupCompleted;At(OleRegistrarStage.Complete);}catch(Exception error){FailureCode??=error.GetType().Name;lock(sync)revoked=false;}
                reservation.ConfirmCleanup(revoked);done.SetResult();
            }
        }){IsBackground=true,Name="KuaiYi OLE registrar STA (no presentation)"};
        try{thread.SetApartmentState(ApartmentState.STA);thread.Start();}
        catch(Exception error){CryptographicOperations.ZeroMemory(ownedPacket);FailureCode=error.GetType().Name;lock(sync)revoked=true;reservation.ConfirmCleanup(true);ready.SetException(error);done.SetResult();}
        return ready.Task;
    }
    private void RequestStop()
    {
        stopping=true;var pump=dispatcher;if(pump==null||pump.HasShutdownStarted||pump.HasShutdownFinished)return;
        try{pump.BeginInvokeShutdown(DispatcherPriority.Send);}
        catch(InvalidOperationException error){if(!pump.HasShutdownStarted&&!pump.HasShutdownFinished)FailureCode=error.GetType().Name;} // Original registrar work is still retained until actual completion.
    }
    public async ValueTask<bool> TryFinalizeAsync()
    {
        RequestStop();Task? pending;lock(sync)pending=original;
        if(pending!=null)try{await pending.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);}catch(TimeoutException){return false;}
        return IsSettledAndRevoked;
    }
}
