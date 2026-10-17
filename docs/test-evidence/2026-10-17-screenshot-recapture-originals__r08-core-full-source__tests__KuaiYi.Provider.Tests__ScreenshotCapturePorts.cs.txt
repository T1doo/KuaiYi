using KuaiYi.Core;
using KuaiYi.Imaging;
using System.Windows.Controls;
namespace KuaiYi.App;
// Only the native final-crop acquisition is replaced. Actual menu, managed intent/admission, ownership,
// tracked task, cleanup, Shell presentation and production HTTP coordinator are linked unchanged.
internal sealed partial class ShellWindow
{
    private readonly CancellationTokenSource lifetime=new();
    internal Func<ScreenshotIntent,CancellationToken,Task<OwnedSourceImage?>>? CapturePort=null;
    internal int CaptureCalls;
    private Task<OwnedSourceImage?> CaptureFinalScreenshot(ScreenshotIntent intent,CancellationTokenSource cancellation,long revision)
    {Interlocked.Increment(ref CaptureCalls);return (CapturePort??throw new InvalidOperationException("Public final-crop port required."))(intent,cancellation.Token);}
    internal ContextMenu ScreenshotRecoveryMenuForTest(){var menu=new ContextMenu();AddScreenshotRecovery(menu);return menu;}
    internal Task CaptureTaskForTest=>screenshotHotkeyTask;
    internal Task HotkeyCaptureForTest(HotkeyInputIdentity input)=>StartHotkeyScreenshot(input);
    internal void CancelCropForTest()=>screenshotHotkeyStop?.Cancel();
    internal void ObserveImageGateForTest()=>ObserveScreenshotState();
}
