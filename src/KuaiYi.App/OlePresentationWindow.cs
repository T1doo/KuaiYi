using System.IO;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using KuaiYi.Core;
using KuaiYi.OleTransfer;
using KuaiYi.Windows;
namespace KuaiYi.App;
/// <summary>One real App-owned floating HWND. No worker-owned visuals or hit proxy. P0 local delivery only.</summary>
internal sealed class OlePresentationWindow:Window
{
    private readonly RuntimeGate gate;private readonly Action<RequestIdentity,string> localText;private readonly Action<string> report;
    private readonly Border button=new(){Width=48,Height=48,CornerRadius=new CornerRadius(24),Background=Brushes.Firebrick,Child=new TextBlock{Text="译",Foreground=Brushes.White,FontSize=18,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
    private readonly TextBlock hint=new(){Text="松开复制 · 本地",FontSize=11,Visibility=Visibility.Collapsed,IsHitTestVisible=false};
    private readonly Canvas canvas=new();private readonly OverlayPointerGesture gesture=new();
    private readonly DispatcherTimer poll=new(){Interval=TimeSpan.FromMilliseconds(50)};
    private readonly Guid session=Guid.NewGuid();private nint hwnd;private PixelRect anchor;
    private OverlayRawMouse? raw;private OleReceiverClient? client;private Task? run,control;private long sentEpoch=-1;private bool sentEnabled,allowClose,hover,locked;
    private readonly object feedbackSync=new();private (OleReceiverClient Owner,OleReceiverEvent Event)? latest;
    private bool desiredHover;
    private bool geometryPending;
    private readonly OlePresentationConsent consent=new();
    private bool ownMoving,busyAtPress;
    private OleTargetRegistrar? registrationOwner;
    public bool LifetimeSettled=>client==null&&run is not {IsCompleted:false};
    public OlePresentationWindow(RuntimeGate gate,Action<RequestIdentity,string> localText,Action<string> report)
    {
        this.gate=gate;this.localText=localText;this.report=report;
        Title="快译 · P0 OLE 接线";Width=Height=48;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;ShowActivated=false;Topmost=true;Background=Brushes.White;
        Content=canvas;canvas.Children.Add(button);canvas.Children.Add(hint);Canvas.SetTop(hint,49);
        SourceInitialized+=(_,_)=>
        {
            try{hwnd=new WindowInteropHelper(this).Handle;NativeDesktop.SetOverlayStyle(hwnd,false);var p=ScreenshotDesktop.Cursor();Move(p.X+70,p.Y);var source=(HwndSource)PresentationSource.FromVisual(this);source.AddHook(GeometryEvent);raw=new(source,Input,code=>{gesture.Cancel();Stop(true);report("浮钮输入不可用 · "+code);});}
            catch(Exception error){Stop(true);report("浮钮初始化失败 · "+error.GetType().Name);}
        };
        Loaded+=(_,_)=>{poll.Tick+=Tick;poll.Start();if(raw?.IsReady==true)StartReceiver();};
        Closing+=async(_,e)=>
        {
            if(allowClose)return;e.Cancel=true;Stop(true);
            if(client is {} held&&await held.TryFinalizeAsync(true)&&ReferenceEquals(client,held))client=null;
            if(!LifetimeSettled){report("浮钮保留：接收进程/启动/注册撤销尚未确认；主呈现仍可操作。");return;}
            try{raw?.Dispose();raw=null;}catch(Exception error){report("浮钮输入撤销未确认 · "+error.GetType().Name);return;}
            allowClose=true;Close();
        };
        Closed+=(_,_)=>poll.Stop();
    }
    private void Input(ushort flags,PixelPoint point,bool actualHit)
    {
        if(gate.Snapshot.Suspension.HasFlag(Suspension.SessionLocked)){gesture.Cancel();return;}
        if((flags&4)!=0&&actualHit&&Contains(point)){gesture.Cancel();Stop(true);return;}
        if((flags&1)!=0&&gesture.Down(point,anchor,actualHit)==OverlayPointerAction.Pressed)
        {
            busyAtPress=client?.IsBusy==true;ownMoving=true;gate.SetSuspension(Suspension.Moving,true);
            if(client is {} held&&held.IsReady)control=held.SetEnabledAsync(gate.Snapshot.Epoch,false); // Shared Off store happens before the first await.
        }
        if(gesture.Move(point,out var desired)==OverlayPointerAction.Moved)Move(desired.X,desired.Y);
        if((flags&2)==0)return;var action=gesture.Up();FinishMovement();
        if(action==OverlayPointerAction.Clicked)
        {
            if(busyAtPress||client?.IsBusy==true){Stop(false);return;}
            if(client?.IsReady!=true){_ = Restart();return;}
            if(consent.IsGranted&&gate.Snapshot.Activation==Activation.On)Stop(true);
            else{report(consent.TryEnable(gate)?"On · 仅接收本次本地拖放；不发送模型。":"Off · 当前工作流暂停采集，尚未授予拖放权限。");}
        }
    }
    private void FinishMovement(){if(ownMoving){ownMoving=false;gate.SetSuspension(Suspension.Moving,false);}}
    private bool Contains(PixelPoint point)=>point.X>=anchor.Left&&point.X<anchor.Right&&point.Y>=anchor.Top&&point.Y<anchor.Bottom;
    private nint GeometryEvent(nint handle,int message,nint wParam,nint lParam,ref bool handled)
    {
        // Let WPF process DPI first; preserve the physical anchor and recompute against current work area afterward.
        if(message is 0x02e0 or 0x007e or 0x001a&&!geometryPending)
        {
            geometryPending=true;
            if(message!=0x02e0||!ownMoving){gesture.Cancel();Stop(true);} // A DPI transition during our own gesture stays movable; its acquisition is already suspended.
            Dispatcher.BeginInvoke(DispatcherPriority.Background,()=>{geometryPending=false;if(!allowClose)Move(anchor.Left,anchor.Top);});
        }
        return 0;
    }
    private void Move(int x,int y)
    {
        var monitor=NativeDesktop.MonitorAt(new(x,y));int size=monitor.Dpi.CeilingPixels(48);anchor=NativeDesktop.ClampAnchor(new(x,y,x+size,y+size),monitor);Layout();
    }
    private void Layout()
    {
        var monitor=NativeDesktop.MonitorAt(anchor.Center);var work=monitor.WorkArea.Inset(monitor.Dpi.CeilingPixels(12));
        int width=monitor.Dpi.CeilingPixels(hover?96:48),height=monitor.Dpi.CeilingPixels(hover?64:48);
        int x=Math.Clamp(anchor.Left,work.Left,Math.Max(work.Left,work.Right-width)),y=Math.Clamp(anchor.Top,work.Top,Math.Max(work.Top,work.Bottom-height));
        NativeDesktop.ApplyBounds(hwnd,new(x,y,x+width,y+height),monitor.Dpi.CeilingPixels(12));Canvas.SetLeft(button,monitor.Dpi.Dips(anchor.Left-x));Canvas.SetTop(button,monitor.Dpi.Dips(anchor.Top-y));hint.Visibility=hover?Visibility.Visible:Visibility.Collapsed;
    }
    private void StartReceiver()
    {
        if(client!=null||run is {IsCompleted:false}||raw?.IsReady!=true)return;
        gate.Disable();gate.SetMode(TriggerMode.Drop);consent.Revoke();sentEpoch=-1;hover=false;Layout();
        lock(feedbackSync){desiredHover=false;latest=null;}
        OleSharedActivation? activation=null;
        try
        {
            Guid instance=Guid.NewGuid();activation=new OleSharedActivation(instance);
            registrationOwner=new();var owner=new OleReceiverClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.OleReceiver.exe"),registrationOwner,activation);client=owner;
            var hello=new OleReceiverHello(instance,gate.Snapshot.Epoch,Environment.ProcessId,hwnd.ToInt64(),RandomNumberGenerator.GetBytes(32),activation.Name);
            run=Run(owner,hello);
        }
        catch(Exception error){activation?.Dispose();client=null;report("OLE 启动不可用 · "+error.GetType().Name);}
    }
    private async Task Run(OleReceiverClient owner,OleReceiverHello hello)
    {
        try
        {
            await owner.RunAsync(hello,item=>{lock(feedbackSync){if(item.Phase=="Hover")desiredHover=true;if(item.Phase is "Leave" or "DropBegin" or "DropEnd")desiredHover=false;latest=(owner,item);}},(epoch,transaction,bytes,token)=>Dispatcher.InvokeAsync(()=>
            {
                owner.TryApplyLocal(epoch,()=>
                {
                    var current=gate.Snapshot;
                    if(!ReferenceEquals(client,owner)||token.IsCancellationRequested||!consent.Allows(gate,IsVisible)||current.Activation!=Activation.On||current.Suspension!=Suspension.None||current.Mode!=TriggerMode.Drop||epoch!=current.Epoch)return;
                    string text=OleReceiverWire.DecodeText(bytes.Span);
                    if(gate.TryStartConfirmedLocalRequest(epoch,session,transaction,out var identity,out _))gate.TryApply(identity,()=>localText(identity,text));
                });
            },DispatcherPriority.Background,token).Task);
        }
        catch(OperationCanceledException){/* Explicit cancel/Off; prior result is not replaced. */}
        catch(Exception error){if(ReferenceEquals(client,owner)){consent.Revoke();gate.Disable();report("Off · OLE 接收未完成 · "+(error is OleReceiverException typed?typed.Failure:error.GetType().Name));}}
        finally{CryptographicOperations.ZeroMemory(hello.Nonce);if(await owner.TryFinalizeAsync()&&ReferenceEquals(client,owner))client=null;}
    }
    private void Stop(bool off)
    {
        gesture.Cancel();FinishMovement();consent.Revoke();if(off)gate.Disable();client?.Cancel();hover=false;if(hwnd!=0)Layout();report(off?"Off · 已撤销拖放并请求确认接收进程退出。":"已取消本次拖放；旧本地结果保留。重新启动接收需明确点击。 ");
    }
    public void RevokeFromApp(){consent.Revoke();client?.Cancel();} // Thread-safe activation write; no WPF from session-lock thread.
    private async Task Restart()
    {
        var held=client;if(run is {IsCompleted:false})return;
        if(held!=null){if(!await held.TryFinalizeAsync(true)){report("接收进程或注册撤销尚未确认，禁止重启；浮钮可继续移动/Off。");return;}if(ReferenceEquals(client,held))client=null;}
        StartReceiver();
    }
    private void Tick(object? sender,EventArgs args)
    {
        var snapshot=gate.Snapshot;consent.Allows(gate,IsVisible);button.Background=consent.IsGranted?Brushes.ForestGreen:Brushes.Firebrick;
        if(snapshot.Suspension.HasFlag(Suspension.SessionLocked)){if(!locked){locked=true;gesture.Cancel();Stop(true);Hide();}return;}
        if(locked){locked=false;Show();}
        (OleReceiverClient Owner,OleReceiverEvent Event)? update;bool requestedHover;lock(feedbackSync){update=latest;latest=null;requestedHover=desiredHover;}
        if(update is {} next&&ReferenceEquals(next.Owner,client)&&next.Event.Epoch==snapshot.Epoch)
        {
            bool before=hover;hover=requestedHover&&next.Owner.IsReady&&consent.IsGranted&&snapshot.Activation==Activation.On&&snapshot.Suspension==Suspension.None;if(before!=hover)Layout();
            if(next.Event.Phase is "Ready" or "QueryBegin" or "QueryEnd" or "DropBegin" or "DropEnd")
            {
                uint uiThread=NativeDesktop.GetWindowThreadProcessId(hwnd,out uint pid);
                report($"OLE · {next.Event.Phase} · App PID {pid} / HWND线程 {uiThread} / registrar线程 {registrationOwner?.NativeRegistrationThreadId} / receiver线程 {next.Event.NativeThreadId} {next.Event.Apartment} · 本地，未调用模型。");
            }
        }
        bool enabled=consent.Allows(gate,IsVisible);
        if(client is {} ready&&ready.IsReady&&control is not {IsCompleted:false}&&(snapshot.Epoch!=sentEpoch||enabled!=sentEnabled))
        {
            sentEpoch=snapshot.Epoch;sentEnabled=enabled;control=ready.SetEnabledAsync(sentEpoch,sentEnabled);
        }
    }
}
