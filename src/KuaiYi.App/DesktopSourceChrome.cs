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
        header.SetValue(Control.TemplateProperty,DesktopTheme.SourceToggleTemplate());header.SetValue(Control.BackgroundProperty,System.Windows.Media.Brushes.Transparent);
        header.SetValue(Control.BorderThicknessProperty,new Thickness(0));header.SetValue(Control.PaddingProperty,new Thickness(2,0,2,0));
        header.SetValue(Control.FocusVisualStyleProperty,null);header.SetValue(FrameworkElement.MinHeightProperty,28d);
        header.SetValue(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch);
        header.SetValue(Control.ForegroundProperty,new TemplateBindingExtension(Control.ForegroundProperty));
        header.SetValue(ContentControl.ContentProperty,new TemplateBindingExtension(HeaderedContentControl.HeaderProperty));
        header.SetValue(System.Windows.Automation.AutomationProperties.NameProperty,"展开或收起所示版本原文");
        header.SetBinding(ToggleButton.IsCheckedProperty,new Binding(nameof(Expander.IsExpanded)){RelativeSource=RelativeSource.TemplatedParent,Mode=BindingMode.TwoWay});
        stack.AppendChild(header);
        var content=new FrameworkElementFactory(typeof(ContentPresenter),"sourceContent");content.SetValue(ContentPresenter.ContentSourceProperty,"Content");
        content.SetValue(FrameworkElement.MarginProperty,new Thickness(0,8,0,0));content.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);stack.AppendChild(content);
        template.VisualTree=stack;
        var expanded=new Trigger{Property=Expander.IsExpandedProperty,Value=true};expanded.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"sourceContent"));template.Triggers.Add(expanded);
        source.Template=template;
    }
}
