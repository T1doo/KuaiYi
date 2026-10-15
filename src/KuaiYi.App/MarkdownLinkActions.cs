using KuaiYi.Core;
namespace KuaiYi.App;

/// <summary>Called only by the explicit native link action. Confirmation and launch are separate boundaries.</summary>
internal sealed class MarkdownLinkActions(Func<MarkdownHttpTarget,bool> confirm,Action<MarkdownHttpTarget> open,Action<Exception> failed)
{
    public void Activate(MarkdownHttpTarget target,Func<bool> current)
    {
        if(!current()||!MarkdownHttpTarget.TryParse(target.Url,out var checkedTarget)||checkedTarget!=target)return;
        try{if(confirm(target)&&current()&&MarkdownHttpTarget.TryParse(target.Url,out checkedTarget)&&checkedTarget==target)open(target);}
        catch(Exception error){if(current())failed(error);}
    }
}
