using System.Text.Json;
using System.Text.Json.Nodes;

namespace KuaiYi.Providers;

internal static class ProtocolJson
{
    public static JsonDocument Parse(string text) => JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = 32 });
    public static long? Number(JsonElement root, string property) => root.TryGetProperty(property, out var p) && p.TryGetInt64(out long n) && n >= 0 ? n : null;
    public static string? String(JsonElement root, string property) => root.TryGetProperty(property, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;
    public static JsonObject ImageRequest(byte[] image, string mime, bool openAi)
    {
        if (image.Length is 0 or > 4194304 || mime is not ("image/png" or "image/jpeg" or "image/webp"))
            throw new ArgumentException("Invalid prepared image size/MIME.");
        string encoded = Convert.ToBase64String(image);
        return openAi ? new() { ["type"] = "image_url", ["image_url"] = new JsonObject { ["url"] = $"data:{mime};base64,{encoded}" } }
            : new() { ["type"] = "image", ["source"] = new JsonObject { ["type"] = "base64", ["media_type"] = mime, ["data"] = encoded } };
    }
}

public sealed class OpenAiChatAdapter : IProtocolAdapter
{
    public ApiProtocol Protocol => ApiProtocol.OpenAIChat;
    public bool LastEventWasEffective{get;private set;}
    private bool roleSeen,finishSeen;private TokenUsage? previousUsage;
    public JsonObject BuildBody(TranslationRequest request)
    {
        JsonNode content = JsonValue.Create(request.SourceText)!;
        if (request.Image is { } image) content = new JsonArray(new JsonObject { ["type"] = "text", ["text"] = request.SourceText },
            ProtocolJson.ImageRequest(image, request.ImageMime, true));
        var body = new JsonObject
        {
            ["model"] = request.Profile.Model,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "system", ["content"] = request.SystemPrompt }, new JsonObject { ["role"] = "user", ["content"] = content }),
            ["stream"] = request.Profile.Stream
        };
        if (request.Profile.OutputLimit != OutputLimitParameter.Omit)
            body[request.Profile.OutputLimit == OutputLimitParameter.MaxTokens ? "max_tokens" : "max_completion_tokens"] = request.Profile.MaxOutputTokens;
        if(request.Profile.Temperature is {} temperature)body["temperature"]=temperature;
        if(request.Profile.Stream&&request.Profile.IncludeUsage)body["stream_options"]=new JsonObject{["include_usage"]=true};
        if(request.Profile.SendStoreFalse)body["store"]=false;
        return body;
    }
    public IReadOnlyList<ModelEvent> ParseEvent(string data)
    {
        LastEventWasEffective=false;
        if (data == "[DONE]"){LastEventWasEffective=true;return [new(ModelEventKind.Completed, Reason: "done")];}
        using var document = ProtocolJson.Parse(data);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("error", out _)){LastEventWasEffective=true;return [new(ModelEventKind.Failed, Reason: "PROVIDER_ERROR")];}
        List<ModelEvent> events = [];
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            events.Add(new(ModelEventKind.Usage, Usage: new(ProtocolJson.Number(usage, "prompt_tokens"), ProtocolJson.Number(usage, "completion_tokens"))));
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid OpenAI choices.");
        foreach (JsonElement choice in choices.EnumerateArray())
        {
            if (ProtocolJson.Number(choice, "index") != 0) continue;
            if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object)
            {
                if(ProtocolJson.String(delta,"role")=="assistant"&&!roleSeen){roleSeen=true;LastEventWasEffective=true;}
                string? text = ProtocolJson.String(delta, "content");
                if (!string.IsNullOrEmpty(text)) events.Add(new(ModelEventKind.TextDelta, text));
                if (!string.IsNullOrEmpty(ProtocolJson.String(delta, "refusal"))) events.Add(new(ModelEventKind.Failed, Reason: "CONTENT_REFUSED"));
                if (delta.TryGetProperty("tool_calls", out var tools) && tools.ValueKind == JsonValueKind.Array && tools.GetArrayLength() > 0)
                    events.Add(new(ModelEventKind.Failed, Reason: "UNSUPPORTED_TOOL_RESPONSE"));
            }
            string? finish = ProtocolJson.String(choice, "finish_reason");
            if(finish=="stop"&&!finishSeen){finishSeen=true;LastEventWasEffective=true;}
            if (finish is "length" or "content_filter" or "tool_calls" or "function_call")
                events.Add(new(ModelEventKind.Failed, Reason: finish == "length" ? "OUTPUT_TRUNCATED" : finish == "content_filter" ? "CONTENT_REFUSED" : "UNSUPPORTED_TOOL_RESPONSE"));
        }
        foreach(var item in events){if(item.Kind is ModelEventKind.Completed or ModelEventKind.Failed||item.Kind==ModelEventKind.TextDelta&&item.Text?.Length>0)LastEventWasEffective=true;else if(item.Kind==ModelEventKind.Usage&&item.Usage is {} counts&&(counts.InputTokens!=null||counts.OutputTokens!=null)&&counts!=previousUsage){LastEventWasEffective=true;previousUsage=counts;}}
        return events;
    }
    public IReadOnlyList<ModelEvent> ParseNonStreaming(string json)
    {
        using var document = ProtocolJson.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("choices", out var choices) || choices.ValueKind != JsonValueKind.Array) throw new InvalidDataException("Invalid OpenAI response.");
        var choice = choices.EnumerateArray().FirstOrDefault(c => ProtocolJson.Number(c, "index") == 0);
        if (choice.ValueKind != JsonValueKind.Object || !choice.TryGetProperty("message", out var message)) throw new InvalidDataException("Missing selected choice.");
        string? text = ProtocolJson.String(message, "content");
        string? finish = ProtocolJson.String(choice, "finish_reason");
        if (message.TryGetProperty("tool_calls", out _) || !string.IsNullOrEmpty(ProtocolJson.String(message, "refusal")))
            return [new(ModelEventKind.Failed, Reason: "UNSUPPORTED_OR_REFUSED")];
        if (text is null) throw new InvalidDataException("Missing OpenAI text.");
        List<ModelEvent> events = [new(ModelEventKind.TextDelta, text)];
        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
            events.Add(new(ModelEventKind.Usage, Usage: new(ProtocolJson.Number(usage, "prompt_tokens"), ProtocolJson.Number(usage, "completion_tokens"))));
        events.Add(finish == "stop" ? new(ModelEventKind.Completed, Reason: "non-streaming") : new(ModelEventKind.Failed, Reason: finish == "length" ? "OUTPUT_TRUNCATED" : "STREAM_INTERRUPTED"));
        return events;
    }
}

