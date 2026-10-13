using KuaiYi.Core;
using System.Windows;
namespace KuaiYi.CompatibilityProbe;
internal static class SelectionGeometryContracts
{
    internal static int Run()
    {
        if(!OperatingSystem.IsWindows()||Environment.GetEnvironmentVariable("CI")!="true")return 2;
        var empty=Rect.Empty;List<double> destination=[];int observed=0;
        var actual=new SelectionRangeRectangle(empty.X,empty.Y,empty.Width,empty.Height);
        if(!empty.IsEmpty||!SelectionRangeBounds.TryAppend([actual],destination,ref observed)||observed!=1||destination.Count!=0)return 3;
        var finite=new Rect(1,2,3,4);
        if(!SelectionRangeBounds.TryAppend([actual,new(finite.X,finite.Y,finite.Width,finite.Height)],destination,ref observed)||observed!=3||!destination.SequenceEqual(new double[]{1,2,3,4}))return 3;
        if(SelectionRangeBounds.TryAppend([new(double.NaN,0,1,1)],destination,ref observed)||observed!=3||destination.Count!=4)return 3;
        destination.Clear();observed=0;
        if(SelectionRangeBounds.TryAppend(Enumerable.Repeat(actual,257),destination,ref observed)||observed!=0||destination.Count!=0)return 3;
        Console.WriteLine("WPF_SELECTION_GEOMETRY_CONTRACTS passed=4 failed=0");return 0;
    }
}
