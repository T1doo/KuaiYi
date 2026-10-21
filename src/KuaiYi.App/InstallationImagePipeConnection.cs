using System.IO;
using KuaiYi.Contracts;
using KuaiYi.Core.Installation;
namespace KuaiYi.App;
internal sealed class InstallationImageConnectionRejectedException(IEnumerable<Exception> errors):IOException("Installed image connection rejected after original work settled.",new AggregateException(errors));
// The Windows caller supplies real peer/live checks. Tests exercise only this bounded connection settlement, never authentication.
internal static class InstallationImagePipeConnection
{
    internal static async Task ServeAsync(Stream pipe,Action requireCurrent,Func<CancellationToken,Task> verify,InstallationWarmImageClient client,CancellationToken cancellation)
    {
        using var connection=CancellationTokenSource.CreateLinkedTokenSource(cancellation);connection.CancelAfter(TimeSpan.FromSeconds(10));
        List<Task> originals=[];List<Exception> errors=[];InstallationWarmImageClient.Request? request=null;CancellationToken requestToken=default;Task? externalRead=null;
        Task Own(Task task){if(originals.Count>=12)throw new InvalidOperationException("Installed connection task bound.");originals.Add(task);return task;}
        CancellationTokenRegistration revoke=default;
        try
        {
            await Own(verify(connection.Token)).ConfigureAwait(false);requireCurrent();
            var helloRead=InstallationImagePipeWire.ReadAsync(pipe,IpcMessageType.Ping,connection.Token);_ =Own(helloRead);var helloFrame=await helloRead.ConfigureAwait(false);
            var hello=InstallationImagePipeWire.Decode<InstallationImageHello>(helloFrame);if(!InstallationWarmIdentity.Nonce(hello.ClientNonce))throw new InvalidDataException("Invalid hello.");
            requireCurrent();var attempt=new InstallationWarmConnection(hello.ClientNonce);var reserve=client.ReserveAsync(attempt);_ =Own(reserve);
            var active=client.Active;if(active?.Lease.Grant.Identity.Connection!=attempt.Id)throw new InstallationBusyException();request=active;requestToken=request.Lease.Token;
            revoke=connection.Token.Register(()=>{_=client.Cancel(request);});
            request=await reserve.ConfigureAwait(false);requireCurrent();
            await Own(InstallationImagePipeWire.WriteAsync(pipe,IpcMessageType.Ping,request.Lease.Grant.Identity.Request,new InstallationImageChallenge(helloFrame.RequestId,InstallationWarmImageWire.Encode(request.Lease.Grant)),connection.Token)).ConfigureAwait(false);
            var submitRead=InstallationImagePipeWire.ReadAsync(pipe,IpcMessageType.SubmitImagePath,connection.Token);_ =Own(submitRead);var submitFrame=await submitRead.ConfigureAwait(false);var submit=InstallationImagePipeWire.Decode<InstallationImageSubmit>(submitFrame);
            var grant=InstallationWarmImageWire.Decode<InstallationWarmGrant>(submit.Grant);if(grant!=request.Lease.Grant||submitFrame.RequestId!=grant.Identity.Request)throw new InvalidDataException("Consumed request binding differs.");
            KuaiYi.Core.OleImageFileDrop.ValidatePath(submit.Path);requireCurrent();connection.CancelAfter(TimeSpan.FromSeconds(15));
            var commit=client.CommitAsync(request,submit.Path);_ =Own(commit);
            byte[] extra=new byte[1];var disconnect=pipe.ReadAsync(extra,connection.Token).AsTask();externalRead=disconnect;_ =Own(disconnect);
            if(await Task.WhenAny(commit,disconnect).ConfigureAwait(false)==disconnect){await disconnect.ConfigureAwait(false);throw new EndOfStreamException("No second request is accepted on this connection.");}
            var result=await commit.ConfigureAwait(false);requireCurrent();
            if(result.Outcome==InstallationWarmOutcome.PreviewReady&&result.SourceVersion==Guid.Empty)throw new InvalidDataException("Missing actual App source version.");
            await Own(InstallationImagePipeWire.WriteAsync(pipe,IpcMessageType.SubmitImagePath,result.Identity.Request,InstallationWarmImageWire.Encode(result),connection.Token)).ConfigureAwait(false);
        }
        catch(Exception error){errors.Add(error);}
        // Raw cancellation comes before joining any original write, read, or worker receipt.
        List<Exception> settlementFailures=[];Task cancel=connection.CancelAsync();
        if(request!=null)try{await client.Cancel(request).ConfigureAwait(false);}catch(Exception error){settlementFailures.Add(error);}
        try{await cancel.ConfigureAwait(false);}catch(Exception error){settlementFailures.Add(error);}
        try{await revoke.DisposeAsync().ConfigureAwait(false);}catch(Exception error){settlementFailures.Add(error);}
        foreach(var original in originals)
            try{await original.ConfigureAwait(false);}catch(OperationCanceledException)when(connection.IsCancellationRequested||requestToken.IsCancellationRequested){}
            catch(Exception error){if(original.Exception is {} all)errors.AddRange(all.InnerExceptions);else errors.Add(error);}
        if(request!=null)
        {
            try{await request.Result.Task.ConfigureAwait(false);}catch(Exception error){settlementFailures.Add(error);}
            if(request.Finish==null)settlementFailures.Add(new IOException("Original warm retirement missing."));
            else try{if(!await request.Finish.ConfigureAwait(false))settlementFailures.Add(new IOException("Original warm cleanup unconfirmed."));}catch(Exception error){settlementFailures.Add(error);}
        }
        GC.KeepAlive(externalRead);
        if(settlementFailures.Count!=0)throw new AggregateException("Original installed connection settlement failed.",settlementFailures.Concat(errors));
        if(errors.Count!=0)
        {
            bool Expected(Exception e)=>e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or FormatException or OperationCanceledException or InvalidOperationException;
            if(errors.All(Expected))throw new InstallationImageConnectionRejectedException(errors);
            throw new AggregateException("Unexpected installed connection failure.",errors);
        }
    }
}
