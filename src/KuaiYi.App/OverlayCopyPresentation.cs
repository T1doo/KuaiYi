using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    // UI feedback only. No source text, clipboard payload or history is retained here.
    private sealed record CopyFeedback(RequestIdentity Identity,int Length,int Number,bool Complete,bool Confirmed,long Epoch,long Revision);
    private CopyFeedback? copyFeedback;private long copyAttempt;
    private bool CanCopyVisibleResult()=>CanReadOriginalSource()&&!LocalImagePreview&&
        lifecycle.Gate.Snapshot.Presentation!=Presentation.Hidden&&
        !lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.Capture);
    private CachedOverlayVersion? CopyVersion()=>CanCopyVisibleResult()
        ?overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==overlayResults.SelectedVersionId&&v.Text.Length>0):null;
    private void CopyResult()
    {
        long attempt=++copyAttempt,revision=resultRevision,epoch=lifecycle.Gate.Snapshot.Epoch;
        copyFeedback=null;
        var selected=CopyVersion();if(selected==null){RefreshResultLabel();return;}
        bool StillSelected()=>attempt==copyAttempt&&revision==resultRevision&&epoch==lifecycle.Gate.Snapshot.Epoch&&
            CopyVersion() is {} current&&current.Identity==selected.Identity&&current.Text==selected.Text&&
            current.Complete==selected.Complete&&current.Stopped==selected.Stopped&&current.Failure==selected.Failure;
        bool confirmed=false;
        try
        {
            if(!StillSelected()){RefreshResultLabel();return;}
            WriteResultClipboard(selected.Text); // One explicit write; never retry automatically.
            confirmed=true;
        }
        catch(Exception error)
        {
            // A native write may have changed the clipboard before throwing.
            // Do not claim it is unchanged, and never clear a result on failure.
            if(StillSelected())Report("复制未确认；结果保留，可手动重试",error);
        }
        if(StillSelected())copyFeedback=new(selected.Identity,selected.Text.Length,selected.Number,
            selected.Complete&&!selected.Stopped&&selected.Failure==null,confirmed,epoch,revision);
        else copyFeedback=null; // Overlapping native writes have unknown final ordering.
        if(copyFeedback?.Confirmed==true)ScheduleCopyFeedbackReset();
        RefreshResultLabel();
    }
    private void ExpireCopyFeedback(){if(copyFeedback?.Confirmed!=true)return;copyFeedback=null;RefreshResultLabel();}
    private void RefreshCopyFeedback()
    {
        foreach(var tool in tools.Where(t=>Equals(t.Tag,"复制")))
        {tool.Content="复制";System.Windows.Automation.AutomationProperties.SetName(tool,"复制当前显示版本");}
        if(copyFeedback is not {} feedback)return;
        if(feedback.Epoch!=lifecycle.Gate.Snapshot.Epoch||feedback.Revision!=resultRevision||
            CopyVersion() is not {} selected||selected.Identity!=feedback.Identity||selected.Text.Length!=feedback.Length)
        {copyFeedback=null;return;}
        foreach(var tool in tools.Where(t=>Equals(t.Tag,"复制")))
        {tool.Content=feedback.Confirmed?"已复制":"未确认";System.Windows.Automation.AutomationProperties.SetName(tool,feedback.Confirmed?feedback.Complete?"已复制完整结果":"已复制部分结果":"复制未确认，可手动重试");}
        string notice=feedback.Confirmed
            ?$"已复制版本 {feedback.Number} · {(feedback.Complete?"完整结果":"复制时为部分结果")}"
            :$"版本 {feedback.Number} 复制未确认 · 可手动重试";
        resultLabel.Text=notice+(resultLabel.Text.Length>0?" · "+resultLabel.Text:"");
        resultLabel.ToolTip=notice+"\n按当前选中版本复制原始文字（保留 Markdown）；应用不新增磁盘历史；系统剪贴板历史可能保留复制内容。\n"+resultLabel.ToolTip;
    }
}
