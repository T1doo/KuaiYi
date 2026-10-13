using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.OleTransfer;
using KuaiYi.Windows;

namespace KuaiYi.App;

// Compiled only by the explicit CI property. The ordinary application does not contain this entry or access surface.
internal sealed partial class ShellWindow
{
    internal bool CiTextDropEnabled;
    internal Action<OleCiTraceEvent>? CiTrace;
    internal readonly ConcurrentQueue<OleReceiverEvent> CiOleEvents=new();
    internal readonly ConcurrentDictionary<OleReceiverClient,OleTargetRegistrar> CiRegistrars=new();
    internal readonly ConcurrentDictionary<OleReceiverClient,CiLifetime> CiLifetimes=new();
    internal readonly ConcurrentQueue<object> CiReceiverTransitions=new();
    internal readonly ConcurrentDictionary<Guid,long> CiDropBegins=new();
    internal bool CiUnsafeCreation;
    internal sealed record CiLifetime(Guid Instance,Func<Task> Run,Func<Task> Control){public Process? HeldProcess;public Task? OriginalRun;public readonly ConcurrentQueue<Task> OriginalControls=new();public bool ControlsOverflow;}
    internal void CiTrackOriginal(OleReceiverClient client,Task task,bool run)
    {
        if(!CiLifetimes.TryGetValue(client,out var life))return;
        if(run)life.OriginalRun=task;
        else if(life.OriginalControls.Count<256)life.OriginalControls.Enqueue(task);
        else life.ControlsOverflow=true;
    }
    internal OleCiOwnerDiagnostic[] CiDiagnosticOwners()=>CiLifetimes.Take(8).Select(pair=>
    {
        var row=new OleCiOwnerDiagnostic {Kind="Receiver",Run=OleCiDiagnostic.TaskState(pair.Value.OriginalRun),Control=pair.Value.ControlsOverflow?"UNKNOWN":OleCiDiagnostic.TasksState(pair.Value.OriginalControls.ToArray())};
        row.ErrorRead=OleCiDiagnostic.StatusState(pair.Key.DiagnosticReadTaskStatus);
        row.TargetTrace=pair.Key.TargetObservations.Select(item=>item.ToString()).ToArray();
        if(CiRegistrars.TryGetValue(pair.Key,out var registrar)){row.RegistrarTask=OleCiDiagnostic.StatusState(registrar.DiagnosticTaskStatus);row.RegistrarStage=registrar.DiagnosticStage.ToString();row.RegistrarFailureStage=registrar.DiagnosticFailureStage.ToString();}
        if(pair.Key.StartupFailure is {} f)row.StartupFailure=new(){Stage=f.Stage.ToString(),Exception=f.Exception.ToString(),HResult=f.HResult};
        try { row.ExitCode=pair.Key.OwnedExitCode;if(row.ExitCode.HasValue)row.ProcessState="EXITED";else if(pair.Value.HeldProcess is {} held)row.ProcessState=held.HasExited?"EXITED":"RUNNING";else if(pair.Key.OwnedProcessId.HasValue&&pair.Key.ObserveExit())row.ProcessState="EXITED"; } catch { }
        return row;
    }).ToArray();
    internal void CiReceiverCreated(OleReceiverClient client,OleTargetRegistrar registrar,Guid instance,Func<Task> run,Func<Task> control)
    {
        var prior=CiLifetimes.Select(pair=>new{pair.Value.Instance,pid=pair.Key.OwnedProcessId,actualExit=pair.Value.HeldProcess?.HasExited??pair.Key.ObserveExit(),runSettled=pair.Value.Run().IsCompleted,controlSettled=pair.Value.Control().IsCompleted,ownedExit=pair.Key.ObserveExit(),revoked=CiRegistrars[pair.Key].IsSettledAndRevoked}).ToArray();
        bool safe=prior.All(p=>p.actualExit&&p.runSettled&&p.controlSettled&&p.ownedExit&&p.revoked);if(!safe)CiUnsafeCreation=true;
        CiReceiverTransitions.Enqueue(new{utc=DateTimeOffset.UtcNow,timestamp=Stopwatch.GetTimestamp(),phase="Created",instance,safe,prior});
        CiRegistrars[client]=registrar;CiLifetimes[client]=new(instance,run,control);
    }
    internal void CiReceiverFeedback(OleReceiverEvent item,OleReceiverClient client)
    {
        CiOleEvents.Enqueue(item);
        switch(item.Phase)
        {
            case "Ready": CiTrace?.Invoke(OleCiTraceEvent.ReceiverReady); break;
            case "QueryBegin": CiTrace?.Invoke(OleCiTraceEvent.ReceiverQueryBegin); break;
            case "QueryEnd": CiTrace?.Invoke(item.Effect==1?OleCiTraceEvent.ReceiverQueryEndCopy:OleCiTraceEvent.ReceiverQueryEndNoCopy); break;
            case "DropBegin": CiTrace?.Invoke(OleCiTraceEvent.ReceiverDropBegin); break;
        }
        if(item.Phase=="DropBegin")CiDropBegins[item.Instance]=Stopwatch.GetTimestamp();
        if(item.Phase=="Ready")
        {
            var life=CiLifetimes[client];var held=Process.GetProcessById(client.OwnedProcessId!.Value);_=held.Handle;life.HeldProcess=held;
            CiReceiverTransitions.Enqueue(new{utc=DateTimeOffset.UtcNow,timestamp=Stopwatch.GetTimestamp(),phase="Ready",item.Instance,pid=held.Id,registrarThread=CiRegistrars[client].NativeRegistrationThreadId});
        }
    }
    internal int CiDown,CiMove,CiUp,CiHeartbeat;
    internal void CiObservePointer()
    {
        button.AddHandler(UIElement.PreviewMouseLeftButtonDownEvent,new MouseButtonEventHandler((_,_)=>CiDown++),handledEventsToo:true);
        button.AddHandler(UIElement.PreviewMouseMoveEvent,new MouseEventHandler((_,_)=>CiMove++),handledEventsToo:true);
        button.AddHandler(UIElement.PreviewMouseLeftButtonUpEvent,new MouseButtonEventHandler((_,_)=>CiUp++),handledEventsToo:true);
    }
    internal void CiEnableDrop()
    {
        if(!lifecycle.TryPrepareEnable(out var intent,out _))throw new InvalidOperationException("Public fixture enable rejected.");
        if(!lifecycle.ConfirmEnable(intent!,true))throw new InvalidOperationException("Public fixture binding rejected.");
        Refresh();
    }
    internal void CiCancelDrop(){if(lifecycle.Gate.Snapshot.Activation==Activation.On)Toggle();else{RevokeTextDrop();Refresh();}}
    internal CiPeer? CiCurrentPeer()=>dropPeer is {} peer&&CiRegistrars.TryGetValue(peer.Client,out var registrar)?new(peer.Client,registrar,peer.Run,()=>peer.Control,peer.Lease.Receiver):null;
    internal PixelRect CiAnchor=>anchor;
    internal bool CiHasSource=>selectionSource!=null;
    internal bool CiPositionSettled=>positionSave.IsCompleted;
    internal sealed record CiPeer(OleReceiverClient Client,OleTargetRegistrar Registrar,Task Run,Func<Task> ReadControl,Guid Instance){public Task Control=>ReadControl();}
}

