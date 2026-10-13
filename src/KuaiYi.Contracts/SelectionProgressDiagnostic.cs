namespace KuaiYi.Contracts;
public sealed record SelectionProgressDiagnostic(string Checkpoint,string TimeoutSource,string BatchSource,string CountState,int? RangeTotal,int? RangeCompleted,string TextState,int? TextStartMs,int? TextEndMs,int? TextDurationMs);
public static class SelectionProgressProjection
{
    private static readonly string[] Checks=["None","FocusAncestors","HitAncestors","BeforeCandidate","BeforeRange","FinalIdentity","AfterRuntime","AfterCandidate","BeforeBfs","BfsExpansion","FinalEmpty","FinishSnapshot","BeforeCopy"];
    private static readonly string[] Sources=["None","Focus","Hit","Bfs"];
    public static SelectionProgressDiagnostic? Project(long?[]? raw)
    {
        if(raw is not {Length:7}||raw[0] is not {} check||check<0||check>=Checks.Length||raw[1] is not {} timeout||timeout<0||timeout>=Sources.Length||raw[2] is not {} batch||batch<0||batch>=Sources.Length||check==0&&timeout!=0)return null;
        int? total=null,completed=null;string countState="Unknown";
        if(raw[3] is >=0 and <=64&&raw[4] is >=0 and <=64&&raw[4]<=raw[3]){total=(int)raw[3]!.Value;completed=(int)raw[4]!.Value;countState="Observed";}
        if(batch==0&&(total!=null||raw[5]!=null||raw[6]!=null))return null;
        string textState="NotRun";int? start=null,end=null,duration=null;
        if(raw[5]!=null||raw[6]!=null){
            if(raw[5]==null)return null;
            if(raw[5]<0||raw[5]>600||raw[6]<0||raw[6]>600)textState="OverLimit";
            else{start=(int)raw[5]!.Value;if(raw[6]==null)textState="Started";else{if(raw[6]<raw[5])return null;end=(int)raw[6]!.Value;duration=end-start;textState="Measured";}}
        }
        return new(Checks[(int)check],Sources[(int)timeout],Sources[(int)batch],countState,total,completed,textState,start,end,duration);
    }
}
