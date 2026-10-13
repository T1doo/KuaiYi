using System.Buffers.Binary;
using System.Security.Cryptography;

namespace KuaiYi.SelectionReusePrototype;

// Offline protocol experiment only. No process, UIA, target access, timer or production reference.
public enum ReuseState { Disabled, Fresh, Running, ResetPending, Idle, Retiring, Exited }
public enum ReuseFailure { None, Protocol, ConcurrentRequest, IdentityChanged, Cancelled, HardTimeout, ResetFailed, Crash, Exhausted, InitializationFailed }
public enum ReuseKind : byte { Begin=1, Initialized=2, Completed=3, Quiescent=4, InitializationFailed=5 }
public enum ReuseOutcome : byte { None, Success, Empty, Sensitive, Stale, TooLarge, SoftTimeout, InitializationFailed, BrokerFailure }
[Flags] public enum PersistentResources : ushort { None=0, WindowlessMta=1, Automation=2 }
[Flags] public enum RequestResources : ushort { None=0, Root=1, Focus=2, Hit=4, Pattern=8, Range=16, Attributes=32, Text=64, Buffers=128 }
public readonly record struct ReuseTarget(int Pid,long StartTicks,long Hwnd)
{
    public bool Valid => Pid>0&&StartTicks>0&&Hwnd!=0;
}
public sealed record ReuseFrame(ReuseKind Kind,byte Sequence,Guid Nonce,ReuseTarget Target,
    bool Reused,long ElapsedMs,PersistentResources Persistent=PersistentResources.None,
    RequestResources Transient=RequestResources.None,byte PendingOperations=0,bool ResetOk=false,
    ReuseOutcome Outcome=ReuseOutcome.None,byte[]? Payload=null);

// Fixed 96 bytes, at most 32 synthetic bytes. There is no arbitrary diagnostic string.
public static class ReuseFrameCodec
{
    public const int Size=96,MaxPayload=32;
    public static byte[] Encode(ReuseFrame frame)
    {
        var bytes=new byte[Size];
        bytes[0]=2;bytes[1]=(byte)frame.Kind;bytes[2]=frame.Sequence;
        bytes[3]=(byte)((frame.Reused?1:0)|(frame.ResetOk?2:0));bytes[4]=(byte)frame.Outcome;
        if(frame.Payload is {Length:>MaxPayload})throw new InvalidDataException("Frame bounds.");
        bytes[5]=(byte)(frame.Payload?.Length??0);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(6),(ushort)frame.Persistent);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8),(ushort)frame.Transient);
        bytes[10]=frame.PendingOperations;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(12),frame.Target.Pid);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(16),frame.Target.StartTicks);
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(24),frame.Target.Hwnd);
        frame.Nonce.TryWriteBytes(bytes.AsSpan(32,16));
        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(48),frame.ElapsedMs);
        frame.Payload?.CopyTo(bytes,64);
        try{Validate(bytes);return bytes;}catch{CryptographicOperations.ZeroMemory(bytes);throw;}
    }
    private static void Validate(ReadOnlySpan<byte> bytes)
    {
        if(bytes.Length!=Size||bytes[0]!=2||bytes[1] is <1 or >5||bytes[2] is <1 or >2||
            bytes[3]>3||bytes[4]>8||bytes[5]>MaxPayload||bytes[10]>64||bytes[11]!=0||
            BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..])>3||BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..])>255||
            bytes[56..64].ContainsAnyExcept((byte)0)||bytes[(64+bytes[5])..].ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Frame bounds.");
        var target=new ReuseTarget(BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]),BinaryPrimitives.ReadInt64LittleEndian(bytes[16..]),BinaryPrimitives.ReadInt64LittleEndian(bytes[24..]));
        long elapsed=BinaryPrimitives.ReadInt64LittleEndian(bytes[48..]);
        if(!target.Valid||new Guid(bytes[32..48])==Guid.Empty||elapsed is <0 or >600)
            throw new InvalidDataException("Frame identity.");
    }
    public static ReuseFrame Decode(ReadOnlySpan<byte> bytes)
    {
        Validate(bytes);
        return new((ReuseKind)bytes[1],bytes[2],new(bytes[32..48]),
            new(BinaryPrimitives.ReadInt32LittleEndian(bytes[12..]),BinaryPrimitives.ReadInt64LittleEndian(bytes[16..]),BinaryPrimitives.ReadInt64LittleEndian(bytes[24..])),
            (bytes[3]&1)!=0,BinaryPrimitives.ReadInt64LittleEndian(bytes[48..]),
            (PersistentResources)BinaryPrimitives.ReadUInt16LittleEndian(bytes[6..]),
            (RequestResources)BinaryPrimitives.ReadUInt16LittleEndian(bytes[8..]),bytes[10],(bytes[3]&2)!=0,
            (ReuseOutcome)bytes[4],bytes[64..(64+bytes[5])].ToArray());
    }
}

