using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
namespace KuaiYi.App;

/// <summary>Locally constructed, read-only text only; never interprets the code as markup or commands.</summary>
internal sealed class MarkdownCodeRegion:IDisposable
{
    private readonly Action? observeReader;
    private readonly ScrollEventHandler scrollHandler;
    private readonly Func<Func<bool>>? captureReaderGuard;
    private long revision;
    private (int Start,int Length,double Horizontal,double Vertical)? pendingSelection;
    private Func<bool>? pendingGuard;
    private Func<bool>? shouldFollow;
    private bool isTail,pendingFollow,loadedOnce;
    private bool disposed;
    public TextBox View {get;}=new()
    {
        IsReadOnly=true,IsUndoEnabled=false,IsReadOnlyCaretVisible=true,AllowDrop=false,AcceptsReturn=true,
        TextWrapping=TextWrapping.NoWrap,HorizontalScrollBarVisibility=ScrollBarVisibility.Visible,VerticalScrollBarVisibility=ScrollBarVisibility.Visible,
        BorderThickness=new Thickness(0),Padding=new Thickness(0),Background=Brushes.Transparent,ClipToBounds=true,
        FontFamily=new FontFamily("Consolas"),FontWeight=FontWeights.Normal,FontStyle=FontStyles.Normal
    };
    public MarkdownCodeRegion(string text,Action? observeReader,Func<Func<bool>>? captureReaderGuard)
    {
        this.observeReader=observeReader;this.captureReaderGuard=captureReaderGuard;View.Text=text;
        System.Windows.Automation.AutomationProperties.SetName(View,"代码");
        View.PreviewMouseWheel+=Wheel;View.PreviewMouseDown+=MouseDown;View.PreviewMouseMove+=MouseMove;
        View.PreviewKeyDown+=KeyDown;View.PreviewTouchDown+=Touch;View.PreviewTouchMove+=Touch;
        scrollHandler=Scroll;View.AddHandler(ScrollBar.ScrollEvent,scrollHandler,true);
        View.Loaded+=Loaded;
    }
    public void Constrain(double width,double maximumHeight)
    {View.Width=View.MaxWidth=width;View.MaxHeight=maximumHeight;}
    public void ConfigureTail(bool value,Func<bool>? follow){isTail=value;shouldFollow=follow;}
    private bool CanFollow()=>!disposed&&isTail&&shouldFollow?.Invoke()==true;
    private void Loaded(object? sender,RoutedEventArgs e){if(loadedOnce)return;loadedOnce=true;if(CanFollow())FollowEnd();}
    public void FollowEnd()
    {
        if(!CanFollow()||captureReaderGuard?.Invoke() is not {} guard)return;
        var state=pendingSelection is {} pending&&pendingGuard?.Invoke()==true?pending:(View.SelectionStart,View.SelectionLength,View.HorizontalOffset,View.VerticalOffset);
        Restore(state,true,++revision,guard);
    }
    public void UpdateText(string text)
    {
        if(View.Text==text)return;
        bool coalesced=pendingSelection is not null&&pendingGuard?.Invoke()==true;
        var state=coalesced?pendingSelection!.Value:(View.SelectionStart,View.SelectionLength,View.HorizontalOffset,View.VerticalOffset);
        bool end=CanFollow()&&(coalesced?pendingFollow:View.ExtentHeight-View.ViewportHeight-View.VerticalOffset<=24);
        long current=++revision;
        var canRestore=captureReaderGuard?.Invoke();
        if(text.StartsWith(View.Text,StringComparison.Ordinal))View.AppendText(text[View.Text.Length..]);else View.Text=text;
        if(canRestore is null)return; // Measurement controls have no reader state or deferred work.
        Restore(state,end,current,canRestore);
    }
    private void Restore((int Start,int Length,double Horizontal,double Vertical) state,bool end,long current,Func<bool> canRestore)
    {
        pendingSelection=state;pendingGuard=canRestore;pendingFollow=end;
        View.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {
            if(disposed||current!=revision||!canRestore())return;
            int selected=Math.Min(state.Item1,View.Text.Length);View.Select(selected,Math.Min(state.Item2,View.Text.Length-selected));
            if(end&&CanFollow())View.ScrollToEnd();else View.ScrollToVerticalOffset(state.Item4);
            View.ScrollToHorizontalOffset(state.Item3);pendingSelection=null;pendingGuard=null;pendingFollow=false;
        });
    }
    private void Observe(){if(!disposed){revision++;pendingSelection=null;pendingGuard=null;observeReader?.Invoke();}}
    private void Wheel(object? sender,MouseWheelEventArgs e)=>Observe();
    private void MouseDown(object? sender,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Left)Observe();}
    private void MouseMove(object? sender,MouseEventArgs e){if(e.LeftButton==MouseButtonState.Pressed)Observe();}
    private void Touch(object? sender,TouchEventArgs e)=>Observe();
    private void KeyDown(object? sender,KeyEventArgs e)
    {if(e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.PageUp or Key.PageDown or Key.Home or Key.End||e.Key==Key.A&&Keyboard.Modifiers.HasFlag(ModifierKeys.Control))Observe();}
    private void Scroll(object? sender,ScrollEventArgs e)=>Observe();
    public void Dispose()
    {
        if(disposed)return;disposed=true;revision++;pendingSelection=null;pendingGuard=null;
        View.PreviewMouseWheel-=Wheel;View.PreviewMouseDown-=MouseDown;View.PreviewMouseMove-=MouseMove;
        View.PreviewKeyDown-=KeyDown;View.PreviewTouchDown-=Touch;View.PreviewTouchMove-=Touch;View.RemoveHandler(ScrollBar.ScrollEvent,scrollHandler);
        View.Loaded-=Loaded;shouldFollow=null;
        View.Clear();View.Select(0,0);View.ScrollToHorizontalOffset(0);View.ScrollToVerticalOffset(0);
    }
}
