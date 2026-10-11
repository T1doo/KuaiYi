using KuaiYi.Imaging;
using KuaiYi.Windows;
namespace KuaiYi.CaptureHost;
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if(args.Length!=1||args[0]!="--private-stdio")return 2;
        return await CaptureHostSession.RunAsync(Console.OpenStandardInput(),Console.OpenStandardOutput(),Capture);
    }
    private static async Task<ScreenshotSnapshot> Capture(CaptureJob job,CancellationToken token)
    {
        if(!OperatingSystem.IsWindows())throw new CaptureException(CaptureFailure.UnsupportedPlatform);
        // OS parent verification holds its handle throughout capture. Job's PID/HWND are only comparison data.
        using var parent=CaptureBoundary.VerifyActualParent(job);
        ScreenshotSnapshot? frame=null;
        try
        {
            CaptureBoundary.VerifyHidden(job,parent);
            ScreenshotDesktop.FlushHiddenWindows(); // Child-only preparation; never a parent compositor fence.
            CaptureBoundary.VerifyHidden(job,parent);
            var displays=ScreenshotDesktop.Displays();
            frame=await ScreenshotDesktop.CaptureAsync(displays,job.Backend==CaptureBackend.WindowsGraphicsCapture?ScreenshotBackend.WindowsGraphicsCapture:ScreenshotBackend.GdiCompatibility,token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();CaptureBoundary.VerifyHidden(job,parent);
            var accepted=frame;frame=null;return accepted;
        }
        catch(ScreenshotException error){throw new CaptureException(error.Code switch{ScreenshotFailure.Timeout=>CaptureFailure.Timeout,ScreenshotFailure.TopologyChanged=>CaptureFailure.TopologyChanged,ScreenshotFailure.ProtectedOrBlackFrame=>CaptureFailure.ProtectedOrBlackFrame,_=>CaptureFailure.CaptureUnavailable});}
        finally{frame?.Dispose();}
    }
}
