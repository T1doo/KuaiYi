using System.IO.Pipes;
using System.Threading.Channels;
using KuaiYi.Core.Installation;
internal static class InstallationAppControlFixture
{
    private static InstallationApplicationControl? control;
    internal static async Task<int> Run(Guid launch,string mode,string pipeName)
    {
        _=Task.Run(async()=>{await Task.Delay(TimeSpan.FromSeconds(40));Environment.Exit(91);});
        string root=Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.Parent!.Parent!.FullName;
        string state="NotAdmitted";var cancelRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // No UI/cleanup channel creation before the same production proof validation succeeds.
        try
        {
            Stream output=Console.OpenStandardOutput();if(mode is "lost-commit-output" or "unknown-after-commit")output=new LostCommitOutput(output,mode=="unknown-after-commit"?()=>File.WriteAllText(Path.Combine(root,"application","handoff-pending.json"),"PUBLIC_UNKNOWN_AFTER_COMMIT"):null);
            Stream input=Console.OpenStandardInput();if(mode=="held-cancel")input=new HeldCancelInput(input,()=>state="CancelHeld",cancelRelease.Task);
            control=await InstallationApplicationControl.AdmitAsync(input,output,launch,AppContext.BaseDirectory,Path.Combine(root,"user-data"));
        }
        catch(Exception error)when(error is IOException or InvalidDataException or FormatException or System.Text.Json.JsonException)
        {Console.Error.WriteLine("PUBLIC_START_REFUSED|"+error.GetType().Name);return 3;}
        File.AppendAllText(Path.Combine(root,"PUBLIC_SHELL_CREATED"),"1\n");
        using var pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.InOut,PipeOptions.Asynchronous);await pipe.ConnectAsync(5000);
        using var reader=new StreamReader(pipe);using var writer=new StreamWriter(pipe){AutoFlush=true};
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var cleanup=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        if(mode!="held-ready")ready.TrySetResult();
        if(mode=="bad-ready")ready=new(TaskCreationOptions.RunContinuationsAsynchronously);if(mode=="bad-ready")ready.TrySetException(new IOException("PUBLIC_BAD_READY"));
        if(mode!="held-close")cleanup.TrySetResult();
        var failedCloseRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var retryWhilePending=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state="Created";bool failedClose=false;
        var closeRequests=Channel.CreateBounded<byte>(new BoundedChannelOptions(1){SingleReader=true});
        // Keep and await each original attempt; coalesce at most one retry while it is still pending.
        Task closeWorker=Task.Run(async()=>{while(await closeRequests.Reader.WaitToReadAsync())if(closeRequests.Reader.TryRead(out _)){Task originalClose=Close();await originalClose;}});
        _=closeWorker.ContinueWith(t=>{_ =t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        Task RequestClose(CancellationToken _){if(state=="CleanupUnconfirmed")retryWhilePending.TrySetResult();closeRequests.Writer.TryWrite(0);return Task.CompletedTask;}
        Task loop=control.RunAsync(ready.Task,RequestClose);_ =loop.ContinueWith(t=>{_ =t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        while(await reader.ReadLineAsync() is {} command)
        {
            if(command=="PING")await writer.WriteLineAsync("PUBLIC_SHELL|"+state+(control.FailureCode==null?"":"|ControlFault="+control.FailureCode));
            else if(command=="READY")ready.TrySetResult();
            else if(command=="CLOSE")await RequestClose(CancellationToken.None);
            else if(command=="RELEASE_CLOSE")cleanup.TrySetResult();
            else if(command=="RELEASE_CANCEL")cancelRelease.TrySetResult();
            else if(command=="WAIT_RETRY"){await retryWhilePending.Task;await writer.WriteLineAsync("PUBLIC_RETRY_WHILE_CLOSE_PENDING");}
            else if(command=="RELEASE_FAILED_CLOSE")failedCloseRelease.TrySetResult();
            else throw new InvalidDataException("Unknown public test command.");
        }
        GC.KeepAlive(control);Environment.Exit(90);return 90;
        async Task Close()
        {
            state="Closing";
            try
            {
                if(mode is "fail-close-once" or "held-failed-close"&&!failedClose){failedClose=true;throw new IOException("PUBLIC_CLEANUP_FAILED_ONCE");}
                await cleanup.Task;await control!.SettleForExitAsync();await writer.WriteLineAsync("PUBLIC_SHELL_CLOSED");await writer.FlushAsync();Environment.Exit(0);
            }
            catch(Exception error){state="CleanupUnconfirmed";await writer.WriteLineAsync("PUBLIC_CLOSE_UNCONFIRMED|"+error.GetType().Name);if(mode=="held-failed-close")await failedCloseRelease.Task;}
        }
    }
    private sealed class LostCommitOutput(Stream inner,Action? beforeFailure=null):Stream
    {
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override Task FlushAsync(CancellationToken ct)=>inner.FlushAsync(ct);
        public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)
        {if(buffer.Span.StartsWith("COMMITTED|"u8)){beforeFailure?.Invoke();throw new IOException("PUBLIC_COMMIT_OUTPUT_FAULT");}return inner.WriteAsync(buffer,cancellationToken);}
    }
    private sealed class HeldCancelInput(Stream inner,Action entered,Task release):Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            using var registration=cancellationToken.Register(()=>{entered();release.GetAwaiter().GetResult();});
            int read=await inner.ReadAsync(buffer,cancellationToken);if(read==0&&cancellationToken.CanBeCanceled)await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);return read;
        }
    }
}
