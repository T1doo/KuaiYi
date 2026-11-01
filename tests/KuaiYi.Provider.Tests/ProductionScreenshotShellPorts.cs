using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
// Only WPF/native surfaces are ports. The production send command, source selection, result bridge, markdown, labels and cleanup are linked unchanged.
internal sealed partial class ShellWindow
{
    private Action<ProductionScreenshotUpdate>? regenerationObserver;
    internal void RegenerationObserverForTest(Action<ProductionScreenshotUpdate> observe)=>regenerationObserver=observe;
    internal string? RegenerationStatusForTest=>button.ToolTip?.ToString();
    internal ProductionScreenshotCoordinator ConfigureScreenshotForTest(SettingsSnapshot settings,ProviderClient provider,SynchronizationContext destination,Func<RequestIdentity,ScreenshotGenerationSnapshot,CancellationToken,Task<KuaiYi.Providers.SecretLease?>>? secret=null,Action<ProductionScreenshotUpdate>? observe=null)
    {
        lifecycle.Settings=settings;lifecycle.Gate.ObserveSettingsMetadata(settings);
        return imageTranslation=new(lifecycle.Gate,()=>lifecycle.Settings,ScreenshotSourceCurrent,provider,destination,overlayResults,update=>{ScreenshotTranslationChanged(update);observe?.Invoke(update);regenerationObserver?.Invoke(update);},secret??((_,_,_)=>throw new InvalidOperationException("AuthNone fixture must not open a secret.")));
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
    // Synthetic UI-port driver invokes the real workflow below the fixed deployment gate.
    // It never modifies ImageRegenerationQualified or claims the normal UI is enabled.
    internal async Task<bool> RegenerateImageForTest(bool current,bool rejudge,ImageOutputMode? mode,Func<ProductionScreenshotOffer,bool> confirm,
        Func<Func<bool>,Func<KuaiYi.Imaging.OcrOnlyCoordinator,Task,RequestIdentity,bool>,Func<KuaiYi.Imaging.OcrOnlyCoordinator,bool,bool>,Func<KuaiYi.Imaging.OcrOnlyCoordinator,Task,bool,Task<bool>>,Task<KuaiYi.Imaging.SmartImageApproval?>>? approve=null)
    {
        bool launched=false;
        var original=StartImageRegeneration(current,rejudge,mode,async command=>
        {
            KuaiYi.Imaging.SmartImageApproval? approval=null;KuaiYi.Imaging.OcrOnlyCoordinator? originalOwner=null;List<Exception> errors=[];
            try
            {
                if(command.Mode==ImageOutputMode.Automatic&&approve!=null)
                    approval=await approve(()=>ImageRegenerationCurrent(command),(owner,task,identity)=>AdvanceImageRegenerationOcr(command,owner,task,identity),(owner,admitted)=>{originalOwner=owner;return ImageRegenerationOcrAdmission(command,owner,admitted);},(owner,task,advanced)=>SettleImageRegenerationOcr(command,owner,task,advanced));
                if(ImageRegenerationCurrent(command))launched=await SubmitSmartScreenshot(approval,confirm,command);
            }
            catch(Exception error){errors.Add(error);}
            finally{if(approval!=null)ScreenshotPreviewCleanup.Attempt(errors,approval.Dispose);if(originalOwner!=null)ReleaseImageRegenerationOcrOutcomes(command,originalOwner,errors);}
            if(errors.Count>0)throw new AggregateException("Original UI test-port workflow/cleanup failed.",errors);
        });
        await original;return launched;
    }
    internal Task QualifiedImageRegenerationForTest(Action invoked)=>StartQualifiedImageRegeneration(false,false,null,_=>{invoked();return Task.CompletedTask;});
    internal bool RegenerationQualifiedForTest=>ImageRegenerationQualified;
    internal Guid? RetainedImageVersionForTest=>retainedImage?.VersionId;
    internal bool RetainedImageCurrentForTest(Guid version,Guid source)=>ScreenshotSourceCurrent(version,source);
}
