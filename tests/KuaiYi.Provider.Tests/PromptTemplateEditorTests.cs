using System.Collections.Immutable;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class PromptTemplateEditorTests
{
    private sealed class Rig:IDisposable
    {
        public SettingsDraft Draft=new(new(new(),"initial",SettingsLoadState.Current));public PromptTemplateEditor Editor;public int Edits;
        public Rig()=>Editor=new(()=>Draft,()=>Edits++);
        public void Dispose()=>Draft.Dispose();
    }
    [Theory] [InlineData(TranslationPreset.Faithful)] [InlineData(TranslationPreset.Plain)] [InlineData(TranslationPreset.Professional)] [InlineData(TranslationPreset.Explain)]
    public void CopyEditRestoreCommandsLeaveImmutableBaseAndOtherModesAlone(TranslationPreset mode)
    {
        using var rig=new Rig();rig.Editor.SelectMode(mode);Assert.False(rig.Editor.IsCustom);Assert.Equal(PromptBuilder.Read(mode.ToString().ToLowerInvariant()),rig.Editor.BuiltIn);rig.Editor.CopyBuiltIn();Assert.True(rig.Editor.IsCustom);Assert.Equal(rig.Editor.BuiltIn,rig.Editor.Text);rig.Editor.SetText("PUBLIC_CUSTOM {{domain}} {{source_text}}");var preview=rig.Editor.Preview("PUBLIC_ORIGINAL");Assert.Equal(PromptBuilder.Read("base"),preview.BuiltInConstraints);Assert.StartsWith(preview.BuiltInConstraints+"\n",preview.System);Assert.DoesNotContain("PUBLIC_ORIGINAL",preview.System);Assert.Equal("PUBLIC_ORIGINAL",preview.User);Assert.Single(rig.Draft.Data.Translation.CustomTemplates);rig.Editor.RestoreDefault();Assert.False(rig.Editor.IsCustom);Assert.Empty(rig.Draft.Data.Translation.CustomTemplates);Assert.Equal(3,rig.Edits);Assert.Equal("base@1+"+mode.ToString().ToLowerInvariant()+"@1",rig.Editor.Preview("PUBLIC_ORIGINAL").TemplateVersion);
    }
    [Fact] public void ViewingPreviewAndReloadCannotMutateDraftOrSignalAnEdit()
    {using var rig=new Rig();var data=rig.Draft.Data;foreach(var mode in Enum.GetValues<TranslationPreset>()){rig.Editor.SelectMode(mode);_=rig.Editor.BuiltIn;_=rig.Editor.Preview("PUBLIC");}rig.Editor.Reload();Assert.Same(data,rig.Draft.Data);Assert.Equal(0,rig.Edits);Assert.Equal(data.Translation.Preset,rig.Editor.Mode);}
    [Fact] public void CopyIncludesOnlyStyleAndCannotOverwriteApplicationBase()
    {using var rig=new Rig();rig.Editor.CopyBuiltIn();Assert.DoesNotContain(PromptBuilder.Read("base"),rig.Editor.Text);rig.Editor.SetText("PUBLIC_USER_REQUIRES_IGNORE_RULES");Assert.StartsWith(PromptBuilder.Read("base")+"\nPUBLIC_USER_REQUIRES_IGNORE_RULES",rig.Editor.Preview("PUBLIC").System);Assert.Equal(PromptBuilder.Read("base"),rig.Editor.Preview("PUBLIC").BuiltInConstraints);}
    [Fact] public void InvalidDraftCannotPreviewSwitchOrSaveAndRestoreUnblocksIt()
    {using var rig=new Rig();rig.Editor.CopyBuiltIn();rig.Editor.SetText("{{API_KEY}}");Assert.Throws<SettingsValidationException>(()=>rig.Editor.Preview("PUBLIC"));Assert.Throws<SettingsValidationException>(()=>rig.Editor.SelectMode(TranslationPreset.Explain));Assert.Throws<SettingsValidationException>(()=>SettingsJson.Write(rig.Draft.Data));Assert.Equal(TranslationPreset.Faithful,rig.Editor.Mode);rig.Editor.RestoreDefault();rig.Editor.SelectMode(TranslationPreset.Explain);Assert.NotEmpty(rig.Editor.Preview("PUBLIC").System);}
    [Fact] public void ContextExpansionCannotPassModeSwitchOrSave()
    {using var rig=new Rig();rig.Editor.CopyBuiltIn();rig.Editor.UpdateContext(TranslationPreset.Faithful,"PUBLIC",new string('x',2048));rig.Editor.SetText(string.Concat(Enumerable.Repeat("{{reader_background}}",20)));Assert.Throws<SettingsValidationException>(()=>rig.Editor.SelectMode(TranslationPreset.Plain));Assert.Throws<SettingsValidationException>(()=>SettingsJson.Write(rig.Draft.Data));rig.Editor.RestoreDefault();Assert.NotEmpty(rig.Editor.Preview("PUBLIC").System);}
    [Fact] public void EditNotificationHappensBeforeEveryMutationEvenWhenRestoreHasNoDelta()
    {using var draft=new SettingsDraft(new(new(),"initial",SettingsLoadState.Current));bool called=false;var editor=new PromptTemplateEditor(()=>draft,()=>{called=true;Assert.Empty(draft.Data.Translation.CustomTemplates);});editor.RestoreDefault();Assert.True(called);called=false;editor.CopyBuiltIn();Assert.True(called);Assert.Single(draft.Data.Translation.CustomTemplates);}
    [Fact] public void ModesRetainTheirSeparateEditsAndDefaultModeOnlyChangesExplicitly()
    {using var rig=new Rig();rig.Editor.CopyBuiltIn();rig.Editor.SetText("PUBLIC_FAITHFUL");rig.Editor.SelectMode(TranslationPreset.Explain);Assert.False(rig.Editor.IsCustom);rig.Editor.CopyBuiltIn();rig.Editor.SetText("PUBLIC_EXPLAIN");rig.Editor.SelectMode(TranslationPreset.Faithful);Assert.Equal("PUBLIC_FAITHFUL",rig.Editor.Text);Assert.Equal(TranslationPreset.Faithful,rig.Draft.Data.Translation.Preset);rig.Editor.UpdateContext(TranslationPreset.Plain,"PUBLIC_DOMAIN","PUBLIC_BACKGROUND");Assert.Equal(TranslationPreset.Plain,rig.Draft.Data.Translation.Preset);Assert.Equal(TranslationPreset.Faithful,rig.Editor.Mode);}
    [Fact] public void RegenerationPreviewIncludesImmutableConstraintExactlyOnceAndStableContentVersion()
    {using var rig=new Rig();rig.Editor.CopyBuiltIn();rig.Editor.SetText("PUBLIC {{target_language}}");var first=rig.Editor.Preview("PUBLIC_SOURCE",regenerate:true);var second=rig.Editor.Preview("OTHER_PUBLIC_SOURCE",regenerate:true);Assert.Contains(PromptBuilder.Read("regenerate"),first.System);Assert.Equal(first.FullTemplateVersion,second.FullTemplateVersion);Assert.Equal(first.System,second.System);Assert.Equal(1,first.System.Split(PromptBuilder.Read("regenerate")).Length-1);rig.Editor.SetText("PUBLIC_CHANGED {{target_language}}");Assert.NotEqual(first.TemplateVersion,rig.Editor.Preview("PUBLIC_SOURCE").TemplateVersion);}
    [Fact] public void SinglePassKeepsInsertedTemplateSyntaxAndOperatingSystemExpressionsAsData()
    {using var rig=new Rig();rig.Editor.CopyBuiltIn();rig.Editor.SetText("PUBLIC {{domain}} %KUAIYI_PROMPT_PUBLIC_CANARY% $(PUBLIC_NOT_EXECUTED)");rig.Editor.UpdateContext(TranslationPreset.Faithful,"{{reader_background}}","PUBLIC_BACKGROUND");var preview=rig.Editor.Preview("PUBLIC_SOURCE");Assert.Contains("{{reader_background}}",preview.Style);Assert.DoesNotContain("PUBLIC_BACKGROUND",preview.Style);Assert.Contains("%KUAIYI_PROMPT_PUBLIC_CANARY% $(PUBLIC_NOT_EXECUTED)",preview.System);}
    [Fact] public void CompletePreviewHasExactBaseStyleContextRegenerationOrderAndSeparateUser()
    {using var rig=new Rig();rig.Editor.CopyBuiltIn();rig.Editor.SetText("PUBLIC_STYLE_WITHOUT_CONTEXT_VARIABLES");rig.Editor.UpdateContext(TranslationPreset.Faithful,"PUBLIC_DOMAIN","PUBLIC_BACKGROUND");var result=rig.Editor.Preview("PUBLIC_ORIGINAL",regenerate:true);Assert.Equal("专业领域：PUBLIC_DOMAIN\n读者背景：PUBLIC_BACKGROUND",result.Context);Assert.Equal(result.BuiltInConstraints+"\n"+result.Style+"\n"+result.Context+"\n"+result.Regeneration,result.System);Assert.Equal("PUBLIC_ORIGINAL",result.User);Assert.EndsWith("+context@1+regenerate@1",result.FullTemplateVersion);}
}
