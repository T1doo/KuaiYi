using KuaiYi.Providers;
using Xunit;

namespace KuaiYi.Provider.Tests;

public sealed class AnthropicLifecycleTests
{
    private const string Start = "{\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":2,\"output_tokens\":0}}}";
    private const string Block = "{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"text\",\"text\":\"初始\"}}";
    private const string Delta = "{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"续写\"}}";
    private const string BlockStop = "{\"type\":\"content_block_stop\",\"index\":0}";
    private const string Final = "{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":3}}";
    private const string Stop = "{\"type\":\"message_stop\"}";
    [Fact]
    public void CompleteLifecycleEmitsOnlyTextBlocksAndActualUsage()
    {
        var adapter = new AnthropicMessagesAdapter();
        Assert.Equal(2, Assert.Single(adapter.ParseEvent(Start)).Usage!.InputTokens);
        Assert.Equal("初始", Assert.Single(adapter.ParseEvent(Block)).Text);
        Assert.Equal("续写", Assert.Single(adapter.ParseEvent(Delta)).Text);
        Assert.Empty(adapter.ParseEvent(BlockStop)); Assert.Equal(3, Assert.Single(adapter.ParseEvent(Final)).Usage!.OutputTokens);
        Assert.Equal(ModelEventKind.Completed, Assert.Single(adapter.ParseEvent(Stop)).Kind);
        Assert.Throws<InvalidDataException>(() => adapter.ParseEvent(Delta));
    }
    [Theory]
    [InlineData(Delta)] [InlineData(Stop)] [InlineData(Block)] [InlineData(Final)]
    public void EventsCannotPrecedeMessageStart(string json) => Assert.Throws<InvalidDataException>(() => new AnthropicMessagesAdapter().ParseEvent(json));
    [Fact]
    public void OrphanDuplicateAndIncompleteEventsCannotComplete()
    {
        var orphan = new AnthropicMessagesAdapter(); orphan.ParseEvent(Start);
        Assert.Throws<InvalidDataException>(() => orphan.ParseEvent(Delta));
        Assert.Throws<InvalidDataException>(() => orphan.ParseEvent(Stop));
        var duplicate = new AnthropicMessagesAdapter(); duplicate.ParseEvent(Start); duplicate.ParseEvent(Block);
        Assert.Throws<InvalidDataException>(() => duplicate.ParseEvent(Block));
        var incomplete = new AnthropicMessagesAdapter(); incomplete.ParseEvent(Start); incomplete.ParseEvent(Block);
        Assert.Throws<InvalidDataException>(() => incomplete.ParseEvent(Final));
        Assert.Throws<InvalidDataException>(() => incomplete.ParseEvent(Stop));
    }
    [Fact]
    public void ThinkingAndUnknownBlocksCannotExposeTextDelta()
    {
        var adapter = new AnthropicMessagesAdapter(); adapter.ParseEvent(Start);
        adapter.ParseEvent("{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"thinking\",\"thinking\":\"private-reasoning\"}}");
        Assert.Empty(adapter.ParseEvent("{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"thinking_delta\",\"thinking\":\"private-reasoning\"}}"));
        Assert.Throws<InvalidDataException>(() => adapter.ParseEvent(Delta));
    }
    [Fact]
    public void ToolsAndTruncationAreFailuresNotCompletedMessages()
    {
        var tool = new AnthropicMessagesAdapter(); tool.ParseEvent(Start);
        Assert.Equal("UNSUPPORTED_TOOL_RESPONSE", Assert.Single(tool.ParseEvent("{\"type\":\"content_block_start\",\"index\":0,\"content_block\":{\"type\":\"tool_use\"}}")).Reason);
        var truncated = new AnthropicMessagesAdapter(); truncated.ParseEvent(Start);
        Assert.Equal("OUTPUT_TRUNCATED", Assert.Single(truncated.ParseEvent("{\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"max_tokens\"}}")).Reason);
        Assert.Throws<InvalidDataException>(() => truncated.ParseEvent(Stop));
    }
}
