using System.Text;
using System.Diagnostics;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OleStartupDiagnosticsTests
{
    [Fact] public void OnlyCanonicalWhitelistFieldsRoundTrip()
    {
        foreach (var stage in Enum.GetValues<OleStartupStage>())
        foreach (var code in Enum.GetValues<OleExceptionCode>())
        {
            var expected = new OleStartupDiagnostic(stage, code, code == OleExceptionCode.None ? 0 : int.MinValue);
            Assert.True(OleStartupDiagnostics.TryParse(Encoding.ASCII.GetBytes(OleStartupDiagnostics.Format(expected)), out var actual));
            Assert.Equal(expected, actual);
        }
    }
    [Fact] public void RejectsUnknownDuplicateNumericExtraAndSecretFields()
    {
        foreach (string line in new[] { "KYOD1|PUBLIC_SENTINEL|None|0", "KYOD1|Entry|PUBLIC_SENTINEL|0", "KYOD1|0|None|0", "KYOD1|Entry|0|0", "KYOD1|Entry|None|0|nonce=PUBLIC_SENTINEL", "KYOD1|Entry|None|0\npath=PUBLIC_SENTINEL", "KYOD1|Entry|None|1", "KYOD1|Entry|Io|01", "KYOD1|Entry|Io|2147483648", "KYOD1|Entry|Io|+1", "KYOD1|Entry|Io|-0", "KYOD1|Entry|Io|0 ", "KYOD1|Entry|None|0|Entry", "KYOD1|Entry|Io|PUBLIC_SENTINEL", "KYOD1|Entry|Io|0\0" })
        { Assert.False(OleStartupDiagnostics.TryParse(Encoding.UTF8.GetBytes(line), out var result)); Assert.Null(result); }
    }
    [Fact] public void ExceptionMessagesAndCustomTypeNamesNeverEnterProjection()
    {
        var projected = OleStartupDiagnostics.Project(OleStartupStage.ParentVerify, new PublicSentinelException("PUBLIC_SENTINEL path nonce content"));
        Assert.Equal(OleExceptionCode.Other, projected.Exception);
        Assert.DoesNotContain("SENTINEL", OleStartupDiagnostics.Format(projected));
        Assert.Equal(OleExceptionCode.FileMissing, OleStartupDiagnostics.Code(new FileNotFoundException("PUBLIC_SENTINEL", "PUBLIC_SENTINEL")));
        Assert.Equal(OleExceptionCode.Com, OleStartupDiagnostics.Code(new System.Runtime.InteropServices.COMException("PUBLIC_SENTINEL", unchecked((int)0x80004005))));
    }
    [Fact] public async Task ChunkBoundariesCrLfAndMalformedTailCannotLeakOutput()
    {
        string valid = OleStartupDiagnostics.Format(new(OleStartupStage.ParentVerify, OleExceptionCode.InvalidOperation, unchecked((int)0x80131509)));
        using var source = new ChunkStream(Encoding.UTF8.GetBytes("PUBLIC_SENTINEL\n" + valid + "\r\n" + "KYOD1|Entry|None|0"), 1);
        var drain = new OleStartupDiagnosticDrain(); await drain.RunAsync(source);
        Assert.Single(drain.Snapshot); Assert.Equal(OleStartupStage.ParentVerify, drain.Snapshot[0].Stage);
        Assert.Equal(source.Length, source.Position); Assert.Equal(OleExceptionCode.None, drain.Failure);
    }
    [Fact] public async Task OversizedAndFloodedOutputIsFullyDrainedWithinRetentionLimit()
    {
        string valid = OleStartupDiagnostics.Format(new(OleStartupStage.Entry, OleExceptionCode.None, 0));
        using var source = new ChunkStream(Encoding.UTF8.GetBytes(new string('x', 1024 * 1024) + "\n" + string.Concat(Enumerable.Repeat(valid + "\n", 1000))), 4096);
        var drain = new OleStartupDiagnosticDrain(); await drain.RunAsync(source);
        Assert.Equal(OleStartupDiagnostics.RecordLimit, drain.Snapshot.Length); Assert.Equal(source.Length, source.Position);
        Assert.All(drain.Snapshot, item => Assert.Equal(OleStartupStage.Entry, item.Stage));
    }
    [Fact] public async Task CancelledReadSettlesWithoutRecordingRawException()
    {
        using var cancellation = new CancellationTokenSource(); using var source = new PendingStream();
        var drain = new OleStartupDiagnosticDrain(); Task read = drain.RunAsync(source, cancellation.Token);
        Assert.False(read.IsCompleted); cancellation.Cancel(); await read.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Empty(drain.Snapshot); Assert.Equal(OleExceptionCode.Cancelled, drain.Failure);
    }
    [Fact] public async Task IoFailureStoresOnlyFixedCode()
    {
        using var source = new FailingStream(); var drain = new OleStartupDiagnosticDrain(); await drain.RunAsync(source);
        Assert.Empty(drain.Snapshot); Assert.Equal(OleExceptionCode.Io, drain.Failure);
    }
    [Fact] public async Task OriginalReadIgnoringCancellationRemainsPendingUntilActualEof()
    {
        using var source = new IgnoringCancellationStream(); using var stop = new CancellationTokenSource();
        var drain = new OleStartupDiagnosticDrain(); Task read = drain.RunAsync(source, stop.Token);
        await source.Entered.Task; stop.Cancel(); Assert.False(read.IsCompleted);
        source.Release.TrySetResult(); await read.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(OleExceptionCode.None, drain.Failure); Assert.Empty(drain.Snapshot);
    }
    [Fact] public async Task OriginalDiagnosticSettlementCannotBeReleasedByWaiterTimeoutOrCancellation()
    {
        Guid id = Guid.NewGuid(); var activation = new Activation(id); var registrar = new Registrar();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new OleReceiverClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", registrar, activation, FixturePath(), new Starter("startup-diagnostic-fail"))
        { AfterDiagnosticReadForTest = () => release.Task };
        var run = client.RunAsync(Hello(id), _ => Assert.Fail("Failed child must not become Ready."), (_, _, _, _) => Task.CompletedTask);
        try
        {
            Assert.IsType<EndOfStreamException>(await Record.ExceptionAsync(() => run.WaitAsync(TimeSpan.FromSeconds(4))));
            Assert.Equal(3, client.OwnedExitCode); Assert.False(client.DiagnosticReadSettled);
            Assert.False(await client.TryFinalizeAsync()); Assert.False(activation.Disposed);
            release.TrySetResult();
            await UntilDiagnosticSettled(client).WaitAsync(TimeSpan.FromSeconds(2));
            Assert.True(await client.TryFinalizeAsync(true)); Assert.True(activation.Disposed);
        }
        finally { release.TrySetResult(); await Cleanup(client, run); }
    }
    [Fact] public async Task RealOwnedChildStderrFloodCannotDeadlockHandshake()
    {
        Guid id = Guid.NewGuid(); var registrar = new Registrar(); var activation = new Activation(id);
        var client = Client("startup-diagnostic-flood", registrar, activation);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = client.RunAsync(Hello(id), e => { if (e.Phase == "Ready") ready.TrySetResult(); }, (_, _, _, _) => Task.CompletedTask);
        try
        {
            await ready.Task.WaitAsync(TimeSpan.FromSeconds(4)); Assert.True(client.IsReady);
            Assert.Single(client.StartupDiagnostics); Assert.Equal(OleStartupStage.Entry, client.StartupDiagnostics[0].Stage);
        }
        finally { await Cleanup(client, run); }
        Assert.True(client.DiagnosticReadSettled); Assert.Equal(HostExitState.ExitConfirmed, client.ExitState);
    }
    [Fact] public async Task RealOwnedStartupFailureProjectsStageExitCodeAndSettlement()
    {
        Guid id = Guid.NewGuid(); var client = Client("startup-diagnostic-fail", new Registrar(), new Activation(id));
        var run = client.RunAsync(Hello(id), _ => Assert.Fail("Failed child must not become Ready."), (_, _, _, _) => Task.CompletedTask);
        try
        {
            var failure = await Record.ExceptionAsync(() => run.WaitAsync(TimeSpan.FromSeconds(4)));
            Assert.IsType<EndOfStreamException>(failure); Assert.Equal(OleStartupStage.ParentHelloRead, client.StartupFailure!.Stage);
            Assert.Equal(3, client.OwnedExitCode); Assert.True(client.DiagnosticReadSettled);
            var item = Assert.Single(client.StartupDiagnostics); Assert.Equal(OleStartupStage.ParentVerify, item.Stage);
            Assert.Equal(OleExceptionCode.InvalidOperation, item.Exception);
            Assert.DoesNotContain("SENTINEL", OleStartupDiagnostics.Format(item));
        }
        finally { await Cleanup(client, run); }
    }
    private static OleReceiverHello Hello(Guid id) => new(id, 1, Environment.ProcessId, 1, new byte[32], OleReceiverWire.ActivationName(id));
    private static OleReceiverClient Client(string mode, Registrar registrar, Activation activation)
    {
        return new(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", registrar, activation, FixturePath(), new Starter(mode));
    }
    private static string FixturePath() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../tools/KuaiYi.OleReceiverFixture/bin/Release/net10.0/KuaiYi.OleReceiverFixture.dll"));
    private static async Task UntilDiagnosticSettled(OleReceiverClient client) { while (!client.DiagnosticReadSettled) await Task.Delay(5); }
    private static async Task Cleanup(OleReceiverClient client, Task run)
    {
        client.Cancel(); await Record.ExceptionAsync(() => run.WaitAsync(TimeSpan.FromSeconds(6)));
        Assert.True(await client.TryFinalizeAsync(true)); await client.DisposeAsync();
    }
    private sealed class Starter(string mode) : IOwnedProcessStarter
    {
        public Process Start(ProcessStartInfo info) { info.ArgumentList.Add(mode); return SystemOwnedProcessStarter.Instance.Start(info); }
    }
    private sealed class Activation(Guid instance) : IOleActivationLease
    {
        public bool Disposed;
        public string Name => OleReceiverWire.ActivationName(instance);
        public void Set(long epoch, bool enabled) { }
        public void Dispose() => Disposed = true;
    }
    private sealed class Registrar : IOleTargetRegistrar
    {
        public Task RegisterAsync(long hwnd, byte[] packet, CancellationToken cancellation) { Array.Clear(packet); return Task.CompletedTask; }
        public ValueTask<bool> TryFinalizeAsync() => ValueTask.FromResult(true);
        public bool IsSettledAndRevoked => true;
    }
    private sealed class PublicSentinelException(string message) : Exception(message);
    private sealed class ChunkStream(byte[] bytes, int maximum) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
            => base.ReadAsync(buffer[..Math.Min(buffer.Length, maximum)], cancellation);
    }
    private sealed class PendingStream : MemoryStream
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        { await Task.Delay(Timeout.Infinite, cancellation); return 0; }
    }
    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
            => ValueTask.FromException<int>(new IOException("PUBLIC_SENTINEL stderr path nonce"));
    }
    private sealed class IgnoringCancellationStream : MemoryStream
    {
        public readonly TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously), Release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        { Entered.TrySetResult(); await Release.Task; return 0; }
    }
}
