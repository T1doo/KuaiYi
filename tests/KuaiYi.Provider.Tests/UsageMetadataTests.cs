using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class UsageMetadataTests
{
    private static IReadOnlyList<ModelEvent> Parse(ApiProtocol protocol,string fields)
    {
        IProtocolAdapter adapter=protocol==ApiProtocol.OpenAIChat?new OpenAiChatAdapter():new AnthropicMessagesAdapter();
        string json=protocol==ApiProtocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"PUBLIC\"},\"finish_reason\":\"stop\"}]"+fields+"}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"PUBLIC\"}],\"stop_reason\":\"end_turn\""+fields+"}";
        return adapter.ParseNonStreaming(json);
    }
    public static IEnumerable<object[]> InvalidNumbers()
    {
        foreach(var protocol in Enum.GetValues<ApiProtocol>())foreach(var value in new[]{"null","\"13\"","-1","1.5","9223372036854775808","true","{}","[]","1e100"})yield return [protocol,value];
    }
    [Theory][MemberData(nameof(InvalidNumbers))]
    public void InvalidUsageNumberIsUnknownWhileIndependentValidFieldAndCompletionSurvive(ApiProtocol protocol,string value)
    {
        string input=protocol==ApiProtocol.OpenAIChat?"prompt_tokens":"input_tokens",output=protocol==ApiProtocol.OpenAIChat?"completion_tokens":"output_tokens";
        var events=Parse(protocol,",\"usage\":{\""+input+"\":"+value+",\""+output+"\":7}");
        Assert.Equal(new TokenUsage(null,7),Assert.Single(events,e=>e.Kind==ModelEventKind.Usage).Usage);Assert.Single(events,e=>e.Kind==ModelEventKind.Completed);Assert.Equal("PUBLIC",Assert.Single(events,e=>e.Kind==ModelEventKind.TextDelta).Text);
    }
    [Theory][InlineData(ApiProtocol.OpenAIChat)][InlineData(ApiProtocol.AnthropicMessages)]
    public void DuplicateFieldsRemainUnknownAndValidZeroOrInt64MaximumAreNeverEstimated(ApiProtocol protocol)
    {
        string input=protocol==ApiProtocol.OpenAIChat?"prompt_tokens":"input_tokens",output=protocol==ApiProtocol.OpenAIChat?"completion_tokens":"output_tokens";
        var duplicate=Parse(protocol,",\"usage\":{\""+input+"\":1,\""+input+"\":2,\""+output+"\":0}");Assert.Equal(new TokenUsage(null,0),Assert.Single(duplicate,e=>e.Kind==ModelEventKind.Usage).Usage);
        var valid=Parse(protocol,",\"usage\":{\""+input+"\":0,\""+output+"\":9223372036854775807}");Assert.Equal(new TokenUsage(0,long.MaxValue),Assert.Single(valid,e=>e.Kind==ModelEventKind.Usage).Usage);
        var rootDuplicate=Parse(protocol,",\"usage\":{\""+input+"\":1},\"usage\":{\""+input+"\":2}");Assert.Equal(new TokenUsage(null,null),Assert.Single(rootDuplicate,e=>e.Kind==ModelEventKind.Usage).Usage);
    }
    public static IEnumerable<object[]> MissingOrMalformedUsage()
    {foreach(var protocol in Enum.GetValues<ApiProtocol>())foreach(var field in new[]{"",",\"usage\":null",",\"usage\":\"unknown\"",",\"usage\":[]",",\"usage\":12",",\"usage\":{}"})yield return [protocol,field];}
    [Theory][MemberData(nameof(MissingOrMalformedUsage))]
    public void MissingOrMalformedUsageCannotPreventAnOtherwiseValidCompletion(ApiProtocol protocol,string fields)
    {
        var events=Parse(protocol,fields);Assert.Single(events,e=>e.Kind==ModelEventKind.Completed);Assert.DoesNotContain(events,e=>e.Kind==ModelEventKind.Failed);Assert.All(events.Where(e=>e.Kind==ModelEventKind.Usage),e=>Assert.Equal(new TokenUsage(null,null),e.Usage));
    }
}
