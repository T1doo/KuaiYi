using System.Text;
using KuaiYi.Providers;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class SseAndProfileTests
{
    private sealed class FragmentStream(byte[] bytes, int fragment) : Stream
    {
        private int position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => bytes.Length;
        public override long Position { get => position; set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            int n = Math.Min(fragment, Math.Min(buffer.Length, bytes.Length - position));
            bytes.AsMemory(position, n).CopyTo(buffer);
            position += n;
            return ValueTask.FromResult(n);
        }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(4096)]
    public async Task AllUtf8AndNewlineBoundariesPreserveMultilineDataAndDiscardIncompleteEof(int fragment)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("\uFEFF:心跳\r\n\r\nevent: custom\rdata: 中文🌍\rdata: 第二行\r\runknown: safe\ndata: 最后完整\n\ndata: 未完成");
        using var stream = new FragmentStream(bytes, fragment);
        List<SseEvent> events = [];
        await foreach (var e in SseReader.ReadAsync(stream)) events.Add(e);
        Assert.Equal(2, events.Count);
        Assert.Equal(new SseEvent("custom", "中文🌍\n第二行"), events[0]);
        Assert.Equal(new SseEvent("message", "最后完整"), events[1]);
    }
    [Fact]
    public async Task OversizeSingleLineAndInvalidUtf8AreRejected()
    {
        using var huge = new FragmentStream(Encoding.UTF8.GetBytes("data: " + new string('a', SseReader.MaximumEventBytes) + "\n\n"), 4096);
        await Assert.ThrowsAsync<InvalidDataException>(async () => { await foreach (var _ in SseReader.ReadAsync(huge)) { } });
        using var invalid = new FragmentStream([100, 97, 116, 97, 58, 32, 0xFF, 10, 10], 1);
        await Assert.ThrowsAsync<DecoderFallbackException>(async () => { await foreach (var _ in SseReader.ReadAsync(invalid)) { } });
    }
    [Theory]
    [InlineData("https://example.test/v1", "chat/completions", "https://example.test/v1/chat/completions")]
    [InlineData("https://example.test/proxy/vendor/v1/", "/chat/completions", "https://example.test/proxy/vendor/v1/chat/completions")]
    [InlineData("https://example.test/v1", "https://example.test/custom/messages", "https://example.test/custom/messages")]
    public void EndpointPrefixIsNotDiscardedOrDuplicated(string baseUrl, string endpoint, string expected)
    {
        var profile = new ApiProfile(ApiProtocol.OpenAIChat, new Uri(baseUrl), endpoint, "model", Authentication.None);
        Assert.Equal(expected, profile.ResolveEndpoint().AbsoluteUri);
    }
    [Theory]
    [InlineData("https://user:password@example.test", "messages")]
    [InlineData("http://example.test", "messages")]
    [InlineData("https://example.test", "https://other.test/messages")]
    [InlineData("https://example.test", "../messages")]
    [InlineData("https://example.test", "messages?key=unsafe")]
    public void InvalidOrUnencryptedAddressesFailBeforeEgress(string baseUrl, string endpoint)
    {
        var profile = new ApiProfile(ApiProtocol.AnthropicMessages, new Uri(baseUrl), endpoint, "model", Authentication.None);
        Assert.Throws<ArgumentException>(() => profile.ResolveEndpoint());
    }
    [Fact]
    public void FourPromptModesAreVersionedDistinctAndSourceIsNotInterpolatedIntoSystem()
    {
        string[] modes = ["faithful", "plain", "professional", "explain"];
        Assert.Equal(4, modes.Select(m => PromptBuilder.Build(m)).Distinct().Count());
        Assert.Contains("保留", PromptBuilder.Build("faithful"));
        Assert.Contains("指定领域：计算机", PromptBuilder.Build("professional", domain: "计算机"));
        Assert.Contains("译意", PromptBuilder.Build("explain"));
        Assert.Throws<ArgumentException>(() => PromptBuilder.Read("../../secrets"));
    }
}
