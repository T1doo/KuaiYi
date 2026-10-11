namespace KuaiYi.Imaging;
/// <summary>Retain ownership until capture actually returns; never abandon an uncancellable OS operation or its buffers.</summary>
public static class ScreenshotAcquisition
{
    public static async Task<ScreenshotSnapshot> AcquireAsync(ScreenshotIntent intent,Func<CancellationToken,Task<ScreenshotSnapshot>> capture,CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();if(!intent.IsCurrent)throw new OperationCanceledException();
        var snapshot=await capture(cancellation).ConfigureAwait(false);
        try{cancellation.ThrowIfCancellationRequested();if(!intent.IsCurrent)throw new OperationCanceledException();return snapshot;}
        catch{snapshot.Dispose();throw;}
    }
}
