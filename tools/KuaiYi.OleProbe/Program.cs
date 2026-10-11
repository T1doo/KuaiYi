using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KuaiYi.Contracts;
using KuaiYi.Core;

namespace KuaiYi.OleProbe;

internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        CleanupFault fault = CleanupFault.None;
        if (args.Length == 4 && (args[2] != "--cleanup-fault" || !Enum.TryParse(args[3], true, out fault) || !Enum.IsDefined(fault))) return 2;
        if (args.Length is not (2 or 4)) { Console.Error.WriteLine("Usage: KuaiYi.OleProbe.exe PUBLIC_FIXTURE METADATA_JSONL [--cleanup-fault NAME]"); return 2; }
        var app = new Application();
        if (args[0] == "--host" && Guid.TryParse(args[1], out Guid instance) && instance != Guid.Empty) app.Run(new DropWindow(instance, fault));
        else app.Run(new SupervisorWindow(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]), fault));
        return 0;
    }
}

/// <summary>Private inherited pipes and one owned child. Restart requires authoritative exit confirmation.</summary>
internal sealed class SupervisorWindow : Window
{
    private readonly string fixture, output;
    private readonly CleanupFault cleanupFault;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
    private readonly Queue<string> metadata = new();
    private readonly OleCallWatch deadline = new();
    private readonly DispatcherTimer watchdog = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly SemaphoreSlim writer = new(1);
    private Process? child;
    private OwnedHostTermination? termination;
    private CancellationTokenSource? lifetime;
    private Task<bool>? stopTask;
    private Guid instance, transfer;
    private long epoch, ticks, intent;
    private int deadlineMilliseconds;
    private bool enabled, stopping, restarting, closing, allowClose;
    public SupervisorWindow(string publicFixture, string evidence, CleanupFault fault)
    {
        fixture = File.ReadAllText(publicFixture, Encoding.UTF8).Trim(); output = evidence; cleanupFault = fault;
        Title = "KuaiYi P0 OLE supervisor — no model / no clipboard"; Width = 570; Height = 330;
        var panel = new StackPanel(); Content = panel;
        void Button(string label, Action action) { var button = new Button { Content = label, Margin = new Thickness(6) }; button.Click += (_, _) => action(); panel.Children.Add(button); }
        Button("启动 / 重启独立宿主（确认旧宿主退出后，Off）", Restart);
        Button("On / Off（Off 请求终止并确认宿主退出）", () => SetEnabled(!enabled));
        Button("把按钮移到鼠标旁", () => { if (!stopping && KuaiYi.Windows.NativeDesktop.GetCursorPos(out var p)) Send(new("Move", epoch, X: p.X + 70, Y: p.Y)); });
        panel.Children.Add(status);
        watchdog.Tick += (_, _) =>
        {
            ticks++;
            if (stopping && stopTask is not { IsCompleted: false } && child is { } owned && termination is { } exiting && exiting.ObserveExit()) ConfirmExit(owned, exiting, "Late exit observed");
            if (!stopping && deadline.Expired) { Record(new { kind = "WatchdogExpired", instance, epoch, ticks, timeoutMs = deadlineMilliseconds }); RequestStop("Native cleanup deadline expired"); }
        };
        Loaded += (_, _) => { watchdog.Start(); Restart(); };
        Closing += (_, e) => { if (!allowClose && child is not null) { e.Cancel = true; if (!closing) { closing = true; intent++; _ = CloseAfterExit(); } } };
        Closed += (_, _) => { watchdog.Stop(); Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllLines(output, metadata, new UTF8Encoding(false)); };
    }
    private void Record(object value) { if (metadata.Count == 100_000) metadata.Dequeue(); metadata.Enqueue(JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, item = value })); }
    private async Task CloseAfterExit()
    {
        if (await StopChildAsync("Close")) { allowClose = true; Close(); }
        else { closing = false; status.Text = "Exit not confirmed: supervisor stays open and retains ownership."; }
    }
    private async void Restart()
    {
        if (restarting || closing) return; restarting = true; long requested = ++intent;
        try
        {
            if (!await StopChildAsync("Restart") || requested != intent) return;
            instance = Guid.NewGuid(); epoch++; enabled = false; transfer = Guid.Empty;
            var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("--host"); start.ArgumentList.Add(instance.ToString("D")); start.ArgumentList.Add("--cleanup-fault"); start.ArgumentList.Add(cleanupFault.ToString());
            child = Process.Start(start) ?? throw new InvalidOperationException("Could not start owned host.");
            termination = new(new StartedHostLifetime(child)); lifetime = new(); stopping = false; stopTask = null;
            deadlineMilliseconds = 10_000; deadline.Start(instance, epoch); status.Text = "Starting host, Off";
            Record(new { kind = "OwnedHostStarted", instance, processId = child.Id, cleanupFault = cleanupFault.ToString() });
            _ = ReadEvents(child, instance, lifetime.Token); _ = DrainError(child, instance, lifetime.Token); Send(new("SetEnabled", epoch));
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        { Record(new { kind = "LaunchFailure", code = error.GetType().Name }); status.Text = "Owned host launch failed"; RequestStop("Launch failure"); }
        finally { restarting = false; }
    }
    private void SetEnabled(bool value)
    {
        if (!value) { intent++; RequestStop("Off"); return; }
        if (stopping || child is null) { status.Text = "Wait for exit confirmation, then explicitly restart (Off)."; return; }
        if (deadline.IsArmed) { status.Text = "On cannot extend an active native deadline."; return; }
        epoch++; enabled = true; transfer = Guid.Empty; deadline.Reset(); Send(new("SetEnabled", epoch, true)); status.Text = "On: drag public text to the green button";
    }
    private void Send(OleProbeControl control) => _ = SendControl(control);
    private async Task SendControl(OleProbeControl control)
    {
        var owned = child; var current = lifetime; var id = instance;
        if (owned is null || current is null || stopping) return;
        try
        {
            await writer.WaitAsync(current.Token);
            try { if (owned == child && !stopping) { await IpcFrameCodec.WriteAsync(owned.StandardInput.BaseStream, new(IpcMessageType.Ping, id, JsonSerializer.SerializeToUtf8Bytes(control)), current.Token); await owned.StandardInput.BaseStream.FlushAsync(current.Token); } }
            finally { writer.Release(); }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        { if (owned == child && !stopping) { Record(new { kind = "ControlFailure", code = error.GetType().Name }); RequestStop("Control pipe failed"); } }
    }
    private async Task ReadEvents(Process owned, Guid id, CancellationToken cancellation)
    {
        try
        {
            while (await IpcFrameCodec.ReadAsync(owned.StandardOutput.BaseStream, cancellation) is { } frame)
            {
                try
                {
                    if (stopping || owned != child || id != instance) continue;
                    if (frame.Type == IpcMessageType.BinaryChunk)
                    {
                        if (enabled && frame.RequestId == transfer && deadline.CanReadPayload(frame.RequestId, epoch))
                        {
                            string text = new UTF8Encoding(false, true).GetString(frame.Payload);
                            Record(new { kind = "PublicFixtureComparison", instance, epoch, transaction = transfer, matches = text == fixture, codeUnits = text.Length, ticks });
                        }
                        continue;
                    }
                    if (frame.Type != IpcMessageType.Ping) throw new InvalidDataException("Unexpected host message.");
                    var item = JsonSerializer.Deserialize<OleProbeEvent>(frame.Payload) ?? throw new InvalidDataException("Missing host event.");
                    if (item.Instance != instance) continue;
                    if (item.Epoch != epoch) { if (item.Phase == "Shown") Record(new { kind = "StartupFocusObservation", item, ticks }); continue; }
                    Record(new { item, transaction = frame.RequestId, ticks }); status.Text = $"{item.Phase}: {item.Status} — supervisor ticks {ticks}";
                    var decision = deadline.Accept(frame.RequestId, epoch, item.Phase, item.CleanupCompleted);
                    if (decision == OleWatchDecision.Rejected) { RequestStop("Expired or overlapping native operation"); continue; }
                    if (item.Phase is "QueryBegin" or "DropBegin" or "ShutdownBegin") { transfer = item.Phase == "DropBegin" ? frame.RequestId : Guid.Empty; deadlineMilliseconds = 1500; }
                }
                finally { CryptographicOperations.ZeroMemory(frame.Payload); }
            }
            if (owned == child && !stopping) RequestStop("Host pipe EOF");
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException or InvalidDataException or JsonException or InvalidOperationException)
        { if (owned == child && !stopping) { Record(new { kind = "PipeFailure", code = error.GetType().Name }); RequestStop("Host pipe failed"); } }
    }
    private async Task DrainError(Process owned, Guid id, CancellationToken token)
    {
        try { char[] bounded = new char[1024]; while (await owned.StandardError.ReadAsync(bounded.AsMemory(), token) > 0) { if (id == instance) Record(new { kind = "HostStderrDiscarded" }); Array.Clear(bounded); } }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        { if (!token.IsCancellationRequested && id == instance) Record(new { kind = "StderrReadFailure", code = error.GetType().Name }); }
    }
    private void RequestStop(string reason) => _ = StopChildAsync(reason);
    private Task<bool> StopChildAsync(string reason)
    {
        if (stopTask is { IsCompleted: false }) return stopTask;
        return stopTask = StopOwned(reason);
    }
    private async Task<bool> StopOwned(string reason)
    {
        var owned = child; var exiting = termination;
        enabled = false; stopping = true; epoch++; transfer = Guid.Empty; deadline.Reset(); lifetime?.Cancel();
        if (owned is null || exiting is null) { stopping = false; return true; }
        Record(new { kind = "StopAttempt", instance, processId = owned.Id, reason, exitBudgetMs = 2000 });
        status.Text = "Termination requested; waiting asynchronously for exit confirmation";
        var outcome = await exiting.StopAsync(TimeSpan.FromSeconds(2));
        Record(new { kind = "StopOutcome", instance, outcome = outcome.ToString(), exiting.FailureCode, reason });
        if (outcome != HostExitState.ExitConfirmed) { status.Text = "Exit not confirmed; restart blocked. The owned handle is retained."; return false; }
        ConfirmExit(owned, exiting, reason); return true;
    }
    private void ConfirmExit(Process owned, OwnedHostTermination exiting, string reason)
    {
        if (owned != child || !exiting.CanRestart) return;
        Record(new { kind = "ExitConfirmed", instance, processId = owned.Id, reason });
        owned.Dispose(); child = null; termination = null; lifetime?.Dispose(); lifetime = null; stopping = false;
        status.Text = "Owned host exit confirmed; explicit restart starts Off";
    }
}
