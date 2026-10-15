// Native control property/event-call ports only; no WPF expansion, geometry or input routing.
namespace System.Windows.Controls;
internal sealed class Expander:FrameworkElement
{
    public object? Header {get;set;}
    public object? Content {get;set;}
    public Media.Brush? Foreground {get;set;}
    public double FontSize {get;set;}
    private bool expanded;
    public bool IsExpanded {get=>expanded;set{if(value==expanded)return;expanded=value;if(value)Expanded?.Invoke(this,new());else Collapsed?.Invoke(this,new());}}
    public event EventHandler<RoutedEventArgs>? Expanded,Collapsed;
    internal Action CaptureExpand(){var callback=Expanded;return()=>callback?.Invoke(this,new());}
    internal int Handlers=>(Expanded?.GetInvocationList().Length??0)+(Collapsed?.GetInvocationList().Length??0);
}
