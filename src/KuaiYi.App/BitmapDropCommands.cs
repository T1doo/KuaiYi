using KuaiYi.Core;
using KuaiYi.Imaging;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    private Task droppedBitmapTask=Task.CompletedTask;
    // The factory transfers ownership only after every shared image-task exclusion has passed.
    private bool TryStartBitmapPreparation(Func<IBitmapImageOperation> create,long epoch,Activation activation)
    {
        if(exiting||FileImageBusy||!screenshotHotkeyTask.IsCompletedSuccessfully||!screenshotHotkeyCleanup.IsCompletedSuccessfully||screenshotHotkeyOwner!=null)return false;
        var owner=create();fileImageOwner=owner;
        droppedBitmapTask=fileImageTask=LoadPreparedBitmap(owner,epoch,activation);_=ObserveOpenedImage(droppedBitmapTask,resultRevision);return true;
    }
    private async Task LoadPreparedBitmap(IBitmapImageOperation owner,long epoch,Activation activation)
    {
        try
        {
            await LoadPreparedImage(()=>owner,async(held,session,version,jobEpoch,token)=>
            {
                var image=await ((IBitmapImageOperation)held).LoadAsync(session,version,jobEpoch,token);
                if(image.Kind==ImageSourceKind.Bitmap)return image;
                image.Dispose();throw new System.IO.InvalidDataException("Bitmap source kind mismatch.");
            },expectedEpoch:epoch,initial:activation);
        }
        finally
        {
            // This original task remains the slot owner even when cancellation ends its preparation waiter first.
            await owner.ObservePeerCleanupAsync();
            if(await FinalizeFileOwner(owner)&&ReferenceEquals(fileImageOwner,owner))fileImageOwner=null;
        }
    }
}
