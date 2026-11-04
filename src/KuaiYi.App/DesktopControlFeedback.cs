using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;

// Only the small template tint animates. Command delivery, focus and geometry stay immediate.
internal static class DesktopControlFeedback
{
    internal static readonly DependencyProperty SurfaceProperty=DependencyProperty.RegisterAttached("Surface",typeof(bool),typeof(DesktopControlFeedback),new PropertyMetadata(false));
    internal static readonly DependencyProperty TintProperty=DependencyProperty.RegisterAttached("Tint",typeof(double),typeof(DesktopControlFeedback),new PropertyMetadata(0d,TintChanged));
    private static readonly DependencyProperty ReduceProperty=DependencyProperty.RegisterAttached("Reduce",typeof(bool),typeof(DesktopControlFeedback),new FrameworkPropertyMetadata(true,FrameworkPropertyMetadataOptions.Inherits,MotionChanged));
    internal static void Apply(Window window,MotionPreference preference)
        =>window.SetValue(ReduceProperty,preference==MotionPreference.Reduce||preference!=MotionPreference.Full&&!SystemParameters.ClientAreaAnimation);
    private static void MotionChanged(DependencyObject target,DependencyPropertyChangedEventArgs change)
    {
        if(change.NewValue is true&&target is Border surface&&(bool)surface.GetValue(SurfaceProperty))
        {surface.BeginAnimation(UIElement.OpacityProperty,null);surface.SetCurrentValue(UIElement.OpacityProperty,(double)surface.GetValue(TintProperty));}
    }
    private static void TintChanged(DependencyObject target,DependencyPropertyChangedEventArgs change)
    {
        if(target is not Border surface||!(bool)surface.GetValue(SurfaceProperty))return;
        double from=surface.Opacity,to=(double)change.NewValue;
        surface.BeginAnimation(UIElement.OpacityProperty,null);surface.SetCurrentValue(UIElement.OpacityProperty,to);
        if(!(bool)surface.GetValue(ReduceProperty)&&surface.IsVisible)
            surface.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(100)){FillBehavior=FillBehavior.Stop,EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}},HandoffBehavior.SnapshotAndReplace);
    }
}
