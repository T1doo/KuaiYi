using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal static class ScreenshotPreview
{
    internal static bool Confirm(Window owner,ScreenshotPixels crop,bool off,CancellationToken cancellation)
    {
        var bitmap=new WriteableBitmap(crop.Bounds.Width,crop.Bounds.Height,96,96,PixelFormats.Bgra32,null);
        using(var memory=crop.Bytes.Pin())unsafe{bitmap.WritePixels(new(0,0,bitmap.PixelWidth,bitmap.PixelHeight),(nint)memory.Pointer,crop.Bytes.Length,bitmap.PixelWidth*4);}
        var window=new Window{Owner=owner,Title="快译 · 本地截图预览",Width=460,Height=360,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var panel=new StackPanel{Margin=new(16)};panel.Children.Add(new Image{Source=bitmap,Height=220,Stretch=Stretch.Uniform});
        panel.Children.Add(new TextBlock{Text=off?"Off · 本次只做本地文字识别，不发送模型。":"本次只做本地文字识别，不发送模型。",TextWrapping=TextWrapping.Wrap});
        var buttons=new StackPanel{Orientation=Orientation.Horizontal};var allow=new Button{Content=off?"启用并本地识别":"确认本地识别",Margin=new(0,10,10,0)};var reject=new Button{Content="取消",IsCancel=true,IsDefault=true,Margin=new(0,10,0,0)};
        allow.Click+=(_,_)=>window.DialogResult=true;reject.Click+=(_,_)=>window.DialogResult=false;buttons.Children.Add(allow);buttons.Children.Add(reject);panel.Children.Add(buttons);window.Content=panel;window.Loaded+=(_,_)=>reject.Focus();
        var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(30)};bool closed=false;
        void Cancel(){if(!closed&&window.IsLoaded)window.DialogResult=false;}
        window.Closed+=(_,_)=>closed=true;window.Deactivated+=(_,_)=>Cancel();timer.Tick+=(_,_)=>Cancel();
        using var registration=cancellation.Register(()=>owner.Dispatcher.BeginInvoke(Cancel));
        try{cancellation.ThrowIfCancellationRequested();timer.Start();return window.ShowDialog()==true&&!cancellation.IsCancellationRequested;}
        finally
        {
            timer.Stop();try{if(!closed)window.Close();}finally
            {
            byte[] zero=new byte[bitmap.PixelWidth*4];for(int y=0;y<bitmap.PixelHeight;y++)bitmap.WritePixels(new(0,y,bitmap.PixelWidth,1),zero,zero.Length,0);
            }
        }
    }
}
