using System.Buffers.Binary;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
[assembly: InternalsVisibleTo("KuaiYi.Core.Tests")]
namespace KuaiYi.Contracts;

public enum SelectionBoundary : byte
{
    SourceIntegritySid=1,SourceIntegrityCount,SourceIntegrityValue,
    OwnIntegritySid,OwnIntegrityCount,OwnIntegrityValue,
    GetSelection,GetBoundingRectangles,GetText,CaptureReturned,MetadataPrepared,FramesWritten,MainReturning
}
public enum SelectionBoundaryPosition : byte { Enter=1,Returned=2 }
public sealed record SelectionReachedBoundary(string Boundary,string Position,int Sequence);

/// <summary>Diagnostic evidence only: never a trusted selection, fault instruction, or complete broker observation.</summary>
public static class SelectionBoundaryWire
{
    public const int Capacity=64,WordOffset=24,MaximumSequence=16384;
    private const uint Magic=0x3142594b;
    public static byte[] Header(Guid request)
    {
        if(request==Guid.Empty)throw new ArgumentException("Empty diagnostic identity.");
        byte[] header=new byte[Capacity];BinaryPrimitives.WriteUInt32LittleEndian(header,Magic);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4),1);request.TryWriteBytes(header.AsSpan(8,16));return header;
    }
    internal static long Encode(SelectionBoundary boundary,SelectionBoundaryPosition position,int sequence)
    {
        if(!Enum.IsDefined(boundary)||!Enum.IsDefined(position)||sequence is <1 or >MaximumSequence)return 0;
        return unchecked((long)(((ulong)(uint)sequence<<32)|0xb14d0000UL|((ulong)position<<8)|(byte)boundary));
    }
    public static SelectionReachedBoundary? Decode(ReadOnlySpan<byte> header,long word,Guid request)
    {
        if(request==Guid.Empty||header.Length!=Capacity||BinaryPrimitives.ReadUInt32LittleEndian(header)!=Magic||BinaryPrimitives.ReadUInt32LittleEndian(header[4..])!=1||new Guid(header.Slice(8,16))!=request||header[32..].ContainsAnyExcept((byte)0))return null;
        ulong value=unchecked((ulong)word);int sequence=(int)(value>>32);
        var boundary=(SelectionBoundary)(byte)value;var position=(SelectionBoundaryPosition)(byte)(value>>8);
        if((value&0xffff0000UL)!=0xb14d0000UL||sequence is <1 or >MaximumSequence||!Enum.IsDefined(boundary)||!Enum.IsDefined(position))return null;
        return new(boundary.ToString(),position.ToString(),sequence);
    }
}

/// <summary>One parent-owned page-file mapping per request. No disk file, locks, threads, stream, or flush.</summary>
public sealed unsafe class SelectionBoundaryMap:IDisposable
{
    private readonly MemoryMappedFile mapping;private readonly MemoryMappedViewAccessor view;
    private byte* pointer;private int sequence;private bool disposed;
    private SelectionBoundaryMap(MemoryMappedFile mapping)
    {
        this.mapping=mapping;
        try
        {
            view=mapping.CreateViewAccessor(0,SelectionBoundaryWire.Capacity,MemoryMappedFileAccess.ReadWrite);
            try {byte* acquired=null;view.SafeMemoryMappedViewHandle.AcquirePointer(ref acquired);pointer=acquired+view.PointerOffset;}
            catch {view.Dispose();throw;}
        }
        catch {mapping.Dispose();throw;}
    }
    private static string Name(Guid request)=>"Local\\KuaiYi-Boundary-"+request.ToString("N");
    public static SelectionBoundaryMap? TryCreate(Guid request)
    {
        if(!OperatingSystem.IsWindows()||request==Guid.Empty)return null;
        try {return Initialize(new(MemoryMappedFile.CreateNew(Name(request),SelectionBoundaryWire.Capacity)),request);}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){return null;}
    }
    public static SelectionBoundaryMap? TryOpen(Guid request)
    {
        if(!OperatingSystem.IsWindows()||request==Guid.Empty)return null;
        try
        {
            var result=new SelectionBoundaryMap(MemoryMappedFile.OpenExisting(Name(request),MemoryMappedFileRights.ReadWrite));
            if(!result.HeaderMatches(request)){result.Dispose();return null;}return result;
        }
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){return null;}
    }
    private static SelectionBoundaryMap Initialize(SelectionBoundaryMap map,Guid request)
    {try{SelectionBoundaryWire.Header(request).CopyTo(new Span<byte>(map.pointer,SelectionBoundaryWire.Capacity));return map;}catch{map.Dispose();throw;}}
    internal static SelectionBoundaryMap CreateAnonymousForTest(Guid request)=>Initialize(new(MemoryMappedFile.CreateNew(null,SelectionBoundaryWire.Capacity)),request);
    private bool HeaderMatches(Guid request)
    {
        var bytes=new ReadOnlySpan<byte>(pointer,SelectionBoundaryWire.Capacity);
        // Use a known valid word to validate the immutable request header before publishing anything.
        return SelectionBoundaryWire.Decode(bytes,SelectionBoundaryWire.Encode(SelectionBoundary.GetSelection,SelectionBoundaryPosition.Enter,1),request)!=null;
    }
    public void Reach(SelectionBoundary boundary,SelectionBoundaryPosition position)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        long word=SelectionBoundaryWire.Encode(boundary,position,++sequence);
        Interlocked.Exchange(ref *(long*)(pointer+SelectionBoundaryWire.WordOffset),word);
    }
    public SelectionReachedBoundary? Read(Guid request)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        long word=Interlocked.CompareExchange(ref *(long*)(pointer+SelectionBoundaryWire.WordOffset),0,0);
        return SelectionBoundaryWire.Decode(new ReadOnlySpan<byte>(pointer,SelectionBoundaryWire.Capacity),word,request);
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;pointer=null;
        view.SafeMemoryMappedViewHandle.ReleasePointer();view.Dispose();mapping.Dispose();
    }
}
