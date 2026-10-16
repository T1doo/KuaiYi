using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
namespace KuaiYi.Imaging;

public sealed record ImageJob(Guid SessionId, long Version, long Epoch, string Language, int ByteLength);
public sealed record WorkerDiagnostic(IdentityDiagnostic Identity, string[] Languages, uint MaxImageDimension, ImageFailure Failure);
public sealed record OcrDiagnosticJob(Guid SessionId,long Version,long Epoch);
public sealed record OcrDiagnosticReply(OcrDiagnosticJob Job,WorkerDiagnostic Diagnostic);
public sealed record OcrWord(string Text, double X, double Y, double Width, double Height);
public sealed record OcrLine(string Text, OcrWord[] Words);
public sealed record OcrOutput(uint Width, uint Height, string Language, string Text, OcrLine[] Lines);
public sealed record ImageReply(ImageJob Job, ImageFailure Failure, OcrOutput? Output);
public static class ImageJson
{
    public static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow };
    public static byte[] Serialize<T>(T value) => JsonSerializer.SerializeToUtf8Bytes(value, Options);
    public static T Read<T>(byte[] value)
    {
        using var document = JsonDocument.Parse(value, new JsonDocumentOptions { MaxDepth = 16 });
        CheckFields(document.RootElement);
        return JsonSerializer.Deserialize<T>(value, Options) ?? throw new InvalidDataException("Empty image control.");
    }
    private static void CheckFields(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in element.EnumerateObject()) { if (!names.Add(field.Name)) throw new InvalidDataException("Duplicate image field."); CheckFields(field.Value); }
        }
        else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) CheckFields(item);
    }
}
/// <summary>One owned worker / one private job. ID, sequencing and total lengths checked before allocating image memory.</summary>
public sealed class ImageJobReceiver : IDisposable
{
    private byte[]? data;
    private int filled, next;
    private bool committed,terminal;
    public Guid RequestId { get; private set; }
    public ImageJob? Job { get; private set; }
    public void Begin(IpcFrame frame)
    {
        if (terminal || frame.Type != IpcMessageType.ImageBegin || data != null || committed || frame.RequestId == Guid.Empty) Abort();
        try
        {
            var job = ImageJson.Read<ImageJob>(frame.Payload);
            if (job.SessionId == Guid.Empty || job.Version <= 0 || job.Epoch <= 0 || job.ByteLength is <= 0 or > ImagePolicy.ByteLimit ||
                string.IsNullOrWhiteSpace(job.Language) || job.Language.Length > 64 || job.Language.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '-')))
                throw new InvalidDataException("Invalid image job.");
            Job = job; RequestId = frame.RequestId; data = new byte[job.ByteLength];
        }
        catch { Dispose(); throw; }
    }
    public void Chunk(IpcFrame frame)
    {
        if (data == null || committed || frame.Type != IpcMessageType.BinaryChunk || frame.RequestId != RequestId || frame.Payload.Length <= 4 ||
            BinaryPrimitives.ReadUInt32LittleEndian(frame.Payload) != (uint)next || frame.Payload.Length - 4 > data.Length - filled) Abort();
        frame.Payload.AsSpan(4).CopyTo(data!.AsSpan(filled)); filled += frame.Payload.Length - 4; next++;
    }
    public ReadOnlyMemory<byte> Commit(IpcFrame frame)
    {
        if (data == null || committed || frame.Type != IpcMessageType.ImageCommit || frame.RequestId != RequestId || frame.Payload.Length != 0 || filled != data.Length) Abort();
        committed = true;
        return data!;
    }
    private void Abort() { Dispose(); throw new InvalidDataException("Invalid private image transfer."); }
    public void Dispose() { terminal=true;if (data != null) CryptographicOperations.ZeroMemory(data); data = null; Job = null;RequestId=Guid.Empty;filled=next=0; }
}
