namespace KuaiYi.Core;
public sealed record OverlayToolSlot(int Index,double OffsetDip,double WidthDip);
public sealed record OverlayToolbarPlan(IReadOnlyList<OverlayToolSlot> Slots,IReadOnlyList<int> Overflow,double? OverflowOffsetDip);
/// <summary>Reserve the settings action and an overflow target before placing optional actions.</summary>
public static class OverlayToolbarLayout
{
    public static OverlayToolbarPlan Plan(double availableDip,int count,int settingsIndex)
    {
        if(!double.IsFinite(availableDip)||availableDip<0||count<1||settingsIndex<0||settingsIndex>=count)throw new ArgumentOutOfRangeException();
        var slots=new List<OverlayToolSlot>();var overflow=new List<int>();double offset=0;
        if(availableDip>=28){double width=availableDip>=68?40:28;slots.Add(new(settingsIndex,0,width));offset=width;}
        else overflow.Add(settingsIndex);
        var optional=Enumerable.Range(0,count).Where(i=>i!=settingsIndex).ToArray();
        bool fits=availableDip-offset>=optional.Length*40;
        double capacity=availableDip-offset-(fits?0:28);
        foreach(int i in optional){if(capacity>=40){slots.Add(new(i,offset,40));offset+=40;capacity-=40;}else overflow.Add(i);}
        // On an extremely narrow card the primary button context menu is the overflow route.
        double? more=overflow.Count>0&&availableDip-offset>=28?offset:null;
        return new(slots,overflow,more);
    }
}
