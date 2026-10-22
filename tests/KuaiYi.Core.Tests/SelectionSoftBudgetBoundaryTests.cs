using System.Diagnostics;
using System.Globalization;
using System.Runtime.ExceptionServices;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.NativeHost;
using Xunit;
using Xunit.Abstractions;
namespace KuaiYi.Core.Tests;

/// <summary>Real owned fixture/IPC/client/coordinator; substitutes only desktop-source and clipboard OS ports.</summary>
public sealed class SelectionSoftBudgetBoundaryTests(ITestOutputHelper output)
{
    private static readonly SelectionWindow Source=new(123,456,789,@"C:\Public\public.exe");
    private sealed class Starter(string fault):IOwnedProcessStarter
    {
        public int Starts;public Action<Process>? BeforeReturn;
        public Process Start(ProcessStartInfo original)
        {
            Starts++;string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            string assembly=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../tools/KuaiYi.CaptureControlFixture/bin",configuration,"net10.0/KuaiYi.CaptureControlFixture.dll"));
            var info=OwnedProcessSession.Info(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",null);info.ArgumentList.Clear();
            info.ArgumentList.Add(assembly);info.ArgumentList.Add("--selection-fixture");info.ArgumentList.Add(fault);foreach(string argument in original.ArgumentList)info.ArgumentList.Add(argument);
            var child=Process.Start(info)??throw new IOException("Public fixture failed to start.");BeforeReturn?.Invoke(child);return child;
        }
    }
    // One test clock owns both absolute elapsed checks and the original deadline cancellation.
    private sealed class ControlledTime:TimeProvider,IDisposable
    {
        private readonly object sync=new();private long ticks;private CancellationTokenSource? deadline;private long dueAt;
        public int Arms,Fires;public TimeSpan Due;
        public override long TimestampFrequency=>TimeSpan.TicksPerSecond;
        public override long GetTimestamp()=>Interlocked.Read(ref ticks);
        public override DateTimeOffset GetUtcNow()=>new DateTimeOffset(2026,10,22,0,0,0,TimeSpan.Zero)+TimeSpan.FromTicks(GetTimestamp());
        public void Arm(CancellationTokenSource source,TimeSpan due){lock(sync){if(deadline!=null)throw new InvalidOperationException("Already armed.");Arms++;Due=due;dueAt=checked(GetTimestamp()+due.Ticks);deadline=source;}}
        public void AdvanceTo(TimeSpan elapsed)
        {
            CancellationTokenSource? fire=null;lock(sync){if(elapsed.Ticks<ticks)throw new InvalidOperationException("Clock moved backwards.");Interlocked.Exchange(ref ticks,elapsed.Ticks);if(deadline!=null&&ticks>=dueAt){fire=deadline;deadline=null;Fires++;}}
            fire?.Cancel();
        }
        public void Dispose(){lock(sync)deadline=null;}
    }
    private sealed class Destination:SynchronizationContext
    {public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private sealed class Platform(SelectionBrokerClient broker):ISelectionCapturePlatform,ISelectionCopyPlatform
    {
        public SelectionCaptureResult? UiaResult;public int Copies,Inputs;
        public bool IsCurrent(SelectionWindow source)=>source==Source;
        public bool IsCopyCurrent(SelectionWindow source,SelectionCopyEvidence evidence)=>source==Source;
        public async Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation)=>UiaResult=await broker.CaptureAsync(gesture,cancellation);
        public Task<SelectionCaptureResult> CaptureCopiedAsync(SelectionGesture gesture,SelectionCopyEvidence evidence,Func<uint,ClipboardCopyStart?> issueCopy,Func<bool> current,CancellationToken cancellation)
        {
            Copies++;Assert.True(current());Assert.NotNull(issueCopy(10));cancellation.ThrowIfCancellationRequested();
            return Task.FromResult(new SelectionCaptureResult(SelectionCaptureStatus.Success,"PUBLIC copied",evidence.SelectionBounds,evidence.RuntimeId,evidence.ControlBounds,SourceKind:SelectionSourceKind.Clipboard,Recovery:ClipboardRecovery.Restored));
        }
        public ClipboardCopyStart? TryIssueCopy(SelectionGesture gesture,SelectionCopyEvidence evidence,uint sequence){Inputs++;return new(10000,42);}
    }
    [Theory]
    [InlineData(false,249.999,SelectionCaptureStatus.Success)]
    [InlineData(false,250,SelectionCaptureStatus.SoftTimeout)]
    [InlineData(false,250.001,SelectionCaptureStatus.SoftTimeout)]
    [InlineData(true,249.999,SelectionCaptureStatus.EmptyOrUnsupported)]
    [InlineData(true,250,SelectionCaptureStatus.ProtocolFailure)]
    [InlineData(true,250.001,SelectionCaptureStatus.ProtocolFailure)]
    public async Task BoundaryMetadataCannotDeliverExpiredTextOrAuthorizeCompatibilityCopy(bool copy,double elapsed,SelectionCaptureStatus expected)
    {
        string fault=(copy?"copy-soft:":"soft-text:")+elapsed.ToString("R",CultureInfo.InvariantCulture);
        using var time=new ControlledTime();using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var starter=new Starter(fault);var broker=new SelectionBrokerClient("public-fixture",starter,time){ArmDeadlineForTest=time.Arm};var platform=new Platform(broker);
        var settings=new SettingsSnapshot(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}],Privacy=new(){ClipboardAllowedApplications=[Source.Executable]}},"PUBLIC-settings",SettingsLoadState.Current);
        var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(settings);var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));
        SelectionSourceSnapshot? accepted=null;var failures=new List<SelectionCaptureStatus>();
        var coordinator=new SelectionCaptureCoordinator(gate,()=>settings,platform,new(4,4,500,4,4),999,new Destination(),(source,_)=>accepted=source,failures.Add,(_,_)=>Task.CompletedTask);
        var original=coordinator.ExplicitAsync(Source,new(70,3));using var abort=guard.Token.Register(()=>coordinator.CancelPending());
        List<Exception> errors=[];
        try
        {
            await original.WaitAsync(guard.Token);guard.Token.ThrowIfCancellationRequested();await broker.DisposeAsync();
            var result=Assert.IsType<SelectionCaptureResult>(platform.UiaResult);
            output.WriteLine(FormattableString.Invariant($"ReportedMs={elapsed:R}; Copy={copy}; WireStatus={result.Status}; Accepted={accepted!=null}; CopyTransactions={platform.Copies}; CopyInputs={platform.Inputs}; ChildStarts={starter.Starts}; CleanupSettled={broker.ExitState==null}"));
            Assert.Equal(1,time.Arms);Assert.Equal(TimeSpan.FromMilliseconds(600),time.Due);Assert.Equal(0,time.Fires);
            Assert.Equal(expected,result.Status);Assert.Equal(1,starter.Starts);Assert.Null(broker.ExitState);Assert.Equal(0,coordinator.PendingSourceCountForTest);
            bool timely=elapsed<250;
            Assert.Equal(timely,accepted!=null);Assert.Equal(copy&&timely?1:0,platform.Copies);Assert.Equal(copy&&timely?1:0,platform.Inputs);
            Assert.Equal(!copy&&timely?"public selected\n  code":"",result.Text);
            if(copy&&timely){Assert.NotNull(result.CopyEvidence);Assert.Equal(SelectionSourceKind.Clipboard,accepted!.SourceKind);Assert.Equal("PUBLIC copied",accepted.Text);}
            else Assert.Null(result.CopyEvidence);
            if(timely){Assert.Empty(failures);Assert.Equal(Source,accepted!.Window);}else Assert.Equal(expected,Assert.Single(failures));
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            try{coordinator.CancelPending();}catch(Exception error){errors.Add(error);}
            try{await original;}catch(Exception error){if(!errors.Any(old=>ReferenceEquals(old,error)))errors.Add(error);}
            try{await coordinator.DisposeAsync();}catch(Exception error){errors.Add(error);}
            try{await broker.DisposeAsync();}catch(Exception error){errors.Add(error);}
        }
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException("Original soft-boundary body and cleanup failed.",errors);
    }
    [Fact]public async Task IndependentHardBoundaryCanExpireBeforeConfigured249MetadataAndRetainsOriginalStartup()
    {
        using var time=new ControlledTime();using var release=new ManualResetEventSlim();using var guard=new CancellationTokenSource(TimeSpan.FromSeconds(5));var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var starter=new Starter("soft-text:249.999"){BeforeReturn=_=>{entered.TrySetResult();release.Wait();}};
        var broker=new SelectionBrokerClient("public-fixture",starter,time){ArmDeadlineForTest=time.Arm};var platform=new Platform(broker);var settings=new SettingsSnapshot(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}]},"PUBLIC-settings",SettingsLoadState.Current);var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(settings);var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));
        SelectionSourceSnapshot? accepted=null;var failures=new List<SelectionCaptureStatus>();var coordinator=new SelectionCaptureCoordinator(gate,()=>settings,platform,new(4,4,500,4,4),999,new Destination(),(source,_)=>accepted=source,failures.Add,(_,_)=>Task.CompletedTask);var original=coordinator.ExplicitAsync(Source,new(70,3));using var abort=guard.Token.Register(()=>coordinator.CancelPending());List<Exception> errors=[];
        try
        {
            await entered.Task.WaitAsync(guard.Token);Assert.False(original.IsCompleted);Assert.Equal(1,time.Arms);Assert.Equal(TimeSpan.FromMilliseconds(600),time.Due);time.AdvanceTo(TimeSpan.FromMilliseconds(600));await original.WaitAsync(guard.Token);
            Assert.Equal(1,time.Fires);Assert.Equal(SelectionCaptureStatus.HardTimeout,platform.UiaResult!.Status);Assert.Equal("",platform.UiaResult.Text);Assert.Null(broker.LastObservation);Assert.Null(accepted);Assert.Equal(0,platform.Copies);Assert.Equal(0,platform.Inputs);Assert.Equal(SelectionCaptureStatus.HardTimeout,Assert.Single(failures));Assert.Equal(HostExitState.ExitUnconfirmed,broker.ExitState);Assert.Equal(1,starter.Starts);
            output.WriteLine("ConfiguredReportedMs=249.999; OriginalStartupPending=True; MetadataRead=False; ControlledHardElapsedMs=600; Result=HardTimeout; Accepted=False; CopyTransactions=0");
        }
        catch(Exception error){errors.Add(error);}
        finally
        {
            release.Set();
            try{await original;}catch(Exception error){if(!errors.Any(old=>ReferenceEquals(old,error)))errors.Add(error);}
            try{await coordinator.DisposeAsync();}catch(Exception error){errors.Add(error);}
            try{await broker.DisposeAsync();}catch(Exception error){errors.Add(error);}
        }
        if(errors.Count==1)ExceptionDispatchInfo.Capture(errors[0]).Throw();if(errors.Count>1)throw new AggregateException("Original soft-boundary body and cleanup failed.",errors);Assert.Null(broker.ExitState);
    }

}
