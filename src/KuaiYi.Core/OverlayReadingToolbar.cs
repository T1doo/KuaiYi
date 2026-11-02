namespace KuaiYi.Core;

public sealed record ReadingToolSlot(string Command,double OffsetDip,double WidthDip);
/// <summary>Frequency-based control edge. Commands keep identity when their labels change.</summary>
public static class OverlayReadingToolbar
{
    public static IReadOnlyList<ReadingToolSlot> Plan(double availableDip,bool reading,bool generating,bool touch=false)
    {
        if(!double.IsFinite(availableDip)||availableDip<0)throw new ArgumentOutOfRangeException(nameof(availableDip));
        double hit=touch?44:28;
        var result=new List<ReadingToolSlot>();
        if(!reading)
        {
            Add("回看",56);Add("设置",hit);Add("更多",hit);return result;
        }
        // Reserve the two direct task actions and a discoverable overflow before optional controls.
        double primary=Math.Max(hit,56),collapse=Math.Max(hit,40),more=hit;
        double required=primary+collapse+more;
        if(availableDip<required)
        {Add(generating?"停止":"重新生成",hit);Add("收起",hit);Add("更多",hit);return result;}
        bool style=availableDip>=required+64;
        bool extras=availableDip>=required+64+44+hit;
        if(style)Add("风格",64);
        Add(generating?"停止":"重新生成",primary);
        if(extras)Add("复制",44);
        Add("收起",collapse);
        if(extras)Add("设置",hit);
        Add("更多",more);
        return result;
        void Add(string command,double width)
        {
            double offset=result.Count==0?0:result[^1].OffsetDip+result[^1].WidthDip;
            if(offset+width<=availableDip)result.Add(new(command,offset,width));
        }
    }
}
