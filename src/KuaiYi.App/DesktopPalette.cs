namespace KuaiYi.App;

// Presentation values only. No settings mutation, process, network, source or credential access.
internal sealed record DesktopPalette(string Canvas,string Surface,string Rail,string Input,string Text,string Muted,string Stroke,string InputBorder,string Accent,string AccentText,string AccentSoft)
{
    internal static readonly DesktopPalette Light=new("#F9F5EF","#FFFCF8","#F2E9DD","#FFFFFF","#302A25","#6C5B4B","#D9CBBB","#9C7F67","#A6471B","#FFFFFF","#FBE8D8");
    internal static readonly DesktopPalette Dark=new("#1F1A17","#29221D","#241D19","#211C18","#F7EEE5","#C7B7A7","#544335","#9B806B","#F4B27D","#2C1B10","#4C3020");
}
internal static class SettingsUiMetrics
{
    internal const double RailWidth=192,CardRadius=18,ControlRadius=10,PageInset=24,ControlHeight=40;
}
