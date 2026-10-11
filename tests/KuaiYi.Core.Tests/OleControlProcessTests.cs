using System.Diagnostics;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;
public sealed class OleControlProcessTests
{
    [Theory] [InlineData("Query", "Block")] [InlineData("Drop", "Block")] [InlineData("Query", "Throw")] [InlineData("Drop", "Throw")]
    public async Task CleanupPendingOrFailedKeepsActualChildBoundedUntilConfirmedExit(string kind, string fault)
    {
        using var process = Start(kind, fault); using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10)); var watch = new OleCallWatch();
        var phases = new List<string>();
        try
        {
            while (true)
            {
                var frame = (await IpcFrameCodec.ReadAsync(process.StandardOutput.BaseStream, guard.Token))!;
                var item = JsonSerializer.Deserialize<OleProbeEvent>(frame.Payload)!; phases.Add(item.Phase);
                watch.Accept(frame.RequestId, item.Epoch, item.Phase, item.CleanupCompleted);
                if (item.Phase == (fault == "Block" ? "CleanupBegin" : "CleanupFailed")) break;
            }
            Assert.True(watch.IsArmed); int responsiveTicks = 0;
            while (!watch.Expired) { await Task.Delay(10, guard.Token); responsiveTicks++; }
            var stop = new OwnedHostTermination(new StartedHostLifetime(process));
            Assert.Equal(HostExitState.ExitConfirmed, await stop.StopAsync(TimeSpan.FromSeconds(2))); Assert.True(process.HasExited); Assert.True(stop.CanRestart); Assert.True(responsiveTicks > 0);
            while (await IpcFrameCodec.ReadAsync(process.StandardOutput.BaseStream, guard.Token) is { } frame) phases.Add(JsonSerializer.Deserialize<OleProbeEvent>(frame.Payload)!.Phase);
            Assert.DoesNotContain(kind + "End", phases); Assert.DoesNotContain("CleanupComplete", phases); Assert.Contains("BodyFinished", phases);
            Assert.Equal("", await process.StandardError.ReadToEndAsync(guard.Token));
        }
        finally { if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(guard.Token); } }
    }
    [Theory] [InlineData("Query")] [InlineData("Drop")]
    public async Task CleanIndependentChildPublishesTerminalOnlyAfterCleanup(string kind)
    {
        using var process = Start(kind, "Normal"); using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(10)); var watch = new OleCallWatch(); var phases = new List<string>();
        try
        {
            while (true)
            {
                var frame = (await IpcFrameCodec.ReadAsync(process.StandardOutput.BaseStream, guard.Token))!;
                var item = JsonSerializer.Deserialize<OleProbeEvent>(frame.Payload)!; phases.Add(item.Phase); watch.Accept(frame.RequestId, item.Epoch, item.Phase, item.CleanupCompleted);
                if (item.Phase == kind + "End") { Assert.True(item.CleanupCompleted); break; }
                Assert.True(watch.IsArmed);
            }
            Assert.Equal(new[] { kind + "Begin", "BodyFinished", "CleanupBegin", "CleanupComplete", kind + "End" }, phases); Assert.False(watch.IsArmed);
            process.StandardInput.Close(); await process.WaitForExitAsync(guard.Token); Assert.Equal(0, process.ExitCode);
        }
        finally { if (!process.HasExited) { process.Kill(); await process.WaitForExitAsync(guard.Token); } }
    }
    private static Process Start(string kind, string fault)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string assembly = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tools/KuaiYi.OleControlFixture/bin", configuration, "net10.0/KuaiYi.OleControlFixture.dll"));
        var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet") { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (string arg in new[] { assembly, "--fixture", Guid.NewGuid().ToString("D"), kind, fault }) start.ArgumentList.Add(arg);
        return Process.Start(start) ?? throw new InvalidOperationException("Cannot start owned public control fixture.");
    }
}
