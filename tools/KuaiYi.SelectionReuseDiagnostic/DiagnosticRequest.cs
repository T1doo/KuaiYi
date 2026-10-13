using System.Buffers.Binary;
using KuaiYi.SelectionReusePrototype;
namespace KuaiYi.SelectionReuseDiagnostic;

public enum ReuseMatch { NotRun, Matched, NotMatched }
public enum WorkerInitialization { NotRun, Ready, Failed }
public enum WorkerTermination { NotObserved, NaturalZero, NaturalNonZero, ParentRequested }
public sealed record DiagnosticRequest(ReuseTarget Source,int DownX,int DownY,int UpX,int UpY,bool Explicit)
{
    public byte[] Encode(long origin,bool jobAttached)
    {
        if(!Source.Valid||origin<=0)throw new InvalidDataException("Request identity.");
        byte[] bytes=new byte[25];BinaryPrimitives.WriteInt32LittleEndian(bytes,DownX);BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(4),DownY);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(8),UpX);BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12),UpY);
        bytes[16]=(byte)((Explicit?1:0)|(jobAttached?2:0));BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(17),origin);return bytes;
    }
}
