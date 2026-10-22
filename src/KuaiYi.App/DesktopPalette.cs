namespace KuaiYi.App;

// Presentation values only. No settings mutation, process, network, source or credential access.
internal sealed record DesktopPalette(string Canvas,string Surface,string Rail,string Input,string Text,string Muted,string Stroke,string InputBorder,string Accent,string AccentText,string AccentSoft)
{
    internal static readonly DesktopPalette Light=new("#F5F7FC","#FFFFFF","#EDF2FA","#FFFFFF","#18253D","#52627B","#D8E0EE","#7B8DA7","#3158D8","#FFFFFF","#E7EDFF");
    internal static readonly DesktopPalette Dark=new("#101827","#172338","#121D2F","#101B2E","#EDF2FC","#B1BED4","#35465F","#697F9F","#9DB9FF","#10203A","#293F67");
}
internal static class SettingsUiMetrics
{
    internal const double RailWidth=184,CardRadius=16,ControlRadius=8,PageInset=24,ControlHeight=36;
}
