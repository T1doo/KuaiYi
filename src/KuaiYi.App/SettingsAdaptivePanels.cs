using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace KuaiYi.App;

// Child identity and order never change when the window or font size changes.
internal sealed class PreferencePanel:Panel
{
    private readonly FrameworkElement labels,input;
    private PreferenceLayout plan;
    internal PreferencePanel(FrameworkElement labels,FrameworkElement input)
    {this.labels=labels;this.input=input;Children.Add(labels);Children.Add(input);}
    protected override Size MeasureOverride(Size availableSize)
    {
        double font=input is Control control?control.FontSize:13;
        double width=SettingsLayoutPlan.Finite(availableSize.Width,640*SettingsLayoutPlan.Scale(font));
        bool hasLabels=labels.Visibility!=Visibility.Collapsed,hasInput=input.Visibility!=Visibility.Collapsed;
        // Bounded probe uses the actual template/font, including an enlarged system font.
        input.Measure(new Size(width,double.PositiveInfinity));
        plan=SettingsLayoutPlan.Preference(width,font,input.DesiredSize.Width,hasLabels,hasInput);
        labels.Measure(new Size(plan.LabelWidth,double.PositiveInfinity));
        input.Measure(new Size(plan.InputWidth,double.PositiveInfinity));
        return new Size(width,plan.Height(labels.DesiredSize.Height,input.DesiredSize.Height));
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        // Use the measured plan: changing to stacked here would leave height unreserved.
        double labelHeight=labels.DesiredSize.Height,inputHeight=input.DesiredSize.Height;
        labels.Arrange(new Rect(0,plan.Stacked?0:Math.Max(0,(finalSize.Height-labelHeight)/2),plan.LabelWidth,labelHeight));
        double x=plan.Stacked||!plan.Labels?0:plan.LabelWidth+plan.Gap;
        double y=plan.Stacked?labelHeight+plan.Gap:Math.Max(0,(finalSize.Height-inputHeight)/2);
        input.Arrange(new Rect(x,y,plan.InputWidth,inputHeight));
        return finalSize;
    }
}

internal sealed class SettingsChromePanel:Panel
{
    private static readonly DependencyPropertyKey CompactKey=DependencyProperty.RegisterReadOnly(nameof(IsCompact),typeof(bool),typeof(SettingsChromePanel),new FrameworkPropertyMetadata(false));
    internal static readonly DependencyProperty IsCompactProperty=CompactKey.DependencyProperty;
    public bool IsCompact=>(bool)GetValue(IsCompactProperty);
    public static readonly DependencyProperty UiFontSizeProperty=DependencyProperty.Register(nameof(UiFontSize),typeof(double),typeof(SettingsChromePanel),new FrameworkPropertyMetadata(13d,FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double UiFontSize{get=>(double)GetValue(UiFontSizeProperty);set=>SetValue(UiFontSizeProperty,value);}
    public static readonly DependencyProperty ScrollBarWidthProperty=DependencyProperty.Register(nameof(ScrollBarWidth),typeof(double),typeof(SettingsChromePanel),new FrameworkPropertyMetadata(17d,FrameworkPropertyMetadataOptions.AffectsMeasure));
    public double ScrollBarWidth{get=>(double)GetValue(ScrollBarWidthProperty);set=>SetValue(ScrollBarWidthProperty,value);}
    private readonly UIElement rail,main,footer;
    private SettingsChromeLayout plan;
    internal SettingsChromePanel(UIElement rail,UIElement main,UIElement footer)
    {this.rail=rail;this.main=main;this.footer=footer;Children.Add(rail);Children.Add(main);Children.Add(footer);}
    protected override Size MeasureOverride(Size availableSize)
    {
        var bounds=SettingsLayoutPlan.Chrome(availableSize.Width,availableSize.Height,UiFontSize,0);
        if(IsCompact!=bounds.Compact)SetValue(CompactKey,bounds.Compact);
        footer.Measure(new Size(bounds.Width,bounds.Height*.35));
        if(bounds.Compact)rail.Measure(new Size(bounds.Width,bounds.Height*.24));
        plan=SettingsLayoutPlan.Chrome(bounds.Width,bounds.Height,UiFontSize,footer.DesiredSize.Height,
            bounds.Compact?rail.DesiredSize.Height:0);
        rail.Measure(new Size(plan.Compact?plan.Width:plan.RailWidth,plan.Compact?plan.NavigationHeight:plan.MainHeight));
        main.Measure(new Size(plan.MainWidth,plan.MainHeight));
        return new Size(plan.Width,plan.Height);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        rail.Arrange(new Rect(0,0,plan.Compact?plan.Width:plan.RailWidth,plan.Compact?plan.NavigationHeight:plan.MainHeight));
        main.Arrange(new Rect(plan.Compact?0:plan.RailWidth,plan.Compact?plan.NavigationHeight:0,plan.MainWidth,plan.MainHeight));
        footer.Arrange(new Rect(0,plan.Height-plan.FooterHeight,plan.Width,plan.FooterHeight));
        return finalSize;
    }
}

// Keep the selected category visible after a reflow without changing focus, selection or drafts.
// Ordinary user scrolling does not requeue this operation and is never snapped back.
internal sealed class SettingsNavigationListBox:ListBox
{
    private DispatcherOperation? reveal;
    private int visualEpoch;
    internal SettingsNavigationListBox()
    {
        Loaded+=(_,_)=>{visualEpoch++;QueueSelectedVisibility();};
        Unloaded+=(_,_)=>{visualEpoch++;reveal?.Abort();reveal=null;};
        ItemContainerGenerator.StatusChanged+=(_,_)=>
        {if(ItemContainerGenerator.Status==GeneratorStatus.ContainersGenerated)QueueSelectedVisibility();};
    }
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {base.OnSelectionChanged(e);QueueSelectedVisibility();}
    protected override void OnRenderSizeChanged(SizeChangedInfo info)
    {base.OnRenderSizeChanged(info);QueueSelectedVisibility();}
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if(e.Property==FontSizeProperty||e.Property==FontFamilyProperty||e.Property==FontWeightProperty||e.Property==FontStyleProperty||e.Property==FlowDirectionProperty)
            QueueSelectedVisibility();
    }
    private void QueueSelectedVisibility()
    {
        if(!IsLoaded||Dispatcher.HasShutdownStarted||reveal?.Status==DispatcherOperationStatus.Pending)return;
        int epoch=visualEpoch;
        reveal=Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {
            if(epoch!=visualEpoch)return;
            reveal=null;
            // Read the current item at delivery; a queued old selection cannot move the new page.
            if(IsLoaded&&IsVisible&&SelectedItem is {} selected&&ItemContainerGenerator.ContainerFromItem(selected) is ListBoxItem item&&ReferenceEquals(ItemContainerGenerator.ItemFromContainer(item),selected))
                item.BringIntoView();
        });
    }
}

internal sealed class SettingsMinimumWidth:IMultiValueConverter
{
    public object Convert(object[] values,Type targetType,object parameter,CultureInfo culture)=>SettingsLayoutPlan.MinimumWidth(values[0] is double size?size:13,values[1] is double scroll?scroll:17);
    public object[] ConvertBack(object value,Type[] targetTypes,object parameter,CultureInfo culture)=>targetTypes.Select(_=>Binding.DoNothing).ToArray();
}
internal sealed class SettingsNavigationOrientation:IValueConverter
{
    public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is true?Orientation.Horizontal:Orientation.Vertical;
    public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
}
