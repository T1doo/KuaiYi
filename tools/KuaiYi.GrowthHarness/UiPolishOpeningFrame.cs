using KuaiYi.Core;
namespace KuaiYi.GrowthHarness;
// Geometry is the intermediate-frame contract. Opacity is observed separately, never a substitute.
internal sealed record UiPolishOpeningFrame(PixelRect Card,PixelRect Anchor,PixelRect Target,long Epoch,double Opacity)
{
    internal static UiPolishOpeningFrame? Observe(OverlayFrames frames,PixelRect target,long epoch,long baseline)
        =>epoch>baseline&&frames.Transition==OverlayTransition.Opening&&frames.NeedsFrame&&frames.Current!=frames.Anchor&&frames.Current!=target
            ?new(frames.Current,frames.Anchor,target,epoch,frames.ContentOpacity):null;
}
// Both independently observed facts are mandatory for Full. No frame/one missing fact cannot pass.
internal sealed class UiPolishOpeningEvidence(long baseline)
{
    internal UiPolishOpeningFrame? Geometry{get;private set;}
    internal UiPolishOpeningFrame? Fade{get;private set;}
    private long? boundEpoch;
    internal bool Complete=>Geometry!=null&&Fade!=null;
    internal void Observe(OverlayFrames frames,PixelRect target,long epoch,bool bodyReady)
    {
        if(!bodyReady)return;
        if(boundEpoch.HasValue&&epoch!=boundEpoch)throw new InvalidOperationException("Original opening epoch changed.");
        if(epoch<=baseline||frames.Transition!=OverlayTransition.Opening||!frames.NeedsFrame)return;
        boundEpoch??=epoch;
        Geometry??=UiPolishOpeningFrame.Observe(frames,target,epoch,baseline);
        if(Fade==null&&frames.ContentOpacity>0&&frames.ContentOpacity<1)Fade=new(frames.Current,frames.Anchor,target,epoch,frames.ContentOpacity);
    }
    internal void RequireComplete()
    {if(!Complete)throw new InvalidOperationException($"Actual opening evidence missing: geometry={Geometry!=null}, fade={Fade!=null}; no fabricated frame.");}
}