internal static class HostedOleProbe
{
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=false,Converters={new JsonStringEnumConverter()}};
    private sealed class InputBlocked(string message):Exception(message);
    public static int Run(string output,string sourceExecutable,string publicFixture)
    {
        var diagnostic=new OleCiDiagnostic(Path.GetFullPath(output)+".diagnostic.json");
        diagnostic.At("Entry");
        try { return RunCore(output,sourceExecutable,publicFixture,diagnostic); }
        catch(Exception error) { diagnostic.Fail(error); return 3; }
        finally { diagnostic.Snapshot(); }
    }
    private static int RunCore(string output,string sourceExecutable,string publicFixture,OleCiDiagnostic diagnostic)
    {
        output=Path.GetFullPath(output);sourceExecutable=Path.GetFullPath(sourceExecutable);publicFixture=Path.GetFullPath(publicFixture);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        diagnostic.At("Config");
        string config=Path.Combine(Path.GetTempPath(),"KuaiYi-public-ole-ci-"+Guid.NewGuid().ToString("N"));
        var coordinator=new SettingsCoordinator(new(Path.Combine(config,"config")),new(Path.Combine(config,"secrets"),new NoKeys(),new DirectoryPolicy()));
        using(var draft=coordinator.OpenDraftAsync().GetAwaiter().GetResult())
        {draft.Update(s=>s with{TriggerMode=TriggerMode.Drop,ActiveProfileId="public",Profiles=[new(){Id="public",Model="public-fixture",BaseUrl="https://public.invalid",Authentication=ApiAuthentication.None}]});coordinator.SaveAsync(draft).GetAwaiter().GetResult();}
        using var self=Process.GetCurrentProcess();
        diagnostic.At("WindowCreate");
        var app=new Application();OleOwnershipProbe.Verify(app);
        var window=new ShellWindow("KuaiYi.OleCi."+Guid.NewGuid().ToString("N"),coordinator,enableSelectionPlatform:false){CiTextDropEnabled=true,CiTrace=diagnostic.Trace};
        Pointer.Trace=diagnostic.Trace;
        var diagnosticSources=new ConcurrentQueue<PublicSource>();
        diagnostic.Owners=()=>window.CiDiagnosticOwners().Concat(diagnosticSources.Select(source=>source.Snapshot())).Take(8).ToArray();
        int snapshotBusy=0;
        using var lateSnapshots=new Timer(_=>
        {
            if(Interlocked.CompareExchange(ref snapshotBusy,1,0)!=0)return;
            try{diagnostic.Snapshot();}catch{ /* Optional checkpoint never changes the retained failure. */ }finally{Volatile.Write(ref snapshotBusy,0);}
        },null,Timeout.InfiniteTimeSpan,Timeout.InfiniteTimeSpan);
        window.CiObservePointer();var heartbeat=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(25)};heartbeat.Tick+=(_,_)=>window.CiHeartbeat++;
        var closed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);window.Closed+=(_,_)=>{heartbeat.Stop();closed.TrySetResult();};
        var events=new ConcurrentQueue<object>();var scenarios=new List<object>();var errors=new List<string>();string inputStatus="NotRun";bool recovered=false,closeConfirmed=false;
        var clock=Stopwatch.StartNew();Task? workflow=null;using var overall=new CancellationTokenSource(TimeSpan.FromSeconds(60));
        void Record(string phase,object? details=null){var item=new{ms=clock.Elapsed.TotalMilliseconds,phase,details};events.Enqueue(item);Console.Error.WriteLine("OLE_CI_PHASE "+JsonSerializer.Serialize(item,Json));}
        async Task<T> Ui<T>(Func<T> action)=>await window.Dispatcher.InvokeAsync(action).Task.WaitAsync(TimeSpan.FromSeconds(1),overall.Token);
        async Task UiAction(Action action)=>await window.Dispatcher.InvokeAsync(action).Task.WaitAsync(TimeSpan.FromSeconds(1),overall.Token);
        async Task Until(Func<Task<bool>> ready,int milliseconds,string reason)
        {long started=Stopwatch.GetTimestamp();while(!await ready()){overall.Token.ThrowIfCancellationRequested();if(Stopwatch.GetElapsedTime(started).TotalMilliseconds>=milliseconds)throw new TimeoutException(reason);await Task.Delay(20,overall.Token);}}
        async Task<ShellWindow.CiPeer> ReadyPeer()
        {
            diagnostic.At("ReceiverReadyWait");
            await Until(async()=>await Ui(()=>window.CiCurrentPeer()?.Client.IsReady==true),12000,"ReceiverReadyDeadline");
            var peer=await Ui(()=>window.CiCurrentPeer()!)
                ??throw new InvalidOperationException("Missing receiver owner.");
            if(peer.Client.OwnedProcessId is not >0||peer.Registrar.NativeRegistrationThreadId==0)throw new InvalidOperationException("Missing actual receiver/registrar identity.");
            return peer;
        }
        async Task<bool> FinalizePeer(ShellWindow.CiPeer peer,Process receiver)
        {
            await Until(()=>Task.FromResult(peer.Run.IsCompleted&&peer.Control.IsCompleted),5000,"OriginalReceiverSettlementDeadline");
            bool finalized=await peer.Client.TryFinalizeAsync(true);
            bool actualExit=receiver.HasExited;bool revoked=peer.Registrar.IsSettledAndRevoked;
            Record("ReceiverFinalization",new{peer.Instance,pid=peer.Client.OwnedProcessId,actualExit,revoked,finalized,runSettled=peer.Run.IsCompleted,controlSettled=peer.Control.IsCompleted,exit=peer.Client.ExitState?.ToString()});
            return finalized&&actualExit&&revoked&&peer.Client.ObserveExit()&&peer.Client.ExitState==HostExitState.ExitConfirmed;
        }
        Process HoldReceiver(ShellWindow.CiPeer peer)
        {
            var process=Process.GetProcessById(peer.Client.OwnedProcessId!.Value);_=process.Handle;
            if(process.HasExited||process.SessionId!=self.SessionId||!string.Equals(process.MainModule?.FileName,Path.Combine(AppContext.BaseDirectory,"KuaiYi.OleReceiver.exe"),StringComparison.OrdinalIgnoreCase)){process.Dispose();throw new InvalidOperationException("Actual receiver identity mismatch.");}
            return process; // Diagnostic handle only. Only the production Client terminates its own Process object.
        }
        async Task RunScenario(string name,bool closing)
        {
            await UiAction(window.CiEnableDrop);var peer=await ReadyPeer();using var receiver=HoldReceiver(peer);
            nint hwnd=await Ui(()=>new WindowInteropHelper(window).Handle);
            uint uiThread=NativeDesktop.GetWindowThreadProcessId(hwnd,out uint uiPid);
            if(uiPid!=Environment.ProcessId||uiThread==peer.Registrar.NativeRegistrationThreadId)throw new InvalidOperationException("Presentation and registrar thread identity mismatch.");
            Record("ReceiverReady",new{name,peer.Instance,pid=receiver.Id,uiPid,uiThread,registrarThread=peer.Registrar.NativeRegistrationThreadId});
            diagnostic.At("SourceStart");
            var source=new PublicSource(sourceExecutable,publicFixture,output+"."+name+".source.jsonl",Record,diagnostic.Trace);
            diagnosticSources.Enqueue(source);
            Exception? scenarioFailure=null;
            try {
            diagnostic.At("SourceReadyWait");
            var ready=await source.Ready.Task.WaitAsync(TimeSpan.FromSeconds(8),overall.Token);
            if(ready.ProcessId!=source.Process.Id)throw new InvalidOperationException("Owned source identity mismatch.");
            var before=await Ui(()=>window.CiAnchor);int downBefore=await Ui(()=>window.CiDown),beatsBefore=await Ui(()=>window.CiHeartbeat);
            Pointer.RequireIdle();Pointer.RequireForeground((nint)ready.Hwnd);
            Pointer.Move(ready.X,ready.Y,(nint)ready.Hwnd);Pointer.Down();await Task.Delay(100,overall.Token);
            Pointer.Move(before.Center.X,before.Center.Y,hwnd);
            diagnostic.At("DragEnterWait");
            await Until(()=>Task.FromResult(window.CiOleEvents.Any(e=>e.Instance==peer.Instance&&e.Phase=="QueryEnd"&&e.Effect==1)),1000,"RealDragEnterNotObserved");
            diagnostic.At("GetDataWait");
            Pointer.Up();await source.Blocking.Task.WaitAsync(TimeSpan.FromSeconds(2),overall.Token);
            var drop=window.CiOleEvents.LastOrDefault(e=>e.Instance==peer.Instance&&e.Phase=="DropBegin");
            if(drop==null||drop.Apartment!="STA"||drop.NativeThreadId==0||drop.NativeThreadId==uiThread||drop.Source?.ProcessId!=(uint)source.Process.Id)throw new InvalidOperationException("Real production Drop/source/apartment not observed.");
            if(!window.CiDropBegins.TryGetValue(peer.Instance,out long dropStarted))throw new InvalidOperationException("Missing actual DropBegin receipt time.");
            long blockingStarted=source.BlockingTimestamp;int beatAtBlock=await Ui(()=>window.CiHeartbeat);
            void RequireStillBlocked()
            {
                if(receiver.HasExited||peer.Run.IsCompleted||source.BlockReturned||Stopwatch.GetElapsedTime(blockingStarted).TotalMilliseconds>=1500||Stopwatch.GetElapsedTime(dropStarted).TotalMilliseconds>=1500)throw new InvalidOperationException("Original GetData/Drop no longer live before response action.");
            }
            Record("GetDataBlockingObserved",new{name,pid=source.Process.Id,receiverPid=receiver.Id,drop.NativeThreadId,drop.ThreadId,drop.Apartment,uiThread,registrarThread=peer.Registrar.NativeRegistrationThreadId,sourceThread=source.BlockingNativeThread,receiverStillRunning=!receiver.HasExited,sourceStillRunning=!source.Process.HasExited});
            RequireStillBlocked();diagnostic.Observed("Drop");diagnostic.Observed("GetData");diagnostic.At("Heartbeat");
            await Task.Delay(100,overall.Token);int beats=await Ui(()=>window.CiHeartbeat);
            if(beats<=beatAtBlock)throw new InvalidOperationException("Presentation heartbeat stopped during real GetData.");
            if(closing)
            {
                RequireStillBlocked();
                Record("CloseRequestedDuringGetData",new{elapsedMs=Stopwatch.GetElapsedTime(blockingStarted).TotalMilliseconds});
                diagnostic.At("Close");
                await UiAction(()=>{RequireStillBlocked();window.Close();});await closed.Task.WaitAsync(TimeSpan.FromSeconds(6),overall.Token);
                diagnostic.At("CloseFinalize");
                closeConfirmed=await FinalizePeer(peer,receiver);
                if(!closeConfirmed)throw new InvalidOperationException("Close ownership unconfirmed.");
                diagnostic.Observed("Close");
            }
            else
            {
                bool attemptedBeforeExit=!receiver.HasExited&&!peer.Run.IsCompleted;
                Exception? inputFailure=null;
                try
                {
                    RequireStillBlocked();Pointer.RequireIdle();Pointer.RequireForeground((nint)ready.Hwnd);Pointer.Move(before.Center.X,before.Center.Y,hwnd);Pointer.Down();await Task.Delay(50,overall.Token);
                    Pointer.RequireCapture(uiThread,hwnd);Pointer.MoveCaptured(before.Center.X-80,before.Center.Y-40);await Task.Delay(100,overall.Token);Pointer.Up();await Task.Delay(50,overall.Token);
                    var after=await Ui(()=>window.CiAnchor);int down=await Ui(()=>window.CiDown),up=await Ui(()=>window.CiUp);
                    bool moved=after!=before&&NativeDesktop.GetWindowRect(hwnd,out var actual)&&actual.ToPixels().Contains(after);
                    inputStatus=attemptedBeforeExit&&down>downBefore&&up>0&&moved&&Stopwatch.GetElapsedTime(blockingStarted).TotalMilliseconds<1500?"Pass":"Blocked";
                    Record("SyntheticDragObserved",new{inputStatus,attemptedBeforeExit,down,up,before,after,moved,elapsedMs=Stopwatch.GetElapsedTime(blockingStarted).TotalMilliseconds,receiverExitedByEnd=receiver.HasExited});
                }
                catch(InputBlocked error){inputStatus="Blocked";Record("SyntheticInputBlocked",new{reason=error.Message});}
                catch(Exception error){inputFailure=error;throw;}
                finally
                {
                    await OleCiScenarioCleanup.CompleteAsync(inputFailure,Pointer.ReleaseOwnedPress,()=>ValueTask.CompletedTask,
                        error=>{diagnostic.Fail(error,true);if(inputFailure==null)diagnostic.Fail(error,atStage:"Cleanup");});
                }
                // An actual self-drag may already cancel the lease. This direct production command proves lifecycle only.
                diagnostic.At("Cancel");
                await UiAction(window.CiCancelDrop);diagnostic.At("CancelFinalize");
                if(!await FinalizePeer(peer,receiver))throw new InvalidOperationException("Cancel ownership unconfirmed.");
                diagnostic.Observed("Cancel");
                if(await Ui(()=>window.CiHasSource))throw new InvalidOperationException("Blocked/late source committed.");
                diagnostic.At("Recovery");
                await UiAction(window.CiEnableDrop);var next=await ReadyPeer();
                if(next.Instance==peer.Instance||next.Client.OwnedProcessId==peer.Client.OwnedProcessId)throw new InvalidOperationException("Old receiver reused.");
                recovered=true;diagnostic.Observed("Recovery");Record("RecoveryAfterConfirmedExit",new{oldInstance=peer.Instance,newInstance=next.Instance,oldPid=peer.Client.OwnedProcessId,newPid=next.Client.OwnedProcessId});
                using var nextReceiver=HoldReceiver(next);await UiAction(window.CiCancelDrop);
                diagnostic.At("RecoveryFinalize");
                if(!await FinalizePeer(next,nextReceiver))throw new InvalidOperationException("Recovery cleanup unconfirmed.");
            }
            scenarios.Add(new{name,realProductionDrop=true,realGetDataBlocked=true,uiHeartbeatAdvanced=beats>beatAtBlock,heartbeatBefore=beatsBefore,heartbeatAtBlock=beatAtBlock,heartbeatAfter=beats,receiverPid=receiver.Id,sourcePid=source.Process.Id,registrarThread=peer.Registrar.NativeRegistrationThreadId,receiverThread=drop.NativeThreadId});
            } catch(Exception error) { scenarioFailure=error;diagnostic.Fail(error);throw; }
            finally
            {
                try
                {
                    await OleCiScenarioCleanup.CompleteAsync(scenarioFailure,
                        ()=>{try{Pointer.ReleaseOwnedPress();diagnostic.Trace(OleCiTraceEvent.OwnedInputCleanupSettled);}catch{diagnostic.Trace(OleCiTraceEvent.OwnedInputCleanupFailed);throw;}},
                        async()=>{diagnostic.Trace(OleCiTraceEvent.SourceCleanupBegin);try{await source.DisposeAsync();diagnostic.Trace(OleCiTraceEvent.SourceCleanupCompleted);}catch{diagnostic.Trace(OleCiTraceEvent.SourceCleanupFailed);throw;}},
                        error=>{diagnostic.Fail(error,true);if(scenarioFailure==null)diagnostic.Fail(error,atStage:"Cleanup");});
                }
                finally { diagnostic.Snapshot(); }
            }
        }
        window.Loaded+=(_,_)=>
        {
            heartbeat.Start();workflow=Task.Run(async()=>
            {
                try{diagnostic.At("PresentationReady");await window.PresentationStartup;await RunScenario("cancel-recover",false);await RunScenario("close",true);}
                catch(InputBlocked error){diagnostic.Fail(error);inputStatus="Blocked";errors.Add(error.Message);Record("EnvironmentInputBlocked",new{error.Message});}
                catch(Exception error){diagnostic.Fail(error);errors.Add(error.GetType().Name+":"+error.Message);Record("ContractFailure",new{type=error.GetType().Name,error.Message});}
                finally
                {
                    try { Pointer.ReleaseOwnedPress(); }
                    catch(Exception error){diagnostic.Fail(error,true);errors.Add("InputReleaseUnconfirmed:"+error.GetType().Name);}
                    // Keep a later checkpoint while owned receiver tasks are settling, even if WPF close is blocked.
                    lateSnapshots.Change(TimeSpan.FromSeconds(1),TimeSpan.FromSeconds(1));
                    try { if(!closed.Task.IsCompleted)await UiAction(window.Close); }
                    catch(Exception error){diagnostic.Fail(error,true);errors.Add("CloseUnconfirmed:"+error.GetType().Name);}
                    diagnostic.Snapshot();
                }
            });
        };
        app.Run(window);
        try { workflow?.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult(); }
        catch(Exception error) { diagnostic.Fail(error,atStage:"WorkflowJoin");throw; }
        string isolation=scenarios.Count==2&&recovered&&closeConfirmed&&errors.Count==0&&!window.CiUnsafeCreation?"Pass":errors.Count>0&&inputStatus=="Blocked"&&scenarios.Count==0?"Blocked":"Fail";
        int exit=isolation=="Pass"?0:isolation=="Blocked"?2:3;
        diagnostic.At("FinalSnapshot");
        File.WriteAllLines(output+".events.jsonl",events.Select(e=>JsonSerializer.Serialize(e,Json)));
        var finalDiagnostics=window.CiLifetimes.Select(pair=>new{pair.Value.Instance,pair.Key.OwnedProcessId,pair.Key.OwnedExitCode,pair.Key.StartupFailure,pair.Key.DiagnosticReadFailure,pair.Key.DiagnosticReadSettled,OriginalRunSettled=pair.Value.Run().IsCompleted,OriginalControlSettled=pair.Value.Control().IsCompleted,OwnedFinalized=pair.Key.TryFinalizeAsync().AsTask().GetAwaiter().GetResult()}).ToArray();
        diagnostic.At("ReceiptWrite");
        File.WriteAllText(output,JsonSerializer.Serialize(new{Kind="HostedOleResponsivenessPublicProbe",Source="PublicFixtureOnly",ModelCalls=0,ProductionTextDrop=true,IsolationStatus=isolation,SyntheticPointerStatus=inputStatus,PhysicalMouseAcceptance="NotRun",Windows11Acceptance="NotRun",RecoveredAfterConfirmedExit=recovered,CloseConfirmed=closeConfirmed,AllReceiverCreationsAfterConfirmedExit=!window.CiUnsafeCreation,ReceiverTransitions=window.CiReceiverTransitions,Scenarios=scenarios,Errors=errors,ReceiverEvents=window.CiOleEvents.Select(e=>new{e.Instance,e.Transaction,e.Phase,e.Effect,e.CodeUnits,e.ThreadId,e.NativeThreadId,e.Apartment,e.CleanupCompleted,SourcePid=e.Source?.ProcessId}),StartupDiagnostics=finalDiagnostics,Registrars=window.CiRegistrars.Select(pair=>new{window.CiLifetimes[pair.Key].Instance,pair.Value.NativeRegistrationThreadId,pair.Value.RegistrationThreadId,pair.Value.IsSettledAndRevoked,pair.Value.FailureCode}),ExitCode=exit},Json));
        diagnostic.At("Cleanup");
        foreach(var life in window.CiLifetimes.Values)life.HeldProcess?.Dispose();
        if(Directory.Exists(config))Directory.Delete(config,true);diagnostic.At("Complete");return exit;
    }
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoKeys:ISecretProtector{public byte[] Protect(ReadOnlySpan<byte> plain)=>throw new InvalidOperationException("No keys in public OLE fixture.");public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>throw new InvalidOperationException("No keys in public OLE fixture.");}
    private sealed record SourceReady(int ProcessId,long Hwnd,int X,int Y);
    private sealed class PublicSource:IAsyncDisposable
    {
        public Process Process{get;}
        public TaskCompletionSource<SourceReady> Ready{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Blocking{get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public uint BlockingNativeThread;
        public long BlockingTimestamp;
        private int blockReturned;
        public bool BlockReturned=>Volatile.Read(ref blockReturned)!=0;
        private readonly Task read;private readonly Task<bool> errors;private Task? closingWrite,closingFlush,closingExitWait;
        private static readonly HashSet<PublicSource> Unsettled=[];
        public PublicSource(string executable,string fixture,string log,Action<string,object?> record,Action<OleCiTraceEvent> trace)
        {
            lock(Unsettled)if(Unsettled.Count!=0)throw new InvalidOperationException("Prior owned source settlement unconfirmed.");
            var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"--ci-stdio",fixture,"GetDataBlocked","Copy",log})start.ArgumentList.Add(arg);
            Process=Process.Start(start)??throw new IOException("Owned source start failed.");errors=OleCiSourcePipe.DrainErrorsAsync(Process.StandardError.BaseStream);
            read=OleCiSourcePipe.ReadMetadataAsync(Process.StandardOutput.BaseStream,line=>
            {
                    if(line.Length>8192)throw new InvalidDataException("Source metadata bound.");using var doc=JsonDocument.Parse(line);var root=doc.RootElement;var item=root.GetProperty("item");string? kind=item.GetProperty("kind").GetString();
                    record("SourceMetadata",root.Clone());
                    switch(kind)
                    {
                        case "SourceReady": trace(OleCiTraceEvent.SourceReady); break;
                        case "DoDragDropBegin": trace(OleCiTraceEvent.SourceDragBegin); break;
                        case "DoDragDropReturn": trace(OleCiTraceEvent.SourceDragReturn); break;
                        case "QueryGetData": trace(OleCiTraceEvent.SourceQuery); break;
                        case "GetData": trace(OleCiTraceEvent.SourceGetData); break;
                        case "GetDataBlocking": trace(OleCiTraceEvent.SourceGetDataBlocking); break;
                        case "GetDataBlockReturned": trace(OleCiTraceEvent.SourceGetDataReturned); break;
                    }
                    if(kind=="SourceReady")Ready.TrySetResult(new(root.GetProperty("processId").GetInt32(),item.GetProperty("hwnd").GetInt64(),item.GetProperty("x").GetInt32(),item.GetProperty("y").GetInt32()));
                    if(kind=="GetDataBlocking"){if(root.GetProperty("frequency").GetInt64()!=Stopwatch.Frequency)throw new InvalidDataException("Source monotonic frequency mismatch.");BlockingTimestamp=root.GetProperty("timestamp").GetInt64();BlockingNativeThread=root.GetProperty("nativeThreadId").GetUInt32();Blocking.TrySetResult();}
                    if(kind=="GetDataBlockReturned")Volatile.Write(ref blockReturned,1);
            });
        }
        private OleCiOwnerDiagnostic? finalSnapshot;
        public OleCiOwnerDiagnostic Snapshot()
        {
            if(finalSnapshot is {} final)return final;
            var row=new OleCiOwnerDiagnostic {Kind="Source",MetadataRead=OleCiDiagnostic.TaskState(read),ErrorRead=OleCiDiagnostic.TaskState(errors),CloseWrite=OleCiDiagnostic.TaskState(closingWrite),CloseFlush=OleCiDiagnostic.TaskState(closingFlush),ExitWait=OleCiDiagnostic.TaskState(closingExitWait)};
            try { row.ProcessState=Process.HasExited?"EXITED":"RUNNING";if(Process.HasExited)row.ExitCode=Process.ExitCode; } catch { }
            return row;
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if(!Process.HasExited){closingWrite=Process.StandardInput.WriteLineAsync("close");await closingWrite.WaitAsync(TimeSpan.FromSeconds(1));closingFlush=Process.StandardInput.FlushAsync();await closingFlush.WaitAsync(TimeSpan.FromSeconds(1));closingExitWait=Process.WaitForExitAsync();await closingExitWait.WaitAsync(TimeSpan.FromSeconds(8));}
                await read.WaitAsync(TimeSpan.FromSeconds(1));bool empty=await errors.WaitAsync(TimeSpan.FromSeconds(1));
                if(Process.ExitCode!=0||!empty)throw new IOException("Owned source failed: "+Process.ExitCode);
            }
            finally
            {
                bool finalized=false;
                try
                {
                    if(!Process.HasExited){Process.Kill(true);if(!Process.WaitForExit(3000))throw new IOException("Owned source exit unconfirmed.");}
                    try{await read.WaitAsync(TimeSpan.FromSeconds(1));}catch(Exception){if(!read.IsCompleted)throw;}
                    await errors.WaitAsync(TimeSpan.FromSeconds(1));
                    foreach(Task pending in new[]{closingWrite,closingFlush,closingExitWait}.OfType<Task>())
                        try{await pending.WaitAsync(TimeSpan.FromSeconds(1));}catch(Exception){if(!pending.IsCompleted)throw;}
                    finalized=Process.HasExited&&read.IsCompleted&&errors.IsCompleted&&closingWrite is not {IsCompleted:false}&&closingFlush is not {IsCompleted:false}&&closingExitWait is not {IsCompleted:false};
                }
                finally
                {
                    if(finalized){finalSnapshot=Snapshot();lock(Unsettled)Unsettled.Remove(this);Process.Dispose();}
                    else{lock(Unsettled)Unsettled.Add(this);}
                }
                if(!finalized)throw new IOException("Owned source original settlement unconfirmed.");
            }
        }
    }
    private static class Pointer
    {
        [StructLayout(LayoutKind.Sequential)]private struct Mouse{public int X,Y;public uint Data,Flags,Time;public nuint Extra;}
        [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public Mouse Mouse;}
        [StructLayout(LayoutKind.Sequential)]private struct GuiInfo{public uint Size,Flags;public nint Active,Focus,Capture,MenuOwner,MoveSize,Caret;public NativeDesktop.Rect CaretRect;}
        [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] input,int size);
        [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")]private static extern int GetSystemMetrics(int index);
        [DllImport("user32.dll")]private static extern nint WindowFromPoint(NativeDesktop.Point point);
        [DllImport("user32.dll")]private static extern nint GetAncestor(nint hwnd,uint flags);
        [DllImport("user32.dll")][return:MarshalAs(UnmanagedType.Bool)]private static extern bool GetGUIThreadInfo(uint thread,ref GuiInfo info);
        private static readonly OleCiOwnedPress press=new(()=>Send(new(){Mouse=new(){Flags=2}}),()=>Send(new(){Mouse=new(){Flags=4}}));
        internal static Action<OleCiTraceEvent>? Trace;
        public static void RequireIdle(){if(new[]{1,2,16,17,18,27,91,92}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0))throw new InputBlocked("MouseOrModifierAlreadyHeld");}
        public static void RequireForeground(nint expected){if(NativeDesktop.GetForegroundWindow()!=expected)throw new InputBlocked("OwnedSourceNotForeground");}
        public static void RequireCapture(uint thread,nint hwnd){var info=new GuiInfo{Size=(uint)Marshal.SizeOf<GuiInfo>()};if(!GetGUIThreadInfo(thread,ref info)||info.Capture!=hwnd)throw new InputBlocked("PresentationDidNotCaptureSyntheticPress");}
        private static void Send(Input value){if(SendInput(1,[value],Marshal.SizeOf<Input>())!=1)throw new InputBlocked("SendInputRejected");}
        public static void Move(int x,int y,nint expected){if(GetAncestor(WindowFromPoint(new(){X=x,Y=y}),2)!=expected)throw new InputBlocked("OwnedWindowNotAtInputPoint");MoveCaptured(x,y);}
        public static void MoveCaptured(int x,int y)
        {int left=GetSystemMetrics(76),top=GetSystemMetrics(77),width=GetSystemMetrics(78),height=GetSystemMetrics(79);if(width<2||height<2||x<left||y<top||x>=left+width||y>=top+height)throw new InputBlocked("InputOutsideVirtualDesktop");Send(new(){Mouse=new(){X=(int)Math.Round((x-left)*65535.0/(width-1)),Y=(int)Math.Round((y-top)*65535.0/(height-1)),Flags=0xc001}});}
        public static void Down(){press.Down();Trace?.Invoke(OleCiTraceEvent.OwnedPressDown);}
        public static void Up(){if(press.Release())Trace?.Invoke(OleCiTraceEvent.OwnedPressUp);}
        public static void ReleaseOwnedPress()=>Up();
    }
}
