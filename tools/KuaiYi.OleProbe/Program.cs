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
        var app = new Application();
        if (args.Length == 2 && args[0] == "--host" && Guid.TryParse(args[1], out Guid instance) && instance != Guid.Empty)
        {
            app.Run(new DropWindow(instance)); return 0;
        }
        if (args.Length != 2) { Console.Error.WriteLine("Usage: KuaiYi.OleProbe.exe PUBLIC_FIXTURE METADATA_JSONL"); return 2; }
        app.Run(new SupervisorWindow(Path.GetFullPath(args[0]), Path.GetFullPath(args[1]))); return 0;
    }
}

/// <summary>Private inherited pipes and an owned killable child, not a production IPC endpoint.</summary>
internal sealed class SupervisorWindow : Window
{
    private readonly string fixture, output;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(12) };
    private readonly Queue<string> metadata = new();
    private readonly OleCallDeadline deadline = new();
    private readonly DispatcherTimer watchdog = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private Process? child;
    private CancellationTokenSource? lifetime;
    private readonly SemaphoreSlim writer = new(1);
    private Guid instance, transfer;
    private long epoch, ticks;
    private int deadlineMilliseconds;
    private bool enabled;
    public SupervisorWindow(string publicFixture, string evidence)
    {
        fixture = File.ReadAllText(publicFixture, Encoding.UTF8).Trim(); output = evidence;
        Title = "KuaiYi P0 OLE supervisor — no model / no clipboard"; Width = 570; Height = 330;
        var panel = new StackPanel(); Content = panel;
        void Button(string label, Action action) { var button = new Button { Content = label, Margin = new Thickness(6) }; button.Click += (_, _) => action(); panel.Children.Add(button); }
        Button("启动 / 重启独立宿主（Off）", Restart);
        Button("On / Off（Off 会终止正在阻塞的宿主）", () => SetEnabled(!enabled));
        Button("把按钮移到鼠标旁", () => { if (KuaiYi.Windows.NativeDesktop.GetCursorPos(out var p)) Send(new("Move", epoch, X: p.X + 70, Y: p.Y)); });
        panel.Children.Add(status);
        watchdog.Tick += (_, _) => { ticks++; if (deadline.Expired) { Record(new { kind = "WatchdogExpired", instance, epoch, ticks, timeoutMs = deadlineMilliseconds }); StopChild("Timeout: owned host termination requested; restart stays Off"); } };
        Loaded += (_, _) => { watchdog.Start(); Restart(); };
        Closed += (_, _) => { watchdog.Stop(); StopChild("Closed"); Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllLines(output, metadata, new UTF8Encoding(false)); };
    }
    private void Record(object value) { if (metadata.Count == 100_000) metadata.Dequeue(); metadata.Enqueue(JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, item = value })); }
    private void Restart()
    {
        if (!StopChild("Restart")) return; instance = Guid.NewGuid(); epoch++; enabled = false; transfer = Guid.Empty;
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("--host"); start.ArgumentList.Add(instance.ToString("D"));
        child = Process.Start(start) ?? throw new InvalidOperationException("Could not start owned OLE host.");
        lifetime = new(); var owned = child; var token = lifetime.Token; Guid id = instance;
        _ = ReadEvents(owned, id, token);
        _ = DrainError(owned, id, token);
        deadlineMilliseconds = 10_000; deadline.Begin(instance, epoch, TimeSpan.FromSeconds(10)); status.Text = "Starting host, Off";
        Send(new("SetEnabled", epoch));
    }
    private void SetEnabled(bool value)
    {
        if (!value) { StopChild("Off: owned native host terminated"); return; }
        if (child is null) { status.Text = "Restart the host first (it starts Off)."; return; }
        if (deadline.IsArmed) { status.Text = "Wait for this fixed native deadline; On cannot extend it."; return; }
        epoch++; enabled = value; transfer = Guid.Empty; deadline.Reset(); Send(new("SetEnabled", epoch, value));
        status.Text = value ? "On: drag the public text to the green button" : "Off: formats and data must not be queried";
    }
    private void Send(OleProbeControl control) => _ = SendControl(control);
    private async Task SendControl(OleProbeControl control)
    {
        var owned = child; var current = lifetime; var id = instance;
        if (owned is null || current is null) return;
        try
        {
            await writer.WaitAsync(current.Token);
            try { if (owned == child) { await IpcFrameCodec.WriteAsync(owned.StandardInput.BaseStream, new(IpcMessageType.Ping, id, JsonSerializer.SerializeToUtf8Bytes(control)), current.Token); await owned.StandardInput.BaseStream.FlushAsync(current.Token); } }
            finally { writer.Release(); }
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        { if (owned == child) { Record(new { kind = "ControlFailure", code = error.GetType().Name }); StopChild("Control pipe failed"); } }
    }
    private async Task ReadEvents(Process owned, Guid id, CancellationToken cancellation)
    {
        try
        {
            while (await IpcFrameCodec.ReadAsync(owned.StandardOutput.BaseStream, cancellation) is { } frame)
            {
                try
                {
                    if (owned != child || id != instance) continue;
                    if (frame.Type == IpcMessageType.BinaryChunk)
                    {
                        if (enabled && frame.RequestId == transfer)
                        {
                            string text = new UTF8Encoding(false, true).GetString(frame.Payload);
                            Record(new { kind = "PublicFixtureComparison", instance, epoch, transaction = transfer, matches = text == fixture, codeUnits = text.Length, ticks });
                        }
                        continue;
                    }
                    if (frame.Type != IpcMessageType.Ping) throw new InvalidDataException("Unexpected host message.");
                    var item = JsonSerializer.Deserialize<OleProbeEvent>(frame.Payload) ?? throw new InvalidDataException("Missing host event.");
                    if (item.Instance != instance) continue;
                    if (item.Epoch != epoch)
                    {
                        if (item.Phase == "Shown") Record(new { kind = "StartupFocusObservation", item, ticks });
                        continue;
                    }
                    Record(new { item, transaction = frame.RequestId, ticks }); status.Text = $"{item.Phase}: {item.Status} — supervisor ticks {ticks}";
                    if (item.Phase == "Ready") deadline.Complete(instance, epoch);
                    if (item.Phase is "QueryBegin" or "DropBegin")
                    {
                        transfer = item.Phase == "DropBegin" ? frame.RequestId : Guid.Empty;
                        deadlineMilliseconds = 1500;
                        if (!deadline.Begin(frame.RequestId, epoch, TimeSpan.FromMilliseconds(1500))) StopChild("Overlapping native calls rejected");
                    }
                    if (item.Phase is "QueryEnd" or "DropEnd") deadline.Complete(frame.RequestId, epoch);
                }
                finally { CryptographicOperations.ZeroMemory(frame.Payload); }
            }
            if (owned == child) StopChild("Host exited");
        }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException or InvalidDataException or JsonException or InvalidOperationException)
        { if (owned == child) { Record(new { kind = "PipeFailure", code = error.GetType().Name }); StopChild("Host pipe failed"); } }
    }
    private async Task DrainError(Process owned, Guid id, CancellationToken token)
    {
        try { char[] bounded = new char[1024]; while (await owned.StandardError.ReadAsync(bounded.AsMemory(), token) > 0) { if (id == instance) Record(new { kind = "HostStderrDiscarded" }); Array.Clear(bounded); } }
        catch (Exception error) when (error is IOException or OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        { if (!token.IsCancellationRequested && id == instance) Record(new { kind = "StderrReadFailure", code = error.GetType().Name }); }
    }
    private bool StopChild(string reason)
    {
        var owned = child; child = null; enabled = false; epoch++; transfer = Guid.Empty; deadline.Reset();
        lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
        if (owned is not null)
        {
            try { if (!owned.HasExited) owned.Kill(true); }
            catch (InvalidOperationException) { Record(new { kind = "ProcessAlreadyExited" }); }
            catch (System.ComponentModel.Win32Exception error)
            { child = owned; status.Text = "Host termination failed; restart blocked. Close this owned probe process manually."; Record(new { kind = "TerminationFailed", error.NativeErrorCode, reason, epoch }); return false; }
            owned.Dispose();
        }
        status.Text = reason; Record(new { kind = "TerminationRequested", reason, epoch, ticks }); return true;
    }
}
