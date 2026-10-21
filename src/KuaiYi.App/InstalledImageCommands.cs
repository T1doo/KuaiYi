using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private readonly record struct InstalledImageState(long Epoch,Activation Activation,long Input,long Result,string Settings);
    private readonly object installedImageSync=new();
    private Task installationImageDispatch=Task.CompletedTask;
    private bool installedImageClosed;
    // Called before queuing Dispatcher work. Only immutable settings, the thread-safe gate, and atomic ordinals are read.
    private InstalledImageState CaptureInstalledImageState()
    {var state=lifecycle.Gate.Snapshot;return new(state.Epoch,state.Activation,Volatile.Read(ref imageInputRevision),Volatile.Read(ref resultRevision),lifecycle.Settings?.Version??"");}
    private void RegisterInstalledImageDispatch(Func<InstalledImageState,Task> dispatch)
    {
        lock(installedImageSync)
        {
            if(installedImageClosed)return;
            installationImageDispatch=dispatch(CaptureInstalledImageState());
            _=installationImageDispatch.ContinueWith(t=>{_ =t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        }
    }
    private Task StartInstalledImage(string path,InstalledImageState expected,Func<IFileImageOperation> create,CancellationToken cancellation,Func<bool> current,Func<CancellationToken,Task> verify)
    {
        if(Volatile.Read(ref installedImageClosed)||expected!=CaptureInstalledImageState()||lifecycle.Gate.Snapshot.Suspension!=Suspension.None)return Task.CompletedTask;
        return StartImagePreparation(create,async(owner,session,version,epoch,token)=>
        {
            var image=await ((IFileImageOperation)owner).LoadAsync(path,session,version,epoch,token);
            try{await verify(token);return image;}
            catch{image.Dispose();throw;} // The outer image assignment has not happened yet: retain/zero this exact raster here.
        },cancellation,current,expected.Epoch,expected.Activation);
    }
    private Task StopInstalledImageDispatch()
    {lock(installedImageSync){Volatile.Write(ref installedImageClosed,true);return installationImageDispatch;}}
}
