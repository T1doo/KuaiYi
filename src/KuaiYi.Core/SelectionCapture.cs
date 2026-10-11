using System.Collections.Immutable;
using System.Text;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core;
public sealed record SelectionWindow(long Handle,uint ProcessId,long ProcessStartUtcTicks,string Executable);
public sealed record SelectionInputIdentity(CaptureTicket Ticket,string SettingsVersion,Guid HookId);
public sealed record SelectionRawInput(bool Down,PixelPoint Point,long TimeMs,bool Injected,long Foreground,uint ProcessId,long CapturedUtcTicks,long LossRevision,SelectionInputIdentity Identity);
public sealed record SelectionPointerEvent(bool Down,PixelPoint Point,long TimeMs,bool Injected,SelectionWindow Window,SelectionInputIdentity? InputIdentity=null);
public sealed record SelectionGesture(SelectionWindow Window,PixelPoint Down,PixelPoint Up,long TimeMs,int ClickCount,bool Explicit=false);
public enum SelectionCaptureStatus{Success,EmptyOrUnsupported,Sensitive,Stale,SoftTimeout,HardTimeout,Crash,Cancelled,ProtocolFailure,CleanupPending,Paused,TooLarge,UntrustedGesture}
public sealed record SelectionCaptureResult(SelectionCaptureStatus Status,string Text="",ImmutableArray<PixelRect> Ranges=default,ImmutableArray<int> RuntimeId=default,PixelRect? ControlBounds=null);
public sealed record SelectionSourceSnapshot(SelectionWindow Window,string RawText,string Text,ImmutableArray<PixelRect> Ranges,ImmutableArray<int> RuntimeId,string SettingsVersion,int Scalars,bool RequiresConfirmation);
public interface ISelectionCapturePlatform
{
    bool IsCurrent(SelectionWindow window);
    Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation);
}
public sealed class SelectionGestureTracker(int dragX,int dragY,int multipleClickMs,int clickX,int clickY)
{
    private SelectionPointerEvent? down,lastClick;private int clicks;
    public void Reset(){down=lastClick=null;clicks=0;}
    public SelectionGesture? Accept(SelectionPointerEvent input)
    {
        if(input.Injected){Reset();return null;}
        if(input.Down){down=input;return null;}
        var start=down;down=null;if(start==null||start.Window!=input.Window||start.InputIdentity!=input.InputIdentity||input.TimeMs<start.TimeMs)return null;
        bool drag=Math.Abs((long)input.Point.X-start.Point.X)>=dragX||Math.Abs((long)input.Point.Y-start.Point.Y)>=dragY;
        if(drag){lastClick=null;clicks=0;return new(input.Window,start.Point,input.Point,input.TimeMs,1);}
        bool consecutive=lastClick is {} old&&old.Window==input.Window&&input.TimeMs>=old.TimeMs&&input.TimeMs-old.TimeMs<=multipleClickMs&&Math.Abs((long)input.Point.X-old.Point.X)<=clickX&&Math.Abs((long)input.Point.Y-old.Point.Y)<=clickY;
        clicks=consecutive?Math.Min(3,clicks+1):1;lastClick=input;
        return clicks>=2?new(input.Window,start.Point,input.Point,input.TimeMs,clicks):null;
    }
}
public static class SelectionTextPolicy
{
    public static bool Excluded(SelectionWindow window,AppSettings settings,uint ownProcess)
    {
        string file=Path.GetFileName(window.Executable.Replace('\\','/')),name=Path.GetFileNameWithoutExtension(file);
        return window.Handle==0||window.ProcessId==0||window.ProcessId==ownProcess||window.ProcessStartUtcTicks<=0||name.StartsWith("KuaiYi",StringComparison.OrdinalIgnoreCase)||name.ToLowerInvariant() is "cmd" or "powershell" or "pwsh" or "windowsterminal" or "conhost"||settings.Privacy.BlockedApplications.Any(v=>string.Equals(v,window.Executable,StringComparison.OrdinalIgnoreCase)||string.Equals(v,file,StringComparison.OrdinalIgnoreCase)||string.Equals(v,name,StringComparison.OrdinalIgnoreCase));
    }
    public static SelectionSourceSnapshot? Snapshot(SelectionGesture gesture,SelectionCaptureResult result,SettingsSnapshot settings)
    {
        if(result.Status!=SelectionCaptureStatus.Success||string.IsNullOrWhiteSpace(result.Text)||result.Text.Length>=100002||result.RuntimeId.IsDefaultOrEmpty||result.RuntimeId.Length>64||!result.Ranges.IsDefault&&result.Ranges.Length>256)return null;
        for(int i=0;i<result.Text.Length;i++){char c=result.Text[i];if(char.IsHighSurrogate(c)){if(++i>=result.Text.Length||!char.IsLowSurrogate(result.Text[i]))return null;}else if(char.IsLowSurrogate(c))return null;}
        if(!gesture.Explicit&&(result.Ranges.IsDefaultOrEmpty||result.ControlBounds is not {} control||!Contains(control,gesture.Down)||!Contains(control,gesture.Up)||!result.Ranges.Any(r=>Contains(r,gesture.Down,2)||Contains(r,gesture.Up,2))))return null;
        string normalized=result.Text.Replace("\r\n","\n",StringComparison.Ordinal).Replace('\r','\n');var clean=new StringBuilder(normalized.Length);
        foreach(char c in normalized)if((!char.IsControl(c)||c is '\n' or '\t')&&c is not '\u200b' and not '\ufeff')clean.Append(c);
        string text=clean.ToString().Trim();int count=text.EnumerateRunes().Count();if(count==0||count>settings.Data.Translation.HardLimitScalars)return null;
        return new(gesture.Window,result.Text,text,result.Ranges.IsDefault?[]:result.Ranges,result.RuntimeId,settings.Version,count,count>settings.Data.Translation.AutoSoftLimitScalars);
    }
    private static bool Contains(PixelRect r,PixelPoint p,int margin=0)=>(long)p.X>=r.Left-(long)margin&&(long)p.X<=r.Right+(long)margin&&(long)p.Y>=r.Top-(long)margin&&(long)p.Y<=r.Bottom+(long)margin;
}
/// <summary>Production debounce, provenance, exclusion and destination commit. Platform tests substitute only OS/UIA.</summary>
public sealed class SelectionCaptureCoordinator: IAsyncDisposable
{
    private readonly RuntimeGate gate;private readonly Func<SettingsSnapshot?> settings;private readonly ISelectionCapturePlatform platform;private readonly SelectionGestureTracker tracker;private readonly uint ownProcess;
    private readonly Func<TimeSpan,CancellationToken,Task> delay;private readonly SynchronizationContext destination;private readonly Action<SelectionSourceSnapshot,bool> accepted;private readonly Action<SelectionCaptureStatus> failed;
    private readonly object sync=new();private readonly HashSet<Task> operations=[];private CancellationTokenSource? pending;private long generation;private bool disposed;private SelectionSourceSnapshot? previous;
    public SelectionCaptureCoordinator(RuntimeGate gate,Func<SettingsSnapshot?> settings,ISelectionCapturePlatform platform,SelectionGestureTracker tracker,uint ownProcess,SynchronizationContext destination,Action<SelectionSourceSnapshot,bool> accepted,Action<SelectionCaptureStatus> failed,Func<TimeSpan,CancellationToken,Task>? delay=null)
    {this.gate=gate;this.settings=settings;this.platform=platform;this.tracker=tracker;this.ownProcess=ownProcess;this.destination=destination;this.accepted=accepted;this.failed=failed;this.delay=delay??Task.Delay;}
    public void CancelPending(bool clearSource=false){lock(sync){generation++;pending?.Cancel();tracker.Reset();if(clearSource)previous=null;}}
    public SelectionInputIdentity? FreezeInputIdentity(Guid hookId)
    {lock(sync){if(disposed||hookId==Guid.Empty||settings() is not {} config||!gate.TryBeginPassiveCapture(TriggerMode.Selection,out var ticket))return null;return new(ticket,config.Version,hookId);}}
    public Task InputAsync(SelectionPointerEvent input)
    {
        lock(sync)
        {
            if(disposed)return Task.CompletedTask;
            // Raw input never acquires a new epoch at dispatch. Dropping an old event must not cancel a newer gesture.
            if(input.InputIdentity is not {} frozen||frozen.HookId==Guid.Empty||frozen.SettingsVersion!=settings()?.Version||frozen.Ticket.Mode!=TriggerMode.Selection||!gate.IsCaptureCurrent(frozen.Ticket))return Task.CompletedTask;
            if(input.Down){generation++;pending?.Cancel();}
            if(!gate.TryBeginPassiveCapture(TriggerMode.Selection,out _)||settings() is not {} config||SelectionTextPolicy.Excluded(input.Window,config.Data,ownProcess)){tracker.Reset();return Task.CompletedTask;}
            var gesture=tracker.Accept(input);return gesture==null?Task.CompletedTask:Start(gesture,true,frozen.Ticket);
        }
    }
    public Task ExplicitAsync(SelectionWindow window,PixelPoint point)
    {lock(sync){if(disposed)return Task.CompletedTask;tracker.Reset();return Start(new(window,point,point,Environment.TickCount64,0,true),false);}}
    private Task Start(SelectionGesture gesture,bool debounce,CaptureTicket? frozen=null)
    {
        if(settings() is not {} config||SelectionTextPolicy.Excluded(gesture.Window,config.Data,ownProcess)||!gate.TryBeginPassiveCapture(gesture.Explicit?gate.Snapshot.Mode:TriggerMode.Selection,out var ticket))return Task.CompletedTask;
        if(frozen is {} original&&ticket!=original)return Task.CompletedTask;
        pending?.Cancel();var cancellation=pending=new();long serial=++generation;var task=RunAsync(gesture,config,ticket,serial,cancellation,debounce);operations.Add(task);_ =ObserveAsync(task,cancellation);return task;
    }
    public string? FailureCode{get;private set;}
    private async Task ObserveAsync(Task task,CancellationTokenSource cancellation)
    {try{await task.ConfigureAwait(false);}catch(Exception error){FailureCode=error.GetType().Name;}finally{lock(sync){operations.Remove(task);if(pending==cancellation)pending=null;cancellation.Dispose();}}}
    private bool Current(SelectionGesture gesture,SettingsSnapshot config,CaptureTicket ticket,long serial)
    {lock(sync)return !disposed&&generation==serial&&settings()?.Version==config.Version&&gate.IsCaptureCurrent(ticket)&&!SelectionTextPolicy.Excluded(gesture.Window,settings()!.Data,ownProcess)&&platform.IsCurrent(gesture.Window);}
    private async Task RunAsync(SelectionGesture gesture,SettingsSnapshot config,CaptureTicket ticket,long serial,CancellationTokenSource cancellation,bool debounce)
    {
        try
        {
            if(debounce)await delay(TimeSpan.FromMilliseconds(config.Data.Translation.SelectionDebounceMs),cancellation.Token).ConfigureAwait(false);
            if(!Current(gesture,config,ticket,serial))return;
            var result=await platform.CaptureAsync(gesture,cancellation.Token).ConfigureAwait(false);
            if(cancellation.IsCancellationRequested||!Current(gesture,config,ticket,serial))return;
            var snapshot=SelectionTextPolicy.Snapshot(gesture,result,config);
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            destination.Post(_=>
            {
                try{lock(sync){if(!Current(gesture,config,ticket,serial))return;gate.TryCommitCapture(ticket,()=>Current(gesture,config,ticket,serial),()=>{if(snapshot==null){failed(result.Status==SelectionCaptureStatus.Success?SelectionCaptureStatus.UntrustedGesture:result.Status);return;}bool same=previous is {} old&&old.Window==snapshot.Window&&old.Text==snapshot.Text&&old.SettingsVersion==snapshot.SettingsVersion&&old.RuntimeId.SequenceEqual(snapshot.RuntimeId)&&old.Ranges.SequenceEqual(snapshot.Ranges);previous=snapshot;accepted(snapshot,same);});}}
                catch(Exception error){done.TrySetException(error);}
                finally{done.TrySetResult();}
            },null);await done.Task.WaitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
    }
    public async ValueTask DisposeAsync()
    {Task[] tasks;lock(sync){disposed=true;generation++;pending?.Cancel();tracker.Reset();previous=null;tasks=operations.ToArray();}await Task.WhenAll(tasks).ConfigureAwait(false);}
}
