using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
namespace KuaiYi.GrowthHarness;
/// <summary>Test tool only: drives the exact shipped HWND/view with public text and a bound, synthetic, no-network request.</summary>
internal static class ActualOverlayProbe
{
    private const string PublicText="快译公开浮窗验证。没有读取其他应用，没有请求模型。\n\n同一个按钮是整张卡片的锚点；文字按最终宽度排版，窗口从按钮生长，跨DPI拖动后松手保存。\n\n";
    public static int Run(string output,string rate,bool boundedCi=false)
    {
        bool burst=rate=="burst";double perSecond=0;
        if(!burst&&(!double.TryParse(rate,System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out perSecond)||!double.IsFinite(perSecond)||perSecond is <1 or >10000))return 2;
        output=Path.GetFullPath(output);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        var phaseClock=Stopwatch.StartNew();
        void Phase(string value)
        {
            if(!boundedCi)return;
            string line=FormattableString.Invariant($"{phaseClock.Elapsed.TotalMilliseconds:F3} {value}");
            File.AppendAllText(output+".phases.log",line+Environment.NewLine);Console.Error.WriteLine("KuaiYi public overlay phase: "+line);
        }
        Phase("RunEntered");
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-overlay-probe-"+Guid.NewGuid().ToString("N"));
        var coordinator=new SettingsCoordinator(new SettingsStore(Path.Combine(root,"config")),new SecretFileStore(Path.Combine(root,"secrets"),new NoKeys(),new DirectoryPolicy()));
        Phase("CoordinatorCreated");
        using(var draft=coordinator.OpenDraftAsync().GetAwaiter().GetResult())
        {Phase("DraftOpened");draft.Update(s=>s with{ActiveProfileId="public",Profiles=[new(){Id="public",Model="public-fixture",BaseUrl="https://public.invalid",Authentication=ApiAuthentication.None}]});coordinator.SaveAsync(draft).GetAwaiter().GetResult();}
        Phase("PublicSettingsSaved");
        var app=new Application();var window=new ShellWindow("KuaiYi.PublicOverlayProbe."+Guid.NewGuid().ToString("N"),coordinator,enableSelectionPlatform:false){Title="快译 · 公开测试浮窗（非模型）"};
        Phase("ProductionWindowConstructed");
        window.SourceInitialized+=(_,_)=>Phase("SourceInitialized");window.Closing+=(_,_)=>Phase("Closing");window.Closed+=(_,_)=>Phase("Closed");
        app.DispatcherUnhandledException+=(_,e)=>Phase("DispatcherUnhandled:"+e.Exception.GetType().Name);
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};RequestIdentity? current=null;int offset=0;bool busy=false;double budget=0;long feedTime=Stopwatch.GetTimestamp();
        string stream=string.Concat(Enumerable.Repeat(PublicText,24));var errors=new List<string>();
        bool completed=false,timedOut=false;long completedAt=0;
        var deadline=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};long startedAt=Stopwatch.GetTimestamp();
        var checks=new Dictionary<string,bool>{{"AnchorContained",true},{"WorkContained",true},{"NativeMatches",true},{"ActualAnchorContained",true},{"ActualWorkContained",true},{"ForegroundUnchanged",true},{"ActualButtonMatchesAnchor",true},{"AnchorStable",true},{"Grew",false}};
        PixelRect? firstAnchor=null;
        async Task Start()
        {
            Phase("StartupAwait");await window.PresentationStartup;Phase("StartupReady");var gate=window.PresentationGate;var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);
            if(!gate.TryEnable(origin,true)||!gate.TryGetProfileBinding("public",out var binding)||!gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var id,out _)){errors.Add("PublicRequestRejected");return;}
            current=id;offset=0;budget=0;
            if(!await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Begin))){errors.Add("BeginRejected");return;}
            Phase("BeginAccepted");feedTime=Stopwatch.GetTimestamp();timer.Start();
        }
        timer.Tick+=async(_,_)=>
        {
            if(busy||current is not {} id)return;busy=true;
            try
            {
                long now=Stopwatch.GetTimestamp();budget+=burst?2000:perSecond*Stopwatch.GetElapsedTime(feedTime,now).TotalSeconds;feedTime=now;int count=Math.Min((int)Math.Min(budget,stream.Length-offset),stream.Length-offset);budget-=count;if(count>0){bool accepted=await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Delta,stream.Substring(offset,count)));if(!accepted){timer.Stop();errors.Add("DeltaRejected");return;}offset+=count;}
                if(offset==stream.Length){timer.Stop();completed=await window.ReceivePresentationAsync(id,new(OverlayResultEventKind.Complete));if(!completed)errors.Add("CompleteRejected");completedAt=Stopwatch.GetTimestamp();Phase(completed?"StreamCompleted":"CompleteRejected");}
            }
            catch(Exception error){timer.Stop();errors.Add(error.GetType().Name);}
            finally{busy=false;}
        };
        window.Loaded+=async(_,_)=>{try{await Start();}catch(Exception error){errors.Add(error.GetType().Name);}};
        window.PreviewKeyDown+=async(_,e)=>{if(e.Key!=Key.F5)return;e.Handled=true;try{await Start();}catch(Exception error){errors.Add(error.GetType().Name);}};
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
            File.WriteAllLines(output,new[]{"ms,stage,dpi,left,top,right,bottom,anchorContained,workContained,nativeMatches,actualAnchorContained,actualWorkContained,foregroundUnchanged,actualButtonMatchesAnchor,buttonLeft,buttonTop,buttonRight,buttonBottom,anchorLeft,anchorTop,anchorRight,anchorBottom"}.Concat(frames));
            File.WriteAllText(output+".summary.json",System.Text.Json.JsonSerializer.Serialize(new{Kind="ActualProductionOverlayPublicProbe",Rate=rate,RetainedFrames=frames.Count,DiscardedFrames=discarded,Errors=errors,ModelCalls=0,Source="PublicFixtureOnly",DesktopAcceptance="NotEvaluated",Mode=boundedCi?"BoundedCi":"Interactive",StreamCompleted=completed,Checks=checks,ExitCode=boundedCi?ExitCode():0}));
            if(Directory.Exists(root))Directory.Delete(root,true);
        }
        return boundedCi?ExitCode():result;
        int ExitCode()=>timedOut?4:errors.Count>0||!completed||frames.Count<10||discarded!=0||checks.Values.Any(value=>!value)?3:0;
    }
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoKeys:ISecretProtector{public byte[] Protect(ReadOnlySpan<byte> plain)=>throw new InvalidOperationException("This public fixture has no keys.");public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>throw new InvalidOperationException("This public fixture has no keys.");}
}
