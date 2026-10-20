using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.Imaging;
namespace KuaiYi.App;

/// <summary>Borrowed source; this region owns only its lazy, bounded native preview bitmap.</summary>
internal sealed class MarkdownImageSourceRegion:IDisposable
{
    private ImageSourceOwner? source;
    private Action? observeReader,changed;
    private Func<bool>? canRead;
    private Action<Exception>? failed;
    private Exception? previewFailure;
    private readonly Func<Func<bool>>? captureReaderGuard;
    private WriteableBitmap? bitmap;
    private bool updating,disposed;
    private long revealRevision;
    public Expander View {get;}=new(){Header="查看原文图片",IsExpanded=false,ClipToBounds=true};
    public Image Image {get;}=new(){Stretch=Stretch.Uniform,ClipToBounds=true};
    public MarkdownImageSourceRegion(ImageSourceOwner source,Action? observeReader,Action? changed,Func<bool>? canRead,Func<Func<bool>>? captureReaderGuard=null,Action<Exception>? failed=null)
    {
        View.Header=source.Kind==ImageSourceKind.LocalFile?"查看文件图片"+(source.Notice is {} note?" · "+note:""):"查看原文图片";
        this.source=source;this.observeReader=observeReader;this.changed=changed;this.canRead=canRead;this.captureReaderGuard=captureReaderGuard;this.failed=failed;View.Content=Image;
        System.Windows.Automation.AutomationProperties.SetName(View,"查看所示版本原文图片");System.Windows.Automation.AutomationProperties.SetName(Image,"所示版本原文图片的本地缩略预览");
        View.Expanded+=Expanded;View.Collapsed+=Collapsed;
    }
    public bool Matches(ImageSourceOwner value)=>ReferenceEquals(source,value);
    public void Constrain(double width,double height,double size,Brush foreground)
    {View.Width=View.MaxWidth=width;Image.Width=Image.MaxWidth=width;Image.Height=Image.MaxHeight=Math.Min(180,Math.Max(1,height/2));View.FontSize=size;View.Foreground=foreground;}
    public void SetExpanded(bool expanded,bool materialize=true)
    {
        if(disposed)return;
        updating=true;
        try{View.IsExpanded=expanded&&Eligible();if(View.IsExpanded&&materialize)Materialize();else ReleaseBitmap();}
        finally{updating=false;}
    }
    private bool Eligible()=>!disposed&&source is {IsDisposed:false}&&canRead?.Invoke()!=false;
    private void Materialize()
    {
        if(!Eligible())return;
        if(previewFailure!=null){ReleaseBitmap();previewFailure=null;}
        if(bitmap!=null)return;
        using var raster=source!.CreatePreview(OwnedSourceImage.PreviewSideLimit,OwnedSourceImage.PreviewSideLimit);
        var created=new WriteableBitmap(raster.Bounds.Width,raster.Bounds.Height,96,96,PixelFormats.Bgra32,null);bitmap=created;
        try
        {
            if(!System.Runtime.InteropServices.MemoryMarshal.TryGetArray(raster.Bytes,out ArraySegment<byte> bytes)||bytes.Array==null)throw new InvalidOperationException("Preview raster storage unavailable.");
            created.WritePixels(new Int32Rect(0,0,created.PixelWidth,created.PixelHeight),bytes.Array,created.PixelWidth*4,bytes.Offset);
            if(!Eligible()){ReleaseBitmap();return;}Image.Source=created;
        }
        catch(Exception error){List<Exception> errors=[error];ScreenshotPreviewCleanup.Attempt(errors,ReleaseBitmap);throw new ScreenshotCleanupException(errors);}
    }
    private void ReleaseBitmap()
    {
        var held=bitmap;if(held==null){Image.Source=null;return;}
        // Keep ownership if wiping fails, so the next clear/dispose can retry it.
        ScreenshotPreviewCleanup.Release(null,null,Image,held);bitmap=null;
    }
    public void Expand()
    {
        if(!Eligible())return;
        try
        {
            View.IsExpanded=true;if(!View.IsExpanded)return;Materialize();observeReader?.Invoke();long current=++revealRevision;var guard=captureReaderGuard?.Invoke();
            if(guard==null)return;
            Image.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,()=>
            {if(!Eligible()||current!=revealRevision||!View.IsExpanded||!guard())return;try{observeReader?.Invoke();View.BringIntoView();}catch(Exception error){Failed(error);}});
        }
        catch(Exception error){Failed(error);}
    }
    private void Expanded(object? sender,RoutedEventArgs args)
    {if(disposed||updating)return;try{if(!Eligible()){SetExpanded(false);return;}Materialize();observeReader?.Invoke();changed?.Invoke();}catch(Exception error){Failed(error);}}
    private void Collapsed(object? sender,RoutedEventArgs args)
    {if(disposed||updating)return;revealRevision++;try{ReleaseBitmap();observeReader?.Invoke();changed?.Invoke();}catch(Exception error){Failed(error);}}
    private void Failed(Exception error)
    {previewFailure=error;revealRevision++;updating=true;try{View.IsExpanded=false;}finally{updating=false;}failed?.Invoke(error);}
    public void Dispose()
    {
        revealRevision++;source=null;observeReader=changed=null;canRead=null;failed=null;List<Exception> errors=[];
        if(!disposed)
        {
            disposed=true;View.Expanded-=Expanded;View.Collapsed-=Collapsed;
            ScreenshotPreviewCleanup.Attempt(errors,()=>View.IsExpanded=false);ScreenshotPreviewCleanup.Attempt(errors,()=>View.Content=null);
        }
        ScreenshotPreviewCleanup.Attempt(errors,ReleaseBitmap);if(errors.Count>0)throw new ScreenshotCleanupException(errors);
    }
}
