using System.Security.Cryptography;
using System.Text.Json;

namespace KuaiYi.Contracts;

/// <summary>Bounded canonical Chunk JSON frames. Browser Begin/Commit and origin authorization remain host responsibilities.</summary>
public static class ImageChunkWire
{
    public static ValueTask WriteAsync(Stream destination, Guid transferId, int index, ReadOnlyMemory<byte> bytes, CancellationToken cancellation = default)
    {
        if (transferId == Guid.Empty || index < 0 || bytes.Length is <= 0 or > ImageTransferAssembler.ChunkLimit)
            throw new InvalidDataException("Invalid image chunk declaration.");
        return NativeMessagingCodec.WriteAsync(destination,
            new { type = "Chunk", transferId = transferId.ToString("D"), index, data = Convert.ToBase64String(bytes.Span) }, cancellation);
    }

    public static async ValueTask ReadAndAppendAsync(Stream source, ImageTransferAssembler assembler, CancellationToken cancellation = default)
    {
        try
        {
            using var message = await NativeMessagingCodec.ReadAsync(source, cancellation).ConfigureAwait(false) ?? throw new EndOfStreamException("Missing image chunk frame.");
            Append(message.RootElement, assembler);
        }
        catch { assembler.Dispose(); throw; } // A malformed frame aborts and clears this connection's incomplete transfer.
    }

    private static void Append(JsonElement root, ImageTransferAssembler assembler)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in root.EnumerateObject())
            if (field.Name is not ("type" or "transferId" or "index" or "data") || !fields.Add(field.Name)) throw new InvalidDataException("Unknown/duplicate chunk field.");
        if (fields.Count != 4 || !root.TryGetProperty("type", out var type) || type.ValueKind != JsonValueKind.String || type.GetString() != "Chunk" ||
            !root.TryGetProperty("transferId", out var identifier) || identifier.ValueKind != JsonValueKind.String || identifier.GetString() is not { Length: 36 } idText ||
            !Guid.TryParseExact(idText, "D", out Guid id) || id == Guid.Empty || !root.TryGetProperty("index", out var number) ||
            number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out int index) || index < 0 ||
            !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.String || data.GetString() is not { } encoded)
            throw new InvalidDataException("Invalid chunk envelope.");
        const int encodedLimit = 4 * ((ImageTransferAssembler.ChunkLimit + 2) / 3);
        if (encoded.Length is 0 or > encodedLimit || encoded.Length % 4 != 0 || encoded.Any(char.IsWhiteSpace)) throw new InvalidDataException("Invalid base64 chunk size.");
        byte[] decoded = new byte[encoded.Length / 4 * 3];
        try
        {
            if (!Convert.TryFromBase64String(encoded, decoded, out int size) || size is <= 0 or > ImageTransferAssembler.ChunkLimit)
                throw new InvalidDataException("Invalid/oversized base64 chunk.");
            assembler.Chunk(id, index, decoded.AsSpan(0, size));
        }
        finally { CryptographicOperations.ZeroMemory(decoded); }
    }
}
