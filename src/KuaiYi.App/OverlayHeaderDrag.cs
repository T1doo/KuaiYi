using System.Windows;
using System.Windows.Media;
using KuaiYi.Core;
using KuaiYi.Windows;

namespace KuaiYi.App;

internal sealed partial class ShellWindow
{
    private void SetupHeaderDrag()
    {
        controls.Background=Brushes.Transparent;
        controls.PreviewMouseLeftButtonDown+=(_,e)=>Pointer(()=>
        {
            // Only the canvas's empty surface. Buttons and their template children
            // keep their normal click semantics; the reader is a separate sibling.
            if(!ReferenceEquals(e.OriginalSource,controls)||exiting||!geometryReady||gesture.Active||
                lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked)||
                !NativeDesktop.GetCursorPos(out var cursor))return;
            var topLeft=controls.PointToScreen(new Point(0,0));
            var bottomRight=controls.PointToScreen(new Point(controls.ActualWidth,controls.ActualHeight));
            var surface=new PixelRect((int)Math.Floor(topLeft.X),(int)Math.Floor(topLeft.Y),
                (int)Math.Ceiling(bottomRight.X),(int)Math.Ceiling(bottomRight.Y));
            gestureDpi=MonitorTopology.Read().At(anchor).Dpi;
            if(gesture.DownSurface(new(cursor.X,cursor.Y),anchor,surface,true)!=OverlayPointerAction.Pressed)return;
            wpfPointer=true;pointerFinished=false;e.Handled=true;
            lifecycle.Gate.SetSuspension(Suspension.Moving,true);RefreshSelection();RevokeTextDrop();BeginOverlayMove();
            // Reuse the anchor's captured move/up/lost-capture and cancellation path.
            if(!button.CaptureMouse())throw new InvalidOperationException("Header pointer capture failed.");
        });
    }
}
