using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace KuaiYi.App;

/// <summary>Exact retained source, displayed only on explicit expansion as inert native text.</summary>
internal sealed class MarkdownSourceRegion:IDisposable
{
    private string? source;
    private Action? observeReader,changed;
    private Func<bool>? canRead;
    private readonly Func<Func<bool>>? captureReaderGuard;
    private long revealRevision;
    private bool updating,disposed;
    private readonly ScrollEventHandler scrollHandler;
    public Expander View {get;}=new(){Header="查看原文",IsExpanded=false,ClipToBounds=true};
    public TextBox Text {get;}=new()
    {
        IsReadOnly=true,IsUndoEnabled=false,IsReadOnlyCaretVisible=true,AllowDrop=false,AcceptsReturn=true,
        TextWrapping=TextWrapping.Wrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,
        BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,ClipToBounds=true,
        FontFamily=new FontFamily("Segoe UI"),FontWeight=FontWeights.Normal,FontStyle=FontStyles.Normal
    };
    public MarkdownSourceRegion(string source,Action? observeReader,Action? changed,Func<bool>? canRead,Func<Func<bool>>? captureReaderGuard=null)
    {
        this.source=source;this.observeReader=observeReader;this.changed=changed;this.canRead=canRead;this.captureReaderGuard=captureReaderGuard;View.Content=Text;
        System.Windows.Automation.AutomationProperties.SetName(View,"查看所示版本原文");
        System.Windows.Automation.AutomationProperties.SetName(Text,"所示版本原文（未经格式渲染）");
        View.Expanded+=Expanded;View.Collapsed+=Collapsed;
        Text.PreviewMouseWheel+=Wheel;Text.PreviewMouseDown+=MouseDown;Text.PreviewMouseMove+=MouseMove;Text.PreviewKeyDown+=KeyDown;
        Text.PreviewTouchDown+=Touch;Text.PreviewTouchMove+=Touch;scrollHandler=Scroll;Text.AddHandler(ScrollBar.ScrollEvent,scrollHandler,true);
    }
    public bool Matches(string value)=>source==value;
    public void Constrain(double width,double height,double size,Brush foreground)
    {View.Width=View.MaxWidth=width;Text.Width=Text.MaxWidth=width;Text.MaxHeight=Math.Min(180,Math.Max(1,height/2));Text.FontSize=size;Text.Foreground=foreground;View.Foreground=foreground;View.FontSize=size;}
    public void SetExpanded(bool expanded)
    {
        if(disposed)return;
        bool Current()=>!disposed&&canRead?.Invoke()!=false;
        updating=true;try
        {
            View.IsExpanded=expanded&&Current();
            Text.Text=Current()&&View.IsExpanded?source??"":"";
            if(!Current()){View.IsExpanded=false;Text.Clear();}
        }
        finally{updating=false;}
    }
    public void Expand()
    {
        if(disposed||canRead?.Invoke()==false)return;
        View.IsExpanded=true;
        // Only the explicit command requests a reading target. Measurement/streaming do not.
        observeReader?.Invoke();long current=++revealRevision;var guard=captureReaderGuard?.Invoke();
        if(guard==null)return;
        Text.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {
            if(disposed||current!=revealRevision||!View.IsExpanded||canRead?.Invoke()==false||!guard())return;
            observeReader?.Invoke(); // Fence an outer flush restore queued since the command.
            View.BringIntoView();
        });
    }
    private void Expanded(object? sender,RoutedEventArgs e)
    {
        if(disposed||updating)return;
        if(canRead?.Invoke()==false){SetExpanded(false);return;}
        Text.Text=source??"";observeReader?.Invoke();changed?.Invoke();
    }
    private void Collapsed(object? sender,RoutedEventArgs e)
    {if(disposed||updating)return;revealRevision++;Text.Clear();observeReader?.Invoke();changed?.Invoke();}
    private void Observe(){if(!disposed&&canRead?.Invoke()!=false)observeReader?.Invoke();}
    private void Wheel(object? sender,MouseWheelEventArgs e)=>Observe();
    private void MouseDown(object? sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left)Observe();}
    private void MouseMove(object? sender,MouseEventArgs e){if(e.LeftButton==MouseButtonState.Pressed)Observe();}
    private void Touch(object? sender,TouchEventArgs e)=>Observe();
    private void KeyDown(object? sender,KeyEventArgs e)
    {if(e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End||e.Key==Key.A&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control))Observe();}
    private void Scroll(object? sender,ScrollEventArgs e)=>Observe();
    public void Dispose()
    {
        if(disposed)return;disposed=true;revealRevision++;source=null;observeReader=changed=null;canRead=null;
        View.Expanded-=Expanded;View.Collapsed-=Collapsed;View.IsExpanded=false;
        Text.PreviewMouseWheel-=Wheel;Text.PreviewMouseDown-=MouseDown;Text.PreviewMouseMove-=MouseMove;Text.PreviewKeyDown-=KeyDown;
        Text.PreviewTouchDown-=Touch;Text.PreviewTouchMove-=Touch;Text.RemoveHandler(ScrollBar.ScrollEvent,scrollHandler);
        Text.Clear();Text.Select(0,0);Text.ScrollToHorizontalOffset(0);Text.ScrollToVerticalOffset(0);View.Content=null;
    }
}
