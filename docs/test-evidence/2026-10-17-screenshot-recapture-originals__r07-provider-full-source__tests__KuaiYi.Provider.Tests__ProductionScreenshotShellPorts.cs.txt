using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
// Only WPF/native surfaces are ports. The production send command, source selection, result bridge, markdown, labels and cleanup are linked unchanged.
internal sealed partial class ShellWindow
{
    internal ProductionScreenshotCoordinator ConfigureScreenshotForTest(SettingsSnapshot settings,ProviderClient provider,SynchronizationContext destination)
    {
        lifecycle.Settings=settings;lifecycle.Gate.ObserveSettingsMetadata(settings);
        return imageTranslation=new(lifecycle.Gate,()=>lifecycle.Settings,ScreenshotSourceCurrent,provider,destination,overlayResults,ScreenshotTranslationChanged,(_,_,_)=>throw new InvalidOperationException("AuthNone fixture must not open a secret."));
    }
    internal bool SendScreenshotForTest(ImageOutputMode mode,Func<ProductionScreenshotOffer,bool> confirmation)=>SubmitScreenshot(mode,confirmation);
    internal string VersionDescriptionForTest(Guid id)=>VersionMenuLabel(overlayResults.Versions.Single(v=>v.Identity.VersionId==id));
    internal void SelectScreenshotVersionForTest(Guid id)=>SelectResultVersion(id);
    internal bool ScreenshotSendEnabledForTest=>CanTranslateScreenshot;
    internal bool LocalPreviewForTest=>LocalImagePreview;
}
