using System.Diagnostics;
using System.Text;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Windows;

if(args.Length==1&&args[0]=="--ci-range-geometry")return KuaiYi.CompatibilityProbe.SelectionGeometryContracts.Run();
if (args.Length == 5 && args[0] is "--ci-installed-selection" or "--ci-selection-diagnostic" or "--ci-com-selection-diagnostic")
    return await KuaiYi.CompatibilityProbe.InstalledSelectionProbe.RunAsync(args[1], args[2], args[3], args[4],args[0]!="--ci-installed-selection",args[0]=="--ci-com-selection-diagnostic");

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: KuaiYi.CompatibilityProbe.exe <CaptureBroker.exe> <public-fixture.txt>. Select that fixture in the target app within five seconds. No model calls.");
    return 2;
}
string broker = Path.GetFullPath(args[0]);
string fixture = await File.ReadAllTextAsync(args[1], new UTF8Encoding(false, true));
if (fixture.Length > 100_002 || string.IsNullOrWhiteSpace(fixture) || !File.Exists(broker)) return 2;
Console.WriteLine("Five-second preparation window; only diagnostic metadata is printed.");
await Task.Delay(TimeSpan.FromSeconds(5));
nint hwnd = NativeDesktop.GetForegroundWindow();
NativeDesktop.GetWindowThreadProcessId(hwnd, out uint pid);
if (!NativeDesktop.GetCursorPos(out var point) || hwnd == 0 || pid == 0) return 3;
Guid id = Guid.NewGuid();
var info = new ProcessStartInfo(broker) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
foreach (string argument in new[] { hwnd.ToInt64().ToString(), pid.ToString(), point.X.ToString(), point.Y.ToString(), id.ToString() }) info.ArgumentList.Add(argument);
using var process = Process.Start(info) ?? throw new InvalidOperationException("Cannot start Broker.");
using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(600));
try
{
    var first = await IpcFrameCodec.ReadAsync(process.StandardOutput.BaseStream, deadline.Token);
    var second = await IpcFrameCodec.ReadAsync(process.StandardOutput.BaseStream, deadline.Token);
    await process.WaitForExitAsync(deadline.Token);
    if (process.ExitCode != 0 || first?.RequestId != id || second?.RequestId != id || first.Type != IpcMessageType.Ping || second.Type != IpcMessageType.BinaryChunk) return 4;
    using var metadata = JsonDocument.Parse(first.Payload);
    string text = new UTF8Encoding(false, true).GetString(second.Payload);
    string? status = metadata.RootElement.GetProperty("Status").GetString();
    bool foregroundUnchanged = NativeDesktop.GetForegroundWindow() == hwnd;
    bool matches = status == "Success" && foregroundUnchanged && text.Replace("\r\n", "\n", StringComparison.Ordinal).Trim() == fixture.Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
    Console.WriteLine(JsonSerializer.Serialize(new { status, matchesFixture = matches,
        rangeCount = metadata.RootElement.GetProperty("RangeCount").GetInt32(), candidateCount = metadata.RootElement.GetProperty("CandidateCount").GetInt32(),
        elapsedMs = metadata.RootElement.GetProperty("ElapsedMs").GetDouble(), processId = pid, foregroundUnchanged }));
    return matches ? 0 : 5;
}
catch (OperationCanceledException) when (deadline.IsCancellationRequested)
{
    Console.WriteLine("{\"status\":\"HardTimeout\",\"matchesFixture\":false}");
    return 6;
}
finally
{
    if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync(); }
}
