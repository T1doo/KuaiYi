using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using KuaiYi.Imaging;
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
    private CancellationTokenSource? running;
    private ImageWorkerClient? ownedWorker;
    private long version;
    private readonly string? pendingPath;
    public ImageProbeWindow(string? path)
    {
        pendingPath = path;
        Title = "快译 · P0 OCR 实验"; Width = 560; Height = 420;
        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(status); panel.Children.Add(enabled); panel.Children.Add(language); panel.Children.Add(choose);
        result.Height = 260; panel.Children.Add(result); Content = panel;
        if (path != null) status.Text = "Off · Explorer 图片待确认；尚未读取。P0 仅支持 PNG。";
        choose.Click += async (_,_) => await Recognize();
        enabled.Unchecked += (_,_) => { version++; running?.Cancel(); result.Clear(); status.Text = "Off · 已取消"; };
        Closing += (_,e) => { if (running != null || ownedWorker != null) { e.Cancel = true; running?.Cancel(); status.Text = "正在确认 Worker 退出，请稍后关闭。"; } };
        var exitPoll = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        exitPoll.Tick += async (_,_) => { if (running == null && ownedWorker is { } worker && worker.ObserveExit()) { await worker.DisposeAsync(); ownedWorker = null; choose.IsEnabled = true; status.Text = "Worker 退出已确认；保持 Off。"; enabled.IsChecked = false; } };
        exitPoll.Start(); Closed += (_,_) => exitPoll.Stop();
    }
    private async Task Recognize()
    {
        if (enabled.IsChecked != true || running != null || ownedWorker != null) { status.Text = "请先允许本次本地 OCR，并等待旧 Worker 退出确认。"; return; }
        var identity = PackageIdentity.Read("KuaiYi");
        if (identity.Failure != ImageFailure.None) { status.Text = identity.Failure + " · 请先按开发身份说明从安装位置验证主程序身份。"; return; }
        string? path = pendingPath;
        if (path == null) { var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "PNG|*.png", Multiselect = false }; if (dialog.ShowDialog(this) != true) return; path = dialog.FileName; }
        long current = ++version;
        using var cancellation = new CancellationTokenSource(); running = cancellation; choose.IsEnabled = false;
        byte[]? bytes = null;
        try
        {
            status.Text = "读取并启动独立 ImageWorker…";
            // Native file validation and worker startup are off the dispatcher; no decode/OCR on the UI thread.
            bytes = await Task.Run(() => LocalImageFile.ReadPngAsync(path, cancellation.Token), cancellation.Token);
            var worker = new ImageWorkerClient(Path.Combine(AppContext.BaseDirectory,"KuaiYi.ImageWorker.exe")); ownedWorker = worker;
            var reply = await worker.RunAsync(bytes, new(Guid.NewGuid(),current,1,language.Text,bytes.Length), cancellation.Token);
            if (version != current || enabled.IsChecked != true) return;
            status.Text = reply.Failure.ToString(); result.Text = reply.Output?.Text ?? "";
            if (reply.Failure == ImageFailure.LanguageUnavailable) status.Text += " · 请在 Windows 语言设置中安装相应 OCR 能力，或之后配置明确支持视觉的模型。";
        }
        catch (OperationCanceledException) { status.Text = "已取消；Worker 退出已确认。"; }
        catch (Exception e) { status.Text = e is ImageJobException job ? job.Code.ToString() : e.GetType().Name; }
        finally
        {
            if (bytes != null) CryptographicOperations.ZeroMemory(bytes);
            if (ownedWorker is { } worker && worker.ObserveExit()) { await worker.DisposeAsync(); ownedWorker = null; }
            running = null; choose.IsEnabled = ownedWorker == null;
            if (ownedWorker != null) { enabled.IsChecked = false; status.Text = "Worker 退出未确认；禁止重启/关闭，保留所有权等待实际退出。"; }
        }
    }
}
