using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using Xunit;

namespace KuaiYi.Core.Tests;
public sealed class OleCleanupTests
{
    [Fact] public void WorkCannotFinishBeforeReleaseAndAllCleanupErrorsArePreserved()
    {
        var order = new List<string>(); var scope = new OleCleanupScope();
        scope.Own(() => order.Add("first released"));
        scope.Own(() => { order.Add("second released"); throw new IOException("public release fault"); });
        var work = new InvalidDataException("public body fault");
        var error = Assert.Throws<OleCleanupException>(() => scope.Run<int>(() => { order.Add("body"); throw work; }));
        Assert.Equal(new[] { "body", "second released", "first released" }, order);
        Assert.Contains(work, error.InnerExceptions); Assert.Contains(error.InnerExceptions, x => x is IOException);
        Assert.False(scope.CleanupCompleted);
        Assert.Throws<OleCleanupException>(scope.Dispose); Assert.Equal(3, order.Count);
    }
    [Fact] public void ReleasingLeaseEarlyDoesNotCauseAnotherNativeRelease()
    {
        int calls = 0; var scope = new OleCleanupScope(); var lease = scope.Own(() => calls++);
        lease.Dispose(); scope.Run(() => 1); Assert.Equal(1, calls); Assert.True(scope.CleanupCompleted);
    }
    [Fact] public void FailedEarlyReleaseCannotBeReclassifiedAsClean()
    {
        int calls = 0; var scope = new OleCleanupScope(); var lease = scope.Own(() => { calls++; throw new IOException("public fault"); });
        Assert.Throws<IOException>(lease.Dispose); Assert.Throws<OleCleanupException>(scope.Dispose);
        Assert.False(scope.CleanupCompleted); Assert.Equal(1, calls);
    }
    [Fact] public void CleanupLeaseRejectsUnmarshaledThreadTransfer()
    {
        var scope = new OleCleanupScope(); int released = 0; var lease = scope.Own(() => released++);
        Exception? failure = null;
        var thread = new Thread(() => { try { lease.Dispose(); } catch (Exception error) { failure = error; } });
        thread.Start(); Assert.True(thread.Join(TimeSpan.FromSeconds(2))); Assert.IsType<InvalidOperationException>(failure); Assert.Equal(0, released);
        scope.Dispose(); Assert.Equal(1, released); Assert.True(scope.CleanupCompleted);
    }
    [Theory] [InlineData("Query")] [InlineData("Drop")] [InlineData("Shutdown")]
    public void OnlyMatchingCleanTerminalCompletesActiveKind(string kind)
    {
        Guid id = Guid.NewGuid(); var clock = new Clock(); var watch = new OleCallWatch(clock);
        watch.Accept(id, 3, kind + "Begin", false);
        Assert.Equal(OleWatchDecision.Ignored, watch.Accept(Guid.NewGuid(), 3, kind + "End", true));
        Assert.Equal(OleWatchDecision.Ignored, watch.Accept(id, 4, kind + "End", true));
        watch.Accept(id, 3, (kind == "Drop" ? "Query" : "Drop") + "End", true); Assert.True(watch.IsArmed);
        watch.Accept(id, 3, "CleanupFailed", false); Assert.True(watch.IsArmed);
        Assert.False(watch.CanReadPayload(id, 3));
        watch.Accept(id, 3, "CleanupComplete", true); Assert.Equal(kind == "Drop", watch.CanReadPayload(id, 3));
        Assert.Equal(OleWatchDecision.Completed, watch.Accept(id, 3, kind + "End", true)); Assert.False(watch.IsArmed); Assert.False(watch.CanReadPayload(id, 3));
    }
    [Theory] [InlineData("Query")] [InlineData("Drop")] [InlineData("Shutdown")]
    public void OverdueCleanupCannotBeatTheWatchdogTick(string kind)
    {
        Guid id = Guid.NewGuid(); var clock = new Clock(); var watch = new OleCallWatch(clock);
        watch.Accept(id, 3, kind + "Begin", false); clock.Value = 1499;
        watch.Accept(id, 3, "CleanupBegin", false); watch.Accept(id, 3, kind + "Begin", false); clock.Value++;
        Assert.Equal(OleWatchDecision.Rejected, watch.Accept(id, 3, "CleanupComplete", true));
        Assert.Equal(OleWatchDecision.Rejected, watch.Accept(id, 3, kind + "End", true)); Assert.True(watch.Expired); Assert.False(watch.CanReadPayload(id, 3));
    }
    [Fact] public void DropCannotPublishBeforeCleanupAndLaterFailureRevokesPermission()
    {
        Guid id = Guid.NewGuid(); var watch = new OleCallWatch(); watch.Accept(id, 3, "DropBegin", false);
        Assert.Equal(OleWatchDecision.Progress, watch.Accept(id, 3, "DropEnd", true)); Assert.True(watch.IsArmed); Assert.False(watch.CanReadPayload(id, 3));
        watch.Accept(id, 3, "CleanupComplete", true); Assert.True(watch.CanReadPayload(id, 3));
        watch.Accept(id, 3, "CleanupFailed", false); Assert.False(watch.CanReadPayload(id, 3));
        Assert.Equal(OleWatchDecision.Progress, watch.Accept(id, 3, "DropEnd", true)); Assert.True(watch.IsArmed);
    }
    [Theory] [InlineData("Query")] [InlineData("Drop")] [InlineData("Shutdown")]
    public async Task LegacyEndWithoutCleanupConfirmationDoesNotRevokeDeadline(string kind)
    {
        Guid id = Guid.NewGuid(), instance = Guid.NewGuid(); var watch = new OleCallWatch();
        foreach (string phase in new[] { kind + "Begin", kind + "End" })
        {
            using var stream = new MemoryStream();
            // The old host writes no CleanupCompleted property. Exercise the actual frame/JSON receiver.
            await IpcFrameCodec.WriteAsync(stream, new(IpcMessageType.Ping, id, JsonSerializer.SerializeToUtf8Bytes(new { Instance = instance, Epoch = 2, Phase = phase, Status = "public fixture" })));
            stream.Position = 0; var frame = (await IpcFrameCodec.ReadAsync(stream))!;
            var item = JsonSerializer.Deserialize<OleProbeEvent>(frame.Payload)!;
            watch.Accept(frame.RequestId, item.Epoch, item.Phase, item.CleanupCompleted);
        }
        Assert.True(watch.IsArmed);
    }
    private sealed class Clock : TimeProvider { public long Value; public override long TimestampFrequency => 1000; public override long GetTimestamp() => Value; }
}
