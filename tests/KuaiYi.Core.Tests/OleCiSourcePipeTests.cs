using System.Text;
using KuaiYi.App;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OleCiSourcePipeTests
{
    [Fact] public async Task ValidPublicMetadataUsesCompleteBoundedLines()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("public-one\r\npublic-two\n"));
        var received = new List<string>(); await OleCiSourcePipe.ReadMetadataAsync(input, received.Add);
        Assert.Equal(new[] { "public-one", "public-two" }, received); Assert.Equal(input.Length, input.Position);
    }
    [Fact] public async Task OversizedFirstLineFailsClosedButConsumesAllLaterFlood()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(new string('x', OleCiSourcePipe.LineLimit + 1) + "\n" + string.Concat(Enumerable.Repeat("public-tail\n", 10000))));
        int received = 0; await Assert.ThrowsAsync<InvalidDataException>(() => OleCiSourcePipe.ReadMetadataAsync(input, _ => received++));
        Assert.Equal(0, received); Assert.Equal(input.Length, input.Position);
    }
    [Fact] public async Task ConsumerFailureDoesNotStopOriginalPipeDrainOrAdmitLaterMetadata()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("invalid\npublic-ready\n"));
        int calls = 0; await Assert.ThrowsAsync<InvalidDataException>(() => OleCiSourcePipe.ReadMetadataAsync(input, _ => { calls++; throw new ArgumentException("PUBLIC_SENTINEL"); }));
        Assert.Equal(1, calls); Assert.Equal(input.Length, input.Position);
    }
    [Fact] public async Task InvalidUtf8NeverReachesConsumerAndStillDrainsToEof()
    {
        using var input = new MemoryStream(new byte[] { 0xff, 10, (byte)'x', 10 });
        int calls = 0; await Assert.ThrowsAsync<InvalidDataException>(() => OleCiSourcePipe.ReadMetadataAsync(input, _ => calls++));
        Assert.Equal(0, calls); Assert.Equal(input.Length, input.Position);
    }
    [Fact] public async Task UnterminatedMetadataIsRejectedInsteadOfReportedReady()
    {
        using var input = new MemoryStream(Encoding.ASCII.GetBytes("public-ready"));
        int calls = 0; await Assert.ThrowsAsync<InvalidDataException>(() => OleCiSourcePipe.ReadMetadataAsync(input, _ => calls++)); Assert.Equal(0, calls);
    }
    [Fact] public async Task StderrIsFullyDiscardedAndStillFailsTheExistingEmptyErrorRequirement()
    {
        using var input = new MemoryStream(new byte[1024 * 1024]);
        Assert.False(await OleCiSourcePipe.DrainErrorsAsync(input)); Assert.Equal(input.Length, input.Position);
        using var empty = new MemoryStream(); Assert.True(await OleCiSourcePipe.DrainErrorsAsync(empty));
    }
    [Fact] public async Task OriginalReadRemainsPendingAfterItsWaiterCancelsUntilActualEof()
    {
        using var input = new EofHeldStream(); Task original = OleCiSourcePipe.ReadMetadataAsync(input, _ => throw new InvalidOperationException());
        await input.Started.Task; using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => original.WaitAsync(stop.Token)); Assert.False(original.IsCompleted);
        input.Eof.TrySetResult(); await original; Assert.True(original.IsCompletedSuccessfully);
    }
    private sealed class EofHeldStream : Stream
    {
        public readonly TaskCompletionSource Started = new(TaskCreationOptions.RunContinuationsAsynchronously), Eof = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) { Started.TrySetResult(); await Eof.Task; return 0; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer,int offset,int count) => throw new NotSupportedException(); public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException();
    }
}
