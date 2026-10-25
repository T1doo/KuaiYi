using System.Text;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;

// Component counterexamples only: held original stream tasks, not a real UI/Windows acceptance.
public sealed partial class InstallationLauncherSettlementTests
{
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private sealed class Output:Stream
    {
        internal readonly TaskCompletionSource Held=Signal(),Release=Signal();internal readonly List<string> Lines=[];internal int Writes;internal bool HoldFirst,HoldClosing;
        public override bool CanRead=>false;public override bool CanWrite=>true;public override bool CanSeek=>false;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> data,CancellationToken cancellation=default)
        {
            int ordinal=Interlocked.Increment(ref Writes);string row=Encoding.ASCII.GetString(data.Span);lock(Lines)Lines.Add(row);
            if(HoldFirst&&ordinal==1||HoldClosing&&row.StartsWith("CONTROLLER_CLOSING",StringComparison.Ordinal)){Held.TrySetResult();await Release.Task;}
        }
        public override Task FlushAsync(CancellationToken cancellation)=>Task.CompletedTask;
        public override void Flush(){}public override int Read(byte[] b,int o,int c)=>throw new NotSupportedException();public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();public override void Write(byte[] b,int o,int c)=>throw new NotSupportedException();
    }
    private sealed class Input(byte[] bytes):MemoryStream(bytes)
    {
        internal readonly TaskCompletionSource End=Signal();
        internal readonly TaskCompletionSource<byte[]> Tail=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool DeferredTail;
        private int tailPosition;
        public override async ValueTask<int> ReadAsync(Memory<byte> target,CancellationToken cancellation=default)
        {
            if(Position<Length)return await base.ReadAsync(target,cancellation);
            if(DeferredTail)
            {
                byte[] tail=await Tail.Task.WaitAsync(cancellation);int count=Math.Min(target.Length,tail.Length-tailPosition);
                if(count>0){tail.AsMemory(tailPosition,count).CopyTo(target);tailPosition+=count;return count;}
            }
            await End.Task.WaitAsync(cancellation);return 0;
        }
    }
    private static (InstallationStableSelection Selection,Input Input) Entry()
    {
        string root=Path.Combine(Path.GetTempPath(),"PUBLIC_COMPONENT_"+Guid.NewGuid().ToString("N"));string selected=Path.Combine(root,"versions",Guid.NewGuid().ToString("N"));
        var selection=new InstallationStableSelection(Guid.NewGuid(),root,root+"-data",new("1.0",selected,new string('0',64)),Convert.ToBase64String("PUBLIC_CURRENT"u8.ToArray()));
        return(selection,new Input(Encoding.ASCII.GetBytes("LAUNCH_CURRENT|"+selection.Encode()+"\n")));
    }
    [Fact]public async Task BridgeCannotOverlapClosingWithTheOriginalReadyWriteOrReturnBeforeItSettles()
    {
        var (selection,input)=Entry();using(input)using(var output=new Output{HoldFirst=true})
        {
            var original=new IOException("PUBLIC_ORIGINAL_SESSION_ERROR");Task? published=null;
            Task<int> run=InstallationControllerBridge.RunAsync(input,output,selection.Nonce,selection.Version.DirectoryPath,selection.UserData,(s,ready,_)=>
            {
                var proof=new InstallationControlledLaunch(Guid.NewGuid(),"KuaiYi.App.exe",s.Root,s.UserData,s.Version,"","",null);
                published=ready(proof,CancellationToken.None);return Task.FromException<int>(original);
            });
            try{await output.Held.Task;Assert.Equal(1,output.Writes);Assert.False(run.IsCompleted);}
            finally{input.End.TrySetResult();output.Release.TrySetResult();if(published!=null)await published;await Record.ExceptionAsync(()=>run);}
        }
    }
    [Fact]public async Task BridgeObservationTimeoutCannotDisposeTheOwnerOfAnUnfinishedClosingWrite()
    {
        var (selection,input)=Entry();using(input)using(var output=new Output{HoldClosing=true})
        {
            var observed=Signal();var timeout=new TimeoutException("PUBLIC_CONTROLLED_OBSERVATION_TIMEOUT");
            Task<int> run=InstallationControllerBridge.RunAsync(input,output,selection.Nonce,selection.Version.DirectoryPath,selection.UserData,(_,_,_)=>Task.FromException<int>(new IOException("PUBLIC_SESSION_FAILURE")),
                original=>{if(original.IsCompleted)return original;observed.TrySetResult();return Task.FromException(timeout);});
            try{await output.Held.Task;await observed.Task;Assert.False(run.IsCompleted);}
            finally{input.End.TrySetResult();output.Release.TrySetResult();await Record.ExceptionAsync(()=>run);}
        }
    }
    [Fact]public async Task OriginalSessionAndOriginalCancellationCallbackFailuresAreBothPreserved()
    {
        var (selection,input)=Entry();using(input)using(var output=new Output())
        {
            var sessionFailure=new IOException("PUBLIC_ORIGINAL_SESSION_FAILURE");var callbackFailure=new InvalidOperationException("PUBLIC_ORIGINAL_CANCEL_CALLBACK_FAILURE");
            var cancelled=Signal();var session=new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);CancellationTokenRegistration registration=default;
            Task<int> run=InstallationControllerBridge.RunAsync(input,output,selection.Nonce,selection.Version.DirectoryPath,selection.UserData,(_,_,token)=>
            {registration=token.Register(()=>{cancelled.TrySetResult();throw callbackFailure;});return session.Task;});
            try
            {
                input.End.TrySetResult();await cancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));session.TrySetException(sessionFailure);
                var failure=await Assert.ThrowsAsync<AggregateException>(()=>run.WaitAsync(TimeSpan.FromSeconds(5)));
                Assert.Contains(failure.Flatten().InnerExceptions,error=>ReferenceEquals(sessionFailure,error));
                Assert.Contains(failure.Flatten().InnerExceptions,error=>ReferenceEquals(callbackFailure,error));
                Assert.DoesNotContain(output.Lines,row=>row.StartsWith("CONTROLLER_EXIT_READY",StringComparison.Ordinal));
            }
            finally{input.End.TrySetResult();session.TrySetException(sessionFailure);await Record.ExceptionAsync(()=>run);registration.Dispose();}
        }
    }
    [Fact]public async Task DisposedStableOwnerCannotLaterStartAQueuedRun()
    {
        string root=Path.Combine(Path.GetTempPath(),"PUBLIC_NO_STORE_"+Guid.NewGuid().ToString("N"));var launcher=new InstallationStableLauncher(root,root+"-data");await launcher.DisposeAsync();Task<int>? started=null;
        try{Assert.Throws<InvalidOperationException>(()=>{started=launcher.RunAsync();});}
        finally{if(started!=null)await Record.ExceptionAsync(()=>started);_ =launcher.Ready.Exception;}
    }
}
