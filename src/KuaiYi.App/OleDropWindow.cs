using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using KuaiYi.Core;
using KuaiYi.Windows;

namespace KuaiYi.App;

/// <summary>Owns the initial WPF registration and every replacement for this window's lifetime.</summary>
internal abstract class OleDropWindow : Window
{
    private HwndSource? dropSource;
    protected OleWindowOwnership? DropOwnership { get; private set; }
    protected sealed override void OnSourceInitialized(EventArgs e)
    {
        Dispatcher.VerifyAccess();
        nint handle = new WindowInteropHelper(this).Handle;
        var source = HwndSource.FromHwnd(handle);
        uint thread = NativeDesktop.GetWindowThreadProcessId(handle, out uint process);
        // No arbitrary HWND, post-Loaded adoption, EnsureHandle-only source, or second handoff.
        if (dropSource != null || handle == 0 || IsLoaded || source == null || source.IsDisposed ||
            source.Dispatcher != Dispatcher || !ReferenceEquals(source.RootVisual, this) ||
            process != Environment.ProcessId || thread != NativeDesktop.GetCurrentThreadId())
            throw new InvalidOperationException("Drop registration requires the newly created owning WPF window.");
        dropSource = source;
        DropOwnership = new(handle.ToInt64());
        source.Disposed += (_, _) => DropOwnership.Invalidate();
        // WPF registers during HwndSource creation, even with AllowDrop=false. This override runs
        // before our SourceInitialized subscribers can publish/start any custom receiver.
        // This is never called as a retry after RegisterDragDrop reports ALREADYREGISTERED.
        using (Dispatcher.DisableProcessing())
            DropOwnership.TransferFrameworkRegistration(() => Marshal.ThrowExceptionForHR(OleNative.RevokeDragDrop(handle)));
        base.OnSourceInitialized(e);
    }
    protected OleTargetRegistrar CreateDropRegistrar()
    {
        Dispatcher.VerifyAccess();
        if (dropSource == null || dropSource.IsDisposed || !ReferenceEquals(HwndSource.FromHwnd(dropSource.Handle), dropSource))
            throw new InvalidOperationException("Drop source lifetime ended.");
        return new(DropOwnership!);
    }
    protected sealed override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        // WPF teardown also revokes this HWND. Never let it race an owned native registration.
        if (!e.Cancel && DropOwnership != null && !DropOwnership.TryClose()) e.Cancel = true;
    }
    protected override void OnClosed(EventArgs e)
    {
        DropOwnership?.Invalidate();
        base.OnClosed(e);
    }
}
