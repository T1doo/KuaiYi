using System.Security.Cryptography;
using System.Text;
using KuaiYi.Core.Settings;
namespace KuaiYi.Providers;
public sealed record PromptComposition(string BuiltInConstraints,string Style,string Context,string Regeneration,string System,string User,string TemplateVersion,bool IsCustom)
{public string FullTemplateVersion=>TemplateVersion+(Regeneration.Length>0?"+regenerate@1":"");}
public static class PromptBuilder
{
    public static PromptComposition Compose(TranslationSettings translation,string source,bool regenerate=false,string imageMode="text_only")
    {
        if(!Enum.IsDefined(translation.Preset))throw new SettingsValidationException("translation.preset");PromptTemplatePolicy.Validate(translation);
        string name=translation.Preset.ToString().ToLowerInvariant();bool custom=translation.CustomTemplates.TryGetValue(translation.Preset,out var template);template??=Read(name);
        string style=PromptTemplatePolicy.Render(template,PromptTemplatePolicy.Values(translation,imageMode));
        // Unused optional context remains data. No second replacement pass is allowed through inserted values.
        string context=custom?string.Join("\n",new[]{translation.Domain.Length>0&&!Uses(template,"domain")?"专业领域："+translation.Domain:"",translation.ReaderBackground.Length>0&&!Uses(template,"reader_background")?"读者背景："+translation.ReaderBackground:""}.Where(value=>value.Length>0)):"";
        string basis=Read("base"),regen=regenerate?Read("regenerate"):"";string system=basis+"\n"+style+(context.Length>0?"\n"+context:"")+(regen.Length>0?"\n"+regen:"");
        if(system.Length>PromptTemplatePolicy.MaximumComposedCharacters)throw new SettingsValidationException("translation.customTemplates.expansion");
        string version="base@1+"+(custom?$"custom-{name}@1:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(template))).ToLowerInvariant()}":$"{name}@1")+(context.Length>0?"+context@1":"");
        return new(basis,style,context,regen,system,source,version,custom);
    }
    private static bool Uses(string template,string variable)
    {int position=0;while((position=template.IndexOf("{{",position,StringComparison.Ordinal))>=0){int end=template.IndexOf("}}",position+2,StringComparison.Ordinal);if(end<0)return false;if(template[(position+2)..end].Trim()==variable)return true;position=end+2;}return false;}
    public static string Build(string style,string domain="",string readerBackground="",bool regenerate=false)
    {
        if(style is not ("faithful" or "plain" or "professional" or "explain")||domain.Length>2000||readerBackground.Length>2048)throw new ArgumentException("Invalid prompt selection or context.");
        return Compose(new(){Preset=Enum.Parse<TranslationPreset>(style,true),Domain=domain,ReaderBackground=readerBackground},"",regenerate).System;
    }
    public static string Read(string name)
    {
        if(name is not ("base" or "faithful" or "plain" or "professional" or "explain" or "image-classify" or "image-explain" or "regenerate"))throw new ArgumentException("Unknown prompt resource.");
        var assembly=typeof(PromptBuilder).Assembly;using var stream=assembly.GetManifestResourceStream("KuaiYi.Prompts."+name+"@1.txt")??throw new InvalidOperationException("Missing prompt resource.");using var reader=new StreamReader(stream,Encoding.UTF8);return reader.ReadToEnd();
    }
}
