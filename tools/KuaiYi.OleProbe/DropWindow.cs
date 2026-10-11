using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
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
using ComDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

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
    public DropWindow(Guid id)
    {
        instance = id; Title = "KuaiYi isolated OLE host"; WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize;
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
        Closed += (_, _) => { controls.Cancel(); if (registered) { int hr = OleNative.RevokeDragDrop(hwnd); if (hr < 0) Emit(instance, "RevokeFailure", hr.ToString("X8")); registered = false; } apartment?.Dispose(); controls.Dispose(); stdout.Dispose(); };
    }
    private void Initialize()
    {
        apartment = new(); hwnd = new WindowInteropHelper(this).Handle; NativeDesktop.SetOverlayStyle(hwnd, false);
        NativeDesktop.GetCursorPos(out var p); Move(p.X + 70, p.Y);
        target = new(this); Marshal.ThrowExceptionForHR(OleNative.RegisterDragDrop(hwnd, target)); registered = true;
        Emit(instance, "Ready", "Off"); _ = ReadControls(controls.Token);
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
    internal void Emit(Guid transaction, string phase, string status, OleEffect effect = OleEffect.None, bool? available = null, bool? mode = null, bool? started = null, int length = 0, bool? foreground = null)
    {
        var item = new OleProbeEvent(instance, epoch, phase, status, (uint)effect, available, mode, started, length, foreground, hwnd.ToInt64(), Environment.CurrentManagedThreadId, Thread.CurrentThread.GetApartmentState().ToString());
        Write(new(IpcMessageType.Ping, transaction, JsonSerializer.SerializeToUtf8Bytes(item)));
    }
    internal void WorkerMetadata(Guid transaction, long operationEpoch, string phase)
    {
        // Output is thread-safe; no WPF or origin-apartment COM object is accessed here.
        var item = new OleProbeEvent(instance, operationEpoch, phase, "Worker apartment diagnostic", ManagedThreadId: Environment.CurrentManagedThreadId, Apartment: Thread.CurrentThread.GetApartmentState().ToString());
        Write(new(IpcMessageType.Ping, transaction, JsonSerializer.SerializeToUtf8Bytes(item)));
    }
    internal void Result(Guid transaction, long operationEpoch, string? text, string status, nint foreground)
    {
        if (operationEpoch != epoch || !enabled) { Emit(transaction, "DropEnd", "Discarded stale result"); return; }
        if (text is not null)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            try { Write(new(IpcMessageType.BinaryChunk, transaction, bytes)); }
            finally { CryptographicOperations.ZeroMemory(bytes); }
        }
        Preview(false); Emit(transaction, "DropEnd", status, text is null ? OleEffect.None : OleEffect.Copy, length: text?.Length ?? 0, foreground: NativeDesktop.GetForegroundWindow() == foreground);
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
        public int DragEnter(ComDataObject data, uint keys, OlePoint point, ref uint effect)
        {
            Guid queryId = transaction.Enter(); id = queryId; advertised = false;
            if (!owner.enabled || active || ((OleEffect)effect & OleEffect.Copy) == 0) { effect = 0; return 0; }
            nint foreground = NativeDesktop.GetForegroundWindow();
            owner.Emit(queryId, "QueryBegin", "QueryGetData only");
            try { using var reference = new OleDataReference(data); advertised = reference.OffersUnicode(); effect = (uint)OleTransferPolicy.Preview(owner.enabled, (OleEffect)effect, advertised); owner.Preview(effect != 0); owner.Emit(queryId, "QueryEnd", "Format negotiation complete", (OleEffect)effect, foreground: NativeDesktop.GetForegroundWindow() == foreground); }
            catch (Exception error) { effect = 0; owner.Emit(queryId, "QueryEnd", error.GetType().Name); }
            return 0;
        }
        public int DragOver(uint keys, OlePoint point, ref uint effect) { effect = (uint)OleTransferPolicy.Preview(owner.enabled && !active, (OleEffect)effect, advertised); return 0; }
        public int DragLeave() { Invalidate(); owner.Preview(false); return 0; }
        public int Drop(ComDataObject data, uint keys, OlePoint point, ref uint effect)
        {
            if (active || !transaction.TryConsume(id, owner.enabled, (OleEffect)effect, advertised)) { effect = 0; return 0; }
            Guid operationId = id; long operationEpoch = owner.epoch; nint foreground = NativeDesktop.GetForegroundWindow(); active = true;
            owner.Emit(operationId, "DropBegin", "Native extraction deadline begins"); effect = 0;
            try
            {
                using var reference = new OleDataReference(data); var capability = reference.QueryAsync();
                bool mode = false, started = false;
                try
                {
                    if (capability is not null)
                    {
                        try { mode = capability.Enabled; if (mode) started = capability.StartOperation() >= 0; }
                        catch (COMException) { mode = false; }
                    }
                    owner.Emit(operationId, "Negotiated", "Optional async capability", available: capability is not null, mode: mode, started: started);
                    if (OleTransferPolicy.Extraction(capability is not null, mode, started) == OleExtractionPath.NegotiatedAsync)
                    {
                        nint stream = reference.MarshalToStream();
                        var heldCapability = capability!;
                        // Release the origin IDataObject and finish diagnostics before queuing: a proxy Release
                        // can pump COM messages. No origin COM call follows BeginInvoke on the success path.
                        try
                        {
                            reference.Dispose();
                            owner.Emit(operationId, "DropReturningAsync", "Marshaled stream; extraction deferred", OleEffect.Copy);
                            owner.Dispatcher.BeginInvoke(() => LaunchWorker(stream, heldCapability, operationId, operationEpoch, foreground));
                        }
                        catch
                        {
                            try { OleNative.ReleaseUnusedMarshalPacket(stream); }
                            finally { Marshal.Release(stream); }
                            throw;
                        }
                        capability = null; // Ownership moved to the origin-STA completion action.
                        effect = (uint)OleEffect.Copy; return 0;
                    }
                    // Sources without the optional interface remain supported in their synchronous lifetime.
                    string text = reference.ReadUnicode(); effect = (uint)OleEffect.Copy;
                    owner.Result(operationId, operationEpoch, text, "Synchronous copy before Drop return", foreground); active = false; return 0;
                }
                finally
                {
                    if (capability is not null)
                    {
                        // A successful Start that was not handed off must be ended even if diagnostics/marshaling fails.
                        try { if (started) Marshal.ThrowExceptionForHR(capability.EndOperation(OleNative.Failure, OleEffect.None)); }
                        finally { capability.Dispose(); }
                    }
                }
            }
            catch (Exception error) { owner.Result(operationId, operationEpoch, null, error.GetType().Name, foreground); active = false; return 0; }
        }
        private void LaunchWorker(nint stream, OleAsyncReference originCapability, Guid operation, long operationEpoch, nint foreground)
        {
            // Keep one stream reference on the origin for cleanup if unmarshaling fails.
            Marshal.AddRef(stream);
            var worker = new Thread(() =>
            {
                bool consumed = false, streamReleased = false, endAttempted = false; string? result = null; string status = "Worker failed";
                try
                {
                    using var apartment = new OleApartment(); var dispatcher = Dispatcher.CurrentDispatcher;
                    dispatcher.BeginInvoke(() =>
                    {
                        try
                        {
                            owner.WorkerMetadata(operation, operationEpoch, "WorkerUnmarshalBegin");
                            Guid iid = OleNative.DataObjectId; streamReleased = true; int hr = OleNative.CoGetInterfaceAndReleaseStream(stream, ref iid, out nint pointer);
                            consumed = hr >= 0; Marshal.ThrowExceptionForHR(hr);
                            using var reference = new OleDataReference(pointer); using var capability = reference.QueryAsync() ?? throw new COMException("Async capability disappeared.");
                            int extractionHr = OleNative.Failure;
                            try { owner.WorkerMetadata(operation, operationEpoch, "WorkerReadBegin"); result = reference.ReadUnicode(); extractionHr = 0; status = "Async copy on marshaled STA"; }
                            catch (Exception error) { status = error.GetType().Name; }
                            finally { owner.WorkerMetadata(operation, operationEpoch, "WorkerEndAttempt"); endAttempted = true; int end = capability.EndOperation(extractionHr, result is null ? OleEffect.None : OleEffect.Copy); if (end < 0) { result = null; status = "EndOperation failed"; } }
                        }
                        catch (Exception error) { result = null; status = error.GetType().Name; }
                        finally { dispatcher.BeginInvokeShutdown(DispatcherPriority.Send); }
                    });
                    Dispatcher.Run();
                }
                catch (Exception error) { status = error.GetType().Name; }
                owner.Dispatcher.BeginInvoke(() => FinishWorker(stream, originCapability, operation, operationEpoch, foreground, consumed, streamReleased, endAttempted, result, status));
            }) { IsBackground = true, Name = "KuaiYi marshaled OLE STA" };
            try { worker.SetApartmentState(ApartmentState.STA); worker.Start(); }
            catch (Exception error)
            {
                // Worker never owned its reference. Both references and the marshal packet stay on this STA.
                string failure = error.GetType().Name;
                try { OleNative.ReleaseUnusedMarshalPacket(stream); }
                catch (Exception cleanup) { failure = cleanup.GetType().Name; }
                finally
                {
                    try { Marshal.ThrowExceptionForHR(originCapability.EndOperation(OleNative.Failure, OleEffect.None)); }
                    catch (Exception completion) { failure = completion.GetType().Name; }
                    finally { Marshal.Release(stream); Marshal.Release(stream); originCapability.Dispose(); active = false; owner.Result(operation, operationEpoch, null, failure, foreground); }
                }
            }
        }
        private void FinishWorker(nint stream, OleAsyncReference capability, Guid operation, long operationEpoch, nint foreground, bool consumed, bool streamReleased, bool endAttempted, string? text, string status)
        {
            try { if (!consumed) OleNative.ReleaseUnusedMarshalPacket(stream); }
            catch (Exception error) { text = null; status = error.GetType().Name; }
            finally
            {
                try { if (!endAttempted) Marshal.ThrowExceptionForHR(capability.EndOperation(OleNative.Failure, OleEffect.None)); }
                catch (Exception error) { text = null; status = error.GetType().Name; }
                finally { if (!streamReleased) Marshal.Release(stream); Marshal.Release(stream); capability.Dispose(); active = false; owner.Result(operation, operationEpoch, text, status, foreground); }
            }
        }
    }
}
