using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.NativeHost;
/// <summary>Only a held owned child may be terminated; restart waits for startup and exit settlement.</summary>
public sealed class SelectionBrokerClient(string executable,IOwnedProcessStarter? starter=null,TimeProvider? clock=null,bool boundaryDiagnostic=false,bool timingDiagnostic=false):IAsyncDisposable
{
    private SelectionBoundaryMap? boundaryMap;
    internal Func<Guid,SelectionBoundaryMap?>? BoundaryFactoryForTest{get;set;}
    internal Action? BeforeBoundaryDeliveryForTest{get;set;}
    public SelectionReachedBoundary? LastReachedBoundary{get;private set;}
    public string BoundaryMarkerSetup{get;private set;}="Disabled";
    private readonly object sync=new();private OwnedProcessSession? owner;private Task? cleanup;private bool disposed;
    private readonly Dictionary<string,Queue<DateTimeOffset>> failures=new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string,DateTimeOffset> paused=new(StringComparer.OrdinalIgnoreCase);
    private TimeProvider Clock=>clock??TimeProvider.System;
    private static readonly TimeSpan HardBudget=TimeSpan.FromMilliseconds(600);
    private sealed class AbsoluteDeadlineException:TimeoutException;
    private sealed class TimingDeliveryCancelledException:OperationCanceledException;
    private void CheckDelivery(CancellationToken cancellation,long? started)
    {
        cancellation.ThrowIfCancellationRequested();
        if(started is {} timestamp&&Clock.GetElapsedTime(timestamp)>=HardBudget)throw new AbsoluteDeadlineException();
    }
    public HostExitState? ExitState{get{lock(sync)return owner?.ExitState;}}
    public SelectionBrokerObservation? LastObservation{get;private set;}
    public int? LastProcessExitCode{get;private set;}
    public ImmutableArray<SelectionPhaseTiming>? LastPhaseTimings{get;private set;}
    internal Action? BeforeTimingDeliveryForTest{get;set;}
    private void PublishTimings(SelectionCaptureMetadata meta,CancellationToken cancellation,long started)
    {
        if(!timingDiagnostic)return;
        var projected=SelectionPhaseTimingProjection.Project(meta.PhaseTimings);
        BeforeTimingDeliveryForTest?.Invoke();
        lock(sync){CheckDelivery(cancellation,started);if(disposed)throw new TimingDeliveryCancelledException();LastPhaseTimings=projected;}
    }
    internal static SelectionBrokerObservation ObserveMetadata(SelectionCaptureMetadata metadata)
    {
        string status=metadata.Status switch{"Success" or "Stale" or "Sensitive" or "SensitiveOrElevated" or "UnsupportedOrProtected" or "SoftTimeout" or "TooLarge" or "TooLargeOrInvalidUnicode" or "EmptyOrUnsupported" or "BrokerFailure"=>metadata.Status,_=>"Unknown"};
        var diagnostic=metadata.Diagnostic;
        return new(status,diagnostic==null?"Unknown":Enum.GetName(diagnostic.Stage)??"Unknown",diagnostic==null?"Unknown":Enum.GetName(diagnostic.Exception)??"Unknown",diagnostic is {Exception:not SelectionBrokerException.None}?diagnostic.HResult:null);
    }
    public async Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation)
    {
        long started=Clock.GetTimestamp();OwnedProcessSession session;DateTimeOffset now=Clock.GetUtcNow();
        lock(sync)
        {
            LastObservation=null;LastProcessExitCode=null;LastReachedBoundary=null;LastPhaseTimings=null;BoundaryMarkerSetup=boundaryDiagnostic?"NotStarted":"Disabled";
            if(disposed)return new(SelectionCaptureStatus.Cancelled);
            if(paused.TryGetValue(gesture.Window.Executable,out var until)&&now<until)return new(SelectionCaptureStatus.Paused);
            if(owner!=null){if(cleanup?.IsCompleted==true)cleanup=CleanupAsync(owner);return new(SelectionCaptureStatus.CleanupPending);}
            session=owner=new(starter);
        }
        Guid id=Guid.NewGuid();using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        var info=OwnedProcessSession.Info(executable,null);info.ArgumentList.Clear();
        foreach(string value in new[]{gesture.Window.Handle.ToString(CultureInfo.InvariantCulture),gesture.Window.ProcessId.ToString(CultureInfo.InvariantCulture),gesture.Up.X.ToString(CultureInfo.InvariantCulture),gesture.Up.Y.ToString(CultureInfo.InvariantCulture),id.ToString(),gesture.Down.X.ToString(CultureInfo.InvariantCulture),gesture.Down.Y.ToString(CultureInfo.InvariantCulture),gesture.Explicit?"explicit":"gesture",gesture.Window.ProcessStartUtcTicks.ToString(CultureInfo.InvariantCulture)})info.ArgumentList.Add(value);
        SelectionCaptureStatus status=SelectionCaptureStatus.ProtocolFailure;
        byte[]? payload=null;
        try
        {
            CheckDelivery(deadline.Token,started);
            if(boundaryDiagnostic)
            {
                lock(sync)
                {
                    if(disposed){status=SelectionCaptureStatus.Cancelled;return new(status);}
                    boundaryMap=(BoundaryFactoryForTest??SelectionBoundaryMap.TryCreate)(id);
                    BoundaryMarkerSetup=boundaryMap==null?"Unavailable":"Ready";
                    if(boundaryMap!=null)info.ArgumentList.Add("--boundary-marker");
                }
            }
            CheckDelivery(deadline.Token,started);
            if(timingDiagnostic){if(!boundaryDiagnostic)throw new ArgumentException("Timing requires diagnostic mode.");info.ArgumentList.Add("--phase-timing");}
            // Timer wakes blocked operations; the single monotonic origin decides every delivery.
            var remaining=HardBudget-Clock.GetElapsedTime(started);if(remaining<=TimeSpan.Zero)throw new AbsoluteDeadlineException();deadline.CancelAfter(remaining);
            var process=await session.Start(info,deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);
            CheckDelivery(deadline.Token,started);
            var first=await ReadAsync(process.StandardOutput.BaseStream,deadline.Token,started).ConfigureAwait(false);
            var second=await ReadAsync(process.StandardOutput.BaseStream,deadline.Token,started).ConfigureAwait(false);payload=second?.Payload;
            var extra=await ReadAsync(process.StandardOutput.BaseStream,deadline.Token,started).ConfigureAwait(false);if(extra!=null){CryptographicOperations.ZeroMemory(extra.Payload);throw new InvalidDataException("Unexpected selection frame.");}
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);
            CheckDelivery(deadline.Token,started);
            int exitCode=process.ExitCode;
            if(exitCode!=0)
            {
                SelectionReachedBoundary? reached;
                lock(sync)reached=disposed?null:boundaryMap?.Read(id);
                BeforeBoundaryDeliveryForTest?.Invoke();
                lock(sync)
                {
                    CheckDelivery(deadline.Token,started);
                    if(disposed){status=SelectionCaptureStatus.Cancelled;return new(status);}
                    LastProcessExitCode=exitCode;LastReachedBoundary=reached;status=SelectionCaptureStatus.Crash;return new(status);
                }
            }
            if(first?.RequestId!=id||second?.RequestId!=id||first.Type!=IpcMessageType.Ping||second.Type!=IpcMessageType.BinaryChunk)throw new InvalidDataException("Selection frame identity/type mismatch.");
            var meta=JsonSerializer.Deserialize<SelectionCaptureMetadata>(first.Payload)??throw new InvalidDataException("Missing metadata.");
            if(!double.IsFinite(meta.ElapsedMs)||meta.ElapsedMs<0||meta.CandidateCount is <0 or >64||meta.RangeCount is <0 or >64)throw new InvalidDataException("Invalid diagnostic bounds.");
            LastObservation=ObserveMetadata(meta);
            status=ParseStatus(meta.Status);
            if(status!=SelectionCaptureStatus.Success)
            {
                SelectionCopyEvidence? evidence=null;
                if(status==SelectionCaptureStatus.EmptyOrUnsupported&&meta.Copy is {} copy)
                {
                    if(meta.Status!="EmptyOrUnsupported"||meta.ElapsedMs>250||meta.ProcessStartUtcTicks!=gesture.Window.ProcessStartUtcTicks||copy.FocusHandle==0||copy.RuntimeId is not {Length:>0 and <=64}||copy.ControlRectangle is not {Length:4}||copy.SelectionRectangles==null)throw new InvalidDataException("Untrusted copy evidence.");
                    var bounds=Rectangles(copy.ControlRectangle).Single();var selection=Rectangles(copy.SelectionRectangles);evidence=new(copy.FocusHandle,copy.RuntimeId.ToImmutableArray(),bounds,selection,copy.AutomaticTrusted);if(!SelectionTextPolicy.CopyTrusted(gesture,evidence)&&copy.AutomaticTrusted)throw new InvalidDataException("Copy gesture association missing.");
                }
                CheckDelivery(deadline.Token,started);PublishTimings(meta,deadline.Token,started);return new(status,CopyEvidence:evidence);
            }
            if(meta.ElapsedMs>250){PublishTimings(meta,deadline.Token,started);return new(status=SelectionCaptureStatus.SoftTimeout);}
            if(meta.ProcessStartUtcTicks!=gesture.Window.ProcessStartUtcTicks||meta.RuntimeId is not {Length:>0 and <=64}||meta.RangeCount<1)throw new InvalidDataException("Selection source mismatch.");
            string text=new UTF8Encoding(false,true).GetString(second.Payload);
            if(string.IsNullOrWhiteSpace(text)||text.Length>=100002||text.EnumerateRunes().Count()>50000)throw new InvalidDataException("Selection content limit.");
            var ranges=Rectangles(meta.RangeRectangles);PixelRect? control=meta.ControlRectangle is {Length:4} coordinates?Rectangles(coordinates).Single():null;
            var result=new SelectionCaptureResult(status,text,ranges,meta.RuntimeId.ToImmutableArray(),control);
            BeforeSelectionDeliveryForTest?.Invoke(payload!);
            CheckDelivery(deadline.Token,started);
            PublishTimings(meta,deadline.Token,started);
            return result;
        }
        catch(TimingDeliveryCancelledException){status=SelectionCaptureStatus.Cancelled;return new(status);}
        catch(AbsoluteDeadlineException){status=cancellation.IsCancellationRequested?SelectionCaptureStatus.Cancelled:SelectionCaptureStatus.HardTimeout;return new(status);}
        catch(OperationCanceledException)when(deadline.IsCancellationRequested){status=cancellation.IsCancellationRequested?SelectionCaptureStatus.Cancelled:SelectionCaptureStatus.HardTimeout;return new(status);}
        catch(Exception error)when(error is IOException or InvalidDataException or JsonException or DecoderFallbackException or InvalidOperationException or System.ComponentModel.Win32Exception or OverflowException or ArgumentException){status=SelectionCaptureStatus.ProtocolFailure;return new(status);}
        finally
        {
            if(status is SelectionCaptureStatus.HardTimeout or SelectionCaptureStatus.Cancelled or SelectionCaptureStatus.ProtocolFailure||status==SelectionCaptureStatus.Crash&&LastObservation?.Status!="BrokerFailure")LastObservation=null;
            if(status is SelectionCaptureStatus.HardTimeout or SelectionCaptureStatus.Cancelled or SelectionCaptureStatus.ProtocolFailure or SelectionCaptureStatus.Crash)LastPhaseTimings=null;
            if(payload!=null)CryptographicOperations.ZeroMemory(payload);
            lock(sync)
            {
                if(status is SelectionCaptureStatus.HardTimeout or SelectionCaptureStatus.Crash)
                {if(!failures.TryGetValue(gesture.Window.Executable,out var queue))failures[gesture.Window.Executable]=queue=new();while(queue.Count>0&&now-queue.Peek()>TimeSpan.FromSeconds(60))queue.Dequeue();queue.Enqueue(now);if(queue.Count>=3){paused[gesture.Window.Executable]=now.AddSeconds(30);queue.Clear();}}
                else if(status==SelectionCaptureStatus.Success)failures.Remove(gesture.Window.Executable);
                cleanup=CleanupAsync(session);
            }
        }
    }
    private async Task CleanupAsync(OwnedProcessSession session)
    {
        bool finalized=await session.TryFinalizeAsync().ConfigureAwait(false);
        if(!finalized){try{await session.StartupSettlement.ConfigureAwait(false);}catch(Exception){/* The session retains the typed startup failure; no child may be abandoned. */}finalized=await session.TryFinalizeAsync(retry:true).ConfigureAwait(false);}
        lock(sync)if(finalized&&owner==session){boundaryMap?.Dispose();boundaryMap=null;owner=null;}
    }
    public async ValueTask DisposeAsync()
    {Task? task;OwnedProcessSession? session;lock(sync){disposed=true;task=cleanup;session=owner;}if(task!=null){try{await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);}catch(TimeoutException){throw new IOException("Selection Broker startup/cleanup still pending; held ownership retained.");}}if(session!=null){bool finished=await session.TryFinalizeAsync(retry:true).ConfigureAwait(false);if(!finished)throw new IOException("Selection Broker exit/startup settlement unconfirmed.");lock(sync)if(owner==session){boundaryMap?.Dispose();boundaryMap=null;owner=null;}}}
    private static ImmutableArray<PixelRect> Rectangles(double[]? values)
    {
        if(values==null)return [];if(values.Length>1024||values.Length%4!=0||values.Any(v=>!double.IsFinite(v)))throw new InvalidDataException("Invalid range rectangles.");
        var result=ImmutableArray.CreateBuilder<PixelRect>();for(int i=0;i<values.Length;i+=4){if(values[i+2]<0||values[i+3]<0)throw new InvalidDataException("Negative range size.");result.Add(new(checked((int)Math.Floor(values[i])),checked((int)Math.Floor(values[i+1])),checked((int)Math.Ceiling(values[i]+values[i+2])),checked((int)Math.Ceiling(values[i+1]+values[i+3]))));}return result.ToImmutable();
    }
    public string? LateReadFailureCode{get;private set;}
    internal Action<byte[]>? BeforeSelectionDeliveryForTest{get;set;}
    internal Func<Task<IpcFrame?>,Task>? BeforeReadSettlementForTest{get;set;}
    internal Task? LateReadObserverForTest{get;private set;}
    internal Action<Task<IpcFrame?>>? LateReadObserverStartedForTest{get;set;}
    internal async Task<IpcFrame?> ReadAsync(Stream stream,CancellationToken cancellation,long? started=null)
    {
        var pending=IpcFrameCodec.ReadAsync(stream,cancellation).AsTask();bool delivered=false;
        try{var result=await pending.WaitAsync(cancellation).ConfigureAwait(false);CheckDelivery(cancellation,started);delivered=true;return result;}
        finally{if(BeforeReadSettlementForTest!=null)await BeforeReadSettlementForTest(pending).ConfigureAwait(false);if(!delivered)LateReadObserverForTest=ObserveLateRead(pending);}
    }
    private async Task ObserveLateRead(Task<IpcFrame?> pending)
    {LateReadObserverStartedForTest?.Invoke(pending);try{var result=await pending.ConfigureAwait(false);if(result!=null)CryptographicOperations.ZeroMemory(result.Payload);}catch(Exception error){LateReadFailureCode=error.GetType().Name;}}
    private static SelectionCaptureStatus ParseStatus(string status)=>status switch{"Success"=>SelectionCaptureStatus.Success,"Stale"=>SelectionCaptureStatus.Stale,"Sensitive" or "SensitiveOrElevated" or "UnsupportedOrProtected"=>SelectionCaptureStatus.Sensitive,"BrokerFailure"=>SelectionCaptureStatus.Crash,"SoftTimeout"=>SelectionCaptureStatus.SoftTimeout,"TooLarge" or "TooLargeOrInvalidUnicode"=>SelectionCaptureStatus.TooLarge,_=>SelectionCaptureStatus.EmptyOrUnsupported};
}
