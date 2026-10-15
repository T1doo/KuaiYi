// Data/event-delegate ports only. They do not simulate routed events, WPF scrolling or selection.
namespace System.Windows.Input
{
    internal enum Key { Up, Down, Left, Right, PageUp, PageDown, Home, End, A, C }
    internal enum MouseButton { Left, Right }
    internal enum MouseButtonState { Released, Pressed }
    [Flags]internal enum ModifierKeys { None=0, Control=1 }
    internal static class Keyboard {public static ModifierKeys Modifiers {get;set;}}
    internal class MouseEventArgs:EventArgs {public MouseButtonState LeftButton {get;init;}}
    internal sealed class MouseWheelEventArgs:MouseEventArgs;
    internal sealed class MouseButtonEventArgs:MouseEventArgs {public MouseButton ChangedButton {get;init;}}
    internal sealed class KeyEventArgs:EventArgs {public Key Key {get;init;}}
}
namespace System.Windows.Controls.Primitives
{
    internal sealed class ScrollEventArgs:EventArgs;
    internal delegate void ScrollEventHandler(object sender,ScrollEventArgs e);
    internal static class ScrollBar {public static readonly object ScrollEvent=new();}
}
namespace System.Windows.Controls
{
    internal sealed class TextBox:FrameworkElement
    {
        public bool IsReadOnly {get;set;}
        public bool IsUndoEnabled {get;set;}
        public bool IsReadOnlyCaretVisible {get;set;}
        public bool AllowDrop {get;set;}
        public bool AcceptsReturn {get;set;}
        public TextWrapping TextWrapping {get;set;}
        public ScrollBarVisibility HorizontalScrollBarVisibility {get;set;}
        public ScrollBarVisibility VerticalScrollBarVisibility {get;set;}
        public Thickness BorderThickness {get;set;}
        public Thickness Padding {get;set;}
        public Media.Brush? Background {get;set;}
        public Media.Brush? Foreground {get;set;}
        public Media.FontFamily? FontFamily {get;set;}
        public string? FontWeight {get;set;}
        public string? FontStyle {get;set;}
        public double FontSize {get;set;}
        private string text="";
        public string Text {get=>text;set{text=value;Select(0,0);HorizontalOffset=VerticalOffset=0;}}
        public void AppendText(string value)=>Text+=value;
        public Threading.CodeDispatcherPort Dispatcher {get;}=new();
        public int SelectionStart {get;private set;}
        public int SelectionLength {get;private set;}
        public double HorizontalOffset {get;private set;}
        public double VerticalOffset {get;private set;}
        public event EventHandler<Input.MouseWheelEventArgs>? PreviewMouseWheel;
        public event EventHandler<Input.MouseButtonEventArgs>? PreviewMouseDown;
        public event EventHandler<Input.MouseEventArgs>? PreviewMouseMove;
        public event EventHandler<Input.KeyEventArgs>? PreviewKeyDown;
        public event EventHandler<TouchEventArgs>? PreviewTouchDown,PreviewTouchMove;
        private Primitives.ScrollEventHandler? scroll;
        public void AddHandler(object routedEvent,Delegate handler,bool handledEventsToo)=>scroll+=(Primitives.ScrollEventHandler)handler;
        public void RemoveHandler(object routedEvent,Delegate handler)=>scroll-=(Primitives.ScrollEventHandler)handler;
        public void Clear()=>Text="";
        public void Select(int start,int length){SelectionStart=start;SelectionLength=length;}
        public void ScrollToHorizontalOffset(double value)=>HorizontalOffset=value;
        public void ScrollToVerticalOffset(double value)=>VerticalOffset=value;
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
namespace System.Windows.Threading
{
    internal sealed class CodeDispatcherPort
    {
        private readonly List<Action> callbacks=[];
        public int Pending=>callbacks.Count;
        public void BeginInvoke(DispatcherPriority priority,Action action)=>callbacks.Add(action);
        public void Deliver(int index=0){var action=callbacks[index];callbacks.RemoveAt(index);action();}
    }
}
