using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    internal void DropFeedbackForTest(string phase,uint effect,KuaiYi.Core.OleTextFormat format)=>ApplyDropFeedback(phase,effect,format);
    internal void RevokeDropForFileTest()=>RevokeTextDrop();
    internal void EnableImageFileDropForTest(bool value){allowNonClearingDropRevocationForTest=true;SetImageFileDropEnabled(value);}
    internal Task DropImageFileForTest(string path,IFileImageOperation op)=>StartDroppedImageFile(path,()=>op);
    internal bool ImageFileDropEnabledForTest=>imageFileDropEnabled;
    internal bool EnableFileForTest(Func<bool> confirm,Action enable,Action send)=>EnableAndSendOpenedImage(confirm,enable,send);
    internal Task OpenFileForTest(IFileImageOperation operation)=>StartOpenImage("PUBLIC_SYNTHETIC_FILE",()=>operation);
    internal Task SettleFileForTest()=>StopOpenImage();
    internal bool FileBusyForTest=>FileImageBusy;
    internal void ObserveFileForTest()=>ObserveFileImageState();
}
