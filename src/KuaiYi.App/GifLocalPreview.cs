using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;

// Actual P0 local action; production file/drop and model routing remain separate unfinished work.
internal sealed partial class ImageProbeWindow
{
    private GifPreviewOwner? gifPreview;
    private void SetupGifPreview(StackPanel panel)
    {
        var open=new Button{Content="GIF · 使用首帧 · 本地预览"};panel.Children.Add(open);
        open.Click+=async(_,_)=>await PreviewGif();
    }
    private void ClearGifPreview()
    {
        if(gifPreview==null)return;
        try{gifPreview.Dispose();gifPreview=null;}
        catch(Exception error){status.Text="GIF 预览清理未确认 · "+error.GetType().Name;} // Keep same bitmap owner for retry.
    }
    private async Task PreviewGif()
    {
        if(oleWindow!=null||running!=null||ownedWorker!=null||ownedCaptureHost!=null||captureTask is {IsCompleted:false}||settingsOpening||settingsWindow!=null||gate.Snapshot.Activation!=Activation.On||gate.Snapshot.Suspension!=Suspension.None||gifPreview!=null)
        {status.Text="请先允许本次本地操作，并等待旧任务及预览清理完成。";return;}
        var dialog=new Microsoft.Win32.OpenFileDialog{Filter="GIF|*.gif",Multiselect=false};if(dialog.ShowDialog(this)!=true)return;
        if(!gate.TryStartRequest(session,Guid.NewGuid(),null,out var current,out _)||!gate.TryLinkRequestCancellation(current,default,out var linked))return;
        using(linked)
        {
            running=current;choose.IsEnabled=false;browserInbox.Clear();byte[]? bytes=null;OwnedImageWorker? owner=null;DecodedRaster? raster=null;
            try
            {
                await workflow.PublishAsync(current,ImageUiUpdate.Started(),Apply);
                bytes=await Task.Run(()=>LocalImageFile.ReadGifAsync(dialog.FileName,linked!.Token),linked!.Token);
                linked.Token.ThrowIfCancellationRequested();var worker=new ImageWorkerClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe"));owner=new(current,worker);ownedWorker=owner;
                var job=new ImageDecodeJob(current.SessionId,++workerVersion,current.Epoch,bytes.Length,ImageDecodePolicy.Hash(bytes));
                // All container scans and native work stay off the dispatcher. Delivery includes original IO/actual exit.
                raster=await Task.Run(()=>worker.PrepareAsync(bytes,job,512,linked.Token),linked.Token);
                linked.Token.ThrowIfCancellationRequested();
                await workflow.PublishAsync(current,new(ImageUiEventKind.Completed,"GIF · 使用首帧 · 本地预览；未发送模型",string.Empty),update=>
                {
                    gifPreview=new();gifPreview.Show(this,raster);Apply(update);
                });
                if(gifPreview is {} shown)
                {
                    using var cancel=linked.Token.Register(()=>Dispatcher.BeginInvoke(()=>{if(ReferenceEquals(gifPreview,shown))ClearGifPreview();}));
                    await shown.Closed;
                }
            }
            catch(OperationCanceledException){await workflow.PublishAsync(current,ImageUiUpdate.Cancelled(),Apply);}
            catch(Exception error){await workflow.PublishAsync(current,ImageUiUpdate.Failed(error),Apply);}
            finally
            {
                ClearGifPreview();raster?.Dispose();if(bytes!=null)CryptographicOperations.ZeroMemory(bytes);
                if(owner!=null&&await owner.Client.TryFinalizeAsync())if(ReferenceEquals(ownedWorker,owner))ownedWorker=null;
                if(running==current)running=null;choose.IsEnabled=running==null&&ownedWorker==null&&ownedCaptureHost==null&&gifPreview==null;
                if(owner!=null&&ReferenceEquals(ownedWorker,owner))await workflow.PublishAsync(current,ImageUiUpdate.Unconfirmed(),Apply);
            }
        }
    }
}

internal sealed class GifPreviewOwner:IDisposable
{
    private Window? window;private Image? image;private WriteableBitmap? bitmap;
    private readonly TaskCompletionSource closed=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal Task Closed=>closed.Task;
    internal void Show(Window owner,DecodedRaster source)
    {
        if(source.Metadata.Format!=EncodedRasterFormat.Gif||source.Notice!="使用首帧"||source.Metadata.PreparedInfo==null||source.Metadata.Width>512||source.Metadata.Height>512)throw new InvalidDataException("GIF preview requires a verified bounded first frame.");
        var meta=source.Metadata;bitmap=new(meta.Width,meta.Height,96,96,PixelFormats.Bgra32,null);
        if(!MemoryMarshal.TryGetArray(source.Bytes,out ArraySegment<byte> held)||held.Array==null)throw new InvalidDataException("Missing preview storage.");
        bitmap.WritePixels(new(0,0,meta.Width,meta.Height),held.Array,meta.Stride,held.Offset);
        image=new(){Source=bitmap,Height=260,Stretch=Stretch.Uniform};
        var panel=new StackPanel{Margin=new(16)};
        panel.Children.Add(new TextBlock{Text=source.Notice,FontWeight=FontWeights.Bold,FontSize=18});
        panel.Children.Add(image);panel.Children.Add(new TextBlock{Text="本地 GIF 预览 · 未翻译 · 未发送",TextWrapping=TextWrapping.Wrap});
        System.Windows.Automation.AutomationProperties.SetName(image,"GIF 原图使用首帧的本地预览");
        window=new(){Owner=owner,Title="快译 · GIF · 使用首帧",Width=460,Height=380,Content=panel,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var close=new Button{Content="关闭预览"};panel.Children.Add(close);close.Click+=(_,_)=>window?.Close();
        window.Closed+=(_,_)=>closed.TrySetResult();window.Show();
    }
    public void Dispose()
    {
        if(bitmap!=null)ScreenshotPreviewCleanup.Release(window,null,image,bitmap);else window?.Close();
        bitmap=null;image=null;window=null;closed.TrySetResult();
    }
}
