using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KuaiYi.App;
using KuaiYi.Core.Settings;
using KuaiYi.Windows;
namespace KuaiYi.GrowthHarness;
internal static partial class UiPolishProbe
{
    private sealed partial class Owner
    {
        private async Task ChoiceScenes(ThemePreference theme)
        {
            // Original motion choice changes only the draft, never system motion or theme.
            var field=typeof(SettingsWindow).GetField("motion",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);
            Require(field?.FieldType==typeof(ComboBox),"Exact original choice unavailable.");
            var combo=(ComboBox)field!.GetValue(window)!;int initial=combo.SelectedIndex;object enabled=combo.ReadLocalValue(UIElement.IsEnabledProperty);
            Require(combo.Items.Count==3&&initial>=0,"Fixed original motion choices required.");
            int changes=0;SelectionChangedEventHandler changed=(_,_)=>changes++;combo.SelectionChanged+=changed;
            try
            {
                await Focus(combo);combo.ApplyTemplate();
                var popup=combo.Template.FindName("PART_Popup",combo) as Popup??throw new InvalidOperationException("Original PART_Popup missing.");
                await ChoiceKey(combo,Key.F4);Require(combo.IsDropDownOpen&&popup.IsOpen,"Original F4 did not open popup.");
                await window!.Dispatcher.InvokeAsync(()=>{},System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                CapturePopup(theme+"-choice-popup",theme,combo,popup);
                await ChoiceKey(combo,Key.Escape);Require(!combo.IsDropDownOpen&&!popup.IsOpen&&combo.SelectedIndex==initial,"Escape failed to close without selection.");
                Capture(theme+"-choice-escape",theme,null,"ProgrammaticEscapeClosed",940,700,combo);
                await ChoiceKey(combo,Key.F4);Require(popup.IsOpen,"Second original popup did not open.");
                await ChoiceKey(combo,initial<combo.Items.Count-1?Key.Down:Key.Up);await ChoiceKey(combo,Key.Enter);
                int expected=initial<combo.Items.Count-1?initial+1:initial-1;
                Require(!popup.IsOpen&&!combo.IsDropDownOpen&&combo.SelectedIndex==expected&&changes>0,"Original keyboard selection/close event not observed.");
                combo.SelectedIndex=initial;await Next();Require(combo.SelectedIndex==initial,"Original draft choice not restored.");
                Require(combo.IsKeyboardFocusWithin,"Original keyboard focus not retained.");
                Capture(theme+"-choice-keyboard",theme,null,"ProgrammaticRoutedKeysSelectionRestored",940,700,combo);
                choiceChecks.Add(new{Theme=theme.ToString(),OriginalSelectedIndex=initial,AfterKeySelectedIndex=expected,SelectionChangedCount=changes,OriginalRestored=true,EscapeClosed=true,InputScope="ProgrammaticRoutedKey_OriginalComboOnly",ComboIdentity=System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(combo)});
                combo.IsEnabled=false;await Next();Require(!combo.IsEnabled&&!combo.IsDropDownOpen,"Disabled original choice state differs.");
                Capture(theme+"-choice-disabled",theme,null,"OriginalChoiceDisabled_NoKeyDispatched",940,700,combo);
            }
            finally
            {
                combo.SelectionChanged-=changed;combo.IsDropDownOpen=false;combo.SelectedIndex=initial;
                if(enabled==DependencyProperty.UnsetValue)combo.ClearValue(UIElement.IsEnabledProperty);else combo.SetValue(UIElement.IsEnabledProperty,enabled);
            }
        }
        private async Task ChoiceKey(ComboBox combo,Key key)
        {
            Check();Require(combo.IsEnabled,"Never dispatch synthetic input into a disabled control.");
            var source=PresentationSource.FromVisual(combo)??throw new InvalidOperationException("Original keyboard presentation source missing.");
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key){RoutedEvent=Keyboard.KeyDownEvent});
            combo.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,Environment.TickCount,key){RoutedEvent=Keyboard.KeyUpEvent});
            await Next();
        }
        private sealed record PopupState(long Hwnd,int Left,int Top,int Right,int Bottom,bool Open,int Selected,double Width,double Height,double DpiX,double DpiY,int Focus);
        private PopupState PopupFacts(ComboBox combo,Popup popup)
        {
            var child=popup.Child as FrameworkElement??throw new InvalidOperationException("Original popup child missing.");
            Require(popup.IsOpen&&combo.IsDropDownOpen&&child.IsVisible&&child.ActualWidth>0&&child.ActualHeight>0,"Original popup not visibly arranged.");
            var source=PresentationSource.FromVisual(child) as HwndSource??throw new InvalidOperationException("Original popup HWND source missing.");
            Require(NativeDesktop.GetWindowRect(source.Handle,out var bounds),"Original popup bounds unavailable.");
            var dpi=VisualTreeHelper.GetDpi(child);
            return new((long)source.Handle,bounds.Left,bounds.Top,bounds.Right,bounds.Bottom,popup.IsOpen,combo.SelectedIndex,child.ActualWidth,child.ActualHeight,dpi.PixelsPerInchX,dpi.PixelsPerInchY,Keyboard.FocusedElement==null?0:System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Keyboard.FocusedElement));
        }
        private void CapturePopup(string name,ThemePreference theme,ComboBox combo,Popup popup)
        {
            Check();var before=PopupFacts(combo,popup);var child=(FrameworkElement)popup.Child;var dpi=VisualTreeHelper.GetDpi(child);
            int x=checked((int)Math.Ceiling(child.ActualWidth*dpi.DpiScaleX)),y=checked((int)Math.Ceiling(child.ActualHeight*dpi.DpiScaleY));
            Require(x>0&&y>0&&(long)x*y<=8_000_000&&scenes.Count<24,"Popup raster dimensions/count bound.");
            var bitmap=new RenderTargetBitmap(x,y,dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32);bitmap.Render(child);bitmap.Freeze();
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var memory=new MemoryStream();encoder.Save(memory);
            Require(memory.Length<=2*1024*1024&&pngBytes+memory.Length<=16*1024*1024,"Popup raster byte bound.");
            byte[] bytes=memory.ToArray();string file=name+".png";using(var stream=new FileStream(Path.Combine(output,file),FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}pngBytes+=bytes.Length;
            var after=PopupFacts(combo,popup);bool coherent=before==after;
            scenes.Add(new{Name=name,File=file,Bytes=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),request.Side,request.ProductHead,AppDllSha256=productHash,
                Theme=theme.ToString(),Motion=(string?)null,Status="ActualPopup_ProgrammaticF4",RequestedWidthDip=940d,RequestedHeightDip=(double?)700d,WindowWidthDip=window!.ActualWidth,WindowHeightDip=window.ActualHeight,
                ClientWidthDip=child.ActualWidth,ClientHeightDip=child.ActualHeight,PixelsX=x,PixelsY=y,DpiX=dpi.PixelsPerInchX,DpiY=dpi.PixelsPerInchY,window.FontSize,
                Target=new{Type=combo.GetType().Name,combo.IsEnabled,combo.IsKeyboardFocused},BeforePopup=before,AfterPopup=after,OwnerHwnd=(long)new WindowInteropHelper(window).Handle,StateCoherent=coherent,
                RasterKind="ActualOriginalPopupChild_NotSettingsContent",ProgrammaticRoutedKey="F4",PhysicalInput=false,SyntheticSource=true});
            Require(coherent,"Popup changed during capture; invalid PNG retained.");
        }
    }
}
