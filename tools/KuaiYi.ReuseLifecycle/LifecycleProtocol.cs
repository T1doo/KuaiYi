using System.Buffers.Binary;
namespace KuaiYi.ReuseLifecycle;
public enum LifecycleCase:byte {Normal=1,BoundParentDeath=2,BeforeBindParentDeath=3,Watchdog=4}
public enum LifecyclePhase:byte {Started=1,Bound=2,Authorize=3,Initialized=4,LocalReleased=5,Quiescent=6,FinalReleased=7,Parked=8,ParentSettled=9,InitializationFailed=10,SoftTimeout=11,WatchdogExpired=12,WatchdogKillIssued=13,WatchdogNaturalExit=14,WatchdogKillNotIssued=15}
public readonly record struct WorkerIdentity(int Pid,long Created){public bool Valid=>Pid>0&&Created>0;}
public sealed record LifecycleFrame(LifecyclePhase Phase,byte Sequence,LifecycleCase Case,long Origin,Guid Nonce,WorkerIdentity Worker,int Elapsed=0);
public static class LifecycleCodec
{
    public const int Size=64;
    public static byte[] Encode(LifecycleFrame f){byte[] b=new byte[Size];BinaryPrimitives.WriteUInt32LittleEndian(b,0x434c594b);b[4]=1;b[5]=(byte)f.Phase;b[6]=f.Sequence;b[7]=(byte)f.Case;BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(8),f.Origin);f.Nonce.TryWriteBytes(b.AsSpan(16));BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(32),f.Worker.Pid);BinaryPrimitives.WriteInt64LittleEndian(b.AsSpan(36),f.Worker.Created);BinaryPrimitives.WriteInt32LittleEndian(b.AsSpan(44),f.Elapsed);Validate(b);return b;}
    private static void Validate(ReadOnlySpan<byte> b){if(b.Length!=Size||BinaryPrimitives.ReadUInt32LittleEndian(b)!=0x434c594b||b[4]!=1||b[5] is <1 or >15||b[6]>2||b[7] is <1 or >4||BinaryPrimitives.ReadInt64LittleEndian(b[8..])<0||BinaryPrimitives.ReadInt32LittleEndian(b[32..])<=0||BinaryPrimitives.ReadInt64LittleEndian(b[36..])<=0||BinaryPrimitives.ReadInt32LittleEndian(b[44..]) is <0 or >600||new Guid(b[16..32])==Guid.Empty||b[48..].ContainsAnyExcept((byte)0))throw new InvalidDataException();}
    public static LifecycleFrame Decode(ReadOnlySpan<byte> b){Validate(b);return new((LifecyclePhase)b[5],b[6],(LifecycleCase)b[7],BinaryPrimitives.ReadInt64LittleEndian(b[8..]),new(b[16..32]),new(BinaryPrimitives.ReadInt32LittleEndian(b[32..]),BinaryPrimitives.ReadInt64LittleEndian(b[36..])),BinaryPrimitives.ReadInt32LittleEndian(b[44..]));}
}
public sealed class LifecycleTrace(LifecycleCase scenario,Guid first)
{
    private WorkerIdentity? worker;private Guid nonce;private byte sequence;private LifecyclePhase last;private long lastElapsed;private long origin;
    public int ReleasedRequests{get;private set;}public bool FinalReturned{get;private set;}public bool Parked{get;private set;}public bool Bound{get;private set;}public bool ParentSettled{get;private set;}public bool WatchdogExpired{get;private set;}public bool WatchdogKillIssued{get;private set;}public LifecyclePhase? WatchdogFailure{get;private set;}
    public void Accept(LifecycleFrame f){
        if(f.Case!=scenario||!f.Worker.Valid||worker!=null&&worker!=f.Worker||(f.Elapsed>=600&&f.Phase is not (LifecyclePhase.WatchdogExpired or LifecyclePhase.WatchdogKillIssued or LifecyclePhase.WatchdogNaturalExit or LifecyclePhase.WatchdogKillNotIssued)))throw new InvalidDataException();
        if(last==0){if(f.Phase!=LifecyclePhase.Started||f.Sequence!=0||f.Origin!=0||f.Elapsed!=0||f.Nonce!=first)throw new InvalidDataException();worker=f.Worker;nonce=first;last=f.Phase;return;}
        if(f.Phase==LifecyclePhase.Bound){if(scenario==LifecycleCase.BeforeBindParentDeath||last!=LifecyclePhase.Started||f.Sequence!=0||f.Origin!=0||f.Elapsed!=0||f.Nonce!=first)throw new InvalidDataException();Bound=true;last=f.Phase;return;}
        if(f.Phase is LifecyclePhase.WatchdogExpired or LifecyclePhase.WatchdogKillIssued or LifecyclePhase.WatchdogNaturalExit or LifecyclePhase.WatchdogKillNotIssued){
            if(scenario!=LifecycleCase.Watchdog||f.Sequence!=1||f.Nonce!=nonce||f.Origin!=origin||f.Elapsed!=600||ParentSettled||WatchdogFailure!=null)throw new InvalidDataException();
            if(f.Phase==LifecyclePhase.WatchdogExpired){if(last!=LifecyclePhase.Parked||WatchdogExpired)throw new InvalidDataException();WatchdogExpired=true;}
            else {if(last!=LifecyclePhase.WatchdogExpired||!WatchdogExpired)throw new InvalidDataException();if(f.Phase==LifecyclePhase.WatchdogKillIssued)WatchdogKillIssued=true;else WatchdogFailure=f.Phase;}
            last=f.Phase;return;
        }
        if(f.Phase==LifecyclePhase.ParentSettled){if(ParentSettled||(scenario==LifecycleCase.Normal?!FinalReturned||last!=LifecyclePhase.FinalReleased:scenario!=LifecycleCase.Watchdog||!Parked||!WatchdogExpired||!WatchdogKillIssued||WatchdogFailure!=null||last!=LifecyclePhase.WatchdogKillIssued))throw new InvalidDataException();if(f.Sequence!=sequence||f.Nonce!=nonce||f.Origin!=0||f.Elapsed!=0)throw new InvalidDataException();ParentSettled=true;last=f.Phase;return;}
        if(f.Phase==LifecyclePhase.FinalReleased){if(scenario!=LifecycleCase.Normal||ReleasedRequests!=2||last!=LifecyclePhase.Quiescent||f.Sequence!=2||f.Nonce!=nonce||f.Origin!=0||f.Elapsed!=0)throw new InvalidDataException();FinalReturned=true;last=f.Phase;return;}
        if(f.Sequence!=sequence){if(f.Sequence!=sequence+1||f.Sequence>2||(f.Sequence==1?last!=LifecyclePhase.Bound||f.Nonce!=first:last!=LifecyclePhase.Quiescent||f.Nonce==nonce))throw new InvalidDataException();sequence=f.Sequence;nonce=f.Nonce;lastElapsed=0;origin=f.Origin;}
        if(f.Nonce!=nonce||f.Origin!=origin||f.Origin<=0||f.Elapsed<lastElapsed)throw new InvalidDataException();lastElapsed=f.Elapsed;
        if(f.Phase is LifecyclePhase.InitializationFailed or LifecyclePhase.SoftTimeout)throw new InvalidDataException();
        if(f.Phase==LifecyclePhase.Parked){if(f.Elapsed>=250||sequence!=1||last!=LifecyclePhase.Bound||scenario is not (LifecycleCase.BoundParentDeath or LifecycleCase.Watchdog))throw new InvalidDataException();Parked=true;last=f.Phase;return;}
        if(scenario!=LifecycleCase.Normal||f.Elapsed>=250)throw new InvalidDataException();
        if(f.Phase==LifecyclePhase.Initialized){if(sequence!=1||last!=LifecyclePhase.Bound)throw new InvalidDataException();}
        else if(f.Phase==LifecyclePhase.LocalReleased){if(sequence==1?last!=LifecyclePhase.Initialized:last!=LifecyclePhase.Quiescent)throw new InvalidDataException();}
        else if(f.Phase==LifecyclePhase.Quiescent){if(last!=LifecyclePhase.LocalReleased)throw new InvalidDataException();ReleasedRequests++;}
        else throw new InvalidDataException();last=f.Phase;
    }
    public bool ExpectedStages=>scenario switch{LifecycleCase.Normal=>ReleasedRequests==2&&FinalReturned&&ParentSettled,LifecycleCase.BeforeBindParentDeath=>last==LifecyclePhase.Started&&!Bound,LifecycleCase.BoundParentDeath=>Parked&&!FinalReturned,LifecycleCase.Watchdog=>Parked&&WatchdogExpired&&WatchdogKillIssued&&WatchdogFailure==null&&ParentSettled&&!FinalReturned,_=>false};
}
public static class LifecycleBudgets
{
    public const int Soft=250,Hard=600,Idle=120,Settlement=2000;
    public static bool Allows(long elapsed,int ceiling)=>elapsed>=0&&elapsed<ceiling;
    public static bool Pass(LifecycleCase scenario,bool expectedStages,bool parentExited,bool workerExited,bool naturalParentZero,bool naturalWorkerZero,bool eof,bool rescue,bool ioSettled,bool parentStopIssued,bool watchdogExpired,bool workerStopIssued)=>expectedStages&&(scenario!=LifecycleCase.Watchdog||WatchdogGate.Passed(watchdogExpired,workerStopIssued,workerExited,ioSettled,naturalWorkerZero))&&parentExited&&workerExited&&ioSettled&&!rescue&&(scenario is LifecycleCase.BoundParentDeath or LifecycleCase.BeforeBindParentDeath?parentStopIssued&&!naturalParentZero:true)&&(scenario is LifecycleCase.Normal or LifecycleCase.Watchdog?naturalParentZero&&eof: true)&&(scenario is LifecycleCase.Normal or LifecycleCase.BeforeBindParentDeath?naturalWorkerZero:true);
}
