namespace KuaiYi.App;

// DIP geometry shared by the real WPF panels and platform-independent boundary tests.
// No control values, selection, focus or settings transactions belong here.
internal readonly record struct PreferenceLayout(double Width,double LabelWidth,double InputWidth,double Gap,bool Stacked,bool Labels,bool Input)
{
    internal double Height(double labelHeight,double inputHeight)=>Stacked
        ?(Labels?labelHeight:0)+(Input?inputHeight:0)+(Labels&&Input?Gap:0)
        :Math.Max(Labels?labelHeight:0,Input?inputHeight:0);
}
internal readonly record struct SettingsChromeLayout(bool Compact,double Width,double Height,double RailWidth,double NavigationHeight,double FooterHeight)
{
    internal double MainWidth=>Compact?Width:Math.Max(0,Width-RailWidth);
    internal double MainHeight=>Math.Max(0,Height-FooterHeight-(Compact?NavigationHeight:0));
}
internal static class SettingsLayoutPlan
{
    internal static double Scale(double fontSize)=>double.IsFinite(fontSize)&&fontSize>13?fontSize/13:1;
    internal static double Finite(double value,double fallback)=>double.IsFinite(value)?Math.Max(0,value):fallback;
    // Recovery's hard 200 + margin 16 + footer padding 32, plus the actual system scrollbar.
    internal static double MinimumWidth(double fontSize,double scrollBarWidth)=>Math.Max(280*Scale(fontSize),248+Finite(scrollBarWidth,17));
    internal static PreferenceLayout Preference(double availableWidth,double fontSize,double inputDesiredWidth,bool labels=true,bool input=true)
    {
        double scale=Scale(fontSize),width=Finite(availableWidth,640*scale);
        double inputWidth=input?Math.Min(width,Math.Max(180*scale,Finite(inputDesiredWidth,180*scale))):0;
        bool stacked=labels&&input&&width<160*scale+20*scale+inputWidth;
        double gap=labels&&input?(stacked?8:20)*scale:0;
        return new(width,labels?(stacked||!input?width:Math.Max(0,width-inputWidth-gap)):0,
            input?(stacked||!labels?width:inputWidth):0,gap,stacked,labels,input);
    }
    internal static SettingsChromeLayout Chrome(double availableWidth,double availableHeight,double fontSize,double footerDesiredHeight)
    {
        double scale=Scale(fontSize),width=Finite(availableWidth,940),height=Finite(availableHeight,740);
        bool compact=width<616*scale;
        double footer=Math.Min(Finite(footerDesiredHeight,0),height*.35);
        double navigation=compact?Math.Min(76*scale,height*.24):0;
        return new(compact,width,height,192*scale,navigation,footer);
    }
}
