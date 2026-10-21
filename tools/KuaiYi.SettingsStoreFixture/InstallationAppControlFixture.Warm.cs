using KuaiYi.Core.Installation;
internal static partial class InstallationAppControlFixture
{
    // All preparation/owner signals here are synthetic. Only the child process and its inherited stdio are real.
    private sealed class WarmFixture(string root,string mode)
    {
        private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource prepared=Signal(),prepareRelease=Signal(),cancelled=Signal(),cancelRelease=Signal(),writeEntered=Signal(),writeRelease=Signal();
        private InstallationWarmImageControl? actor;
        private int prepares,accepted,discarded,finalized;
        private volatile bool finalizeAllowed;
        private readonly TaskCompletionSource finalizeEntered=Signal();
        internal Stream Wrap(Stream output)=>new WarmOutput(output,this,mode);
        internal void Attach(InstallationApplicationControl control)
        {
            if(mode!="warm-held-work")prepareRelease.TrySetResult();if(mode!="warm-held-callback")cancelRelease.TrySetResult();
            finalizeAllowed=mode!="warm-false-finalize";
            actor=control.AttachWarm(TimeProvider.System,()=>41,async(admission,lease,path,token)=>
            {
                Interlocked.Increment(ref prepares);File.WriteAllText(Path.Combine(root,"PUBLIC_SYNTHETIC_WARM_OWNER"),"PUBLIC_HELD");
                using var registration=token.Register(()=>{cancelled.TrySetResult();cancelRelease.Task.GetAwaiter().GetResult();});
                prepared.TrySetResult();
                if(mode=="warm-held-callback")await cancelled.Task;
                await prepareRelease.Task; // Explicit noncooperative synthetic owner; original task remains held on cancellation.
                if(mode!="warm-false-finalize"&&admission.TryAccept(lease,()=>{Interlocked.Increment(ref accepted);return new(Guid.NewGuid(),1);}))return;
                Interlocked.Increment(ref discarded);
            },_=>
            {
                Interlocked.Increment(ref finalized);finalizeEntered.TrySetResult();if(!finalizeAllowed)return ValueTask.FromResult(false);File.Delete(Path.Combine(root,"PUBLIC_SYNTHETIC_WARM_OWNER"));return ValueTask.FromResult(true);
            });
        }
        internal async Task<bool> Command(string command,StreamWriter writer)
        {
            if(command=="WAIT_WARM_PREPARE"){await prepared.Task;await writer.WriteLineAsync("PUBLIC_SYNTHETIC_WARM_PREPARE");}
            else if(command=="RELEASE_WARM_WORK")prepareRelease.TrySetResult();
            else if(command=="WAIT_WARM_CALLBACK"){await cancelled.Task;await writer.WriteLineAsync("PUBLIC_SYNTHETIC_WARM_CANCEL_HELD");}
            else if(command=="RELEASE_WARM_CALLBACK")cancelRelease.TrySetResult();
            else if(command=="WAIT_WARM_WRITE"){await writeEntered.Task;await writer.WriteLineAsync("PUBLIC_ORIGINAL_WARM_WRITE_HELD");}
            else if(command=="RELEASE_WARM_WRITE")writeRelease.TrySetResult();
            else if(command=="WAIT_WARM_IDLE"){bool complete=await actor!.OriginalCompletion;await writer.WriteLineAsync(complete?"PUBLIC_WARM_ORIGINAL_SETTLED":"PUBLIC_WARM_CLEANUP_UNCONFIRMED");}
            else if(command=="WAIT_WARM_FINALIZE"){await finalizeEntered.Task;await writer.WriteLineAsync("PUBLIC_SYNTHETIC_FINALIZE_OBSERVED");}
            else if(command=="RELEASE_WARM_FINALIZE")finalizeAllowed=true;
            else if(command=="RETRY_WARM_CLEANUP")await writer.WriteLineAsync("PUBLIC_WARM_RETRY|"+actor!.RetryCleanup());
            else if(command=="WARM_STATUS")await writer.WriteLineAsync($"PUBLIC_WARM|busy={actor!.Admission.Busy}|prepares={prepares}|accepted={accepted}|discarded={discarded}|finalized={finalized}");
            else return false;return true;
        }
        private sealed class WarmOutput(Stream inner,WarmFixture owner,string mode):Stream
        {
            private bool result;
            public override bool CanRead=>false;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
            public override void Flush()=>throw new NotSupportedException();public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long v)=>throw new NotSupportedException();
            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken cancellationToken=default)
            {
                result=buffer.Span.StartsWith("WARM_RESULT|"u8);
                if(result&&mode=="warm-drop-result"){owner.writeEntered.TrySetResult();return;} // Deliberately lost receipt, not a production success.
                await inner.WriteAsync(buffer,cancellationToken);
                if(result&&mode=="warm-result-write"){owner.writeEntered.TrySetResult();await owner.writeRelease.Task;}
            }
            public override async Task FlushAsync(CancellationToken cancellationToken)
            {
                if(result&&mode=="warm-result-flush"){owner.writeEntered.TrySetResult();await owner.writeRelease.Task;}
                await inner.FlushAsync(cancellationToken);
            }
        }
    }
}
