using System.Windows;
using System.Windows.Interop;
using KuaiYi.Core;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private OverlayRawMouse? overlayMouse;
    private bool wpfPointer;private Task overlayInputStartup=Task.CompletedTask;
    private bool UsesRawOverlayInput=>!wpfPointer&&lifecycle.Gate.Snapshot.Mode==TriggerMode.Drop&&overlayMouse?.IsReady==true;
    private void SetupOverlayInput(HwndSource source)
    {
        if(!enableSelectionPlatform)return;
        // Retain the exact owner across failed startup/cleanup. A second registration is never substituted.
        overlayInputReady=()=>overlayMouse?.IsReady==true;
        overlayInputCleanup=async()=>
        {
            if(overlayMouse is not {} owner)return;List<Exception> errors=[];
            try{await owner.DisposeAsync();}catch(Exception error){errors.Add(error);}
            try{await overlayInputStartup;}catch(Exception error){errors.Add(error);}
            if(errors.Count>0)throw new AggregateException("Original overlay input lifetime unsettled.",errors);
            if(ReferenceEquals(overlayMouse,owner))overlayMouse=null;
        };
        try{overlayMouse=new(source,OverlayPhysicalInput,code=>{CancelPointer();lifecycle.Gate.Disable();RevokeTextDrop();Refresh();button.ToolTip="拖放浮钮输入不可用 · "+code;},OverlayAnchorContains);overlayMouse.Start();overlayInputStartup=ObserveOverlayInputStart(overlayMouse);}
        catch(Exception error){lifecycle.Gate.Disable();Report("拖放浮钮输入未能启动",error);}
    }
    private async Task ObserveOverlayInputStart(OverlayRawMouse owner)
    {await owner.Readiness;if(!exiting&&ReferenceEquals(overlayMouse,owner)&&owner.IsReady)RefreshTextDrop();}
    private bool OverlayAnchorContains(PixelPoint point)=>point.X>=anchor.Left&&point.X<anchor.Right&&point.Y>=anchor.Top&&point.Y<anchor.Bottom;
    private void OverlayPhysicalInput(ushort flags,PixelPoint point,bool hit)=>Pointer(()=>
    {
        if(!UsesRawOverlayInput||exiting||!geometryReady||!IsVisible||lifecycle.Gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked))return;
        if((flags&4)!=0&&hit&&OverlayAnchorContains(point)){CancelPointer();lifecycle.Gate.Disable();RevokeTextDrop();Refresh();return;}
        if((flags&1)!=0)
        {
            gestureDpi=MonitorTopology.Read().At(anchor).Dpi;
            if(gesture.Down(point,anchor,hit)!=OverlayPointerAction.Pressed)return;
            lifecycle.Gate.SetSuspension(Suspension.Moving,true);RefreshSelection();
            RevokeTextDrop();BeginOverlayMove(); // Original source/task owns cancellation and eventual cleanup; no early dispose.
        }
        if(!gesture.Active)return;
        if(gesture.Move(point,out var desired,gestureDpi.CeilingPixels(SystemParameters.MinimumHorizontalDragDistance),gestureDpi.CeilingPixels(SystemParameters.MinimumVerticalDragDistance),gestureDpi)==OverlayPointerAction.Moved)
        {positionTouched=true;MoveOverlay(desired,MonitorTopology.Read());}
        if((flags&2)==0)return;
        var action=gesture.Up();lifecycle.Gate.SetSuspension(Suspension.Moving,false);RefreshSelection();EndOverlayMove();
        if(action==OverlayPointerAction.Clicked)Toggle();
        else if(action==OverlayPointerAction.Released){pendingPosition=CurrentPosition();QueuePositionSave();}
    });
}
