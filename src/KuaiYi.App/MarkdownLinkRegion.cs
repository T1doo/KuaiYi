using System.Windows;
using System.Windows.Documents;
using KuaiYi.Core;
namespace KuaiYi.App;

/// <summary>No NavigateUri or automatic navigation. A live explicit Click is the only action.</summary>
internal sealed class MarkdownLinkRegion:IDisposable
{
    private MarkdownHttpTarget? target;
    private Action? observeReader;
    private Action<MarkdownHttpTarget,Func<bool>>? activate;
    private Func<bool>? current;
    private bool disposed;
    public Hyperlink View {get;}
    public MarkdownLinkRegion(string text,MarkdownHttpTarget target,Action? observeReader,Action<MarkdownHttpTarget,Func<bool>>? activate,Func<bool> current)
    {
        this.target=target;this.observeReader=observeReader;this.activate=activate;this.current=current;
        View=new(new Run(text)){ToolTip="目标域名："+target.Domain+"\n"+target.Url};
        if(activate!=null)View.Click+=Click;
    }
    private bool IsCurrent()=>!disposed&&current?.Invoke()==true;
    private void Click(object? sender,RoutedEventArgs args)
    {args.Handled=true;if(!IsCurrent()||target==null)return;observeReader?.Invoke();activate?.Invoke(target,IsCurrent);}
    public void Dispose()
    {if(disposed)return;disposed=true;View.Click-=Click;View.Inlines.Clear();View.ToolTip=null;target=null;observeReader=null;activate=null;current=null;}
}
