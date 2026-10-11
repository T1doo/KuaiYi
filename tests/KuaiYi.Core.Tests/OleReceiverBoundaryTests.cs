using System.Buffers.Binary;
using System.Text;
using KuaiYi.OleTransfer;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class OleReceiverBoundaryTests
{
    private static byte[] StandardPacket(){byte[] bytes=new byte[72];BinaryPrimitives.WriteUInt32LittleEndian(bytes,0x574f454d);BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),1);OleReceiverWire.DropTargetId.TryWriteBytes(bytes.AsSpan(8));return bytes;}
    [Theory][InlineData("custom")][InlineData("handler")][InlineData("iid")][InlineData("signature")][InlineData("short")][InlineData("oversize")]
    public void OnlyOwnedStandardDropTargetPacketShapeCanReachRegistrar(string kind)
    {
        byte[] bytes=StandardPacket();
        switch(kind){case "custom":BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),4);break;case "handler":BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4),2);break;case "iid":Guid.NewGuid().TryWriteBytes(bytes.AsSpan(8));break;case "signature":bytes[0]=0;break;case "short":bytes=bytes[..71];break;case "oversize":bytes=new byte[OleReceiverWire.ExportLimit+1];break;}
        Assert.Throws<InvalidDataException>(()=>OleReceiverWire.ValidateExport(bytes));OleReceiverWire.ValidateExport(StandardPacket()); // Shape validation is not COM proof or authentication.
    }
    [Theory][InlineData("empty")][InlineData("blank")][InlineData("scalars")][InlineData("bytes")][InlineData("utf8")]
    public void ActualTextDecodeEnforcesStrictFiniteUnicodeBeforeDelivery(string kind)
    {
        byte[] bytes=kind switch{"empty"=>[],"blank"=>Encoding.UTF8.GetBytes(" \n\t"),"scalars"=>Encoding.UTF8.GetBytes(new string('a',50_001)),"bytes"=>new byte[OleReceiverWire.TextLimit+1],_=>[0xf0,0x80,0x80,0x80]};
        var failure=Record.Exception(()=>OleReceiverWire.DecodeText(bytes));Assert.True(failure is InvalidDataException or DecoderFallbackException);Assert.Equal("public 文本",OleReceiverWire.DecodeText(Encoding.UTF8.GetBytes("public 文本")));
    }
    [Fact]public void RevokedSessionCannotBeRestoredByOldOrTerminalEnable()
    {
        var gate=new OleReceiverGate();Assert.True(gate.Set(5,true));Assert.True(gate.Current(5));Assert.True(gate.Set(6,false));Assert.False(gate.Set(5,true));Assert.False(gate.Current(5));Assert.True(gate.Set(long.MaxValue,false));Assert.False(gate.Set(7,true));Assert.False(gate.Set(long.MaxValue,true));Assert.False(gate.Snapshot.Enabled);
    }
}
