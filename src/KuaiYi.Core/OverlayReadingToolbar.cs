namespace KuaiYi.Core;

public sealed record ReadingToolSlot(string Command,double OffsetDip,double WidthDip,int Row=0);
public sealed record ReadingToolbarLayout(IReadOnlyList<ReadingToolSlot> Slots,double ExtraHeightDip);
/// <summary>Frequency-based control edge. Commands keep identity when their labels change.</summary>
public static class OverlayReadingToolbar
{
    /// <summary>Reader-first rail: secondary commands remain in the existing overflow menu.</summary>
    public static ReadingToolbarLayout Focused(double sideWidthDip,double cardWidthDip,bool reading,bool generating,bool touch,double textScale)
    {
        if(!double.IsFinite(sideWidthDip)||sideWidthDip<0||!double.IsFinite(cardWidthDip)||cardWidthDip<48||!double.IsFinite(textScale)||textScale<1||textScale>2.25)throw new ArgumentOutOfRangeException();
        var slots=new List<ReadingToolSlot>();double hit=touch?44:32,gap=8,offset=8;
        double primary=Math.Max(hit,36*textScale+20),style=Math.Max(hit,24*textScale+28);
        string action=reading?(generating?"停止":"复制"):"回看";
        double required=primary+hit*(reading?2:1)+gap*(reading?4:3);
        if(required<=sideWidthDip)
        {
            if(reading&&required+style+gap<=sideWidthDip){slots.Add(new("风格",offset,style));offset+=style+gap;}
            slots.Add(new(action,offset,primary));offset+=primary+gap;
            if(reading){slots.Add(new("收起",offset,hit));offset+=hit+gap;}
            slots.Add(new("更多",offset,hit));return new(slots,0);
        }
        // Preserve immediate stop/collapse even when the anchor leaves little side space.
        int row=1;double usable=cardWidthDip-16;
        void Add(string command,double requested)
        {
            double width=Math.Min(requested,usable);
            if(offset>8&&offset+width>cardWidthDip-8){row++;offset=8;}
            slots.Add(new(command,offset,width,row));offset+=width+gap;
        }
        if(generating)Add("停止",primary);
        if(reading)Add("收起",hit);
        Add("更多",hit);
        return new(slots,row*48);
    }
    public static ReadingToolbarLayout Responsive(double sideWidthDip,double cardWidthDip,bool reading,bool generating,bool touch,double textScale)
    {
        if(!double.IsFinite(cardWidthDip)||cardWidthDip<48||!double.IsFinite(textScale)||textScale<1||textScale>2.25)throw new ArgumentOutOfRangeException();
        if(textScale==1)return new(Plan(sideWidthDip,reading,generating,touch),0);
        var slots=new List<ReadingToolSlot>();double hit=touch?44:32;
        if(!reading)
        {
            double width=Math.Max(hit,24*textScale+12),position=0;
            foreach(var command in new[]{"回看","设置","更多"})
            {double current=command=="回看"?width:hit;if(position+current<=sideWidthDip){slots.Add(new(command,position,current));position+=current;}}
            return new(slots,0);
        }
        double style=Math.Max(64,24*textScale+24),primary=Math.Max(56,48*textScale+8),collapse=Math.Max(hit,Math.Max(40,24*textScale+8)),more=Math.Max(32,hit),copy=Math.Max(44,36*textScale+8);
        if(style+primary+collapse+more<=sideWidthDip)
        {
            double position=0,remaining=sideWidthDip-style-primary-collapse-more;
            void One(string command,double width){slots.Add(new(command,position,width));position+=width;}
            One("风格",style);One(generating?"停止":"重新生成",primary);
            if(remaining>=copy){One("复制",copy);remaining-=copy;}
            One("收起",collapse);if(remaining>=hit)One("设置",hit);One("更多",more);
            return new(slots,0);
        }
        // A second row uses the full approved card width, including balanced growth.
        // The 48 DIP anchor row stays physically unchanged; no text or hit region is shrunk.
        if(style<=sideWidthDip)slots.Add(new("风格",0,style));
        double rowWidth=Math.Max(0,cardWidthDip-20);
        double offset=10;int row=1;
        void Add(string command,double width)
        {if(offset>10&&offset+width>cardWidthDip-10){row++;offset=10;}slots.Add(new(command,offset,Math.Min(width,rowWidth),row));offset+=width;}
        Add(generating?"停止":"重新生成",primary);
        if(primary+copy+collapse+more<=rowWidth)Add("复制",copy);
        Add("收起",collapse);Add("更多",more);
        return new(slots,row*48);
    }
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
