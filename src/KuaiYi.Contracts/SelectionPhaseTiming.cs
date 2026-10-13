using System.Collections.Immutable;
namespace KuaiYi.Contracts;
public sealed record SelectionPhaseTiming(string Stage,string State,int? StartMs,int? EndMs,int? DurationMs);
public static class SelectionPhaseTimingProjection
{
    private static readonly string[] Stages=["Preflight","ComInitialize","AutomationCreate","Root","Focus","Hit","Password","Walker","FocusAncestors"];
    public static ImmutableArray<SelectionPhaseTiming>? Project(long?[]? raw)
    {
        if(raw is not {Length:18})return null;
        var output=ImmutableArray.CreateBuilder<SelectionPhaseTiming>(9);long previous=0;bool ended=false,over=false;
        for(int i=0;i<9;i++)
        {
            var start=raw[2*i];var end=raw[2*i+1];
            if(start==null&&end==null){ended=true;output.Add(new(Stages[i],"NotRun",null,null,null));continue;}
            if(start==null||end==null)return null;
            if(ended)return null;
            if(over||start<0||start>600||end<0||end>600){over=true;output.Add(new(Stages[i],"OverLimit",null,null,null));continue;}
            if(end<start||start<previous)return null;
            output.Add(new(Stages[i],"Measured",(int)start.Value,(int)end.Value,(int)(end.Value-start.Value)));previous=end.Value;
        }
        return output.MoveToImmutable();
    }
}
