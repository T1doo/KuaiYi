using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using KuaiYi.Core.Settings;
using KuaiYi.Core;
namespace KuaiYi.App;

// Window-local resources. No global implicit styles or changes to business control instances.
internal static class DesktopTheme
{
    internal const string Canvas="KuaiYi.Canvas",Surface="KuaiYi.Surface",Rail="KuaiYi.Rail",Input="KuaiYi.Input",Text="KuaiYi.Text",Muted="KuaiYi.Muted",Stroke="KuaiYi.Stroke",InputBorder="KuaiYi.InputBorder",Accent="KuaiYi.Accent",AccentText="KuaiYi.AccentText",AccentSoft="KuaiYi.AccentSoft",SelectionText="KuaiYi.SelectionText";
    internal const string PrimaryButton="KuaiYi.PrimaryButton",ToolButton="KuaiYi.ToolButton",AnchorButton="KuaiYi.AnchorButton",NavigationItem="KuaiYi.NavigationItem";
    private const string Applied="KuaiYi.AppliedPalette",Installed="KuaiYi.ThemeInstalled";
    internal static DataTemplate ChoiceTemplate()
    {var template=new DataTemplate();var text=new FrameworkElementFactory(typeof(TextBlock));text.SetBinding(TextBlock.TextProperty,new Binding{Converter=new ChoiceLabel()});template.VisualTree=text;return template;}
    private sealed class ChoiceLabel:IValueConverter
    {
        public object Convert(object value,Type targetType,object parameter,CultureInfo culture)=>value switch
        {
            ThemePreference.FollowSystem or MotionPreference.FollowSystem=>"跟随系统",
            ThemePreference.Light=>"浅色",ThemePreference.Dark=>"深色",
            MotionPreference.Reduce=>"减少动画",MotionPreference.Full=>"完整动画",
            TriggerMode.Selection=>"自动划词",TriggerMode.Drop=>"拖放输入",
            TranslationPreset.Faithful=>"忠实翻译",TranslationPreset.Plain=>"通俗翻译",TranslationPreset.Professional=>"专业翻译",TranslationPreset.Explain=>"解释模式",
            ApiAddressMode.BasePrefix=>"基础地址",ApiAddressMode.ExactEndpoint=>"完整请求端点",
            ApiAuthentication.None=>"无需认证",ApiAuthentication.Bearer=>"Bearer Token",ApiAuthentication.ApiKey=>"API Key",ApiAuthentication.CustomHeader=>"自定义认证头",
            _=>value?.ToString()??""
        };
        public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture)=>Binding.DoNothing;
    }
    internal static Brush Brush(FrameworkElement element,string key)=>(Brush)element.FindResource(key);
    internal static void Bind(FrameworkElement element,DependencyProperty property,string key)=>element.SetResourceReference(property,key);
    internal static bool IsDark(ThemePreference preference)
    {
        if(preference!=ThemePreference.FollowSystem)return preference==ThemePreference.Dark;
        try{return Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1) is int value&&value==0;}
        catch(Exception error)when(error is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException){System.Diagnostics.Trace.TraceWarning("KuaiYi theme preference unavailable: "+error.GetType().Name);return false;}
    }
    internal static void Apply(Window window,ThemePreference preference)
    {
        bool dark=IsDark(preference),contrast=SystemParameters.HighContrast;string identity=contrast?"contrast":dark?"dark":"light";
        if(Equals(window.Resources[Applied],identity)&&!contrast)return;
        var p=dark?DesktopPalette.Dark:DesktopPalette.Light;
        Set(Canvas,contrast?SystemColors.WindowBrush:Color(p.Canvas));Set(Surface,contrast?SystemColors.WindowBrush:Color(p.Surface));Set(Rail,contrast?SystemColors.WindowBrush:Color(p.Rail));Set(Input,contrast?SystemColors.WindowBrush:Color(p.Input));Set(Text,contrast?SystemColors.WindowTextBrush:Color(p.Text));Set(Muted,contrast?SystemColors.WindowTextBrush:Color(p.Muted));Set(Stroke,contrast?SystemColors.WindowTextBrush:Color(p.Stroke));Set(InputBorder,contrast?SystemColors.WindowTextBrush:Color(p.InputBorder));Set(Accent,contrast?SystemColors.HighlightBrush:Color(p.Accent));Set(AccentText,contrast?SystemColors.HighlightTextBrush:Color(p.AccentText));Set(AccentSoft,contrast?SystemColors.HighlightBrush:Color(p.AccentSoft));Set(SelectionText,contrast?SystemColors.HighlightTextBrush:Color(p.Accent));window.Resources[Applied]=identity;
        void Set(string key,Brush brush)=>window.Resources[key]=brush;
    }
    internal static void Install(Window window,Func<ThemePreference> preference,bool forms,Action? changedAppearance=null)
    {
        Apply(window,preference());if(window.Resources.Contains(Installed))return;window.Resources[Installed]=true;
        window.UseLayoutRounding=true;window.SnapsToDevicePixels=true;
        if(forms)
        {
            window.FontFamily=new FontFamily("Segoe UI");window.FontSize=Math.Max(13,SystemFonts.MessageFontSize);
            Bind(window,Control.BackgroundProperty,Canvas);Bind(window,Control.ForegroundProperty,Text);
            window.Resources[typeof(Button)]=ButtonStyle(8,36);
            var primary=ButtonStyle(8,36);primary.Setters.Add(Value(Control.BackgroundProperty,Accent));primary.Setters.Add(Value(Control.ForegroundProperty,AccentText));primary.Setters.Add(Value(Control.BorderBrushProperty,Accent));primary.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.SemiBold));window.Resources[PrimaryButton]=primary;
            foreach(var type in new[]{typeof(TextBox),typeof(PasswordBox),typeof(ComboBox)})
            {
                var field=new Style(type);field.Setters.Add(Value(Control.BackgroundProperty,Input));field.Setters.Add(Value(Control.ForegroundProperty,Text));field.Setters.Add(Value(Control.BorderBrushProperty,InputBorder));field.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));field.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,7,10,7)));field.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,SettingsUiMetrics.ControlHeight));
                var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(Value(Control.ForegroundProperty,Muted));disabled.Setters.Add(Value(Control.BackgroundProperty,Canvas));field.Triggers.Add(disabled);window.Resources[type]=field;
            }
            var check=new Style(typeof(CheckBox));check.Setters.Add(Value(Control.ForegroundProperty,Text));check.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(6,3,0,3)));check.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,5,0,5)));check.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,28d));window.Resources[typeof(CheckBox)]=check;
            var comboItem=new Style(typeof(ComboBoxItem));comboItem.Setters.Add(Value(Control.BackgroundProperty,Input));comboItem.Setters.Add(Value(Control.ForegroundProperty,Text));comboItem.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(10,7,10,7)));var highlight=new Trigger{Property=ComboBoxItem.IsHighlightedProperty,Value=true};highlight.Setters.Add(Value(Control.BackgroundProperty,AccentSoft));highlight.Setters.Add(Value(Control.ForegroundProperty,SelectionText));comboItem.Triggers.Add(highlight);window.Resources[typeof(ComboBoxItem)]=comboItem;
            window.Resources[NavigationItem]=NavigationStyle();
        }
        var tool=ButtonStyle(7,28);tool.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(3)));tool.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(0)));
        foreach(var icon in Icons)
        {var trigger=new Trigger{Property=FrameworkElement.TagProperty,Value=icon.Key};trigger.Setters.Add(new Setter(Control.TemplateProperty,ButtonTemplate(7,icon.Value)));tool.Triggers.Add(trigger);}
        window.Resources[ToolButton]=tool;window.Resources[AnchorButton]=ButtonStyle(24,48);
        bool closed=false;
        PropertyChangedEventHandler changed=(_,_)=>
        {
            if(closed||window.Dispatcher.HasShutdownStarted)return;
            window.Dispatcher.BeginInvoke(()=>{if(!closed){Apply(window,preference());changedAppearance?.Invoke();}});
        };
        SystemParameters.StaticPropertyChanged+=changed;
        window.Closed+=(_,_)=>{closed=true;SystemParameters.StaticPropertyChanged-=changed;};
    }
    private static Brush Color(string hex){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));brush.Freeze();return brush;}
    private static Setter Value(DependencyProperty property,string key)=>new(property,new DynamicResourceExtension(key));
    private static Style ButtonStyle(double radius,double height)
    {
        var style=new Style(typeof(Button));style.Setters.Add(Value(Control.BackgroundProperty,Surface));style.Setters.Add(Value(Control.ForegroundProperty,Text));style.Setters.Add(Value(Control.BorderBrushProperty,Stroke));style.Setters.Add(new Setter(Control.BorderThicknessProperty,new Thickness(1)));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(14,7,14,7)));style.Setters.Add(new Setter(FrameworkElement.MinHeightProperty,height));style.Setters.Add(new Setter(Control.TemplateProperty,ButtonTemplate(radius)));
        var disabled=new Trigger{Property=UIElement.IsEnabledProperty,Value=false};disabled.Setters.Add(Value(Control.ForegroundProperty,Muted));disabled.Setters.Add(Value(Control.BackgroundProperty,Canvas));style.Triggers.Add(disabled);return style;
    }
    private static ControlTemplate ButtonTemplate(double radius,string? icon=null)
    {
        var template=new ControlTemplate(typeof(Button));var border=new FrameworkElementFactory(typeof(Border),"surface");border.SetValue(Border.CornerRadiusProperty,new CornerRadius(radius));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderBrushProperty,new TemplateBindingExtension(Control.BorderBrushProperty));border.SetValue(Border.BorderThicknessProperty,new TemplateBindingExtension(Control.BorderThicknessProperty));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
        FrameworkElementFactory content;
        if(icon==null){content=new(typeof(ContentPresenter));content.SetValue(ContentPresenter.ContentSourceProperty,"Content");content.SetValue(ContentPresenter.RecognizesAccessKeyProperty,true);}
        else{content=new(typeof(Path));content.SetValue(Path.DataProperty,Geometry.Parse(icon));content.SetValue(Path.StrokeProperty,new TemplateBindingExtension(Control.ForegroundProperty));content.SetValue(Path.StrokeThicknessProperty,1.6);content.SetValue(Path.StrokeStartLineCapProperty,PenLineCap.Round);content.SetValue(Path.StrokeEndLineCapProperty,PenLineCap.Round);content.SetValue(Path.StrokeLineJoinProperty,PenLineJoin.Round);content.SetValue(Path.StretchProperty,Stretch.Uniform);content.SetValue(FrameworkElement.WidthProperty,18d);content.SetValue(FrameworkElement.HeightProperty,18d);content.SetValue(UIElement.IsHitTestVisibleProperty,false);}
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);content.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(content);template.VisualTree=border;
        var hover=new Trigger{Property=UIElement.IsMouseOverProperty,Value=true};hover.Setters.Add(new Setter(Border.BorderBrushProperty,new DynamicResourceExtension(InputBorder),"surface"));template.Triggers.Add(hover);
        var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,new DynamicResourceExtension(Text),"surface"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(2),"surface"));template.Triggers.Add(focus);
        var pressed=new Trigger{Property=System.Windows.Controls.Primitives.ButtonBase.IsPressedProperty,Value=true};pressed.Setters.Add(new Setter(Border.BorderBrushProperty,new DynamicResourceExtension(Accent),"surface"));template.Triggers.Add(pressed);return template;
    }
    private static Style NavigationStyle()
    {
        var style=new Style(typeof(ListBoxItem));style.Setters.Add(new Setter(Control.PaddingProperty,new Thickness(12,10,12,10)));style.Setters.Add(new Setter(FrameworkElement.MarginProperty,new Thickness(0,3,0,3)));style.Setters.Add(Value(Control.ForegroundProperty,Muted));style.Setters.Add(new Setter(Control.BackgroundProperty,Brushes.Transparent));style.Setters.Add(new Setter(Control.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch));
        var template=new ControlTemplate(typeof(ListBoxItem));var border=new FrameworkElementFactory(typeof(Border),"selection");border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8));border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));border.SetValue(Border.BorderThicknessProperty,new Thickness(0));border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));var content=new FrameworkElementFactory(typeof(ContentPresenter));content.SetValue(ContentPresenter.ContentSourceProperty,"Content");border.AppendChild(content);template.VisualTree=border;
        var focus=new Trigger{Property=UIElement.IsKeyboardFocusWithinProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,new DynamicResourceExtension(Text),"selection"));focus.Setters.Add(new Setter(Border.BorderThicknessProperty,new Thickness(2),"selection"));template.Triggers.Add(focus);style.Setters.Add(new Setter(Control.TemplateProperty,template));
        var selected=new Trigger{Property=ListBoxItem.IsSelectedProperty,Value=true};selected.Setters.Add(Value(Control.BackgroundProperty,AccentSoft));selected.Setters.Add(Value(Control.ForegroundProperty,SelectionText));selected.Setters.Add(new Setter(Control.FontWeightProperty,FontWeights.SemiBold));style.Triggers.Add(selected);return style;
    }
    private static readonly IReadOnlyDictionary<string,string> Icons=new Dictionary<string,string>
    {
        ["风格"]="M2 17 L8 3 L14 17 M4 12 L12 12 M15 7 L20 7 M17.5 7 L17.5 17",
        ["翻译截图"]="M7 2 L3 2 L3 7 M15 2 L19 2 L19 7 M3 15 L3 19 L7 19 M19 15 L19 19 L15 19 M7 10 L15 10 M11 6 L11 14",
        ["回看"]="M4 7 A8 8 0 1 1 3 13 M4 2 L4 7 L9 7 M11 7 L11 12 L15 14",
        ["收起"]="M3 14 L11 6 L19 14",
        ["停止"]="M5 5 L17 5 L17 17 L5 17 Z",
        ["重新生成"]="M18 8 A8 8 0 0 0 4 6 M4 2 L4 6 L8 6 M4 14 A8 8 0 0 0 18 16 M18 20 L18 16 L14 16",
        ["复制"]="M8 7 L19 7 L19 20 L8 20 Z M14 3 L3 3 L3 16",
        ["查看原文"]="M3 4 L10 4 L11 6 L12 4 L19 4 L19 18 L12 18 L11 20 L10 18 L3 18 Z M11 6 L11 17",
        ["清除当前内容"]="M3 6 L19 6 M8 6 L8 3 L14 3 L14 6 M5 6 L6 20 L16 20 L17 6 M9 10 L9 16 M13 10 L13 16",
        ["设置"]="M8 3 L14 3 L15 7 L19 8 L19 14 L15 15 L14 19 L8 19 L7 15 L3 14 L3 8 L7 7 Z M14 11 A3 3 0 1 1 8 11 A3 3 0 1 1 14 11"
    };
}
