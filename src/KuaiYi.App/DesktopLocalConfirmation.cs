using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using KuaiYi.NativeHost;
namespace KuaiYi.App;

/// <summary>Explicit desktop UI: no persisted decision, default accept or browser-configurable answer.</summary>
internal sealed class DesktopLocalConfirmation(Window owner,Func<bool> available):ILocalImageConfirmation
{
    public async Task<bool> ConfirmAsync(LocalImageConfirmation request,CancellationToken cancellation)
    {
        var completion=new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        Window? prompt=null;
        using var registration=cancellation.Register(()=>
        {
            completion.TrySetResult(false);
            if(!owner.Dispatcher.HasShutdownStarted)owner.Dispatcher.BeginInvoke(()=>prompt?.Close());
        });
        await owner.Dispatcher.InvokeAsync(()=>
        {
            if(cancellation.IsCancellationRequested||!available()||!owner.IsVisible){completion.TrySetResult(false);return;}
            var panel=new StackPanel{Margin=new Thickness(16)};
            panel.Children.Add(new TextBlock
            {
                Text=$"一个已核验的本地快译宿主（进程 {request.ClientProcessId}）请求接收一张图片。\n扩展声明：{request.DeclaredOrigin}\n此声明不能证明浏览器来源。仅在您刚刚主动操作时允许。\n允许后只接收本次受支持图片到内存（最多 20 MiB），随后只在本机准备预览，不自动识别文字或发送模型。关闭、超时或停用会取消。",
                TextWrapping=TextWrapping.Wrap
            });
            var allow=new Button{Content="仅允许本次本地接收",Margin=new Thickness(0,16,0,6),IsDefault=false};
            var reject=new Button{Content="拒绝",IsCancel=true,IsDefault=true};
            panel.Children.Add(allow);panel.Children.Add(reject);
            prompt=new Window{Owner=owner,Title="快译 · 确认本次本地接收",Width=470,SizeToContent=SizeToContent.Height,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=panel,ResizeMode=ResizeMode.NoResize};
            allow.Click+=(_,_)=>{completion.TrySetResult(!cancellation.IsCancellationRequested&&available());prompt.Close();};
            reject.Click+=(_,_)=>{completion.TrySetResult(false);prompt.Close();};
            prompt.Closed+=(_,_)=>completion.TrySetResult(false);
            prompt.Show();reject.Focus();
        },DispatcherPriority.Normal);
        try{return await completion.Task.ConfigureAwait(false);}
        finally{await owner.Dispatcher.InvokeAsync(()=>prompt?.Close(),DispatcherPriority.Normal);}
    }
}
