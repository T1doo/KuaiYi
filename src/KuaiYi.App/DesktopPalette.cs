namespace KuaiYi.App;

// Presentation values only. No settings mutation, process, network, source or credential access.
internal sealed record DesktopPalette(string Canvas,string Surface,string Rail,string Input,string Text,string Muted,string Stroke,string InputBorder,string Accent,string AccentText,string AccentSoft)
{
    internal static readonly DesktopPalette Light=new("#FFFFFF","#FFFFFF","#F3F5F7","#FFFFFF","#20252B","#626D79","#BFC6CE","#87929E","#0067C0","#FFFFFF","#EFF5FC");
    internal static readonly DesktopPalette Dark=new("#202327","#202327","#2C3035","#202327","#EFF1F3","#BAC2CB","#525961","#76818D","#70B5F5","#102333","#203548");
}
internal static class SettingsUiMetrics
{
    internal const double RailWidth=192,CardRadius=6,ControlRadius=3,PageInset=24,ControlHeight=40;
    internal const double HoverTint=.04,PressedTint=.08;
}
