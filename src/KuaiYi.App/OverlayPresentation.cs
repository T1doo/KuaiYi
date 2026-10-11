using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal static class OverlayTokens
{
    public static readonly Brush Off=new SolidColorBrush(Color.FromRgb(0xd9,0x43,0x43));
    public static readonly Brush On=new SolidColorBrush(Color.FromRgb(0x16,0x88,0x4a));
    public static readonly Brush Dark=new SolidColorBrush(Color.FromRgb(0x20,0x24,0x2a));
    public const double Padding=16,ButtonSize=48,ToolSize=28;
}
internal sealed partial class ShellWindow
{
    private readonly Canvas overlayCanvas=new(){ClipToBounds=true};
    private readonly Border cardBackground=new(){Background=Brushes.White,IsHitTestVisible=false};
    private readonly TextBox body=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,BorderThickness=new Thickness(0),Padding=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,FontFamily=new("Segoe UI"),Background=Brushes.Transparent};
    private readonly TextBlock measurement=new(){TextWrapping=TextWrapping.Wrap,FontFamily=new("Segoe UI")};
    private readonly Canvas controls=new(){Height=48};private readonly List<Button> tools=[];
    private readonly Button newContent=new(){Content="有新内容",Visibility=Visibility.Collapsed,MinHeight=28};
    private readonly Ellipse progressRing=new(){Width=42,Height=42,Stroke=Brushes.White,StrokeThickness=2,StrokeDashArray=new DoubleCollection{12,5},IsHitTestVisible=false,Visibility=Visibility.Collapsed,RenderTransformOrigin=new Point(.5,.5)};
    private readonly TextBlock connectionStatus=new(){Text="正在连接…",VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
    private readonly DispatcherTimer flush=new(){Interval=TimeSpan.FromMilliseconds(50)},peekIn=new(){Interval=TimeSpan.FromMilliseconds(350)},peekOut=new(){Interval=TimeSpan.FromMilliseconds(250)},waiting=new(){Interval=TimeSpan.FromMilliseconds(400)};
    private OverlayResults overlayResults=null!;private IdentityEventReceiver<OverlayResultEvent> presentationReceiver=null!;
    private OverlayFrames? overlayFrames;private bool rendering,peek,dirty,follow=true,awaitingText,pendingReveal;private TimeSpan lastRender;private long resultRevision;
    private readonly Stopwatch frameClock=Stopwatch.StartNew();
    private PixelRect? appliedRect;private int appliedRadius;private bool appliedVisible;
    internal RuntimeGate PresentationGate=>lifecycle.Gate;
    internal OverlayFrames? PresentationGeometry=>overlayFrames;
    internal PixelRect? PresentationButtonScreenBounds
    {
        get{if(!button.IsVisible||button.ActualWidth<=0)return null;Point first=button.PointToScreen(new Point(0,0)),last=button.PointToScreen(new Point(button.ActualWidth,button.ActualHeight));return new((int)Math.Round(first.X),(int)Math.Round(first.Y),(int)Math.Round(last.X),(int)Math.Round(last.Y));}
    }
    internal Task PresentationStartup=>startup;
    internal Task<bool> ReceivePresentationAsync(RequestIdentity identity,OverlayResultEvent update,CancellationToken cancellation=default)
        =>presentationReceiver.ReceiveAsync(identity,update,value=>ApplyResult(identity,value),cancellation);
    private void SetupPresentation()
    {
        overlayResults=new(lifecycle.Gate);presentationReceiver=new(lifecycle.Gate,new DispatcherSynchronizationContext(Dispatcher));
        Content=overlayCanvas;overlayCanvas.Children.Add(cardBackground);overlayCanvas.Children.Add(body);overlayCanvas.Children.Add(controls);overlayCanvas.Children.Add(button);overlayCanvas.Children.Add(progressRing);overlayCanvas.Children.Add(connectionStatus);overlayCanvas.Children.Add(newContent);
        body.Visibility=controls.Visibility=Visibility.Collapsed;button.Width=button.Height=48;button.Padding=new Thickness(0);RefreshPresentationTheme();UpdateButtonGlyph(false);
        Tool("回看",ReopenResult);Tool("收起",CollapseResult);Tool("停止",StopResult);Tool("复制",CopyResult);Tool("设置",()=>_=OpenSettings());
        void Tool(string label,Action action){var b=new Button{Content=label,MinWidth=28,Height=28,Padding=new Thickness(2)};b.Click+=(_,_)=>action();tools.Add(b);controls.Children.Add(b);}
        newContent.Click+=(_,_)=>{follow=true;body.ScrollToEnd();newContent.Visibility=Visibility.Collapsed;};
        body.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler((_,e)=>{if(e.ExtentHeightChange==0&&e.VerticalChange!=0){follow=body.ExtentHeight-body.ViewportHeight-body.VerticalOffset<=24;if(follow)newContent.Visibility=Visibility.Collapsed;}}));
        body.PreviewMouseLeftButtonDown+=(_,_)=>{NativeDesktop.SetOverlayStyle(hwnd,true);Activate();};
        PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){NativeDesktop.SetOverlayStyle(hwnd,false);CollapseResult();e.Handled=true;}};
        MouseEnter+=(_,_)=>{peekOut.Stop();if(overlayFrames?.IsExpanded!=true)peekIn.Start();};
        MouseLeave+=(_,_)=>{peekIn.Stop();if(peek)peekOut.Start();};
        peekIn.Tick+=(_,_)=>{peekIn.Stop();if(!gesture.Active&&IsMouseOver&&lifecycle.Gate.TryPeek(true)){peek=true;overlayFrames?.OpenToolbar();StartFrames();}};
        peekOut.Tick+=(_,_)=>{peekOut.Stop();if(peek&&!IsMouseOver&&!IsKeyboardFocusWithin&&lifecycle.Gate.TryPeek(false)){peek=false;overlayFrames?.Collapse();StartFrames();}};
        flush.Tick+=(_,_)=>{flush.Stop();FlushResult();};
        waiting.Tick+=(_,_)=>{waiting.Stop();if(overlayResults.Identity is {} id&&lifecycle.Gate.CanApply(id)&&!overlayResults.HasText&&!gesture.Active&&lifecycle.Gate.TryRevealDelta(id)){peek=true;overlayFrames?.OpenToolbar();button.ToolTip="正在连接…";StartFrames();}};
    }
    private void UpdateButtonGlyph(bool on)
    {
        // Linear monochrome vector; both shape and accessible name identify state.
        var glyph=new Path{Width=20,Height=20,Stretch=Stretch.Uniform,Fill=SystemParameters.HighContrast?SystemColors.HighlightTextBrush:Brushes.White,
            Data=Geometry.Parse(on?"M 2,1 L 8,1 L 8,19 L 2,19 Z M 12,1 L 18,1 L 18,19 L 12,19 Z":"M 4,1 L 18,10 L 4,19 Z")};button.Content=glyph;
        if(SystemParameters.HighContrast)button.Background=SystemColors.HighlightBrush;
    }
    private LayoutPreferences Preferences()=>new(lifecycle.Settings?.Data.Overlay.PreferredWidthDip??420,640);
    private bool ReduceMotion=>lifecycle.Settings?.Data.Overlay.ReduceMotion==MotionPreference.Reduce||lifecycle.Settings?.Data.Overlay.ReduceMotion!=MotionPreference.Full&&!SystemParameters.ClientAreaAnimation;
    private void RefreshPresentationTheme()
    {
        bool dark=lifecycle.Settings?.Data.Overlay.Theme==ThemePreference.Dark;
        if(lifecycle.Settings?.Data.Overlay.Theme is null or ThemePreference.FollowSystem){try{dark=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int value&&value==0;}catch(Exception error)when(error is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException){Report("系统主题读取失败",error);}}
        cardBackground.Background=SystemParameters.HighContrast?SystemColors.WindowBrush:dark?OverlayTokens.Dark:Brushes.White;body.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:dark?Brushes.WhiteSmoke:Brushes.Black;connectionStatus.Foreground=body.Foreground;
    }
    private void ApplyResult(RequestIdentity identity,OverlayResultEvent value)
    {
        if(exiting)return;
        switch(value.Kind)
        {
            case OverlayResultEventKind.Begin:
                if(!overlayResults.Begin(identity))return;resultRevision++;follow=true;dirty=true;peek=false;pendingReveal=false;awaitingText=true;progressRing.Visibility=Visibility.Visible;if(!gesture.Active)overlayFrames?.Collapse();StartFrames();waiting.Stop();waiting.Start();Dispatcher.BeginInvoke(()=>{if(!exiting)Refresh();});break;
            case OverlayResultEventKind.Delta:
                if(!overlayResults.Append(identity,value.Text))return;dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();
                if(overlayResults.TryAutoReveal(identity)&&(peek||overlayFrames?.IsExpanded!=true))pendingReveal=true;break;
            case OverlayResultEventKind.Complete:
                if(!overlayResults.Complete(identity))return;dirty=true;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();break;
            case OverlayResultEventKind.Stopped:overlayResults.MarkStopped();awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();break;
            default:throw new ArgumentOutOfRangeException(nameof(value));
        }
        if(dirty&&!flush.IsEnabled)flush.Start();
    }
    private int MeasureBody()
    {
        if(overlayFrames==null)return 48;
        var plan=overlayFrames.Plan??OverlayLayoutPlanner.Plan(overlayFrames.Anchor,overlayFrames.Monitor,Preferences());
        double size=lifecycle.Settings?.Data.Overlay.FontSizeDip??15;
        body.FontSize=measurement.FontSize=size;TextBlock.SetLineHeight(body,size*1.55);measurement.LineHeight=size*1.55;
        measurement.Text=overlayResults.Text;measurement.Measure(new Size(Math.Max(1,plan.Dpi.Dips(plan.Target.Width)-32),double.PositiveInfinity));
        return plan.Dpi.CeilingPixels(48+32+measurement.DesiredSize.Height);
    }
    private void OpenBody(){if(overlayFrames==null)return;overlayFrames.Open(MeasureBody(),Preferences());dirty=true;if(!flush.IsEnabled)flush.Start();StartFrames();}
    private void FlushResult()
    {
        if(!dirty||exiting)return;dirty=false;
        double offset=body.VerticalOffset;body.Text=overlayResults.Text;bool bottom=follow;long revision=resultRevision;
        if(!bottom&&overlayResults.HasText)newContent.Visibility=Visibility.Visible;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>{if(exiting||revision!=resultRevision||lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))return;if(bottom&&follow)body.ScrollToEnd();else body.ScrollToVerticalOffset(offset);});
        if(overlayFrames!=null&&!gesture.Active&&overlayFrames.Transition!=OverlayTransition.Moving)
        {
            if(pendingReveal&&overlayResults.Identity is {} id&&overlayResults.TryAutoReveal(id)){pendingReveal=false;peek=false;overlayFrames.Open(MeasureBody(),Preferences());StartFrames();}
            else if(overlayFrames.IsExpanded&&!peek){overlayFrames.Grow(MeasureBody());StartFrames();}
        }
        ApplyOverlayFrame();
    }
    private void ReopenResult(){if(!IsVisible)ShowOverlay();if(overlayResults.Reopen()){peek=false;OpenBody();}}
    private void CollapseResult(){peek=false;pendingReveal=false;peekIn.Stop();peekOut.Stop();waiting.Stop();overlayResults.Collapse();overlayFrames?.Collapse();StartFrames();}
    private void StopResult(){lifecycle.Gate.StopGeneration();overlayResults.MarkStopped();awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();button.ToolTip="已停止生成 · 结果保留在本次会话";}
    private void CopyResult(){if(!overlayResults.HasText)return;try{Clipboard.SetText(overlayResults.Text);}catch(Exception error){Report("复制失败",error);}}
    private void StopPresentation(){overlayResults.MarkStopped();awaitingText=false;progressRing.Visibility=Visibility.Collapsed;CollapseResult();}
    private void HidePresentation(bool clear)
    {waiting.Stop();flush.Stop();peekIn.Stop();peekOut.Stop();StopFrames();peek=false;awaitingText=false;progressRing.Visibility=Visibility.Collapsed;overlayResults.MarkStopped();if(clear){overlayResults.Clear();body.Clear();measurement.Text="";resultRevision++;}overlayFrames?.Compact();ApplyOverlayFrame();}
    private void BeginOverlayMove(){StopFrames();peekIn.Stop();peekOut.Stop();overlayFrames?.BeginMove();}
    private void MoveOverlay(PixelPoint desired,MonitorTopology topology)
    {
        if(overlayFrames==null)return;
        var monitor=topology.At(new(desired.X,desired.Y,desired.X+anchor.Width,desired.Y+anchor.Height));overlayFrames.Move(desired,monitor);anchor=overlayFrames.Anchor;anchorMonitor=overlayFrames.Monitor;ApplyOverlayFrame();
    }
    private void EndOverlayMove()
    {
        if(overlayFrames?.Transition!=OverlayTransition.Moving)return;
        bool reveal=pendingReveal;pendingReveal=false;
        if(reveal&&overlayResults.Identity is {} id&&overlayResults.TryAutoReveal(id)){peek=false;OpenBody();}
        else if(peek)overlayFrames.OpenToolbar();else overlayFrames.EndMove(Preferences(),MeasureBody());StartFrames();
    }
    private void DpiPlacement()
    {
        if(exiting||hwnd==0||overlayFrames==null)return;
        StopFrames();RefreshPresentationTheme();var topology=MonitorTopology.Read();
        if(!gesture.Active&&topology.Monitors.FirstOrDefault(m=>m.Id==anchorMonitor?.Id)==anchorMonitor)
        {ReapplyAfterDpi();StartFrames();return;}
        if(gesture.Active&&NativeDesktop.GetCursorPos(out var cursor)&&topology.Monitors.Any(m=>m.Id==anchorMonitor?.Id))
        {
            var point=new PixelPoint(cursor.X,cursor.Y);
            var monitor=topology.At(new(cursor.X,cursor.Y,cursor.X+1,cursor.Y+1));
            if(!gesture.TryDpiDesiredAnchor(point,monitor.Dpi,out var desired))return;
            overlayFrames.Move(desired,monitor);
            anchor=overlayFrames.Anchor;anchorMonitor=monitor;gestureDpi=monitor.Dpi;gesture.Rebase(point,anchor,true);positionTouched=true;ApplyOverlayFrame();
        }
        else
        {
            CancelPointer();Reposition(CurrentPosition());pendingPosition=CurrentPosition();QueuePositionSave();
        }
        // WPF owns its DPI transforms/suggested HWND processing. Reapply the validated card before rendering after its update.
        ReapplyAfterDpi();
    }
    private void ReapplyAfterDpi()=>Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>{if(!exiting&&geometryReady)ApplyOverlayFrame(forceNative:true);});
    protected override void OnDpiChanged(System.Windows.DpiScale oldDpi,System.Windows.DpiScale newDpi)
    {base.OnDpiChanged(oldDpi,newDpi);if(hwnd!=0)Pointer(DpiPlacement);}
    private void StartFrames(){if(rendering||!awaitingText&&overlayFrames?.NeedsFrame!=true||!IsVisible||exiting)return;lastRender=frameClock.Elapsed;rendering=true;CompositionTarget.Rendering+=RenderOverlay;}
    private void StopFrames(){if(!rendering)return;CompositionTarget.Rendering-=RenderOverlay;rendering=false;}
    private void RenderOverlay(object? sender,EventArgs e)
    {
        try
        {if(exiting||!IsVisible||overlayFrames==null){StopFrames();return;}if(awaitingText&&overlayResults.Identity is {} id&&!lifecycle.Gate.CanApply(id)){awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();overlayResults.MarkStopped();}var now=frameClock.Elapsed;overlayFrames.Tick(now-lastRender,ReduceMotion);lastRender=now;progressRing.RenderTransform=ReduceMotion?Transform.Identity:new RotateTransform(now.TotalMilliseconds*.24);ApplyOverlayFrame();if(!awaitingText&&!overlayFrames.NeedsFrame)StopFrames();}
        catch(Exception error){StopFrames();lifecycle.Gate.Disable();Report("浮窗几何更新失败",error);}
    }
    private void ApplyOverlayFrame(bool forceNative=false)
    {
        if(overlayFrames==null||hwnd==0)return;
        anchor=overlayFrames.Anchor;anchorMonitor=overlayFrames.Monitor;var rect=overlayFrames.Current;var dpi=anchorMonitor.Dpi;
        bool expanded=rect!=anchor;
        double expansion=Math.Clamp((rect.Width-anchor.Width+rect.Height-anchor.Height)/(48*dpi.Factor),0,1);
        int radius=dpi.CeilingPixels(24-8*expansion);
        if(forceNative||appliedRect!=rect||appliedRadius!=radius||appliedVisible!=IsVisible){NativeDesktop.ApplyBounds(hwnd,rect,radius,show:IsVisible);appliedRect=rect;appliedRadius=radius;appliedVisible=IsVisible;}
        double width=dpi.Dips(rect.Width),height=dpi.Dips(rect.Height);overlayCanvas.Width=width;overlayCanvas.Height=height;cardBackground.Width=width;cardBackground.Height=height;
        double bx=dpi.Dips(anchor.Left-rect.Left),by=dpi.Dips(anchor.Top-rect.Top);Canvas.SetLeft(button,bx);Canvas.SetTop(button,by);
        Canvas.SetLeft(progressRing,bx+3);Canvas.SetTop(progressRing,by+3);
        controls.Visibility=expanded?Visibility.Visible:Visibility.Collapsed;controls.Width=width;Canvas.SetTop(controls,by);
        double before=bx,after=width-bx-48;bool left=before>after;double available=Math.Max(before,after),start=left?0:bx+48;
        connectionStatus.Visibility=awaitingText&&peek&&expanded?Visibility.Visible:Visibility.Collapsed;Canvas.SetLeft(connectionStatus,start+8);Canvas.SetTop(connectionStatus,by+14);
        for(int i=0;i<tools.Count;i++){var tool=tools[i];double tw=tools[i].Content!.ToString()!.Length*15+10;tool.Visibility=connectionStatus.Visibility!=Visibility.Visible&&available>=tw?Visibility.Visible:Visibility.Collapsed;if(tool.Visibility==Visibility.Visible){tool.Width=tw;Canvas.SetLeft(tool,start);Canvas.SetTop(tool,10);start+=tw;available-=tw;}}
        var plan=overlayFrames.Plan;var direction=overlayFrames.ContentDirection;if(direction==null||peek||!expanded){body.Visibility=Visibility.Collapsed;newContent.Visibility=Visibility.Collapsed;return;}
        bool down=direction is GrowthDirection.RightDown or GrowthDirection.LeftDown or GrowthDirection.BalancedDown;
        int pad=dpi.CeilingPixels(16),top=down?anchor.Bottom+pad:rect.Top+pad,bottom=down?rect.Bottom-pad:anchor.Top-pad;
        double targetWidth=overlayFrames.TextWidthDip;
        int targetLeft=plan?.Target.Left??(direction is GrowthDirection.RightDown or GrowthDirection.RightUp?anchor.Left:direction is GrowthDirection.LeftDown or GrowthDirection.LeftUp?anchor.Right-dpi.CeilingPixels(targetWidth):anchor.Center.X-dpi.CeilingPixels(targetWidth)/2);
        Canvas.SetLeft(body,dpi.Dips(targetLeft+pad-rect.Left));Canvas.SetTop(body,dpi.Dips(top-rect.Top));body.Width=Math.Max(1,targetWidth-32);body.Height=Math.Max(0,dpi.Dips(bottom-top));body.Opacity=overlayFrames.ContentOpacity;body.Visibility=body.Height>0?Visibility.Visible:Visibility.Collapsed;
        Canvas.SetLeft(newContent,Math.Max(0,width-90));Canvas.SetTop(newContent,Math.Max(0,height-30));
    }
    private void DisposePresentation(){StopFrames();flush.Stop();peekIn.Stop();peekOut.Stop();waiting.Stop();overlayResults.Clear();body.Clear();measurement.Text="";}
}
