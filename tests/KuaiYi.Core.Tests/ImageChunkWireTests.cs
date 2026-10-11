using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KuaiYi.Contracts;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class ImageChunkWireTests
{
    [Fact]
    public async Task MaximumDeclaredChunkFitsActualJsonFrameAndCommitsImage()
    {
        Guid id = Guid.NewGuid(); byte[] bytes = new byte[ImageTransferAssembler.ChunkLimit];
        using var assembler = new ImageTransferAssembler(); assembler.Begin(id, bytes.Length, "image/png", Convert.ToHexString(SHA256.HashData(bytes)));
        using var wire = new MemoryStream();
        await NativeMessagingCodec.WriteAsync(wire, new { type = "Chunk", transferId = id.ToString("D"), index = 0, data = Convert.ToBase64String(bytes) });
        Assert.InRange(wire.Length, 5, NativeMessagingCodec.MessageLimit + 4);
        wire.Position = 0; using var message = await NativeMessagingCodec.ReadAsync(wire);
        var root = message!.RootElement;
        assembler.Chunk(root.GetProperty("transferId").GetGuid(), root.GetProperty("index").GetInt32(), Convert.FromBase64String(root.GetProperty("data").GetString()!));
        using var image = assembler.Commit(id); Assert.Equal(bytes, image.Bytes.ToArray());
    }
    [Theory]
    [InlineData("zeros")] [InlineData("pluses")] [InlineData("random")]
    public async Task CanonicalMaximumChunkWithWorstBase64CharactersCommits(string pattern)
    {
        Guid id = Guid.NewGuid(); byte[] bytes = new byte[ImageTransferAssembler.ChunkLimit];
        if (pattern == "random") RandomNumberGenerator.Fill(bytes);
        if (pattern == "pluses") for (int i = 0; i < bytes.Length; i++) bytes[i] = (byte)((i % 3) switch { 0 => 251, 1 => 239, _ => 190 });
        using var assembler = Begin(id, bytes); using var wire = new MemoryStream();
        await ImageChunkWire.WriteAsync(wire, id, 0, bytes); Assert.InRange(wire.Length, 5, NativeMessagingCodec.MessageLimit + 4);
        if (pattern == "pluses") { Assert.Contains("++++", Encoding.UTF8.GetString(wire.ToArray())); Assert.DoesNotContain("\\u002B", Encoding.UTF8.GetString(wire.ToArray())); }
        wire.Position = 0; await ImageChunkWire.ReadAndAppendAsync(wire, assembler);
        using var image = assembler.Commit(id); Assert.Equal(bytes, image.Bytes.ToArray());
    }
    [Fact]
    public async Task MaximumPlusOneIsRejectedByBothSenderAndReceiverWithoutPartialFrame()
    {
        Guid id = Guid.NewGuid(); byte[] bytes = new byte[ImageTransferAssembler.ChunkLimit + 1]; using var wire = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ImageChunkWire.WriteAsync(wire, id, 0, bytes)); Assert.Equal(0, wire.Length);
        await NativeMessagingCodec.WriteAsync(wire, new { type = "Chunk", transferId = id.ToString("D"), index = 0, data = Convert.ToBase64String(bytes) });
        using var assembler = Begin(id, bytes); wire.Position = 0;
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ImageChunkWire.ReadAndAppendAsync(wire, assembler));
        Assert.Throws<InvalidDataException>(() => assembler.Commit(id));
    }
    [Fact]
    public async Task LongestCanonicalIdAndIntIndexFitFrameButWrongOrderCannotCommit()
    {
        Guid id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"); byte[] bytes = new byte[ImageTransferAssembler.ChunkLimit];
        using var wire = new MemoryStream(); await ImageChunkWire.WriteAsync(wire, id, int.MaxValue, bytes);
        Assert.InRange(wire.Length, 5, NativeMessagingCodec.MessageLimit + 4); wire.Position = 0;
        using var assembler = Begin(id, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ImageChunkWire.ReadAndAppendAsync(wire, assembler));
        Assert.Throws<InvalidDataException>(() => assembler.Commit(id));
    }
    [Theory]
    [InlineData("long-id")] [InlineData("index-overflow")] [InlineData("unicode-type")] [InlineData("unicode-extra-field")]
    public async Task InvalidLongOrMultibyteHeadersNeverReachImageCommit(string kind)
    {
        Guid id = Guid.NewGuid(); byte[] bytes = new byte[ImageTransferAssembler.ChunkLimit];
        var message = new Dictionary<string, object> { ["type"] = "Chunk", ["transferId"] = id.ToString("D"), ["index"] = 0, ["data"] = Convert.ToBase64String(bytes) };
        if (kind == "long-id") message["transferId"] = new string('a', 256);
        if (kind == "index-overflow") message["index"] = (long)int.MaxValue + 1;
        if (kind == "unicode-type") message["type"] = "分片🌍";
        if (kind == "unicode-extra-field") message["多字节🌍"] = new string('译', 500);
        using var wire = new MemoryStream(); await NativeMessagingCodec.WriteAsync(wire, message);
        Assert.InRange(wire.Length, 5, NativeMessagingCodec.MessageLimit + 4);
        wire.Position = 0; using var assembler = Begin(id, bytes);
        await Assert.ThrowsAsync<InvalidDataException>(async () => await ImageChunkWire.ReadAndAppendAsync(wire, assembler));
        Assert.Throws<InvalidDataException>(() => assembler.Commit(id));
    }
    [Fact]
    public async Task Utf8EnvelopeBudgetCountsActualMultibyteBytes()
    {
        Guid id = Guid.NewGuid(); string data = Convert.ToBase64String(new byte[ImageTransferAssembler.ChunkLimit]);
        var message = new { type = "Chunk", transferId = id.ToString("D"), index = int.MaxValue, data, label = new string('译', 1000) };
        using var wire = new MemoryStream();
        await Assert.ThrowsAsync<InvalidDataException>(async () => await NativeMessagingCodec.WriteAsync(wire, message)); Assert.Equal(0, wire.Length);
        using var exact = new MemoryStream();
        await NativeMessagingCodec.WriteAsync(exact, new { type = "分片🌍" });
        byte[] actual = exact.ToArray()[4..]; string serialized = Encoding.UTF8.GetString(actual);
        Assert.Equal((uint)actual.Length, BitConverter.ToUInt32(exact.ToArray())); Assert.True(actual.Length > serialized.Length);
        Assert.Contains("分片", serialized); using var parsed = JsonDocument.Parse(actual); Assert.Equal("分片🌍", parsed.RootElement.GetProperty("type").GetString());
    }
    [Fact]
    public async Task FullTwentyMiBImageUsesBoundedJsonChunksAndMatchingCommitDigest()
    {
        Guid id = Guid.NewGuid(); byte[] bytes = new byte[ImageTransferAssembler.ImageLimit]; RandomNumberGenerator.Fill(bytes);
        using var assembler = Begin(id, bytes); int index = 0;
        for (int offset = 0; offset < bytes.Length; offset += ImageTransferAssembler.ChunkLimit)
        {
            int length = Math.Min(ImageTransferAssembler.ChunkLimit, bytes.Length - offset); using var frame = new MemoryStream();
            await ImageChunkWire.WriteAsync(frame, id, index++, bytes.AsMemory(offset, length)); Assert.InRange(frame.Length, 5, NativeMessagingCodec.MessageLimit + 4);
            frame.Position = 0; await ImageChunkWire.ReadAndAppendAsync(frame, assembler);
        }
        using var image = assembler.Commit(id); Assert.Equal(ImageTransferAssembler.ImageLimit, image.Bytes.Length);
        Assert.Equal(SHA256.HashData(bytes), SHA256.HashData(image.Bytes.Span));
        Assert.Throws<InvalidDataException>(() => assembler.Begin(Guid.NewGuid(), ImageTransferAssembler.ImageLimit + 1, "image/png", new string('0', 64)));
    }
    private static ImageTransferAssembler Begin(Guid id, byte[] bytes)
    {
        var assembler = new ImageTransferAssembler(); assembler.Begin(id, bytes.Length, "image/png", Convert.ToHexString(SHA256.HashData(bytes))); return assembler;
    }
}
