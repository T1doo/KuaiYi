using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using KuaiYi.Contracts;
namespace KuaiYi.Imaging;

public enum EncodedRasterFormat{Png,Jpeg}
public sealed record ImageDecodeJob(Guid SessionId,long Version,long Epoch,int ByteLength,string Sha256,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? PrepareLongEdge=null);
public sealed record ImagePreparedInfo(int SourceWidth,int SourceHeight,ImageOrientation SourceOrientation);
// Tightly packed top-down BGRA8888. Preparation retains only numeric dimensions/direction, never arbitrary EXIF/GPS/paths.
public sealed record ImageDecodeReply(ImageDecodeJob Job,ImageFailure Failure,EncodedRasterFormat? Format=null,int Width=0,int Height=0,int Stride=0,int ByteLength=0,string? Sha256=null,string? PixelFormat=null,int Frames=0,int Orientation=0,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] ImagePreparedInfo? PreparedInfo=null);
public sealed class DecodedRaster:IDisposable
{
    private byte[]? bytes;
    public ImageDecodeReply Metadata{get;}
    public ImageTransform? Transform=>Metadata.PreparedInfo is {} p?ImageTransform.PlanEncoded(new((uint)p.SourceWidth,(uint)p.SourceHeight),p.SourceOrientation,Metadata.Job.PrepareLongEdge!.Value):null;
    public ReadOnlyMemory<byte> Bytes=>bytes??throw new ObjectDisposedException(nameof(DecodedRaster));
    public DecodedRaster(ImageDecodeReply metadata,byte[] owned)
    {
        try{ImageDecodePolicy.ValidateReply(metadata);if(metadata.Failure!=ImageFailure.None||owned.Length!=metadata.ByteLength||ImageDecodePolicy.Hash(owned)!=metadata.Sha256)throw new InvalidDataException("Invalid decoded raster.");Metadata=metadata;bytes=owned;}
        catch{CryptographicOperations.ZeroMemory(owned);throw;}
    }
    public void Dispose(){var previous=Interlocked.Exchange(ref bytes,null);if(previous!=null)CryptographicOperations.ZeroMemory(previous);}
}
public static class ImageDecodePolicy
{
    private static readonly uint[] CrcTable=Enumerable.Range(0,256).Select(value=>{uint c=(uint)value;for(int i=0;i<8;i++)c=(c>>1)^((c&1)!=0?0xedb88320u:0);return c;}).ToArray();
    public static string Hash(ReadOnlySpan<byte> bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    public static void ValidateJob(ImageDecodeJob job)
    {
        if(job.PrepareLongEdge is <1 or >ImageTransform.LongEdgeLimit||job.SessionId==Guid.Empty||job.Version<=0||job.Epoch<=0||job.ByteLength is <=0 or >ImagePolicy.ByteLimit||!Digest(job.Sha256))throw new InvalidDataException("Invalid decode job.");
    }
    private static bool Digest(string? value)=>value is {Length:64}&&value.All(c=>char.IsAsciiDigit(c)||c is >= 'a' and <= 'f');
    public static (EncodedRasterFormat Format,ImageSize Size) Inspect(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length>ImagePolicy.ByteLimit)throw new ImageJobException(ImageFailure.ImageTooLarge);
        if(bytes.Length>=8&&bytes[..8].SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))return(EncodedRasterFormat.Png,ImagePolicy.InspectPng(bytes));
        if(bytes.Length<4||bytes[0]!=255||bytes[1]!=216)throw new ImageJobException(ImageFailure.UnsupportedImage);
        if(bytes[^2]!=255||bytes[^1]!=217)throw new ImageJobException(ImageFailure.InvalidImage);
        int offset=2;ImageSize? size=null;
        while(offset<bytes.Length-2)
        {
            if(bytes[offset++]!=255)throw new ImageJobException(ImageFailure.InvalidImage);
            while(offset<bytes.Length&&bytes[offset]==255)offset++;
            if(offset>=bytes.Length)break;byte marker=bytes[offset++];
            if(marker is 0 or 216 or 217 or >=208 and <=215)throw new ImageJobException(ImageFailure.InvalidImage);
            if(bytes.Length-offset<2)break;int length=BinaryPrimitives.ReadUInt16BigEndian(bytes[offset..]);
            if(length<2||length>bytes.Length-offset)break;
            if(marker==218){if(size is {} found)return(EncodedRasterFormat.Jpeg,found);break;}
            if(marker is 192 or 193 or 194 or 195 or 197 or 198 or 199 or 201 or 202 or 203 or 205 or 206 or 207)
            {
                if(size!=null||length<8)break;
                uint height=BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset+3)..]),width=BinaryPrimitives.ReadUInt16BigEndian(bytes[(offset+5)..]);ImagePolicy.ValidateDimensions(width,height);size=new(width,height);
            }
            offset+=length;
        }
        throw new ImageJobException(ImageFailure.InvalidImage);
    }
    public static int RasterBytes(int width,int height)
    {if(width<=0||height<=0)throw new InvalidDataException("Invalid raster dimensions.");ImagePolicy.ValidateDimensions((uint)width,(uint)height);return checked(checked(width*height)*4);}
    // Full PNG container checks run only inside the isolated worker, never on a UI caller.
    public static void ValidatePngContainer(ReadOnlySpan<byte> bytes,CancellationToken cancellation)
    {
        int offset=8;bool first=true,idat=false;
        while(bytes.Length-offset>=12)
        {
            cancellation.ThrowIfCancellationRequested();uint count=BinaryPrimitives.ReadUInt32BigEndian(bytes[offset..]);
            if(count>(uint)(bytes.Length-offset-12))break;int length=(int)count;var type=bytes.Slice(offset+4,4);
            if(first?(!type.SequenceEqual("IHDR"u8)||length!=13):type.SequenceEqual("IHDR"u8))break;first=false;
            ValidatePngChunkCrc(bytes.Slice(offset+4,length+4),BinaryPrimitives.ReadUInt32BigEndian(bytes[(offset+8+length)..]),cancellation);
            if(type.SequenceEqual("acTL"u8))throw new ImageJobException(ImageFailure.UnsupportedImage);
            if(type.SequenceEqual("IDAT"u8))idat=true;
            offset+=length+12;
            if(type.SequenceEqual("IEND"u8))
            {if(length==0&&offset==bytes.Length&&idat)return;break;}
        }
        throw new ImageJobException(ImageFailure.InvalidImage);
    }
    internal static void ValidatePngChunkCrc(ReadOnlySpan<byte> chunk,uint expected,CancellationToken cancellation)
    {
        uint crc=uint.MaxValue;
        for(int i=0;i<chunk.Length;i++){if((i&65535)==0)cancellation.ThrowIfCancellationRequested();crc=CrcTable[(crc^chunk[i])&255]^(crc>>8);}
        if(~crc!=expected)throw new ImageJobException(ImageFailure.InvalidImage);
    }
    public static void ValidateReply(ImageDecodeReply reply)
    {
        ValidateJob(reply.Job);
        if(!Enum.IsDefined(reply.Failure))throw new InvalidDataException("Invalid decode failure.");
        if(reply.Failure!=ImageFailure.None)
        {if(reply.Format!=null||reply.Width!=0||reply.Height!=0||reply.Stride!=0||reply.ByteLength!=0||reply.Sha256!=null||reply.PixelFormat!=null||reply.Frames!=0||reply.Orientation!=0||reply.PreparedInfo!=null)throw new InvalidDataException("Failure contains raster metadata.");return;}
        if(reply.Format==null||!Enum.IsDefined(reply.Format.Value)||reply.ByteLength!=RasterBytes(reply.Width,reply.Height)||reply.Stride!=checked(reply.Width*4)||!Digest(reply.Sha256)||reply.Frames!=1||reply.Orientation!=1)throw new InvalidDataException("Invalid raster layout.");
        if(reply.Job.PrepareLongEdge is {} limit)
        {
            var p=reply.PreparedInfo??throw new InvalidDataException("Missing preparation metadata.");
            if(p.SourceWidth<=0||p.SourceHeight<=0)throw new InvalidDataException("Invalid source dimensions.");
            var plan=ImageTransform.PlanEncoded(new((uint)p.SourceWidth,(uint)p.SourceHeight),p.SourceOrientation,limit);
            if(reply.PixelFormat!="BGRA8888-opaque-white"||(uint)reply.Width!=plan.NormalizedSize.Width||(uint)reply.Height!=plan.NormalizedSize.Height)throw new InvalidDataException("Invalid prepared layout.");
        }
        else if(reply.PreparedInfo!=null||reply.PixelFormat!="BGRA8888-straight")throw new InvalidDataException("Invalid raw raster metadata.");
    }
}
/// <summary>Own the exact bounded output until ordered commit and digest validation. Never return a partial raster.</summary>
public sealed class ImageRasterReceiver:IDisposable
{
    private readonly Guid id;private readonly ImageDecodeReply metadata;private byte[]? data;private int filled,next;private bool committed,disposed;
    public ImageRasterReceiver(Guid id,ImageDecodeJob job,ImageDecodeReply metadata,Action<byte[]>? allocated=null)
    {
        if(id==Guid.Empty||metadata.Job!=job)throw new InvalidDataException("Raster job mismatch.");ImageDecodePolicy.ValidateReply(metadata);this.id=id;this.metadata=metadata;
        if(metadata.Failure==ImageFailure.None)
        {data=new byte[metadata.ByteLength];try{allocated?.Invoke(data);}catch{Dispose();throw;}}
    }
    public void Chunk(IpcFrame frame)
    {
        try
        {
            if(disposed||data==null||committed||frame.Type!=IpcMessageType.BinaryChunk||frame.RequestId!=id||frame.Payload.Length<=4||BinaryPrimitives.ReadUInt32LittleEndian(frame.Payload)!=(uint)next||frame.Payload.Length-4>data.Length-filled)throw new InvalidDataException("Invalid raster chunk.");
            frame.Payload.AsSpan(4).CopyTo(data.AsSpan(filled));filled+=frame.Payload.Length-4;next++;
        }
        catch{Dispose();throw;}
    }
    public DecodedRaster? Commit(IpcFrame frame)
    {
        try
        {
            if(disposed||committed||frame.Type!=IpcMessageType.ImageDecodeCommit||frame.RequestId!=id||frame.Payload.Length!=0)throw new InvalidDataException("Invalid raster commit.");committed=true;
            if(metadata.Failure!=ImageFailure.None)return null;
            if(data==null||filled!=data.Length||ImageDecodePolicy.Hash(data)!=metadata.Sha256)throw new InvalidDataException("Incomplete raster.");
            if(metadata.PreparedInfo!=null)
                for(int i=3;i<data.Length;i+=4)if(data[i]!=255)throw new InvalidDataException("Prepared raster is not opaque.");
            var owned=data;data=null;return new(metadata,owned);
        }
        catch{Dispose();throw;}
    }
    public void Dispose(){disposed=true;var owned=data;data=null;if(owned!=null)CryptographicOperations.ZeroMemory(owned);}
}
