using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private ListBox? settingsNavigation;private string? validationProfileId;
    private void ValidateProfileRows()
    {
        foreach(var profile in draft.Data.Profiles)
        {
            try{KuaiYi.Core.Settings.SettingsJson.Validate(new KuaiYi.Core.Settings.AppSettings{Profiles=[profile],ActiveProfileId=profile.Id});}
            catch(KuaiYi.Core.Settings.SettingsValidationException){validationProfileId=profile.Id;throw;}
        }
    }
    private readonly Dictionary<FrameworkElement,TextBlock> fieldIssues=[];
    private void RegisterFieldIssue(Panel panel,FrameworkElement field)
    {
        var issue=new TextBlock{TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,4,0,0),Visibility=Visibility.Collapsed};
        DesktopTheme.Bind(issue,TextBlock.ForegroundProperty,DesktopTheme.Error);
        AutomationProperties.SetLiveSetting(issue,AutomationLiveSetting.Polite);panel.Children.Add(issue);fieldIssues[field]=issue;
    }
    private void ClearFieldIssues()
    {foreach(var issue in fieldIssues.Values){issue.Text="";issue.Visibility=Visibility.Collapsed;}}
    private void ShowValidationIssue(string field)
    {
        if(validationProfileId is {} profileId&&draft.Data.Profiles.Any(p=>p.Id==profileId)&&selectedId!=profileId&&
            (field.StartsWith("profile.",StringComparison.Ordinal)||field.StartsWith("settings.Profiles",StringComparison.Ordinal)))
        {
            bool wasLoading=loading;loading=true;try{selectedId=profileId;profiles.SelectedValue=profileId;}finally{loading=wasLoading;}LoadProfile();
        }
        if(field.StartsWith("hotkeys:",StringComparison.Ordinal)&&hotkeyFields.TryGetValue(field.Split(':')[1],out var hotkey))
        {
            if(settingsNavigation!=null)settingsNavigation.SelectedIndex=4;
            if(fieldIssues.TryGetValue(hotkey,out var hotkeyIssue)){hotkeyIssue.Text="此组合未通过验证。请修改后重新保存；草稿保留。";hotkeyIssue.Visibility=Visibility.Visible;}
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>{if(IsVisible&&!saving&&!draft.IsClosed){hotkey.BringIntoView();hotkey.Focus();}});return;
        }
        if(field.StartsWith("settings.",StringComparison.Ordinal))field=field.Split('.').Last() switch
        {"FontSizeDip"=>"fontSizeDip","PreferredWidthDip"=>"preferredWidthDip","SelectionDebounceMs"=>"selectionDebounceMs","AutoSoftLimitScalars"=>"autoSoftLimitScalars","MaxOutputTokens"=>"profile.maxOutputTokens","TimeoutSeconds"=>"profile.timeoutSeconds","MaxContextTokens"=>"profile.maxContextTokens","ImageByteLimit"=>"profile.imageByteLimit","Temperature"=>"profile.temperature",_=>field};
        (FrameworkElement Target,int Page,bool Advanced) destination=field switch
        {
            "fontSizeDip"=>(font,3,false),"preferredWidthDip"=>(width,3,false),
            "selectionDebounceMs"=>(delay,3,false),"autoSoftLimitScalars"=>(softLimit,3,false),
            "activeProfileId"=>(textProfile,1,false),"visionProfileId"=>(visionProfile,1,false),
            "maxOutputTokens" or "profile.maxOutputTokens"=>(tokens,1,false),
            "timeoutSeconds" or "profile.timeoutSeconds"=>(timeout,1,false),
            "apiKey" or "apiKeyLength" or "profile.secretRef"=>(password,1,false),
            "profile.model"=>(model,1,false),"profile.name"=>(profileName,1,false),
            "profile.baseUrl" or "profile.baseIsCompleteEndpoint" or "profile.addressMode"=>(url,1,false),
            "profile.timeouts.connectSeconds"=>(connectTimeout,1,true),"profile.timeouts.headersSeconds"=>(headersTimeout,1,true),
            "profile.timeouts.firstResponseSeconds"=>(firstTimeout,1,true),"profile.timeouts.idleSeconds"=>(idleTimeout,1,true),
            "profile.endpointPath" or "profile.exactEndpointExtraPath"=>(endpointPath,1,true),
            "profile.temperature"=>(temperature,1,true),"profile.maxContextTokens" or "profile.outputExceedsContext"=>(contextTokens,1,true),
            "profile.imageByteLimit"=>(imageBytes,1,true),"profile.authenticationHeader" or "profile.duplicateAuthenticationHeader" or "profile.unusedAuthenticationHeader"=>(authHeader,1,true),
            "profile.headers" or "profile.duplicateHeader" or "profile.anthropicVersion"=>(headers,1,true),
            "profile.outputLimitParameter" or "profile.anthropicRequiresMaxTokens"=>(outputParameter,1,true),
            "profile.httpConsent"=>(Uri.TryCreate(url.Text,UriKind.Absolute,out var endpoint)&&endpoint.IsLoopback?localHttp:insecureHttp,1,true),
            "domain"=>(domain,2,false),"readerBackground"=>(background,2,false),"blockedApplications"=>(blocked,6,false),
            _ when field.StartsWith("translation.",StringComparison.Ordinal)=>(templateEditor,2,false),
            _=>(profiles,1,false)
        };
        var (Target,Page,Advanced)=destination;
        if(Advanced&&apiAdvanced!=null)apiAdvanced.IsExpanded=true;
        if(settingsNavigation!=null)settingsNavigation.SelectedIndex=Page;
        if(fieldIssues.TryGetValue(Target,out var issue))
        {issue.Text="此项未通过验证，请检查后再保存。草稿已保留。";issue.Visibility=Visibility.Visible;}
        // Save reenables the form in finally; focus only after that settlement and layout.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
        {if(IsVisible&&!saving&&!draft.IsClosed&&Target.IsEnabled){Target.BringIntoView();Target.Focus();}});
    }
}
