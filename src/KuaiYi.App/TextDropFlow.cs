using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using KuaiYi.Windows;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private sealed class TextDropPeer(OleReceiverClient client,DropCaptureLease lease,CancellationTokenSource cancellation,string[] blocked)
    {public OleReceiverClient Client{get;}=client;public DropCaptureLease Lease{get;}=lease;public CancellationTokenSource Cancellation{get;}=cancellation;public string[] Blocked{get;}=blocked;public Task Run=Task.CompletedTask,Control=Task.CompletedTask;public DropTargetGeometry? SentTarget;public int Stopping;}
    private string? dropUnavailable;private DropCaptureCoordinator? dropCapture;private TextDropPeer? dropPeer;private Task dropReconcile=Task.CompletedTask;private bool dropVisible,externalDropHover,restoreDropBody,restoreDropPeek;private DropTargetGeometry? dropBounds;
    private readonly DispatcherTimer dropPoll=new(){Interval=TimeSpan.FromMilliseconds(100)};
    private readonly TextBlock dropHint=new(){Text="松开\n翻译",FontSize=11,TextAlignment=TextAlignment.Center,IsHitTestVisible=false,Visibility=Visibility.Collapsed};
    private void SetupTextDrop()
    {
#if KUAIYI_WINDOWS_CI_PROBE
        if(!enableSelectionPlatform&&!CiTextDropEnabled)return;
#else
        if(!enableSelectionPlatform)return; // Public growth probe remains isolated from every real selection/Drop backend.
#endif
        overlayCanvas.Children.Add(dropHint);
        dropCapture=new(lifecycle.Gate,()=>lifecycle.Settings,SelectionDesktop.IsDropSourceCurrent,()=>Volatile.Read(ref dropVisible),(uint)Environment.ProcessId,new DispatcherSynchronizationContext(Dispatcher),AcceptSelection,status=>button.ToolTip="拖放未取得可信文本，旧来源保留 · "+status);
        dropPoll.Tick+=(_,_)=>RefreshTextDrop();dropPoll.Start();PublishDropBounds();
    }
    private void RevokeTextDrop(bool clearSource=false)
    {
        dropCapture?.Invalidate(clearSource);var peer=Volatile.Read(ref dropPeer);
        if(peer!=null&&Interlocked.Exchange(ref peer.Stopping,1)==0)
        {
#if KUAIYI_WINDOWS_CI_PROBE
            CiReceiverRevoking(peer.Client,!peer.Cancellation.IsCancellationRequested);
#endif
            peer.Client.Cancel();peer.Cancellation.Cancel();
        }
    }
    private async Task PublishDropControl(TextDropPeer peer,Task previous,DropTargetGeometry target)
    {
        try{await previous;if(ReferenceEquals(dropPeer,peer)&&Volatile.Read(ref peer.Stopping)==0&&dropCapture?.Lease==peer.Lease&&lifecycle.Gate.IsCaptureCurrent(peer.Lease.Ticket)&&lifecycle.Settings?.Version==peer.Lease.SettingsVersion)
        {
            var originalControl=peer.Client.SetEnabledAsync(peer.Lease.WireEpoch,true,target,peer.Blocked);
#if KUAIYI_WINDOWS_CI_PROBE
            CiTrackOriginal(peer.Client,originalControl,false);
#endif
            await originalControl;
        }}
        catch(Exception error){if(ReferenceEquals(dropPeer,peer)&&Volatile.Read(ref peer.Stopping)==0){RevokeTextDrop();Report("拖放门控发布未完成",error);}else peer.Client.Cancel();}
    }
    private async Task ReconcileTextDrop()
    {
        if(dropPeer is {} old)
        {
            if(Volatile.Read(ref old.Stopping)==0)return;
            try{await old.Run.WaitAsync(TimeSpan.FromSeconds(3));}catch(TimeoutException){button.ToolTip="拖放退出尚未结算，暂不建立第二接收器。";return;}
            await old.Control;
            if(!await old.Client.TryFinalizeAsync(true)){button.ToolTip="拖放进程、注册或交付清理尚未确认。";return;}
            if(ReferenceEquals(dropPeer,old)){dropPeer=null;old.Cancellation.Dispose();}
        }
        var state=lifecycle.Gate.Snapshot;
        if(dropStopping||exiting||!dropVisible||state.Activation!=Activation.On||state.Mode!=TriggerMode.Drop||state.Suspension!=Suspension.None||dropBounds is not {} target||lifecycle.Settings is not {} config)return;
        OleSharedActivation? activation=null;
        try
        {
            if(!File.Exists(Path.Combine(AppContext.BaseDirectory,"KuaiYi.OleNativeBridge.dll")))throw new FileNotFoundException("Native OLE component has not been built/installed.");
            Guid instance=Guid.NewGuid();var lease=dropCapture!.Open(instance,state.Epoch,target);if(lease==null)return;
            activation=new(instance);var registrar=CreateDropRegistrar();var client=new OleReceiverClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.OleReceiver.exe"),registrar,activation);var cancellation=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            var peer=new TextDropPeer(client,lease,cancellation,config.Data.Privacy.BlockedApplications.ToArray());dropPeer=peer;activation=null;
#if KUAIYI_WINDOWS_CI_PROBE
            CiReceiverCreated(client,registrar,instance,()=>peer.Run,()=>peer.Control);
#endif
            var hello=new OleReceiverHello(instance,lease.WireEpoch,Environment.ProcessId,hwnd.ToInt64(),RandomNumberGenerator.GetBytes(32),OleReceiverWire.ActivationName(instance),ProductionTextDrop:true,AllowImageFiles:imageFileDropEnabled);
            peer.Run=RunTextDrop(peer,hello);
        }
        catch(Exception error){activation?.Dispose();RevokeTextDrop();lifecycle.Gate.Disable();SetDropHover(false);dropUnavailable="正式文本拖放尚不可用 · "+error.GetType().Name;Report("正式文本拖放尚不可用",error);}
    }
    private async Task RunTextDrop(TextDropPeer peer,OleReceiverHello hello)
    {
        try
        {
            var originalRun=peer.Client.RunAsync(hello,item=>
            {
#if KUAIYI_WINDOWS_CI_PROBE
                CiReceiverFeedback(item,peer.Client);
#endif
                if(!ReferenceEquals(Volatile.Read(ref dropPeer),peer)||Volatile.Read(ref peer.Stopping)!=0||dropCapture?.Lease!=peer.Lease||item.Instance!=peer.Lease.Receiver||item.Epoch!=peer.Lease.WireEpoch)return;
                if(item.Phase=="QueryEnd"&&item.Effect==1&&item.Source is {} origin&&item.Point is {} point)dropCapture.Enter(peer.Lease,item.Transaction,origin,point,OleEffect.Copy);
                else if(item.Phase=="DropBegin"&&item.Source is {} source&&item.Point is {} dropped)dropCapture.Begin(peer.Lease,item.Transaction,source,dropped,(OleEffect)item.Effect);
                else if(item.Phase=="Leave")dropCapture.Leave(peer.Lease,item.Transaction);
                Dispatcher.BeginInvoke(()=>
                {
                    if(!ReferenceEquals(dropPeer,peer)||Volatile.Read(ref peer.Stopping)!=0||dropCapture?.Lease!=peer.Lease||!lifecycle.Gate.IsCaptureCurrent(peer.Lease.Ticket))return;
                    ApplyDropFeedback(item.Phase,item.Effect,item.TextFormat);
                    RefreshTextDrop();
                });
            },(epoch,transaction,bytes,token)=>dropCapture!.ReceiveAsync(peer.Lease,transaction,bytes,action=>peer.Client.TryApplyDrop(epoch,transaction,action),token,()=>peer.Client.IsDropCurrent(epoch,transaction)),peer.Cancellation.Token,deliverImageFile:(epoch,transaction,bytes,token)=>dropCapture!.ReceiveImageFileAsync(peer.Lease,transaction,bytes,action=>peer.Client.TryApplyDrop(epoch,transaction,action),path=>{_=StartDroppedImageFile(path,CreateFileImageOperation);},token,()=>peer.Client.IsDropCurrent(epoch,transaction)));
#if KUAIYI_WINDOWS_CI_PROBE
            CiTrackOriginal(peer.Client,originalRun,true);
#endif
            await originalRun.ConfigureAwait(false);
        }
        catch(OperationCanceledException)when(peer.Cancellation.IsCancellationRequested){ }
        catch(Exception error)
        {
            await Dispatcher.InvokeAsync(()=>
            {
                if(ReferenceEquals(dropPeer,peer)&&Volatile.Read(ref peer.Stopping)==0&&lifecycle.Gate.IsCaptureCurrent(peer.Lease.Ticket)&&lifecycle.Settings?.Version==peer.Lease.SettingsVersion){lifecycle.Gate.Disable();RevokeTextDrop();SetDropHover(false);Refresh();Report("拖放接收失败，旧来源保留",error);}
            });
        }
        finally{CryptographicOperations.ZeroMemory(hello.Nonce);Interlocked.Exchange(ref peer.Stopping,1);peer.Client.Cancel();}
    }
    private void SetDropHover(bool show)
    {
        if(show==externalDropHover)return;
        if(show){restoreDropBody=overlayFrames?.IsExpanded==true&&!peek;restoreDropPeek=peek;externalDropHover=true;peekIn.Stop();peekOut.Stop();peek=true;overlayFrames?.OpenToolbar(96);}
        else{externalDropHover=false;dropHint.Visibility=Visibility.Collapsed;peek=restoreDropPeek;if(restoreDropBody){peek=false;OpenBody();}else if(restoreDropPeek)overlayFrames?.OpenToolbar();else overlayFrames?.Collapse();}
        StartFrames();ApplyOverlayFrame();
    }
    private void PublishDropBounds()
    {
        if(dropCapture==null||overlayFrames==null||anchorMonitor==null)return;
        var bounds=anchor;int radius=anchorMonitor.Dpi.CeilingPixels(24);
        if(externalDropHover&&overlayFrames.Plan is {} plan)
        {
            var current=overlayFrames.Current;var final=plan.Target;bounds=new(Math.Max(current.Left,final.Left),Math.Max(current.Top,final.Top),Math.Min(current.Right,final.Right),Math.Min(current.Bottom,final.Bottom));
            double expansion=Math.Clamp((bounds.Width-anchor.Width+ bounds.Height-anchor.Height)/(48*anchorMonitor.Dpi.Factor),0,1);radius=anchorMonitor.Dpi.CeilingPixels(24-8*expansion);
        }
        dropBounds=new(anchor,bounds,Math.Min(radius,Math.Min(bounds.Width,bounds.Height)/2));dropCapture.UpdateGeometry(dropBounds);
        if(externalDropHover)
        {
            var rect=overlayFrames.Current;var dpi=anchorMonitor.Dpi;bool left=anchor.Left-bounds.Left>bounds.Right-anchor.Right;
            Canvas.SetLeft(dropHint,dpi.Dips((left?bounds.Left:anchor.Right)-rect.Left));Canvas.SetTop(dropHint,dpi.Dips(anchor.Top-rect.Top+8));dropHint.Width=Math.Max(0,dpi.Dips(bounds.Width-anchor.Width));dropHint.Height=32;dropHint.Foreground=body.Foreground;dropHint.Visibility=dropHint.Width>=20?Visibility.Visible:Visibility.Collapsed;
        }
        RefreshTextDrop();
    }
}
