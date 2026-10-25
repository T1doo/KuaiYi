using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using KuaiYi.Windows;

namespace KuaiYi.GrowthHarness;

// Actual production windows, synthetic content. This is NOT an offscreen/side-effect-free runner.
// No workflow invokes this entry. Its separately approved Windows window remains required.
internal static partial class UiPolishProbe
{
    internal static int Run(UiPolishRequest request)
    {
        var product=typeof(ShellWindow).Assembly;
        UiPolishContract.RequireProductVersion(request,product.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
        string output=Path.GetFullPath(request.Output);
        if(!Directory.Exists(output)||Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Existing empty exclusive output required.");
        for(DirectoryInfo? p=new(output);p!=null;p=p.Parent)
            if((p.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked output rejected.");
        using(var claim=new FileStream(Path.Combine(output,"probe-claim.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None))
        {byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(new{request.Side,request.View,request.ProductHead,ProcessId=System.Environment.ProcessId});claim.Write(bytes);claim.Flush(true);}
        string state=Path.Combine(output,"state");if(Directory.Exists(state))throw new IOException("Fixture state already exists; retained.");Directory.CreateDirectory(state);
        // Dedicated probe process only; no previous/default system proxy is inspected.
        System.Net.Http.HttpClient.DefaultProxy=new System.Net.WebProxy();
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var previous=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        var owner=new Owner(app,request,output,state,Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(product.Location))).ToLowerInvariant());
        try
        {
            owner.Lifetime=new(owner.Drive,owner.CloseOriginal,owner.SettleResources);
            Task finalization=owner.FinalizeOriginal();
            var release=app.Dispatcher.BeginInvoke(owner.Lifetime.Release);
            app.Run();
            if(!owner.Lifetime.Original.IsCompleted||!finalization.IsCompleted)throw new InvalidOperationException("Dispatcher ended before original settlement; retained UNKNOWN.");
            release.Task.GetAwaiter().GetResult();owner.Lifetime.Original.GetAwaiter().GetResult();finalization.GetAwaiter().GetResult();
            owner.Shutdown!.Task.GetAwaiter().GetResult();return owner.Succeeded?0:3;
        }
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }
    private sealed partial class Owner(Application app,UiPolishRequest request,string output,string state,string productHash)
    {
        private readonly Stopwatch clock=Stopwatch.StartNew();
        private readonly CancellationTokenSource cancellation=new();
        private readonly List<object> scenes=[];
        private readonly List<object> retirements=[];
        private readonly List<object> openingFrames=[];
        private readonly List<object> fadeFrames=[];
        private readonly List<object> choiceChecks=[];
        private Window? window;
        private SettingsDraft? draft;
        private TaskCompletionSource? closed;
        private Task<bool>? retirement;
        private UiPolishTextFixture? fixture;
        private bool attached,constructionUnknown,timedOut;
        private long pngBytes;
        private DispatcherTimer? deadline;
        internal OverlayProbeLifetime Lifetime{get;set;}=null!;
        internal DispatcherOperation? Shutdown{get;private set;}
        internal bool Succeeded{get;private set;}
        internal async Task Drive()
        {
            deadline=new(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(100)};
            deadline.Tick+=(_,_)=>
            {
                if(clock.Elapsed<TimeSpan.FromSeconds(45))return;
                deadline.Stop();timedOut=true;
                try{cancellation.Cancel();}catch(Exception error){Lifetime.Record("Cancel",error);}
                try{Write("deadline-unknown.json",new{Status="UNKNOWN",StateRetained=true,OriginalTasksRetained=true,Milliseconds=clock.Elapsed.TotalMilliseconds});}
                catch(Exception error){Lifetime.Record("DeadlineEvidence",error);}
            };
            deadline.Start();
            Require(Environment.UserInteractive,"Interactive desktop required.");
            Require(!SystemParameters.HighContrast,"High contrast is active; ordinary palette comparison NotRun. System setting unchanged.");
            foreach(var theme in new[]{ThemePreference.Light,ThemePreference.Dark})
                if(request.View=="settings")await Settings(theme);
                else foreach(var motion in new[]{MotionPreference.Full,MotionPreference.Reduce})
                    {await Overlay(theme,motion,false);await Overlay(theme,motion,true);}
        }
        private void Check(){cancellation.Token.ThrowIfCancellationRequested();if(clock.Elapsed>=TimeSpan.FromSeconds(45))throw new TimeoutException("Original 45 second visual driver budget exceeded.");}
        private SettingsCoordinator Coordinator(string name)
        {
            string root=Path.Combine(state,name);Directory.CreateDirectory(root);
            return new(new SettingsStore(Path.Combine(root,"config")),new SecretFileStore(Path.Combine(root,"secrets"),new NoKeys(),new DirectoryPolicy()));
        }
        private void BeginOpen()
        {Check();Require(window==null&&draft==null&&fixture==null&&!constructionUnknown,"Previous original owner not settled.");retirement=null;}
        private void Own(Window original)
        {
            window=original;constructionUnknown=false;
            closed=new(TaskCreationOptions.RunContinuationsAsynchronously);
            var originalClosed=closed;original.Closed+=(_,_)=>originalClosed.TrySetResult();
        }
        private async Task Settings(ThemePreference theme)
        {
            BeginOpen();var coordinator=Coordinator("settings-"+theme);
            draft=await coordinator.OpenDraftAsync(cancellation.Token);
            draft.Update(s=>s with{Overlay=s.Overlay with{Theme=theme}});
            constructionUnknown=true;Own(new SettingsWindow(coordinator,draft));window!.Show();await Next();
            var navigation=Descendants<ListBox>(window).Single(p=>AutomationProperties.GetName(p)=="设置分类");navigation.SelectedIndex=3;
            window.MinWidth=1;window.MinHeight=1;window.Width=940;window.Height=700;window.FontSize=13;
            await Next();Require(Math.Abs(window.ActualWidth-940)<1&&Math.Abs(window.ActualHeight-700)<1,"Requested wide viewport unavailable; no DPI/system change attempted.");Capture(theme+"-wide",theme,null,"Default",940,700);
            window.Width=420;window.Height=680;await Next();Require(Math.Abs(window.ActualWidth-420)<1&&Math.Abs(window.ActualHeight-680)<1,"Requested narrow viewport unavailable.");
            var input=Descendants<PreferencePanel>(window).Where(p=>p.IsVisible).Select(p=>(Control)p.Children[1]).First(p=>p.Focusable&&p.IsEnabled);
            await Focus(input);Capture(theme+"-narrow-input-focus",theme,null,"ProgrammaticInputFocus",420,680,input);
            var button=Descendants<Button>(window).Single(p=>AutomationProperties.GetName(p)=="取消设置更改");
            await Focus(button);Capture(theme+"-narrow-button-focus",theme,null,"ProgrammaticButtonFocus",420,680,button);
            object local=input.ReadLocalValue(UIElement.IsEnabledProperty);
            try{input.IsEnabled=false;input.BringIntoView();await Next();Require(!input.IsEnabled,"Original input not disabled.");Capture(theme+"-narrow-input-disabled",theme,null,"OriginalInputDisabled",420,680,input);}
            finally{if(local==DependencyProperty.UnsetValue)input.ClearValue(UIElement.IsEnabledProperty);else input.SetValue(UIElement.IsEnabledProperty,local);}
            window.Width=940;window.Height=700;await Next();await ChoiceScenes(theme);
            Require(await CloseOriginal(),"Settings original close unconfirmed.");
        }
        private async Task Overlay(ThemePreference theme,MotionPreference motion,bool stopped)
        {
            BeginOpen();var coordinator=Coordinator("overlay-"+theme+"-"+motion+(stopped?"-stopped":"-complete"));
            draft=await coordinator.OpenDraftAsync(cancellation.Token);draft.Update(_=>UiPolishTextFixture.Configuration(theme,motion));
            var saved=await coordinator.SaveAsync(draft,cancellation:cancellation.Token);draft.Dispose();draft=null;
            constructionUnknown=true;Own(new ShellWindow("KuaiYi.UiPolish."+Guid.NewGuid().ToString("N"),coordinator,enableSelectionPlatform:false));
            var shell=(ShellWindow)window!;shell.Show();await shell.PresentationStartup;await Next();
            var gate=shell.PresentationGate;var origin=new EndpointOrigin(new Uri(UiPolishTextFixture.Endpoint));gate.ConsentTo(origin);
            Require(gate.TryEnable(origin,true),"Original synthetic request gate refused.");
            var results=Field<OverlayResults>(shell,"overlayResults");
            var flowField=ExactField("translation",typeof(TextTranslationCoordinator));Require(flowField.GetValue(shell)==null,"Original translation field already owned.");
            var method=typeof(ShellWindow).GetMethod("TranslationChanged",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly,null,[typeof(TextTranslationUpdate)],null);
            Require(method!=null&&method.ReturnType==typeof(void),"Original translation callback signature differs.");
            var callback=method!.CreateDelegate<Action<TextTranslationUpdate>>(shell);
            fixture=new(gate,results,saved,SynchronizationContext.Current!,callback);
            flowField.SetValue(shell,fixture.Flow);attached=true;
            var id=await fixture.Start(cancellation.Token);
            string name=theme+"-"+motion;
            // Begin and source snapshot are created only by the original coordinator.
            if(!stopped&&motion==MotionPreference.Full)
                await Intermediate(shell,id,theme+"-"+motion+"-growth",theme,motion,()=>Send(shell,id,new(OverlayResultEventKind.Delta,UiPolishTextFixture.ResultText)));
            else await Send(shell,id,new(OverlayResultEventKind.Delta,UiPolishTextFixture.ResultText));
            var stop=Descendants<Button>(shell).Single(p=>Equals(p.Tag,"停止"));
            if(stopped)
            {
                await Settled(shell);Require(stop.IsEnabled,"Original Stop command disabled during running generation.");
                stop.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));await fixture.CancelAndJoin(id);await Settled(shell);
                RequireLabel(shell,"已停止");Capture(name+"-stopped",theme,motion,"StoppedByOriginalCommand",480,null);
            }
            else
            {
                await Settled(shell);RequireLabel(shell,"生成中");Capture(name+"-generating",theme,motion,"Generating",480,null);
                Require(results.ObserveUsage(id,24,18),"Original usage admission refused.");await Send(shell,id,new(OverlayResultEventKind.Usage));
                await Send(shell,id,new(OverlayResultEventKind.Complete));await Send(shell,id,new(OverlayResultEventKind.Usage));
                await fixture.CancelAndJoin(id);await Settled(shell);
                Require(results.Versions.Single() is {Complete:true,Stopped:false},"Synthetic completed version changed during original cancellation.");
                RequireLabel(shell,"已完成");Capture(name+"-complete",theme,motion,"Complete",480,null);
                // Observe the original button's actual disabled visual without invoking its command.
                object local=stop.ReadLocalValue(UIElement.IsEnabledProperty);
                try{stop.IsEnabled=false;await Next();Require(!stop.IsEnabled,"Original Stop control not disabled.");Capture(name+"-stop-disabled",theme,motion,"OriginalStopForcedDisabled",480,null,stop);}
                finally{if(local==DependencyProperty.UnsetValue)stop.ClearValue(UIElement.IsEnabledProperty);else stop.SetValue(UIElement.IsEnabledProperty,local);}
            }
            Require(await CloseOriginal(),"Shell original close unconfirmed.");
        }
        private async Task Send(ShellWindow shell,RequestIdentity id,OverlayResultEvent update)
        {Check();Require(await shell.ReceivePresentationAsync(id,update,cancellation.Token),"Original receiver refused "+update.Kind);}
        private void RequireLabel(ShellWindow shell,string status)
        {
            var label=Field<TextBlock>(shell,"resultLabel");
            var version=Field<OverlayResults>(shell,"overlayResults").Versions.Single();
            Require(status=="生成中"?!version.Complete&&!version.Stopped:status=="已完成"?version.Complete&&!version.Stopped:version.Stopped&&!version.Complete,"Original version state differs.");
            string expected=request.Side=="before"?status=="生成中"?"生成版本 1":status=="已完成"?"版本 1":"已停止":status;
            Require(label.Text.Contains(expected,StringComparison.Ordinal)&&label.Text.Contains("选区",StringComparison.Ordinal)&&label.Text.Contains(UiPolishTextFixture.Model,StringComparison.Ordinal),"Original snapshot-derived label differs.");
            Require(label.IsVisible&&label.Opacity>0,"Original source label not visible.");
        }
        private async Task Intermediate(ShellWindow shell,RequestIdentity id,string name,ThemePreference theme,MotionPreference motion,Func<Task> issue)
        {
            long baseline=shell.PresentationAnimationEpoch;bool capturing=false,deltaAccepted=false,geometrySaved=false,fadeSaved=false;
            var evidence=new UiPolishOpeningEvidence(baseline);
            var completed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var targetField=typeof(OverlayFrames).GetField("target",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);
            Require(targetField?.FieldType==typeof(PixelRect),"Exact original frame target observation unavailable.");
            EventHandler observe=(_,_)=>
            {
                if(capturing||completed.Task.IsCompleted)return;
                try
                {
                    Check();Require(shell.PresentationGate.CanApply(id)&&Field<OverlayResults>(shell,"overlayResults").Identity==id,"Original opening request changed.");
                    var g=shell.PresentationGeometry??throw new InvalidOperationException("Original geometry missing.");
                    long current=shell.PresentationAnimationEpoch;
                    if(!deltaAccepted)return;
                    var results=Field<OverlayResults>(shell,"overlayResults");var body=Descendants<RichTextBox>(shell).Single();
                    bool bodyReady=results.Text==UiPolishTextFixture.ResultText&&results.SelectedVersionId==id.VersionId&&
                        new TextRange(body.Document.ContentStart,body.Document.ContentEnd).Text.Contains(UiPolishTextFixture.ResultText,StringComparison.Ordinal);
                    evidence.Observe(g,(PixelRect)targetField!.GetValue(g)!,current,bodyReady);
                    if(evidence.Fade!=null&&!fadeSaved){fadeFrames.Add(new{Request=id,Frame=evidence.Fade});fadeSaved=true;}
                    if(evidence.Geometry is {} frame&&!geometrySaved)
                    {
                        capturing=true;
                        try
                        {
                            Capture(name,theme,motion,"ActualGeometryIntermediate_FadeSeparatelyRequired",480,null);
                            Require(shell.PresentationAnimationEpoch==frame.Epoch&&g.Current==frame.Card&&((PixelRect)targetField.GetValue(g)!)==frame.Target&&shell.PresentationGate.CanApply(id),"Original geometry changed during capture.");
                            openingFrames.Add(new{Request=id,Frame=frame});geometrySaved=true;
                        }
                        finally{capturing=false;}
                    }
                    if(evidence.Complete&&geometrySaved)completed.TrySetResult();
                    else if(current>baseline&&g.Transition==OverlayTransition.Expanded&&!g.NeedsFrame)
                    {evidence.RequireComplete();throw new InvalidOperationException("Actual geometry raster missing; no fabricated frame.");}
                }
                catch(Exception error){completed.TrySetException(error);}
            };
            // Own the original observer before the delta can cause an opening/render callback.
            CompositionTarget.Rendering+=observe;
            using var cancelled=cancellation.Token.Register(()=>completed.TrySetCanceled(cancellation.Token));
            try{await issue();deltaAccepted=true;await completed.Task;Check();}
            finally{CompositionTarget.Rendering-=observe;}
        }
        private async Task Settled(ShellWindow shell)
        {
            do{await Next();}while(shell.PresentationGeometry is not {Transition:OverlayTransition.Expanded,NeedsFrame:false}||!BodyVisible(shell));
            await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check();
        }
        private static bool BodyVisible(ShellWindow shell)
        {var body=Descendants<RichTextBox>(shell).Single();return body.IsVisible&&body.ActualWidth>0&&body.ActualHeight>0&&body.Opacity>=1&&new TextRange(body.Document.ContentStart,body.Document.ContentEnd).Text.Contains(UiPolishTextFixture.ResultText,StringComparison.Ordinal);}
        private async Task Focus(Control control)
        {control.BringIntoView();await Next();Require(window!.Activate(),"Real window activation refused.");Keyboard.Focus(control);await Next();Require(control.IsKeyboardFocused&&ReferenceEquals(Keyboard.FocusedElement,control),"Actual programmatic focus not acquired.");}
        private async Task Next()
        {
            Check();await app.Dispatcher.InvokeAsync(()=>window!.UpdateLayout(),DispatcherPriority.ApplicationIdle);
            var next=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);EventHandler handler=(_,_)=>next.TrySetResult();
            CompositionTarget.Rendering+=handler;
            try{await next.Task.WaitAsync(cancellation.Token);}finally{CompositionTarget.Rendering-=handler;}
            Check();Require(window!.IsLoaded&&window.IsVisible&&window.IsMeasureValid&&window.IsArrangeValid,"Actual window layout not settled.");
        }
        private void Capture(string name,ThemePreference theme,MotionPreference? motion,string status,double width,double? height,Control? target=null)
        {
            Check();var root=(FrameworkElement)window!.Content;var dpi=VisualTreeHelper.GetDpi(root);
            int x=checked((int)Math.Ceiling(root.ActualWidth*dpi.DpiScaleX)),y=checked((int)Math.Ceiling(root.ActualHeight*dpi.DpiScaleY));
            Require(x>0&&y>0&&(long)x*y<=8_000_000&&scenes.Count<24,"Raster dimensions/count exceed bound.");
            var before=RasterState(target);Require(RasterQualified(before),"Original geometry/state not raster-qualified.");
            var bitmap=new RenderTargetBitmap(x,y,dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);bitmap.Render(root);bitmap.Freeze();
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var memory=new MemoryStream();encoder.Save(memory);
            Require(memory.Length<=2*1024*1024&&pngBytes+memory.Length<=16*1024*1024,"Raster evidence exceeds byte budget.");
            byte[] bytes=memory.ToArray();string file=name+".png";using(var stream=new FileStream(Path.Combine(output,file),FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}pngBytes+=bytes.Length;
            var after=RasterState(target);bool coherent=RasterQualified(after)&&before==after&&root.ActualWidth==before.ClientWidth&&root.ActualHeight==before.ClientHeight&&VisualTreeHelper.GetDpi(root).Equals(dpi);
            var shell=window as ShellWindow;var g=shell?.PresentationGeometry;
            PixelRect? native=null;if(NativeDesktop.GetWindowRect(new WindowInteropHelper(window).Handle,out var bounds))native=bounds.ToPixels();
            scenes.Add(new{Name=name,File=file,Bytes=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),request.Side,request.ProductHead,AppDllSha256=productHash,
                Theme=theme.ToString(),Motion=motion?.ToString(),Status=status,RequestedWidthDip=width,RequestedHeightDip=height,WindowWidthDip=window.ActualWidth,WindowHeightDip=window.ActualHeight,
                ClientWidthDip=root.ActualWidth,ClientHeightDip=root.ActualHeight,PixelsX=x,PixelsY=y,DpiX=dpi.PixelsPerInchX,DpiY=dpi.PixelsPerInchY,window.FontSize,ConfiguredBodyFontSizeDip=fixture?.Flow.SelectedSnapshot?.Settings.Data.Overlay.FontSizeDip,
                Target=target==null?null:new{Type=target.GetType().Name,Name=AutomationProperties.GetName(target),target.IsEnabled,target.IsKeyboardFocused,target.IsKeyboardFocusWithin,target.IsMouseOver},
                Label=shell==null?null:Field<TextBlock>(shell,"resultLabel").Text,Tooltip=shell==null?null:Field<TextBlock>(shell,"resultLabel").ToolTip,
                Geometry=g==null?null:new{g.Current,g.Anchor,Native=native,g.Transition,g.NeedsFrame,g.ContentOpacity,g.ContentDirection,g.Monitor,
                    Epoch=shell!.PresentationAnimationEpoch,StartedAt=shell.PresentationAnimationStartedAt,LastTick=shell.PresentationLastTick},
                BeforeRaster=before,AfterRaster=after,StateCoherent=coherent,
                Qpc=Stopwatch.GetTimestamp(),Milliseconds=clock.Elapsed.TotalMilliseconds,SyntheticSource=true,PhysicalInput=false});
            Require(coherent,"Original state changed during raster; PNG retained as invalid, never qualified.");
        }
        private sealed record RasterIdentity(long Hwnd,PixelRect? Native,PixelRect? Card,PixelRect? Anchor,PixelRect? Work,PixelRect? Button,
            long? Epoch,RequestIdentity? Request,bool Applying,OverlayTransition? Transition,bool? NeedsFrame,double? ContentOpacity,
            double ClientWidth,double ClientHeight,int FocusIdentity,bool? TargetEnabled,bool? TargetFocused,bool? TargetHover,
            bool? BodyVisible,double? BodyWidth,double? BodyHeight,double? BodyOpacity,string? BodyHash,string? Label,double? LabelOpacity);
        private RasterIdentity RasterState(Control? target)
        {
            var root=(FrameworkElement)window!.Content;var shell=window as ShellWindow;var g=shell?.PresentationGeometry;
            nint hwnd=new WindowInteropHelper(window).Handle;PixelRect? native=NativeDesktop.GetWindowRect(hwnd,out var bounds)?bounds.ToPixels():null;
            Require(native!=null,"Original HWND bounds unavailable.");
            var body=shell==null?null:Descendants<RichTextBox>(shell).Single();var label=shell==null?null:Field<TextBlock>(shell,"resultLabel");
            string? text=body==null?null:new TextRange(body.Document.ContentStart,body.Document.ContentEnd).Text;
            return new((long)hwnd,native,g?.Current,g?.Anchor,g?.Monitor.WorkArea.Inset(g.Monitor.Dpi.CeilingPixels(12)),shell?.PresentationButtonScreenBounds,
                shell?.PresentationAnimationEpoch,shell==null?null:Field<OverlayResults>(shell,"overlayResults").Identity,
                shell?.PresentationFrameApplying??false,g?.Transition,g?.NeedsFrame,g?.ContentOpacity,root.ActualWidth,root.ActualHeight,
                Keyboard.FocusedElement==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Keyboard.FocusedElement),target?.IsEnabled,target?.IsKeyboardFocused,target?.IsMouseOver,
                body?.IsVisible,body?.ActualWidth,body?.ActualHeight,body?.Opacity,text==null?null:Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))),label?.Text,label?.Opacity);
        }
        private static bool RasterQualified(RasterIdentity value)=>!value.Applying&&value.Hwnd!=0&&value.Native!=null&&
            (value.Card is not {} card||value.Anchor is {} anchor&&value.Work is {} work&&card.Contains(anchor)&&work.Contains(card)&&value.Native==card&&value.Button==anchor);
        internal Task<bool> CloseOriginal()=>retirement??=RetireOriginal();
        private async Task<bool> RetireOriginal()
        {
            bool ok=!constructionUnknown;
            if(window is SettingsWindow settings)
            {
                try{if(!closed!.Task.IsCompleted)settings.RequestCloseWhenSettled();await closed.Task;window=null;closed=null;}
                catch(Exception error){ok=false;Lifetime.Record("SettingsClose",error);}
            }
            else if(window is ShellWindow shell)
            {
                var originalTray=Field<NativeTray>(shell,"tray");
                try
                {
                    try{if(!closed!.Task.IsCompleted)shell.Close();}
                    catch(Exception error){ok=false;Lifetime.Record("ShellCloseRequest",error);}
                    Task original=shell.PresentationCloseOperation??Task.FromException(new InvalidOperationException("Original Shell close task missing; retain original Closed waiter."));
                    bool joined=await Lifetime.JoinClose(original,closed!.Task);ok&=joined;
                    if(joined)
                    {
                        bool disposed=TrayFlag(originalTray,"disposed"),added=TrayFlag(originalTray,"added");
                        retirements.Add(new{OwnWindowClosed=true,TrayDisposed=disposed,TrayRemoved=!added});
                        Require(disposed&&!added,"Original tray removal unconfirmed; owner retained.");
                        window=null;closed=null;
                    }
                }
                catch(Exception error){ok=false;Lifetime.Record("ShellClose",error);}
            }
            if(fixture!=null)
                try
                {
                    if(attached){if(window!=null||!ok)throw new InvalidOperationException("Attached fixture still owned by unconfirmed Shell close.");fixture.DisposeProviderAfterOriginalClose();}
                    else await fixture.DisposeAsync();
                    retirements.Add(new{Runs=fixture.OriginalRuns.Count,Connectors=fixture.OriginalConnectors.Count,Attempts=fixture.Attempts,AllOriginalTasksCompleted=fixture.OriginalRuns.Concat(fixture.OriginalConnectors).All(t=>t.IsCompleted)});
                    fixture=null;attached=false;
                }
                catch(Exception error){ok=false;Lifetime.Record("FixtureSettlement",error);}
            if(window==null&&!constructionUnknown)
                try{draft?.Dispose();draft=null;}catch(Exception error){ok=false;Lifetime.Record("DraftSettlement",error);}
            return ok&&window==null&&fixture==null&&draft==null;
        }
        internal void SettleResources(){Require(window==null&&fixture==null&&draft==null&&!constructionUnknown,"Original resources unconfirmed.");cancellation.Dispose();}
        internal async Task FinalizeOriginal()
        {
            await Lifetime.Original;deadline?.Stop();
            Succeeded=Lifetime.Failures.Count==0&&!timedOut&&Lifetime.CloseConfirmed&&Lifetime.CleanupConfirmed&&scenes.Count==(request.View=="settings"?16:18);
            try{Write("ui-polish-result.json",new{Status=Succeeded?"Pass_LimitedWpfRaster":"Fail",request.Side,request.View,request.ProductHead,AppDllSha256=productHash,
                TimedOut=timedOut,Lifetime.CloseConfirmed,ResourcesSettled=Lifetime.CleanupConfirmed,StateRetained=true,Scenes=scenes,ChoiceChecks=choiceChecks,OpeningFrames=openingFrames,FadeFrames=fadeFrames,Retirements=retirements,Lifetime.Failures,
                Environment=new{System.Environment.OSVersion.VersionString,System.Environment.UserInteractive,SessionId=Process.GetCurrentProcess().SessionId,RenderTier=RenderCapability.Tier>>16,
                    SystemParameters.HighContrast,SystemParameters.ClientAreaAnimation},
                Scope="Actual production WPF content raster; programmatic controls and public synthetic coordinator snapshots; no model response",
                SideEffects="Original Settings/Shell HWND, foreground/focus where requested, Shell tray and session subscription; own fixture config writes. No system DPI/theme/motion changes.",
                NotEvaluated=new[]{"DWM display or fluidity","physical keyboard/pointer","system theme/DPI/motion changes","image/OCR/development-source labels","real model or native registration","production pins"}});}
            catch(Exception error){Succeeded=false;Lifetime.Record("FinalEvidence",error);Console.Error.WriteLine(error);}
            if(!Lifetime.CloseConfirmed||!Lifetime.CleanupConfirmed)await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
            Shutdown=app.Dispatcher.BeginInvoke(()=>app.Shutdown(Succeeded?0:3));
        }
        private void Write(string name,object value)
        {byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions{WriteIndented=true});Require(bytes.Length<=512*1024,"JSON evidence exceeds bound.");using var stream=new FileStream(Path.Combine(output,name),FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes);stream.Flush(true);}
    }
    private static bool TrayFlag(NativeTray tray,string name)
    {var field=typeof(NativeTray).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);Require(field?.FieldType==typeof(bool),"Original tray observation signature differs.");return (bool)field!.GetValue(tray)!;}
    private static FieldInfo ExactField(string name,Type type)
    {var field=typeof(ShellWindow).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.DeclaredOnly);Require(field!=null&&field.FieldType==type,"Original field signature differs: "+name);return field!;}
    private static T Field<T>(ShellWindow shell,string name)=>(T)(ExactField(name,typeof(T)).GetValue(shell)??throw new InvalidOperationException("Original field null: "+name));
    private static void Require(bool value,string message){if(!value)throw new InvalidOperationException(message);}
    private static IEnumerable<T> Descendants<T>(DependencyObject parent)where T:DependencyObject
    {for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++){var child=VisualTreeHelper.GetChild(parent,i);if(child is T match)yield return match;foreach(var descendant in Descendants<T>(child))yield return descendant;}}
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoKeys:ISecretProtector
    {public byte[] Protect(ReadOnlySpan<byte> plain)=>throw new InvalidOperationException("No fixture secrets allowed.");public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>throw new InvalidOperationException("No fixture secrets allowed.");}
}
