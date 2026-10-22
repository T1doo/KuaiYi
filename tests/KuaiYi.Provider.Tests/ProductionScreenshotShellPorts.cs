using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
// Only WPF/native surfaces are ports. The production send command, source selection, result bridge, markdown, labels and cleanup are linked unchanged.
internal sealed partial class ShellWindow
{
    internal ProductionScreenshotCoordinator ConfigureScreenshotForTest(SettingsSnapshot settings,ProviderClient provider,SynchronizationContext destination,Func<RequestIdentity,ScreenshotGenerationSnapshot,CancellationToken,Task<KuaiYi.Providers.SecretLease?>>? secret=null,Action<ProductionScreenshotUpdate>? observe=null)
    {
        lifecycle.Settings=settings;lifecycle.Gate.ObserveSettingsMetadata(settings);
        return imageTranslation=new(lifecycle.Gate,()=>lifecycle.Settings,ScreenshotSourceCurrent,provider,destination,overlayResults,update=>{ScreenshotTranslationChanged(update);observe?.Invoke(update);},secret??((_,_,_)=>throw new InvalidOperationException("AuthNone fixture must not open a secret.")));
    }
    internal bool SendScreenshotForTest(ImageOutputMode mode,Func<ProductionScreenshotOffer,bool> confirmation)
    {
        if(mode!=ImageOutputMode.Automatic)return SubmitScreenshot(mode,confirmation);
        if(!CanTranslateScreenshot||retainedImage is not {} retained||imageTranslation is not {} flow)return false;
        var approval=KuaiYi.Provider.Tests.SmartOcrFixture.Approve(lifecycle.Gate,()=>lifecycle.Settings,retained.VersionId,retained.Image,ScreenshotSourceCurrent);
        if(approval==null)return false;var offer=flow.Prepare(retained.VersionId,retained.Image,mode,approval);if(offer==null){approval.Dispose();return false;}
        bool launched=false;try{return launched=confirmation(offer)&&flow.Submit(offer,true);}finally{if(!launched)flow.Reject(offer);}
    }
    internal Task TrackSmartForTest(Func<Task> action)=>StartSmartImageWorkflow(action);
    internal Task SmartOriginalForTest=>smartImageTask;
    internal Task<bool> SendSmartForTest(KuaiYi.Imaging.SmartImageApproval approval,Func<ProductionScreenshotOffer,bool> confirm)=>SubmitSmartScreenshot(approval,confirm);
    internal string VersionDescriptionForTest(Guid id)=>VersionMenuLabel(overlayResults.Versions.Single(v=>v.Identity.VersionId==id));
    internal void SelectScreenshotVersionForTest(Guid id)=>SelectResultVersion(id);
    internal bool ScreenshotSendEnabledForTest=>CanTranslateScreenshot;
    internal bool LocalPreviewForTest=>LocalImagePreview;
    internal System.Windows.Controls.MenuItem ScreenshotStyleMenuForTest(){var menu=new System.Windows.Controls.ContextMenu();AddScreenshotStyleMenu(menu);return (System.Windows.Controls.MenuItem)menu.Items.Single();}
    internal TranslationPreset? ScreenshotPresetForTest=>SelectedScreenshotPreset;
    internal void ScreenshotSettingsForTest(SettingsSnapshot config){lifecycle.Settings=config;lifecycle.Gate.ObserveSettingsMetadata(config);imageTranslation?.ObserveSettings(config);RefreshResultLabel();}
}
