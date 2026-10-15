// Additional WPF control/bitmap call ports only. No native WPF rendering or pixel storage is tested.
using KuaiYi.Imaging;
namespace System.Windows.Media
{
    internal enum Stretch { Uniform }
    internal static class PixelFormats {public static readonly object Bgra32=new();}
}
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        internal (ScreenshotIntent Intent,CancellationTokenSource Cancellation,Func<OwnedSourceImage,bool> Commit) ImageInputForTest()
        {
            var intent=new ScreenshotIntent(lifecycle.Gate);var cancel=new CancellationTokenSource();screenshotHotkeyStop=cancel;var admission=CaptureImageAdmission(cancel);
            return(intent,cancel,image=>AcceptLocalImage(admission,intent,image));
        }
        internal Guid? SelectedSourceVersionForTest=>SelectedSource.VersionId;
        internal OwnedSourceImage? SelectedImageForTest=>SelectedSource.Image;
        internal void ClearImagePresentationForTest()=>HidePresentation(clear:true);
        internal void HideImageWindowForTest(){lifecycle.Gate.Disable(hide:true);HidePresentation(clear:false);Hide();}
        private readonly Task startup=Task.CompletedTask,positionSave=Task.CompletedTask;
        internal Task SelectionExit=Task.CompletedTask,HotkeyExit=Task.CompletedTask,TranslationExit=Task.CompletedTask;
        internal int SelectionExitCalls,HotkeyExitCalls,TranslationExitCalls,PrivacyHides,PointerCancellations,HotkeyRecoveries;
        private void Hide(){IsVisible=false;PrivacyHides++;}
        private void CancelPointer()=>PointerCancellations++;
        private void RecoverHotkeys(bool locked)=>HotkeyRecoveries++;
        private Task StopSelection(){SelectionExitCalls++;return SelectionExit;}
        private Task StopHotkeys(){HotkeyExitCalls++;return HotkeyExit;}
        private Task StopTranslation(){TranslationExitCalls++;return TranslationExit;}
        internal void ApplyLockForTest(){lifecycle.Gate.SetSessionLocked(true);ApplySessionPresentation(true);}
        internal Task ExitContentForTest(){exiting=true;return SettleExitContent();}
        internal void SettingsExitForTest(Action closeSettings)=>PrepareSettingsExit(closeSettings);
    }
}
