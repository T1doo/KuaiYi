using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Globalization;
using KuaiYi.Windows;
namespace KuaiYi.App;

// Text metrics only: no LayoutTransform, DPI mutation, activation or content/settings changes.
internal static class DesktopTextScale
{
    private const string StateKey="KuaiYi.TextScale.State";
    internal static readonly DependencyProperty FactorProperty=DependencyProperty.RegisterAttached("Factor",typeof(double),typeof(DesktopTextScale),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.Inherits));
    private static readonly DependencyProperty IgnoreProperty=DependencyProperty.RegisterAttached("Ignore",typeof(bool),typeof(DesktopTextScale),new PropertyMetadata(false));
    internal static double Factor(DependencyObject element)=>(double)element.GetValue(FactorProperty);
    internal static void Ignore(DependencyObject element)=>element.SetValue(IgnoreProperty,true);
    internal static void Font(FrameworkElement element,double baseline)
        =>Bind(element,element is TextBlock?TextBlock.FontSizeProperty:Control.FontSizeProperty,baseline);
    internal static Binding Metric(double baseline)=>new(){RelativeSource=new RelativeSource(RelativeSourceMode.Self),Path=new PropertyPath("(0)",FactorProperty),Converter=new Multiply(baseline)};
    private static void Bind(DependencyObject element,DependencyProperty property,double baseline)
        =>BindingOperations.SetBinding(element,property,Metric(baseline));
    private sealed class Multiply(double baseline):IValueConverter
    {
        public object Convert(object value,Type type,object parameter,CultureInfo culture)=>baseline*(value is double factor?factor:1);
        public object ConvertBack(object value,Type type,object parameter,CultureInfo culture)=>Binding.DoNothing;
    }
    internal static void Install(Window window,Action? changed)
    {
        if(window.Resources.Contains(StateKey))return;
        var state=new ScaleState(window,changed);window.Resources[StateKey]=state;state.Start();
    }
    private sealed class ScaleState(Window window,Action? changed)
    {
        private volatile bool closed;private int queued;private HwndSource? source;
        internal SystemTextScale LastObservation{get;private set;}
        internal void Start()
        {
            Refresh();window.Loaded+=Loaded;window.Closed+=Closed;
            window.SourceInitialized+=SourceReady;Microsoft.Win32.SystemEvents.UserPreferenceChanged+=PreferenceChanged;
            if(new WindowInteropHelper(window).Handle!=0)SourceReady(null,EventArgs.Empty);
        }
        private void SourceReady(object? sender,EventArgs e)
        {source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);source?.AddHook(Hook);}
        private nint Hook(nint hwnd,int message,nint w,nint l,ref bool handled)
        {if(message==0x001a)Queue();return 0;}
        private void PreferenceChanged(object? sender,Microsoft.Win32.UserPreferenceChangedEventArgs e)=>Queue();
        private void Queue()
        {
            if(closed||window.Dispatcher.HasShutdownStarted||Interlocked.Exchange(ref queued,1)!=0)return;
            try{window.Dispatcher.BeginInvoke(()=>{Interlocked.Exchange(ref queued,0);if(!closed)Refresh();});}
            catch(InvalidOperationException)when(window.Dispatcher.HasShutdownStarted){Interlocked.Exchange(ref queued,0);}
        }
        private void Loaded(object? sender,RoutedEventArgs e)=>Refresh();
        private void Refresh()
        {
            LastObservation=NativeTextScale.Read();double before=Factor(window);
            // A failed read remains Unknown and preserves the last known presentation factor.
            if(LastObservation.Known)window.SetValue(FactorProperty,LastObservation.Factor);
            BindTree(window,new HashSet<DependencyObject>());
            if(window.IsLoaded&&before!=Factor(window))changed?.Invoke();
        }
        private void Closed(object? sender,EventArgs e)
        {
            closed=true;if(source is {IsDisposed:false})source.RemoveHook(Hook);source=null;
            Microsoft.Win32.SystemEvents.UserPreferenceChanged-=PreferenceChanged;
            window.Loaded-=Loaded;window.SourceInitialized-=SourceReady;window.Closed-=Closed;
        }
    }
    private static void BindTree(DependencyObject node,HashSet<DependencyObject> visited)
    {
        if(!visited.Add(node)||(bool)node.GetValue(IgnoreProperty))return;
        if(node is FrameworkElement element)
        {
            var property=element is TextBlock?TextBlock.FontSizeProperty:element is Control?Control.FontSizeProperty:null;
            if(property!=null&&element.ReadLocalValue(property) is double size&&double.IsFinite(size)&&size>0)Bind(element,property,size);
            if(element is TextBlock block&&block.ReadLocalValue(TextBlock.LineHeightProperty) is double height&&double.IsFinite(height)&&height>0)Bind(block,TextBlock.LineHeightProperty,height);
        }
        foreach(object? child in LogicalTreeHelper.GetChildren(node))if(child is DependencyObject dependency)BindTree(dependency,visited);
        if(node is Visual)for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)BindTree(VisualTreeHelper.GetChild(node,i),visited);
    }
}
