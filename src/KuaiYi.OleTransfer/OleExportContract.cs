using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace KuaiYi.OleTransfer;

public enum OleExportFailure { None, LengthLower, LengthUpper, Capacity, Signature, PacketType, Iid }
public enum OleExportSource { NativeExport, ParentImport }
public enum OleHeaderMatch { Missing, Match, Mismatch }
public enum OlePacketKind { Missing, Standard, Handler, Custom, Extended, Unknown }
public sealed record OleExportObservation(OleExportSource Source, OleExportFailure Failure, int? Length, int? Capacity,
    OleHeaderMatch Signature, OlePacketKind Kind, OleHeaderMatch Iid);

/// <summary>Unchanged production acceptance rule with fixed diagnostic classification. No packet identity or body leaves this type.</summary>
public static class OleExportContract
{
    public const int MinimumLength = 72, DiagnosticLengthLimit = 2 * OleReceiverWire.ExportLimit;
    public static OleExportObservation Inspect(OleExportSource source, ulong length, ulong capacity, ReadOnlySpan<byte> header)
    {
        var signature = header.Length < 4 ? OleHeaderMatch.Missing : BinaryPrimitives.ReadUInt32LittleEndian(header) == 0x574f454d ? OleHeaderMatch.Match : OleHeaderMatch.Mismatch;
        var kind = header.Length < 8 ? OlePacketKind.Missing : BinaryPrimitives.ReadUInt32LittleEndian(header[4..]) switch
        { 1 => OlePacketKind.Standard, 2 => OlePacketKind.Handler, 4 => OlePacketKind.Custom, 8 => OlePacketKind.Extended, _ => OlePacketKind.Unknown };
        var iid = header.Length < 24 ? OleHeaderMatch.Missing : new Guid(header.Slice(8, 16)) == OleReceiverWire.DropTargetId ? OleHeaderMatch.Match : OleHeaderMatch.Mismatch;
        var failure = length < MinimumLength ? OleExportFailure.LengthLower : length > OleReceiverWire.ExportLimit ? OleExportFailure.LengthUpper : length > capacity ? OleExportFailure.Capacity :
            signature != OleHeaderMatch.Match ? OleExportFailure.Signature : kind != OlePacketKind.Standard ? OleExportFailure.PacketType : iid != OleHeaderMatch.Match ? OleExportFailure.Iid : OleExportFailure.None;
        return new(source, failure, SafeLength(length), SafeLength(capacity), signature, kind, iid);
    }
    private static int? SafeLength(ulong value) => value <= DiagnosticLengthLimit ? (int)value : null;
    private static bool Safe(int? value) => value is null or >= 0 and <= DiagnosticLengthLimit;
    public static string Format(OleExportObservation item)
    {
        if (!Enum.IsDefined(item.Source) || !Enum.IsDefined(item.Failure) || !Enum.IsDefined(item.Signature) || !Enum.IsDefined(item.Kind) || !Enum.IsDefined(item.Iid) || !Safe(item.Length) || !Safe(item.Capacity))
            throw new ArgumentException("Invalid export metadata.");
        return $"KYOE1|{item.Source}|{item.Failure}|{Number(item.Length)}|{Number(item.Capacity)}|{item.Signature}|{item.Kind}|{item.Iid}";
    }
    private static string Number(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "-";
    public static bool TryParse(ReadOnlySpan<byte> bytes, out OleExportObservation? item)
    {
        item = null;
        if (bytes.Length is < 1 or > OleStartupDiagnostics.LineLimit) return false;
        foreach (byte value in bytes)
            if (!(value is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or >= (byte)'0' and <= (byte)'9' or (byte)'|' or (byte)'-')) return false;
        string[] parts = Encoding.ASCII.GetString(bytes).Split('|');
        if (parts.Length != 8 || parts[0] != "KYOE1" || !Named(parts[1], out OleExportSource source) || !Named(parts[2], out OleExportFailure failure) ||
            !Length(parts[3], out int? length) || !Length(parts[4], out int? capacity) || !Named(parts[5], out OleHeaderMatch signature) ||
            !Named(parts[6], out OlePacketKind kind) || !Named(parts[7], out OleHeaderMatch iid)) return false;
        item = new(source, failure, length, capacity, signature, kind, iid); return true;
    }
    private static bool Named<T>(string text, out T value) where T : struct, Enum
        => Enum.TryParse(text, out value) && Enum.IsDefined(value) && value.ToString() == text;
    private static bool Length(string text, out int? value)
    {
        value = null; if (text == "-") return true;
        if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || !Safe(parsed) || Number(parsed) != text) return false;
        value = parsed; return true;
    }
    public static void Emit(OleExportObservation item) => Console.Error.WriteLine(Format(item));
}
