using System.Windows;
using System.Windows.Media;
using System.Diagnostics;
using KuaiYi.Core.Settings;

namespace KuaiYi.App;

// Original vector material. The native desktop acrylic is behind this tint;
// the fully opaque variant remains usable when composition/transparency is off.
internal sealed class AuroraGlassSurface:FrameworkElement
{
    private bool dark,contrast,acrylic,reduce,animating;
    private OverlaySettings appearance=new();
    private readonly Stopwatch clock=new();private double last,phase;
    private Brush fill=Brushes.Transparent,first=Brushes.Transparent,second=Brushes.Transparent,third=Brushes.Transparent,sheen=Brushes.Transparent;
    internal AuroraGlassSurface()
    {Loaded+=(_,_)=>UpdateMotion();Unloaded+=(_,_)=>StopMotion();IsVisibleChanged+=(_,_)=>UpdateMotion();SizeChanged+=(_,_)=>UpdateMotion();Rebuild();}
    internal void Configure(bool dark,bool contrast,bool acrylic,OverlaySettings appearance,bool reduce)
    {
        bool changed=this.dark!=dark||this.contrast!=contrast||this.acrylic!=acrylic||this.appearance.ColorStyle!=appearance.ColorStyle||this.appearance.CustomColor!=appearance.CustomColor;
        this.dark=dark;this.contrast=contrast;this.acrylic=acrylic;this.appearance=appearance;this.reduce=reduce;
        if(changed)Rebuild();UpdateMotion();InvalidateVisual();
    }
    internal static (string First,string Second,string Third) ColorsFor(OverlayColorStyle style,string custom="#8B5CF6")=>style switch
    {
        OverlayColorStyle.Prism=>("#8B5CF6","#5B8CFF","#F0ABFC"),OverlayColorStyle.Ocean=>("#22D3EE","#2563EB","#2DD4BF"),
        OverlayColorStyle.Sakura=>("#FB7185","#C084FC","#F9A8D4"),OverlayColorStyle.Sunset=>("#FB923C","#F43F5E","#FACC15"),
        OverlayColorStyle.Graphite=>("#64748B","#334155","#94A3B8"),OverlayColorStyle.Custom=>(custom,"#60A5FA","#F0ABFC"),_ =>("#42D8F6","#8B5CF6","#F472B6")
    };
    private void Rebuild()
    {
        byte alpha=acrylic?(byte)222:(byte)255;
        var material=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1)};
        material.GradientStops.Add(new(dark?Color.FromArgb(alpha,10,21,46):Color.FromArgb(alpha,225,244,255),0));
        material.GradientStops.Add(new(dark?Color.FromArgb(alpha,24,20,56):Color.FromArgb(alpha,235,243,255),1));material.Freeze();fill=material;
        var colors=ColorsFor(appearance.ColorStyle,appearance.CustomColor);
        first=GlowBrush(colors.First,dark?(byte)65:(byte)44);second=GlowBrush(colors.Second,dark?(byte)80:(byte)42);third=GlowBrush(colors.Third,dark?(byte)42:(byte)35);
        var gloss=new LinearGradientBrush(Color.FromArgb(dark?(byte)20:(byte)65,255,255,255),Colors.Transparent,90);gloss.Freeze();sheen=gloss;
    }
    private static Brush GlowBrush(string hex,byte alpha)
    {
        Color color=(Color)ColorConverter.ConvertFromString(hex);color.A=alpha;
        var brush=new RadialGradientBrush(color,Color.FromArgb(0,color.R,color.G,color.B));brush.Freeze();return brush;
    }
    private void UpdateMotion()
    {
        bool enabled=IsLoaded&&IsVisible&&ActualWidth>60&&ActualHeight>60&&!contrast&&!reduce&&appearance.BackgroundMotion==OverlayBackgroundMotion.Flow;
        if(enabled&&!animating){animating=true;clock.Start();CompositionTarget.Rendering+=Animate;}
        else if(!enabled)StopMotion();
    }
    private void StopMotion(){if(!animating)return;CompositionTarget.Rendering-=Animate;animating=false;clock.Stop();}
    private void Animate(object? sender,EventArgs e)
    {double now=clock.Elapsed.TotalSeconds;if(now-last<1d/30)return;last=now;phase=now;InvalidateVisual();}
    internal static Brush Edge(bool dark,OverlayColorStyle style=OverlayColorStyle.Aurora,string custom="#8B5CF6")
    {
        var colors=ColorsFor(style,custom);
        var brush=new LinearGradientBrush{StartPoint=new(0,0),EndPoint=new(1,1)};
        brush.GradientStops.Add(new(Tint(colors.First,dark?(byte)190:(byte)220),0));
        brush.GradientStops.Add(new(Tint(colors.Second,110),.5));
        brush.GradientStops.Add(new(Tint(colors.Third,160),1));brush.Freeze();return brush;
        static Color Tint(string hex,byte alpha){var color=(Color)ColorConverter.ConvertFromString(hex);color.A=alpha;return color;}
    }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);var rect=new Rect(RenderSize);
        if(contrast){dc.DrawRectangle(SystemColors.WindowBrush,null,rect);return;}
        dc.DrawRectangle(fill,null,rect);
        Glow(dc,first,new(RenderSize.Width*(.04+.12*Math.Sin(phase*.18)),RenderSize.Height*(.07+.08*Math.Sin(phase*.14))),Math.Max(170,RenderSize.Width*.64),Math.Max(150,RenderSize.Height*.7));
        Glow(dc,second,new(RenderSize.Width*(.9+.08*Math.Sin(phase*.13+1)),RenderSize.Height*(.1+.12*Math.Sin(phase*.11))),Math.Max(180,RenderSize.Width*.65),Math.Max(160,RenderSize.Height*.8));
        Glow(dc,third,new(RenderSize.Width*(.6+.15*Math.Sin(phase*.12)),RenderSize.Height*(1+.08*Math.Cos(phase*.14))),Math.Max(180,RenderSize.Width*.6),Math.Max(130,RenderSize.Height*.6));
        dc.DrawRectangle(sheen,null,new Rect(0,0,RenderSize.Width,Math.Min(110,RenderSize.Height)));
    }
    private static void Glow(DrawingContext dc,Brush brush,Point center,double rx,double ry)=>dc.DrawEllipse(brush,null,center,rx,ry);
}
