using System.Security.Cryptography;
using System.Text;
using KuaiYi.Contracts;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class NativeMessagingTests
{
    [Fact]
    public async Task Utf8JsonUsesNativeByteLengthAndMultipleFrames()
    {
        using var stream = new MemoryStream();
        await NativeMessagingCodec.WriteAsync(stream, new { type = "Ping", text = "中文🌍" });
        await NativeMessagingCodec.WriteAsync(stream, new { type = "Ping" });
        uint length = BitConverter.ToUInt32(stream.ToArray());
        Assert.Equal("{"[0], (char)stream.ToArray()[4]); Assert.True(length > 0);
        stream.Position = 0;
        using var first = await NativeMessagingCodec.ReadAsync(stream);
        Assert.Equal("中文🌍", first!.RootElement.GetProperty("text").GetString()); Assert.Equal(4 + length, stream.Position);
        using var second = await NativeMessagingCodec.ReadAsync(stream); Assert.NotNull(second);
        Assert.Null(await NativeMessagingCodec.ReadAsync(stream));
    }
    [Theory]
    [InlineData(0u)] [InlineData(262145u)] [InlineData(2147483648u)] [InlineData(4294967295u)]
    public async Task RejectsUnsignedLengthBeforePayloadRead(uint length)
    {
        using var stream = new MemoryStream(BitConverter.GetBytes(length));
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NativeMessagingCodec.ReadAsync(stream));
        Assert.Equal(4, stream.Position);
    }
    [Theory]
    [InlineData("[]")] [InlineData("null")] [InlineData("\"string\"")]
    public async Task RejectsNonObjectJson(string json)
    {
        byte[] payload = Encoding.UTF8.GetBytes(json);
        using var stream = new MemoryStream([.. BitConverter.GetBytes((uint)payload.Length), .. payload]);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NativeMessagingCodec.ReadAsync(stream));
    }
    [Fact]
    public async Task RawUnicodeAndInvalidUtf8AreCheckedAtWireBoundary()
    {
        byte[] payload = Encoding.UTF8.GetBytes("{\"text\":\"中文🌍\"}");
        using var valid = new MemoryStream([.. BitConverter.GetBytes((uint)payload.Length), .. payload]);
        using var document = await NativeMessagingCodec.ReadAsync(valid);
        Assert.Equal("中文🌍", document!.RootElement.GetProperty("text").GetString());
        byte[] invalid = [(byte)'{', (byte)'"', (byte)'s', (byte)'"', (byte)':', (byte)'"', 255, (byte)'"', (byte)'}'];
        using var corrupt = new MemoryStream([.. BitConverter.GetBytes((uint)invalid.Length), .. invalid]);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NativeMessagingCodec.ReadAsync(corrupt));
    }
    [Fact]
    public async Task ByteLimitAppliesToMultibyteTextAndNeverWritesPartialFrame()
    {
        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NativeMessagingCodec.WriteAsync(stream, new { text = new string('译', 100_000) }));
        Assert.Equal(0, stream.Length);
    }
    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(5)]
    public async Task PartialFrameIsFailure(int length)
    {
        using var stream = new MemoryStream(); await NativeMessagingCodec.WriteAsync(stream, new { type = "Ping" });
        using var partial = new MemoryStream(stream.ToArray()[..length]);
        await Assert.ThrowsAsync<EndOfStreamException>(async () => await NativeMessagingCodec.ReadAsync(partial));
    }
    [Fact]
    public void CommitRequiresEveryByteAndMatchingDigestAndTransfersOwnedMemory()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(210_000); Guid id = Guid.NewGuid();
        using var assembler = new ImageTransferAssembler(); assembler.Begin(id, bytes.Length, "image/png", Convert.ToHexString(SHA256.HashData(bytes)));
        assembler.Chunk(id, 0, bytes.AsSpan(0, 100_000)); assembler.Chunk(id, 1, bytes.AsSpan(100_000));
        using var image = assembler.Commit(id); Assert.Equal(bytes, image.Bytes.ToArray()); Assert.Equal(id, image.TransferId);
        ReadOnlyMemory<byte> owned = image.Bytes; image.Dispose(); Assert.All(owned.ToArray(), b => Assert.Equal(0, b));
        Assert.Throws<ObjectDisposedException>(() => image.Bytes); Assert.Throws<InvalidDataException>(() => assembler.Commit(id));
    }
    [Theory]
    [InlineData("reorder")] [InlineData("duplicate")] [InlineData("wrong-id")] [InlineData("short")] [InlineData("digest")] [InlineData("oversize-chunk")]
    public void InvalidTransferAbortsWithoutProducingImage(string kind)
    {
        byte[] bytes = [1, 2, 3]; Guid id = Guid.NewGuid(); using var assembler = new ImageTransferAssembler();
        assembler.Begin(id, 3, "image/png", Convert.ToHexString(SHA256.HashData(kind == "digest" ? new byte[] { 9 } : bytes)));
        Assert.Throws<InvalidDataException>(() =>
        {
            if (kind == "reorder") assembler.Chunk(id, 1, bytes);
            else if (kind == "wrong-id") assembler.Chunk(Guid.NewGuid(), 0, bytes);
            else if (kind == "oversize-chunk") assembler.Chunk(id, 0, new byte[ImageTransferAssembler.ChunkLimit + 1]);
            else { assembler.Chunk(id, 0, kind == "short" ? bytes.AsSpan(0, 1) : bytes); if (kind == "duplicate") assembler.Chunk(id, 0, bytes); else assembler.Commit(id); }
        });
        Assert.Throws<InvalidDataException>(() => assembler.Commit(id));
    }
    [Fact]
    public void DeclaredLimitAndAbsoluteDeadlineAreEnforced()
    {
        var clock = new TestClock(); Guid id = Guid.NewGuid(); using var assembler = new ImageTransferAssembler(clock);
        string hash = Convert.ToHexString(SHA256.HashData(new byte[] { 1 }));
        Assert.Throws<InvalidDataException>(() => assembler.Begin(id, ImageTransferAssembler.ImageLimit + 1, "image/png", hash));
        assembler.Begin(id, 1, "image/png", hash); clock.Advance(TimeSpan.FromSeconds(30));
        Assert.Throws<InvalidDataException>(() => assembler.Chunk(id, 0, new byte[] { 1 }));
        assembler.Begin(id, 1, "image/png", hash); assembler.Chunk(id, 0, new byte[] { 1 }); using var image = assembler.Commit(id); Assert.Single(image.Bytes.ToArray());
    }
    private sealed class TestClock : TimeProvider
    {
        private long ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => ticks;
        public void Advance(TimeSpan delta) => ticks += delta.Ticks;
    }
}
