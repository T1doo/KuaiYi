using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Windows;

namespace KuaiYi.OleProbe;

public sealed class DropWindow : Window
{
    private readonly Guid instance;
    private readonly Button button = new() { Content = "译", Foreground = Brushes.White, Background = Brushes.Firebrick, Width = 48, Height = 48 };
    private readonly TextBlock hint = new() { Text = "释放以复制", FontSize = 12, Visibility = Visibility.Collapsed, IsHitTestVisible = false };
    private readonly Canvas canvas = new();
    private readonly Stream stdout = Console.OpenStandardOutput();
    private readonly object outputLock = new();
    private OleApartment? apartment;
    private NativeDropTarget? target;
    private nint hwnd, readingForeground;
    private readonly nint beforeShow = NativeDesktop.GetForegroundWindow();
    private bool enabled, registered, reading, moving, suppressClick;
    private long epoch;
    private PixelPoint dragStart;
    private PixelRect dragAnchor, anchor;
    private CancellationTokenSource controls = new();
    private readonly CleanupFault cleanupFault;
    public DropWindow(Guid id, CleanupFault fault = CleanupFault.None)
    {
        instance = id; cleanupFault = fault; Title = "KuaiYi isolated OLE host"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
        Width = Height = 48; Topmost = true; ShowActivated = false; ShowInTaskbar = false; Background = Brushes.White; Content = canvas;
        canvas.Children.Add(button); canvas.Children.Add(hint); Canvas.SetTop(hint, 49);
        button.PreviewMouseLeftButtonDown += (_, _) => { NativeDesktop.GetCursorPos(out var p); dragStart = new(p.X, p.Y); dragAnchor = anchor; moving = suppressClick = false; button.CaptureMouse(); };
        button.PreviewMouseMove += (_, e) =>
        {
            if (!button.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed || !NativeDesktop.GetCursorPos(out var p)) return;
            int dx = p.X - dragStart.X, dy = p.Y - dragStart.Y;
            if (!moving && Math.Abs(dx) + Math.Abs(dy) < 5) return;
            moving = suppressClick = true; Move(dragAnchor.Left + dx, dragAnchor.Top + dy);
        };
        button.PreviewMouseLeftButtonUp += (_, _) => { button.ReleaseMouseCapture(); moving = false; };
        // On/Off is owned by the independent supervisor. A local click is only a recorded focus experiment.
        button.Click += (_, _) => { if (suppressClick) { suppressClick = false; return; } Emit(instance, "ButtonClick", "No capture", foreground: NativeDesktop.GetForegroundWindow() != hwnd); };
        var menu = new ContextMenu(); var readItem = new MenuItem { Header = "主动阅读 / 返回非激活" };
        readItem.Click += (_, _) => ToggleReading(); menu.Items.Add(readItem); button.ContextMenu = menu;
        button.ContextMenuOpening += (_, _) => { if (!reading) readingForeground = NativeDesktop.GetForegroundWindow(); };
        PreviewKeyDown += (_, e) => { if (reading && e.Key == Key.Escape) { ToggleReading(); e.Handled = true; } };
        SourceInitialized += (_, _) => Initialize();
        Loaded += (_, _) => Emit(instance, "Shown", "Non-activating native window", foreground: NativeDesktop.GetForegroundWindow() == beforeShow);
        Closed += (_, _) => ShutdownNative();
    }
    private void Initialize()
    {
        apartment = new(); hwnd = new WindowInteropHelper(this).Handle; NativeDesktop.SetOverlayStyle(hwnd, false);
        NativeDesktop.GetCursorPos(out var p); Move(p.X + 70, p.Y);
        target = new(this); Marshal.ThrowExceptionForHR(OleNative.RegisterDragDrop(hwnd, target)); registered = true;
        Emit(instance, "Ready", "Off"); _ = ReadControls(controls.Token);
    }
    private void ShutdownNative()
    {
        Guid shutdown = Guid.NewGuid(); Emit(shutdown, "ShutdownBegin", "RevokeDragDrop and OleUninitialize deadline");
        var scope = new OleCleanupScope();
        if (apartment is not null) scope.Own(apartment.Dispose);
        if (registered) scope.Own(() => { Marshal.ThrowExceptionForHR(OleNative.RevokeDragDrop(hwnd)); registered = false; });
        try { scope.Run(() => { controls.Cancel(); return 0; }); }
        catch (Exception error) { Emit(shutdown, "CleanupFailed", error.GetType().Name); return; }
        Emit(shutdown, "ShutdownEnd", "Native shutdown cleanup completed", cleanupCompleted: scope.CleanupCompleted);
        controls.Dispose(); stdout.Dispose();
    }
    private async Task ReadControls(CancellationToken cancellation)
    {
        using var stdin = Console.OpenStandardInput();
        try
        {
            while (await IpcFrameCodec.ReadAsync(stdin, cancellation) is { } frame)
            {
                try
                {
                    if (frame.Type != IpcMessageType.Ping || frame.RequestId != instance) throw new InvalidDataException("Invalid owned control.");
                    var command = JsonSerializer.Deserialize<OleProbeControl>(frame.Payload) ?? throw new InvalidDataException("Missing control.");
                    if (command.Epoch < epoch) continue;
                    if (command.Command == "SetEnabled") { epoch = command.Epoch; enabled = command.Enabled; target?.Invalidate(); button.Background = enabled ? Brushes.ForestGreen : Brushes.Firebrick; Preview(false); Emit(instance, "Ready", enabled ? "On" : "Off"); }
                    else if (command.Command == "Move" && command.Epoch == epoch) Move(command.X, command.Y);
                    else throw new InvalidDataException("Unknown control.");
                }
                finally { CryptographicOperations.ZeroMemory(frame.Payload); }
            }
            Close();
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or InvalidDataException or JsonException)
        { if (!cancellation.IsCancellationRequested) { Emit(instance, "ControlFailure", error.GetType().Name); Close(); } }
    }
    private void Move(int x, int y)
    {
        var monitor = NativeDesktop.MonitorAt(new(x, y)); int size = monitor.Dpi.CeilingPixels(48);
        anchor = NativeDesktop.ClampAnchor(new(x, y, x + size, y + size), monitor); Preview(false);
    }
    internal void Preview(bool visible)
    {
        var monitor = NativeDesktop.MonitorAt(anchor.Center); var work = monitor.WorkArea.Inset(monitor.Dpi.CeilingPixels(12));
        int width = monitor.Dpi.CeilingPixels(visible ? 96 : 48), height = monitor.Dpi.CeilingPixels(visible ? 64 : 48);
        int left = Math.Clamp(anchor.Left, work.Left, Math.Max(work.Left, work.Right - width));
        int top = Math.Clamp(anchor.Top, work.Top, Math.Max(work.Top, work.Bottom - height));
        NativeDesktop.ApplyBounds(hwnd, new(left, top, left + width, top + height), monitor.Dpi.CeilingPixels(12));
        Canvas.SetLeft(button, monitor.Dpi.Dips(anchor.Left - left)); Canvas.SetTop(button, monitor.Dpi.Dips(anchor.Top - top));
        Canvas.SetLeft(hint, 2); Canvas.SetTop(hint, Math.Min(49, monitor.Dpi.Dips(height) - 15)); hint.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }
    private void ToggleReading()
    {
        reading = !reading;
        NativeDesktop.SetOverlayStyle(hwnd, reading);
        if (reading) { Activate(); button.Focus(); Emit(instance, "ReadingEnter", "Explicit activation", foreground: NativeDesktop.GetForegroundWindow() == hwnd); }
        else { bool restored = readingForeground != 0 && NativeDesktop.IsWindow(readingForeground) && OleNative.SetForegroundWindow(readingForeground); Emit(instance, "ReadingExit", restored ? "Foreground restore requested" : "Foreground restore refused", foreground: NativeDesktop.GetForegroundWindow() == readingForeground); }
    }
    internal void Emit(Guid transaction, string phase, string status, OleEffect effect = OleEffect.None, bool? available = null, bool? mode = null, bool? started = null, int length = 0, bool? foreground = null, bool cleanupCompleted = false)
    {
        var item = new OleProbeEvent(instance, epoch, phase, status, (uint)effect, available, mode, started, length, foreground, hwnd.ToInt64(), Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState().ToString(), cleanupCompleted);
        Write(new(IpcMessageType.Ping, transaction, JsonSerializer.SerializeToUtf8Bytes(item)));
    }
    internal void WorkerMetadata(Guid transaction, long operationEpoch, string phase, string status = "Worker apartment diagnostic")
    {
        // Output is thread-safe; no WPF or origin-apartment COM object is accessed here.
        var item = new OleProbeEvent(instance, operationEpoch, phase, status, ManagedThreadId: Environment.CurrentManagedThreadId, Apartment: Thread.CurrentThread.GetApartmentState().ToString());
        Write(new(IpcMessageType.Ping, transaction, JsonSerializer.SerializeToUtf8Bytes(item)));
    }
    internal void Result(Guid transaction, long operationEpoch, string? text, string status, nint foreground)
    {
        if (operationEpoch != epoch || !enabled) { Emit(transaction, "DropEnd", "Discarded stale clean result", cleanupCompleted: true); return; }
        Emit(transaction, "CleanupComplete", "All owned native cleanup completed", cleanupCompleted: true);
        if (text is not null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            try { Write(new(IpcMessageType.BinaryChunk, transaction, bytes)); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        Preview(false); Emit(transaction, "DropEnd", status, text is null ? OleEffect.None : OleEffect.Copy, length: text?.Length ?? 0, foreground: NativeDesktop.GetForegroundWindow() == foreground, cleanupCompleted: true);
    }
    internal void ReleaseOwned(Guid transaction, long operationEpoch, string stage, Action release)
    {
        // Explicit probe-only injection surrounds a real release. It does not simulate a Windows COM pass.
        bool blocked = cleanupFault.ToString() == stage + "Blocked", failed = cleanupFault.ToString() == stage + "Failure";
        try { WorkerMetadata(transaction, operationEpoch, stage + "CleanupBegin"); if (blocked) Thread.Sleep(5000); }
        finally { release(); }
        if (failed) throw new IOException("Explicit public cleanup fault injection.");
    }
    private void Write(IpcFrame frame)
    {
        lock (outputLock) { IpcFrameCodec.WriteAsync(stdout, frame).AsTask().GetAwaiter().GetResult(); stdout.Flush(); }
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class NativeDropTarget(DropWindow owner) : IOleDropTarget
    {
        private readonly OleDropTransaction transaction = new();
        private Guid id;
        private bool advertised, active;
        public void Invalidate() { transaction.Leave(); advertised = false; }
        private bool Clean(OleCleanupScope scope, Guid operation, long generation, bool earlierCleanupFailed = false)
        {
            try { scope.Dispose(); }
            catch (Exception error) { owner.WorkerMetadata(operation, generation, "CleanupFailed", error.GetType().Name); return false; }
            if (earlierCleanupFailed) { owner.WorkerMetadata(operation, generation, "CleanupFailed", "Earlier native cleanup unconfirmed"); return false; }
            return scope.CleanupCompleted;
        }
        public int DragEnter(nint data, uint keys, OlePoint point, ref uint effect)
        {
            Guid query = transaction.Enter(); id = query; advertised = false;
            if (!owner.enabled || active || ((OleEffect)effect & OleEffect.Copy) == 0) { effect = 0; return 0; }
            long generation = owner.epoch; nint foreground = NativeDesktop.GetForegroundWindow();
            owner.Emit(query, "QueryBegin", "QueryGetData and cleanup deadline");
            var scope = new OleCleanupScope(); string status = "Format query and native cleanup completed";
            try
            {
                var reference = OleDataReference.FromBorrowed(data);
                scope.Own(() => owner.ReleaseOwned(query, generation, "QueryRelease", reference.Dispose));
                advertised = reference.OffersUnicode();
            }
            catch (Exception error) { advertised = false; status = error.GetType().Name; }
            if (!Clean(scope, query, generation)) { effect = 0; advertised = false; active = true; return 0; }
            effect = (uint)OleTransferPolicy.Preview(owner.enabled && generation == owner.epoch, (OleEffect)effect, advertised);
            owner.Preview(effect != 0);
            owner.Emit(query, "QueryEnd", status, (OleEffect)effect, foreground: NativeDesktop.GetForegroundWindow() == foreground, cleanupCompleted: scope.CleanupCompleted);
            return 0;
        }
        public int DragOver(uint keys, OlePoint point, ref uint effect) { effect = (uint)OleTransferPolicy.Preview(owner.enabled && !active, (OleEffect)effect, advertised); return 0; }
        public int DragLeave() { Invalidate(); owner.Preview(false); return 0; }
        public int Drop(nint data, uint keys, OlePoint point, ref uint effect)
        {
            if (active || !transaction.TryConsume(id, owner.enabled, (OleEffect)effect, advertised)) { effect = 0; return 0; }
            Guid operation = id; long generation = owner.epoch; nint foreground = NativeDesktop.GetForegroundWindow(); active = true; effect = 0;
            owner.Emit(operation, "DropBegin", "Extraction, EndOperation and all cleanup deadline");
            var scope = new OleCleanupScope(); var transfer = new AsyncTransfer(scope);
            string? text = null; string status = "Synchronous copy and all cleanup completed"; bool priorCleanupFailed = false;
            try
            {
                var reference = OleDataReference.FromBorrowed(data);
                var dataLease = scope.Own(() => owner.ReleaseOwned(operation, generation, "DropRelease", reference.Dispose));
                var capability = reference.QueryAsync(); bool mode = false, started = false;
                if (capability is not null)
                {
                    scope.Own(() => owner.ReleaseOwned(operation, generation, "AsyncRelease", capability.Dispose));
                    try { mode = capability.Enabled; if (mode) started = capability.StartOperation() >= 0; }
                    catch (COMException error) { owner.Emit(operation, "NegotiationFailed", error.GetType().Name); }
                    if (started)
                    {
                        scope.Own(() =>
                        {
                            if (!transfer.EndAttempted) { transfer.EndAttempted = true; Marshal.ThrowExceptionForHR(capability.EndOperation(OleNative.Failure, OleEffect.None)); transfer.EndSucceeded = true; }
                            if (!transfer.EndSucceeded) throw new InvalidOperationException("EndOperation was not confirmed; no retry.");
                        });
                    }
                }
                owner.Emit(operation, "Negotiated", "Optional async capability", available: capability is not null, mode: mode, started: started);
                if (OleTransferPolicy.Extraction(capability is not null, mode, started) == OleExtractionPath.NegotiatedAsync)
                {
                    transfer.Stream = reference.MarshalToStream();
                    // Two owned stream references: origin cleanup and the future unmarshal call.
                    scope.Own(() => ReleaseStream(transfer));
                    Marshal.AddRef(transfer.Stream); transfer.ExtraReference = true;
                    dataLease.Dispose(); // A remote Release may pump messages: do it before queuing.
                    owner.Emit(operation, "DropReturningAsync", "Owned transfer pending; deadline remains armed", OleEffect.Copy);
                    owner.Dispatcher.BeginInvoke(() => LaunchWorker(transfer, operation, generation, foreground));
                    effect = (uint)OleEffect.Copy; return 0; // The origin scope remains owned until FinishWorker.
                }
                text = reference.ReadUnicode();
            }
            catch (Exception error) { text = null; status = error.GetType().Name; priorCleanupFailed = error is OleCleanupException; }
            if (!Clean(scope, operation, generation, priorCleanupFailed)) return 0;
            effect = text is null ? 0 : (uint)OleEffect.Copy;
            active = false; owner.Result(operation, generation, text, status, foreground); return 0;
        }
        private sealed class AsyncTransfer(OleCleanupScope origin)
        {
            public OleCleanupScope Origin { get; } = origin;
            public nint Stream;
            public bool ExtraReference, StreamReleased, Unmarshaled, EndAttempted, EndSucceeded;
        }
        private static void ReleaseStream(AsyncTransfer transfer)
        {
            try { if (!transfer.Unmarshaled) OleNative.ReleaseUnusedMarshalPacket(transfer.Stream); }
            finally
            {
                try { if (transfer.ExtraReference && !transfer.StreamReleased) Marshal.Release(transfer.Stream); }
                finally { Marshal.Release(transfer.Stream); }
            }
        }
        private void LaunchWorker(AsyncTransfer transfer, Guid operation, long generation, nint foreground)
        {
            var worker = new Thread(() =>
            {
                string? text = null; string status = "Async copy and all cleanup completed";
                bool nativeCleanupFailed = false; var apartmentScope = new OleCleanupScope();
                try
                {
                    var apartment = new OleApartment(); apartmentScope.Own(apartment.Dispose);
                    var dispatcher = Dispatcher.CurrentDispatcher;
                    dispatcher.BeginInvoke(() =>
                    {
                        var scope = new OleCleanupScope();
                        try
                        {
                            owner.WorkerMetadata(operation, generation, "WorkerUnmarshalBegin");
                            Guid iid = OleNative.DataObjectId; transfer.StreamReleased = true;
                            int hr = OleNative.CoGetInterfaceAndReleaseStream(transfer.Stream, ref iid, out nint pointer);
                            transfer.Unmarshaled = hr >= 0; Marshal.ThrowExceptionForHR(hr);
                            var reference = new OleDataReference(pointer); scope.Own(reference.Dispose);
                            var capability = reference.QueryAsync() ?? throw new COMException("Async capability disappeared."); scope.Own(capability.Dispose);
                            int extractionHr = OleNative.Failure;
                            try { owner.WorkerMetadata(operation, generation, "WorkerReadBegin"); text = reference.ReadUnicode(); extractionHr = 0; }
                            catch (Exception error) { text = null; status = error.GetType().Name; nativeCleanupFailed = error is OleCleanupException; }
                            finally
                            {
                                owner.WorkerMetadata(operation, generation, "WorkerEndAttempt"); transfer.EndAttempted = true;
                                int end = capability.EndOperation(extractionHr, text is null ? OleEffect.None : OleEffect.Copy);
                                transfer.EndSucceeded = end >= 0;
                                if (!transfer.EndSucceeded) { text = null; status = "EndOperation failed"; nativeCleanupFailed = true; }
                            }
                        }
                        catch (Exception error) { text = null; status = error.GetType().Name; nativeCleanupFailed |= error is OleCleanupException; }
                        finally { if (!Clean(scope, operation, generation)) { text = null; nativeCleanupFailed = true; } dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                    });
                    Dispatcher.Run();
                }
                catch (Exception error) { text = null; status = error.GetType().Name; nativeCleanupFailed |= error is OleCleanupException; }
                finally { if (!Clean(apartmentScope, operation, generation)) { text = null; nativeCleanupFailed = true; } }
                // Queuing establishes visibility of worker state; all origin COM calls remain on the origin STA.
                owner.Dispatcher.BeginInvoke(() => FinishWorker(transfer, operation, generation, foreground, text, status, nativeCleanupFailed));
            }) { IsBackground = true, Name = "KuaiYi marshaled OLE STA" };
            try { worker.SetApartmentState(ApartmentState.STA); worker.Start(); }
            catch (Exception error) { FinishWorker(transfer, operation, generation, foreground, null, error.GetType().Name, false); }
        }
        private void FinishWorker(AsyncTransfer transfer, Guid operation, long generation, nint foreground, string? text, string status, bool workerCleanupFailed)
        {
            if (!Clean(transfer.Origin, operation, generation, workerCleanupFailed)) return;
            active = false; owner.Result(operation, generation, text, status, foreground);
        }
    }
}

public enum CleanupFault { None, QueryReleaseBlocked, QueryReleaseFailure, DropReleaseBlocked, DropReleaseFailure, AsyncReleaseBlocked, AsyncReleaseFailure }
