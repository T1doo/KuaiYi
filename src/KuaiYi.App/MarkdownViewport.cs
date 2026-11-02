using KuaiYi.Core;
namespace KuaiYi.App;

internal readonly record struct MarkdownViewport(double WidthDip,double MaximumHeightDip)
{
    public double CodeMaximumHeightDip=>Math.Min(240,MaximumHeightDip);
    public double TableMaximumHeightDip=>Math.Min(320,MaximumHeightDip);
    public void Validate()
    {if(!double.IsFinite(WidthDip)||!double.IsFinite(MaximumHeightDip)||WidthDip<=0||MaximumHeightDip<=0)throw new ArgumentOutOfRangeException(nameof(MarkdownViewport));}
    public static MarkdownViewport FromCard(double widthDip,double maximumHeightDip,bool hasLabel,double noticeHeight=0,double toolbarExtra=0,double footerHeight=28)=>new(Math.Max(1,widthDip-32),Math.Max(1,maximumHeightDip-80-toolbarExtra-(hasLabel?footerHeight:0)-noticeHeight));
    public static MarkdownViewport FromPlan(LayoutPlan plan,bool hasLabel,double noticeHeight=0,double toolbarExtra=0,double footerHeight=28)=>FromCard(plan.Dpi.Dips(plan.Target.Width),plan.Dpi.Dips(plan.MaximumHeightPixels),hasLabel,noticeHeight,toolbarExtra,footerHeight);
}