public sealed class ReuseProtocolPrototype
{
    private const PersistentResources Allowed=PersistentResources.WindowlessMta|PersistentResources.Automation;
    private readonly object sync=new();private readonly Func<long> clock;private readonly Guid ownedChild;
    private ReuseState state;private ReuseFailure failure;
    private ReuseTarget? identity;private ReuseFrame? active;private Guid firstNonce;
    private byte issued;private bool initialized;private long origin,lastTime,lastChildElapsed;
    private byte[]? buffered;private ReuseOutcome pendingOutcome;
    private int confirmed;private bool terminateRequested;
    public ReuseProtocolPrototype(Func<long> monotonicMilliseconds,Guid ownedChildToken,bool enabled=false)
    {clock=monotonicMilliseconds;ownedChild=ownedChildToken;state=enabled&&ownedChildToken!=Guid.Empty?ReuseState.Fresh:ReuseState.Disabled;}
    public ReuseState State{get{lock(sync)return state;}}
    public ReuseFailure Failure{get{lock(sync)return failure;}}
    public bool TerminateOwnedChildRequested{get{lock(sync)return terminateRequested;}}
    public bool CanReplaceProcess{get{lock(sync)return state==ReuseState.Exited;}}
    public int ConfirmedRequests{get{lock(sync)return confirmed;}}
    public bool HasBufferedPayload{get{lock(sync)return buffered!=null;}}
    internal byte[]? BufferedForTest{get{lock(sync)return buffered;}}
    private void ClearRequest()
    {if(buffered!=null)CryptographicOperations.ZeroMemory(buffered);buffered=null;active=null;pendingOutcome=ReuseOutcome.None;}
    private void Retire(ReuseFailure reason)
    {if(state is ReuseState.Disabled or ReuseState.Exited)return;if(failure==ReuseFailure.None)failure=reason;ClearRequest();state=ReuseState.Retiring;terminateRequested=true;}
    private bool InTime()
    {
        long now=clock();if(now<lastTime){Retire(ReuseFailure.Protocol);return false;}lastTime=now;
        // Subtract without overflow: elapsed values are bounded by the request's 600ms ceiling.
        if(now>=origin&&((ulong)now-(ulong)origin)>=600){Retire(ReuseFailure.HardTimeout);return false;}
        return true;
    }
    public ReuseFrame? Begin(ReuseTarget target,Guid nonce)
    {
        lock(sync)
        {
            if(state is ReuseState.Disabled or ReuseState.Retiring or ReuseState.Exited)return null;
            if(state is ReuseState.Running or ReuseState.ResetPending){Retire(ReuseFailure.ConcurrentRequest);return null;}
            if(!target.Valid||nonce==Guid.Empty||nonce==firstNonce){Retire(ReuseFailure.Protocol);return null;}
            if(identity!=null&&identity!=target){Retire(ReuseFailure.IdentityChanged);return null;}
            if(issued>=2){Retire(ReuseFailure.Exhausted);return null;}
            long now=clock();if(issued>0&&now<lastTime){Retire(ReuseFailure.Protocol);return null;}
            origin=lastTime=now;lastChildElapsed=0;identity=target;
            issued++;if(issued==1)firstNonce=nonce;
            active=new(ReuseKind.Begin,issued,nonce,target,issued==2,0);
            initialized=issued==2;state=ReuseState.Running;
            return active;
        }
    }
    public bool CanAccessTarget()
    {lock(sync)return state==ReuseState.Running&&InTime()&&initialized&&((ulong)lastTime-(ulong)origin)<250;}
    public void ObserveDeadline()
    {lock(sync){if(state is ReuseState.Running or ReuseState.ResetPending)InTime();}}
    public void ObserveIdentity(ReuseTarget target)
    {lock(sync){if(identity!=null&&identity!=target)Retire(ReuseFailure.IdentityChanged);}}
    public void Cancel(){lock(sync)Retire(ReuseFailure.Cancelled);}
    public void Crash(){lock(sync)Retire(ReuseFailure.Crash);}
    public void FinishRound(){lock(sync)Retire(ReuseFailure.Exhausted);}
    public void ProtocolFault(){lock(sync)Retire(ReuseFailure.Protocol);}
    internal void DiscardBufferedPayload(){lock(sync){if(buffered!=null)CryptographicOperations.ZeroMemory(buffered);buffered=null;}}
    // Host takes ownership of the complete fixed frame and clears it even if late or invalid.
    public bool ReceiveOwnedFrame(byte[] bytes)
    {
        lock(sync)
        {
            ReuseFrame? frame=null;
            try
            {
                if(state is ReuseState.Disabled or ReuseState.Retiring or ReuseState.Exited)return false;
                if(active==null){Retire(ReuseFailure.Protocol);return false;}
                if(!InTime())return false;
                frame=ReuseFrameCodec.Decode(bytes);
                if(frame.Nonce!=active.Nonce||frame.Sequence!=active.Sequence||frame.Target!=active.Target||frame.Reused!=active.Reused||frame.ElapsedMs<lastChildElapsed)
                {Retire(frame.Target!=active.Target?ReuseFailure.IdentityChanged:ReuseFailure.Protocol);return false;}
                lastChildElapsed=frame.ElapsedMs;
                if(frame.ElapsedMs>=600){Retire(ReuseFailure.HardTimeout);return false;}
                if(frame.Kind==ReuseKind.InitializationFailed&&state==ReuseState.Running&&!initialized&&issued==1&&frame.Persistent==PersistentResources.None&&frame.Transient==RequestResources.None&&frame.PendingOperations==0&&!frame.ResetOk&&frame.Outcome is (ReuseOutcome.InitializationFailed or ReuseOutcome.SoftTimeout)&&frame.Payload!.Length==0)
                {Retire(ReuseFailure.InitializationFailed);return true;}
                if(frame.Kind==ReuseKind.Initialized&&state==ReuseState.Running&&!initialized&&issued==1&&
                    frame.Persistent==Allowed&&frame.Transient==RequestResources.None&&frame.PendingOperations==0&&
                    !frame.ResetOk&&frame.Outcome==ReuseOutcome.None&&frame.Payload!.Length==0)
                {initialized=true;return true;}
                if(frame.Kind==ReuseKind.Completed&&state==ReuseState.Running&&initialized&&frame.Persistent==Allowed&&
                    frame.Outcome is not (ReuseOutcome.None or ReuseOutcome.InitializationFailed)&&!frame.ResetOk)
                {
                    if(frame.Outcome==ReuseOutcome.Stale){Retire(ReuseFailure.IdentityChanged);return false;}
                    if(frame.Outcome!=ReuseOutcome.Success&&frame.Payload!.Length!=0||
                        frame.Outcome==ReuseOutcome.Success&&(frame.ElapsedMs>=250||((ulong)lastTime-(ulong)origin)>=250))
                    {Retire(ReuseFailure.Protocol);return false;}
                    buffered=frame.Payload!.ToArray();pendingOutcome=frame.Outcome;state=ReuseState.ResetPending;return true;
                }
                if(frame.Kind==ReuseKind.Quiescent&&state==ReuseState.ResetPending)
                {
                    if(!frame.ResetOk||frame.Persistent!=Allowed||frame.Transient!=RequestResources.None||
                        frame.PendingOperations!=0||frame.Payload!.Length!=0||frame.Outcome!=pendingOutcome)
                    {Retire(ReuseFailure.ResetFailed);return false;}
                    // This is a protocol claim plus host-buffer erasure, NOT proof about UIAutomationCore.
                    ClearRequest();confirmed++;state=ReuseState.Idle;
                    if(issued==2)Retire(ReuseFailure.Exhausted);
                    return true;
                }
                Retire(ReuseFailure.Protocol);return false;
            }
            catch(InvalidDataException){Retire(ReuseFailure.Protocol);return false;}
            finally
            {if(frame?.Payload!=null)CryptographicOperations.ZeroMemory(frame.Payload);CryptographicOperations.ZeroMemory(bytes);}
        }
    }
    // Only a held owner can attest actual exit. A kill request or completion frame is insufficient.
    public bool ConfirmOwnedProcessExit(Guid token)
    {lock(sync){if(state==ReuseState.Disabled||token!=ownedChild||token==Guid.Empty)return false;if(state!=ReuseState.Retiring)Retire(ReuseFailure.Crash);ClearRequest();state=ReuseState.Exited;return true;}}
}
