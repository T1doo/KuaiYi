using System.IO.Pipes;
using System.Text.Json;
using KuaiYi.Core.Installation;
internal static partial class InstallationAppControlFixture
{
    private static InstallationApplicationControl? control;
    internal static async Task<int> Run(Guid launch,string mode,string pipeName)
    {
        _=Task.Run(async()=>{await Task.Delay(TimeSpan.FromSeconds(40));Environment.Exit(91);});
        string root=Directory.GetParent(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory))!.Parent!.Parent!.FullName;
        var warmFixture=mode.StartsWith("warm-",StringComparison.Ordinal)?new WarmFixture(root,mode):null;
        string state="NotAdmitted";var cancelRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closingWritten=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);Task? originalLoop=null;
        Task? originalSettlement=null;var cancellationTrace=new CancellationTrace();CancellationTokenRegistration lateCancellation=default;
        // No UI/cleanup channel creation before the same production proof validation succeeds.
        try
        {
            Stream output=Console.OpenStandardOutput();if(warmFixture!=null)output=warmFixture.Wrap(output);if(mode is "lost-commit-output" or "unknown-after-commit")output=new LostCommitOutput(output,mode=="unknown-after-commit"?()=>File.WriteAllText(Path.Combine(root,"application","handoff-pending.json"),"PUBLIC_UNKNOWN_AFTER_COMMIT"):null);
            if(mode is "fault-close-pending" or "eof-close-pending")output=new ClosingBarrierOutput(output,closingWritten,()=>originalLoop!);
            if(mode.StartsWith("held-cancel",StringComparison.Ordinal))output=new HeldReadyReturnOutput(output,cancellationTrace,mode=="held-cancel-ready-return");
            Stream input=Console.OpenStandardInput();if(mode.StartsWith("held-cancel",StringComparison.Ordinal))input=new HeldCancelInput(input,()=>state="CancelHeld",cancelRelease.Task,cancellationTrace,mode=="held-cancel");
            if(mode=="cancel-loop-first")input=new CancellableControlInput(input);
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
        var cleanupEntered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state="Created";bool failedClose=false;var exitRelease=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var shell=new KuaiYi.App.ShellWindow(Settle,()=>{state="Closing";cleanupEntered.TrySetResult();},error=>
        {
            state="CleanupUnconfirmed";writer.WriteLine("PUBLIC_CLOSE_UNCONFIRMED|"+error.GetType().Name);
            if(mode=="held-failed-close")failedCloseRelease.Task.GetAwaiter().GetResult(); // Hold this exact reported attempt, not the control loop.
        },()=>
        {
            if(mode.StartsWith("held-cancel",StringComparison.Ordinal))File.WriteAllText(Path.Combine(root,"PUBLIC_CANCELLATION_TRACE.json"),cancellationTrace.Snapshot(originalLoop,originalSettlement));
            GC.KeepAlive(lateCancellation);
            writer.WriteLine("PUBLIC_SHELL_CLOSED");writer.Flush();if(mode=="held-after-close")exitRelease.Task.GetAwaiter().GetResult();Environment.Exit(0);
        });
        async Task RequestClose(CancellationToken _){if(state is "CleanupUnconfirmed" or "Closing")retryWhilePending.TrySetResult();shell.Close();if(mode is "fault-close-pending" or "eof-close-pending"&&control!.FailureCode!=null)await closingWritten.Task;}
        InstallationImageOffer? image=null;var imageReceived=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var imageRevoked=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        warmFixture?.Attach(control!);
        Task loop=originalLoop=control.RunAsync(ready.Task,RequestClose,(offer,token)=>
        {
            image=offer;token.Register(()=>{imageRevoked.TrySetResult();if(mode=="held-image-cancel")cancelRelease.Task.GetAwaiter().GetResult();});File.AppendAllText(Path.Combine(root,"PUBLIC_IMAGE_QUEUED"),"1\n");imageReceived.TrySetResult();
        });_ =loop.ContinueWith(t=>{_ =t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        if(mode=="cancel-loop-first")control.ExitCancellationStartedForTest=()=>loop;
        while(await reader.ReadLineAsync() is {} command)
        {
            if(warmFixture!=null&&await warmFixture.Command(command,writer))continue;
            if(command=="PING")await writer.WriteLineAsync("PUBLIC_SHELL|"+state+(control.FailureCode==null?"":"|ControlFault="+control.FailureCode));
            else if(command=="WAIT_IMAGE"){await imageReceived.Task;await writer.WriteLineAsync($"PUBLIC_IMAGE|{image!.Request:N}|{image.Path}");}
            else if(command=="WAIT_IMAGE_REVOKED"){await imageRevoked.Task;await writer.WriteLineAsync("PUBLIC_IMAGE_REVOKED");}
            else if(command=="EXIT0")Environment.Exit(0);
            else if(command=="EXIT23")Environment.Exit(23);
            else if(command.StartsWith("TERMINAL_",StringComparison.Ordinal))
            {
                string closing=$"CLOSING|{control.Proof.Launch:N}|{control.Proof.Fingerprint}\n",terminal=$"EXIT_READY|{control.Proof.Launch:N}|{control.Proof.Fingerprint}\n";
                Console.Write(command switch{"TERMINAL_MISSING"=>closing,"TERMINAL_WRONG"=>closing+$"EXIT_READY|{Guid.Empty:N}|{control.Proof.Fingerprint}\n","TERMINAL_DUPLICATE"=>closing+terminal+terminal,"TERMINAL_EARLY"=>terminal+closing,_=>throw new InvalidDataException("Unknown terminal fixture.")});Console.Out.Flush();Environment.Exit(0);
            }
            else if(command=="EXTRA_OUTPUT")
            {Console.WriteLine("PUBLIC_UNEXPECTED_CONTROL_ROW");Console.Out.Flush();await writer.WriteLineAsync("PUBLIC_OUTPUT_WRITTEN");}
            else if(command=="WRONG_CLOSING")
            {Console.WriteLine($"CLOSING|{Guid.Empty:N}|{control.Proof.Fingerprint}");Console.Out.Flush();await writer.WriteLineAsync("PUBLIC_OUTPUT_WRITTEN");}
            else if(command=="DUPLICATE_CLOSING")
            {string closing=$"CLOSING|{control.Proof.Launch:N}|{control.Proof.Fingerprint}\n";Console.Write(closing+closing);Console.Out.Flush();}
            else if(command=="RELEASE_EXIT")exitRelease.TrySetResult();
            else if(command=="READY")ready.TrySetResult();
            else if(command=="CLOSE")
            {
                if(mode=="held-cancel-before-callback")
                {
                    await cancellationTrace.Armed.Task;
                    lateCancellation=cancellationTrace.Token.Register(()=>
                    {Interlocked.Increment(ref cancellationTrace.LateEntered);cancellationTrace.Disposed.Task.GetAwaiter().GetResult();Interlocked.Increment(ref cancellationTrace.LateReturned);});
                }
                await RequestClose(CancellationToken.None);
            }
            else if(command=="WAIT_READ_ARMED"){await cancellationTrace.Armed.Task;await writer.WriteLineAsync("PUBLIC_READ_ARMED");}
            else if(command=="RELEASE_CLOSE")cleanup.TrySetResult();
            else if(command=="RELEASE_CANCEL")cancelRelease.TrySetResult();
            else if(command=="WAIT_CLEANUP"){await cleanupEntered.Task;await writer.WriteLineAsync("PUBLIC_CLEANUP_ENTERED");}
            else if(command=="WAIT_RETRY"){await retryWhilePending.Task;await writer.WriteLineAsync("PUBLIC_RETRY_WHILE_CLOSE_PENDING");}
            else if(command=="RELEASE_FAILED_CLOSE")failedCloseRelease.TrySetResult();
            else throw new InvalidDataException("Unknown public test command.");
        }
        GC.KeepAlive(control);Environment.Exit(90);return 90;
        async Task Settle()
        {
            if(mode=="held-cleanup-fails-once"&&!failedClose)await failedCloseRelease.Task;
            if(mode is "fail-close-once" or "held-failed-close" or "held-cleanup-fails-once"&&!failedClose){failedClose=true;throw new IOException("PUBLIC_CLEANUP_FAILED_ONCE");}
            await cleanup.Task;if(mode=="ended-loop-first")await originalLoop!;
            if(mode=="cancel-loop-first")await Task.WhenAll(Enumerable.Range(0,8).Select(_=>control!.SettleForExitAsync()));
            originalSettlement=control!.SettleForExitAsync();await originalSettlement;
        }
    }
    private sealed class CancellationTrace
    {
        internal readonly TaskCompletionSource Armed=new(TaskCreationOptions.RunContinuationsAsynchronously),Disposed=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken Token;
        internal int ReadyWritten,ReadyReturned,ReadyCancelled,ReadEntered,RegistrationArmed,CallbackEntered,CallbackReturned,InnerReturned,ReadCancelled,RegistrationRetained,DisposeStarted,DisposeReturned,LateEntered,LateReturned;
        internal string Snapshot(Task? loop,Task? settlement)=>JsonSerializer.Serialize(new
        {
            ReadyWritten,ReadyReturned,ReadyCancelled,ReadEntered,RegistrationArmed,CallbackEntered,CallbackReturned,InnerReturned,ReadCancelled,RegistrationRetained,DisposeStarted,DisposeReturned,LateEntered,LateReturned,
            OriginalLoop=loop?.Status.ToString(),OriginalSettlement=settlement?.Status.ToString()
        });
    }
    // The real READY bytes/flush are visible, but the same original Send cannot finish before its stop token cancels.
    private sealed class HeldReadyReturnOutput(Stream inner,CancellationTrace trace,bool hold):Stream
    {
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override Task FlushAsync(CancellationToken ct)=>inner.FlushAsync(ct);
        public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)
        {
            await inner.WriteAsync(buffer,cancellationToken);
            if(buffer.Span.StartsWith("READY|"u8))
            {
                await inner.FlushAsync(cancellationToken);Interlocked.Increment(ref trace.ReadyWritten);
                try{if(hold)await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);}
                catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){Interlocked.Increment(ref trace.ReadyCancelled);throw;}
                Interlocked.Increment(ref trace.ReadyReturned);
            }
        }
    }
    private sealed class CancellableControlInput(Stream inner):Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            if(!cancellationToken.CanBeCanceled)return await inner.ReadAsync(buffer,cancellationToken); // Actual admission on original stdin.
            await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);return 0; // Explicit cancellable command-read port, no abandoned underlying read.
        }
    }
    private sealed class ClosingBarrierOutput(Stream inner,TaskCompletionSource closing,Func<Task> original):Stream
    {
        public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>inner.Flush();public override Task FlushAsync(CancellationToken ct)=>inner.FlushAsync(ct);
        public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)
        {
            await inner.WriteAsync(buffer,cancellationToken);
            if(buffer.Span.StartsWith("CLOSING|"u8)){closing.TrySetResult();try{await original();}catch(Exception error)when(error is InvalidDataException){ /* Observe the exact protocol fault before exit settlement continues. */ }}
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
    private sealed class HeldCancelInput(Stream inner,Action entered,Task release,CancellationTrace trace,bool retain):Stream
    {
        public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken cancellationToken=default)
        {
            if(!cancellationToken.CanBeCanceled)return await inner.ReadAsync(buffer,cancellationToken);
            Interlocked.Increment(ref trace.ReadEntered);trace.Token=cancellationToken;
            var callbackStarted=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); // This registration only; never a prior Read's signal.
            var registration=cancellationToken.Register(()=>
            {Interlocked.Increment(ref trace.CallbackEntered);callbackStarted.TrySetResult();entered();try{release.GetAwaiter().GetResult();}finally{Interlocked.Increment(ref trace.CallbackReturned);}});
            Interlocked.Increment(ref trace.RegistrationArmed);
            try
            {
                var originalRead=inner.ReadAsync(buffer,cancellationToken);trace.Armed.TrySetResult();
                int read=await originalRead;Interlocked.Increment(ref trace.InnerReturned);
                if(read==0)await Task.Delay(Timeout.InfiniteTimeSpan,cancellationToken);return read;
            }
            catch(OperationCanceledException)when(cancellationToken.IsCancellationRequested){Interlocked.Increment(ref trace.ReadCancelled);throw;}
            finally
            {
                if(retain&&cancellationToken.IsCancellationRequested){Interlocked.Increment(ref trace.RegistrationRetained);await callbackStarted.Task;}
                Interlocked.Increment(ref trace.DisposeStarted);registration.Dispose();Interlocked.Increment(ref trace.DisposeReturned);trace.Disposed.TrySetResult();
            }
        }
    }
}
