using System.Buffers.Binary;
using System.Text;
using KuaiYi.Contracts;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class IpcTests
{
    [Fact]
    public async Task Utf8RoundtripAndCleanEof()
    {
        var frame = new IpcFrame(IpcMessageType.Ping, Guid.NewGuid(), Encoding.UTF8.GetBytes("{\"文字\":\"测试\"}"));
        using var stream = new MemoryStream();
        await IpcFrameCodec.WriteAsync(stream, frame);
        stream.Position = 0;
        var decoded = await IpcFrameCodec.ReadAsync(stream);
        Assert.NotNull(decoded);
        Assert.Equal(frame.Type, decoded.Type);
        Assert.Equal(frame.RequestId, decoded.RequestId);
        Assert.Equal(frame.Payload, decoded.Payload);
        Assert.Null(await IpcFrameCodec.ReadAsync(stream));
    }
    [Theory]
    [InlineData(65537u)]
    [InlineData(2147483648u)]
    [InlineData(4294967295u)]
    public async Task ForgedLengthsAreRejectedBeforeReadingOrAllocatingPayload(uint length)
    {
        using var stream = new MemoryStream();
        await IpcFrameCodec.WriteAsync(stream, new(IpcMessageType.Ping, Guid.NewGuid(), []));
        byte[] bytes = stream.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), length);
        using var forged = new MemoryStream(bytes);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await IpcFrameCodec.ReadAsync(forged));
        Assert.Equal(IpcFrameCodec.HeaderLength, forged.Position);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(27)]
    [InlineData(30)]
    public async Task TruncatedHeadersAndPayloadAreNotCleanDisconnects(int bytes)
    {
        using var encoded = new MemoryStream();
        await IpcFrameCodec.WriteAsync(encoded, new(IpcMessageType.Ping, Guid.NewGuid(), new byte[20]));
        using var stream = new MemoryStream(encoded.ToArray()[..bytes]);
        await Assert.ThrowsAsync<EndOfStreamException>(async () => await IpcFrameCodec.ReadAsync(stream));
    }
}
