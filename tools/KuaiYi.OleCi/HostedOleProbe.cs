using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
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
    internal readonly ConcurrentQueue<OleReceiverEvent> CiOleEvents=new();
    internal readonly ConcurrentDictionary<OleReceiverClient,OleTargetRegistrar> CiRegistrars=new();
    internal readonly ConcurrentDictionary<OleReceiverClient,CiLifetime> CiLifetimes=new();
    internal readonly ConcurrentQueue<object> CiReceiverTransitions=new();
    internal readonly ConcurrentDictionary<Guid,long> CiDropBegins=new();
    internal bool CiUnsafeCreation;
    internal sealed record CiLifetime(Guid Instance,Func<Task> Run,Func<Task> Control){public Process? HeldProcess;}
    internal void CiReceiverCreated(OleReceiverClient client,OleTargetRegistrar registrar,Guid instance,Func<Task> run,Func<Task> control)
    {
        var prior=CiLifetimes.Select(pair=>new{pair.Value.Instance,pid=pair.Key.OwnedProcessId,actualExit=pair.Value.HeldProcess?.HasExited==true,runSettled=pair.Value.Run().IsCompleted,controlSettled=pair.Value.Control().IsCompleted,ownedExit=pair.Key.ObserveExit(),revoked=CiRegistrars[pair.Key].IsSettledAndRevoked}).ToArray();
        bool safe=prior.All(p=>p.actualExit&&p.runSettled&&p.controlSettled&&p.ownedExit&&p.revoked);if(!safe)CiUnsafeCreation=true;
        CiReceiverTransitions.Enqueue(new{utc=DateTimeOffset.UtcNow,timestamp=Stopwatch.GetTimestamp(),phase="Created",instance,safe,prior});
        CiRegistrars[client]=registrar;CiLifetimes[client]=new(instance,run,control);
    }
    internal void CiReceiverFeedback(OleReceiverEvent item,OleReceiverClient client)
    {
        CiOleEvents.Enqueue(item);
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
        button.PreviewMouseLeftButtonDown+=(_,_)=>CiDown++;
        button.PreviewMouseMove+=(_,_)=>CiMove++;
        button.PreviewMouseLeftButtonUp+=(_,_)=>CiUp++;
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
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=false};
    private sealed class InputBlocked(string message):Exception(message);
    public static int Run(string output,string sourceExecutable,string publicFixture)
    {
        output=Path.GetFullPath(output);sourceExecutable=Path.GetFullPath(sourceExecutable);publicFixture=Path.GetFullPath(publicFixture);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        string config=Path.Combine(Path.GetTempPath(),"KuaiYi-public-ole-ci-"+Guid.NewGuid().ToString("N"));
        var coordinator=new SettingsCoordinator(new(Path.Combine(config,"config")),new(Path.Combine(config,"secrets"),new NoKeys(),new DirectoryPolicy()));
        using(var draft=coordinator.OpenDraftAsync().GetAwaiter().GetResult())
        {draft.Update(s=>s with{TriggerMode=TriggerMode.Drop,ActiveProfileId="public",Profiles=[new(){Id="public",Model="public-fixture",BaseUrl="https://public.invalid",Authentication=ApiAuthentication.None}]});coordinator.SaveAsync(draft).GetAwaiter().GetResult();}
        using var self=Process.GetCurrentProcess();
        var app=new Application();var window=new ShellWindow("KuaiYi.OleCi."+Guid.NewGuid().ToString("N"),coordinator,enableSelectionPlatform:false){CiTextDropEnabled=true};
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
            await using var source=new PublicSource(sourceExecutable,publicFixture,output+"."+name+".source.jsonl",Record);
            var ready=await source.Ready.Task.WaitAsync(TimeSpan.FromSeconds(8),overall.Token);
            if(ready.ProcessId!=source.Process.Id)throw new InvalidOperationException("Owned source identity mismatch.");
            var before=await Ui(()=>window.CiAnchor);int downBefore=await Ui(()=>window.CiDown),beatsBefore=await Ui(()=>window.CiHeartbeat);
            Pointer.RequireIdle();Pointer.RequireForeground((nint)ready.Hwnd);
            Pointer.Move(ready.X,ready.Y,(nint)ready.Hwnd);Pointer.Down();await Task.Delay(100,overall.Token);
            Pointer.Move(before.Center.X,before.Center.Y,hwnd);
            await Until(()=>Task.FromResult(window.CiOleEvents.Any(e=>e.Instance==peer.Instance&&e.Phase=="QueryEnd"&&e.Effect==1)),1000,"RealDragEnterNotObserved");
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
            RequireStillBlocked();
            await Task.Delay(100,overall.Token);int beats=await Ui(()=>window.CiHeartbeat);
            if(beats<=beatAtBlock)throw new InvalidOperationException("Presentation heartbeat stopped during real GetData.");
            if(closing)
            {
                RequireStillBlocked();
                Record("CloseRequestedDuringGetData",new{elapsedMs=Stopwatch.GetElapsedTime(blockingStarted).TotalMilliseconds});
                await UiAction(()=>{RequireStillBlocked();window.Close();});await closed.Task.WaitAsync(TimeSpan.FromSeconds(6),overall.Token);
                closeConfirmed=await FinalizePeer(peer,receiver);
                if(!closeConfirmed)throw new InvalidOperationException("Close ownership unconfirmed.");
            }
            else
            {
                bool attemptedBeforeExit=!receiver.HasExited&&!peer.Run.IsCompleted;
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
                finally{Pointer.ReleaseOwnedPress();}
                // An actual self-drag may already cancel the lease. This direct production command proves lifecycle only.
                await UiAction(window.CiCancelDrop);
                if(!await FinalizePeer(peer,receiver))throw new InvalidOperationException("Cancel ownership unconfirmed.");
                if(await Ui(()=>window.CiHasSource))throw new InvalidOperationException("Blocked/late source committed.");
                await UiAction(window.CiEnableDrop);var next=await ReadyPeer();
                if(next.Instance==peer.Instance||next.Client.OwnedProcessId==peer.Client.OwnedProcessId)throw new InvalidOperationException("Old receiver reused.");
                recovered=true;Record("RecoveryAfterConfirmedExit",new{oldInstance=peer.Instance,newInstance=next.Instance,oldPid=peer.Client.OwnedProcessId,newPid=next.Client.OwnedProcessId});
                using var nextReceiver=HoldReceiver(next);await UiAction(window.CiCancelDrop);
                if(!await FinalizePeer(next,nextReceiver))throw new InvalidOperationException("Recovery cleanup unconfirmed.");
            }
            scenarios.Add(new{name,realProductionDrop=true,realGetDataBlocked=true,uiHeartbeatAdvanced=beats>beatAtBlock,heartbeatBefore=beatsBefore,heartbeatAtBlock=beatAtBlock,heartbeatAfter=beats,receiverPid=receiver.Id,sourcePid=source.Process.Id,registrarThread=peer.Registrar.NativeRegistrationThreadId,receiverThread=drop.NativeThreadId});
        }
        window.Loaded+=(_,_)=>
        {
            heartbeat.Start();workflow=Task.Run(async()=>
            {
                try{await window.PresentationStartup;await RunScenario("cancel-recover",false);await RunScenario("close",true);}
                catch(InputBlocked error){inputStatus="Blocked";errors.Add(error.Message);Record("EnvironmentInputBlocked",new{error.Message});}
                catch(Exception error){errors.Add(error.GetType().Name+":"+error.Message);Record("ContractFailure",new{type=error.GetType().Name,error.Message});}
                finally{Pointer.ReleaseOwnedPress();if(!closed.Task.IsCompleted)try{await UiAction(window.Close);}catch(Exception error){errors.Add("CloseUnconfirmed:"+error.GetType().Name);}}
            });
        };
        app.Run(window);workflow?.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        string isolation=scenarios.Count==2&&recovered&&closeConfirmed&&errors.Count==0&&!window.CiUnsafeCreation?"Pass":errors.Count>0&&inputStatus=="Blocked"&&scenarios.Count==0?"Blocked":"Fail";
        int exit=isolation=="Pass"?0:isolation=="Blocked"?2:3;
        File.WriteAllLines(output+".events.jsonl",events.Select(e=>JsonSerializer.Serialize(e,Json)));
        File.WriteAllText(output,JsonSerializer.Serialize(new{Kind="HostedOleResponsivenessPublicProbe",Source="PublicFixtureOnly",ModelCalls=0,ProductionTextDrop=true,IsolationStatus=isolation,SyntheticPointerStatus=inputStatus,PhysicalMouseAcceptance="NotRun",Windows11Acceptance="NotRun",RecoveredAfterConfirmedExit=recovered,CloseConfirmed=closeConfirmed,AllReceiverCreationsAfterConfirmedExit=!window.CiUnsafeCreation,ReceiverTransitions=window.CiReceiverTransitions,Scenarios=scenarios,Errors=errors,ReceiverEvents=window.CiOleEvents.Select(e=>new{e.Instance,e.Transaction,e.Phase,e.Effect,e.CodeUnits,e.ThreadId,e.NativeThreadId,e.Apartment,e.CleanupCompleted,SourcePid=e.Source?.ProcessId}),Registrars=window.CiRegistrars.Values.Select(r=>new{r.NativeRegistrationThreadId,r.RegistrationThreadId,r.IsSettledAndRevoked,r.FailureCode}),ExitCode=exit},Json));
        foreach(var life in window.CiLifetimes.Values)life.HeldProcess?.Dispose();
        if(Directory.Exists(config))Directory.Delete(config,true);return exit;
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
        private readonly Task read;private readonly Task<string> errors;
        public PublicSource(string executable,string fixture,string log,Action<string,object?> record)
        {
            var start=new ProcessStartInfo(executable){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
            foreach(string arg in new[]{"--ci-stdio",fixture,"GetDataBlocked","Copy",log})start.ArgumentList.Add(arg);
            Process=Process.Start(start)??throw new IOException("Owned source start failed.");errors=Process.StandardError.ReadToEndAsync();
            read=Task.Run(async()=>
            {
                while(await Process.StandardOutput.ReadLineAsync() is {} line)
                {
                    if(line.Length>8192)throw new InvalidDataException("Source metadata bound.");using var doc=JsonDocument.Parse(line);var root=doc.RootElement;var item=root.GetProperty("item");string? kind=item.GetProperty("kind").GetString();
                    record("SourceMetadata",root.Clone());
                    if(kind=="SourceReady")Ready.TrySetResult(new(root.GetProperty("processId").GetInt32(),item.GetProperty("hwnd").GetInt64(),item.GetProperty("x").GetInt32(),item.GetProperty("y").GetInt32()));
                    if(kind=="GetDataBlocking"){if(root.GetProperty("frequency").GetInt64()!=Stopwatch.Frequency)throw new InvalidDataException("Source monotonic frequency mismatch.");BlockingTimestamp=root.GetProperty("timestamp").GetInt64();BlockingNativeThread=root.GetProperty("nativeThreadId").GetUInt32();Blocking.TrySetResult();}
                    if(kind=="GetDataBlockReturned")Volatile.Write(ref blockReturned,1);
                }
            });
        }
        public async ValueTask DisposeAsync()
        {
            try
            {
                if(!Process.HasExited){await Process.StandardInput.WriteLineAsync("close");await Process.StandardInput.FlushAsync();await Process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));}
                await read.WaitAsync(TimeSpan.FromSeconds(1));string stderr=await errors.WaitAsync(TimeSpan.FromSeconds(1));
                if(Process.ExitCode!=0||stderr.Length>0)throw new IOException("Owned source failed: "+Process.ExitCode);
            }
            finally
            {
                if(!Process.HasExited){Process.Kill(true);if(!Process.WaitForExit(3000))throw new IOException("Owned source exit unconfirmed.");}
                Process.Dispose();
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
        private static bool pressed;
        public static void RequireIdle(){if(new[]{1,2,16,17,18,27,91,92}.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0))throw new InputBlocked("MouseOrModifierAlreadyHeld");}
        public static void RequireForeground(nint expected){if(NativeDesktop.GetForegroundWindow()!=expected)throw new InputBlocked("OwnedSourceNotForeground");}
        public static void RequireCapture(uint thread,nint hwnd){var info=new GuiInfo{Size=(uint)Marshal.SizeOf<GuiInfo>()};if(!GetGUIThreadInfo(thread,ref info)||info.Capture!=hwnd)throw new InputBlocked("PresentationDidNotCaptureSyntheticPress");}
        private static void Send(Input value){if(SendInput(1,[value],Marshal.SizeOf<Input>())!=1)throw new InputBlocked("SendInputRejected");}
        public static void Move(int x,int y,nint expected){if(GetAncestor(WindowFromPoint(new(){X=x,Y=y}),2)!=expected)throw new InputBlocked("OwnedWindowNotAtInputPoint");MoveCaptured(x,y);}
        public static void MoveCaptured(int x,int y)
        {int left=GetSystemMetrics(76),top=GetSystemMetrics(77),width=GetSystemMetrics(78),height=GetSystemMetrics(79);if(width<2||height<2||x<left||y<top||x>=left+width||y>=top+height)throw new InputBlocked("InputOutsideVirtualDesktop");Send(new(){Mouse=new(){X=(int)Math.Round((x-left)*65535.0/(width-1)),Y=(int)Math.Round((y-top)*65535.0/(height-1)),Flags=0xc001}});}
        public static void Down(){Send(new(){Mouse=new(){Flags=2}});pressed=true;}
        public static void Up(){Send(new(){Mouse=new(){Flags=4}});pressed=false;}
        public static void ReleaseOwnedPress(){if(pressed)Up();}
    }
}
