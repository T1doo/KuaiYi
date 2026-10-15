using KuaiYi.Core;
namespace KuaiYi.App;

// Only these two desktop boundaries are replaced: confirmation and browser launch.
// The parser, FlowView, region, action and ShellWindow eligibility code are linked production.
internal sealed partial class ShellWindow
{
    internal List<MarkdownHttpTarget> LinkConfirmations {get;}=[];
    internal List<MarkdownHttpTarget> LinkLaunches {get;}=[];
    internal bool LinkConsent=true;
    internal Action? DuringLinkConfirmation;
    internal Exception? LinkLaunchError;
    internal int LinkErrors;
    private MarkdownLinkActions CreateMarkdownLinkActions()=>new(target=>
    {LinkConfirmations.Add(target);DuringLinkConfirmation?.Invoke();return LinkConsent;},target=>
    {if(LinkLaunchError is {} error)throw error;LinkLaunches.Add(target);},_=>LinkErrors++);
    internal void LinkHidden(){IsVisible=false;lifecycle.Hide();}
    internal void LinkPeek()=>peek=true;
    internal void LinkCapture()=>lifecycle.Gate.SetSuspension(Suspension.Capture,true);
}
