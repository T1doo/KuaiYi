using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace KuaiYi.Contracts;

/// <summary>Chromium stdio framing, separate from the internal IPC codec. Authorization belongs to the host.</summary>
public static class NativeMessagingCodec
{
    public const int MessageLimit = 256 * 1024;
    // Native Messaging is UTF-8 JSON on a binary pipe, never HTML. Preserve base64 '+' and
    // actual Unicode bytes instead of introducing HTML-safe escapes that inflate chunks.
    private static readonly JsonSerializerOptions WireOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 32 };
    public static async ValueTask<JsonDocument?> ReadAsync(Stream stream, CancellationToken cancellation = default)
    {
        byte[] header = new byte[4];
        if (await stream.ReadAsync(header.AsMemory(0, 1), cancellation).ConfigureAwait(false) == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), cancellation).ConfigureAwait(false);
        uint length = BitConverter.ToUInt32(header); // Native byte order as required by Chromium, not the IPC wire order.
        if (length is 0 or > MessageLimit) throw new InvalidDataException("Invalid Native Messaging byte length.");
        byte[] payload = new byte[(int)length];
        try
        {
            await stream.ReadExactlyAsync(payload, cancellation).ConfigureAwait(false);
            return ParseOwned(payload);
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }
    private static JsonDocument ParseOwned(ReadOnlySpan<byte> payload)
    {
        try { _ = new UTF8Encoding(false, true).GetCharCount(payload); }
        catch (DecoderFallbackException error) { throw new InvalidDataException("Native message is not valid UTF-8.", error); }
        var reader = new Utf8JsonReader(payload, new JsonReaderOptions { MaxDepth = 32 });
        // ParseValue copies the input; a document must not retain the buffer cleared by ReadAsync.
        var document = JsonDocument.ParseValue(ref reader);
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Native message must be a JSON object.");
            if (reader.Read()) throw new InvalidDataException("Trailing Native Messaging JSON value.");
            return document;
        }
        catch { document.Dispose(); throw; }
    }
    public static async ValueTask WriteAsync<T>(Stream stream, T message, CancellationToken cancellation = default)
    {
        byte[] payload = JsonSerializer.SerializeToUtf8Bytes(message, WireOptions);
        try
        {
            if (payload.Length is 0 or > MessageLimit) throw new InvalidDataException("Native message exceeds UTF-8 byte limit.");
            using var parsed = JsonDocument.Parse(payload, new JsonDocumentOptions { MaxDepth = 32 });
            if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Native message must be a JSON object.");
            await stream.WriteAsync(BitConverter.GetBytes((uint)payload.Length), cancellation).ConfigureAwait(false);
            await stream.WriteAsync(payload, cancellation).ConfigureAwait(false);
            await stream.FlushAsync(cancellation).ConfigureAwait(false);
        }
        finally { CryptographicOperations.ZeroMemory(payload); }
    }
}

public sealed class ReceivedImage : IDisposable
{
    private byte[]? bytes;
    public Guid TransferId { get; }
    public string Mime { get; }
    public ReadOnlyMemory<byte> Bytes => bytes ?? throw new ObjectDisposedException(nameof(ReceivedImage));
    internal ReceivedImage(Guid id, string mime, byte[] owned) { TransferId = id; Mime = mime; bytes = owned; }
    public void Dispose() { byte[]? owned = Interlocked.Exchange(ref bytes, null); if (owned is not null) CryptographicOperations.ZeroMemory(owned); }
}

/// <summary>One bounded in-memory transfer per authenticated connection. Commit produces bytes, never a model request.</summary>
public sealed class ImageTransferAssembler : IDisposable
{
    public const int ImageLimit = 20 * 1024 * 1024;
    // 190 KiB => at most 259416 base64 bytes, reserving 2728 bytes for the bounded envelope.
    // The codec still checks the complete serialized UTF-8 message against 256 KiB.
    public const int ChunkLimit = 190 * 1024;
    private readonly TimeProvider time;
    private byte[]? buffer, expectedHash;
    private Guid id;
    private string mime = "";
    private int offset, nextIndex;
    private long started;
    public ImageTransferAssembler(TimeProvider? timeProvider = null) => time = timeProvider ?? TimeProvider.System;
    public void Begin(Guid transferId, int totalLength, string imageMime, string sha256)
    {
        if (buffer is not null) Fail("Transfer already active.");
        if (transferId == Guid.Empty || totalLength is <= 0 or > ImageLimit || imageMime is not ("image/png" or "image/jpeg" or "image/webp" or "image/bmp" or "image/gif") ||
            sha256.Length != 64 || !sha256.All(char.IsAsciiHexDigit)) throw new InvalidDataException("Invalid image transfer declaration.");
        expectedHash = Convert.FromHexString(sha256); buffer = new byte[totalLength]; id = transferId; mime = imageMime;
        offset = nextIndex = 0; started = time.GetTimestamp();
    }
    public void Chunk(Guid transferId, int index, ReadOnlySpan<byte> bytes)
    {
        Check(transferId);
        if (index != nextIndex || bytes.Length is <= 0 or > ChunkLimit || bytes.Length > buffer!.Length - offset) Fail("Invalid image chunk order/length.");
        bytes.CopyTo(buffer.AsSpan(offset)); offset += bytes.Length; nextIndex++;
    }
    public ReceivedImage Commit(Guid transferId)
    {
        Check(transferId);
        if (offset != buffer!.Length || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(buffer), expectedHash!)) Fail("Incomplete image or digest mismatch.");
        var result = new ReceivedImage(id, mime, buffer!);
        buffer = null; Reset(); return result;
    }
    private void Check(Guid transferId)
    {
        if (buffer is null || id != transferId || time.GetElapsedTime(started) >= TimeSpan.FromSeconds(30)) Fail("Unknown, stale or expired image transfer.");
    }
    private void Fail(string reason) { Reset(); throw new InvalidDataException(reason); }
    private void Reset()
    {
        if (buffer is not null) CryptographicOperations.ZeroMemory(buffer);
        if (expectedHash is not null) CryptographicOperations.ZeroMemory(expectedHash);
        buffer = expectedHash = null; id = Guid.Empty; mime = ""; offset = nextIndex = 0;
    }
    public void Dispose() => Reset();
}
