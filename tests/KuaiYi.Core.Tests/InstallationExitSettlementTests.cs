using System.Reflection;
using System.Text;
using KuaiYi.Core.Installation;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class InstallationExitSettlementTests
{
    private static readonly TimeSpan Guard=TimeSpan.FromSeconds(5);
    private static TaskCompletionSource Signal()=>new(TaskCreationOptions.RunContinuationsAsynchronously);
    private static CancellationTokenSource Source(InstallationApplicationControl control,string name)=>(CancellationTokenSource)typeof(InstallationApplicationControl).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(control)!;
    private static InstallationApplicationControl Control(Stream input,Stream output,InstallationControlledLaunch proof)=>(InstallationApplicationControl)Activator.CreateInstance(typeof(InstallationApplicationControl),BindingFlags.Instance|BindingFlags.NonPublic,null,[input,output,proof],null)!;
    private sealed class Input(Exception? failure,Exception? readFailure=null):MemoryStream
    {
        internal readonly TaskCompletionSource Armed=Signal(),Entered=Signal(),Release=Signal(),Returned=Signal();
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken token=default)
        {
            await using var registration=token.Register(()=>{Entered.TrySetResult();Release.Task.GetAwaiter().GetResult();if(failure!=null)throw failure;});
            Armed.TrySetResult();try{await Entered.Task;if(readFailure!=null)throw readFailure;throw new OperationCanceledException(token);}finally{Returned.TrySetResult();}
        }
    }
    private sealed class Output:MemoryStream
    {
        internal readonly List<string> Lines=[];
        internal readonly TaskCompletionSource<InstallationWarmGrant> Grant=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken token=default)
        {
            string row=Encoding.ASCII.GetString(buffer.Span).TrimEnd('\n');lock(Lines)Lines.Add(row);
            if(row.StartsWith("WARM_GRANTED|",StringComparison.Ordinal))Grant.TrySetResult(InstallationWarmImageWire.Decode<InstallationWarmGrant>(row[13..]));return ValueTask.CompletedTask;
        }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OriginalWarmAndCancellationFailuresBothSurviveEitherBarrierOrder(bool warmFirst)
    {
        var warmError=new IOException("PUBLIC_WARM_ORIGINAL");var cancelError=new InvalidOperationException("PUBLIC_CANCEL_ORIGINAL");
        using var input=new Input(cancelError);using var output=new Output();var proof=InstallationWarmImageTests.Proof();var control=Control(input,output,proof);
        var entered=Signal();var release=Signal();var issuer=new InstallationWarmImageAdmission(proof,new InstallationWarmImageTests.Clock());var issued=issuer.Reserve(new(),4);
        var warm=control.AttachWarm(new InstallationWarmImageTests.Clock(),()=>4,async(_,_,_,_)=>{entered.TrySetResult();await release.Task;throw warmError;},_=>ValueTask.FromResult(true));
        var loop=control.RunAsync(Task.CompletedTask,_=>Task.CompletedTask);await input.Armed.Task.WaitAsync(Guard);
        warm.Handle("WARM_RESERVE|"+InstallationWarmImageWire.Encode(issued.Grant.Identity));var grant=await output.Grant.Task.WaitAsync(Guard);
        warm.Handle("WARM_COMMIT|"+InstallationWarmImageWire.Encode(new InstallationWarmSubmit(grant,"C:\\public\\a.png")));await entered.Task.WaitAsync(Guard);
        var settle=control.SettleForExitAsync();await input.Entered.Task.WaitAsync(Guard);
        try
        {
            Assert.Same(settle,control.SettleForExitAsync());Assert.True(warm.Admission.Busy);Assert.False(settle.IsCompleted);
            if(warmFirst){release.TrySetResult();await Assert.ThrowsAnyAsync<Exception>(()=>warm.OriginalCompletion.WaitAsync(Guard));Assert.False(settle.IsCompleted);input.Release.TrySetResult();}
            else{input.Release.TrySetResult();await loop.WaitAsync(Guard);Assert.False(settle.IsCompleted);release.TrySetResult();}
            var failure=await Assert.ThrowsAnyAsync<Exception>(()=>settle.WaitAsync(Guard));var leaves=Assert.IsType<AggregateException>(failure).Flatten().InnerExceptions;
            Assert.Contains(leaves,x=>ReferenceEquals(x,warmError));Assert.Contains(leaves,x=>ReferenceEquals(x,cancelError));
            Assert.True(warm.Admission.Busy);Assert.True(loop.IsCompleted);Assert.True(input.Returned.Task.IsCompleted);Assert.DoesNotContain(output.Lines,x=>x.StartsWith("EXIT_READY|",StringComparison.Ordinal));
        }
        finally
        {
            release.TrySetResult();input.Release.TrySetResult();await Record.ExceptionAsync(()=>settle.WaitAsync(Guard));await loop.WaitAsync(Guard);
            await issuer.Revoke(issued);Assert.True(await issuer.RetireAsync(issued,()=>ValueTask.FromResult(true),_=>Task.CompletedTask));
        }
    }
    [Fact]public async Task StopAndImageOriginalCancellationFailuresAreBothPreserved()
    {
        var stopError=new IOException("PUBLIC_STOP_ORIGINAL");var imageError=new InvalidOperationException("PUBLIC_IMAGE_ORIGINAL");
        using var input=new Input(stopError);using var output=new Output();var control=Control(input,output,InstallationWarmImageTests.Proof());
        var imageEntered=Signal();var imageRelease=Signal();await using var registration=Source(control,"imageStop").Token.Register(()=>{imageEntered.TrySetResult();imageRelease.Task.GetAwaiter().GetResult();throw imageError;});
        var loop=control.RunAsync(Task.CompletedTask,_=>Task.CompletedTask);await input.Armed.Task.WaitAsync(Guard);var settle=control.SettleForExitAsync();
        try
        {
            await input.Entered.Task.WaitAsync(Guard);await imageEntered.Task.WaitAsync(Guard);Assert.False(settle.IsCompleted);
            input.Release.TrySetResult();await loop.WaitAsync(Guard);Assert.False(settle.IsCompleted);imageRelease.TrySetResult();
            var failure=await Assert.ThrowsAnyAsync<Exception>(()=>settle.WaitAsync(Guard));var leaves=Assert.IsType<AggregateException>(failure).Flatten().InnerExceptions;
            Assert.Contains(leaves,x=>ReferenceEquals(x,stopError));Assert.Contains(leaves,x=>ReferenceEquals(x,imageError));Assert.DoesNotContain(output.Lines,x=>x.StartsWith("EXIT_READY|",StringComparison.Ordinal));
        }
        finally{input.Release.TrySetResult();imageRelease.TrySetResult();await Record.ExceptionAsync(()=>settle.WaitAsync(Guard));await loop.WaitAsync(Guard);}
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OneOriginalFailureAcrossOneOrTwoCancellationTasksIsRethrownAsTheSameObject(bool both)
    {
        var original=new IOException("PUBLIC_SAME_ORIGINAL");using var input=new Input(original);using var output=new Output();var control=Control(input,output,InstallationWarmImageTests.Proof());
        await using var registration=Source(control,"imageStop").Token.Register(()=>{if(both)throw original;});
        var loop=control.RunAsync(Task.CompletedTask,_=>Task.CompletedTask);await input.Armed.Task.WaitAsync(Guard);var settle=control.SettleForExitAsync();
        try
        {
            await input.Entered.Task.WaitAsync(Guard);input.Release.TrySetResult();
            var failure=await Assert.ThrowsAnyAsync<Exception>(()=>settle.WaitAsync(Guard));Assert.Same(original,failure);
            Assert.Contains(nameof(Input.ReadAsync),failure.StackTrace!);Assert.True(loop.IsCompleted);Assert.DoesNotContain(output.Lines,x=>x.StartsWith("EXIT_READY|",StringComparison.Ordinal));
        }
        finally{input.Release.TrySetResult();await Record.ExceptionAsync(()=>settle.WaitAsync(Guard));await loop.WaitAsync(Guard);}
    }
    [Fact]public async Task CancellationAndBothOriginalLoopFaultsAreCollectedWithoutSuccessReceipt()
    {
        var cancelError=new IOException("PUBLIC_CANCEL_ORIGINAL");var readError=new InvalidDataException("PUBLIC_READ_ORIGINAL");var closeError=new InvalidOperationException("PUBLIC_CLOSE_ORIGINAL");
        using var input=new Input(cancelError,readError);using var output=new Output();var control=Control(input,output,InstallationWarmImageTests.Proof());
        var loop=control.RunAsync(Task.CompletedTask,_=>Task.FromException(closeError));await input.Armed.Task.WaitAsync(Guard);var settle=control.SettleForExitAsync();
        try
        {
            await input.Entered.Task.WaitAsync(Guard);input.Release.TrySetResult();
            var failure=await Assert.ThrowsAnyAsync<Exception>(()=>settle.WaitAsync(Guard));var leaves=Assert.IsType<AggregateException>(failure).Flatten().InnerExceptions;
            Assert.Equal(3,leaves.Count);foreach(var original in new Exception[]{cancelError,readError,closeError})Assert.Contains(leaves,x=>ReferenceEquals(x,original));
            Assert.True(loop.IsFaulted);Assert.DoesNotContain(output.Lines,x=>x.StartsWith("EXIT_READY|",StringComparison.Ordinal));
        }
        finally{input.Release.TrySetResult();await Record.ExceptionAsync(()=>settle.WaitAsync(Guard));await Record.ExceptionAsync(()=>loop.WaitAsync(Guard));}
    }
}
