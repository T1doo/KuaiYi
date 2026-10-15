// Explicit UI/native-adapter call ports. No WPF window, bitmap, broker or process is run here.
using KuaiYi.Imaging;
namespace System.Windows
{
    internal readonly record struct Int32Rect(int X,int Y,int Width,int Height);
    internal sealed class Window
    {
        public object? Content {get;set;}
        public bool ThrowOnClose {get;set;}
        public int CloseCalls {get;private set;}
        public void Close(){CloseCalls++;if(ThrowOnClose)throw new InvalidOperationException("synthetic close failure");}
    }
}
namespace System.Windows.Controls
{
    internal sealed class ContextMenu {public bool IsOpen {get;set;}}
    internal sealed class Panel {public List<UIElement> Children {get;}=[];}
    internal sealed class Image:FrameworkElement {public object? Source {get;set;}}
}
namespace System.Windows.Media.Imaging
{
    internal sealed class WriteableBitmap(int width,int height)
    {
        public int PixelWidth=>width;
        public int PixelHeight=>height;
        public byte[] Pixels {get;}=Enumerable.Repeat((byte)51,width*height*4).ToArray();
        public int? FailRow {get;set;}
        public List<int> Writes {get;}=[];
        public void WritePixels(Int32Rect rect,byte[] zero,int stride,int offset)
        {Writes.Add(rect.Y);if(FailRow==rect.Y)throw new InvalidOperationException("synthetic row failure");zero.AsSpan(offset,rect.Width*4).CopyTo(Pixels.AsSpan(rect.Y*width*4,rect.Width*4));}
    }
}
namespace KuaiYi.Imaging
{
    internal sealed class CaptureHostClient
    {
        public Func<ValueTask<bool>> Finalize {get;set;}=()=>ValueTask.FromResult(true);
        public int Calls {get;private set;}
        public ValueTask<bool> TryFinalizeAsync(bool retry){if(!retry)throw new InvalidOperationException("owned retry expected");Calls++;return Finalize();}
    }
}
namespace KuaiYi.App
{
    internal sealed partial class ShellWindow
    {
        private readonly System.Windows.Controls.ContextMenu overflowMenu=new();
        private readonly System.Windows.Controls.TextBlock connectionStatus=new(){Text="old synthetic connection status"};
        private string? translationStatus="old synthetic translation status";
        private bool externalDropHover,restoreDropBody,restoreDropPeek;
        private readonly BadgePort dropHint=new();
        internal Task DropCleanup=Task.CompletedTask;
        internal int SelectionClears,DropRevocations,FrameStops,ClearErrors;
        private void ClearSelectionSource(){SelectionClears++;ClearOriginalSource();}
        private void RevokeTextDrop(bool clearSource=false){if(!clearSource)throw new InvalidOperationException("clear source expected");DropRevocations++;}
        private void StopFrames()=>FrameStops++;
        private void Report(string text,Exception error){ClearErrors++;button.ToolTip=text+" · "+error.GetType().Name;}
        internal Task<bool> ClearContentForTest()=>ClearCurrentContent();
        internal Task FinishScreenshotForTest()=>StopHotkeyScreenshot();
        internal Task ObserveScreenshotForTest(Task operation,long revision)=>ObserveHotkeyScreenshot(operation,revision);
        internal void ScreenshotForTest(Task operation,CancellationTokenSource? cancellation=null,CaptureHostClient? owner=null)
        {screenshotHotkeyTask=operation;screenshotHotkeyStop=cancellation;screenshotHotkeyOwner=owner;}
        internal Func<bool> RestoreGuardForTest(CancellationTokenSource owner){screenshotHotkeyStop=owner;return CaptureScreenshotRestoreGuard(owner);}
        internal void EndRestoreForTest(CancellationTokenSource owner)=>EndScreenshotRestore(owner);
        internal bool HasScreenshotOwner=>screenshotHotkeyOwner!=null;
        internal void ReopenForTest()=>ReopenResult();
        internal void HoverForTest(){externalDropHover=restoreDropBody=restoreDropPeek=true;dropHint.Visibility=System.Windows.Visibility.Visible;}
        internal bool DropIntentVisible=>externalDropHover||restoreDropBody||restoreDropPeek||dropHint.Visibility==System.Windows.Visibility.Visible;
        internal string? TranslationStatus=>translationStatus;
        internal string ConnectionText=>connectionStatus.Text;
    }
}
