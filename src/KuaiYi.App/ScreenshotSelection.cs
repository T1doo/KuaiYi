using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.Imaging;
using KuaiYi.Windows;
namespace KuaiYi.App;
/// <summary>One HWND per monitor. Physical cursor is authoritative, no desktop-DIP union window.</summary>
internal sealed class ScreenshotSelection:IDisposable
{
    private readonly ScreenshotSnapshot snapshot;
    private readonly Func<bool> current;
    private readonly List<Mask> masks=[];
    private readonly TaskCompletionSource<PixelRect?> answer=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherTimer limit;
    private readonly ScreenshotDrag gesture=new();
    private bool disposed;
    public ScreenshotSelection(ScreenshotSnapshot snapshot,Func<bool> current)
    {
        this.snapshot=snapshot;this.current=current;
        limit=new(){Interval=TimeSpan.FromSeconds(30)};limit.Tick+=(_,_)=>Cancel();
    }
    public async Task<PixelRect?> RunAsync(CancellationToken cancellation)
    {
        try
        {
            foreach(var monitor in snapshot.Topology.Monitors)
            {
                cancellation.ThrowIfCancellationRequested();if(!current())throw new OperationCanceledException();
                var tile=snapshot.Tiles.Single(t=>t.Bounds==monitor.Bounds);var mask=new Mask(this,monitor,tile);masks.Add(mask);mask.Show();
            }
            using var registration=cancellation.Register(()=>Application.Current.Dispatcher.BeginInvoke(Cancel));
            limit.Start();masks.First().Activate();masks.First().Focus();return await answer.Task;
        }
        finally{Dispose();}
    }
    private void Begin(Mask mask){if(!current()){Cancel();return;}gesture.Begin(ScreenshotDesktop.Cursor());mask.CaptureMouse();Update();}
    private void Update()
    {
        if(!gesture.Active)return;gesture.Move(ScreenshotDesktop.Cursor());
        foreach(var mask in masks)mask.InvalidateVisual();
    }
    private void Finish(Mask mask)
    {
        if(!gesture.Active)return;Update();var selected=gesture.Finish(ScreenshotDesktop.Cursor());mask.ReleaseMouseCapture();
        if(!current()){Cancel();return;}
        try{snapshot.Topology.Validate(selected!.Value);answer.TrySetResult(selected);}catch(ScreenshotException error){gesture.Reset();foreach(var m in masks){m.Note=error.Code==ScreenshotFailure.RegionTooSmall?"选区至少 8×8 物理像素，请重选":"范围过大或超出屏幕，请重选";m.InvalidateVisual();}}
    }
    public void Cancel()=>answer.TrySetResult(null);
    public void Dispose(){if(disposed)return;disposed=true;limit.Stop();var failures=new List<Exception>();foreach(var mask in masks)try{mask.ReleaseAndClose();}catch(Exception error){failures.Add(error);}masks.Clear();if(failures.Count>0)throw new AggregateException("Screenshot mask cleanup failed.",failures);}
    private sealed class Mask:Window
    {
        private readonly ScreenshotSelection owner;
        private readonly ScreenshotMonitor monitor;
        private readonly ScreenshotPixels pixels;
        private readonly WriteableBitmap image;
        private HwndSource? source;
        private bool closing,placed;
        public string Note="拖动框选 · 松开确认 · Esc / 右键取消";
        [DllImport("user32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)]private static extern bool SetWindowPos(nint hwnd,nint after,int x,int y,int width,int height,uint flags);
        public Mask(ScreenshotSelection owner,ScreenshotMonitor monitor,ScreenshotPixels pixels)
        {
            this.owner=owner;this.monitor=monitor;this.pixels=pixels;
            WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=true;Background=Brushes.Black;Cursor=Cursors.Cross;WindowStartupLocation=WindowStartupLocation.Manual;
            image=new(pixels.Bounds.Width,pixels.Bounds.Height,96,96,PixelFormats.Bgra32,null);Write(pixels.Bytes);
            SourceInitialized+=(_,_)=>
            {
                source=(HwndSource)PresentationSource.FromVisual(this);ScreenshotDesktop.ExcludeWindow(source.Handle);
                if(!SetWindowPos(source.Handle,new(-1),monitor.Bounds.Left,monitor.Bounds.Top,monitor.Bounds.Width,monitor.Bounds.Height,0x0040))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                source.AddHook(Hook);
            };
            Loaded+=(_,_)=>{if(!SetWindowPos(source!.Handle,new(-1),monitor.Bounds.Left,monitor.Bounds.Top,monitor.Bounds.Width,monitor.Bounds.Height,0x0040))owner.Cancel();placed=true;};
            MouseLeftButtonDown+=(_,e)=>{owner.Begin(this);e.Handled=true;};MouseMove+=(_,_)=>owner.Update();MouseLeftButtonUp+=(_,e)=>{owner.Finish(this);e.Handled=true;};
            MouseRightButtonDown+=(_,e)=>{owner.Cancel();e.Handled=true;};PreviewKeyDown+=(_,e)=>{if(e.Key==Key.Escape){owner.Cancel();e.Handled=true;}};
            LostMouseCapture+=(_,_)=>{if(owner.gesture.LostCapture())owner.Cancel();};
            Closing+=(_,e)=>{if(!closing){e.Cancel=true;owner.Cancel();}};
        }
        private nint Hook(nint hwnd,int msg,nint w,nint l,ref bool handled)
        {if(msg==0x007e||placed&&msg==0x02e0||msg==0x001c&&w==0)owner.Cancel();return 0;} // display, DPI or desktop deactivation invalidates frozen frame.
        private void Write(ReadOnlyMemory<byte> bytes)
        {
            using var pinned=bytes.Pin();unsafe{image.WritePixels(new Int32Rect(0,0,image.PixelWidth,image.PixelHeight),(nint)pinned.Pointer,bytes.Length,image.PixelWidth*4);}
        }
        protected override void OnRender(DrawingContext dc)
        {
            base.OnRender(dc);var full=new Rect(0,0,ActualWidth,ActualHeight);dc.DrawImage(image,full);dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(100,0,0,0)),null,full);
            if(owner.gesture.Region is {} selected)
            {
                var b=monitor.Bounds;int left=Math.Max(selected.Left,b.Left),top=Math.Max(selected.Top,b.Top),right=Math.Min(selected.Right,b.Right),bottom=Math.Min(selected.Bottom,b.Bottom);
                if(right>left&&bottom>top)
                {
                    // Rendering transforms use the actual per-HWND DIP extent; selection/cropping stays physical.
                    var rect=new Rect((left-b.Left)*ActualWidth/b.Width,(top-b.Top)*ActualHeight/b.Height,(right-left)*ActualWidth/b.Width,(bottom-top)*ActualHeight/b.Height);
                    dc.PushClip(new RectangleGeometry(rect));dc.DrawImage(image,full);dc.Pop();dc.DrawRectangle(null,new Pen(Brushes.LimeGreen,2),rect);
                }
                Note=$"{selected.Width}×{selected.Height} 像素 · 松开确认 · Esc / 右键取消";
            }
            var label=new FormattedText(Note,System.Globalization.CultureInfo.CurrentUICulture,FlowDirection.LeftToRight,new Typeface("Segoe UI"),16,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
            dc.DrawRectangle(Brushes.Black,null,new Rect(16,16,label.Width+16,label.Height+12));dc.DrawText(label,new Point(24,22));
        }
        public void ReleaseAndClose()
        {
            closing=true;if(IsMouseCaptured)ReleaseMouseCapture();source?.RemoveHook(Hook);
            // WPF's mutable staging bitmap is wiped before HWND/source release; system/GPU copies cannot be guaranteed erased.
            byte[] zero=new byte[Math.Min(pixels.Bounds.Width*4*32,pixels.Bytes.Length)];int rowCount=zero.Length/(pixels.Bounds.Width*4);
            for(int y=0;y<image.PixelHeight;y+=rowCount)image.WritePixels(new Int32Rect(0,y,image.PixelWidth,Math.Min(rowCount,image.PixelHeight-y)),zero,image.PixelWidth*4,0);
            Close();
        }
    }
}
