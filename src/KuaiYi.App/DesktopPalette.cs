namespace KuaiYi.App;

// Presentation values only. No settings mutation, process, network, source or credential access.
internal sealed record DesktopPalette(string Canvas,string Surface,string Rail,string Input,string Text,string Muted,string Stroke,string InputBorder,string Accent,string AccentText,string AccentSoft)
{
    internal static readonly DesktopPalette Light=new("#EEF1FF","#F1F5FF","#E7ECFC","#FAFBFF","#17213B","#4B5872","#B3C3EA","#7486A5","#5246C9","#FFFFFF","#E3E7FB");
    internal static readonly DesktopPalette Dark=new("#080F24","#111B37","#172440","#0C1630","#F4F7FF","#C2CCE1","#3E5F8E","#809ABF","#75E8F8","#102337","#18334F");
}
internal static class SettingsUiMetrics
{
    internal const double RailWidth=192,CardRadius=12,ControlRadius=8,PageInset=24,ControlHeight=40;
    internal const double HoverTint=.055,PressedTint=.10;
}
