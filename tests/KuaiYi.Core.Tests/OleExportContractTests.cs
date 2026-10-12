using System.Buffers.Binary;
using System.Text;
using KuaiYi.OleTransfer;
using Xunit;

namespace KuaiYi.Core.Tests;

public sealed class OleExportContractTests
{
    // Synthetic header only. No test sends this array to CoUnmarshalInterface.
    private static byte[] Header(int length = 72)
    {
        byte[] bytes = new byte[length];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, 0x574f454d);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), 1);
        OleReceiverWire.DropTargetId.TryWriteBytes(bytes.AsSpan(8)); return bytes;
    }
    [Fact] public void AcceptanceAddsOnlyTheObservedCompactLocalLength()
    {
        foreach (int size in new[] { 0, 1, 23, 24, 67, 68, 71, 72, 73, 65536, 65537 })
        foreach (uint kind in new uint[] { 0, 1, 2, 4, 8, uint.MaxValue })
        {
            byte[] bytes = new byte[size]; if (size >= 24) { Header(24).CopyTo(bytes, 0); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), kind); }
            bool acceptedLength = bytes.Length == 68 || bytes.Length >= 72;
            bool expected = acceptedLength && bytes.Length <= OleReceiverWire.ExportLimit && BinaryPrimitives.ReadUInt32LittleEndian(bytes) == 0x574f454d &&
                BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) == 1 && new Guid(bytes.AsSpan(8, 16)) == OleReceiverWire.DropTargetId;
            bool accepted = Record.Exception(() => OleReceiverWire.ValidateExport(bytes)) is null;
            Assert.Equal(expected, accepted);
        }
    }
    [Fact] public void LengthAndCapacityFailuresAreExactAndBounded()
    {
        var low = OleExportContract.Inspect(OleExportSource.NativeExport, 67, 80, Header(24));
        Assert.Equal(OleExportFailure.LengthLower, low.Failure); Assert.Equal(67, low.Length); Assert.Equal(80, low.Capacity);
        Assert.Equal(OleHeaderMatch.Match, low.Signature); Assert.Equal(OlePacketKind.Standard, low.Kind); Assert.Equal(OleHeaderMatch.Match, low.Iid);
        var high = OleExportContract.Inspect(OleExportSource.NativeExport, 65537, 131072, Header(24)); Assert.Equal(OleExportFailure.LengthUpper, high.Failure); Assert.Equal(65537, high.Length);
        var capacity = OleExportContract.Inspect(OleExportSource.NativeExport, 72, 71, Header(24)); Assert.Equal(OleExportFailure.Capacity, capacity.Failure);
        var huge = OleExportContract.Inspect(OleExportSource.NativeExport, ulong.MaxValue, ulong.MaxValue, Header(24)); Assert.Equal(OleExportFailure.LengthUpper, huge.Failure); Assert.Null(huge.Length); Assert.Null(huge.Capacity);
    }
    [Fact] public void HeaderFailuresKeepMaliciousPacketRejection()
    {
        foreach (var kind in new[] { OlePacketKind.Handler, OlePacketKind.Custom, OlePacketKind.Extended, OlePacketKind.Unknown })
        {
            byte[] bytes = Header(); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), kind switch { OlePacketKind.Handler => 2u, OlePacketKind.Custom => 4u, OlePacketKind.Extended => 8u, _ => 99u });
            var observed = OleExportContract.Inspect(OleExportSource.ParentImport, 72, 72, bytes); Assert.Equal(kind, observed.Kind); Assert.Equal(OleExportFailure.PacketType, observed.Failure);
            Assert.Throws<InvalidDataException>(() => OleReceiverWire.ValidateExport(bytes));
        }
        byte[] signature = Header(); signature[0] = 0; Assert.Equal(OleExportFailure.Signature, OleExportContract.Inspect(OleExportSource.ParentImport, 72, 72, signature).Failure);
        byte[] iid = Header(); iid[8] ^= 1; Assert.Equal(OleExportFailure.Iid, OleExportContract.Inspect(OleExportSource.ParentImport, 72, 72, iid).Failure);
    }
    [Fact] public void CompactLocalExceptionStillRejectsOtherShortLengthsAndMalformedHeaders()
    {
        foreach (int size in Enumerable.Range(0, 68).Concat(new[] { 69, 70, 71 }))
        {
            byte[] bytes = new byte[size]; if (size >= 24) Header(24).CopyTo(bytes, 0);
            Assert.False(OleExportContract.IsAcceptedLength((ulong)size));
            Assert.Throws<InvalidDataException>(() => OleReceiverWire.ValidateExport(bytes));
        }
        Assert.True(OleExportContract.IsAcceptedLength(68));
        Assert.Equal(OleExportFailure.Capacity, OleExportContract.Inspect(OleExportSource.NativeExport, 68, 67, Header(24)).Failure);
        foreach (uint kind in new uint[] { 0, 2, 4, 8, uint.MaxValue })
        {
            byte[] bytes = Header(68); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), kind);
            Assert.Throws<InvalidDataException>(() => OleReceiverWire.ValidateExport(bytes));
        }
        byte[] signature = Header(68); signature[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => OleReceiverWire.ValidateExport(signature));
        byte[] iid = Header(68); iid[8] ^= 1;
        Assert.Throws<InvalidDataException>(() => OleReceiverWire.ValidateExport(iid));
    }
    [Fact] public void CompactLocalHeaderAdmissionDoesNotClaimUnmarshalSuccess()
    {
        byte[] synthetic = Header(68); OleReceiverWire.ValidateExport(synthetic);
        Assert.Equal(OleExportFailure.None, OleExportContract.Inspect(OleExportSource.ParentImport, 68, 68, synthetic).Failure);
        // Synthetic bytes never enter CoUnmarshalInterface; the one real pre-fix CI did not reach Import.
    }
    [Fact] public void HeaderOnlyPassIsNeverAClaimOfComValidity()
    {
        byte[] synthetic = Header(); OleReceiverWire.ValidateExport(synthetic);
        Assert.All(synthetic.AsSpan(24).ToArray(), value => Assert.Equal(0, value));
        Assert.Equal(OleExportFailure.None, OleExportContract.Inspect(OleExportSource.ParentImport, 72, 72, synthetic).Failure);
        // The real CI requires actual CoUnmarshalInterface success and owned cleanup in addition to this classifier.
    }
    [Fact] public void MissingHeaderCannotReadOutsideProvidedPrefix()
    {
        for (int length = 0; length < 24; length++)
        {
            var observed = OleExportContract.Inspect(OleExportSource.NativeExport, 0, 0, new byte[length]);
            Assert.Equal(OleExportFailure.LengthLower, observed.Failure); Assert.Equal(OleHeaderMatch.Missing, observed.Iid);
        }
    }
    [Fact] public void FormatRoundTripsOnlyApprovedFields()
    {
        foreach (var source in Enum.GetValues<OleExportSource>())
        foreach (var failure in Enum.GetValues<OleExportFailure>())
        foreach (int? size in new int?[] { null, 0, 68, 72, 65536, 131072 })
        {
            var input = new OleExportObservation(source, failure, size, size, OleHeaderMatch.Match, OlePacketKind.Standard, OleHeaderMatch.Match);
            Assert.True(OleExportContract.TryParse(Encoding.ASCII.GetBytes(OleExportContract.Format(input)), out var actual)); Assert.Equal(input, actual);
        }
    }
    [Fact] public void RawIdentitiesSecretsAndNoncanonicalNumbersAreRejected()
    {
        foreach (string value in new[] { "PUBLIC_SENTINEL", "0", "nativeexport", "NativeExport|nonce=PUBLIC_SENTINEL", "NativeExport\npath=PUBLIC_SENTINEL" })
            Assert.False(OleExportContract.TryParse(Encoding.UTF8.GetBytes($"KYOE1|{value}|None|72|72|Match|Standard|Match"), out _));
        foreach (string size in new[] { "-1", "+1", "001", "131073", "2147483647", "4294967295", "72 ", "PUBLIC_SENTINEL" })
            Assert.False(OleExportContract.TryParse(Encoding.ASCII.GetBytes($"KYOE1|NativeExport|None|{size}|72|Match|Standard|Match"), out _));
        Assert.False(OleExportContract.TryParse(Encoding.ASCII.GetBytes("KYOE1|NativeExport|None|72|72|Match|Standard|Match|IPID=PUBLIC_SENTINEL"), out _));
        Assert.Throws<ArgumentException>(() => OleExportContract.Format(new(OleExportSource.NativeExport, OleExportFailure.None, 131073, 72, OleHeaderMatch.Match, OlePacketKind.Standard, OleHeaderMatch.Match)));
    }
    [Fact] public async Task MixedDiagnosticsShareTheSameFiniteRetentionAndDrainAllOutput()
    {
        string stage = OleStartupDiagnostics.Format(new(OleStartupStage.Entry, OleExceptionCode.None, 0));
        string export = OleExportContract.Format(OleExportContract.Inspect(OleExportSource.NativeExport, 67, 80, Header(24)));
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat(stage + "\n" + export + "\n", 1000))));
        var drain = new OleStartupDiagnosticDrain(); await drain.RunAsync(input);
        Assert.Equal(OleStartupDiagnostics.RecordLimit, drain.Snapshot.Length + drain.ExportSnapshot.Length);
        Assert.Equal(16, drain.ExportSnapshot.Length); Assert.Equal(input.Length, input.Position);
        Assert.All(drain.ExportSnapshot, item => Assert.Equal(OleExportFailure.LengthLower, item.Failure));
    }
}
