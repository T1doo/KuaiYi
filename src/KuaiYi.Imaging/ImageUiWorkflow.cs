using KuaiYi.Core;
namespace KuaiYi.Imaging;

public enum ImageUiEventKind { Started, Completed, Failed, Cancelled, ExitUnconfirmed }
public sealed record ImageUiUpdate(ImageUiEventKind Kind, string Status, string Text)
{
    public static ImageUiUpdate Started() => new(ImageUiEventKind.Started,"读取并启动独立 ImageWorker…","");
    public static ImageUiUpdate Cancelled() => new(ImageUiEventKind.Cancelled,"已取消","");
    public static ImageUiUpdate Unconfirmed() => new(ImageUiEventKind.ExitUnconfirmed,"Off · Worker 退出未确认；禁止重启/关闭，保留所有权等待实际退出。","");
    public static ImageUiUpdate FromReply(ImageReply reply) => reply.Failure == ImageFailure.None
        ? new(ImageUiEventKind.Completed,"本地 OCR 完成",reply.Output?.Text ?? "")
        : reply.Failure == ImageFailure.Cancelled ? Cancelled() : Failed(new ImageJobException(reply.Failure));
    public static ImageUiUpdate Failed(Exception error)
    {
        string status = error is ImageJobException job ? job.Code.ToString() : error.GetType().Name;
        if (error is ImageJobException {Code:ImageFailure.LanguageUnavailable}) status += " · 请在 Windows 语言设置中安装相应 OCR 能力，或之后配置明确支持视觉的模型。";
        return new(ImageUiEventKind.Failed,status,""); // Never put exception message, paths or credentials into UI diagnostics.
    }
}
/// <summary>The App's real start/outcome boundary. Success, error and cancellation use identical full-identity checks.</summary>
public sealed class ImageUiWorkflow(RuntimeGate gate, SynchronizationContext destination)
{
    private readonly IdentityEventReceiver<ImageUiUpdate> receiver = new(gate,destination);
    public Task<bool> PublishAsync(RequestIdentity identity, ImageUiUpdate update, Action<ImageUiUpdate> apply) => receiver.ReceiveAsync(identity,update,apply);
    public async Task<bool> RunAsync(RequestIdentity identity, Func<CancellationToken,Task<ImageReply>> operation, Action<ImageUiUpdate> apply)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(apply);
        if (!gate.TryLinkRequestCancellation(identity, default, out var linked)) return false;
        using (linked)
        {
            if (!await PublishAsync(identity,ImageUiUpdate.Started(),apply)) return false;
            ImageUiUpdate update;
            try { linked!.Token.ThrowIfCancellationRequested(); update = ImageUiUpdate.FromReply(await operation(linked.Token)); }
            catch (OperationCanceledException) { update = ImageUiUpdate.Cancelled(); }
            catch (Exception error) { update = ImageUiUpdate.Failed(error); }
            // The request token is intentionally not a delivery token: a current cancellation may be shown;
            // Off/replacement/lock/stop are rejected by RuntimeGate on actual queue execution.
            return await PublishAsync(identity,update,apply);
        }
    }
}
