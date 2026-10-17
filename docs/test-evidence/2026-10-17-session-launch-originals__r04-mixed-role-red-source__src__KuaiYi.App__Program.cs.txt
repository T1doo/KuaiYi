using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using KuaiYi.Imaging;
using KuaiYi.Core;
using KuaiYi.NativeHost;
using System.Windows.Threading;
using KuaiYi.Windows;
using System.Security.Principal;
using System.Diagnostics;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;
internal static class Program
{
    private static InstallationApplicationControl? runtimeControl; // Root a committed runtime lease through OS process exit.
    private static InstallationSessionEntry? runtimeSession; // Retain this caller's Process and original tasks through parent exit.
    [STAThread] public static int Main(string[] args)
    {
#if KUAIYI_WINDOWS_CI_PROBE
        if(args.Length==2&&args[0]=="--ci-ole-marshal-contract")return HostedOleMarshalProbe.Run(args[1]);
        if(args.Length==4&&args[0]=="--ci-ole-responsiveness")return HostedOleProbe.Run(args[1],args[2],args[3]);
#endif
        if (args.SequenceEqual(new[] { "--diagnose" })) { Console.WriteLine(JsonSerializer.Serialize(PackageIdentity.Read("KuaiYi"), ImageJson.Options)); return 0; }
        if (args.Length == 3 && args[0] == "--ocr-probe")
            return Probe(args[1], args[2]).GetAwaiter().GetResult(); // Explicit synthetic fixture command, never automatic selection capture.
        bool review=args.SequenceEqual(new[]{"--platform-review"})||(args.Length==2&&args[0]=="--prepare-image");
        bool translationReview=args.Length==2&&args[0]=="--translation-test-provider";
        bool controlled=args.Length==2&&args[0]=="--installation-handshake"&&Guid.TryParseExact(args[1],"N",out _);
        InstallationApplicationControl? installationControl=null;
        KuaiYi.Core.Settings.AppSettings? testSettings=null;
        if(translationReview)
        {
            try{testSettings=KuaiYi.Providers.DevelopmentTranslationProfile.Create(new Uri(args[1],UriKind.Absolute));}
            catch(Exception error)when(error is ArgumentException or UriFormatException){Console.Error.WriteLine("Invalid local test fixture prefix.");return 2;}
        }
        if (args.Length != 0 && !review&&!translationReview&&!controlled) { Console.Error.WriteLine("Unsupported command."); return 2; }
        string userData=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KuaiYi");
        // Route ordinary installed entry before acquiring the UI mutex: the controlled child owns that mutex.
        if(args.Length==0)
        {
            try
            {
                runtimeSession=InstallationSessionEntry.TryCreate(AppContext.BaseDirectory,userData);
                if(runtimeSession!=null)return runtimeSession.RunAsync().GetAwaiter().GetResult();
            }
            catch(Exception error)
            {
                Console.Error.WriteLine("Installation session refused: "+error.GetType().Name);
                // Preserve explicit user re-invocation's existing Show message; it never proves admission or ownership.
                using var callerIdentity=WindowsIdentity.GetCurrent();using var caller=Process.GetCurrentProcess();
                string existingScope=SessionSingleInstance.ScopeFor(callerIdentity.User?.Value??throw new InvalidOperationException("Current user SID unavailable."),caller.SessionId);
                NativeTray.ShowExisting(existingScope);
                return 3;
            }
        }
        InstallationNormalEntry? installationEntry=null;
        try
        {
            if(controlled)runtimeControl=installationControl=InstallationApplicationControl.AdmitAsync(Console.OpenStandardInput(),Console.OpenStandardOutput(),Guid.ParseExact(args[1],"N"),AppContext.BaseDirectory,userData).GetAwaiter().GetResult();
        }
        catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException or FormatException or System.Text.Json.JsonException)
        {Console.Error.WriteLine("Installation startup refused: "+error.GetType().Name);return 3;}
        using var identity=WindowsIdentity.GetCurrent();
        using var process=Process.GetCurrentProcess();
        using var instance=new SessionSingleInstance(identity.User?.Value??throw new InvalidOperationException("Current user SID unavailable."),process.SessionId);
        if(!instance.IsOwner)
        {
            if(controlled)return 3; // Never redirect a controlled health/exit transaction to an unowned instance.
            NativeTray.ShowExisting(instance.Scope);
            // No unverified payload forwarding. Existing installed-role IPC remains the image ingress.
            return args.Length==2?3:0;
        }
        try{if(!controlled)installationEntry=InstallationNormalEntry.Open(AppContext.BaseDirectory,userData);}
        catch(Exception error)when(error is IOException or InvalidDataException or ArgumentException)
        {Console.Error.WriteLine("Installation startup refused: "+error.GetType().Name);return 3;}
        using var installationLifetime=installationEntry;
        var app = new Application();
        if(testSettings!=null)
        {
            // Own disposable fixture config. Never open the user's production config or encrypted secrets.
            string testRoot=Path.Combine(Path.GetTempPath(),"KuaiYi-translation-review-"+Guid.NewGuid().ToString("N"));
            try
            {
                var testCoordinator=new KuaiYi.Core.Settings.SettingsCoordinator(new(Path.Combine(testRoot,"config")),new(Path.Combine(testRoot,"secrets"),new CurrentUserDpapiProtector(),new CurrentUserSecretDirectory()));
                using(var draft=testCoordinator.OpenDraftAsync().GetAwaiter().GetResult())
                {draft.Update(_=>testSettings);testCoordinator.SaveAsync(draft).GetAwaiter().GetResult();}
                return app.Run(new ShellWindow(instance.Scope,testCoordinator,developmentBoundary:KuaiYi.Providers.DevelopmentTranslationBoundary.FromPrefix(new Uri(args[1],UriKind.Absolute))));
            }
            finally{if(Directory.Exists(testRoot))Directory.Delete(testRoot,true);}
        }
        if(review)return app.Run(new ImageProbeWindow(args.Length==2?args[1]:null));
        var shell=new ShellWindow(instance.Scope);if(installationControl!=null)shell.AttachInstallationControl(installationControl);
        int exit=app.Run(shell);GC.KeepAlive(runtimeControl);return exit;
    }
    private static async Task<int> Probe(string path, string language)
    {
        byte[]? image = null;
        try
        {
            using var installationEntry=InstallationNormalEntry.Open(AppContext.BaseDirectory,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"KuaiYi"));
            var identity = PackageIdentity.Read("KuaiYi");
            if (identity.Failure != ImageFailure.None) throw new ImageJobException(identity.Failure);
            image = await LocalImageFile.ReadPngAsync(path);
            await using var worker = new ImageWorkerClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe"));
            var reply = await worker.RunAsync(image, new(Guid.NewGuid(),1,1,language,image.Length));
            // Public fixture evidence only counts/status: never emit OCR text or image path to diagnostics.
            bool matchesPublicFixture = reply.Output != null && System.Text.RegularExpressions.Regex.Replace(reply.Output.Text.ToUpperInvariant(), "\\s+", " ").Trim() == "KUAIYI TEST";
            Console.WriteLine(JsonSerializer.Serialize(new { reply.Failure, CharacterCount = reply.Output?.Text.Length ?? 0, LineCount = reply.Output?.Lines.Length ?? 0, matchesPublicFixture, worker.ExitState }, ImageJson.Options));
            return reply.Failure == ImageFailure.None && matchesPublicFixture ? 0 : 3;
        }
        catch (ImageJobException e) { Console.WriteLine(JsonSerializer.Serialize(new { failure = e.Code }, ImageJson.Options)); return 3; }
        catch (Exception e) { Console.WriteLine(JsonSerializer.Serialize(new { failure = ImageFailure.ProtocolFailure, errorType = e.GetType().Name }, ImageJson.Options)); return 3; }
        finally { if (image != null) CryptographicOperations.ZeroMemory(image); }
    }
}
// P0 OCR review surface; not the production floating translation window or completed P1 application.
internal sealed partial class ImageProbeWindow : Window
{
    private readonly TextBlock status = new() { Text = "Off · 本地 OCR 平台实验；不会调用模型", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox result = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox language = new() { Text = "en-US" };
    private readonly CheckBox enabled = new() { Content = "允许本次本地 OCR", IsChecked = false };
    private readonly Button choose = new() { Content = "选择 PNG 并识别" };
    private readonly Button stopLocal = new() { Content = "关闭并清空本地接收 / OCR" };
    private readonly RuntimeGate gate = new();
    private readonly ImageUiWorkflow workflow;
    private readonly Guid session = Guid.NewGuid();
    private RequestIdentity? running;
    private sealed record OwnedImageWorker(RequestIdentity Identity, ImageWorkerClient Client);
    private OwnedImageWorker? ownedWorker;
    private long workerVersion; // Worker wire ordinal only. UI eligibility always uses RuntimeGate's complete identity.
    private readonly string? pendingPath;
    private readonly CancellationTokenSource browserLifetime=new();
    private readonly LocalImageInbox browserInbox;
    private InstalledBrowserReceiver? browserReceiver;
    private Task? browserTask;
    private RequestIdentity? shownBrowserImage;
    public ImageProbeWindow(string? path)
    {
        pendingPath = path;
        workflow = new(gate,new DispatcherSynchronizationContext(Dispatcher));
        browserInbox=new(gate,new DispatcherSynchronizationContext(Dispatcher));
        Title = "快译 · P0 OCR 实验"; Width = 560; Height = 420;
        // GetWindowDisplayAffinity requires layered HWND. Do not mutate native WPF styles after creation.
        WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=System.Windows.Media.Brushes.White;
        var panel = new StackPanel { Margin = new Thickness(16) };
        var header=new DockPanel();var closeReview=new Button{Content="关闭",HorizontalAlignment=HorizontalAlignment.Right};DockPanel.SetDock(closeReview,Dock.Right);header.Children.Add(closeReview);
        var caption=new TextBlock{Text=Title,Padding=new Thickness(0,4,12,4)};caption.MouseLeftButtonDown+=(_,e)=>{if(e.ButtonState==System.Windows.Input.MouseButtonState.Pressed)DragMove();};header.Children.Add(caption);closeReview.Click+=(_,_)=>Close();panel.Children.Add(header);
        panel.Children.Add(status); panel.Children.Add(enabled); panel.Children.Add(stopLocal); panel.Children.Add(language); panel.Children.Add(choose);
        SetupScreenshot(panel);
        SetupOlePresentation(panel);
        SetupSettings(panel);
        result.Height = 260; panel.Children.Add(result); Content = new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        if (path != null) status.Text = "Off · Explorer 图片待确认；尚未读取。P0 仅支持 PNG。";
        choose.Click += async (_,_) => await Recognize();
        stopLocal.Click+=(_,_)=>StopLocal(); // Available even while the initial Off prompt or transfer is pending.
        enabled.Checked += (_,_) =>
        {
            if ((running == null && ownedWorker != null) || ownedCaptureHost!=null || !gate.TryEnableLocalOperation(true)) { enabled.IsChecked = false; status.Text = "Off · 请等待旧捕获 / OCR 进程退出确认。"; }
            else status.Text = "On · 仅允许本次本地 OCR，不授予模型访问。";
        };
        enabled.Unchecked += (_,_) =>StopLocal();
        Closing += async (_,e) =>
        {
            if(settingsOpening||settingsWindow!=null){e.Cancel=true;settingsWindow?.Close();status.Text="请先结束设置草稿或等待原保存任务完成。";return;}
            if(oleWindow!=null){e.Cancel=true;oleWindow.Close();status.Text="请先确认浮钮的 OLE 进程与注册已退出，再关闭评审窗口。";return;}
            if(captureTask is {IsCompleted:false})
            {
                e.Cancel=true;gate.Disable();CancelScreenshot();
                try{await captureTask.WaitAsync(TimeSpan.FromSeconds(3));}
                catch(TimeoutException){status.Text="Off · 捕获任务尚未结束；窗口保留，可继续取消并等待退出确认。";Show();return;}
                if(ownedCaptureHost==null){Close();return;}
            }
            if(ownedCaptureHost!=null){e.Cancel=true;gate.Disable();CancelScreenshot();status.Text="Off · CaptureHost 的启动或退出尚未确认；窗口保留，请稍后关闭。";return;}
            if (running != null || ownedWorker != null) { e.Cancel = true; gate.Disable(); enabled.IsChecked = false; status.Text = "Off · 正在确认 Worker/启动任务退出，请稍后关闭。"; }
            else if(browserTask is {IsCompleted:false})
            {
                e.Cancel=true;gate.Disable();browserReceiver?.Revoke();browserInbox.Clear();browserLifetime.Cancel();
                status.Text="Off · 正在关闭本地图片接收连接…";
                await browserTask;Close();
            }
        };
        var exitPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        exitPoll.Tick += async (_,_) =>
        {
            if(gate.Snapshot.Activation==Activation.Off&&enabled.IsChecked==true)enabled.IsChecked=false;
            if (running == null && ownedWorker is { } owner && await owner.Client.TryFinalizeAsync())
            {
                if (ReferenceEquals(ownedWorker,owner)) ownedWorker = null;
                choose.IsEnabled = running == null && ownedWorker == null && ownedCaptureHost==null;
                // Resource bookkeeping is independent of eligibility. No old task writes status/results or toggles On here.
            }
            if(captureTask is not {IsCompleted:false}&&ownedCaptureHost is {} captureOwner&&await captureOwner.TryFinalizeAsync())
            {
                if(ReferenceEquals(ownedCaptureHost,captureOwner))ownedCaptureHost=null;
                screenshot.IsEnabled=choose.IsEnabled=running==null&&ownedWorker==null&&ownedCaptureHost==null;
                retryCaptureExit.IsEnabled=ownedCaptureHost!=null;
                // A late exit only releases resources. It cannot commit a frame, change activation or rewrite a newer result.
            }
            if(browserInbox.CurrentIdentity is {} identity&&shownBrowserImage!=identity)
                browserInbox.TryUseCurrent(identity,image=>
                {
                    shownBrowserImage=image.Identity;enabled.IsChecked=true;
                    status.Text=$"已接收本次本地 PNG · {image.Size.Width}×{image.Size.Height} · {image.Bytes.Length} 字节；未发送模型。";
                    result.Clear();
                });
        };
        exitPoll.Start(); Closed += (_,_) => { exitPoll.Stop();try{hotkey?.Dispose();}catch(Exception error){Console.Error.WriteLine("KuaiYi hotkey cleanup: "+error.GetType().Name);} gate.Disable(); browserLifetime.Cancel();browserInbox.Dispose();browserLifetime.Dispose(); Microsoft.Win32.SystemEvents.SessionSwitch-=OnSessionSwitch; };
        Microsoft.Win32.SystemEvents.SessionSwitch+=OnSessionSwitch;
        Loaded+=(_,_)=>
        {
            var confirmation=new DesktopLocalConfirmation(this,()=>oleWindow==null&&running==null&&ownedWorker==null&&ownedCaptureHost==null&&captureTask is not {IsCompleted:false});
            browserTask=Task.Run(async ()=>
            {
                try
                {
                    var policy=InstalledHostPolicy.LoadForApp();browserLifetime.Token.ThrowIfCancellationRequested();
                    browserReceiver=new(policy,gate,confirmation,browserInbox);
                    await browserReceiver.RunAsync(browserLifetime.Token);
                }
                catch(OperationCanceledException)when(browserLifetime.IsCancellationRequested){ /* Expected App shutdown; connection owns finite cancellation status. */ }
                catch(Exception error)
                {
                    string code=error is BrowserException b?b.Code.ToString():error.GetType().Name;
                    await Dispatcher.InvokeAsync(()=>{if(!browserLifetime.IsCancellationRequested)status.Text=$"浏览器本地接收不可用：{code}；未开放管道。";});
                }
            });
        };
    }
    private void StopLocal()
    {
        CancelScreenshot();
        oleWindow?.RevokeFromApp();
        gate.Disable();browserReceiver?.Revoke();browserInbox.Clear();enabled.IsChecked=false;result.Clear();status.Text="Off · 已取消并清空";
    }
    private void OnSessionSwitch(object sender,Microsoft.Win32.SessionSwitchEventArgs e)
    {
        if(e.Reason is not (Microsoft.Win32.SessionSwitchReason.SessionLock or Microsoft.Win32.SessionSwitchReason.SessionUnlock))return;
        // Invalidate immediately on the system event thread; an old queued commit must not precede UI lock handling.
        gate.SetSessionLocked(e.Reason==Microsoft.Win32.SessionSwitchReason.SessionLock);CancelScreenshot();
        oleWindow?.RevokeFromApp();
        browserReceiver?.Revoke();browserInbox.Clear();
        Dispatcher.BeginInvoke(()=>
        {
            enabled.IsChecked=false;browserReceiver?.Revoke();browserInbox.Clear();result.Clear();status.Text="Off · 会话状态变化，需重新确认。";
        });
    }
    private void Apply(ImageUiUpdate update)
    {
        // Only invoked by the shared receiver after RuntimeGate.TryApply on this Dispatcher.
        if (update.Kind == ImageUiEventKind.ExitUnconfirmed) enabled.IsChecked = false;
        status.Text = update.Status; result.Text = update.Text;
    }
    private async Task Recognize(ScreenshotPng? prepared=null,RequestIdentity? preparedIdentity=null)
    {
        if(preparedIdentity is {} expected&&!gate.CanApply(expected))return;
        if (oleWindow!=null || gate.Snapshot.Activation != Activation.On || running != null || ownedWorker != null || ownedCaptureHost!=null) { status.Text = "请先允许本次本地 OCR，并等待旧进程退出确认。"; return; }
        string? path = pendingPath;
        if (prepared==null && path == null) { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PNG|*.png", Multiselect = false }; if (dialog.ShowDialog(this) != true) return; path = dialog.FileName; }
        RequestIdentity current;
        if(preparedIdentity is {} ready){current=ready;if(!gate.CanApply(current))return;}
        else if (!gate.TryStartRequest(session,Guid.NewGuid(),null,out current,out _)) return;
        browserInbox.Clear();
        long ordinal = ++workerVersion;
        string languageTag = language.Text;
        running = current; choose.IsEnabled = false;
        byte[]? bytes = null;
        OwnedImageWorker? operationOwner = null;
        try
        {
            await workflow.RunAsync(current,async cancellation =>
            {
                var identity = PackageIdentity.Read("KuaiYi");
                if (identity.Failure != ImageFailure.None) throw new ImageJobException(identity.Failure);
                bytes = prepared!=null?prepared.Bytes.ToArray():await Task.Run(() => LocalImageFile.ReadPngAsync(path!,cancellation),cancellation);
                cancellation.ThrowIfCancellationRequested();
                var worker = new ImageWorkerClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe"));
                operationOwner = new(current,worker); ownedWorker = operationOwner;
                return await worker.RunAsync(bytes,new(current.SessionId,ordinal,current.Epoch,languageTag,bytes.Length),cancellation);
            },Apply);
        }
        finally
        {
            if (bytes != null) CryptographicOperations.ZeroMemory(bytes);
            if (operationOwner is { } owner && await owner.Client.TryFinalizeAsync())
            {
                if (ReferenceEquals(ownedWorker,owner)) ownedWorker = null;
            }
            if (running == current) running = null;
            choose.IsEnabled = running == null && ownedWorker == null && ownedCaptureHost==null;
            if (operationOwner != null && ReferenceEquals(ownedWorker,operationOwner))
                await workflow.PublishAsync(current,ImageUiUpdate.Unconfirmed(),Apply);
        }
    }
}
