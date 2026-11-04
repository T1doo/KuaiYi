using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace KuaiYi.App;

// Native MenuItem/ContextMenu still own roles, commands, keyboard navigation and
// popup lifetime. The templates replace only the legacy gutter and bevel chrome.
internal static class DesktopMenuChrome
{
    internal static void Install(ContextMenu menu,Style item)
    {
        menu.FontFamily=DesktopTheme.ProseFont;menu.UseLayoutRounding=true;menu.SnapsToDevicePixels=true;
        menu.Resources["KuaiYi.MenuMaxHeight"]=Math.Max(24,menu.MaxHeight-12);
        menu.Template=new ControlTemplate(typeof(ContextMenu)){VisualTree=PopupSurface()};
        item.Setters.Add(new Setter(Control.TemplateProperty,ItemTemplate()));
        item.Setters.Add(new Setter(FrameworkElement.FocusVisualStyleProperty,null));
        var checkedRow=new Trigger{Property=MenuItem.IsCheckedProperty,Value=true};
        checkedRow.Setters.Add(new Setter(Control.BackgroundProperty,new DynamicResourceExtension(DesktopTheme.AccentSoft)));
        checkedRow.Setters.Add(new Setter(Control.ForegroundProperty,new DynamicResourceExtension(DesktopTheme.SelectionText)));item.Triggers.Add(checkedRow);
        var separator=new Style(typeof(Separator));var rule=new FrameworkElementFactory(typeof(Border));
        rule.SetResourceReference(Border.BackgroundProperty,DesktopTheme.Stroke);rule.SetValue(FrameworkElement.HeightProperty,1d);
        rule.SetValue(FrameworkElement.MarginProperty,new Thickness(10,5,10,5));rule.SetValue(UIElement.OpacityProperty,.65);
        separator.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Separator)){VisualTree=rule}));menu.Resources[typeof(Separator)]=separator;
    }
    private static FrameworkElementFactory PopupSurface()
    {
        var surface=new FrameworkElementFactory(typeof(Border));surface.SetResourceReference(Border.BackgroundProperty,DesktopTheme.Surface);
        surface.SetResourceReference(Border.BorderBrushProperty,DesktopTheme.Stroke);surface.SetValue(Border.BorderThicknessProperty,new Thickness(1));
        surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(6));surface.SetValue(Border.PaddingProperty,new Thickness(4));
        surface.SetResourceReference(FrameworkElement.MaxHeightProperty,"KuaiYi.MenuMaxHeight");
        var scroll=new FrameworkElementFactory(typeof(ScrollViewer));scroll.SetValue(Control.FocusableProperty,false);
        scroll.SetValue(ScrollViewer.CanContentScrollProperty,true);scroll.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled);
        scroll.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,ScrollBarVisibility.Auto);
        var items=new FrameworkElementFactory(typeof(ItemsPresenter),"ItemsPresenter");items.SetValue(KeyboardNavigation.DirectionalNavigationProperty,KeyboardNavigationMode.Cycle);
        scroll.AppendChild(items);surface.AppendChild(scroll);return surface;
    }
    private static ControlTemplate ItemTemplate()
    {
        var template=new ControlTemplate(typeof(MenuItem));var root=new FrameworkElementFactory(typeof(Grid));
        var surface=new FrameworkElementFactory(typeof(Border),"surface");surface.SetValue(Border.CornerRadiusProperty,new CornerRadius(3));
        surface.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));surface.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
        surface.SetValue(Border.BorderThicknessProperty,new Thickness(1));surface.SetValue(Border.BorderBrushProperty,Brushes.Transparent);
        var row=new FrameworkElementFactory(typeof(DockPanel));
        var arrow=Glyph("arrow","M1 1 L5 5 L1 9",5,9);arrow.SetValue(DockPanel.DockProperty,Dock.Right);
        arrow.SetValue(FrameworkElement.MarginProperty,new Thickness(14,0,2,0));arrow.SetValue(UIElement.VisibilityProperty,Visibility.Collapsed);row.AppendChild(arrow);
        var gesture=new FrameworkElementFactory(typeof(TextBlock),"gesture");gesture.SetValue(DockPanel.DockProperty,Dock.Right);
        gesture.SetValue(TextBlock.TextProperty,new TemplateBindingExtension(MenuItem.InputGestureTextProperty));gesture.SetResourceReference(TextBlock.ForegroundProperty,DesktopTheme.Muted);
        gesture.SetValue(FrameworkElement.MarginProperty,new Thickness(16,0,4,0));gesture.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(gesture);
        var check=Glyph("check","M1 6 L4 9 L11 2",12,12);check.SetValue(DockPanel.DockProperty,Dock.Left);
        check.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,8,0));check.SetValue(UIElement.VisibilityProperty,Visibility.Hidden);row.AppendChild(check);
        var header=new FrameworkElementFactory(typeof(ContentPresenter));header.SetValue(ContentPresenter.ContentSourceProperty,"Header");
        header.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);header.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(header);
        surface.AppendChild(row);root.AppendChild(surface);
        var popup=new FrameworkElementFactory(typeof(Popup),"PART_Popup");popup.SetValue(Popup.PlacementProperty,PlacementMode.Right);
        popup.SetValue(Popup.HorizontalOffsetProperty,-2d);popup.SetValue(Popup.VerticalOffsetProperty,-4d);popup.SetValue(Popup.AllowsTransparencyProperty,true);
        popup.SetValue(Control.FocusableProperty,false);popup.SetValue(Popup.PopupAnimationProperty,PopupAnimation.None);
        popup.SetBinding(Popup.IsOpenProperty,new Binding(nameof(MenuItem.IsSubmenuOpen)){RelativeSource=RelativeSource.TemplatedParent,Mode=BindingMode.TwoWay});
        popup.AppendChild(PopupSurface());root.AppendChild(popup);template.VisualTree=root;
        var children=new Trigger{Property=MenuItem.HasItemsProperty,Value=true};children.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"arrow"));template.Triggers.Add(children);
        var selected=new Trigger{Property=MenuItem.IsCheckedProperty,Value=true};selected.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"check"));template.Triggers.Add(selected);
        var noGesture=new Trigger{Property=MenuItem.InputGestureTextProperty,Value=""};noGesture.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed,"gesture"));template.Triggers.Add(noGesture);
        var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,new DynamicResourceExtension(DesktopTheme.Accent),"surface"));template.Triggers.Add(focus);
        return template;
    }
    private static FrameworkElementFactory Glyph(string name,string data,double width,double height)
    {
        var path=new FrameworkElementFactory(typeof(Path),name);path.SetValue(Path.DataProperty,Geometry.Parse(data));
        path.SetValue(Path.StrokeProperty,new TemplateBindingExtension(Control.ForegroundProperty));path.SetValue(Path.StrokeThicknessProperty,1.5);
        path.SetValue(Path.StrokeStartLineCapProperty,PenLineCap.Round);path.SetValue(Path.StrokeEndLineCapProperty,PenLineCap.Round);path.SetValue(Path.StrokeLineJoinProperty,PenLineJoin.Round);
        path.SetValue(Path.StretchProperty,Stretch.Uniform);path.SetValue(FrameworkElement.WidthProperty,width);path.SetValue(FrameworkElement.HeightProperty,height);
        path.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);path.SetValue(UIElement.IsHitTestVisibleProperty,false);return path;
    }
}
