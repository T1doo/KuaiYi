namespace KuaiYi.Core;

public readonly record struct SelectionRangeRectangle(double X,double Y,double Width,double Height);
/// <summary>Normalize the managed WPF Empty sentinel; count every original rectangle before filtering.</summary>
public static class SelectionRangeBounds
{
    public static bool TryAppend(IEnumerable<SelectionRangeRectangle>? source,List<double> destination,ref int observed)
    {
        ArgumentNullException.ThrowIfNull(destination);
        if(source==null||observed is <0 or >256||destination.Count>1024||destination.Count%4!=0)return false;
        int count=observed;List<double> staged=[];
        foreach(var rectangle in source)
        {
            if(++count>256)return false;
            if(rectangle.X==double.PositiveInfinity&&rectangle.Y==double.PositiveInfinity&&rectangle.Width==double.NegativeInfinity&&rectangle.Height==double.NegativeInfinity)continue;
            if(!double.IsFinite(rectangle.X)||!double.IsFinite(rectangle.Y)||!double.IsFinite(rectangle.Width)||!double.IsFinite(rectangle.Height)||rectangle.Width<0||rectangle.Height<0)return false;
            if(destination.Count+staged.Count>1020)return false;
            staged.AddRange([rectangle.X,rectangle.Y,rectangle.Width,rectangle.Height]);
        }
        destination.AddRange(staged);observed=count;return true;
    }
}
