using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Windows;

namespace KuaiYi.GrowthHarness;

// Finite test-tool scenarios against the shipped Shell. No alternate animation, network or capture backend.
internal static class OverlayStageProbe
{
    internal static int Run(string output)
    {
        output=Path.GetFullPath(output);
        if(!Directory.Exists(output))throw new IOException("Existing exclusive output directory required.");
        for(DirectoryInfo? p=new(output);p!=null;p=p.Parent)
            if((p.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked output rejected.");
        string state=Path.Combine(output,"overlay-state-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(state);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var previous=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        var owner=new Owner(app,output,state);
        try
        {
            owner.Lifetime=new(owner.Drive,owner.CloseOriginal,owner.ClearState);
            Task finalization=owner.FinalizeOriginal(); // Retain before releasing the original driver.
            DispatcherOperation release=app.Dispatcher.BeginInvoke(owner.Lifetime.Release);
            app.Run();
            if(!owner.Lifetime.Original.IsCompleted||!finalization.IsCompleted)
                throw new InvalidOperationException("Dispatcher ended before original driver/finalization; state retained.");
            release.Task.GetAwaiter().GetResult();owner.Lifetime.Original.GetAwaiter().GetResult();finalization.GetAwaiter().GetResult();
            owner.Shutdown!.Task.GetAwaiter().GetResult();return owner.Succeeded?0:3;
        }
        catch(Exception error){owner.Emergency(error);throw;}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }

    private sealed class Owner(Application app,string output,string state)
    {
        private const string ShortText="公开合成文字：同一个浮钮自然展开。首段结束。";
        private const string Chunk="\n\n这是合成的中文流式段落，用于观察真实浮窗的排版、边界与高度跟随。文字只留在测试窗口，不读取其他应用，也不请求模型。";
        private readonly Stopwatch elapsed=Stopwatch.StartNew();
        private readonly CancellationTokenSource cancellation=new();
        private readonly OverlayProbeJournal journal=new();
        private readonly OverlayRasterBudget rasterBudget=new();
        private readonly List<OverlayProbePhase> phases=[];
        private readonly List<RasterFrame> rasters=[];
        private readonly List<object> files=[];
        private readonly List<object> rejectedRasterFiles=[];
        private readonly HashSet<(int Phase,long Epoch,string Kind)> captured=[];
        private readonly TaskCompletionSource closed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource nextFrame=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private TaskCompletionSource? phaseDone;
        private DispatcherTimer? deadline;
        private ShellWindow? window;
        private SettingsDraft? draft;
        private RichTextBox? body;
        private OverlayProbePhase? phase;
        private Task? originalClose;
        private bool constructionUnknown,draftRetirementStarted,closeRequested,timedOut,observing,capturing,measurementOverflowReported;
        private int depth;
        private nint initialForeground;
        private string mode="Setup";
        private string expectedMarker="";
        private RequestIdentity? sequenceIdentity;
        private long sequenceEpoch;
        private int expectedTextLength;
        private object? environment;
        internal OverlayProbeLifetime Lifetime{get;set;}=null!;
        internal DispatcherOperation? Shutdown{get;private set;}
        internal bool Succeeded{get;private set;}

        internal async Task Drive()
        {
            deadline=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(100)};
            deadline.Tick+=(_,_)=>
            {
                if(elapsed.Elapsed<TimeSpan.FromSeconds(45))return;
                deadline.Stop();timedOut=true;
                try{cancellation.Cancel();}catch(Exception error){Lifetime.Record("Cancel",error);}
                WritePending(); // Keep the original tasks/dispatcher/state; never force a successful close.
            };
            deadline.Start();
            await Open();
            await Sequence("Timing");
            await Sequence("Raster");
            phase=null;mode="Idle";
        }
        private void Check()
        {
            cancellation.Token.ThrowIfCancellationRequested();
            if(elapsed.Elapsed>=TimeSpan.FromSeconds(45))
            {timedOut=true;throw new TimeoutException("Original 45-second driver budget exceeded.");}
            if(sequenceIdentity.HasValue&&(!window!.IsVisible||!RequestCurrent()))
                throw new InvalidOperationException("Original request/epoch or visible window changed; diagnostic continuation refused.");
        }
        private bool RequestCurrent()
        {
            if(window==null||sequenceIdentity is not {} identity)return false;
            var snapshot=window.PresentationGate.Snapshot;
            return window.PresentationGate.CanApply(identity)&&snapshot.Epoch==sequenceEpoch&&snapshot.Activation==Activation.On&&
                snapshot.Suspension==Suspension.None&&snapshot.Presentation!=Presentation.Hidden;
        }
        private async Task Open()
        {
            Check();if(!Environment.UserInteractive)throw new InvalidOperationException("Interactive runner desktop required.");
            var topology=MonitorTopology.Read();var monitor=topology.Monitors.Single(m=>m.Id==topology.PrimaryId);
            var coordinator=new SettingsCoordinator(new SettingsStore(Path.Combine(state,"config")),
                new SecretFileStore(Path.Combine(state,"secrets"),new NoKeys(),new DirectoryPolicy()));
            draft=await coordinator.OpenDraftAsync(cancellation.Token);Check();
            draft.Update(s=>s with{ActiveProfileId="public",Profiles=[new(){Id="public",Model="public-fixture",BaseUrl="https://public.invalid",Authentication=ApiAuthentication.None}],
                Overlay=s.Overlay with{Theme=ThemePreference.Light,ReduceMotion=MotionPreference.Full,
                    LastPosition=new(){MonitorId=monitor.Id,X=.5,Y=.5,OffsetXDip=0,OffsetYDip=0}}});
            await coordinator.SaveAsync(draft,cancellation:cancellation.Token);Check();
            draftRetirementStarted=true;draft.Dispose();draft=null;
            constructionUnknown=true;
            window=new ShellWindow("KuaiYi.OverlayStageProbe."+Guid.NewGuid().ToString("N"),coordinator,enableSelectionPlatform:false);
            constructionUnknown=false;
            window.PresentationMeasureObservedForTest=ObserveMeasurement;
            window.Closed+=(_,_)=>closed.TrySetResult();
            initialForeground=NativeDesktop.GetForegroundWindow();
            window.Show();await window.PresentationStartup;Check();
            await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check();
            body=Descendants<RichTextBox>((DependencyObject)window.Content).Single();
            environment=new{Environment.OSVersion.VersionString,Environment.UserInteractive,SessionId=Process.GetCurrentProcess().SessionId,
                RenderTier=RenderCapability.Tier>>16,SystemClientAreaAnimation=SystemParameters.ClientAreaAnimation,
                FixtureMotionPreference="Full_OwnSettingsOnly",ActualMonitor=window.PresentationGeometry?.Monitor,
                RefreshRateHz=(int?)null,RefreshRateStatus="NotMeasured_No60HzPerformanceClaim"};
            var gate=window.PresentationGate;var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);
            if(!gate.TryEnable(origin,true))throw new InvalidOperationException("Synthetic fixture gate refused.");
            CompositionTarget.Rendering+=Observe;observing=true;
        }
        private async Task Sequence(string collection)
        {
            phase=null;phaseDone=null;mode=collection;expectedMarker="首段结束。";sequenceIdentity=null;expectedTextLength=0;
            var gate=window!.PresentationGate;
            if(!gate.TryGetProfileBinding("public",out var binding)||!gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _))
                throw new InvalidOperationException("Bound synthetic request refused.");
            sequenceIdentity=id;sequenceEpoch=gate.Snapshot.Epoch;
            await Send(id,new(OverlayResultEventKind.Begin));
            await Stage("SeedOpen",OverlayTransition.Opening,OverlayTransition.Expanded,OverlayProbeGoal.Settled,async()=>
            {await Send(id,new(OverlayResultEventKind.Delta,ShortText));await TextVisible("首段结束。");});
            expectedMarker="流式末尾标记。";
            await Stage("LongStreamGrow",OverlayTransition.Expanded,OverlayTransition.Expanded,OverlayProbeGoal.Settled,async()=>
            {
                for(int i=0;i<6;i++)
                {
                    long sent=Stopwatch.GetTimestamp();await Send(id,new(OverlayResultEventKind.Delta,Chunk));
                    if(i<5)while(Stopwatch.GetElapsedTime(sent).TotalMilliseconds<50)await Next();
                }
                await Send(id,new(OverlayResultEventKind.Delta,"\n\n流式末尾标记。"));
                await Send(id,new(OverlayResultEventKind.Complete));await TextVisible("流式末尾标记。");
            });
            var close=await CommandStage("Close", "收起",OverlayTransition.Closing,OverlayTransition.Compact,OverlayTransition.Expanded);
            var reopen=await CommandStage("Reopen", "回看",OverlayTransition.Opening,OverlayTransition.Expanded,OverlayTransition.Compact,prerequisite:close);
            var closing=await CommandStage("CloseUntilReverse", "收起",OverlayTransition.Closing,OverlayTransition.Compact,OverlayTransition.Expanded,OverlayProbeGoal.Intermediate,reopen);
            var reversed=await CommandStage("ReverseToOpen", "回看",OverlayTransition.Opening,OverlayTransition.Expanded,OverlayTransition.Closing,prerequisite:closing);
            var compact=await CommandStage("CloseToCompact", "收起",OverlayTransition.Closing,OverlayTransition.Compact,OverlayTransition.Expanded,prerequisite:reversed);
            var opening=await CommandStage("OpenUntilReverse", "回看",OverlayTransition.Opening,OverlayTransition.Expanded,OverlayTransition.Compact,OverlayProbeGoal.Intermediate,compact);
            await CommandStage("ReverseToClose", "收起",OverlayTransition.Closing,OverlayTransition.Compact,OverlayTransition.Opening,prerequisite:opening);
        }
        private Task<OverlayProbePhase> CommandStage(string name,string tag,OverlayTransition moving,OverlayTransition terminal,OverlayTransition required,
            OverlayProbeGoal goal=OverlayProbeGoal.Settled,OverlayProbePhase? prerequisite=null)
            =>Stage(name,moving,terminal,goal,()=>
            {
                Check();
                var control=Descendants<Button>((DependencyObject)window!.Content).Single(b=>Equals(b.Tag,tag));
                if(!control.IsEnabled)throw new InvalidOperationException("Original production command disabled: "+tag);
                control.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));return Task.CompletedTask;
            },required,prerequisite);
        private async Task<OverlayProbePhase> Stage(string name,OverlayTransition moving,OverlayTransition terminal,OverlayProbeGoal goal,Func<Task> issue,
            OverlayTransition? required=null,OverlayProbePhase? prerequisite=null)
        {
            Check();var geometry=window!.PresentationGeometry??throw new InvalidOperationException("Production geometry missing.");
            var current=new OverlayProbePhase(phases.Count+1,mode,name,Stopwatch.GetTimestamp(),geometry.Current,moving,terminal,goal,expectedMarker,prerequisite?.Id);
            var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            phase=current;phases.Add(current);phaseDone=done;
            // Publish the phase/waiter before Receive or Click can synchronously render.
            try
            {
                if(required.HasValue&&!OverlayProbePhase.CanIssueCommand(required.Value,geometry.Transition,geometry.NeedsFrame,geometry.Current,geometry.Anchor,prerequisite,window.PresentationAnimationEpoch))
                {current.Skip(Stopwatch.GetTimestamp());Lifetime.Record("Stage:"+mode+":"+name,new InvalidOperationException("PrerequisiteInvalid; original command not issued."));return current;}
                using var registration=cancellation.Token.Register(()=>done.TrySetCanceled(cancellation.Token));
                await issue();Check();current.Issued(Stopwatch.GetTimestamp(),window.PresentationAnimationEpoch);await done.Task;Check();return current;
            }
            finally{if(ReferenceEquals(phase,current)){phase=null;phaseDone=null;}}
        }
        private async Task Send(RequestIdentity id,OverlayResultEvent update)
        {
            Check();
            if(!await window!.ReceivePresentationAsync(id,update,cancellation.Token))throw new InvalidOperationException("Original receiver rejected "+update.Kind);
            if(update.Kind==OverlayResultEventKind.Delta)expectedTextLength=checked(expectedTextLength+update.Text.Length);
            Check();
        }
        private async Task TextVisible(string marker)
        {while(!body!.IsVisible||body.ActualWidth<=0||body.ActualHeight<=0||body.Opacity<=0||!DocumentContains(marker)||!MeasurementCurrent())await Next();}
        private bool MeasurementCurrent()=>sequenceIdentity is {} identity&&journal.HasCurrentMeasurement(identity.VersionId,expectedTextLength);
        private bool DocumentContains(string marker)=>new TextRange(body!.Document.ContentStart,body.Document.ContentEnd).Text.Contains(marker,StringComparison.Ordinal);
        private async Task Next(){Check();await nextFrame.Task.WaitAsync(cancellation.Token);Check();}

        private void ObserveMeasurement(OverlayMeasureObservation value)
        {
            if(journal.AddMeasurement(phase?.Id??0,mode,value)||measurementOverflowReported)return;
            measurementOverflowReported=true;
            Lifetime.Record("MeasurementBound",new InvalidOperationException("Original 512 measurement bound exceeded; diagnosis incomplete."));
            // Called on the production timer stack: fail the original driver through its token,
            // retaining the dispatcher and original close task rather than throwing out of this callback.
            try{cancellation.Cancel();}catch(Exception error){Lifetime.Record("MeasurementCancel",error);}
        }

        private OverlayProbeFrame Frame(long renderTicks)
        {
            var g=window!.PresentationGeometry??throw new InvalidOperationException("Production geometry disappeared.");
            nint hwnd=new WindowInteropHelper(window).Handle;
            PixelRect? native=NativeDesktop.GetWindowRect(hwnd,out var bounds)?bounds.ToPixels():null;
            return new(Stopwatch.GetTimestamp(),renderTicks,phase?.Id??0,mode,g.Transition,g.NeedsFrame,
                window.PresentationFrameApplying,depth,(long)hwnd,g.Current,g.Anchor,g.Monitor.WorkArea.Inset(g.Monitor.Dpi.CeilingPixels(12)),native,
                window.PresentationButtonScreenBounds,g.ContentDirection,g.Monitor.Dpi.Factor,body!.ActualWidth,body.VerticalOffset,
                NativeDesktop.GetForegroundWindow()==initialForeground,body.IsVisible,body.ActualHeight,body.Opacity,
                phase?.DocumentMarker is not {} marker||DocumentContains(marker),
                window.PresentationAnimationEpoch,window.PresentationAnimationStartedAt,window.PresentationLastTick,RequestCurrent(),
                MeasurementCurrent());
        }
        private void Observe(object? sender,EventArgs args)
        {
            depth++;
            try
            {
                if(!window!.IsVisible)throw new InvalidOperationException("Original window hidden; continuation refused.");
                if(sequenceIdentity.HasValue&&!RequestCurrent())throw new InvalidOperationException("Original request/epoch changed; continuation refused.");
                long renderingTicks=((RenderingEventArgs)args).RenderingTime.Ticks;
                var sample=journal.Add(Frame(renderingTicks));
                if(sample.QpcReversed||sample.RenderingTimeReversed||!sample.Frame.Applying&&depth==1&&!sample.StableGeometryValid)
                    throw new InvalidOperationException("Stable geometry or monotonic frame evidence failed at "+sample.Sequence);
                bool finished=phase?.Finished==true;
                bool completed=phase?.Observe(sample)==true;
                bool failed=!finished&&phase?.FailedQpc!=null;
                if(failed)Lifetime.Record("Stage:"+mode+":"+phase!.Name,new InvalidOperationException(phase.FailureReason));
                if(mode=="Raster"&&sample.Eligible&&!capturing&&phase is {} current)
                {
                    if(rasterBudget.ClaimIntermediate(current,sample))Capture(sample,"intermediate");
                    if(completed&&current.Goal==OverlayProbeGoal.Settled&&captured.Add((current.Id,sample.Frame.AnimationEpoch,"terminal")))Capture(sample,"terminal");
                    if(failed&&captured.Add((current.Id,sample.Frame.AnimationEpoch,"failed-terminal")))Capture(sample,"failed-terminal");
                }
                if(completed||failed)phaseDone?.TrySetResult();
                if(sample.Eligible){var previous=nextFrame;nextFrame=new(TaskCreationOptions.RunContinuationsAsynchronously);previous.TrySetResult();}
            }
            catch(Exception error)
            {
                Lifetime.Record("Rendering",error);
                try{cancellation.Cancel();}catch(Exception cancel){Lifetime.Record("RenderingCancel",cancel);}
            }
            finally{depth--;}
        }
        private void Capture(OverlayProbeSample sample,string kind)
        {
            capturing=true;
            try
            {
                var content=(FrameworkElement)window!.Content;var dpi=VisualTreeHelper.GetDpi(content);
                int width=checked((int)Math.Ceiling(content.ActualWidth*dpi.DpiScaleX)),height=checked((int)Math.Ceiling(content.ActualHeight*dpi.DpiScaleY));
                rasterBudget.Reserve(width,height);
                var bitmap=new RenderTargetBitmap(width,height,96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);
                bitmap.Render(content);bitmap.Freeze();var after=Frame(sample.Frame.RenderingTicks);
                bool stable=after.Card==sample.Frame.Card&&after.Native==sample.Frame.Native&&after.Anchor==sample.Frame.Anchor&&after.Hwnd==sample.Frame.Hwnd&&
                    after.AnimationEpoch==sample.Frame.AnimationEpoch&&after.Phase==sample.Frame.Phase&&after.Mode==sample.Frame.Mode&&after.RequestCurrent&&after.MeasurementCurrent;
                rasters.Add(new(bitmap,sample,after,kind,stable));
                if(!stable)throw new InvalidOperationException("Geometry, epoch or current request changed during actual raster capture; frame retained as invalid.");
            }
            finally{capturing=false;}
        }
        internal async Task<bool> CloseOriginal()
        {
            phase=null;mode="Exit";
            if(observing){CompositionTarget.Rendering-=Observe;observing=false;}
            if(constructionUnknown)return false;
            if(window!=null)
            {
                window.PresentationMeasureObservedForTest=null;
                bool requestOk=true;
                if(closeRequested)throw new InvalidOperationException("A second close request is forbidden.");
                closeRequested=true;
                try{window.Close();}catch(Exception error){requestOk=false;Lifetime.Record("CloseRequest",error);}
                originalClose=window.PresentationCloseOperation; // Preserve this exact task, once.
                if(originalClose==null){Lifetime.Record("CloseTaskMissing",new InvalidOperationException("Original close task missing."));return false;}
                bool joined=await Lifetime.JoinClose(originalClose,closed.Task);
                if(!requestOk||!joined)return false;
                window=null;body=null;
            }
            if(draft!=null)
            {
                if(draftRetirementStarted)return false;
                draftRetirementStarted=true;draft.Dispose();draft=null;
            }
            return true;
        }
        internal void ClearState(){if(window!=null||draft!=null||constructionUnknown)throw new InvalidOperationException("Original resources retained.");Directory.Delete(state,true);}
        internal async Task FinalizeOriginal()
        {
            await Lifetime.Original;deadline?.Stop();
            try{WriteEvidence();}catch(Exception error){Lifetime.Record("Evidence",error);}
            Succeeded=Lifetime.CleanupConfirmed&&!timedOut&&Lifetime.Failures.Count==0&&phases.Count==18&&phases.All(p=>p.CompletedQpc.HasValue)&&CompleteRasterCollection();
            try{WriteJson("overlay-stages-result.json",Result());}catch(Exception error){Succeeded=false;Lifetime.Record("ResultWrite",error);Console.Error.WriteLine(error);}
            if(!Lifetime.CleanupConfirmed)await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            cancellation.Dispose();Shutdown=app.Dispatcher.BeginInvoke(()=>app.Shutdown(Succeeded?0:3));
        }
        private object Result()=>new{Kind="ActualProductionOverlayStages",SourceHead=Environment.GetEnvironmentVariable("GITHUB_SHA"),
            Status=Succeeded?"Pass_LimitedStages":"FailOrUnconfirmed",TimedOut=timedOut,ElapsedMilliseconds=elapsed.Elapsed.TotalMilliseconds,
            QpcFrequency=Stopwatch.Frequency,OriginalDriverCompleted=Lifetime.Original.IsCompleted,OriginalCloseTaskCompleted=originalClose?.IsCompleted,
            ClosedObserved=closed.Task.IsCompletedSuccessfully,Lifetime.CloseConfirmed,Lifetime.CleanupConfirmed,Errors=Lifetime.Failures,
            Environment=environment,Phases=phases,RawCallbacks=journal.Samples.Count,DuplicateCallbacks=journal.Samples.Count(s=>s.DuplicateRenderingTime),
            Measurements=journal.Measurements,journal.MeasurementOverflow,
            ApplyingCallbacks=journal.Samples.Count(s=>s.Frame.Applying),RasterReservedBytes=rasterBudget.Bytes,EvidenceFiles=files,RejectedRasterFiles=rejectedRasterFiles,
            Fixture="Synthetic neutral text; original programmatic Button.Click, not physical input",ModelCalls=0,ProductionPins="Unavailable",
            PerformanceAcceptance="NotEvaluated_RefreshUnknown_SeparateRasterPass",NotEvaluated=new[]{"B07 fifty cycles","DWM presentation","Physical keyboard/input latency","Mixed system DPI","All corners/monitor changes","Full product/installation"}};
        private void WriteEvidence()
        {
            WriteJson("overlay-raw-frames.json",journal.Samples);
            long total=0;
            for(int i=0;i<rasters.Count;i++)
            {
                var r=rasters[i];var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(r.Bitmap));
                using var memory=new MemoryStream();encoder.Save(memory);
                if(memory.Length>768*1024||memory.Length>8*1024*1024-total)throw new IOException("Encoded raster bound exceeded.");
                byte[] bytes=memory.ToArray();string name=$"overlay-{i+1:00}-phase{r.Before.Frame.Phase}-{r.Kind}.png";WriteBytes(name,bytes);total+=bytes.Length;
                var owner=phases.Single(p=>p.Id==r.Before.Frame.Phase);
                bool qualified=r.Stable&&OverlayRasterBudget.MatchesIssuedEpoch(owner,r.Before,r.After);
                var entry=new{File=name,Bytes=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                    r.Bitmap.PixelWidth,r.Bitmap.PixelHeight,Before=r.Before,After=r.After,r.Kind,r.Stable,
                    RejectionReason=qualified?null:"CaptureChangedOrIssuedEpochMismatch",Scope="Actual WPF content raster; not DWM presented frame"};
                (qualified?files:rejectedRasterFiles).Add(entry);
            }
        }
        private bool CompleteRasterCollection()
        {
            var expected=phases.Where(p=>p.Mode=="Raster").SelectMany(p=>p.Goal==OverlayProbeGoal.Settled?
                new[]{(p.Id,"intermediate"),(p.Id,"terminal")}:new[]{(p.Id,"intermediate")}).ToHashSet();
            var actual=rasters.Where(r=>r.Stable&&OverlayRasterBudget.MatchesIssuedEpoch(phases.Single(p=>p.Id==r.Before.Frame.Phase),r.Before,r.After))
                .Select(r=>(r.Before.Frame.Phase,r.Kind)).ToList();
            return actual.Count==expected.Count&&expected.SetEquals(actual);
        }
        private void WriteJson(string name,object value)
        {byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions{WriteIndented=true});if(bytes.Length>16*1024*1024)throw new IOException("Evidence JSON bound exceeded.");WriteBytes(name,bytes);}
        private void WriteBytes(string name,byte[] bytes)
        {using var stream=new FileStream(Path.Combine(output,name),FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes);stream.Flush(true);}
        private void WritePending()
        {try{WriteJson("overlay-deadline-pending.json",Result());}catch(Exception error){Lifetime.Record("DeadlineEvidence",error);}}
        internal void Emergency(Exception error)
        {Console.Error.WriteLine(error);try{Lifetime?.Record("DispatcherEnded",error);WriteJson("overlay-emergency.json",Result());}catch(Exception evidence){Console.Error.WriteLine(evidence);}}
        private sealed record RasterFrame(RenderTargetBitmap Bitmap,OverlayProbeSample Before,OverlayProbeFrame After,string Kind,bool Stable);
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root)where T:DependencyObject
    {if(root is T match)yield return match;for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)foreach(var child in Descendants<T>(VisualTreeHelper.GetChild(root,i)))yield return child;}
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoKeys:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> plain)=>throw new InvalidOperationException("Fixture has no keys.");public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>throw new InvalidOperationException("Fixture has no keys.");}
}
