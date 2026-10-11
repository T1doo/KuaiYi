using System.Collections.Immutable;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
namespace KuaiYi.App;
/// <summary>Normal activating settings window. Editing is draft-only; no provider, selection or old-secret read.</summary>
internal sealed partial class SettingsWindow:Window
{
    private readonly SettingsCoordinator coordinator;private readonly SettingsRecoveryDraft recoveryDraft;
    private readonly string? clipboardApplication;
    private SettingsDraft draft=>recoveryDraft.Draft;
    private readonly Grid body=new();private readonly TextBlock status=new(){TextWrapping=TextWrapping.Wrap,Margin=new Thickness(8)};
    private readonly Button save=new(){Content="保存",MinWidth=80,Margin=new Thickness(6)};
    private readonly Button cancel=new(){Content="取消",MinWidth=80,Margin=new Thickness(6),IsCancel=true};
    private readonly ComboBox recovery=new(){MinWidth=200,Margin=new Thickness(8)};
    private readonly ComboBox mode=Choice<TriggerMode>(),preset=Choice<TranslationPreset>(),motion=Choice<MotionPreference>(),theme=Choice<ThemePreference>();
    private readonly TextBox font=new(),width=new(),delay=new(),softLimit=new(),domain=new(),background=new(),blocked=new();
    private readonly CheckBox remember=new(){Content="记住浮窗位置"},preview=new(){Content="截图发送前预览"},smart=new(){Content="智能图片路由"};
    private readonly ComboBox profiles=new(){MinWidth=200,DisplayMemberPath="Name",SelectedValuePath="Id"},textProfile=new(){DisplayMemberPath="Name",SelectedValuePath="Id"},visionProfile=new(){DisplayMemberPath="Name",SelectedValuePath="Id"};
    private readonly ComboBox protocol=Choice<ApiProtocol>(),addressMode=Choice<ApiAddressMode>(),authentication=Choice<ApiAuthentication>();
    private readonly TextBox profileName=new(),url=new(),model=new(),tokens=new(),timeout=new(),endpointPreview=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap};
    private readonly PasswordBox password=new(){MaxLength=8192};private readonly TextBox reveal=new(){MaxLength=8192,Visibility=Visibility.Collapsed};
    private readonly CheckBox stream=new(){Content="流式输出"};private readonly TextBlock keyStatus=new();
    private readonly DispatcherTimer revealTimer=new(){Interval=TimeSpan.FromSeconds(5)};
    private string? selectedId;private bool loading,saving,committed,changingRecovery,closeWhenSettled;
    public void RequestCloseWhenSettled(){closeWhenSettled=true;Mask();Close();}
    public SettingsWindow(SettingsCoordinator coordinator,SettingsDraft initialDraft,string? clipboardApplication=null)
    {
        this.coordinator=coordinator;this.clipboardApplication=clipboardApplication;recoveryDraft=new(initialDraft);SetupPlacement();Title="快译设置";ShowInTaskbar=false;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        var work=SystemParameters.WorkArea;Width=Math.Min(860,work.Width);Height=Math.Min(660,work.Height);MinWidth=Math.Min(700,work.Width);MinHeight=Math.Min(520,work.Height);MaxWidth=work.Width;MaxHeight=work.Height;
        var root=new DockPanel();var footer=new StackPanel();DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);
        footer.Children.Add(status);var actions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};actions.Children.Add(recovery);actions.Children.Add(save);actions.Children.Add(cancel);footer.Children.Add(actions);
        var nav=new ListBox{Width=150,Margin=new Thickness(8),ItemsSource=new[]{"常规","API 与模型","翻译风格","交互与浮窗","快捷键","图片与集成","隐私与诊断"}};DockPanel.SetDock(nav,Dock.Left);root.Children.Add(nav);root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Content=root;
        var pages=new[]{General(),Api(),Translation(),Overlay(),Hotkeys(),Images(),Privacy()};foreach(var page in pages){page.Visibility=Visibility.Collapsed;body.Children.Add(page);}
        nav.SelectionChanged+=(_,_)=>{foreach(var p in pages)p.Visibility=Visibility.Collapsed;if(nav.SelectedIndex>=0)pages[nav.SelectedIndex].Visibility=Visibility.Visible;Mask();};nav.SelectedIndex=0;
        LoadGeneral();RefreshProfiles();
        recovery.ItemsSource=new[]{new ComboBoxItem{Content="选择恢复方式"},new ComboBoxItem{Content="恢复上版备份",IsEnabled=draft.Basis.State==SettingsLoadState.RecoverableBackup},new ComboBoxItem{Content="使用安全默认值"}};recovery.SelectedIndex=0;recovery.Visibility=draft.Basis.RequiresRecovery?Visibility.Visible:Visibility.Collapsed;
        recovery.SelectionChanged+=(_,_)=>RecoverySelectionChanged();
        status.Text=draft.Basis.State switch{SettingsLoadState.ReadOnlyUnknown=>"检测到不支持的配置版本。只读打开，文件与密钥均保留。",SettingsLoadState.RecoverableBackup=>"配置损坏或缺失。可恢复上版备份，或使用安全默认值；未保存前不会覆盖原文件。",SettingsLoadState.RecoverableDefaults=>"配置无法读取为有效设置。请明确选择安全默认值；原文件保留。",SettingsLoadState.Migrated=>"旧布局草稿已转换为当前格式。保存后才写入；旧版保留为备份。",_=>"修改仅在草稿中。保存不自动启用翻译，也不发送请求。"};
        if(draft.Basis.ReadOnly){body.IsEnabled=false;save.IsEnabled=false;}
        textProfile.SelectionChanged+=(_,_)=>{if(!loading&&!draft.Basis.ReadOnly)draft.Update(s=>s with{ActiveProfileId=string.IsNullOrEmpty(textProfile.SelectedValue as string)?null:(string)textProfile.SelectedValue});};
        visionProfile.SelectionChanged+=(_,_)=>{if(!loading&&!draft.Basis.ReadOnly)draft.Update(s=>s with{VisionProfileId=string.IsNullOrEmpty(visionProfile.SelectedValue as string)?null:(string)visionProfile.SelectedValue});};
        profiles.SelectionChanged+=(_,_)=>{if(loading)return;if(!TryCaptureProfile()){loading=true;profiles.SelectedValue=selectedId;loading=false;return;}selectedId=profiles.SelectedValue as string;LoadProfile();};
        save.Click+=async(_,_)=>await Save();cancel.Click+=(_,_)=>Close();revealTimer.Tick+=(_,_)=>Mask();Deactivated+=(_,_)=>Mask();password.LostKeyboardFocus+=(_,_)=>Mask();reveal.LostKeyboardFocus+=(_,_)=>Mask();
        Closing+=(_,e)=>{if(saving){e.Cancel=true;status.Text="保存任务尚未完成，请等待实际结果。";}else Mask();};
        Closed+=(_,_)=>{revealTimer.Stop();password.Clear();reveal.Clear();recoveryDraft.Dispose();};
    }
    private static ComboBox Choice<T>()where T:struct,Enum=>new(){ItemsSource=Enum.GetValues<T>(),MinWidth=160};
    private static StackPanel Page()=>new(){Margin=new Thickness(16)};
    private static void Field(Panel panel,string label,UIElement input){panel.Children.Add(new TextBlock{Text=label,Margin=new Thickness(0,10,0,4)});panel.Children.Add(input);}
    private static void Note(Panel panel,string text)=>panel.Children.Add(new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,8,0,8)});
    private StackPanel General(){var p=Page();Note(p,"启动始终未启用翻译。保存设置不恢复 On。主浮钮和托盘使用同一启停状态；隐藏会停用。开机启动注册尚待安装集成验证，本页不进行系统注册。");return p;}
    private StackPanel Api()
    {
        var p=Page();Field(p,"配置",profiles);var row=new StackPanel{Orientation=Orientation.Horizontal};var add=new Button{Content="新增配置",Margin=new Thickness(4)};var remove=new Button{Content="删除此配置（保存后生效）",Margin=new Thickness(4)};row.Children.Add(add);row.Children.Add(remove);p.Children.Add(row);
        add.Click+=(_,_)=>{if(!TryCaptureProfile())return;var profile=new ApiProfile{Id=Guid.NewGuid().ToString("N")};draft.Update(s=>s with{Profiles=s.Profiles.Add(profile)});selectedId=profile.Id;RefreshProfiles();};
        remove.Click+=(_,_)=>{if(selectedId==null)return;string id=selectedId;draft.Update(s=>s with{Profiles=s.Profiles.Where(p=>p.Id!=id).ToImmutableArray(),ActiveProfileId=s.ActiveProfileId==id?null:s.ActiveProfileId,VisionProfileId=s.VisionProfileId==id?null:s.VisionProfileId});selectedId=null;RefreshProfiles();};
        Field(p,"名称",profileName);Field(p,"协议",protocol);Field(p,"地址模式",addressMode);Field(p,"Base URL / 完整端点",url);Field(p,"最终端点预览",endpointPreview);Field(p,"模型 ID（可手动填写）",model);Field(p,"认证方式",authentication);Field(p,"API Key（留空保留现有引用）",password);p.Children.Add(reveal);p.Children.Add(keyStatus);
        var keyButtons=new StackPanel{Orientation=Orientation.Horizontal};var show=new Button{Content="短时显示新输入",Margin=new Thickness(4)};var clear=new Button{Content="清除密钥（保存后生效）",Margin=new Thickness(4)};keyButtons.Children.Add(show);keyButtons.Children.Add(clear);p.Children.Add(keyButtons);
        show.Click+=(_,_)=>{reveal.Text=password.Password;password.Visibility=Visibility.Collapsed;reveal.Visibility=Visibility.Visible;reveal.Focus();revealTimer.Start();};
        clear.Click+=(_,_)=>{if(selectedId==null)return;Mask();password.Clear();draft.ClearKey(selectedId);keyStatus.Text="本草稿已清除引用；取消会保留原密钥。";};
        Field(p,"最大输出 token",tokens);Field(p,"请求超时（秒）",timeout);p.Children.Add(stream);Field(p,"默认文字配置（空为未配置）",textProfile);Field(p,"默认视觉配置（能力默认为未知）",visionProfile);
        Note(p,"本次没有连接测试或模型调用。能力默认未知；完整 API 高级参数与能力测试在 P4 交付。密钥只在保存时加密，不读取旧密钥到表单。");url.TextChanged+=(_,_)=>PreviewEndpoint();protocol.SelectionChanged+=(_,_)=>PreviewEndpoint();addressMode.SelectionChanged+=(_,_)=>PreviewEndpoint();return p;
    }
    private StackPanel Translation(){var p=Page();Field(p,"默认翻译风格",preset);Field(p,"专业领域",domain);Field(p,"读者背景",background);Note(p,"修改影响后续任务。预设提示词已在资源中；自定义模板编辑与完整预览随翻译链路交付。");return p;}
    private StackPanel Overlay(){var p=Page();Field(p,"触发模式",mode);Field(p,"选区稳定等待（80—800 ms）",delay);Field(p,"自动发送软上限（1000—50000 字符）",softLimit);Field(p,"字号（12—22 DIP）",font);Field(p,"首选宽度（240—960 DIP）",width);Field(p,"主题",theme);Field(p,"减少动画",motion);p.Children.Add(remember);var reset=new Button{Content="重置已保存浮窗位置（保存后生效）"};reset.Click+=(_,_)=>draft.Update(s=>s with{Overlay=s.Overlay with{LastPosition=null}});p.Children.Add(reset);Note(p,"自动划词与完整翻译浮窗尚未交付。设置模式不授予采集或上传权限。");return p;}
    private StackPanel Hotkeys(){var p=Page();foreach(var pair in AppSettings.DefaultHotkeys)Note(p,pair.Key+"："+(draft.Data.Hotkeys.GetValueOrDefault(pair.Key)??"未分配"));Note(p,"完整录制、冲突检测与成组注册事务待 P3；本页保留配置，不修改已注册热键。");return p;}
    private StackPanel Images(){var p=Page();p.Children.Add(smart);p.Children.Add(preview);Note(p,"这些是后续图片翻译的偏好。资源管理器与浏览器真实安装状态不由本页修改或伪报成功。");return p;}
    private StackPanel Privacy(){var p=Page();Field(p,"排除应用（每行一个应用标识）",blocked);blocked.AcceptsReturn=true;blocked.MinHeight=100;Note(p,"磁盘历史、遥测和默认复制兼容固定关闭。应用授权与端点发送同意不会通过保存草稿自动授予。密钥删除在保存提交后生效。");var permission=new Button{Content="复制兼容：逐应用授权 / 撤销（即时生效）",Margin=new Thickness(0,8,0,8)};permission.Click+=(_,_)=>{var window=new ClipboardPermissionsWindow(coordinator,clipboardApplication){Owner=this};window.ShowDialog();status.Text="应用授权单独即时生效。若有修改，请关闭并重新打开设置后保存其他草稿，避免覆盖新配置。";};p.Children.Add(permission);return p;}
    private void LoadGeneral()
    {
        var s=draft.Data;mode.SelectedItem=s.TriggerMode;preset.SelectedItem=s.Translation.Preset;domain.Text=s.Translation.Domain;background.Text=s.Translation.ReaderBackground;delay.Text=s.Translation.SelectionDebounceMs.ToString(CultureInfo.InvariantCulture);softLimit.Text=s.Translation.AutoSoftLimitScalars.ToString(CultureInfo.InvariantCulture);font.Text=s.Overlay.FontSizeDip.ToString(CultureInfo.InvariantCulture);width.Text=s.Overlay.PreferredWidthDip.ToString(CultureInfo.InvariantCulture);motion.SelectedItem=s.Overlay.ReduceMotion;theme.SelectedItem=s.Overlay.Theme;remember.IsChecked=s.Overlay.RememberPosition;smart.IsChecked=s.Images.SmartRouting;preview.IsChecked=s.Images.PreviewScreenshotBeforeSend;blocked.Text=string.Join(Environment.NewLine,s.Privacy.BlockedApplications);
    }
    private void RefreshProfiles()
    {
        loading=true;profiles.ItemsSource=draft.Data.Profiles;profiles.SelectedValue=selectedId;var choices=new[]{new ApiProfile{Id="",Name="未配置"}}.Concat(draft.Data.Profiles).ToArray();textProfile.ItemsSource=choices;visionProfile.ItemsSource=choices;textProfile.SelectedValue=draft.Data.ActiveProfileId??"";visionProfile.SelectedValue=draft.Data.VisionProfileId??"";loading=false;LoadProfile();
    }
    private void LoadProfile()
    {
        Mask();password.Clear();var p=draft.Data.Profiles.FirstOrDefault(p=>p.Id==selectedId);foreach(var input in new Control[]{profileName,url,model,protocol,addressMode,authentication,password,reveal,tokens,timeout,stream})input.IsEnabled=p!=null;profileName.Text=p?.Name??"";url.Text=p?.BaseUrl??"";model.Text=p?.Model??"";protocol.SelectedItem=p?.Protocol??ApiProtocol.OpenAIChat;addressMode.SelectedItem=p?.AddressMode??ApiAddressMode.BasePrefix;authentication.SelectedItem=p?.Authentication??ApiAuthentication.Bearer;tokens.Text=(p?.MaxOutputTokens??4096).ToString(CultureInfo.InvariantCulture);timeout.Text=(p?.TimeoutSeconds??60).ToString(CultureInfo.InvariantCulture);stream.IsChecked=p?.Streaming??true;keyStatus.Text=p?.SecretRef==null?"未保存密钥。":"已保存加密密钥引用。";PreviewEndpoint();
    }
    private bool TryCaptureProfile(){try{CaptureProfile();return true;}catch(SettingsValidationException error){status.Text="请先检查当前配置项："+error.Field;return false;}}
    private void CaptureProfile()
    {
        if(selectedId==null||draft.Basis.ReadOnly)return;Mask();string id=selectedId;
        if(password.Password.Length>0){byte[] bytes=Encoding.UTF8.GetBytes(password.Password);try{draft.SetKey(id,bytes);}finally{CryptographicOperations.ZeroMemory(bytes);password.Clear();}}
        draft.Update(s=>s with{Profiles=s.Profiles.Select(p=>p.Id==id?p with{Name=profileName.Text,BaseUrl=url.Text,Model=model.Text,Protocol=(ApiProtocol)(protocol.SelectedItem??ApiProtocol.OpenAIChat),AddressMode=(ApiAddressMode)(addressMode.SelectedItem??ApiAddressMode.BasePrefix),Authentication=(ApiAuthentication)(authentication.SelectedItem??ApiAuthentication.Bearer),MaxOutputTokens=Integer(tokens.Text,"maxOutputTokens"),TimeoutSeconds=Integer(timeout.Text,"timeoutSeconds"),Streaming=stream.IsChecked==true}:p).ToImmutableArray()});
    }
    private void PreviewEndpoint(){string path=(ApiProtocol)(protocol.SelectedItem??ApiProtocol.OpenAIChat)==ApiProtocol.AnthropicMessages?"messages":"chat/completions";endpointPreview.Text=(ApiAddressMode)(addressMode.SelectedItem??ApiAddressMode.BasePrefix)==ApiAddressMode.ExactEndpoint?url.Text:url.Text.TrimEnd('/')+"/"+path;}
    private void Mask(){revealTimer.Stop();if(reveal.Visibility==Visibility.Visible){password.Password=reveal.Text;reveal.Clear();reveal.Visibility=Visibility.Collapsed;password.Visibility=Visibility.Visible;}}
    private static int Integer(string text,string field)=>int.TryParse(text,NumberStyles.Integer,CultureInfo.InvariantCulture,out int value)?value:throw new SettingsValidationException(field);
    private static double Number(string text,string field)=>double.TryParse(text,NumberStyles.Float,CultureInfo.InvariantCulture,out double value)?value:throw new SettingsValidationException(field);
    private void RestoreRecoverySelection()
    {changingRecovery=true;try{recovery.SelectedIndex=recoveryDraft.Choice==SettingsRecovery.UseBackup?1:recoveryDraft.Choice==SettingsRecovery.UseDefaults?2:0;}finally{changingRecovery=false;}}
    private void RecoverySelectionChanged()
    {
        if(changingRecovery)return;
        if(saving||recoveryDraft.IsSaving||draft.IsClosed||draft.Basis.ReadOnly){RestoreRecoverySelection();return;}
        var choice=recovery.SelectedIndex==1?SettingsRecovery.UseBackup:recovery.SelectedIndex==2?SettingsRecovery.UseDefaults:SettingsRecovery.None;
        if(choice==recoveryDraft.Choice)return;
        try
        {
            CaptureProfile();CapturePreferences();
            if(!recoveryDraft.Select(choice)){RestoreRecoverySelection();return;}
            selectedId=draft.Data.Profiles.Any(p=>p.Id==selectedId)?selectedId:draft.Data.ActiveProfileId??draft.Data.Profiles.FirstOrDefault()?.Id;
            LoadGeneral();RefreshProfiles();status.Text=choice switch{SettingsRecovery.UseBackup=>"当前显示上版备份草稿及其编辑。保存此草稿；切换不会丢失另一份草稿。",SettingsRecovery.UseDefaults=>"当前显示安全默认值草稿及其编辑。保存此草稿；原备份在保存前保留。",_=>"尚未选择恢复方式。当前显示原恢复草稿及其编辑；选择后才能保存。"};
        }
        catch(SettingsValidationException error){RestoreRecoverySelection();status.Text="请先检查当前草稿项，再切换恢复方式："+error.Field;}
        catch(SettingsRecoveryRequiredException){RestoreRecoverySelection();status.Text="没有可读取的备份。请选择安全默认值，原文件保留。";}
    }
    private void CapturePreferences()
    {
        draft.Update(s=>s with{TriggerMode=(TriggerMode)mode.SelectedItem,ActiveProfileId=string.IsNullOrEmpty(textProfile.SelectedValue as string)?null:(string)textProfile.SelectedValue,VisionProfileId=string.IsNullOrEmpty(visionProfile.SelectedValue as string)?null:(string)visionProfile.SelectedValue,Translation=s.Translation with{Preset=(TranslationPreset)preset.SelectedItem,Domain=domain.Text,ReaderBackground=background.Text,SelectionDebounceMs=Integer(delay.Text,"selectionDebounceMs"),AutoSoftLimitScalars=Integer(softLimit.Text,"autoSoftLimitScalars")},Overlay=s.Overlay with{FontSizeDip=Number(font.Text,"fontSizeDip"),PreferredWidthDip=Number(width.Text,"preferredWidthDip"),ReduceMotion=(MotionPreference)motion.SelectedItem,Theme=(ThemePreference)theme.SelectedItem,RememberPosition=remember.IsChecked==true},Images=s.Images with{SmartRouting=smart.IsChecked==true,PreviewScreenshotBeforeSend=preview.IsChecked==true},Privacy=s.Privacy with{BlockedApplications=blocked.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).ToImmutableArray()}});
    }
    private async Task Save()
    {
        if(saving||draft.Basis.ReadOnly)return;
        try
        {
            CaptureProfile();CapturePreferences();
            saving=true;save.IsEnabled=cancel.IsEnabled=body.IsEnabled=recovery.IsEnabled=false;status.Text="正在保存草稿…";
            await recoveryDraft.SaveAsync(coordinator);committed=true;
        }
        catch(Exception error){status.Text=error is SettingsValidationException v?"请检查设置项："+v.Field:error is SettingsConflictException?"配置已被另一份草稿修改。请取消后重新打开，避免覆盖。":draft.IsClosed?"配置已提交，通知或密钥清理尚需恢复。关闭后重新打开设置进行核对。":"保存未确认 · "+error.GetType().Name+"；原配置或事务保留。";}
        finally{saving=false;if(!draft.IsClosed)save.IsEnabled=cancel.IsEnabled=body.IsEnabled=recovery.IsEnabled=!draft.Basis.ReadOnly;else cancel.IsEnabled=true;}
        if(committed||closeWhenSettled)Close();
    }
}
