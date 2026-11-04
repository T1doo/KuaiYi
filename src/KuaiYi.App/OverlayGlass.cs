using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using KuaiYi.Windows;

namespace KuaiYi.App;

internal sealed partial class ShellWindow
{
    private readonly AuroraGlassSurface glassSurface=new(){IsHitTestVisible=false};
    private WindowBackdropResult glassBackdrop;
    private (nint Handle,bool Enable,bool Dark)? glassConfiguration;
    internal WindowBackdropResult PresentationBackdrop=>glassBackdrop;
    private void RefreshGlassAppearance()
    {
        bool dark=DesktopTheme.IsDark(lifecycle.Settings?.Data.Overlay.Theme??KuaiYi.Core.Settings.ThemePreference.FollowSystem),contrast=SystemParameters.HighContrast;
        bool transparency=false;
        try{transparency=Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",1) is int value&&value!=0;}
        catch(Exception error)when(error is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {System.Diagnostics.Trace.TraceWarning("Glass preference unavailable: "+error.GetType().Name);}
        // Layered WPF windows supply per-pixel alpha for the cloud outline.
        // DWM frame extension would reintroduce a rectangular backing/shadow.
        var configuration=(hwnd,!AllowsTransparency&&!contrast&&transparency,dark);
        if(hwnd!=0&&glassConfiguration!=configuration)
        {
            glassBackdrop=NativeWindowBackdrop.Apply(hwnd,configuration.Item2,dark);glassConfiguration=configuration;
            if(HwndSource.FromHwnd(hwnd)?.CompositionTarget is {} target)target.BackgroundColor=Colors.Transparent;
        }
        Background=Brushes.Transparent;glassSurface.Configure(dark,contrast,glassBackdrop.Accepted&&!contrast,lifecycle.Settings?.Data.Overlay??new(),ReduceMotion);
        var appearance=lifecycle.Settings?.Data.Overlay??new();
        cardBackground.Background=Brushes.Transparent;cardBackground.BorderBrush=contrast?SystemColors.WindowTextBrush:AuroraGlassSurface.Edge(dark,appearance.ColorStyle,appearance.CustomColor);
        toolbarSurface.Background=contrast?SystemColors.WindowBrush:new SolidColorBrush(dark?Color.FromArgb(20,255,255,255):Color.FromArgb(60,255,255,255));
        toolbarDivider.Background=contrast?SystemColors.WindowTextBrush:new SolidColorBrush(dark?Color.FromArgb(35,170,201,255):Color.FromArgb(65,147,155,224));
    }
}
