using System.Collections.Immutable;
using System.Text;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class PromptTemplatePolicyTests
{
    private static TranslationSettings Settings(string text)=>new(){CustomTemplates=ImmutableDictionary<TranslationPreset,string>.Empty.Add(TranslationPreset.Faithful,text)};
    [Theory] [InlineData("empty")] [InlineData("whitespace")] [InlineData("long")] [InlineData("unknown")] [InlineData("environment")] [InlineData("expression")] [InlineData("open")] [InlineData("close")] [InlineData("nested")] [InlineData("nul")] [InlineData("surrogate")]
    public void InvalidTemplateCannotValidateOrSerialize(string kind)
    {
        string text=kind switch{"empty"=>"","whitespace"=>" \n\t","long"=>new string('x',8193),"unknown"=>"{{api_key}}","environment"=>"{{HOME}}","expression"=>"{{domain.ToString()}}","open"=>"{{domain","close"=>"domain}}","nested"=>"{{domain{{reader_background}}}}","nul"=>"bad\0","surrogate"=>"bad\ud800",_=>throw new ArgumentOutOfRangeException(nameof(kind))};
        Assert.Throws<SettingsValidationException>(()=>PromptTemplatePolicy.Validate(Settings(text)));Assert.Throws<SettingsValidationException>(()=>SettingsJson.Write(new(){Translation=Settings(text)}));
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void FourKnownModeKeysAreIndependentAndMaximumLengthAllowsMultiline(int mode)
    {var translation=new TranslationSettings{CustomTemplates=ImmutableDictionary<TranslationPreset,string>.Empty.Add((TranslationPreset)mode,"\n\t"+new string('x',8190))};PromptTemplatePolicy.Validate(translation);var bytes=SettingsJson.Write(new(){Translation=translation});var parsed=SettingsJson.Read(bytes,out _);Assert.Equal(translation.CustomTemplates[(TranslationPreset)mode],parsed.Translation.CustomTemplates[(TranslationPreset)mode]);}
    [Theory] [InlineData("Faithful")] [InlineData("Base")] [InlineData("faithful")] [InlineData("999")]
    public void ExactJsonMapKeysCannotOverrideFixedBaseOrSilentlyAlias(string key)
    {
        byte[] bytes=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"translation\":{\"customTemplates\":{\""+key+"\":\"PUBLIC\"}}}");
        if(key=="Faithful")Assert.Single(SettingsJson.Read(bytes,out _).Translation.CustomTemplates);else Assert.Throws<SettingsValidationException>(()=>SettingsJson.Read(bytes,out _));
    }
    [Fact] public void UndefinedEnumAndNullTemplateMapAreRejected()
    {Assert.Throws<SettingsValidationException>(()=>PromptTemplatePolicy.Validate(new(){CustomTemplates=ImmutableDictionary<TranslationPreset,string>.Empty.Add((TranslationPreset)999,"PUBLIC")}));Assert.Throws<SettingsValidationException>(()=>PromptTemplatePolicy.Validate(new(){CustomTemplates=null!}));Assert.Throws<SettingsValidationException>(()=>PromptTemplatePolicy.Validate(new(){CustomTemplates=ImmutableDictionary<TranslationPreset,string>.Empty.Add(TranslationPreset.Plain,null!)}));}
    [Fact] public void FiveVariablesRenderOnceAndSourceRemainsASeparateMessageReference()
    {
        var translation=new TranslationSettings{Domain="{{reader_background}}",ReaderBackground="PUBLIC_BACKGROUND",TargetLanguage="zh-CN"};string rendered=PromptTemplatePolicy.Render("{{target_language}}|{{domain}}|{{reader_background}}|{{source_text}}|{{image_mode}}",PromptTemplatePolicy.Values(translation,"text_only"));Assert.Equal("zh-CN|{{reader_background}}|PUBLIC_BACKGROUND|"+PromptTemplatePolicy.SourceReference+"|text_only",rendered);
    }
    [Fact] public void RepeatedContextExpansionIsRejectedBeforeUnboundedAllocation()
    {var settings=Settings(string.Concat(Enumerable.Repeat("{{reader_background}}",20))) with{ReaderBackground=new string('x',2048)};Assert.Throws<SettingsValidationException>(()=>PromptTemplatePolicy.Validate(settings));Assert.Throws<SettingsValidationException>(()=>SettingsJson.Write(new(){Translation=settings}));}
    [Fact] public void SchemaDescribesActualOptionalTemplateKeysAndLimits()
    {var schema=SettingsJson.Schema()["properties"]!["translation"]!["properties"]!["customTemplates"]!;Assert.Equal(4,schema["maxProperties"]!.GetValue<int>());Assert.Equal(8192,schema["additionalProperties"]!["maxLength"]!.GetValue<int>());Assert.Equal(4,schema["propertyNames"]!["enum"]!.AsArray().Count);}
    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task ActualOldConfigurationLoadsWithoutRewritingAndDefaultsRemainBuiltIn(int schema)
    {
        string root=Path.Combine(Path.GetTempPath(),"kuaiyi-template-legacy-"+Guid.NewGuid().ToString("N"));var store=new SettingsStore(root);await store.LoadAsync();string json=schema==0?"{\"schemaVersion\":0,\"fontSizeDip\":17}":"{\"schemaVersion\":1,\"translation\":{\"preset\":\"Plain\",\"domain\":\"PUBLIC_DOMAIN\"}}";await File.WriteAllTextAsync(store.PrimaryPath,json);
        try{var loaded=await store.LoadAsync();Assert.Equal(schema==0?SettingsLoadState.Migrated:SettingsLoadState.Current,loaded.State);Assert.Empty(loaded.Data.Translation.CustomTemplates);Assert.Equal(json,await File.ReadAllTextAsync(store.PrimaryPath));var saved=await store.SaveAsync(loaded,loaded.Data);Assert.Empty(saved.Data.Translation.CustomTemplates);Assert.Equal(1,saved.Data.SchemaVersion);Assert.Equal(json,await File.ReadAllTextAsync(store.BackupPath));}finally{Directory.Delete(root,true);}
    }
    [Fact] public void ReloadedTemplateMapsAreSemanticallyEqualAndDifferentTextsAreNot()
    {var before=new AppSettings{Translation=Settings("PUBLIC")};var after=SettingsJson.Read(SettingsJson.Write(before),out _);Assert.True(PromptTemplatePolicy.Equivalent(before.Translation,after.Translation));Assert.False(PromptTemplatePolicy.Equivalent(before.Translation,after.Translation with{CustomTemplates=after.Translation.CustomTemplates.SetItem(TranslationPreset.Faithful,"CHANGED")}));}
}
