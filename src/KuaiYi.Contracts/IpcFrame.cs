using System.Buffers.Binary;

namespace KuaiYi.Contracts;

public enum IpcMessageType : ushort
{
    Ping = 1, ShowSettings = 2, SubmitImage = 3, SubmitImagePath = 4, BinaryChunk = 5,
    // Private inherited ImageWorker channel only; never accepted by the external command gateway.
    ImageHello = 100, ImageBegin = 101, ImageCommit = 102, ImageResult = 103, ImageCancel = 104,
    // Separate one-shot CaptureHost inherited channel, never accepted by external App commands.
    CaptureHello=120,CaptureBegin=121,CaptureResult=122,CaptureCommit=123,
    OleHello=140,OleExport=141,OleControl=142,OleEvent=143
}
public sealed record IpcFrame(IpcMessageType Type, Guid RequestId, byte[] Payload);

/// <summary>Wire codec only. Windows pipe ACL/session/role authentication is a separate required layer.</summary>
public static class IpcFrameCodec
{
    public const int HeaderLength = 28;
    public const int JsonLimit = 64 * 1024;
    public const int BinaryLimit = 1024 * 1024;
    private const uint Magic = 0x3159594B; // KYY1, little endian
    private const ushort Version = 1;

    public static async ValueTask WriteAsync(Stream stream, IpcFrame frame, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(frame);
        Validate(frame.Type, frame.RequestId, checked((uint)frame.Payload.Length));
        byte[] header = new byte[HeaderLength];
        BinaryPrimitives.WriteUInt32LittleEndian(header, Magic);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(4), Version);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(6), (ushort)frame.Type);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)frame.Payload.Length);
        frame.RequestId.TryWriteBytes(header.AsSpan(12, 16));
        await stream.WriteAsync(header, cancellation).ConfigureAwait(false);
        await stream.WriteAsync(frame.Payload, cancellation).ConfigureAwait(false);
    }

    public static async ValueTask<IpcFrame?> ReadAsync(Stream stream, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        byte[] header = new byte[HeaderLength];
        int first = await stream.ReadAsync(header.AsMemory(0, 1), cancellation).ConfigureAwait(false);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), cancellation).ConfigureAwait(false);
        if (BinaryPrimitives.ReadUInt32LittleEndian(header) != Magic || BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(4)) != Version)
            throw new InvalidDataException("Unsupported IPC magic/version.");
        var type = (IpcMessageType)BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(6));
        uint length = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(8));
        var request = new Guid(header.AsSpan(12, 16));
        Validate(type, request, length); // Validate unsigned length before allocation.
        byte[] payload = new byte[(int)length];
        try
        {
            await stream.ReadExactlyAsync(payload, cancellation).ConfigureAwait(false);
            return new IpcFrame(type, request, payload);
        }
        catch { System.Security.Cryptography.CryptographicOperations.ZeroMemory(payload); throw; }
    }

    private static void Validate(IpcMessageType type, Guid request, uint length)
    {
        if (!Enum.IsDefined(type) || request == Guid.Empty) throw new InvalidDataException("Invalid IPC type/request ID.");
        uint limit = (uint)(type == IpcMessageType.BinaryChunk ? BinaryLimit : JsonLimit);
        if (length > limit) throw new InvalidDataException("IPC payload exceeds limit.");
    }
}
