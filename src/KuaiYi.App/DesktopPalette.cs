namespace KuaiYi.App;

// Presentation values only. No settings mutation, process, network, source or credential access.
internal sealed record DesktopPalette(string Canvas,string Surface,string Rail,string Input,string Text,string Muted,string Stroke,string InputBorder,string Accent,string AccentText,string AccentSoft)
{
    internal static readonly DesktopPalette Light=new("#F5F9FD","#FFFFFF","#EAF3FC","#FFFFFF","#19324A","#526A80","#D7E5F1","#768DA3","#235EA8","#FFFFFF","#E4F0FE");
    internal static readonly DesktopPalette Dark=new("#101827","#172338","#121D2F","#101B2E","#EDF2FC","#B1BED4","#35465F","#697F9F","#9DB9FF","#10203A","#293F67");
}
internal static class SettingsUiMetrics
{
    internal const double RailWidth=192,CardRadius=18,ControlRadius=10,PageInset=24,ControlHeight=40;
}
