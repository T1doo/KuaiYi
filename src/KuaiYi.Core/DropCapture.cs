using System.Collections.Immutable;
using System.Text;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core;
public enum DropCaptureStatus{Unsupported,Untrusted,Stale,TooLarge,InvalidText,Timeout,TransactionLimit}
public sealed record DropTargetGeometry(PixelRect Anchor,PixelRect Bounds,int Radius)
{
    public bool Valid=>Anchor.Width>0&&Anchor.Height>0&&Bounds.Contains(Anchor)&&Bounds.Width>0&&Bounds.Height>0&&Radius>0&&Radius<=Math.Min(Bounds.Width,Bounds.Height)/2;
    public bool Contains(PixelPoint point)
    {
        if(!Valid||point.X<Bounds.Left||point.X>=Bounds.Right||point.Y<Bounds.Top||point.Y>=Bounds.Bottom)return false;
        double x=point.X+.5,y=point.Y+.5,cx=Math.Clamp(x,Bounds.Left+(double)Radius,Bounds.Right-(double)Radius),cy=Math.Clamp(y,Bounds.Top+(double)Radius,Bounds.Bottom-(double)Radius);
        return (x-cx)*(x-cx)+(y-cy)*(y-cy)<=Radius*(double)Radius;
    }
}
public sealed record DropCaptureLease(Guid Receiver,long WireEpoch,CaptureTicket Ticket,string SettingsVersion);
/// <summary>Actual Drop source admission. No UIA/clipboard dependency; data is this transaction's owned OLE snapshot.</summary>
public sealed class DropCaptureCoordinator: IAsyncDisposable
{
    private readonly object sync=new();private readonly RuntimeGate gate;private readonly Func<SettingsSnapshot?> settings;private readonly Func<SelectionWindow,bool> sourceCurrent;private readonly Func<bool> visible;private readonly uint ownProcess;private readonly SynchronizationContext destination;private readonly Action<SelectionSourceSnapshot,bool> accepted;private readonly Action<DropCaptureStatus> failed;private readonly TimeProvider clock;
    private readonly HashSet<Guid> seen=[];private readonly HashSet<Task> operations=[];private readonly HashSet<PendingSource> queued=[];
    private DropCaptureLease? lease;private SettingsSnapshot? config;private DropTargetGeometry? geometry;private DropWork? hover,drop;private CancellationTokenSource? pending;private SelectionSourceSnapshot? previous;private bool disposed;private long generation;
    private sealed record DropWork(Guid Transaction,SelectionWindow Source,PixelPoint Point,DropTargetGeometry Target,long Started,long Generation);
    private sealed class PendingSource(SelectionSourceSnapshot? value){public bool HasValue=>Volatile.Read(ref value)!=null;public SelectionSourceSnapshot? Take()=>Interlocked.Exchange(ref value,null);public void Clear()=>Interlocked.Exchange(ref value,null);}
    public DropCaptureCoordinator(RuntimeGate gate,Func<SettingsSnapshot?> settings,Func<SelectionWindow,bool> sourceCurrent,Func<bool> visible,uint ownProcess,SynchronizationContext destination,Action<SelectionSourceSnapshot,bool> accepted,Action<DropCaptureStatus> failed,TimeProvider? clock=null)
    {this.gate=gate;this.settings=settings;this.sourceCurrent=sourceCurrent;this.visible=visible;this.ownProcess=ownProcess;this.destination=destination;this.accepted=accepted;this.failed=failed;this.clock=clock??TimeProvider.System;}
    public DropCaptureLease? Lease{get{lock(sync)return lease;}}
    internal int PendingSourceCountForTest{get{lock(sync)return queued.Count(item=>item.HasValue);}}
    public DropCaptureLease? Open(Guid receiver,long wireEpoch,DropTargetGeometry target)
    {
        lock(sync)
        {
            InvalidateLocked(false);if(disposed||receiver==Guid.Empty||wireEpoch<0||wireEpoch>long.MaxValue/2||!target.Valid||!visible()||settings() is not {} current||current.ReadOnly||current.RequiresRecovery||!gate.TryBeginPassiveCapture(TriggerMode.Drop,out var ticket))return null;
            config=current;geometry=target;return lease=new(receiver,wireEpoch,ticket,current.Version);
        }
    }
    public void UpdateGeometry(DropTargetGeometry target)
    {lock(sync){if(!target.Valid||geometry is {} old&&old.Anchor!=target.Anchor){InvalidateLocked(false);return;}geometry=target;}}
    public void Invalidate(bool clearSource=false){lock(sync)InvalidateLocked(clearSource);}
    private void InvalidateLocked(bool clearSource)
    {generation++;lease=null;config=null;hover=drop=null;seen.Clear();pending?.Cancel();foreach(var item in queued)item.Clear();if(clearSource)previous=null;}
    private bool Current(DropCaptureLease expected)=>!disposed&&lease==expected&&config?.Version==expected.SettingsVersion&&settings()?.Version==expected.SettingsVersion&&visible()&&gate.IsCaptureCurrent(expected.Ticket);
    private bool SourceCurrent(SelectionWindow source)=>config is {} current&&!SelectionTextPolicy.Excluded(source,current.Data,ownProcess)&&sourceCurrent(source);
    public bool Enter(DropCaptureLease expected,Guid transaction,SelectionWindow source,PixelPoint point,OleEffect effect)
    {
        lock(sync)
        {
            if(!Current(expected)||transaction==Guid.Empty||seen.Contains(transaction)||effect!=OleEffect.Copy||geometry is not {} target||!target.Contains(point)||!SourceCurrent(source))return false;
            if(seen.Count>=256){InvalidateLocked(false);return false;}
            pending?.Cancel();generation++;drop=null;seen.Add(transaction);hover=new(transaction,source,point,target,0,generation);return true;
        }
    }
    public void Leave(DropCaptureLease expected,Guid transaction){lock(sync)if(Current(expected)&&hover?.Transaction==transaction)hover=null;}
    public bool Begin(DropCaptureLease expected,Guid transaction,SelectionWindow source,PixelPoint point,OleEffect effect)
    {
        lock(sync)
        {
            if(!Current(expected)||hover is not {} entered||entered.Transaction!=transaction||entered.Source!=source||entered.Generation!=generation||effect!=OleEffect.Copy||geometry is not {} target||!target.Contains(point)||!SourceCurrent(source))return false;
            hover=null;drop=entered with{Point=point,Target=target,Started=clock.GetTimestamp()};return true;
        }
    }
    public Task ReceiveAsync(DropCaptureLease expected,Guid transaction,ReadOnlyMemory<byte> utf8,Func<Action,bool> receiverAdmission,CancellationToken cancellation=default,Func<bool>? receiverCurrent=null)
    {
        lock(sync)
        {
            if(cancellation.IsCancellationRequested||!Current(expected)||drop is not {} work||work.Transaction!=transaction||work.Generation!=generation||!SourceCurrent(work.Source))return Task.CompletedTask;
            drop=null;pending?.Cancel();var token=pending=CancellationTokenSource.CreateLinkedTokenSource(cancellation);var task=Deliver(expected,work,config!,utf8,receiverAdmission,token,receiverCurrent??(static()=>true));operations.Add(task);_ =Observe(task,token);return task;
        }
    }
    private async Task Observe(Task task,CancellationTokenSource token)
    {try{await task.ConfigureAwait(false);}catch(Exception error){FailureCode=error.GetType().Name;}finally{lock(sync){operations.Remove(task);if(pending==token)pending=null;token.Dispose();}}}
    public string? FailureCode{get;private set;}
    private async Task Deliver(DropCaptureLease expected,DropWork work,SettingsSnapshot settings,ReadOnlyMemory<byte> bytes,Func<Action,bool> receiverAdmission,CancellationTokenSource cancellation,Func<bool> receiverCurrent)
    {
        PendingSource? owner=null;
        bool CurrentWork()=>SourceCurrent(work.Source)&&!cancellation.IsCancellationRequested&&generation==work.Generation&&geometry?.Anchor==work.Target.Anchor&&Current(expected);
        bool Timely()=>clock.GetElapsedTime(work.Started)<TimeSpan.FromMilliseconds(1500);
        try
        {
            if(!CurrentWork())return;DropCaptureStatus? failure=null;SelectionSourceSnapshot? snapshot=null;
            try
            {
                if(!Timely())failure=DropCaptureStatus.Timeout;
                else if(bytes.Length is <1 or >200000)failure=DropCaptureStatus.TooLarge;
                else
                {
                    string text=new UTF8Encoding(false,true).GetString(bytes.Span);
                    snapshot=SelectionTextPolicy.DropSnapshot(work.Source,text,expected.Receiver,work.Transaction,settings);
                    if(snapshot==null)failure=DropCaptureStatus.InvalidText;
                }
            }
            catch(Exception error)when(error is InvalidDataException or DecoderFallbackException){failure=DropCaptureStatus.InvalidText;}
            owner=new(snapshot);snapshot=null;bytes=default;lock(sync)queued.Add(owner);using var release=cancellation.Token.Register(owner.Clear);
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var payloadOwner=owner;
            destination.Post(_=>
            {
                try
                {
                    lock(sync)
                    {
                        if(!CurrentWork())return;
                        receiverAdmission(()=>gate.TryCommitCapture(expected.Ticket,()=>CurrentWork()&&Timely()&&receiverCurrent(),()=>
                        {
                            var value=payloadOwner.Take();if(value==null){failed(failure??DropCaptureStatus.InvalidText);return;}
                            bool duplicate=previous is {} old&&old.Window==value.Window&&old.Text==value.Text&&old.SettingsVersion==value.SettingsVersion&&old.SourceKind==value.SourceKind;
                            previous=value;accepted(value,duplicate);
                        }));
                    }
                }
                catch(Exception error){done.TrySetException(error);}
                finally{payloadOwner.Clear();done.TrySetResult();}
            },null);
            await done.Task.WaitAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(cancellation.IsCancellationRequested){ }
        finally{if(owner!=null){owner.Clear();lock(sync)queued.Remove(owner);}}
    }
    public async ValueTask DisposeAsync(){Task[] tasks;lock(sync){disposed=true;InvalidateLocked(true);tasks=operations.ToArray();}await Task.WhenAll(tasks).ConfigureAwait(false);}
}