public sealed class AnthropicMessagesAdapter : IProtocolAdapter
{
    private bool started, stopped, validStopReason,stopReasonSeen;
    private readonly Dictionary<long, string> activeBlocks = [];
    private readonly HashSet<long> seenBlocks = [];
    public ApiProtocol Protocol => ApiProtocol.AnthropicMessages;
    public bool LastEventWasEffective{get;private set;}
    private TokenUsage? previousUsage;
    public JsonObject BuildBody(TranslationRequest request)
    {
        var content = new JsonArray();
        if (request.Image is { } image) content.Add(ProtocolJson.ImageRequest(image, request.ImageMime, false));
        content.Add(new JsonObject { ["type"] = "text", ["text"] = request.SourceText });
        var body=new JsonObject
        {
            ["model"] = request.Profile.Model, ["system"] = request.SystemPrompt,
            ["max_tokens"] = request.Profile.MaxOutputTokens, ["stream"] = request.Profile.Stream,
            ["messages"] = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = content })
        };
        if(request.Profile.Temperature is {} temperature)body["temperature"]=temperature;return body;
    }
    public IReadOnlyList<ModelEvent> ParseEvent(string data)
    {
        LastEventWasEffective=false;
        using var document = ProtocolJson.Parse(data);
        var root = document.RootElement;
        if (stopped) throw new InvalidDataException("Anthropic event after message_stop.");
        switch (ProtocolJson.String(root, "type"))
        {
            case "content_block_delta":
                RequireStarted();
                long deltaIndex = BlockIndex(root);
                if (!activeBlocks.TryGetValue(deltaIndex, out string? blockType)) throw new InvalidDataException("Delta without active content block.");
                if (root.TryGetProperty("delta", out var delta) && ProtocolJson.String(delta, "type") == "text_delta")
                {
                    if (blockType != "text" || ProtocolJson.String(delta, "text") is not { } text) throw new InvalidDataException("Text delta outside a text block.");
                    LastEventWasEffective=text.Length>0;return [new(ModelEventKind.TextDelta, text)];
                }
                break;
            case "message_start":
                if (started) throw new InvalidDataException("Duplicate Anthropic message_start.");
                started = true;LastEventWasEffective=true;
                if (root.TryGetProperty("message", out var message) && message.TryGetProperty("usage", out var startUsage))
                    {previousUsage=new(ProtocolJson.Number(startUsage,"input_tokens"),ProtocolJson.Number(startUsage,"output_tokens"));return [new(ModelEventKind.Usage,Usage:previousUsage)];}
                break;
            case "message_delta":
                RequireStarted();
                if (activeBlocks.Count != 0) throw new InvalidDataException("Message delta before content blocks stopped.");
                List<ModelEvent> events = [];
                if (root.TryGetProperty("usage", out var usage)) events.Add(new(ModelEventKind.Usage, Usage: new(ProtocolJson.Number(usage, "input_tokens"), ProtocolJson.Number(usage, "output_tokens"))));
                if (root.TryGetProperty("delta", out var final) && ProtocolJson.String(final, "stop_reason") is { } reason)
                {
                    validStopReason = reason is "end_turn" or "stop_sequence";LastEventWasEffective=!stopReasonSeen;stopReasonSeen=true;
                    if (!validStopReason) events.Add(new(ModelEventKind.Failed, Reason: reason == "max_tokens" ? "OUTPUT_TRUNCATED" : "UNSUPPORTED_OR_REFUSED"));
                }
                if(events.Any(item=>item.Usage is {} counts&&(counts.InputTokens!=null||counts.OutputTokens!=null)&&counts!=previousUsage)){LastEventWasEffective=true;previousUsage=events.First(item=>item.Kind==ModelEventKind.Usage).Usage;}
                return events;
            case "message_stop":
                RequireStarted();
                if (activeBlocks.Count != 0 || !validStopReason) throw new InvalidDataException("Incomplete Anthropic message lifecycle.");
                stopped = true;LastEventWasEffective=true; return [new(ModelEventKind.Completed, Reason: "message_stop")];
            case "error":LastEventWasEffective=true; return [new(ModelEventKind.Failed, Reason: "PROVIDER_ERROR")];
            case "content_block_start":
                RequireStarted();
                long index = BlockIndex(root);
                if (seenBlocks.Count >= 128 || !seenBlocks.Add(index)) throw new InvalidDataException("Invalid/duplicate content block.");
                if (!root.TryGetProperty("content_block", out var block) || ProtocolJson.String(block, "type") is not { } type) throw new InvalidDataException("Missing content block type.");
                if (type is "tool_use" or "server_tool_use") return [new(ModelEventKind.Failed, Reason: "UNSUPPORTED_TOOL_RESPONSE")];
                activeBlocks.Add(index, type);LastEventWasEffective=false;
                if (type == "text" && ProtocolJson.String(block, "text") is { Length: > 0 } initial){LastEventWasEffective=true;return [new(ModelEventKind.TextDelta, initial)];}
                break;
            case "content_block_stop":
                RequireStarted();
                long stoppedIndex=BlockIndex(root);if(!activeBlocks.TryGetValue(stoppedIndex,out var endedType)||!activeBlocks.Remove(stoppedIndex))throw new InvalidDataException("Stop without active content block.");LastEventWasEffective=false;
                break;
        }
        return [];
    }
    private void RequireStarted() { if (!started) throw new InvalidDataException("Anthropic event before message_start."); }
    private static long BlockIndex(JsonElement root) => ProtocolJson.Number(root, "index") is { } index && index < 128 ? index : throw new InvalidDataException("Invalid content block index.");
    public IReadOnlyList<ModelEvent> ParseNonStreaming(string json)
    {
        using var document = ProtocolJson.Parse(json);
        var root = document.RootElement;
        if (ProtocolJson.String(root, "type") != "message" || !root.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Invalid Anthropic message.");
        List<ModelEvent> events = [];
        foreach (var block in content.EnumerateArray())
        {
            if (ProtocolJson.String(block, "type") == "text" && ProtocolJson.String(block, "text") is { } text) events.Add(new(ModelEventKind.TextDelta, text));
            else if (ProtocolJson.String(block, "type") is "tool_use" or "server_tool_use") return [new(ModelEventKind.Failed, Reason: "UNSUPPORTED_TOOL_RESPONSE")];
        }
        if (root.TryGetProperty("usage", out var usage)) events.Add(new(ModelEventKind.Usage, Usage: new(ProtocolJson.Number(usage, "input_tokens"), ProtocolJson.Number(usage, "output_tokens"))));
        string? reason = ProtocolJson.String(root, "stop_reason");
        events.Add(reason is "end_turn" or "stop_sequence" ? new(ModelEventKind.Completed, Reason: "non-streaming") : new(ModelEventKind.Failed, Reason: reason == "max_tokens" ? "OUTPUT_TRUNCATED" : "UNSUPPORTED_OR_REFUSED"));
        return events;
    }
}
