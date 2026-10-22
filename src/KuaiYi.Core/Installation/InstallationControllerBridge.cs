using System.Runtime.ExceptionServices;
namespace KuaiYi.Core.Installation;

internal static class InstallationControllerBridge
{
    internal static void Throw(IReadOnlyList<Exception> errors)
    {if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException(errors);}
    internal static async Task<int> RunAsync(Stream input,Stream output,Guid nonce,string directory,string userData,
        Func<InstallationStableSelection,Func<InstallationControlledLaunch,CancellationToken,Task>,CancellationToken,Task<int>> start,
        Func<Task,Task>? observeForTest=null,Func<Task?>? publicationOwner=null)
    {
        var budget=TimeSpan.FromSeconds(10);using var first=new CancellationTokenSource(budget);
        string line=await InstallationControlWire.Read(input,first.Token).ConfigureAwait(false)??throw new EndOfStreamException("No parent selection.");
        if(!line.StartsWith("LAUNCH_CURRENT|",StringComparison.Ordinal))throw new InvalidDataException("Unknown controller entry.");
        var selection=InstallationStableSelection.Decode(line[15..]);selection.RequireOwn(nonce,directory,userData);
        using var stop=new CancellationTokenSource();object sync=new(),writeSync=new();int phase=0;Task? cancellation=null;List<Exception> errors=[];
        var serial=new SemaphoreSlim(1,1);List<Task> writes=[];bool writesSealed=false;
        Task reader=ReadParent();Task<int> original;
        try{original=start(selection,Published,stop.Token);}catch(Exception error){original=Task.FromException<int>(error);}
        try
        {
            if(await original.ConfigureAwait(false)!=0)throw new IOException("Original session failed.");
            lock(sync){if(phase!=0)throw new OperationCanceledException("Parent revoked the original session.");phase=1;}
        }
        catch(Exception error){errors.Add(error);lock(sync)phase=2;}
        // The original coordinator may return Unconfirmed while its already-owned callback producer is still pending.
        // Join that exact producer before sealing writes; a delayed callback cannot publish after bridge settlement.
        if(publicationOwner?.Invoke() is {} producer)await ObserveAndJoin(producer).ConfigureAwait(false);
        Task closing=Send($"CONTROLLER_CLOSING|{nonce:N}|{selection.Fingerprint}",CancellationToken.None);
        await ObserveAndJoin(closing).ConfigureAwait(false);
        await ObserveAndJoin(reader).ConfigureAwait(false);
        Task? cancel;lock(sync)cancel=cancellation;if(cancel!=null)await ObserveAndJoin(cancel).ConfigureAwait(false);
        Task[] previous;lock(writeSync)previous=writes.ToArray();foreach(Task task in previous)await ObserveAndJoin(task).ConfigureAwait(false);
        lock(sync)if(phase!=1)errors.Add(new OperationCanceledException("Parent lifecycle did not close normally."));
        if(errors.Count==0)await ObserveAndJoin(Send($"CONTROLLER_EXIT_READY|{nonce:N}|{selection.Fingerprint}",CancellationToken.None)).ConfigureAwait(false);
        lock(writeSync){writesSealed=true;previous=writes.ToArray();}
        foreach(Task task in previous)await ObserveAndJoin(task).ConfigureAwait(false);
        serial.Dispose();Throw(errors);return 0;
        async Task ObserveAndJoin(Task task)
        {
            try{await (observeForTest?.Invoke(task)??task.WaitAsync(budget)).ConfigureAwait(false);}
            catch(Exception observed)
            {
                if(!errors.Contains(observed))errors.Add(observed);
                // An observation timeout is a failure, never permission to drop the original owner or dispose its CTS.
                try{await task.ConfigureAwait(false);}catch(Exception actual){if(!errors.Contains(actual))errors.Add(actual);}
            }
        }
        Task Send(string row,CancellationToken token)
        {
            lock(writeSync)
            {
                if(writesSealed)throw new InvalidOperationException("Controller writes are sealed.");
                Task task=WriteSerial(row,token);writes.Add(task);return task;
            }
        }
        async Task WriteSerial(string row,CancellationToken token)
        {
            await serial.WaitAsync(token).ConfigureAwait(false);
            try{await InstallationControlWire.Send(output,row,token).ConfigureAwait(false);}finally{serial.Release();}
        }
        Task Published(InstallationControlledLaunch proof,CancellationToken token)
        {
            lock(sync)if(phase!=0)throw new OperationCanceledException("Parent revoked before UI/session publication.");
            return Send($"BOUND_READY|{nonce:N}|{selection.Fingerprint}|{proof.Launch:N}|{proof.Fingerprint}",token);
        }
        async Task ReadParent()
        {
            Exception? failure=null;string? row=null;
            try
            {
                row=await InstallationControlWire.Read(input,CancellationToken.None).ConfigureAwait(false);
                if(row!=null&&row!=$"STOP|{nonce:N}|{selection.Fingerprint}")throw new InvalidDataException("Unexpected parent lifecycle line.");
            }
            catch(Exception error){failure=error;}
            lock(sync)
            {
                // Atomic with original success: only EOF after Closing is a normal acknowledgement; STOP always revokes.
                if(phase!=1||row!=null||failure!=null){phase=2;cancellation??=stop.CancelAsync();}
            }
            if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
        }
    }
}
