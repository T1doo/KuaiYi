using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
namespace KuaiYi.App;
internal sealed class ScreenshotCleanupException(IEnumerable<Exception> errors):AggregateException("Screenshot cleanup unconfirmed.",errors);
internal static class ScreenshotPreviewCleanup
{
    public static void Attempt(List<Exception> errors,Action action){try{action();}catch(Exception error){errors.Add(error);}}
    public static void Own(WriteableBitmap bitmap,Action populateAndShow,Func<(Window? Window,Panel? Panel,Image? Image)> current)
    {try{populateAndShow();}finally{var value=current();Release(value.Window,value.Panel,value.Image,bitmap);}}
    public static void Release(Window? preview,Panel? panel,Image? image,WriteableBitmap bitmap)
    {
        List<Exception> errors=[];if(preview!=null)Attempt(errors,preview.Close);if(image!=null)Attempt(errors,()=>image.Source=null);
        if(panel!=null)Attempt(errors,()=>panel.Children.Clear());if(preview!=null)Attempt(errors,()=>preview.Content=null);
        byte[] zero=new byte[checked(bitmap.PixelWidth*4)];
        for(int y=0;y<bitmap.PixelHeight;y++)Attempt(errors,()=>bitmap.WritePixels(new(0,y,bitmap.PixelWidth,1),zero,zero.Length,0));
        if(errors.Count>0)throw new ScreenshotCleanupException(errors);
    }
}
