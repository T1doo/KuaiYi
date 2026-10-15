using KuaiYi.Core;
namespace KuaiYi.App;

internal readonly record struct MarkdownViewport(double WidthDip,double MaximumHeightDip)
{
    public double CodeMaximumHeightDip=>Math.Min(240,MaximumHeightDip);
    public void Validate()
    {if(!double.IsFinite(WidthDip)||!double.IsFinite(MaximumHeightDip)||WidthDip<=0||MaximumHeightDip<=0)throw new ArgumentOutOfRangeException(nameof(MarkdownViewport));}
    public static MarkdownViewport FromCard(double widthDip,double maximumHeightDip,bool hasLabel)=>new(Math.Max(1,widthDip-32),Math.Max(1,maximumHeightDip-80-(hasLabel?20:0)));
    public static MarkdownViewport FromPlan(LayoutPlan plan,bool hasLabel)=>FromCard(plan.Dpi.Dips(plan.Target.Width),plan.Dpi.Dips(plan.MaximumHeightPixels),hasLabel);
}
