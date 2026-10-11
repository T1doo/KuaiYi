using System.Diagnostics;
using KuaiYi.Contracts;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class NativeProbeTests
{
    [Fact]
    public async Task IndependentStdioProcessExchangesTwoFramesWithoutStdoutLogging()
    {
        using var process = Start(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            Guid first = Guid.NewGuid(), second = Guid.NewGuid();
            await NativeMessagingCodec.WriteAsync(process.StandardInput.BaseStream, new { type = "Ping", requestId = first }, deadline.Token);
            await NativeMessagingCodec.WriteAsync(process.StandardInput.BaseStream, new { type = "Ping", requestId = second }, deadline.Token);
            process.StandardInput.Close();
            foreach (Guid expected in new[] { first, second })
            {
                using var pong = await NativeMessagingCodec.ReadAsync(process.StandardOutput.BaseStream, deadline.Token);
                Assert.NotNull(pong); Assert.Equal("Pong", pong.RootElement.GetProperty("type").GetString());
                Assert.Equal(expected, pong.RootElement.GetProperty("requestId").GetGuid());
            }
            Assert.Null(await NativeMessagingCodec.ReadAsync(process.StandardOutput.BaseStream, deadline.Token));
            await process.WaitForExitAsync(deadline.Token); Assert.Equal(0, process.ExitCode);
            Assert.Equal("", await process.StandardError.ReadToEndAsync(deadline.Token));
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
    }
    [Fact]
    public async Task ProbeCannotDispatchProductCommands()
    {
        using var process = Start(); using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await NativeMessagingCodec.WriteAsync(process.StandardInput.BaseStream, new { type = "SubmitImage", requestId = Guid.NewGuid() }, deadline.Token);
            process.StandardInput.Close();
            using var response = await NativeMessagingCodec.ReadAsync(process.StandardOutput.BaseStream, deadline.Token);
            Assert.Equal("PROBE_COMMAND_REJECTED", response!.RootElement.GetProperty("code").GetString());
            await process.WaitForExitAsync(deadline.Token); Assert.Equal(4, process.ExitCode);
        }
        finally { if (!process.HasExited) { process.Kill(true); await process.WaitForExitAsync(); } }
    }
    private static Process Start()
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        string assembly = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tools/KuaiYi.NativeMessagingProbe/bin", configuration, "net10.0/KuaiYi.NativeMessagingProbe.dll"));
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        info.ArgumentList.Add(assembly); info.ArgumentList.Add("--probe");
        return Process.Start(info) ?? throw new InvalidOperationException("Cannot start Native Messaging probe.");
    }
}
