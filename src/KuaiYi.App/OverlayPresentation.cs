using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    private readonly Canvas overlayCanvas=new(){ClipToBounds=true,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top};
    private readonly Border cardBackground=new(){Background=Brushes.White,IsHitTestVisible=false};
    private readonly RichTextBox body=new(){IsReadOnly=true,IsUndoEnabled=false,IsDocumentEnabled=true,BorderThickness=new Thickness(0),Padding=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,FontFamily=DesktopTheme.ProseFont,Background=Brushes.Transparent};
    private readonly RichTextBox measurement=new(){IsReadOnly=true,IsUndoEnabled=false,IsDocumentEnabled=true,BorderThickness=new Thickness(0),Padding=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Visible,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
    private readonly TextBlock resultLabel=new(){FontSize=12,FontWeight=FontWeights.Normal,TextTrimming=TextTrimming.CharacterEllipsis,Visibility=Visibility.Collapsed};
    private readonly Canvas controls=new(){Height=48};private readonly List<Button> tools=[];private readonly List<Action> toolActions=[];
    private readonly Border toolbarSurface=new(){Height=48,IsHitTestVisible=false};
    private readonly Border toolbarDivider=new(){Height=1,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    private readonly Border toolbarSeam=new(){Height=3,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    private static readonly Brush DownSeam=CreateSeam(90),UpSeam=CreateSeam(270);
    private static Brush CreateSeam(double angle){var brush=new LinearGradientBrush(Color.FromArgb(18,15,23,42),Colors.Transparent,angle);brush.Freeze();return brush;}
    private readonly Button moreTools=new(){Content="…",Width=28,Height=28,ToolTip="更多操作及版本"};
    private readonly ContextMenu overflowMenu=new();private Button? styleTool;
    private readonly Button versionsTool=new(){Content="版本",MinWidth=56,Height=28,FontSize=12,ToolTip="本次来源的内存版本；切换不发送请求"};
    private bool touchToolbar;
    private static readonly DataTemplate CollapseUpGlyph=DesktopTheme.ToolGlyph("M3 14 L11 6 L19 14"),CollapseDownGlyph=DesktopTheme.ToolGlyph("M3 6 L11 14 L19 6"),MoreGlyph=DesktopTheme.ToolGlyph("M3 9 L3 11 M10 9 L10 11 M17 9 L17 11");
    private readonly Button failureNotice=new(){Height=44,FontSize=12,Focusable=true,Visibility=Visibility.Collapsed,Padding=new Thickness(2)};
    private readonly Button newContent=new(){Content="有新内容",Visibility=Visibility.Collapsed,MinHeight=28};
    private readonly Ellipse progressRing=new(){Width=42,Height=42,Stroke=Brushes.White,StrokeThickness=2,StrokeDashArray=new DoubleCollection{18,48},StrokeDashCap=PenLineCap.Round,Opacity=.85,IsHitTestVisible=false,Visibility=Visibility.Collapsed,RenderTransformOrigin=new Point(.5,.5)};
    private readonly TextBlock connectionStatus=new(){Text="正在连接…",VerticalAlignment=VerticalAlignment.Center,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
    private readonly DispatcherTimer flush=new(){Interval=TimeSpan.FromMilliseconds(50)},peekIn=new(){Interval=TimeSpan.FromMilliseconds(350)},peekOut=new(){Interval=TimeSpan.FromMilliseconds(250)},waiting=new(){Interval=TimeSpan.FromMilliseconds(400)};
    private OverlayResults overlayResults=null!;private IdentityEventReceiver<OverlayResultEvent> presentationReceiver=null!;
    private OverlayFrames? overlayFrames;private bool rendering,peek,dirty,follow=true,awaitingText,pendingReveal;private long resultRevision;
    private readonly OverlayFrameClock frameClock=new();
    private PixelRect? appliedRect;private int appliedRadius;private bool appliedVisible,applyingOverlayFrame;
    internal RuntimeGate PresentationGate=>lifecycle.Gate;
    internal OverlayFrames? PresentationGeometry=>overlayFrames;
    internal PixelRect? PresentationButtonScreenBounds
    {
        get{if(!button.IsVisible||button.ActualWidth<=0)return null;Point first=button.PointToScreen(new Point(0,0)),last=button.PointToScreen(new Point(button.ActualWidth,button.ActualHeight));return new((int)Math.Round(first.X),(int)Math.Round(first.Y),(int)Math.Round(last.X),(int)Math.Round(last.Y));}
    }
    internal Task PresentationStartup=>startup;
    internal Task? PresentationCloseOperation=>closeRequests?.Current;
    internal bool PresentationFrameApplying=>applyingOverlayFrame;
    internal OverlayFrameTick? PresentationLastTick=>frameClock.LastTick;
    internal long PresentationAnimationEpoch=>frameClock.AnimationEpoch;
    internal TimeSpan PresentationAnimationStartedAt=>frameClock.AnimationStartedAt;
    private void SetupPresentation()
    {
        DesktopTextScale.Ignore(body);DesktopTextScale.Ignore(measurement);DesktopTextScale.Ignore(button);
        DesktopScrollChrome.Install(body);DesktopScrollChrome.Install(measurement);
        DesktopTheme.Install(this,()=>lifecycle.Settings?.Data.Overlay.Theme??ThemePreference.FollowSystem,forms:false,changedAppearance:RefreshPresentationTheme,motionPreference:()=>lifecycle.Settings?.Data.Overlay.ReduceMotion??MotionPreference.FollowSystem);
        resultLabel.SetBinding(System.Windows.Automation.AutomationProperties.NameProperty,new System.Windows.Data.Binding(nameof(TextBlock.Text)){Source=resultLabel});
        overlayResults=new(lifecycle.Gate);presentationReceiver=new(lifecycle.Gate,new DispatcherSynchronizationContext(Dispatcher));
        bodyMarkdown=new(body,ObserveCodeReaderIntent,CaptureBodyReaderGuard,()=>follow,OriginalSourceChanged,CanReadOriginalSourceVersion,ImageSourcePreviewFailed,OpenMarkdownLink,CaptureOriginalSourceCopyGuard);measurementMarkdown=new(measurement,canReadSource:CanReadOriginalSourceVersion,sourceFailed:ImageSourcePreviewFailed);
        Content=overlayCanvas;overlayCanvas.Children.Add(cardBackground);overlayCanvas.Children.Add(body);overlayCanvas.Children.Add(resultLabel);overlayCanvas.Children.Add(versionsTool);overlayCanvas.Children.Add(failureNotice);overlayCanvas.Children.Add(controls);overlayCanvas.Children.Add(button);overlayCanvas.Children.Add(progressRing);overlayCanvas.Children.Add(connectionStatus);overlayCanvas.Children.Add(newContent);
        KeyboardNavigation.SetTabNavigation(overlayCanvas,KeyboardNavigationMode.Local);
        KeyboardNavigation.SetTabNavigation(controls,KeyboardNavigationMode.Local);
        KeyboardNavigation.SetTabNavigation(body,KeyboardNavigationMode.Local);body.IsReadOnlyCaretVisible=true;
        KeyboardNavigation.SetTabIndex(button,0);KeyboardNavigation.SetTabIndex(controls,1);KeyboardNavigation.SetTabIndex(failureNotice,2);
        KeyboardNavigation.SetTabIndex(body,3);KeyboardNavigation.SetTabIndex(versionsTool,4);KeyboardNavigation.SetTabIndex(newContent,5);
        body.Visibility=controls.Visibility=Visibility.Collapsed;button.Width=button.Height=48;button.Padding=new Thickness(0);button.Style=(Style)Resources[DesktopTheme.AnchorButton];cardBackground.CornerRadius=new CornerRadius(6);cardBackground.BorderThickness=new Thickness(1);RefreshPresentationTheme();
        DesktopTheme.Bind(toolbarSurface,Border.BackgroundProperty,DesktopTheme.Rail);DesktopTheme.Bind(toolbarDivider,Border.BackgroundProperty,DesktopTheme.Stroke);controls.Children.Add(toolbarSurface);controls.Children.Add(toolbarDivider);
        toolbarDivider.Opacity=.7;controls.Children.Add(toolbarSeam);
        styleTool=Tool("风格",OpenStyleMenu);Tool("翻译截图",()=>TranslateScreenshot());Tool("回看",ReopenResult);Tool("收起",CollapseResult);Tool("停止",StopResult);Tool("重新生成",()=>RegenerateResult());Tool("复制",CopyResult);Tool("查看原文",ShowSelectionSource);Tool("清除当前内容",()=>_=ClearCurrentContent());Tool("设置",()=>_=OpenSettings());
        Button Tool(string label,Action action){var b=new Button{Content=label,Tag=label,IsEnabled=label is not ("回看" or "复制" or "重新生成"),MinWidth=28,Height=28,FontSize=12,Padding=new Thickness(2)};System.Windows.Automation.AutomationProperties.SetName(b,label);b.ToolTip=label;if(label is "重新生成" or "回看" or "复制")ToolTipService.SetShowOnDisabled(b,true);b.Style=(Style)Resources[DesktopTheme.ToolButton];if(label=="风格")b.ContentTemplate=DesktopTheme.ChoiceWithChevron();b.Click+=(_,_)=>action();tools.Add(b);toolActions.Add(action);controls.Children.Add(b);return b;}
        versionsTool.Style=(Style)Resources[DesktopTheme.ToolButton];versionsTool.ContentTemplate=DesktopTheme.ChoiceWithChevron();versionsTool.Click+=(_,_)=>OpenVersionMenu();
        moreTools.Style=(Style)Resources[DesktopTheme.ToolButton];moreTools.ContentTemplate=MoreGlyph;failureNotice.Style=(Style)Resources[DesktopTheme.ToolButton];failureNotice.HorizontalContentAlignment=HorizontalAlignment.Left;failureNotice.ContentTemplate=DesktopTheme.WrappingLabel();DesktopTheme.Bind(failureNotice,Control.ForegroundProperty,DesktopTheme.Error);newContent.Style=(Style)Resources[DesktopTheme.ToolButton];controls.Children.Add(moreTools);moreTools.Click+=(_,_)=>OpenOverflowMenu(moreTools);
        System.Windows.Automation.AutomationProperties.SetName(moreTools,"更多操作及版本");overflowMenu.Closed+=(_,_)=>PresentationMenuClosed();overflowMenu.Opened+=(_,_)=>DesktopTheme.ApplyMenu(overflowMenu,this);
        failureNotice.Click+=(_,_)=>ShowFailureRecovery();
        newContent.Click+=(_,_)=>{FollowBodyEnd();newContent.Visibility=Visibility.Collapsed;};
        body.AddHandler(ScrollBar.ScrollEvent,new ScrollEventHandler((_,_)=>ObserveBodyReaderIntent()),true);
        // Reader intent must fence an already queued layout restore before WPF changes the viewport.
        body.AddHandler(Mouse.PreviewMouseWheelEvent,new MouseWheelEventHandler((_,_)=>ObserveBodyReaderIntent()),true);
        body.AddHandler(Keyboard.PreviewKeyDownEvent,new KeyEventHandler((_,e)=>{if(e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End)ObserveBodyReaderIntent();}),true);
        body.AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((_,e)=>{if(e.ChangedButton==MouseButton.Left)ObserveBodyReaderIntent();}),true);
        body.AddHandler(Mouse.PreviewMouseMoveEvent,new MouseEventHandler((_,e)=>{if(e.LeftButton==MouseButtonState.Pressed)ObserveBodyReaderIntent();}),true);
        body.AddHandler(UIElement.PreviewTouchDownEvent,new EventHandler<TouchEventArgs>((_,_)=>ObserveBodyReaderIntent()),true);
        body.AddHandler(UIElement.PreviewTouchMoveEvent,new EventHandler<TouchEventArgs>((_,_)=>ObserveBodyReaderIntent()),true);
        body.PreviewMouseLeftButtonDown+=(_,_)=>{NativeDesktop.SetOverlayStyle(hwnd,true);Activate();};
        PreviewKeyDown+=(_,e)=>
        {
            if(e.Key!=Key.Escape||Keyboard.Modifiers!=ModifierKeys.None)return;
            ContextMenu? menu=overflowMenu.IsOpen?overflowMenu:button.ContextMenu;
            if(menu?.IsOpen!=true)return;
            menu.IsOpen=false;e.Handled=true;
        };
        PreviewTouchDown+=(_,_)=>{if(!touchToolbar){touchToolbar=true;ApplyOverlayFrame();}};
        PreviewMouseDown+=(_,e)=>{if(e.StylusDevice==null&&touchToolbar){touchToolbar=false;ApplyOverlayFrame();}};
        MouseEnter+=(_,_)=>{peekOut.Stop();if(overlayFrames?.IsExpanded!=true)peekIn.Start();};
        MouseLeave+=(_,_)=>{peekIn.Stop();if(peek)peekOut.Start();};
        peekIn.Tick+=(_,_)=>{peekIn.Stop();if(!externalDropHover&&!gesture.Active&&IsMouseOver&&lifecycle.Gate.TryPeek(true)){peek=true;OpenPresentationToolbar();StartFrames();}};
        peekOut.Tick+=(_,_)=>{peekOut.Stop();if(!externalDropHover&&peek&&!IsMouseOver&&!IsKeyboardFocusWithin&&!overflowMenu.IsOpen&&button.ContextMenu?.IsOpen!=true&&lifecycle.Gate.TryPeek(false)){peek=false;overlayFrames?.Collapse();StartFrames();}};
        copyReset.Tick+=(_,_)=>{copyReset.Stop();ExpireCopyFeedback();};Closed+=(_,_)=>copyReset.Stop();
        flush.Tick+=FlushResultTick;
        waiting.Tick+=(_,_)=>{waiting.Stop();if(overlayResults.Identity is {} id&&lifecycle.Gate.CanApply(id)&&!overlayResults.HasText&&!gesture.Active&&lifecycle.Gate.TryRevealDelta(id)){peek=true;OpenPresentationToolbar();button.ToolTip=connectionStatus.Text;StartFrames();}};
    }
    private void UpdateButtonGlyph(bool on)
    {
        // Linear monochrome vector; both shape and accessible name identify state.
        var glyph=new Path{Width=20,Height=20,Stretch=Stretch.Uniform,Fill=SystemParameters.HighContrast?SystemColors.HighlightTextBrush:Brushes.White,
            Data=Geometry.Parse(on?"M 2,1 L 8,1 L 8,19 L 2,19 Z M 12,1 L 18,1 L 18,19 L 12,19 Z":"M 4,1 L 18,10 L 4,19 Z")};button.Content=glyph;
        if(SystemParameters.HighContrast)button.Background=SystemColors.HighlightBrush;
    }
    private bool ReduceMotion=>lifecycle.Settings?.Data.Overlay.ReduceMotion==MotionPreference.Reduce||lifecycle.Settings?.Data.Overlay.ReduceMotion!=MotionPreference.Full&&!SystemParameters.ClientAreaAnimation;
    private void RefreshPresentationTheme()
    {
        DesktopTheme.Apply(this,lifecycle.Settings?.Data.Overlay.Theme??ThemePreference.FollowSystem);
        DesktopControlFeedback.Apply(this,lifecycle.Settings?.Data.Overlay.ReduceMotion??MotionPreference.FollowSystem);
        cardBackground.Background=DesktopTheme.Brush(this,DesktopTheme.Surface);cardBackground.BorderBrush=DesktopTheme.Brush(this,DesktopTheme.Stroke);body.Foreground=DesktopTheme.Brush(this,DesktopTheme.Text);measurement.Foreground=body.Document.Foreground=measurement.Document.Foreground=body.Foreground;connectionStatus.Foreground=resultLabel.Foreground=DesktopTheme.Brush(this,DesktopTheme.Muted);
        button.Foreground=SystemParameters.HighContrast?SystemColors.HighlightTextBrush:Brushes.White;bool on=lifecycle.Gate.Snapshot.Activation==Activation.On;button.Background=on?OverlayTokens.On:OverlayTokens.Off;UpdateButtonGlyph(on);
        progressRing.Stroke=button.Foreground;
        bodyMarkdown.RefreshCodeAppearance();measurementMarkdown.RefreshCodeAppearance();RefreshTextScalePresentation();if(overflowMenu.IsOpen)DesktopTheme.ApplyMenu(overflowMenu,this);if(button.ContextMenu?.IsOpen==true)DesktopTheme.ApplyMenu(button.ContextMenu,this);
    }
    private void PresentationMenuClosed(){if(peek&&!IsMouseOver&&!exiting)peekOut.Start();}
    private void OpenOverflowMenu(UIElement target)
    {
        peekOut.Stop();overflowMenu.Items.Clear();
        // Overflow complements the visible rail. It does not repeat every command,
        // or mix persistent input-receiver switches into a reading menu.
        AddHiddenTools("回看","复制","收起","查看原文");
        AddHiddenTools(lifecycle.Gate.HasRunningGeneration?"停止":"重新生成");
        if(overflowMenu.Items.Count>0)overflowMenu.Items.Add(new Separator());
        AddOpenImageCommands(overflowMenu,includeDropCommands:false);
        if(SelectedSource.Image!=null){AddScreenshotModes(overflowMenu);AddImageRegenerationCommands(overflowMenu);AddScreenshotRecovery(overflowMenu);}
        overflowMenu.Items.Add(new Separator());AddSessionStyleMenu(overflowMenu);
        var versionsItem=new MenuItem{Header="本次来源版本（仅内存）"};AddVersionItems(versionsItem.Items);
        versionsItem.IsEnabled=versionsItem.Items.Count>0;overflowMenu.Items.Add(new Separator());overflowMenu.Items.Add(versionsItem);
        var currentSettings=new MenuItem{Header=RegenerationMenuLabel(true),IsEnabled=lifecycle.Gate.Snapshot.Activation==Activation.On&&(SelectedSource.Text!=null||CanRegenerateImage)};currentSettings.Click+=(_,_)=>RegenerateResult(true);overflowMenu.Items.Add(currentSettings);
        overflowMenu.Items.Add(new Separator());AddHiddenTools("设置","清除当前内容");
        overflowMenu.PlacementTarget=target;DesktopTheme.ApplyMenu(overflowMenu,this);overflowMenu.IsOpen=true;
        void AddHiddenTools(params string[] commands)
        {
            foreach(string command in commands)
            {
                int index=tools.FindIndex(t=>Equals(t.Tag,command));var tool=tools[index];
                if(tool.IsVisible)continue;
                var item=new MenuItem{Header=tool.Content,IsEnabled=command=="复制"?CopyVersion()!=null:tool.IsEnabled};
                item.Click+=(_,_)=>toolActions[index]();overflowMenu.Items.Add(item);
            }
        }
    }
    private void OpenStyleMenu()
    {peekOut.Stop();overflowMenu.Items.Clear();AddSessionStyleMenu(overflowMenu);overflowMenu.PlacementTarget=styleTool;DesktopTheme.ApplyMenu(overflowMenu,this);overflowMenu.IsOpen=true;}
    private void AddVersionItems(ItemCollection items)
    {
        if(LocalImagePreview){var local=new MenuItem{Header="本地图片 · 尚未发送",IsCheckable=true,IsChecked=true};local.Click+=(_,_)=>ReopenResult();items.Add(local);return;}
        foreach(var version in overlayResults.Versions)
        {
            Guid id=version.Identity.VersionId;var item=new MenuItem{Header=VersionMenuLabel(version),IsCheckable=true,IsChecked=overlayResults.SelectedVersionId==id};
            item.Click+=(_,_)=>SelectResultVersion(id);items.Add(item);
        }
    }
    private void OpenVersionMenu()
    {peekOut.Stop();overflowMenu.Items.Clear();AddVersionItems(overflowMenu.Items);overflowMenu.PlacementTarget=versionsTool;DesktopTheme.ApplyMenu(overflowMenu,this);overflowMenu.IsOpen=true;}
    private void WriteResultClipboard(string text)=>Clipboard.SetText(text);
    private void StopPresentation(){overlayResults.MarkStopped();awaitingText=false;progressRing.Visibility=Visibility.Collapsed;CollapseResult();}
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
        if(TryRevealFailure()){peek=false;OpenBody();}
        else if(reveal&&overlayResults.Identity is {} id&&overlayResults.TryAutoReveal(id)){peek=false;OpenBody();}
        else if(peek)OpenPresentationToolbar();else if(overlayFrames.IsExpanded)OpenBody();else overlayFrames.EndMove(Preferences());StartFrames();
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
    private void StartFrames(){if(rendering||!awaitingText&&overlayFrames?.NeedsFrame!=true||!IsVisible||exiting)return;frameClock.Resume();rendering=true;CompositionTarget.Rendering+=RenderOverlay;}
    private void StopFrames(){if(!rendering)return;CompositionTarget.Rendering-=RenderOverlay;rendering=false;}
    private void RenderOverlay(object? sender,EventArgs e)
    {
        // HwndTarget can synchronously raise Rendering inside SetWindowPos. Keep the prepared frame frozen.
        if(applyingOverlayFrame)return;
        try
        {if(exiting||!IsVisible||overlayFrames==null){StopFrames();return;}if(awaitingText&&overlayResults.Identity is {} id&&!lifecycle.Gate.CanApply(id)){awaitingText=false;progressRing.Visibility=Visibility.Collapsed;waiting.Stop();overlayResults.MarkStopped();}var tick=frameClock.Tick(overlayFrames,ReduceMotion);progressRing.RenderTransform=ReduceMotion?Transform.Identity:new RotateTransform(tick.Now.TotalMilliseconds*.24);ApplyOverlayFrame();if(!awaitingText&&!overlayFrames.NeedsFrame)StopFrames();}
        catch(Exception error){StopFrames();lifecycle.Gate.Disable();Report("浮窗几何更新失败",error);}
    }
    private void ApplyOverlayFrame(bool forceNative=false)
    {
        if(overlayFrames==null||hwnd==0)return;
        anchor=overlayFrames.Anchor;anchorMonitor=overlayFrames.Monitor;var rect=overlayFrames.Current;var dpi=anchorMonitor.Dpi;
        bool expanded=rect!=anchor;
        double expansion=overlayFrames.CornerProgress;
        int radius=dpi.CeilingPixels(24-18*expansion);
        applyingOverlayFrame=true;
        try
        {
            PrepareVisuals();
            CommitLayout();
            // Resize may render before ApplyBounds returns: the visual tree must already describe this rectangle.
            if(forceNative||appliedRect!=rect||appliedRadius!=radius||appliedVisible!=IsVisible){NativeDesktop.ApplyBounds(hwnd,rect,radius,show:IsVisible);appliedRect=rect;appliedRadius=radius;appliedVisible=IsVisible;}
            CommitLayout();
        }
        finally{applyingOverlayFrame=false;}

        void CommitLayout()
        {
            if(overlayCanvas.IsLoaded&&(!overlayCanvas.IsMeasureValid||!overlayCanvas.IsArrangeValid))overlayCanvas.UpdateLayout();
        }
        void PrepareVisuals()
        {
        // Match the already-quantized native clip before a resize can synchronously render.
        var corner=new CornerRadius(dpi.Dips(radius));if(cardBackground.CornerRadius!=corner)cardBackground.CornerRadius=corner;
        double width=dpi.Dips(rect.Width),height=dpi.Dips(rect.Height);overlayCanvas.Width=width;overlayCanvas.Height=height;cardBackground.Width=width;cardBackground.Height=height;
        if(overlayCanvas.Clip is not RectangleGeometry clip||clip.Rect.Width!=width||clip.Rect.Height!=height||clip.RadiusX!=corner.TopLeft)
            overlayCanvas.Clip=new RectangleGeometry(new Rect(0,0,width,height),corner.TopLeft,corner.TopLeft);
        double bx=dpi.Dips(anchor.Left-rect.Left),by=dpi.Dips(anchor.Top-rect.Top);Canvas.SetLeft(button,bx);Canvas.SetTop(button,by);
        Canvas.SetLeft(progressRing,bx+3);Canvas.SetTop(progressRing,by+3);
        controls.Visibility=expanded&&!externalDropHover?Visibility.Visible:Visibility.Collapsed;controls.Width=toolbarSurface.Width=toolbarSeam.Width=width;
        double targetCardWidth=overlayFrames.Plan is {} toolbarPlan?dpi.Dips(toolbarPlan.Target.Width):width;
        double targetAnchorX=overlayFrames.Plan is {} targetPlan?dpi.Dips(anchor.Left-targetPlan.Target.Left):bx;
        double targetOrigin=overlayFrames.Plan is {} placement?dpi.Dips(placement.Target.Left-rect.Left):0;
        bool left=targetAnchorX>targetCardWidth-targetAnchorX-48;
        double available=Math.Max(targetAnchorX,targetCardWidth-targetAnchorX-48),start=targetOrigin+(left?0:targetAnchorX+48);
        var toolbarLayout=OverlayReadingToolbar.Responsive(Math.Max(targetAnchorX,targetCardWidth-targetAnchorX-48),targetCardWidth,!peek||awaitingText,lifecycle.Gate.HasRunningGeneration,touchToolbar,PresentationTextScale);
        var toolbar=toolbarLayout.Slots;
        double rowUsed=toolbar.Where(slot=>slot.Row==0).Select(slot=>slot.OffsetDip+slot.WidthDip).DefaultIfEmpty(0).Max();
        double actionGap=!peek&&!awaitingText&&toolbar.Any(slot=>slot.Row==0&&slot.Command=="风格")?Math.Max(0,available-rowUsed):0;
        bool toolsDown=overlayFrames.ContentDirection is not (GrowthDirection.RightUp or GrowthDirection.LeftUp or GrowthDirection.BalancedUp);
        tools.First(t=>Equals(t.Tag,"收起")).ContentTemplate=toolsDown?CollapseUpGlyph:CollapseDownGlyph;
        double edge=toolbarLayout.ExtraHeightDip;controls.Height=toolbarSurface.Height=48+edge;Canvas.SetTop(controls,toolsDown?by:by-edge);
        toolbarSeam.Background=toolsDown?DownSeam:UpSeam;Canvas.SetTop(toolbarSeam,toolsDown?48+edge:-3);
        foreach(var tool in tools)tool.Visibility=Visibility.Collapsed;
        moreTools.Visibility=Visibility.Collapsed;
        foreach(var slot in toolbar)
        {
            var tool=slot.Command=="更多"?moreTools:tools.First(t=>Equals(t.Tag,slot.Command));
            tool.Visibility=Visibility.Visible;tool.Width=slot.WidthDip;tool.Height=touchToolbar?44:Math.Max(28,Math.Ceiling(18*PresentationTextScale+4));
            KeyboardNavigation.SetTabIndex(tool,slot.Row*1000+(int)slot.OffsetDip);
            double rowTop=toolsDown?slot.Row*48:edge-slot.Row*48;
            // Like the prose, controls occupy their final layout and are revealed
            // by the growing native clip, rather than sliding with the left edge.
            Canvas.SetLeft(tool,slot.Row==0?start+slot.OffsetDip+(slot.Command=="风格"?0:actionGap):targetOrigin+slot.OffsetDip);Canvas.SetTop(tool,rowTop+(48-tool.Height)/2);
        }
        double used=toolbar.Where(slot=>slot.Row==0).Select(slot=>slot.OffsetDip+slot.WidthDip).DefaultIfEmpty(0).Max();
        connectionStatus.Visibility=!externalDropHover&&awaitingText&&peek&&expanded&&available-used>=32?Visibility.Visible:Visibility.Collapsed;
        connectionStatus.Width=Math.Max(0,available-used);connectionStatus.TextTrimming=TextTrimming.CharacterEllipsis;
        Canvas.SetLeft(connectionStatus,start+used);Canvas.SetTop(connectionStatus,by+14);
        PublishDropBounds();
        var plan=overlayFrames.Plan;var direction=overlayFrames.ContentDirection;toolbarDivider.Visibility=toolbarSeam.Visibility=Visibility.Collapsed;if(direction==null||peek||!expanded){body.Visibility=resultLabel.Visibility=versionsTool.Visibility=failureNotice.Visibility=Visibility.Collapsed;newContent.Visibility=Visibility.Collapsed;return;}
        bool down=direction is GrowthDirection.RightDown or GrowthDirection.LeftDown or GrowthDirection.BalancedDown;
        toolbarDivider.Width=width;Canvas.SetTop(toolbarDivider,down?47+edge:0);toolbarDivider.Visibility=toolbarSeam.Visibility=Visibility.Visible;
        int pad=dpi.CeilingPixels(16),top=down?anchor.Bottom+dpi.CeilingPixels(edge)+pad:rect.Top+pad,bottom=down?rect.Bottom-pad:anchor.Top-dpi.CeilingPixels(edge)-pad;
        double targetWidth=overlayFrames.TextWidthDip;
        if(overlayFrames.Transition==OverlayTransition.Moving)targetWidth=Math.Min(targetWidth,width);
        int targetLeft=plan?.Target.Left??(direction is GrowthDirection.RightDown or GrowthDirection.RightUp?anchor.Left:direction is GrowthDirection.LeftDown or GrowthDirection.LeftUp?anchor.Right-dpi.CeilingPixels(targetWidth):anchor.Center.X-dpi.CeilingPixels(targetWidth)/2);
        double labelHeight=resultLabel.Text.Length>0?PresentationFooterHeightDip:0,noticeHeight=FailureNoticeHeight;
        SetMarkdownViewport(plan is not null?MarkdownViewport.FromPlan(plan,labelHeight>0,noticeHeight,edge,PresentationFooterHeightDip):MarkdownViewport.FromCard(targetWidth,height,labelHeight>0,noticeHeight,edge,PresentationFooterHeightDip));
        Canvas.SetLeft(resultLabel,dpi.Dips(targetLeft+pad-rect.Left));Canvas.SetTop(resultLabel,dpi.Dips(bottom-rect.Top)-labelHeight);
        Canvas.SetLeft(versionsTool,dpi.Dips(targetLeft-rect.Left)+targetWidth-16-Math.Min(targetWidth/2,76*PresentationTextScale));Canvas.SetTop(versionsTool,dpi.Dips(bottom-rect.Top)-labelHeight);versionsTool.Width=Math.Min(targetWidth/2,76*PresentationTextScale);versionsTool.Height=labelHeight;versionsTool.Visibility=labelHeight>0&&bottom-top>dpi.CeilingPixels(labelHeight)?Visibility.Visible:Visibility.Collapsed;versionsTool.Opacity=overlayFrames.ContentOpacity;resultLabel.Width=Math.Max(1,targetWidth-48-versionsTool.Width);resultLabel.Height=labelHeight;resultLabel.Opacity=overlayFrames.ContentOpacity;resultLabel.Visibility=labelHeight>0&&bottom-top>dpi.CeilingPixels(labelHeight)?Visibility.Visible:Visibility.Collapsed;
        Canvas.SetLeft(failureNotice,dpi.Dips(targetLeft+pad-rect.Left));Canvas.SetTop(failureNotice,dpi.Dips(top-rect.Top));failureNotice.Height=Math.Max(28,noticeHeight-4);failureNotice.Width=Math.Max(1,targetWidth-32);failureNotice.Opacity=overlayFrames.ContentOpacity;failureNotice.Visibility=noticeHeight>0&&bottom-top>dpi.CeilingPixels(labelHeight+noticeHeight)?Visibility.Visible:Visibility.Collapsed;
        Canvas.SetLeft(body,dpi.Dips(targetLeft+pad-rect.Left));Canvas.SetTop(body,dpi.Dips(top-rect.Top)+noticeHeight);body.Width=Math.Max(1,targetWidth-32);body.Height=Math.Max(0,dpi.Dips(bottom-top)-labelHeight-noticeHeight);body.Opacity=overlayFrames.ContentOpacity;body.Visibility=body.Height>0?Visibility.Visible:Visibility.Collapsed;
        newContent.Content=UnreadNoticeLabel;newContent.Width=Math.Min(Math.Max(0,width-32),Math.Ceiling(100*PresentationTextScale));
        Canvas.SetLeft(newContent,Math.Max(0,width-16-newContent.Width));Canvas.SetTop(newContent,Math.Max(0,dpi.Dips(bottom-rect.Top)-labelHeight-32));
        }
    }
    private readonly DispatcherTimer copyReset=new(){Interval=TimeSpan.FromSeconds(1.5)};
    partial void InvalidateMarkdownMeasurement()
    {
        measurement.ApplyTemplate();
        Invalidate(measurement);
        static void Invalidate(DependencyObject node)
        {
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Invalidate(VisualTreeHelper.GetChild(node,i));
            if(node is UIElement element)element.InvalidateMeasure();
        }
    }
    private void ScheduleCopyFeedbackReset(){copyReset.Stop();copyReset.Start();}
    private void DisposePresentation(){copyReset.Stop();StopFrames();flush.Stop();peekIn.Stop();peekOut.Stop();waiting.Stop();try{ClearMarkdown();}finally{ReleaseRetainedImage();overlayResults.Clear();}}
}
