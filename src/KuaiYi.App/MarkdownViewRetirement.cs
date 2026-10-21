using System.Windows.Documents;
namespace KuaiYi.App;
internal sealed partial class MarkdownFlowView
{
    // A single exact retired batch, plus one not-yet-published image region. No unbounded cleanup queue.
    private Action?[]? retiredViews;
    private bool retiringViews;
    private long viewInstallation;
    private MarkdownImageSourceRegion? stagedImage;
    private void ClearStagedImage()
    {var held=stagedImage;if(held==null)return;held.Dispose();if(ReferenceEquals(stagedImage,held))stagedImage=null;}
    private void RetryRetired()
    {
        if(retiredViews is not {} batch||retiringViews)return;
        retiringViews=true;List<Exception> errors=[];
        try
        {
            for(int i=0;i<batch.Length;i++)if(batch[i] is {} original)
                try{original();batch[i]=null;}catch(Exception error){errors.Add(error);}
            if(errors.Count==0&&ReferenceEquals(retiredViews,batch))retiredViews=null;
        }
        finally{retiringViews=false;}
        if(errors.Count!=0)throw new ScreenshotCleanupException(errors);
    }
    private void RetireActive(bool full)
    {
        bool HasActive()=>sourceRegion!=null||imageSourceRegion!=null||sourceBlock!=null||full&&(elements.Count!=0||codeRegions.Count!=0||tableRegions.Count!=0||paragraphLinks.Count!=0);
        if(retiringViews)
        {
            // An old Dispose may reenter and build a new view while all active fields are detached.
            // It cannot replace the still-owned batch with another batch.
            if(HasActive())throw new InvalidOperationException("Original view cleanup still running.");return;
        }
        long before=viewInstallation;RetryRetired();if(before!=viewInstallation)return;
        List<Action?> actions=[];var document=host.Document;var oldText=sourceRegion;var oldImage=imageSourceRegion;var oldBlock=sourceBlock;
        if(oldText!=null)actions.Add(oldText.Dispose);if(oldImage!=null)actions.Add(oldImage.Dispose);
        if(full)
        {
            foreach(var item in paragraphLinks.Values.SelectMany(x=>x).ToArray())actions.Add(item.Dispose);
            foreach(var item in codeRegions.Values.ToArray())actions.Add(item.Dispose);
            foreach(var item in tableRegions.Values.ToArray())actions.Add(item.Dispose);
            // Never clear a global collection after an old Dispose can install a different source into it.
            foreach(Block item in document.Blocks.ToArray())actions.Add(()=>document.Blocks.Remove(item));
            paragraphLinks.Clear();elementParagraphs.Clear();codeRegions.Clear();tableRegions.Clear();applied.Clear();elements.Clear();appliedVersion=null;appliedFontSize=double.NaN;
        }
        else if(oldBlock!=null)actions.Add(()=>document.Blocks.Remove(oldBlock));
        sourceRegion=null;imageSourceRegion=null;sourceBlock=null;sourceVersion=null;
        if(actions.Count==0)return;retiredViews=actions.ToArray();RetryRetired();
    }
}
