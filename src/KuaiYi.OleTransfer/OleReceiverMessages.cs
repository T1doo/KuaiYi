using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using KuaiYi.Core;
namespace KuaiYi.OleTransfer;
public sealed record OleReceiverHello(Guid Instance,long Epoch,int AppProcessId,long Hwnd,byte[] Nonce,string ActivationName);
public sealed record OleReceiverControl(Guid Instance,long Epoch,bool Enabled);
public sealed record OleReceiverEvent(Guid Instance,long Epoch,Guid Transaction,string Phase,bool CleanupCompleted=false,uint Effect=0,int CodeUnits=0,int ThreadId=0,string? Apartment=null,uint NativeThreadId=0);
public static class OleReceiverWire
{
    public const int ExportLimit=64*1024,TextLimit=300_006;
    public static readonly Guid DropTargetId=new("00000122-0000-0000-C000-000000000046");
    public static string ActivationName(Guid instance)=>"Local\\KuaiYi.OleGate."+instance.ToString("N");
    public static byte[] Serialize<T>(T item)=>JsonSerializer.SerializeToUtf8Bytes(item);
    public static T Read<T>(ReadOnlySpan<byte> bytes)=>JsonSerializer.Deserialize<T>(bytes)??throw new InvalidDataException("Missing OLE envelope.");
    public static void Validate(OleReceiverHello hello)
    {
        if(hello.Instance==Guid.Empty||hello.Epoch<0||hello.AppProcessId<=0||hello.Hwnd<=0||hello.Nonce is not {Length:32}||hello.ActivationName!=ActivationName(hello.Instance))throw new InvalidDataException("Invalid OLE hello.");
    }
    // Only owned-channel STANDARD OBJREF for the built-in IID; forbid custom/handler activation.
    public static void ValidateExport(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <72 or >ExportLimit||BinaryPrimitives.ReadUInt32LittleEndian(bytes)!=0x574f454d||BinaryPrimitives.ReadUInt32LittleEndian(bytes[4..])!=1||new Guid(bytes.Slice(8,16))!=DropTargetId)throw new InvalidDataException("Invalid owned OLE standard packet.");
    }
    public static string DecodeText(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length is <1 or >TextLimit)throw new InvalidDataException("Oversized OLE text.");
        string text=new UTF8Encoding(false,true).GetString(bytes);
        if(text.Length>OleUnicodeText.MaximumReadUnits||text.EnumerateRunes().Count()>OleUnicodeText.MaximumScalars||string.IsNullOrWhiteSpace(text))throw new InvalidDataException("Invalid OLE text.");return text;
    }
}
public interface IOleTargetRegistrar
{
    // Ownership transfers unconditionally; registrar clears packet only after its actual operation settles.
    Task RegisterAsync(long hwnd,byte[] ownedPacket,CancellationToken cancellation);
    ValueTask<bool> TryFinalizeAsync();
    bool IsSettledAndRevoked {get;}
}
public interface IOleActivationLease:IDisposable
{
    string Name{get;}
    void Set(long epoch,bool enabled);
}
public sealed class OleReceiverGate
{
    private readonly object sync=new();private long epoch;private bool enabled;
    public (long Epoch,bool Enabled) Snapshot{get{lock(sync)return(epoch,enabled);}}
    public bool Set(long generation,bool next){lock(sync){if(generation<epoch||next&&generation>long.MaxValue/2)return false;epoch=generation;enabled=next;return true;}}
    public bool Current(long generation){lock(sync)return enabled&&epoch==generation;}
}
