using System.Diagnostics;
using System.Security.Cryptography;
namespace KuaiYi.ReuseLifecycle;
public sealed class PendingWork
{
    private readonly object sync=new();private readonly List<Task> observed=[];
    public void Keep(Task original){var observer=Observe(original);lock(sync)observed.Add(observer);}
    private static async Task Observe(Task original){try{await original.ConfigureAwait(false);}catch{} }
    public bool Settled{get{lock(sync)return observed.All(t=>t.IsCompleted);}}
    public async Task WaitAsync(){Task[] snapshot;lock(sync)snapshot=observed.ToArray();await Task.WhenAll(snapshot).ConfigureAwait(false);}
}
internal sealed class TrackedProcess
{
    private readonly Task<Process> starting;private readonly bool redirected;private Task? stderr;private int stderrBytes;private bool released;private readonly SingleTermination termination=new();
    internal readonly PendingWork Work=new();
    public Process? Process{get;private set;}
    public bool StderrBounded=>stderrBytes<=4096;
    private TrackedProcess(Task<Process> start,bool redirected){starting=start;this.redirected=redirected;Work.Keep(start);}
    public static TrackedProcess Start(ProcessStartInfo info)=>new(Task.Run(()=>System.Diagnostics.Process.Start(info)??throw new IOException()),true);
    public static TrackedProcess AttachVerified(WorkerIdentity id){
        Process? p=null;
        try{p=System.Diagnostics.Process.GetProcessById(id.Pid);_=p.SafeHandle;if(p.HasExited||p.StartTime.ToUniversalTime().Ticks!=id.Created)throw new InvalidDataException();return new(Task.FromResult(p),false){Process=p};}
        catch{p?.Dispose();throw;}
    }
    public async Task<Process> ReadyAsync(CancellationToken token){Process=await starting.WaitAsync(token).ConfigureAwait(false);_=Process.SafeHandle;StartDrain();termination.Apply(Process.HasExited,()=>Process.Kill());return Process;}
    private void StartDrain(){if(!redirected||stderr!=null||Process==null)return;stderr=Task.Run(async()=>{byte[] b=new byte[256];try{while(true){int n=await Process.StandardError.BaseStream.ReadAsync(b).ConfigureAwait(false);if(n==0)break;stderrBytes=Math.Min(4097,stderrBytes+n);CryptographicOperations.ZeroMemory(b);}}finally{CryptographicOperations.ZeroMemory(b);}});Work.Keep(stderr);}
    public bool Exited=>Process!=null&&Process.HasExited;
    public bool NaturalZero=>Exited&&Process!.ExitCode==0;
    public Task ExitWait(CancellationToken token){if(Process==null)throw new InvalidOperationException();var original=Process.WaitForExitAsync(token);Work.Keep(original);return original;}
    public bool KillIssued=>termination.Issued;
    public void KillRequiredExact(){if(Process==null)throw new InvalidOperationException();termination.RequireLive(Process.HasExited,()=>Process.Kill());} // Never Kill(true), never an unverified PID.
    private bool NoChild=>starting.IsCompleted&&!starting.IsCompletedSuccessfully&&Process==null&&Work.Settled;
    public bool Settled=>NoChild||starting.IsCompleted&&Work.Settled&&(stderr==null||stderr.IsCompletedSuccessfully)&&Exited;
    public async Task<bool> CleanupAsync(bool kill){
        if(kill)termination.Request();
        if(NoChild)return true;
        using var timeout=new CancellationTokenSource(LifecycleBudgets.Settlement);
        try{await ReadyAsync(timeout.Token).ConfigureAwait(false);termination.Apply(Exited,()=>Process!.Kill());await ExitWait(timeout.Token).WaitAsync(timeout.Token).ConfigureAwait(false);await Work.WaitAsync().WaitAsync(timeout.Token).ConfigureAwait(false);return Settled;}
        catch{return NoChild;}
    }
    public async Task AwaitRetainedSettlementAsync(){try{await ReadyAsync(CancellationToken.None).ConfigureAwait(false);}catch{await Work.WaitAsync().ConfigureAwait(false);return;}await ExitWait(CancellationToken.None).ConfigureAwait(false);await Work.WaitAsync().ConfigureAwait(false);}
    public void Release(){if(released)return;if(!Settled)throw new InvalidOperationException();Process?.Dispose();released=true;}
    public static ProcessStartInfo Info(string executable,IEnumerable<string> args){var info=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};foreach(var arg in args)info.ArgumentList.Add(arg);var retained=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(string key in new[]{"SystemRoot","WINDIR","TEMP","TMP","PATH","DOTNET_ROOT","DOTNET_ROOT_X64"})if(info.Environment.TryGetValue(key,out var value)&&value!=null)retained[key]=value;info.Environment.Clear();foreach(var pair in retained)info.Environment[pair.Key]=pair.Value;return info;}
}
internal static class LifecycleIo
{
    public static async Task<LifecycleFrame?> ReadAsync(Stream stream,PendingWork work,CancellationToken token){byte[] b=new byte[LifecycleCodec.Size];var original=ReadCore(stream,b,token);work.Keep(original);return await original.WaitAsync(token).ConfigureAwait(false);}
    private static async Task<LifecycleFrame?> ReadCore(Stream stream,byte[] b,CancellationToken token){try{int offset=0;while(offset<b.Length){int n=await stream.ReadAsync(b.AsMemory(offset),token).ConfigureAwait(false);if(n==0){if(offset==0)return null;throw new InvalidDataException();}offset+=n;}return LifecycleCodec.Decode(b);}finally{CryptographicOperations.ZeroMemory(b);}}
    public static async Task WriteAsync(Stream stream,LifecycleFrame frame,PendingWork work,CancellationToken token){var original=WriteCore(stream,frame,token);work.Keep(original);await original.WaitAsync(token).ConfigureAwait(false);}
    private static async Task WriteCore(Stream stream,LifecycleFrame frame,CancellationToken token){byte[] b=LifecycleCodec.Encode(frame);try{await stream.WriteAsync(b,token).ConfigureAwait(false);await stream.FlushAsync(token).ConfigureAwait(false);}finally{CryptographicOperations.ZeroMemory(b);}}
    public static CancellationTokenSource Budget(long origin,int limit){var remaining=TimeSpan.FromMilliseconds(limit)-Stopwatch.GetElapsedTime(origin);var token=new CancellationTokenSource();if(remaining<=TimeSpan.Zero)token.Cancel();else token.CancelAfter((int)Math.Ceiling(remaining.TotalMilliseconds));return token;}
    public static async Task ExitWithinAsync(Task original,long origin,CancellationToken token){await original.WaitAsync(token).ConfigureAwait(false);Check(origin,LifecycleBudgets.Hard);}
    public static void Check(long origin,int limit){if(!LifecycleBudgets.Allows((long)Stopwatch.GetElapsedTime(origin).TotalMilliseconds,limit))throw new TimeoutException();}
}
public static class DeathPlan{public static void KillOnlyParent(Action killHeldParent)=>killHeldParent();}

public sealed class SingleTermination
{
    public bool Requested{get;private set;}public bool Attempted{get;private set;}public bool Issued{get;private set;}
    public void Request()=>Requested=true;
    public void Apply(bool exited,Action killHeld){if(!Requested||Attempted)return;Attempted=true;if(exited)return;killHeld();Issued=true;}
    public void RequireLive(bool exited,Action killHeld){if(exited||Attempted)throw new InvalidDataException();Request();Apply(false,killHeld);if(!Issued)throw new InvalidDataException();}
}
