using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
internal sealed partial class SettingsWindow
{
    private readonly ComboBox templateMode=Choice<TranslationPreset>();private readonly CheckBox previewRegeneration=new(){Content="预览重生成附加约束"};
    private readonly TextBox builtInTemplate=PromptTextBox(true),templateEditor=PromptTextBox(true),fixedConstraints=PromptTextBox(true),composedSystem=PromptTextBox(true),composedUser=PromptTextBox(true);
    private readonly TextBlock templateStatus=new(){TextWrapping=TextWrapping.Wrap};private PromptTemplateEditor? promptEditor;private bool loadingTemplates;
    private const string PublicPreviewSample="这是公开预览原文：12.5 mg，版本 2。";
    private static TextBox PromptTextBox(bool readOnly)=>new(){IsReadOnly=readOnly,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=100,MaxHeight=200};
    private static string ModeLabel(TranslationPreset mode)=>mode switch{TranslationPreset.Faithful=>"忠实翻译",TranslationPreset.Plain=>"通俗翻译",TranslationPreset.Professional=>"专业翻译",TranslationPreset.Explain=>"解释",_=>"未知模式"};
    private sealed class ModeConverter:IValueConverter
    {public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value is TranslationPreset mode?ModeLabel(mode):"";public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;}
    private static DataTemplate ModeDisplayTemplate()
    {var label=new FrameworkElementFactory(typeof(TextBlock));label.SetBinding(TextBlock.TextProperty,new Binding{Converter=new ModeConverter(),Mode=BindingMode.OneWay});return new(){VisualTree=label};}
    private StackPanel Translation()
    {
        var page=Page();promptEditor=new(()=>draft,ApiFormEdited);preset.ItemTemplate=templateMode.ItemTemplate=ModeDisplayTemplate();Field(page,"默认翻译风格",preset);Field(page,"专业领域",domain);Field(page,"读者背景",background);domain.MaxLength=512;background.MaxLength=2048;Field(page,"查看 / 编辑的模板模式（不改变默认风格）",templateMode);
        var actions=new StackPanel{Orientation=Orientation.Horizontal};Button view=new Button{Content="查看内置预设",Margin=new Thickness(4)},copy=new Button{Content="复制为自定义",Margin=new Thickness(4)},restore=new Button{Content="恢复该模式默认",Margin=new Thickness(4)},compose=new Button{Content="预览完整组合",Margin=new Thickness(4)};
        foreach(var item in new[]{view,copy,restore,compose})actions.Children.Add(item);page.Children.Add(actions);page.Children.Add(templateStatus);
        Field(page,"内置预设（只读）",builtInTemplate);Field(page,"本模式模板（先复制为自定义再编辑，最多 8192 字符）",templateEditor);templateEditor.MaxLength=PromptTemplatePolicy.MaximumTemplateCharacters;
        Note(page,"变量仅支持 {{target_language}}、{{domain}}、{{reader_background}}、{{source_text}}、{{image_mode}}。单次数据替换，不读取环境或运行代码；source_text 表示独立用户消息中的原文，不把原文嵌入 system。模板不授予采集、上传或外部工具权限。");
        page.Children.Add(previewRegeneration);Field(page,"固定内置安全约束（不可编辑，始终在组合最前）",fixedConstraints);Field(page,"当前预览模式的 system 完整组合（设置默认风格另见顶部）",composedSystem);Field(page,"实际 user 内容：仅公开预览样例，不读取当前选区 / 剪贴板",composedUser);
        view.Click+=(_,_)=>{builtInTemplate.Text=promptEditor!.BuiltIn;templateStatus.Text="当前内置预设仅供查看；自定义编辑未改变。";};
        copy.Click+=(_,_)=>TemplateCommand(()=>promptEditor!.CopyBuiltIn());restore.Click+=(_,_)=>TemplateCommand(()=>promptEditor!.RestoreDefault());compose.Click+=(_,_)=>PreviewTranslationTemplate();
        templateMode.SelectionChanged+=(_,_)=>
        {if(loadingTemplates||templateMode.SelectedItem is not TranslationPreset mode)return;try{promptEditor!.SelectMode(mode);LoadTemplateMode();}catch(SettingsValidationException error){loadingTemplates=true;templateMode.SelectedItem=promptEditor!.Mode;loadingTemplates=false;templateStatus.Text="请修正当前模板或恢复默认后切换："+error.Field;}};
        templateEditor.TextChanged+=(_,_)=>{if(loadingTemplates||promptEditor?.IsCustom!=true)return;promptEditor.SetText(templateEditor.Text);PreviewTranslationTemplate();};
        foreach(var field in new[]{domain,background})field.TextChanged+=(_,_)=>{if(loadingTemplates||draft.Basis.ReadOnly||draft.IsClosed)return;CaptureTemplateContext();};preset.SelectionChanged+=(_,_)=>{if(!loadingTemplates&&!draft.Basis.ReadOnly&&!draft.IsClosed)CaptureTemplateContext();};
        Closed+=(_,_)=>{loadingTemplates=true;foreach(var field in new[]{builtInTemplate,templateEditor,fixedConstraints,composedSystem,composedUser})field.Clear();promptEditor=null;};
        previewRegeneration.Checked+=(_,_)=>PreviewTranslationTemplate();previewRegeneration.Unchecked+=(_,_)=>PreviewTranslationTemplate();return page;
    }
    private void CaptureTemplateContext(){promptEditor?.UpdateContext((TranslationPreset)(preset.SelectedItem??TranslationPreset.Faithful),domain.Text,background.Text);PreviewTranslationTemplate();}
    private void TemplateCommand(Action command)
    {try{command();LoadTemplateMode();}catch(SettingsValidationException error){templateStatus.Text="模板未保存，请检查："+error.Field;}catch(InvalidOperationException){templateStatus.Text="当前草稿不可编辑，请关闭后重新打开。";}}
    private void LoadTranslationTemplates()
    {if(promptEditor==null)return;loadingTemplates=true;try{promptEditor.Reload();templateMode.SelectedItem=promptEditor.Mode;}finally{loadingTemplates=false;}LoadTemplateMode();}
    private void LoadTemplateMode()
    {
        if(promptEditor==null)return;loadingTemplates=true;try{builtInTemplate.Text=promptEditor.BuiltIn;templateEditor.IsReadOnly=!promptEditor.IsCustom;templateEditor.Text=promptEditor.Text;fixedConstraints.Text=PromptBuilder.Read("base");}finally{loadingTemplates=false;}PreviewTranslationTemplate();
    }
    private void PreviewTranslationTemplate()
    {
        if(promptEditor==null)return;
        try
        {
            var combination=promptEditor.Preview(PublicPreviewSample,draft.Data.Translation with{Domain=domain.Text,ReaderBackground=background.Text},previewRegeneration.IsChecked==true);
            fixedConstraints.Text=combination.BuiltInConstraints;composedSystem.Text=combination.System;composedUser.Text=combination.User;
            templateStatus.Text=$"预览模式 {ModeLabel(promptEditor.Mode)}（默认 {ModeLabel((TranslationPreset)(preset.SelectedItem??TranslationPreset.Faithful))}） · {(combination.IsCustom?"用户自定义":"内置预设")} · {combination.FullTemplateVersion}。固定安全约束 → 风格模板 → 可选领域/背景 → 重生成约束；user 样例单独发送。当前预览不发请求。";
        }
        catch(SettingsValidationException error){composedSystem.Clear();composedUser.Clear();templateStatus.Text="模板或上下文无效，不能保存 / 发送："+error.Field;}
    }
}
