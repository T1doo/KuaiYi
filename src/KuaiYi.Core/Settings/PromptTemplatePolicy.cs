using System.Text;
namespace KuaiYi.Core.Settings;
/// <summary>Data-only, single-pass placeholders. This layer never opens files, reads environment or executes code.</summary>
public static class PromptTemplatePolicy
{
    public const int MaximumTemplateCharacters=8192,MaximumComposedCharacters=32768,MaximumExpandedTemplateCharacters=28000;
    public const string SourceReference="[原文由独立 user 消息提供]";
    public static void Validate(TranslationSettings translation)
    {
        Context(translation.TargetLanguage,32);Context(translation.Domain,512);Context(translation.ReaderBackground,2048);
        if(translation.CustomTemplates==null||translation.CustomTemplates.Count>4)throw new SettingsValidationException("translation.customTemplates");
        foreach(var pair in translation.CustomTemplates)
        {if(!Enum.IsDefined(pair.Key))throw new SettingsValidationException("translation.customTemplates.mode");ValidateTemplate(pair.Value);_=Render(pair.Value,Values(translation,"text_only"));}
    }
    public static void ValidateTemplate(string template)
    {
        if(string.IsNullOrWhiteSpace(template)||template.Length>MaximumTemplateCharacters)throw new SettingsValidationException("translation.customTemplates.length");
        Characters(template,true);
        _=Render(template,Values(new(),"text_only"));
    }
    private static void Context(string value,int maximum){if(value==null||value.Length>maximum)throw new SettingsValidationException("translation.context");Characters(value,false);}
    private static void Characters(string value,bool multiline)
    {
        for(int i=0;i<value.Length;i++){char c=value[i];if(char.IsControl(c)&&(!multiline||c is not ('\r' or '\n' or '\t')))throw new SettingsValidationException("translation.customTemplates.character");if(char.IsHighSurrogate(c)){if(++i>=value.Length||!char.IsLowSurrogate(value[i]))throw new SettingsValidationException("translation.customTemplates.character");}else if(char.IsLowSurrogate(c))throw new SettingsValidationException("translation.customTemplates.character");}
    }
    public static IReadOnlyDictionary<string,string> Values(TranslationSettings translation,string imageMode)
    {
        if(imageMode is not ("text_only" or "diagram_or_mixed" or "visual_only" or "uncertain"))throw new SettingsValidationException("translation.imageMode");
        return new Dictionary<string,string>(StringComparer.Ordinal){["target_language"]=translation.TargetLanguage,["domain"]=translation.Domain,["reader_background"]=translation.ReaderBackground,["source_text"]=SourceReference,["image_mode"]=imageMode};
    }
    public static string Render(string template,IReadOnlyDictionary<string,string> values)
    {
        var output=new StringBuilder();int position=0;
        while(position<template.Length)
        {
            int start=template.IndexOf("{{",position,StringComparison.Ordinal);int closing=template.IndexOf("}}",position,StringComparison.Ordinal);
            if(closing>=0&&(start<0||closing<start))throw new SettingsValidationException("translation.customTemplates.placeholder");
            if(start<0){Append(template.AsSpan(position));break;}Append(template.AsSpan(position,start-position));
            int end=template.IndexOf("}}",start+2,StringComparison.Ordinal);if(end<0)throw new SettingsValidationException("translation.customTemplates.placeholder");
            string variable=template[(start+2)..end].Trim();if(variable is not ("target_language" or "domain" or "reader_background" or "source_text" or "image_mode")||!values.TryGetValue(variable,out var value)||value==null)throw new SettingsValidationException("translation.customTemplates.placeholder");
            Append(value.AsSpan());position=end+2;
        }
        return output.ToString();
        void Append(ReadOnlySpan<char> segment){if(segment.Length>MaximumExpandedTemplateCharacters-output.Length)throw new SettingsValidationException("translation.customTemplates.expansion");output.Append(segment);}
    }
    public static bool Equivalent(TranslationSettings first,TranslationSettings second)=>first with{CustomTemplates=second.CustomTemplates}==second&&first.CustomTemplates.Count==second.CustomTemplates.Count&&first.CustomTemplates.OrderBy(p=>p.Key).SequenceEqual(second.CustomTemplates.OrderBy(p=>p.Key));
}
