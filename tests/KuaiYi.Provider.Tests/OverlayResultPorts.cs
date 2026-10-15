// Linux UI call/data ports only. The linked App partials own Stop/Apply/timer tick/Flush/label logic.
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using System.Windows;
using System.Windows.Controls;
namespace System.Windows.Controls
{
    internal sealed class Button:FrameworkElement
    {public object? Content {get;set;}public object? ToolTip {get;set;}public bool IsEnabled {get;set;}=true;}
    internal sealed class TextBlock:FrameworkElement
    {public string Text {get;set;}="";public object? ToolTip {get;set;}}
}
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        private sealed class TimerPort
        {
            public event EventHandler? Tick;
            public bool IsEnabled {get;private set;}
            public int Starts {get;private set;}
            public void Start(){IsEnabled=true;Starts++;}
            public void Stop()=>IsEnabled=false;
            public void Fire(){if(IsEnabled)Tick?.Invoke(this,EventArgs.Empty);}
        }
        private sealed class InlineDestination:SynchronizationContext
        {public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
        private IdentityEventReceiver<OverlayResultEvent> presentationReceiver=null!;
        private readonly TimerPort flush=new(),waiting=new(),peekIn=new(),peekOut=new();
        private readonly BadgePort progressRing=new();
        private readonly Button button=new();
        private readonly TextBlock resultLabel=new();
        private readonly Button? styleTool=new(){Content="风格"};
        private readonly List<Button> tools=[new(){Content="重新生成"}];
        private TextTranslationCoordinator? translation=null;
        private OverlayFrames? overlayFrames=null;
        private readonly OverlayPointerGesture gesture=new();
        private bool dirty,peek,awaitingText,pendingReveal;
        private void StartFrames()=>FrameRequests++;
        private void ApplyOverlayFrame()=>FrameApplications++;
        private void Refresh()=>RefreshResultLabel();
        internal void InitializeResultFlow()
        {InitializeMarkdown();presentationReceiver=new(lifecycle.Gate,new InlineDestination());flush.Tick+=FlushResultTick;}
        internal void ResultDestination(SynchronizationContext destination)=>presentationReceiver=new(lifecycle.Gate,destination);
        internal void UseTranslation(TextTranslationCoordinator value,SettingsSnapshot settings){translation=value;lifecycle.Settings=settings;}
        internal void CoordinatorUpdate(TextTranslationUpdate update){ApplyResult(update.Identity,update.Presentation,true);RefreshResultLabel();}
        internal void RefreshLabelForTest()=>RefreshResultLabel();
        internal void DrainResultDispatchForTest(){while(Dispatcher.Pending>0)Dispatcher.Run(0);}
        internal void StopForTest()=>StopResult();
        internal void TickForTest()=>flush.Fire();
        internal void CollapseForTest()=>CollapseResult();
        internal bool FlushPending=>flush.IsEnabled;
        internal int FlushStarts=>flush.Starts;
        internal bool Dirty=>dirty;
        internal bool Waiting=>waiting.IsEnabled;
        internal bool Awaiting=>awaitingText;
        internal bool ProgressVisible=>progressRing.Visibility==Visibility.Visible;
        internal string ResultLabel=>resultLabel.Text;
        internal string? ResultTip=>resultLabel.ToolTip as string;
        internal object? ButtonTip=>button.ToolTip;
        internal long ResultRevision=>resultRevision;
        internal bool PendingReveal=>pendingReveal;
        internal int FrameRequests {get;private set;}
        internal int FrameApplications {get;private set;}
        internal OverlayFrames? Frames=>overlayFrames;
        internal void WithGeometry()
        {overlayFrames=new(new(100,100,148,148),new("synthetic",new(0,0,1920,1040),new(1)));MarkdownMeasurement.DesiredSize=new(388,320);}
        internal void OpenForTest()=>OpenBody();
    }
}
