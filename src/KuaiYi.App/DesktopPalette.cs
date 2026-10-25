namespace KuaiYi.App;

// Presentation values only. No settings mutation, process, network, source or credential access.
internal sealed record DesktopPalette(string Canvas,string Surface,string Rail,string Input,string Text,string Muted,string Stroke,string InputBorder,string Accent,string AccentText,string AccentSoft)
{
    internal static readonly DesktopPalette Light=new("#FAF7F2","#FFFEFB","#F4EDE4","#FFFFFF","#322A24","#705D4E","#E1D4C5","#A48770","#A4411A","#FFFFFF","#FBE8D8");
    internal static readonly DesktopPalette Dark=new("#1D1916","#28211C","#221C18","#201A16","#FAF0E5","#CBB9A8","#564638","#A48A73","#F5B17C","#2B1A0E","#4A2E1E");
}
internal static class SettingsUiMetrics
{
    internal const double RailWidth=192,CardRadius=18,ControlRadius=10,PageInset=24,ControlHeight=40;
    internal const double HoverTint=.04,PressedTint=.08;
}
