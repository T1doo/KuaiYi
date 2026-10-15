// Data/delegate ports for the linked production table region. No WPF layout, selection or routed-input claim.
namespace System.Windows.Controls
{
    internal sealed partial class RichTextBox
    {
        public RichTextBox()=>Selection=new(()=>Document);
        public bool IsReadOnlyCaretVisible {get;set;}
        public bool AllowDrop {get;set;}
        public Thickness BorderThickness {get;set;}
        public Thickness Padding {get;set;}
        public Media.Brush? Background {get;set;}
        public Threading.CodeDispatcherPort Dispatcher {get;}=new();
        public Documents.TextSelection Selection {get;}
        public double HorizontalOffset {get;private set;}
        public double VerticalOffset {get;private set;}
        public double ExtentHeight {get;set;}=1200;
        public double ViewportHeight {get;set;}=200;
        public int EndCalls {get;private set;}
        public void ScrollToEnd(){VerticalOffset=Math.Max(0,ExtentHeight-ViewportHeight);EndCalls++;}
        public void ScrollToHorizontalOffset(double value)=>HorizontalOffset=value;
        public void ScrollToVerticalOffset(double value)=>VerticalOffset=value;
        public event EventHandler<Input.MouseWheelEventArgs>? PreviewMouseWheel;
        public event EventHandler<Input.MouseButtonEventArgs>? PreviewMouseDown;
        public event EventHandler<Input.MouseEventArgs>? PreviewMouseMove;
        public event EventHandler<Input.KeyEventArgs>? PreviewKeyDown;
        public event EventHandler<TouchEventArgs>? PreviewTouchDown,PreviewTouchMove;
        private Primitives.ScrollEventHandler? scroll;
        public void AddHandler(object routedEvent,Delegate handler,bool handledEventsToo)=>scroll+=(Primitives.ScrollEventHandler)handler;
        public void RemoveHandler(object routedEvent,Delegate handler)=>scroll-=(Primitives.ScrollEventHandler)handler;
        internal int ReaderHandlerCount=>(PreviewMouseWheel?.GetInvocationList().Length??0)+(PreviewMouseDown?.GetInvocationList().Length??0)+(PreviewMouseMove?.GetInvocationList().Length??0)+(PreviewKeyDown?.GetInvocationList().Length??0)+(PreviewTouchDown?.GetInvocationList().Length??0)+(PreviewTouchMove?.GetInvocationList().Length??0)+(scroll?.GetInvocationList().Length??0);
        internal void RaiseWheel()=>PreviewMouseWheel?.Invoke(this,new());
        internal void RaiseMouse(Input.MouseButton button)=>PreviewMouseDown?.Invoke(this,new(){ChangedButton=button});
        internal void RaiseMove(Input.MouseButtonState button)=>PreviewMouseMove?.Invoke(this,new(){LeftButton=button});
        internal void RaiseKey(Input.Key key)=>PreviewKeyDown?.Invoke(this,new(){Key=key});
        internal void RaiseTouch()=>PreviewTouchDown?.Invoke(this,new());
        internal void RaiseTouchMove()=>PreviewTouchMove?.Invoke(this,new());
        internal void RaiseScroll()=>scroll?.Invoke(this,new());
        internal Action CaptureWheel(){var handler=PreviewMouseWheel;return()=>handler?.Invoke(this,new());}
    }
}
