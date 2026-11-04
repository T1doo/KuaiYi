namespace KuaiYi.App;

// Presentation values only. No settings mutation, process, network, source or credential access.
internal sealed record DesktopPalette(string Canvas,string Surface,string Rail,string Input,string Text,string Muted,string Stroke,string InputBorder,string Accent,string AccentText,string AccentSoft)
{
    internal static readonly DesktopPalette Light=new("#F8FAFC","#FFFFFF","#F3F6FA","#FFFFFF","#242C37","#626F80","#CBD4E0","#87929E","#1F5FAD","#FFFFFF","#EAF1FB");
    internal static readonly DesktopPalette Dark=new("#1B2027","#22272F","#2A303A","#1D222A","#EEF2F7","#B5BFCE","#4A5668","#7D899B","#8AB8F3","#13243B","#2B3849");
}
internal static class SettingsUiMetrics
{
    internal const double RailWidth=192,CardRadius=6,ControlRadius=3,PageInset=24,ControlHeight=40;
    internal const double HoverTint=.055,PressedTint=.10;
}
