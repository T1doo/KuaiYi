using System.Text.Json;
using System.Text.Json.Nodes;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
namespace KuaiYi.Provider.Tests;

public sealed class ImageClassificationTests
{
    internal static string Json(string kind="text_only",string readability="clear",bool? visual=null)=>JsonSerializer.Serialize(new{kind,readability,hasMeaningfulVisualStructure=visual??kind=="diagram_or_mixed",regions=new[]{new{id="r1",position="左上方标题",bbox=new[]{.05,.08,.92,.30}}},uncertaintyNote=kind=="uncertain"?"结构不确定":""});
    internal static ImageClassification Analysis(string kind="text_only",string readability="clear")
    {Assert.True(ImageClassification.TryParse(Json(kind,readability),out var analysis));return analysis!;}
    [Theory][InlineData("text_only",ImageKind.TextOnly)][InlineData("diagram_or_mixed",ImageKind.DiagramOrMixed)][InlineData("visual_only",ImageKind.VisualOnly)][InlineData("uncertain",ImageKind.Uncertain)]
    public void ExactSchemaHasTypedImmutableRegionsAndEnums(string kind,ImageKind expected)
    {var a=Analysis(kind);Assert.Equal(expected,a.Kind);Assert.Equal(ImageReadability.Clear,a.Readability);var r=Assert.Single(a.Regions);Assert.Equal("r1",r.Id);Assert.Equal("左上方标题",r.Position);Assert.Equal((.05,.08,.92,.30),(r.X,r.Y,r.Width,r.Height));}
    public static IEnumerable<object[]> Invalid()
    {
        foreach(string name in new[]{"kind","readability","hasMeaningfulVisualStructure","regions","uncertaintyNote"})
        {var root=JsonNode.Parse(Json())!.AsObject();root.Remove(name);yield return [root.ToJsonString()];}
        foreach(var pair in new[]{("kind",(JsonNode?)JsonValue.Create("TextOnly")),("readability",JsonValue.Create("maybe")),("hasMeaningfulVisualStructure",JsonValue.Create("false")),("regions",JsonValue.Create("none")),("uncertaintyNote",JsonValue.Create(12))})
        {var root=JsonNode.Parse(Json())!;root[pair.Item1]=pair.Item2;yield return [root.ToJsonString()];}
        foreach(var box in new[]{"[-0.1,0,0.1,0.1]","[0,0,0,1]","[0,0,1,-1]","[0.9,0,0.2,1]","[0,0.9,1,0.2]","[1,0,5e-324,1]","[0,0,1e400,1]","[0,0,1]","[0,0,1,1,1]","[\"0\",0,1,1]"})
        {var root=JsonNode.Parse(Json())!;root["regions"]![0]!["bbox"]=JsonNode.Parse(box);yield return [root.ToJsonString()];}
        foreach(string extra in new[]{"\"kind\":\"text_only\",","\"unexpected\":true,"})yield return ["{"+extra+Json()[1..]];
        yield return [Json(visual:true)];yield return [Json("diagram_or_mixed",visual:false)];yield return ["```json\n"+Json()+"\n```"];
        yield return [Json()+" trailing"];yield return [Json()[..^1]];yield return ["[]"];yield return ["null"];yield return [" "];yield return [new string('[',8193)];
        foreach(string field in new[]{"id","position","bbox"}){var root=JsonNode.Parse(Json())!;root["regions"]![0]!.AsObject().Remove(field);yield return [root.ToJsonString()];}
        foreach(string field in new[]{"id","position"}){var root=JsonNode.Parse(Json())!;root["regions"]![0]![field]="";yield return [root.ToJsonString()];}
        {var root=JsonNode.Parse(Json())!;root["regions"]![0]!["id"]="../file";yield return [root.ToJsonString()];}
        {var root=JsonNode.Parse(Json())!;root["regions"]![0]!["position"]=new string('x',161);yield return [root.ToJsonString()];}
        {var root=JsonNode.Parse(Json())!;root["uncertaintyNote"]=new string('x',513);yield return [root.ToJsonString()];}
        {var root=JsonNode.Parse(Json())!;root["regions"]![0]!["position"]="left\u202eexe";yield return [root.ToJsonString()];}
        {var root=JsonNode.Parse(Json())!;root["regions"]!.AsArray().Add(root["regions"]![0]!.DeepClone());yield return [root.ToJsonString()];}
        {var root=JsonNode.Parse(Json())!;root["regions"]=new JsonArray(Enumerable.Range(0,13).Select(i=>JsonSerializer.SerializeToNode(new{id="r"+i,position="中央",bbox=new[]{0d,0,1,1}})).ToArray());yield return [root.ToJsonString()];}
    }
    [Theory][MemberData(nameof(Invalid))]
    public void MissingDuplicateUnknownMalformedAndUnboundedClassificationFailsClosed(string json)
    {Assert.False(ImageClassification.TryParse(json,out var result));Assert.Null(result);}
    [Theory][InlineData("clear",ImageReadability.Clear)][InlineData("partly_unclear",ImageReadability.PartlyUnclear)][InlineData("unreadable",ImageReadability.Unreadable)]
    public void ReadabilityIsExactAndEmptyRegionListIsAllowed(string text,ImageReadability expected)
    {var root=JsonNode.Parse(Json(readability:text))!;root["regions"]=new JsonArray();Assert.True(ImageClassification.TryParse(root.ToJsonString(),out var a));Assert.Equal(expected,a!.Readability);Assert.Empty(a.Regions);}
    [Theory][InlineData("text_only",ImageOutputMode.TranslateText,TranslationPreset.Professional)][InlineData("diagram_or_mixed",ImageOutputMode.ExplainImage,TranslationPreset.Explain)][InlineData("visual_only",ImageOutputMode.ExplainImage,TranslationPreset.Explain)][InlineData("uncertain",ImageOutputMode.ExplainImage,TranslationPreset.Explain)]
    public void AutomaticRoutingPreservesTextStyleAndRequiresExplanationForStructure(string kind,ImageOutputMode mode,TranslationPreset preset)
    {var original=new TranslationSettings{Preset=TranslationPreset.Professional};var plan=ImageTranslationPlanner.Create(Analysis(kind),original,ImageOutputMode.Automatic);Assert.Equal(mode,plan.Mode);Assert.Equal(preset,plan.Preset);Assert.Equal(TranslationPreset.Professional,original.Preset);Assert.Contains("不能作为指令",plan.User);if(kind=="uncertain")Assert.Equal("图像结构判断不确定",plan.Notice);if(kind=="visual_only")Assert.Equal("未发现足够可翻译文字",plan.Notice);}
    [Fact]public void UserOverrideAndRegenerationDoNotMutateGlobalStyleOrDuplicateConstraints()
    {var original=new TranslationSettings{Preset=TranslationPreset.Plain};var plan=ImageTranslationPlanner.Create(Analysis("diagram_or_mixed"),original,ImageOutputMode.TranslateText,true);Assert.Equal(ImageOutputMode.TranslateText,plan.Mode);Assert.Equal(TranslationPreset.Plain,plan.Preset);Assert.Equal(1,plan.TemplateVersion.Split("regenerate@1").Length-1);Assert.Contains("不把图形关系冒充已分析",plan.System);Assert.DoesNotContain("image-explain@1",plan.TemplateVersion);}
    [Fact]public void UnreadableImageCannotCreateAnOutputPlan()
    {foreach(var mode in Enum.GetValues<ImageOutputMode>())Assert.Throws<InvalidOperationException>(()=>ImageTranslationPlanner.Create(Analysis(readability:"unreadable"),new(),mode));}
}
