using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;

namespace KuaiYi.App;

internal static class DesktopSourceChrome
{
    internal static void Install(Expander source)
    {
        var template=new ControlTemplate(typeof(Expander));var stack=new FrameworkElementFactory(typeof(StackPanel));
        var header=new FrameworkElementFactory(typeof(ToggleButton),"HeaderSite");
        header.SetValue(Control.TemplateProperty,DesktopTheme.SourceToggleTemplate());header.SetResourceReference(Control.BackgroundProperty,DesktopTheme.Input);
        header.SetResourceReference(Control.BorderBrushProperty,DesktopTheme.Stroke);
        header.SetValue(Control.BorderThicknessProperty,new Thickness(1));header.SetValue(Control.PaddingProperty,new Thickness(10,4,10,4));
        header.SetValue(Control.FocusVisualStyleProperty,null);header.SetValue(FrameworkElement.MinHeightProperty,28d);
        header.SetValue(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch);
        header.SetValue(Control.ForegroundProperty,new TemplateBindingExtension(Control.ForegroundProperty));
        header.SetValue(ContentControl.ContentProperty,new TemplateBindingExtension(HeaderedContentControl.HeaderProperty));
        header.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"展开或收起所示版本原文");
        header.SetBinding(ToggleButton.IsCheckedProperty,new Binding(nameof(Expander.IsExpanded)){RelativeSource=RelativeSource.TemplatedParent,Mode=BindingMode.TwoWay});
        var row=new FrameworkElementFactory(typeof(DockPanel));
        var copy=new FrameworkElementFactory(typeof(ContentPresenter));copy.SetValue(DockPanel.DockProperty,Dock.Right);
        copy.SetValue(ContentPresenter.ContentProperty,new TemplateBindingExtension(FrameworkElement.TagProperty));row.AppendChild(copy);
        header.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Left);row.AppendChild(header);stack.AppendChild(row);
        var content=new FrameworkElementFactory(typeof(ContentPresenter),"sourceContent");content.SetValue(ContentPresenter.ContentSourceProperty,"Content");
        content.SetValue(FrameworkElement.MarginProperty,new Thickness(0,8,0,0));content.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);stack.AppendChild(content);
        template.VisualTree=stack;
        var expanded=new Trigger{Property=Expander.IsExpandedProperty,Value=true};expanded.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"sourceContent"));template.Triggers.Add(expanded);
        source.Template=template;
        // Keep document layout space, but never expose a clipped interactive header.
        // The same check also applies while the user scrolls the original text.
        void Update(object? sender,EventArgs e)
        {
            if(source.Template.FindName("HeaderSite",source) is not ToggleButton toggle)return;
            var reader=Ancestor<RichTextBox>(source);var window=Window.GetWindow(source);
            if(reader==null||window==null)return;
            ShowWhole(toggle,reader,window);
            if(source.Tag is Button copyButton)ShowWhole(copyButton,reader,window);
        }
        source.Loaded+=(_,_)=>{source.LayoutUpdated-=Update;source.LayoutUpdated+=Update;};
        source.Unloaded+=(_,_)=>source.LayoutUpdated-=Update;
    }
    private static T? Ancestor<T>(DependencyObject element)where T:DependencyObject
    {
        for(DependencyObject? node=element;node!=null;node=System.Windows.Media.VisualTreeHelper.GetParent(node))
            if(node is T result)return result;
        return null;
    }
    private static void ShowWhole(Control control,FrameworkElement viewport,Window window)
    {
        if(!control.IsVisible)return;
        var bounds=new Rect(0,0,control.ActualWidth,control.ActualHeight);
        bool shown=control.ActualWidth>0&&control.ActualHeight>0&&
            new Rect(0,0,viewport.ActualWidth,viewport.ActualHeight).Contains(control.TransformToAncestor(viewport).TransformBounds(bounds))&&
            new Rect(0,0,window.ActualWidth,window.ActualHeight).Contains(control.TransformToAncestor(window).TransformBounds(bounds));
        control.Opacity=shown?1:0;control.IsHitTestVisible=shown;control.IsTabStop=shown;
    }
}
