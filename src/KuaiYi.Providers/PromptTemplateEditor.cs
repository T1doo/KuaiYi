using KuaiYi.Core.Settings;
namespace KuaiYi.Providers;
/// <summary>Settings commands operate on a draft only. Preview/copy/reset never acquire source data or make HTTP calls.</summary>
public sealed class PromptTemplateEditor(Func<SettingsDraft> getDraft,Action beforeEdit)
{
    public TranslationPreset Mode{get;private set;}=getDraft().Data.Translation.Preset;
    public string BuiltIn=>PromptBuilder.Read(Mode.ToString().ToLowerInvariant());
    public bool IsCustom=>getDraft().Data.Translation.CustomTemplates.ContainsKey(Mode);
    public string Text=>getDraft().Data.Translation.CustomTemplates.GetValueOrDefault(Mode)??BuiltIn;
    public void Reload()=>Mode=getDraft().Data.Translation.Preset;
    public void SelectMode(TranslationPreset mode)
    {
        if(!Enum.IsDefined(mode))throw new SettingsValidationException("translation.preset");
        if(IsCustom)PromptTemplatePolicy.Validate(getDraft().Data.Translation with{CustomTemplates=System.Collections.Immutable.ImmutableDictionary<TranslationPreset,string>.Empty.Add(Mode,Text)});Mode=mode;
    }
    public void CopyBuiltIn()
    {beforeEdit();string text=BuiltIn;getDraft().Update(s=>s with{Translation=s.Translation with{CustomTemplates=s.Translation.CustomTemplates.SetItem(Mode,text)}});}
    public void SetText(string text)
    {
        if(!IsCustom)throw new InvalidOperationException("Copy the built-in style before editing.");
        // Invalid intermediate typing can exist in this draft. Save, mode-switch and preview validate it.
        beforeEdit();getDraft().Update(s=>s with{Translation=s.Translation with{CustomTemplates=s.Translation.CustomTemplates.SetItem(Mode,text)}});
    }
    public void RestoreDefault()
    {beforeEdit();getDraft().Update(s=>s with{Translation=s.Translation with{CustomTemplates=s.Translation.CustomTemplates.Remove(Mode)}});}
    public void UpdateContext(TranslationPreset preset,string domain,string background)
    {if(!Enum.IsDefined(preset))throw new SettingsValidationException("translation.preset");beforeEdit();getDraft().Update(s=>s with{Translation=s.Translation with{Preset=preset,Domain=domain,ReaderBackground=background}});}
    public PromptComposition Preview(string sample,TranslationSettings? context=null,bool regenerate=false)
    {var settings=context??getDraft().Data.Translation;return PromptBuilder.Compose(settings with{Preset=Mode,CustomTemplates=getDraft().Data.Translation.CustomTemplates},sample,regenerate);}
}
