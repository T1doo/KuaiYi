using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KuaiYi.Core;
using KuaiYi.Windows;

namespace KuaiYi.App;

// Real HWND/OLE regression, compiled only into the explicit public CI entry.
internal static class OleOwnershipProbe
{
    private sealed class OwnedWindow : OleDropWindow { internal OleWindowOwnership Ownership => DropOwnership!; }
    private sealed class Target : IOleDropTarget
    {
        public int DragEnter(nint data, uint keys, OlePoint point, ref uint effect) { effect=0; return 0; }
        public int DragOver(uint keys, OlePoint point, ref uint effect) { effect=0; return 0; }
        public int DragLeave() => 0;
        public int Drop(nint data, uint keys, OlePoint point, ref uint effect) { effect=0; return 0; }
    }
    internal static void Verify(Application app)
    {
        var shutdown = app.ShutdownMode; app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        using var apartment = new OleApartment(); var target = new Target();
        try
        {
            var baseline = new Window { Width=48, Height=48, ShowActivated=false, ShowInTaskbar=false };
            baseline.Show();
            try
            {
                int error = OleNative.RegisterDragDrop(new WindowInteropHelper(baseline).Handle, target);
                if (error != unchecked((int)0x80040101)) throw new InvalidOperationException("WPF initial registration conflict was not reproduced.");
                // No revoke: this failed attempt never acquired a registration. WPF owns its teardown.
            }
            finally { baseline.Close(); }

            var handleOnly = new OwnedWindow(); bool rejected=false, handleOnlyClosed=false;
            handleOnly.Closed+=(_,_)=>handleOnlyClosed=true;
            try { new WindowInteropHelper(handleOnly).EnsureHandle(); }
            catch (InvalidOperationException) { rejected=true; }
            nint rejectedHwnd = new WindowInteropHelper(handleOnly).Handle;
            if (!rejected || rejectedHwnd == 0 || OleNative.RegisterDragDrop(rejectedHwnd,target) != unchecked((int)0x80040101))
                throw new InvalidOperationException("EnsureHandle rejection changed framework registration.");
            handleOnly.Close(); if (!handleOnlyClosed) throw new InvalidOperationException("Rejected source could not close.");

            var window = new OwnedWindow { Width=48, Height=48, ShowActivated=false, ShowInTaskbar=false };
            window.Show(); nint hwnd = new WindowInteropHelper(window).Handle;
            bool closed=false; window.Closed+=(_,_)=>closed=true;
            for (int i=0; i<2; i++)
            {
                var original = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var lease = window.Ownership.Reserve(hwnd.ToInt64(), original.Task);
                bool registered=false, cleanup=false;
                try
                {
                    Marshal.ThrowExceptionForHR(OleNative.RegisterDragDrop(hwnd,target)); registered=true;
                    try { window.Ownership.Reserve(hwnd.ToInt64(), new TaskCompletionSource().Task); throw new Exception("Duplicate ownership admitted."); }
                    catch (InvalidOperationException) { }
                    window.Close(); if (closed) throw new InvalidOperationException("Window closed before original registration settled.");
                    if (OleNative.RegisterDragDrop(hwnd,target) != unchecked((int)0x80040101)) throw new InvalidOperationException("Close revoked a still-owned target.");
                }
                finally
                {
                    if (registered) { Marshal.ThrowExceptionForHR(OleNative.RevokeDragDrop(hwnd)); cleanup=true; }
                    lease.ConfirmCleanup(cleanup); original.SetResult();
                }
            }
            window.Close(); if (!closed) throw new InvalidOperationException("Settled window did not close.");
            try { window.Ownership.Reserve(hwnd.ToInt64(),new TaskCompletionSource().Task); throw new Exception("Closed HWND generation admitted."); }
            catch (InvalidOperationException) { }
            GC.KeepAlive(target);
        }
        finally { app.ShutdownMode = shutdown; }
    }
}
