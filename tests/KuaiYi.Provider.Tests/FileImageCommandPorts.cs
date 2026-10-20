using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    internal bool EnableFileForTest(Func<bool> confirm,Action enable,Action send)=>EnableAndSendOpenedImage(confirm,enable,send);
    internal Task OpenFileForTest(IFileImageOperation operation)=>StartOpenImage("PUBLIC_SYNTHETIC_FILE",()=>operation);
    internal Task SettleFileForTest()=>StopOpenImage();
    internal bool FileBusyForTest=>FileImageBusy;
    internal void ObserveFileForTest()=>ObserveFileImageState();
}
