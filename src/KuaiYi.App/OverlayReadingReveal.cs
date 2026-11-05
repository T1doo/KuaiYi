using System.Diagnostics;
using KuaiYi.Core;
namespace KuaiYi.App;

internal sealed partial class ShellWindow
{
    private readonly ReadingReveal readingReveal=new();
    private readonly Stopwatch readingClock=Stopwatch.StartNew();
    internal bool PresentationTextRevealPending{get;private set;}
    partial void PrepareReadingText(Guid? version,ref string text,ref bool complete)
    {
        bool immediate=complete||ReduceMotion||!IsVisible||!follow||peek||
            overlayResults.Identity?.VersionId!=version||
            overlayResults.Versions.FirstOrDefault(v=>v.Identity.VersionId==version)?.Stopped==true;
        int count=readingReveal.Advance(version,text,readingClock.Elapsed.TotalSeconds,immediate);
        PresentationTextRevealPending=count<text.Length;
        if(count<text.Length){text=text[..count];complete=false;dirty=true;if(!flush.IsEnabled)flush.Start();}
    }
    partial void ResetReadingReveal(){readingReveal.Reset();PresentationTextRevealPending=false;}
}
