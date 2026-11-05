using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KuaiYi.Core.Settings;

namespace KuaiYi.App;

internal sealed partial class SettingsWindow
{
    private readonly ComboBox colorStyle=Choice<OverlayColorStyle>();
    private readonly ComboBox backgroundMotion=Choice<OverlayBackgroundMotion>();
    private readonly ComboBox typeface=Choice<OverlayTypeface>();
    private readonly ComboBox outline=Choice<OverlayOutline>();
    private readonly TextBox customColor=new(){MaxLength=7};
    private readonly AuroraGlassSurface appearancePreview=new(){Height=112,IsHitTestVisible=false};
    private readonly Grid appearancePreviewHost=new(){Height=112,Margin=new Thickness(0,8,0,16),IsHitTestVisible=false};
    private readonly TextBlock appearancePreviewText=new(){Text="让每一种语言，自然靠近。",FontSize=17,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(24),VerticalAlignment=VerticalAlignment.Center};

    private void SetupAppearance(StackPanel page)
    {
        Section(page,"玻璃与色彩","选择自己的浮窗氛围。减少动画时，流动背景会静止。");
        appearancePreviewHost.Children.Add(appearancePreview);appearancePreviewHost.Children.Add(appearancePreviewText);
        page.Children.Add(appearancePreviewHost);
        appearancePreviewHost.SizeChanged+=(_,_)=>RefreshAppearancePreview();
        PreferenceRow(page,"皮肤","只更换配色，保留字号、字体、宽度与动效偏好",colorStyle);
        PreferenceRow(page,"轮廓","圆角方框、柔和云朵、流体或气泡",outline);
        var details=DetailSection(page,"外观细调");
        PreferenceRow(details,"背景动效","静态玻璃或缓慢流动的光晕",backgroundMotion);
        PreferenceRow(details,"自选主色","选择自定义配色时使用，例如 #8B5CF6",customColor);
        PreferenceRow(details,"字体","现代中英搭配，或系统字体",typeface);
        colorStyle.SelectionChanged+=(_,_)=>RefreshAppearancePreview();
        backgroundMotion.SelectionChanged+=(_,_)=>RefreshAppearancePreview();
        outline.SelectionChanged+=(_,_)=>RefreshAppearancePreview();
        typeface.SelectionChanged+=(_,_)=>RefreshAppearancePreview();
        theme.SelectionChanged+=(_,_)=>RefreshAppearancePreview();
        motion.SelectionChanged+=(_,_)=>RefreshAppearancePreview();
        customColor.TextChanged+=(_,_)=>RefreshAppearancePreview();
    }

    private static StackPanel DetailSection(Panel parent,string title)
    {
        var content=new StackPanel{Margin=new Thickness(0,10,0,6)};
        parent.Children.Add(new Expander{Header=title,Content=content,IsExpanded=false,Margin=new Thickness(0,8,0,8)});
        return content;
    }

    private void LoadAppearance()
    {
        var value=draft.Data.Overlay;
        colorStyle.SelectedItem=value.ColorStyle;backgroundMotion.SelectedItem=value.BackgroundMotion;
        typeface.SelectedItem=value.Typeface;outline.SelectedItem=value.Outline;customColor.Text=value.CustomColor;

        RefreshAppearancePreview();
    }

    private OverlaySettings CaptureAppearance(OverlaySettings value)=>value with
    {
        ColorStyle=(OverlayColorStyle)(colorStyle.SelectedItem??OverlayColorStyle.Aurora),
        BackgroundMotion=(OverlayBackgroundMotion)(backgroundMotion.SelectedItem??OverlayBackgroundMotion.Flow),
        Typeface=(OverlayTypeface)(typeface.SelectedItem??OverlayTypeface.Modern),
        Outline=(OverlayOutline)(outline.SelectedItem??OverlayOutline.Glass),
        CustomColor=customColor.Text.Trim()
    };

    private void RefreshAppearancePreview()
    {
        var value=CaptureAppearance(draft.Data.Overlay);
        // Incomplete edits remain draft text; the renderer only receives valid colors.
        if(!System.Text.RegularExpressions.Regex.IsMatch(value.CustomColor,"^#[0-9a-fA-F]{6}$"))value=value with{CustomColor="#8B5CF6"};
        bool dark=DesktopTheme.IsDark((ThemePreference)(theme.SelectedItem??ThemePreference.FollowSystem));
        appearancePreview.Configure(dark,SystemParameters.HighContrast,false,value,
            motion.SelectedItem is MotionPreference.Reduce||!SystemParameters.ClientAreaAnimation);
        appearancePreviewText.FontFamily=DesktopTypography.Resolve(value.Typeface);
        appearancePreviewText.Foreground=SystemParameters.HighContrast?SystemColors.WindowTextBrush:dark?Brushes.White:new SolidColorBrush(Color.FromRgb(23,33,59));
        if(appearancePreviewHost.ActualWidth>=48)
            appearancePreviewHost.Clip=OverlayOutlineGeometry.Create(appearancePreviewHost.ActualWidth,112,18,new Rect(0,0,48,48),value.Outline,1);
    }
}
