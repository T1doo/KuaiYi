using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Reflection;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.GrowthHarness;
/// <summary>Test tool only: drives the exact shipped HWND/view with public text and a bound, synthetic, no-network request.</summary>
internal static class ActualOverlayProbe
{
    private const string PublicText="快译公开浮窗验证。没有读取其他应用，没有请求模型。\n\n同一个按钮是整张卡片的锚点；文字按最终宽度排版，窗口从按钮生长，跨DPI拖动后松手保存。\n\n";
    public static int Run(string output,string rate,bool boundedCi=false,bool markdownReview=false,bool interactiveInspection=false,
        ThemePreference inspectionTheme=ThemePreference.Light,MotionPreference inspectionMotion=MotionPreference.Full,double inspectionTextScale=1,bool captureOpening=false)
    {
        bool burst=rate=="burst";double perSecond=0;
        if(!burst&&(!double.TryParse(rate,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out perSecond)||!double.IsFinite(perSecond)||perSecond is <1 or >10000))return 2;
        output=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        if(new[]{output,output+".summary.json",output+".phases.log",output+".keyboard.jsonl"}.Any(File.Exists))throw new IOException("Existing probe evidence must not be overwritten.");
        var motion=interactiveInspection&&captureOpening?new OpeningMotionCapture(output):null;
        var phaseClock=Stopwatch.StartNew();
        void Phase(string value)
        {
            if(!boundedCi)return;
            string line=FormattableString.Invariant($"{phaseClock.Elapsed.TotalMilliseconds:F3} {value}");
            File.AppendAllText(output+".phases.log",line+Environment.NewLine);Console.Error.WriteLine("KuaiYi public overlay phase: "+line);
        }
        Phase("RunEntered");
        if(interactiveInspection)System.Net.Http.HttpClient.DefaultProxy=new System.Net.WebProxy();
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-overlay-probe-"+Guid.NewGuid().ToString("N"));
        var coordinator=new SettingsCoordinator(new SettingsStore(Path.Combine(root,"config")),new SecretFileStore(Path.Combine(root,"secrets"),new NoKeys(),new DirectoryPolicy()));
        SettingsSnapshot? inspectionSettings=null;UiPolishTextFixture? inspectionFixture=null;
        Phase("CoordinatorCreated");
        using(var draft=coordinator.OpenDraftAsync().GetAwaiter().GetResult())
        {
            Phase("DraftOpened");
            var visual=UiPolishTextFixture.Configuration(inspectionTheme,inspectionMotion);
            draft.Update(s=>interactiveInspection?visual with{Overlay=visual.Overlay with{FontSizeDip=15,PreferredWidthDip=420,RememberPosition=false}}:s with{ActiveProfileId="public",Profiles=[new(){Id="public",Model="public-fixture",BaseUrl="https://public.invalid",Authentication=ApiAuthentication.None}]});
            inspectionSettings=coordinator.SaveAsync(draft).GetAwaiter().GetResult();
        }
        Phase("PublicSettingsSaved");
        var app=new Application();var window=new ShellWindow("KuaiYi.PublicOverlayProbe."+Guid.NewGuid().ToString("N"),coordinator,enableSelectionPlatform:false){Title="快译 · 公开测试浮窗（非模型）"};
        Phase("ProductionWindowConstructed");
        // Explicit inspection only: expose the same HWND to desktop tools that filter TOOLWINDOWs.
        // Native nonactivation/AltTab acceptance belongs to the unchanged bounded probe, never this mode.
        if(interactiveInspection)
        {
            window.Loaded+=(_,_)=>
            {
                DesktopFrontendReview.InstallInspectionWindow(window,output);
                if(inspectionTextScale!=1)
                {
                    window.SetValue(DesktopTextScale.FactorProperty,inspectionTextScale);
                    typeof(ShellWindow).GetMethod("RefreshPresentationTheme",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                }
            };
            window.Deactivated+=(_,_)=>DesktopFrontendReview.ExposeInspectionWindow(window);
        }
        window.SourceInitialized+=(_,_)=>Phase("SourceInitialized");window.Closing+=(_,_)=>Phase("Closing");window.Closed+=(_,_)=>Phase("Closed");
        app.DispatcherUnhandledException+=(_,e)=>Phase("DispatcherUnhandled:"+e.Exception.GetType().Name);
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(interactiveInspection?650:50)};RequestIdentity? current=null;int offset=0;bool busy=false;double budget=0;long feedTime=Stopwatch.GetTimestamp();
        string stream=interactiveInspection?"公开预览 · 零模型请求\n\n雨已经停了，河边的小路很安静。\n我慢慢走向车站。\n空气里飘着刚出炉面包的香味。":markdownReview?MarkdownReviewText.Text:string.Concat(Enumerable.Repeat(PublicText,24));var errors=new List<string>();
        bool completed=false,timedOut=false;long completedAt=0;
        var deadline=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};long startedAt=Stopwatch.GetTimestamp();
        var checks=new Dictionary<string,bool>{{"AnchorContained",true},{"WorkContained",true},{"NativeMatches",true},{"ActualAnchorContained",true},{"ActualWorkContained",true},{"ForegroundUnchanged",true},{"ActualButtonMatchesAnchor",true},{"AnchorStable",true},{"Grew",false}};
        PixelRect? firstAnchor=null;
        async Task Start()
        {
            Phase("StartupAwait");await window.PresentationStartup;Phase("StartupReady");var gate=window.PresentationGate;var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);
            if(interactiveInspection)
            {
                if(!gate.TryEnable(origin,true))throw new InvalidOperationException("Public inspection gate refused.");
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                var results=(OverlayResults)typeof(ShellWindow).GetField("overlayResults",flags)!.GetValue(window)!;
                var callback=typeof(ShellWindow).GetMethod("TranslationChanged",flags)!.CreateDelegate<Action<TextTranslationUpdate>>(window);
                var flow=typeof(ShellWindow).GetField("translation",flags)!;
                if(flow.GetValue(window)!=null)throw new InvalidOperationException("Existing production coordinator must not be replaced.");
                inspectionFixture=new(gate,results,inspectionSettings!,SynchronizationContext.Current!,callback,
                    "The rain had stopped. The riverside path was quiet.\nI walked slowly toward the station.\nThe air smelled of freshly baked bread.");
                flow.SetValue(window,inspectionFixture.Flow);current=await inspectionFixture.Start(CancellationToken.None);
                offset=0;budget=0;feedTime=Stopwatch.GetTimestamp();timer.Start();return;
            }
            if(!gate.TryEnable(origin,true)||!gate.TryGetProfileBinding("public",out var binding)||!gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _)){errors.Add("PublicRequestRejected");return;}
            current=id;offset=0;budget=0;
            if(!await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Begin))){errors.Add("BeginRejected");return;}
            Phase("BeginAccepted");feedTime=Stopwatch.GetTimestamp();timer.Start();
        }
        timer.Tick+=async(_,_)=>
        {
            if(busy||current is not {} id)return;busy=true;
            timer.Interval=TimeSpan.FromMilliseconds(50);
            try
            {
                long now=Stopwatch.GetTimestamp();budget+=burst?2000:perSecond*Stopwatch.GetElapsedTime(feedTime,now).TotalSeconds;feedTime=now;int count=Math.Min((int)Math.Min(budget,stream.Length-offset),stream.Length-offset);budget-=count;if(count>0){bool accepted=await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,stream.Substring(offset,count)));if(!accepted){timer.Stop();errors.Add("DeltaRejected");return;}offset+=count;}
                if(offset==stream.Length)
                {
                    timer.Stop();completed=await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Complete));if(!completed)errors.Add("CompleteRejected");
                    if(inspectionFixture!=null)
                    {
                        await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Usage));
                        // Commit the original queued presentation before retiring
                        // this fixture's held transport, even for a one-chunk sample.
                        await app.Dispatcher.InvokeAsync(()=>typeof(ShellWindow).GetMethod("FlushResult",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null),DispatcherPriority.Background);
                        await inspectionFixture.CancelAndJoin(id);
                        await app.Dispatcher.InvokeAsync(()=>
                        {
                            typeof(ShellWindow).GetMethod("Refresh",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,null);
                            typeof(ShellWindow).GetMethod("ApplyOverlayFrame",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(window,[false]);
                        },DispatcherPriority.Background);
                    }
                    completedAt=Stopwatch.GetTimestamp();Phase(completed?"StreamCompleted":"CompleteRejected");
                }
            }
            catch(Exception error){timer.Stop();errors.Add(error.GetType().Name);}
            finally{busy=false;}
        };
        window.Loaded+=async(_,_)=>{try{await Start();}catch(Exception error){errors.Add(error.GetType().Name);}};
        window.PreviewKeyDown+=async(_,e)=>{if(interactiveInspection||e.Key!=Key.F5)return;e.Handled=true;try{await Start();}catch(Exception error){errors.Add(error.GetType().Name);}};
        window.Closed+=(_,_)=>{timer.Stop();deadline.Stop();};
        deadline.Tick+=(_,_)=>
        {
            if(errors.Count>0||(completed&&Stopwatch.GetElapsedTime(completedAt).TotalMilliseconds>=1200)){Phase("CloseRequested");deadline.Stop();window.Close();return;}
            if(Stopwatch.GetElapsedTime(startedAt).TotalSeconds>=20){timedOut=true;errors.Add("ProbeDeadlineExceeded");Phase("DeadlineCloseRequested");deadline.Stop();window.Close();}
        };
        if(boundedCi)deadline.Start();
        var frames=new Queue<string>();int discarded=0;nint initialForeground=KuaiYi.Windows.NativeDesktop.GetForegroundWindow();var clock=Stopwatch.StartNew();
        EventHandler observe=(_,_)=>
        {
            if(!window.IsVisible)return;
            if(window.PresentationGeometry is not {} state)return;
            nint hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;bool read=KuaiYi.Windows.NativeDesktop.GetWindowRect(hwnd,out var actual);var work=state.Monitor.WorkArea.Inset(state.Monitor.Dpi.CeilingPixels(12));
            var actualButton=window.PresentationButtonScreenBounds;
            firstAnchor??=state.Anchor;
            checks["AnchorContained"]&=state.Current.Contains(state.Anchor);checks["WorkContained"]&=work.Contains(state.Current);
            checks["NativeMatches"]&=read&&actual.ToPixels()==state.Current;
            checks["ActualAnchorContained"]&=read&&actual.ToPixels().Contains(state.Anchor);checks["ActualWorkContained"]&=read&&work.Contains(actual.ToPixels());
            checks["ForegroundUnchanged"]&=KuaiYi.Windows.NativeDesktop.GetForegroundWindow()==initialForeground;
            checks["ActualButtonMatchesAnchor"]&=actualButton==state.Anchor;checks["AnchorStable"]&=firstAnchor==state.Anchor;
            checks["Grew"]|=state.Current.Width>state.Anchor.Width&&state.Current.Height>state.Anchor.Height;
            motion?.Observe(window,read&&actual.ToPixels()==state.Current&&actualButton==state.Anchor,inspectionFixture?.Flow.Results.HasText==true);
            frames.Enqueue(FormattableString.Invariant($"{clock.Elapsed.TotalMilliseconds:F3},{state.Transition},{state.Monitor.Dpi.Factor:F3},{state.Current.Left},{state.Current.Top},{state.Current.Right},{state.Current.Bottom},{state.Current.Contains(state.Anchor)},{work.Contains(state.Current)},{read&&actual.ToPixels()==state.Current},{read&&actual.ToPixels().Contains(state.Anchor)},{read&&work.Contains(actual.ToPixels())},{KuaiYi.Windows.NativeDesktop.GetForegroundWindow()==initialForeground},{actualButton==state.Anchor},{actualButton?.Left},{actualButton?.Top},{actualButton?.Right},{actualButton?.Bottom},{state.Anchor.Left},{state.Anchor.Top},{state.Anchor.Right},{state.Anchor.Bottom}"));
            if(frames.Count>100000){frames.Dequeue();discarded++;}
        };
        System.Windows.Media.CompositionTarget.Rendering+=observe;
        int result;
        try{result=app.Run(window);Phase("RunReturned");}
        catch(Exception error)when(boundedCi){errors.Add(error.GetType().Name);throw;}
        finally
        {
            System.Windows.Media.CompositionTarget.Rendering-=observe;timer.Stop();deadline.Stop();
            if(inspectionFixture!=null)inspectionFixture.DisposeAsync().AsTask().GetAwaiter().GetResult();
            motion?.Save();
            File.WriteAllLines(output,new[]{"ms,stage,dpi,left,top,right,bottom,anchorContained,workContained,nativeMatches,actualAnchorContained,actualWorkContained,foregroundUnchanged,actualButtonMatchesAnchor,buttonLeft,buttonTop,buttonRight,buttonBottom,anchorLeft,anchorTop,anchorRight,anchorBottom"}.Concat(frames));
            File.WriteAllText(output+".summary.json",System.Text.Json.JsonSerializer.Serialize(new{Kind=interactiveInspection?"ActualProductionOverlayPublicInspection":markdownReview?"ActualProductionMarkdownPublicReview":"ActualProductionOverlayPublicProbe",Rate=rate,RetainedFrames=frames.Count,DiscardedFrames=discarded,Errors=errors,ModelCalls=0,Source=interactiveInspection?"BoundSyntheticSource_NoNetwork":markdownReview?"FixedMarkdownReviewV1":"PublicFixtureOnly",DesktopAcceptance="NotEvaluated",Mode=interactiveInspection?"Interactive_NativeStylesAdapted_NotFocusAcceptance":boundedCi?"BoundedCi":"Interactive",InspectionTheme=inspectionTheme.ToString(),InspectionMotion=inspectionMotion.ToString(),TextFactor=DesktopTextScale.Factor(window),StreamCompleted=completed,Checks=checks,ExitCode=boundedCi?ExitCode():errors.Count>0?3:0}));
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
        return boundedCi?ExitCode():interactiveInspection&&errors.Count>0?3:result;
        int ExitCode()=>timedOut?4:errors.Count>0||!completed||frames.Count<10||discarded!=0||checks.Values.Any(value=>!value)?3:0;
    }
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoKeys:ISecretProtector{public byte[] Protect(ReadOnlySpan<byte> plain)=>throw new InvalidOperationException("This public fixture has no keys.");public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>throw new InvalidOperationException("This public fixture has no keys.");}
}
