using System.Text.Json;
using KuaiYi.Contracts;

// P0 echo probe only. No browser registration, image submission, desktop IPC or provider dispatch.
if (args.Length != 1 || args[0] != "--probe") return 2;
using Stream input = Console.OpenStandardInput(), output = Console.OpenStandardOutput();
try
{
    while (true)
    {
        using var message = await NativeMessagingCodec.ReadAsync(input);
        if (message is null) return 0;
        var root = message.RootElement;
        if (!root.TryGetProperty("type", out var type) || type.GetString() != "Ping" ||
            !root.TryGetProperty("requestId", out var request) || !request.TryGetGuid(out Guid id) || id == Guid.Empty)
        {
            await NativeMessagingCodec.WriteAsync(output, new { type = "Error", code = "PROBE_COMMAND_REJECTED" }); return 4;
        }
        await NativeMessagingCodec.WriteAsync(output, new { type = "Pong", requestId = id });
    }
}
catch (Exception error) when (error is JsonException or InvalidDataException or EndOfStreamException or InvalidOperationException)
{
    Console.Error.WriteLine("Native Messaging probe rejected malformed input."); return 3;
}
