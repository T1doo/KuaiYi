using System.Globalization;
using KuaiYi.App;
using Xunit;
namespace KuaiYi.Provider.Tests;
public sealed class DesktopPaletteTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void PresentationPaletteKeepsReadableTextSelectionActionsAndInputEdges(bool dark)
    {
        var p=dark?DesktopPalette.Dark:DesktopPalette.Light;
        foreach(var surface in new[]{p.Canvas,p.Surface,p.Rail,p.Input})
        {Assert.True(Contrast(p.Text,surface)>=4.5);Assert.True(Contrast(p.Muted,surface)>=4.5);}
        Assert.True(Contrast(p.AccentText,p.Accent)>=4.5);
        Assert.True(Contrast(p.Accent,p.AccentSoft)>=4.5);
        Assert.True(Contrast(p.InputBorder,p.Input)>=3);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public void InteractiveTintKeepsPrimarySecondaryAndToolbarTextReadable(bool dark)
    {
        var p=dark?DesktopPalette.Dark:DesktopPalette.Light;
        foreach(var tint in new[]{SettingsUiMetrics.HoverTint,SettingsUiMetrics.PressedTint})
        foreach(var pair in new[]{(p.AccentText,p.Accent),(p.Text,p.Surface),(p.Accent,p.AccentSoft)})
            Assert.True(Contrast(pair.Item1,Blend(pair.Item1,pair.Item2,tint))>=4.5);
    }
    private static string Blend(string foreground,string background,double opacity)
    {
        var fg=uint.Parse(foreground.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        var bg=uint.Parse(background.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        uint channel(int shift)=>(uint)Math.Round(((fg>>shift)&255)*opacity+((bg>>shift)&255)*(1-opacity));
        return $"#{((channel(16)<<16)|(channel(8)<<8)|channel(0)):X6}";
    }
    private static double Contrast(string first,string second)
    {var a=Luminance(first);var b=Luminance(second);return(Math.Max(a,b)+.05)/(Math.Min(a,b)+.05);}
    private static double Luminance(string value)
    {
        Assert.Matches("^#[0-9A-F]{6}$",value);var color=uint.Parse(value.AsSpan(1),NumberStyles.HexNumber,CultureInfo.InvariantCulture);
        static double Channel(uint value){double channel=value/255d;return channel<=.04045?channel/12.92:Math.Pow((channel+.055)/1.055,2.4);}
        return .2126*Channel(color>>16)+.7152*Channel((color>>8)&255)+.0722*Channel(color&255);
    }
}
