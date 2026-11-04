using System.Windows;
using KuaiYi.Core;
namespace KuaiYi.App;

internal sealed partial class ShellWindow
{
    private double lastPresentationTextScale=1;
    private double PresentationTextScale=>DesktopTextScale.Factor(this);
    private double PresentationFooterHeightDip=>Math.Max(28,Math.Ceiling(18*PresentationTextScale+8));
    private double ToolbarExtraFor(LayoutPlan plan)
    {
        double width=plan.Dpi.Dips(plan.Target.Width),x=plan.Dpi.Dips(plan.Anchor.Left-plan.Target.Left);
        return OverlayReadingToolbar.Focused(Math.Max(x,width-x-48),width,true,lifecycle.Gate.HasRunningGeneration,touchToolbar,PresentationTextScale).ExtraHeightDip;
    }
    private void OpenPresentationToolbar()
    {
        double extra=awaitingText?OverlayReadingToolbar.Focused(172,220,true,true,touchToolbar,PresentationTextScale).ExtraHeightDip:0;
        overlayFrames?.OpenToolbar(220,48+extra);
    }
    private void RefreshTextScalePresentation()
    {
        double factor=PresentationTextScale;if(factor==lastPresentationTextScale)return;lastPresentationTextScale=factor;
        // Same selected version, source, reader intent and request. Existing frame/flush owns remeasure.
        if(peek&&!gesture.Active&&overlayFrames?.Transition!=OverlayTransition.Moving){OpenPresentationToolbar();StartFrames();}
        dirty=true;if(!flush.IsEnabled)flush.Start();
    }
}
