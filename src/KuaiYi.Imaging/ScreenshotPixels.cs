using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using KuaiYi.Core;
namespace KuaiYi.Imaging;

public enum ScreenshotFailure { InvalidTopology, SnapshotTooLarge, RegionTooSmall, RegionOutsideDesktop, RegionTooLarge, TopologyChanged, CaptureUnavailable, ProtectedOrBlackFrame, Timeout }
public sealed class ScreenshotException(ScreenshotFailure code):Exception(code.ToString()) { public ScreenshotFailure Code {get;}=code; }
public sealed record ScreenshotMonitor(string Id,PixelRect Bounds,DpiScale Dpi);
/// <summary>Physical edges, exclusive right/bottom. DPI affects display labels only, never composition.</summary>
public sealed class ScreenshotTopology
{
    public const long SnapshotPixelLimit=32_000_000;
    public IReadOnlyList<ScreenshotMonitor> Monitors {get;}
    public PixelRect Bounds {get;}
    public ScreenshotTopology(IEnumerable<ScreenshotMonitor> monitors)
    {
        var items=monitors.OrderBy(m=>m.Id,StringComparer.Ordinal).ToArray();
        if(items.Length is <1 or >16||items.Select(m=>m.Id).Distinct().Count()!=items.Length)throw new ScreenshotException(ScreenshotFailure.InvalidTopology);
        long total=0;
        foreach(var m in items)
        {
            if(string.IsNullOrWhiteSpace(m.Id)||m.Dpi.Factor==0||m.Bounds.Width is <=0 or >16384||m.Bounds.Height is <=0 or >16384)throw new ScreenshotException(ScreenshotFailure.InvalidTopology);
            total=checked(total+(long)m.Bounds.Width*m.Bounds.Height);
        }
        if(total>SnapshotPixelLimit)throw new ScreenshotException(ScreenshotFailure.SnapshotTooLarge);
        for(int i=0;i<items.Length;i++)for(int j=i+1;j<items.Length;j++)if(items[i].Bounds.OverlapArea(items[j].Bounds)>0)throw new ScreenshotException(ScreenshotFailure.InvalidTopology);
        Monitors=Array.AsReadOnly(items);Bounds=new(items.Min(m=>m.Bounds.Left),items.Min(m=>m.Bounds.Top),items.Max(m=>m.Bounds.Right),items.Max(m=>m.Bounds.Bottom));
        if((long)Bounds.Right-Bounds.Left>32768||(long)Bounds.Bottom-Bounds.Top>32768)throw new ScreenshotException(ScreenshotFailure.InvalidTopology);
    }
    public bool Matches(ScreenshotTopology other)=>Monitors.SequenceEqual(other.Monitors);
    public PixelRect Selection(PixelPoint from,PixelPoint to)
    {
        var r=new PixelRect(Math.Min(from.X,to.X),Math.Min(from.Y,to.Y),Math.Max(from.X,to.X),Math.Max(from.Y,to.Y));
        Validate(r);return r;
    }
    public void Validate(PixelRect r)
    {
        if(!Bounds.Contains(r)||!Monitors.Any(m=>m.Bounds.OverlapArea(r)>0))throw new ScreenshotException(ScreenshotFailure.RegionOutsideDesktop);
        if(r.Width<8||r.Height<8)throw new ScreenshotException(ScreenshotFailure.RegionTooSmall);
        if(r.Width>ImagePolicy.SideLimit||r.Height>ImagePolicy.SideLimit||(long)r.Width*r.Height>(long)ImagePolicy.PixelLimit)throw new ScreenshotException(ScreenshotFailure.RegionTooLarge);
    }
}
/// <summary>Takes exclusive ownership of tightly packed top-down straight BGRA (not premultiplied).
/// Captured desktop buffers explicitly set alpha to255. No implicit disk/string copies.</summary>
public sealed class ScreenshotPixels:IDisposable
{
    private byte[]? pixels;
    public PixelRect Bounds {get;}
    public ReadOnlyMemory<byte> Bytes=>pixels??throw new ObjectDisposedException(nameof(ScreenshotPixels));
    public ScreenshotPixels(PixelRect bounds,byte[] ownedBgra)
    {
        try
        {
            if(bounds.Width<=0||bounds.Height<=0||(long)bounds.Width*bounds.Height>ScreenshotTopology.SnapshotPixelLimit||ownedBgra.Length!=checked(bounds.Width*bounds.Height*4))throw new ScreenshotException(ScreenshotFailure.InvalidTopology);
            Bounds=bounds;pixels=ownedBgra;
        }
        catch{CryptographicOperations.ZeroMemory(ownedBgra);throw;}
    }
    public static ScreenshotPixels FromCapturedBuffer(PixelRect bounds,byte[] owned,CancellationToken cancellation=default)
    {
        var image=new ScreenshotPixels(bounds,owned);
        try
        {
            bool visible=false;
            for(int i=0;i<owned.Length;i+=4){if((i&32767)==0)cancellation.ThrowIfCancellationRequested();visible|=owned[i]!=0||owned[i+1]!=0||owned[i+2]!=0;owned[i+3]=255;}
            if(!visible)throw new ScreenshotException(ScreenshotFailure.ProtectedOrBlackFrame);return image;
        }
        catch{image.Dispose();throw;}
    }
    public void Dispose(){var owned=Interlocked.Exchange(ref pixels,null);if(owned!=null)CryptographicOperations.ZeroMemory(owned);}
}
public sealed class ScreenshotSnapshot:IDisposable
{
    private ScreenshotPixels[]? tiles;
    public ScreenshotTopology Topology {get;}
    public IReadOnlyList<ScreenshotPixels> Tiles=>tiles??throw new ObjectDisposedException(nameof(ScreenshotSnapshot));
    public ScreenshotSnapshot(ScreenshotTopology topology,IEnumerable<ScreenshotPixels> ownedTiles)
    {
        tiles=ownedTiles.ToArray();Topology=topology;
        if(tiles.Length!=topology.Monitors.Count||!tiles.Select(t=>t.Bounds).OrderBy(r=>r.Left).ThenBy(r=>r.Top).SequenceEqual(topology.Monitors.Select(m=>m.Bounds).OrderBy(r=>r.Left).ThenBy(r=>r.Top)))
        {Dispose();throw new ScreenshotException(ScreenshotFailure.InvalidTopology);}
    }
    public ScreenshotPixels CropAndRelease(PixelRect region,ScreenshotTopology current,CancellationToken cancellation=default)
    {
        byte[]? output=null;
        try
        {
            cancellation.ThrowIfCancellationRequested();if(!Topology.Matches(current))throw new ScreenshotException(ScreenshotFailure.TopologyChanged);Topology.Validate(region);
            output=new byte[checked(region.Width*region.Height*4)];Array.Fill(output,(byte)255); // Physical gaps have no screen pixels: neutral white, no extrapolation.
            foreach(var tile in Tiles)
            {
                var b=tile.Bounds;int left=Math.Max(region.Left,b.Left),right=Math.Min(region.Right,b.Right),top=Math.Max(region.Top,b.Top),bottom=Math.Min(region.Bottom,b.Bottom);
                if(right<=left||bottom<=top)continue;
                for(int y=top;y<bottom;y++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    tile.Bytes.Span.Slice(((y-b.Top)*b.Width+left-b.Left)*4,(right-left)*4).CopyTo(output.AsSpan(((y-region.Top)*region.Width+left-region.Left)*4));
                }
            }
            var crop=new ScreenshotPixels(region,output);output=null;return crop;
        }
        finally{if(output!=null)CryptographicOperations.ZeroMemory(output);Dispose();} // Includes all validation/cancellation failures.
    }
    public void Dispose(){var owned=Interlocked.Exchange(ref tiles,null);if(owned!=null)foreach(var tile in owned)tile.Dispose();}
}
public sealed class ScreenshotPng:IDisposable
{
    private byte[]? bytes;
    public ReadOnlyMemory<byte> Bytes=>bytes??throw new ObjectDisposedException(nameof(ScreenshotPng));
    public ScreenshotPng(byte[] owned){try{ImagePolicy.InspectPng(owned);bytes=owned;}catch{CryptographicOperations.ZeroMemory(owned);throw;}}
    public void Dispose(){var owned=Interlocked.Exchange(ref bytes,null);if(owned!=null)CryptographicOperations.ZeroMemory(owned);}
}
/// <summary>Real lossless RGB PNG encoder; rows and compressed working buffers cleared, no metadata or resizing.</summary>
public static class ScreenshotPngEncoder
{
    public static ScreenshotPng Encode(ScreenshotPixels crop,CancellationToken cancellation=default,int maximumBytes=ImagePolicy.ByteLimit)
    {
        if(maximumBytes is <1 or >ImagePolicy.ByteLimit)throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var size=crop.Bounds;ImagePolicy.ValidateDimensions((uint)size.Width,(uint)size.Height);
        using var compressed=new BoundedMemory(maximumBytes);using var result=new BoundedMemory(maximumBytes);byte[] row=new byte[checked(size.Width*3+1)];
        try
        {
            using(var zip=new ZLibStream(compressed,CompressionLevel.Fastest,true))
                for(int y=0;y<size.Height;y++)
                {
                    cancellation.ThrowIfCancellationRequested();var pixels=crop.Bytes.Span.Slice(y*size.Width*4,size.Width*4);row[0]=0;
                    for(int x=0;x<size.Width;x++){row[1+x*3]=pixels[x*4+2];row[2+x*3]=pixels[x*4+1];row[3+x*3]=pixels[x*4];}zip.Write(row);
                }
            result.Write(new byte[]{137,80,78,71,13,10,26,10});Span<byte> header=stackalloc byte[13];header.Clear();BinaryPrimitives.WriteUInt32BigEndian(header,(uint)size.Width);BinaryPrimitives.WriteUInt32BigEndian(header[4..],(uint)size.Height);header[8]=8;header[9]=2;
            Chunk(result,"IHDR"u8,header);Chunk(result,"IDAT"u8,compressed.GetBuffer().AsSpan(0,(int)compressed.Length));Chunk(result,"IEND"u8,[]);
            cancellation.ThrowIfCancellationRequested();return new(result.ToArray());
        }
        finally{CryptographicOperations.ZeroMemory(row);}
    }
    private static void Chunk(Stream output,ReadOnlySpan<byte> type,ReadOnlySpan<byte> data)
    {
        Span<byte> number=stackalloc byte[4];BinaryPrimitives.WriteUInt32BigEndian(number,(uint)data.Length);output.Write(number);output.Write(type);output.Write(data);
        uint crc=uint.MaxValue;foreach(byte b in type)Acc(b);foreach(byte b in data)Acc(b);BinaryPrimitives.WriteUInt32BigEndian(number,~crc);output.Write(number);
        void Acc(byte b){crc^=b;for(int i=0;i<8;i++)crc=(crc>>1)^((crc&1)!=0?0xedb88320u:0u);}
    }
    private sealed class BoundedMemory(int limit):MemoryStream
    {
        private void Check(int count)
        {
            long required=Position+count;if(required>limit)throw new ScreenshotException(ScreenshotFailure.RegionTooLarge);
            if(required>Capacity){byte[] old=GetBuffer();Capacity=(int)Math.Min(limit,Math.Max(required,Math.Max(256L,(long)Capacity*2)));CryptographicOperations.ZeroMemory(old);}
        }
        public override void Write(byte[] buffer,int offset,int count){Check(count);base.Write(buffer,offset,count);}
        public override void Write(ReadOnlySpan<byte> buffer){Check(buffer.Length);base.Write(buffer);}
        public override void WriteByte(byte value){Check(1);base.WriteByte(value);}
        protected override void Dispose(bool disposing){if(disposing)CryptographicOperations.ZeroMemory(GetBuffer());base.Dispose(disposing);}
    }
}
