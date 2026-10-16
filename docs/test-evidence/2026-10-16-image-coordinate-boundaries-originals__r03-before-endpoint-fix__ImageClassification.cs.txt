using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using KuaiYi.Core;
namespace KuaiYi.Providers;

public enum ImageKind { TextOnly, DiagramOrMixed, VisualOnly, Uncertain }
public enum ImageReadability { Clear, PartlyUnclear, Unreadable }
public sealed record ImageRegion(string Id,string Position,double X,double Y,double Width,double Height);
/// <summary>Only the bounded, complete, locally validated classifier result can construct this value.</summary>
public sealed record ImageClassification
{
    public const int MaximumCharacters=8192;
    public ImageKind Kind {get;}
    public ImageReadability Readability {get;}
    public bool HasMeaningfulVisualStructure {get;}
    public ImmutableArray<ImageRegion> Regions {get;}
    public string UncertaintyNote {get;}
    private ImageClassification(ImageKind kind,ImageReadability readability,bool visual,ImmutableArray<ImageRegion> regions,string note)
    {Kind=kind;Readability=readability;HasMeaningfulVisualStructure=visual;Regions=regions;UncertaintyNote=note;}
    public static bool TryParse(string? json,out ImageClassification? result)
    {
        result=null;if(json is not {Length:>0 and <=MaximumCharacters})return false;
        try
        {
            using var document=JsonDocument.Parse(json,new(){MaxDepth=6});var root=document.RootElement;
            if(!Fields(root,"kind","readability","hasMeaningfulVisualStructure","regions","uncertaintyNote"))return false;
            ImageKind? kind=String(root,"kind") switch{"text_only"=>ImageKind.TextOnly,"diagram_or_mixed"=>ImageKind.DiagramOrMixed,"visual_only"=>ImageKind.VisualOnly,"uncertain"=>ImageKind.Uncertain,_=>null};
            ImageReadability? readability=String(root,"readability") switch{"clear"=>ImageReadability.Clear,"partly_unclear"=>ImageReadability.PartlyUnclear,"unreadable"=>ImageReadability.Unreadable,_=>null};
            var flag=root.GetProperty("hasMeaningfulVisualStructure");var rows=root.GetProperty("regions");string? note=String(root,"uncertaintyNote");
            if(kind==null||readability==null||flag.ValueKind is not(JsonValueKind.True or JsonValueKind.False)||rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()>12||!Text(note,512,true))return false;
            bool visual=flag.GetBoolean();if(kind==ImageKind.TextOnly&&visual||kind==ImageKind.DiagramOrMixed&&!visual)return false;
            var regions=ImmutableArray.CreateBuilder<ImageRegion>();var ids=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in rows.EnumerateArray())
            {
                if(!Fields(row,"id","position","bbox"))return false;string? id=String(row,"id"),position=String(row,"position");var box=row.GetProperty("bbox");
                if(id is not {Length:>0 and <=32}||id.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c is not('_' or '-'))||!ids.Add(id)||!Text(position,160,false)||box.ValueKind!=JsonValueKind.Array||box.GetArrayLength()!=4)return false;
                double[] values=new double[4];int i=0;foreach(var value in box.EnumerateArray()){if(value.ValueKind!=JsonValueKind.Number||!value.TryGetDouble(out values[i])||!double.IsFinite(values[i]))return false;i++;}
                var (x,y,w,h)=(values[0],values[1],values[2],values[3]);if(x>=1||y>=1||w<=0||h<=0||!UnitInterval.Contains(x,w)||!UnitInterval.Contains(y,h))return false;
                regions.Add(new(id,position!,x,y,w,h));
            }
            result=new(kind.Value,readability.Value,visual,regions.ToImmutable(),note!);return true;
        }
        catch(JsonException){return false;}
        catch(InvalidOperationException){return false;}
    }
    private static string? String(JsonElement item,string name)=>item.GetProperty(name).ValueKind==JsonValueKind.String?item.GetProperty(name).GetString():null;
    private static bool Text(string? value,int maximum,bool empty)=>value!=null&&value.Length<=maximum&&(empty||!string.IsNullOrWhiteSpace(value))&&!value.Any(c=>char.IsControl(c)||char.GetUnicodeCategory(c)==UnicodeCategory.Format);
    private static bool Fields(JsonElement item,params string[] expected)
    {if(item.ValueKind!=JsonValueKind.Object)return false;var names=new HashSet<string>(StringComparer.Ordinal);foreach(var property in item.EnumerateObject())if(!names.Add(property.Name)||!expected.Contains(property.Name,StringComparer.Ordinal))return false;return names.Count==expected.Length;}
}
