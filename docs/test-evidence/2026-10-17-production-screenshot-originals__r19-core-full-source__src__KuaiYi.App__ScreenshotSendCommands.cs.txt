using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    // The only send command. Merely accepting, previewing, opening a menu or changing settings never calls it.
    private bool SubmitScreenshot(ImageOutputMode mode,Func<ProductionScreenshotOffer,bool> confirm)
    {
        if(exiting||imageTranslation is not {} flow||SelectedSource.Image is not {} image||retainedImage is not {} retained||!ReferenceEquals(retained.Image,image))return false;
        var offer=flow.Prepare(retained.VersionId,image,mode);if(offer==null)return false;
        bool launched=false;
        try{if(!confirm(offer))return false;return launched=flow.Submit(offer,true);}
        finally{if(!launched)flow.Reject(offer);}
    }
}
