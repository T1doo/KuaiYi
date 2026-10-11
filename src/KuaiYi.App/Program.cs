using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using KuaiYi.Imaging;
using KuaiYi.Core;
using System.Windows.Threading;
namespace KuaiYi.App;
internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--diagnose" })) { Console.WriteLine(JsonSerializer.Serialize(PackageIdentity.Read("KuaiYi"), ImageJson.Options)); return 0; }
        if (args.Length == 3 && args[0] == "--ocr-probe")
            return Probe(args[1], args[2]).GetAwaiter().GetResult(); // Explicit synthetic fixture command, never automatic selection capture.
        if (args.Length != 0 && !(args.Length == 2 && args[0] == "--prepare-image")) { Console.Error.WriteLine("Unsupported command."); return 2; }
        var app = new Application();
        return app.Run(new ImageProbeWindow(args.Length == 2 ? args[1] : null));
    }
    private static async Task<int> Probe(string path, string language)
    {
        byte[]? image = null;
        try
        {
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
internal sealed class ImageProbeWindow : Window
{
    private readonly TextBlock status = new() { Text = "Off · 本地 OCR 平台实验；不会调用模型", TextWrapping = TextWrapping.Wrap };
    private readonly TextBox result = new() { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBox language = new() { Text = "en-US" };
    private readonly CheckBox enabled = new() { Content = "允许本次本地 OCR", IsChecked = false };
    private readonly Button choose = new() { Content = "选择 PNG 并识别" };
    private readonly RuntimeGate gate = new();
    private readonly ImageUiWorkflow workflow;
    private readonly Guid session = Guid.NewGuid();
    private RequestIdentity? running;
    private sealed record OwnedImageWorker(RequestIdentity Identity, ImageWorkerClient Client);
    private OwnedImageWorker? ownedWorker;
    private long workerVersion; // Worker wire ordinal only. UI eligibility always uses RuntimeGate's complete identity.
    private readonly string? pendingPath;
    public ImageProbeWindow(string? path)
    {
        pendingPath = path;
        workflow = new(gate,new DispatcherSynchronizationContext(Dispatcher));
        Title = "快译 · P0 OCR 实验"; Width = 560; Height = 420;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(status); panel.Children.Add(enabled); panel.Children.Add(language); panel.Children.Add(choose);
        result.Height = 260; panel.Children.Add(result); Content = panel;
        if (path != null) status.Text = "Off · Explorer 图片待确认；尚未读取。P0 仅支持 PNG。";
        choose.Click += async (_,_) => await Recognize();
        enabled.Checked += (_,_) =>
        {
            if ((running == null && ownedWorker != null) || !gate.TryEnableLocalOperation(true)) { enabled.IsChecked = false; status.Text = "Off · 请等待旧 Worker 退出确认。"; }
            else status.Text = "On · 仅允许本次本地 OCR，不授予模型访问。";
        };
        enabled.Unchecked += (_,_) => { gate.Disable(); result.Clear(); status.Text = "Off · 已取消"; };
        Closing += (_,e) =>
        {
            if (running != null || ownedWorker != null) { e.Cancel = true; gate.Disable(); enabled.IsChecked = false; status.Text = "Off · 正在确认 Worker/启动任务退出，请稍后关闭。"; }
        };
        var exitPoll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        exitPoll.Tick += async (_,_) =>
        {
            if (running == null && ownedWorker is { } owner && await owner.Client.TryFinalizeAsync())
            {
                if (ReferenceEquals(ownedWorker,owner)) ownedWorker = null;
                choose.IsEnabled = running == null && ownedWorker == null;
                // Resource bookkeeping is independent of eligibility. No old task writes status/results or toggles On here.
            }
        };
        exitPoll.Start(); Closed += (_,_) => { exitPoll.Stop(); gate.Disable(); };
    }
    private void Apply(ImageUiUpdate update)
    {
        // Only invoked by the shared receiver after RuntimeGate.TryApply on this Dispatcher.
        if (update.Kind == ImageUiEventKind.ExitUnconfirmed) enabled.IsChecked = false;
        status.Text = update.Status; result.Text = update.Text;
    }
    private async Task Recognize()
    {
        if (gate.Snapshot.Activation != Activation.On || running != null || ownedWorker != null) { status.Text = "请先允许本次本地 OCR，并等待旧 Worker 退出确认。"; return; }
        string? path = pendingPath;
        if (path == null) { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PNG|*.png", Multiselect = false }; if (dialog.ShowDialog(this) != true) return; path = dialog.FileName; }
        if (!gate.TryStartRequest(session,Guid.NewGuid(),null,out var current,out _)) return;
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
                bytes = await Task.Run(() => LocalImageFile.ReadPngAsync(path,cancellation),cancellation);
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
            choose.IsEnabled = running == null && ownedWorker == null;
            if (operationOwner != null && ReferenceEquals(ownedWorker,operationOwner))
                await workflow.PublishAsync(current,ImageUiUpdate.Unconfirmed(),Apply);
        }
    }
}
