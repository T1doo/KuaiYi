using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
namespace KuaiYi.NativeHost;
/// <summary>Owned isolated STA service. Originals remain in child memory; parent alone issues input under RuntimeGate.</summary>
public sealed class ClipboardCaptureClient(string executable,IOwnedProcessStarter? starter=null,TimeProvider? provider=null):IAsyncDisposable
{
    private readonly object sync=new();private OwnedProcessSession? owner;private Task? cleanup;private bool disposed;
    private TimeProvider Clock=>provider??TimeProvider.System;private static readonly TimeSpan Budget=TimeSpan.FromMilliseconds(1200);
    private sealed class CurrentStarter(IOwnedProcessStarter original,Func<bool> current):IOwnedProcessStarter
    {public Process Start(ProcessStartInfo info){if(!current())throw new OperationCanceledException();return original.Start(info);}}
    public string? FailureCode{get;private set;}public HostExitState? ExitState{get{lock(sync)return owner?.ExitState;}}
    internal Task? LateReadObserverForTest{get;private set;}
    internal Action<byte[]>? PayloadReleasedForTest{get;set;}
    internal Action? SnapshotReadyForTest{get;set;}
    internal Func<Task<IpcFrame?>,Task>? BeforeReadSettlementForTest{get;set;}
    internal Action? LateReadObserverStartedForTest{get;set;}
    private void Check(long started,CancellationToken token){token.ThrowIfCancellationRequested();if(Clock.GetElapsedTime(started)>=Budget)throw new TimeoutException();}
    public async Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,SelectionCopyEvidence evidence,Func<uint,ClipboardCopyStart?> issueCopy,Func<bool> current,CancellationToken cancellation)
    {
        if(cancellation.IsCancellationRequested||!current())return new(SelectionCaptureStatus.Cancelled);
        if(!SelectionTextPolicy.CopyTrusted(gesture,evidence))return new(SelectionCaptureStatus.UntrustedGesture);
        long started=Clock.GetTimestamp();OwnedProcessSession session;
        lock(sync){if(disposed)return new(SelectionCaptureStatus.Cancelled);if(owner!=null){if(cleanup?.IsCompleted==true)cleanup=Cleanup(owner);return new(SelectionCaptureStatus.CleanupPending);}session=owner=new(new CurrentStarter(starter??SystemOwnedProcessStarter.Instance,current));}
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(cancellation);deadline.CancelAfter(Budget);Guid id=Guid.NewGuid();Process? process=null;bool sent=false,attempted=false;byte[]? payload=null;
        try
        {
            Check(started,deadline.Token);if(!current())return new(SelectionCaptureStatus.Cancelled);
            var info=OwnedProcessSession.Info(executable,null);info.ArgumentList.Clear();foreach(string value in new[]{gesture.Window.Handle.ToString(CultureInfo.InvariantCulture),gesture.Window.ProcessId.ToString(CultureInfo.InvariantCulture),gesture.Window.ProcessStartUtcTicks.ToString(CultureInfo.InvariantCulture),evidence.FocusHandle.ToString(CultureInfo.InvariantCulture),id.ToString()})info.ArgumentList.Add(value);
            process=await session.Start(info,deadline.Token).WaitAsync(deadline.Token).ConfigureAwait(false);Check(started,deadline.Token);
            var first=await Read(process.StandardOutput.BaseStream,started,deadline.Token).ConfigureAwait(false);ClipboardStageMetadata stage;
            try{if(first?.Type!=IpcMessageType.Ping||first.RequestId!=id)throw new InvalidDataException("Clipboard stage identity.");stage=JsonSerializer.Deserialize<ClipboardStageMetadata>(first.Payload)??throw new InvalidDataException("Clipboard stage missing.");}finally{if(first!=null)Release(first.Payload);}
            if(stage.Status=="Ready")
            {
                SnapshotReadyForTest?.Invoke();
                Check(started,deadline.Token);if(stage.Sequence==0||stage.ProcessStartUtcTicks!=gesture.Window.ProcessStartUtcTicks)throw new InvalidDataException("Clipboard source stage.");
                ClipboardCopyStart? issued=null;if(current()){attempted=true;issued=issueCopy(stage.Sequence);}sent=issued!=null;
                var message=new ClipboardStartMetadata(sent?"Sent":"Rejected",issued?.Timestamp??0,issued?.InputTime??0);var write=IpcFrameCodec.WriteAsync(process.StandardInput.BaseStream,new(IpcMessageType.Ping,id,JsonSerializer.SerializeToUtf8Bytes(message)),CancellationToken.None).AsTask();
                try{await write.WaitAsync(deadline.Token).ConfigureAwait(false);}finally{if(!write.IsCompletedSuccessfully)_=ObserveWrite(write);}
            }
            else if(stage.Status!="Finished")throw new InvalidDataException("Unknown clipboard stage.");
            var metadata=await Read(process.StandardOutput.BaseStream,started,deadline.Token).ConfigureAwait(false);ClipboardResultMetadata diagnostic;
            try{if(metadata?.Type!=IpcMessageType.Ping||metadata.RequestId!=id)throw new InvalidDataException("Clipboard result identity.");diagnostic=JsonSerializer.Deserialize<ClipboardResultMetadata>(metadata.Payload)??throw new InvalidDataException("Clipboard result missing.");}finally{if(metadata!=null)Release(metadata.Payload);}
            var binary=await Read(process.StandardOutput.BaseStream,started,deadline.Token).ConfigureAwait(false);payload=binary?.Payload;
            if(binary?.Type!=IpcMessageType.BinaryChunk||binary.RequestId!=id)throw new InvalidDataException("Clipboard text identity.");
            var extra=await Read(process.StandardOutput.BaseStream,started,deadline.Token).ConfigureAwait(false);if(extra!=null){Release(extra.Payload);throw new InvalidDataException("Extra clipboard frame.");}
            await process.WaitForExitAsync(deadline.Token).ConfigureAwait(false);Check(started,deadline.Token);
            if(process.ExitCode!=0)throw new InvalidDataException("Clipboard service failure.");
            if(!Enum.TryParse<ClipboardCaptureStatus>(diagnostic.Status,out var status)||Enum.GetName(status)!=diagnostic.Status||!Enum.TryParse<ClipboardRecovery>(diagnostic.Recovery,out var recovery)||Enum.GetName(recovery)!=diagnostic.Recovery||!double.IsFinite(diagnostic.ObservedMs)||diagnostic.ObservedMs<0)throw new InvalidDataException("Clipboard diagnostic.");
            if(status!=ClipboardCaptureStatus.Success){if(payload!.Length!=0)throw new InvalidDataException("Failed clipboard result contains text.");return new(Map(status),Recovery:recovery);}
            if(diagnostic.ObservedMs>=350)return new(SelectionCaptureStatus.ClipboardTimeout,Recovery:recovery);
            if(!sent||payload!.Length>200000||!current())return new(SelectionCaptureStatus.Cancelled,Recovery:recovery);
            string text=new UTF8Encoding(false,true).GetString(payload);if(string.IsNullOrWhiteSpace(text)||text.Length>=100002||text.EnumerateRunes().Count()>50000)throw new InvalidDataException("Clipboard text bound.");
            Check(started,deadline.Token);if(!current())return new(SelectionCaptureStatus.Cancelled,Recovery:recovery);
            return new(SelectionCaptureStatus.Success,text,evidence.SelectionBounds,evidence.RuntimeId,evidence.ControlBounds,SourceKind:SelectionSourceKind.Clipboard,Recovery:recovery);
        }
        catch(OperationCanceledException)when(deadline.IsCancellationRequested||!current()){return new(cancellation.IsCancellationRequested||!current()?SelectionCaptureStatus.Cancelled:SelectionCaptureStatus.ClipboardTimeout,Recovery:sent?ClipboardRecovery.Unconfirmed:ClipboardRecovery.NotNeeded);}
        catch(TimeoutException){return new(SelectionCaptureStatus.ClipboardTimeout,Recovery:sent?ClipboardRecovery.Unconfirmed:ClipboardRecovery.NotNeeded);}
        catch(Exception error)when(error is IOException or InvalidDataException or JsonException or DecoderFallbackException or InvalidOperationException or ArgumentException or System.ComponentModel.Win32Exception){FailureCode=error.GetType().Name;return new(SelectionCaptureStatus.ProtocolFailure,Recovery:attempted?ClipboardRecovery.Unconfirmed:ClipboardRecovery.NotNeeded);}
        finally
        {
            if(payload!=null)Release(payload);
            if(sent&&process!=null){try{process.StandardInput.Close();if(!process.HasExited)await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(400)).ConfigureAwait(false);}catch(Exception error)when(error is IOException or InvalidOperationException or TimeoutException){FailureCode=error.GetType().Name;}}
            lock(sync)cleanup=Cleanup(session);
        }
    }
    private static SelectionCaptureStatus Map(ClipboardCaptureStatus status)=>status switch{ClipboardCaptureStatus.Cancelled=>SelectionCaptureStatus.Cancelled,ClipboardCaptureStatus.Timeout=>SelectionCaptureStatus.ClipboardTimeout,ClipboardCaptureStatus.Unsupported=>SelectionCaptureStatus.ClipboardUnsupported,ClipboardCaptureStatus.Busy=>SelectionCaptureStatus.ClipboardBusy,ClipboardCaptureStatus.Unrelated or ClipboardCaptureStatus.InvalidText=>SelectionCaptureStatus.ClipboardUnrelated,ClipboardCaptureStatus.Denied=>SelectionCaptureStatus.EmptyOrUnsupported,_=>SelectionCaptureStatus.ProtocolFailure};
    private void Release(byte[] bytes){CryptographicOperations.ZeroMemory(bytes);PayloadReleasedForTest?.Invoke(bytes);}
    internal async Task<IpcFrame?> Read(Stream stream,long started,CancellationToken cancellation)
    {
        var pending=IpcFrameCodec.ReadAsync(stream,cancellation).AsTask();bool delivered=false;
        try{var result=await pending.WaitAsync(cancellation).ConfigureAwait(false);Check(started,cancellation);delivered=true;return result;}
        finally{if(BeforeReadSettlementForTest!=null)await BeforeReadSettlementForTest(pending).ConfigureAwait(false);if(!delivered)LateReadObserverForTest=ObserveRead(pending);}
    }
    private async Task ObserveRead(Task<IpcFrame?> pending){LateReadObserverStartedForTest?.Invoke();try{var result=await pending.ConfigureAwait(false);if(result!=null)Release(result.Payload);}catch(Exception error){FailureCode=error.GetType().Name;}}
    private async Task ObserveWrite(Task pending){try{await pending.ConfigureAwait(false);}catch(Exception error){FailureCode=error.GetType().Name;}}
    private async Task Cleanup(OwnedProcessSession session)
    {
        bool done=await session.TryFinalizeAsync().ConfigureAwait(false);if(!done){try{await session.StartupSettlement.ConfigureAwait(false);}catch(Exception error){FailureCode=error.GetType().Name;}done=await session.TryFinalizeAsync(retry:true).ConfigureAwait(false);}lock(sync)if(done&&owner==session)owner=null;
    }
    public async ValueTask DisposeAsync(){Task? task;OwnedProcessSession? session;lock(sync){disposed=true;task=cleanup;session=owner;}if(task!=null)await task.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);if(session!=null){if(!await session.TryFinalizeAsync(retry:true).ConfigureAwait(false))throw new IOException("Clipboard service ownership remains unconfirmed.");lock(sync)if(owner==session)owner=null;}}
}
