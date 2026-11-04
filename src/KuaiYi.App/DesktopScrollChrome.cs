using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;

namespace KuaiYi.App;

// Retain the system scrollbar column in both readers, so streaming never reflows
// when scrolling becomes necessary. Only its chrome becomes a quiet narrow thumb.
internal static class DesktopScrollChrome
{
    internal static void Install(RichTextBox reader)
    {
        var template=new ControlTemplate(typeof(ScrollBar));
        var root=new FrameworkElementFactory(typeof(Grid));root.SetValue(Panel.BackgroundProperty,Brushes.Transparent);
        var track=new FrameworkElementFactory(typeof(DesktopScrollTrack),"PART_Track");
        track.SetValue(Track.OrientationProperty,Orientation.Vertical);track.SetValue(Track.IsDirectionReversedProperty,true);
        foreach(var pair in new[]{(Track.MinimumProperty,nameof(ScrollBar.Minimum)),(Track.MaximumProperty,nameof(ScrollBar.Maximum)),(Track.ValueProperty,nameof(ScrollBar.Value)),(Track.ViewportSizeProperty,nameof(ScrollBar.ViewportSize))})
            track.SetBinding(pair.Item1,new Binding(pair.Item2){RelativeSource=RelativeSource.TemplatedParent,Mode=pair.Item1==Track.ValueProperty?BindingMode.TwoWay:BindingMode.OneWay});
        root.AppendChild(track);template.VisualTree=root;
        var hidden=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};hidden.Setters.Add(new Setter(UIElement.OpacityProperty,0d));template.Triggers.Add(hidden);
        var style=new Style(typeof(ScrollBar));
        var vertical=new Trigger{Property=ScrollBar.OrientationProperty,Value=Orientation.Vertical};
        vertical.Setters.Add(new Setter(Control.TemplateProperty,template));vertical.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));
        vertical.Setters.Add(new Setter(FrameworkElement.WidthProperty,new DynamicResourceExtension(SystemParameters.VerticalScrollBarWidthKey)));
        style.Triggers.Add(vertical);
        reader.Resources[typeof(ScrollBar)]=style;
    }
}

internal sealed class DesktopScrollTrack:Track
{
    public DesktopScrollTrack()
    {
        DecreaseRepeatButton=PageButton(ScrollBar.PageUpCommand);
        IncreaseRepeatButton=PageButton(ScrollBar.PageDownCommand);
        var thumb=new Thumb{MinHeight=18,Cursor=System.Windows.Input.Cursors.Arrow};
        var thumbTemplate=new ControlTemplate(typeof(Thumb));var line=new FrameworkElementFactory(typeof(Border),"line");
        line.SetValue(FrameworkElement.WidthProperty,4d);line.SetValue(Border.CornerRadiusProperty,new CornerRadius(2));line.SetValue(UIElement.OpacityProperty,.55);
        line.SetBinding(Border.BackgroundProperty,new Binding("Foreground"){RelativeSource=new RelativeSource(RelativeSourceMode.FindAncestor,typeof(RichTextBox),1)});
        thumbTemplate.VisualTree=line;
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(UIElement.OpacityProperty,.8,"line"));thumbTemplate.Triggers.Add(hover);
        var drag=new Trigger{Property=Thumb.IsDraggingProperty,Value=true};drag.Setters.Add(new Setter(UIElement.OpacityProperty,1d,"line"));thumbTemplate.Triggers.Add(drag);
        thumb.Template=thumbTemplate;Thumb=thumb;
    }
    private static RepeatButton PageButton(System.Windows.Input.ICommand command)
    {
        var hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);
        return new RepeatButton{Command=command,Focusable=false,IsTabStop=false,Template=new ControlTemplate(typeof(RepeatButton)){VisualTree=hit}};
    }
}
