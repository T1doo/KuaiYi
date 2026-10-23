using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KuaiYi.App;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using System.Windows.Controls.Primitives;

namespace KuaiYi.GrowthHarness;

// Only the test executable enters here. No copied UI, provider, native worker or registration.
internal static class SettingsLayoutProbe
{
    public static int Run(string output)
    {
        output=Path.GetFullPath(output);
        if(!Directory.Exists(output))throw new IOException("Original output directory required.");
        for(DirectoryInfo? p=new(output);p!=null;p=p.Parent)
            if((p.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked output rejected.");
        string state=Path.Combine(output,"state-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(state);
        var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var owner=new ProbeOwner(app,output,state);
        var previous=SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(app.Dispatcher));
        try
        {
            // Save the original asynchronous operation before allowing any window work to start.
            var start=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Task original=owner.Drive(start.Task);
            app.Dispatcher.BeginInvoke(()=>start.SetResult());
            app.Run();
            if(!original.IsCompleted)throw new InvalidOperationException("Dispatcher ended before original driver settled; state retained.");
            original.GetAwaiter().GetResult();
            owner.ShutdownOperation!.Task.GetAwaiter().GetResult();
            return owner.Succeeded?0:3;
        }
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
    }

    private sealed class ProbeOwner(Application app,string output,string state)
    {
        private readonly List<string> errors=[];
        private readonly List<object> scenes=[];
        private readonly Stopwatch elapsed=Stopwatch.StartNew();
        private readonly CancellationTokenSource cancellation=new();
        private SettingsWindow? window;
        private SettingsDraft? draft;
        private TaskCompletionSource? closed;
        private long pngBytes;
        private bool timedOut,cleanupConfirmed,constructionUnconfirmed;
        public bool Succeeded{get;private set;}
        public DispatcherOperation? ShutdownOperation{get;private set;}
        public async Task Drive(Task start)
        {
            await start;
            var deadline=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(100)};
            deadline.Tick+=(_,_)=>
            {
                if(elapsed.Elapsed<TimeSpan.FromSeconds(45))return;
                deadline.Stop();timedOut=true;
                try{cancellation.Cancel();}catch(Exception error){Record("Cancel",error);}
                try{window?.RequestCloseWhenSettled();}catch(Exception error){Record("DeadlineClose",error);}
            };
            deadline.Start();
            try
            {
                await Open(false);
                var navigation=Descendants<ListBox>(window!).Single(p=>AutomationProperties.GetName(p)=="设置分类");
                navigation.SelectedIndex=3;
                await Scene("01-wide",940,700,13,false);
                var originalInputs=Descendants<PreferencePanel>(window!).ToArray();
                await Scene("02-narrow",420,680,13,true);
                await Scene("03-large-font",620,680,26,true);
                window!.Width=Math.Min(940,SystemParameters.WorkArea.Width);window.FontSize=13;
                await Settle();
                Require(ReferenceEquals(navigation,Descendants<ListBox>(window).Single(p=>AutomationProperties.GetName(p)=="设置分类"))&&navigation.SelectedIndex==3,"Navigation identity/selection changed");
                Require(originalInputs.SequenceEqual(Descendants<PreferencePanel>(window)),"Preference controls replaced on resize");
                await CloseOriginal();
                await Open(true);
                await Scene("04-short-recovery",620,360,13,true,recovery:true);
            }
            catch(Exception error){Record("Driver",error);}
            finally
            {
                deadline.Stop();
                // Close/Closed and the original draft are observed independently of body failure.
                try{await CloseOriginal();}catch(Exception error){Record("Close",error);}
                if(window==null&&!constructionUnconfirmed)
                {
                    try{draft?.Dispose();draft=null;}catch(Exception error){Record("Draft",error);}
                    if(draft==null)
                    {
                        try{Directory.Delete(state,true);cleanupConfirmed=true;}catch(Exception error){Record("StateCleanup",error);}
                    }
                }
                if(window==null&&draft==null&&!constructionUnconfirmed)cancellation.Dispose();
                Succeeded=errors.Count==0&&!timedOut&&cleanupConfirmed&&scenes.Count==4;
                try
                {
                    WriteJson("settings-result.json",new
                    {
                        Kind="ProductionSettingsWpfRaster",Status=Succeeded?"Pass_LimitedRaster":"Fail",SourceHead=Environment.GetEnvironmentVariable("GITHUB_SHA"),
                        ElapsedMilliseconds=elapsed.Elapsed.TotalMilliseconds,TimedOut=timedOut,CleanupConfirmed=cleanupConfirmed,
                        Scenes=scenes,Errors=errors,ModelCalls=0,ProductionPins="Unavailable",
                        Windows=new{Environment.OSVersion.VersionString,Environment.UserInteractive,SessionId=Process.GetCurrentProcess().SessionId},
                        Scope="Actual production SettingsWindow content raster; synthetic forced viewport and FontSize parameter",
                        NotEvaluated=new[]{"DWM presentation", "Popup HWND", "System 200% text/DPI", "Physical keyboard", "Animation fluidity", "Full product/installation"}
                    });
                }
                catch(Exception error){Succeeded=false;Console.Error.WriteLine("Settings result write failed: "+error);}
                // Unknown original cleanup keeps this owner and the dispatcher alive. The external
                // 60s process bound reports failure; no second shutdown pretends the close succeeded.
                if(!cleanupConfirmed)await new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously).Task;
                // Queue shutdown after this original Task returns, never on last-window-close.
                ShutdownOperation=app.Dispatcher.BeginInvoke(()=>app.Shutdown(Succeeded?0:3));
            }
        }
        private void Check(){cancellation.Token.ThrowIfCancellationRequested();if(elapsed.Elapsed>=TimeSpan.FromSeconds(45))throw new TimeoutException("Original 45 second UI budget exceeded.");}
        private void Record(string stage,Exception error){errors.Add(stage+": "+error);}
        private static void Require(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private async Task Open(bool recovery)
        {
            Check();string root=Path.Combine(state,recovery?"recovery":"normal");Directory.CreateDirectory(root);
            var store=new SettingsStore(Path.Combine(root,"config"));
            if(recovery){Directory.CreateDirectory(store.DirectoryPath);File.WriteAllText(store.PrimaryPath,"{synthetic-malformed-settings");}
            var coordinator=new SettingsCoordinator(store,new SecretFileStore(Path.Combine(root,"secrets"),new NoKeys(),new DirectoryPolicy()));
            draft=await coordinator.OpenDraftAsync(cancellation.Token);Check();
            Require(draft.Basis.State==(recovery?SettingsLoadState.RecoverableDefaults:SettingsLoadState.Defaults),"Real settings load state differs");
            // A draft-only ordinary preference; never save, grant permission, enable or send.
            draft.Update(s=>s with{Overlay=s.Overlay with{Theme=ThemePreference.Light}});
            constructionUnconfirmed=true;
            window=new SettingsWindow(coordinator,draft);
            constructionUnconfirmed=false;
            var originalClosed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);closed=originalClosed;
            window.Closed+=(_,_)=>originalClosed.SetResult();
            window.Show();await Settle();
            // Wait past production Loaded placement before forcing a synthetic viewport.
            await app.Dispatcher.InvokeAsync(()=>{},DispatcherPriority.ApplicationIdle);Check();
        }
        private async Task CloseOriginal()
        {
            if(window==null)return;
            var originalWindow=window;var originalClosed=closed!.Task;
            if(!originalClosed.IsCompleted)originalWindow.RequestCloseWhenSettled();
            await originalClosed; // No cancellation shortcut around the real Closed observation.
            draft?.Dispose();draft=null;window=null;closed=null;
        }
        private async Task Settle()
        {
            Check();await app.Dispatcher.InvokeAsync(()=>window!.UpdateLayout(),DispatcherPriority.ApplicationIdle);
            var rendered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler handler=(_,_)=>rendered.TrySetResult();
            CompositionTarget.Rendering+=handler;
            try{await rendered.Task.WaitAsync(cancellation.Token);}finally{CompositionTarget.Rendering-=handler;}
            Check();Require(window!.IsLoaded&&window.IsVisible&&window.IsMeasureValid&&window.IsArrangeValid,"Window layout not settled");
        }
        private async Task Scene(string name,double width,double height,double font,bool compact,bool recovery=false)
        {
            Check();window!.MinWidth=1;window.MinHeight=1;window.Width=Math.Min(width,SystemParameters.WorkArea.Width);window.Height=Math.Min(height,SystemParameters.WorkArea.Height);window.FontSize=font;
            await Settle();
            var chrome=Descendants<SettingsChromePanel>(window).Single();
            Require(chrome.IsCompact==compact,"Unexpected production compact mode");
            var navigation=Descendants<ListBox>(window).Single(p=>AutomationProperties.GetName(p)=="设置分类");
            Require(navigation.ItemContainerGenerator.ContainerFromItem(navigation.SelectedItem) is ListBoxItem,"Selected navigation container missing");
            var selected=(ListBoxItem)navigation.ItemContainerGenerator.ContainerFromItem(navigation.SelectedItem);
            // Observe the production auto-reveal before any fixture BringIntoView call.
            var selectedVisibility=await Reach(selected,"SelectedNavigation",bring:false);
            var icon=Descendants<System.Windows.Shapes.Path>(selected).Single();
            var label=Descendants<TextBlock>(selected).Single();
            Rect iconBounds=icon.TransformToAncestor(selected).TransformBounds(new Rect(icon.RenderSize));
            Rect labelBounds=label.TransformToAncestor(selected).TransformBounds(new Rect(label.RenderSize));
            double alignmentError=Math.Abs(iconBounds.Top+iconBounds.Height/2-labelBounds.Top-labelBounds.Height/2);
            Require(alignmentError<=1,"Navigation icon and label centers differ");
            double navigationHeight=chrome.Children[0].RenderSize.Height;
            double navigationDesiredHeight=chrome.Children[0].DesiredSize.Height;
            if(compact)Require(Math.Abs(navigationHeight-Math.Min(navigationDesiredHeight,chrome.ActualHeight*.24))<=1,"Compact navigation differs from measured content/cap");
            var footer=(ScrollViewer)chrome.Children[2];
            if(recovery){footer.ScrollToEnd();await Settle();}
            var source=(FrameworkElement)window.Content;
            var dpi=VisualTreeHelper.GetDpi(source);int pixelsX=checked((int)Math.Ceiling(source.ActualWidth*dpi.DpiScaleX)),pixelsY=checked((int)Math.Ceiling(source.ActualHeight*dpi.DpiScaleY));
            Require(pixelsX>0&&pixelsY>0&&(long)pixelsX*pixelsY<=4_000_000,"Raster dimensions out of bound");
            var rows=Descendants<PreferencePanel>(window).Where(p=>p.IsVisible).Select(p=>new{Width=p.ActualWidth,Height=p.ActualHeight,InputWidth=((FrameworkElement)p.Children[1]).ActualWidth}).ToArray();
            Require(rows.All(p=>p.Width>0&&p.Height>0&&p.InputWidth>0&&p.InputWidth<=p.Width+.1),"Preference input layout invalid");
            var reachable=new List<object>();
            foreach(var row in Descendants<PreferencePanel>(window).Where(p=>p.IsVisible).ToArray())
                reachable.Add(await Reach((FrameworkElement)row.Children[1],"PreferenceInput"));
            var buttons=Descendants<Button>(footer).Where(p=>AutomationProperties.GetName(p) is "保存设置更改" or "取消设置更改").ToArray();
            Require(buttons.Length==2&&buttons.All(p=>p.IsVisible&&p.ActualWidth>0&&p.ActualHeight>0),"Original footer controls missing");
            foreach(var button in buttons)reachable.Add(await Reach(button,AutomationProperties.GetName(button)));
            if(recovery)reachable.Add(await Reach(Descendants<ComboBox>(footer).Single(),"RecoverySelector"));
            ((ScrollViewer)chrome.Children[1]).ScrollToTop();await Settle();
            var bitmap=new RenderTargetBitmap(pixelsX,pixelsY,96*dpi.DpiScaleX,96*dpi.DpiScaleY,PixelFormats.Pbgra32);bitmap.Render(source);bitmap.Freeze();
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var memory=new MemoryStream();encoder.Save(memory);Check();
            Require(memory.Length<=768*1024&&pngBytes+memory.Length<=2*1024*1024,"PNG evidence exceeds bound");
            string file=name+".png";byte[] bytes=memory.ToArray();
            using(var stream=new FileStream(Path.Combine(output,file),FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(bytes);stream.Flush(true);}
            pngBytes+=bytes.Length;
            scenes.Add(new{Name=name,File=file,Bytes=bytes.Length,Sha256=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),PixelsX=pixelsX,PixelsY=pixelsY,
                DpiX=96*dpi.DpiScaleX,DpiY=96*dpi.DpiScaleY,RequestedWidth=width,RequestedHeight=height,WindowWidth=window.ActualWidth,WindowHeight=window.ActualHeight,
                ClientWidth=source.ActualWidth,ClientHeight=source.ActualHeight,FontSize=window.FontSize,Compact=chrome.IsCompact,SyntheticForcedViewport=true,Recovery=recovery,
                FooterViewport=footer.ViewportHeight,FooterExtent=footer.ExtentHeight,Rows=rows,Reachability=reachable,
                Navigation=new{SelectedVisibility=selectedVisibility,Height=navigationHeight,DesiredHeight=navigationDesiredHeight,AlignmentError=alignmentError},ElapsedMilliseconds=elapsed.Elapsed.TotalMilliseconds});
        }
        private async Task<object> Reach(FrameworkElement target,string name,bool bring=true)
        {
            if(bring)target.BringIntoView();await Settle();
            var root=(FrameworkElement)window!.Content;
            Rect bounds=target.TransformToAncestor(root).TransformBounds(new Rect(target.RenderSize));
            Rect visible=bounds;visible.Intersect(new Rect(root.RenderSize));
            for(DependencyObject? parent=VisualTreeHelper.GetParent(target);parent!=null&&parent!=root;parent=VisualTreeHelper.GetParent(parent))
                if(parent is ScrollContentPresenter viewport)
                    visible.Intersect(viewport.TransformToAncestor(root).TransformBounds(new Rect(viewport.RenderSize)));
            Require(target.IsVisible&&!visible.IsEmpty&&visible.Width>=bounds.Width-.5&&visible.Height>=bounds.Height-.5,"Control is clipped after BringIntoView: "+name);
            return new{Name=name,X=bounds.X,Y=bounds.Y,Width=bounds.Width,Height=bounds.Height,VisibleWidth=visible.Width,VisibleHeight=visible.Height,
                Method=bring?"ProgrammaticBringIntoView_NotPhysicalKeyboard":"ObservedProductionAutoReveal_NoFixtureScroll"};
        }
        private void WriteJson(string file,object value)
        {
            byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value,new JsonSerializerOptions{WriteIndented=true});
            Require(bytes.Length<=256*1024,"Result JSON exceeds bound");
            using var stream=new FileStream(Path.Combine(output,file),FileMode.CreateNew,FileAccess.Write,FileShare.None);stream.Write(bytes);stream.Flush(true);
        }
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject parent)where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(parent);i++)
        {var child=VisualTreeHelper.GetChild(parent,i);if(child is T match)yield return match;foreach(var descendant in Descendants<T>(child))yield return descendant;}
    }
    private sealed class DirectoryPolicy:ISecretDirectoryPolicy{public void EnsurePrivate(string directory)=>Directory.CreateDirectory(directory);}
    private sealed class NoKeys:ISecretProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plain)=>throw new InvalidOperationException("No fixture secrets allowed.");
        public byte[] Unprotect(ReadOnlySpan<byte> encrypted)=>throw new InvalidOperationException("No fixture secrets allowed.");
    }
}
