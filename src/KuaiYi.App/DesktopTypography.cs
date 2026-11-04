using System.Windows.Media;
using KuaiYi.Core.Settings;

namespace KuaiYi.App;

internal static class DesktopTypography
{
    internal static readonly FontFamily Modern=CreateReadingFamily();
    private static FontFamily CreateReadingFamily()
    {
        var family=new FontFamily();
        family.FamilyMaps.Add(new FontFamilyMap{Unicode="0000-024F",Target="Segoe UI Variable Text, Segoe UI",Scale=1});
        family.FamilyMaps.Add(new FontFamilyMap{Target="DengXian, Microsoft YaHei UI",Scale=1});
        return family;
    }
    internal static readonly FontFamily System=new("Microsoft YaHei UI, Segoe UI");
    internal static FontFamily Resolve(OverlayTypeface typeface)=>typeface==OverlayTypeface.System?System:Modern;
}
