using System.IO;
using KuaiYi.Imaging;
using KuaiYi.Core;
namespace KuaiYi.App;
internal sealed partial class ShellWindow
{
    // The caller retains the original inbox receipt through this exact Task. No filename or temporary file.
    private Task StartReceivedBrowserImage(ReadOnlyMemory<byte> encoded,Func<IBrowserImageOperation> create,CancellationToken cancellation=default,Func<bool>? current=null,long? expectedEpoch=null,Activation? initial=null)
        =>StartImagePreparation(create,async(owner,session,version,epoch,cancellation)=>
        {
            var image=await ((IBrowserImageOperation)owner).LoadAsync(encoded,session,version,epoch,cancellation);
            if(image.Kind==ImageSourceKind.Browser)return image;
            image.Dispose();throw new InvalidDataException("Browser image source kind mismatch.");
        },cancellation,current,expectedEpoch,initial);
}
