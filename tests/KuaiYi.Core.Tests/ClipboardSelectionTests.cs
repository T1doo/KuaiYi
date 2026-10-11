using KuaiYi.Core;
using KuaiYi.Core.Settings;
using Xunit;
namespace KuaiYi.Core.Tests;
public sealed class ClipboardSelectionTests
{
    private static readonly SelectionWindow Source=new(123,456,789,@"C:\Public\public.exe");
    private static readonly SelectionCopyEvidence Proof=new(234,[12,4],new(0,0,500,200),[new(0,0,100,20)],true);
    private static SettingsSnapshot Config(bool allowed=true,string version="public-version")=>new(new(){ActiveProfileId="public",Profiles=[new(){Id="public",BaseUrl="https://public.invalid",Model="public",Authentication=ApiAuthentication.None}],Privacy=new(){ClipboardAllowedApplications=allowed?[Source.Executable]:[]}},version,SettingsLoadState.Current);
    private static RuntimeGate Gate(SettingsSnapshot settings){var gate=new RuntimeGate(requireProfileBinding:true);gate.ObserveSettingsMetadata(settings);var origin=new EndpointOrigin(new Uri("https://public.invalid"));gate.ConsentTo(origin);Assert.True(gate.TryEnable(origin,true));return gate;}
    private sealed class Context:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private sealed class Platform:ISelectionCapturePlatform,ISelectionCopyPlatform
    {
        public int Uia,Copies,Inputs;public bool Current=true,Modifiers=true,CopyCurrent=true;public SelectionCaptureResult UiaResult=new(SelectionCaptureStatus.EmptyOrUnsupported,CopyEvidence:Proof);public Action? BeforeIssue,AfterIssue;public TaskCompletionSource? UiaBarrier;public ClipboardRecovery Recovery=ClipboardRecovery.Restored;
        public bool IsCurrent(SelectionWindow source)=>Current&&source==Source;
        public bool IsCopyCurrent(SelectionWindow source,SelectionCopyEvidence evidence)=>Current&&CopyCurrent&&source==Source;
        public async Task<SelectionCaptureResult> CaptureAsync(SelectionGesture gesture,CancellationToken cancellation){Uia++;if(UiaBarrier!=null)await UiaBarrier.Task;return UiaResult;}
        public Task<SelectionCaptureResult> CaptureCopiedAsync(SelectionGesture gesture,SelectionCopyEvidence evidence,Func<uint,ClipboardCopyStart?> issueCopy,Func<bool> current,CancellationToken cancellation)
        {Copies++;BeforeIssue?.Invoke();var sent=issueCopy(10);AfterIssue?.Invoke();return Task.FromResult(sent==null?new SelectionCaptureResult(SelectionCaptureStatus.EmptyOrUnsupported):new(SelectionCaptureStatus.Success,"public copied",evidence.SelectionBounds,evidence.RuntimeId,evidence.ControlBounds,SourceKind:SelectionSourceKind.Clipboard,Recovery:Recovery));}
        public ClipboardCopyStart? TryIssueCopy(SelectionGesture gesture,SelectionCopyEvidence evidence,uint sequence){if(!Current||!Modifiers)return null;Inputs++;return new(10000,42);}
    }
    private static SelectionCaptureCoordinator Coordinator(RuntimeGate gate,Func<SettingsSnapshot> settings,Platform platform,Action<SelectionSourceSnapshot,bool> accepted)=>new(gate,settings,platform,new(4,4,500,4,4),999,new Context(),accepted,_=>{},(_,_)=>Task.CompletedTask);
    [Theory][InlineData("unconsented")][InlineData("off")][InlineData("sensitive")][InlineData("no-proof")][InlineData("hard-timeout")][InlineData("unassociated-automatic")][InlineData("different-path")]
    public async Task UiAFailureNeverCopiesWithoutExactConsentAndCredibleEvidence(string reason)
    {
        var config=Config(reason!="unconsented");if(reason=="different-path")config=config with{Data=config.Data with{Privacy=new(){ClipboardAllowedApplications=[@"C:\Other\public.exe"]}}};var gate=Gate(config);var platform=new Platform();if(reason=="off")gate.Disable();if(reason=="sensitive")platform.UiaResult=new(SelectionCaptureStatus.Sensitive);if(reason=="no-proof")platform.UiaResult=new(SelectionCaptureStatus.EmptyOrUnsupported);if(reason=="hard-timeout")platform.UiaResult=new(SelectionCaptureStatus.HardTimeout);if(reason=="unassociated-automatic")platform.UiaResult=platform.UiaResult with{CopyEvidence=Proof with{AutomaticTrusted=false,SelectionBounds=[]}};
        await using var coordinator=Coordinator(gate,()=>config,platform,(_,_)=>throw new InvalidOperationException("No source expected."));
        if(reason=="unassociated-automatic"){var stamp=coordinator.FreezeInputIdentity(Guid.NewGuid());await coordinator.InputAsync(new(true,new(3,3),1000,false,Source,stamp));await coordinator.InputAsync(new(false,new(70,3),1010,false,Source,stamp));}else await coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(0,platform.Copies);Assert.Equal(0,platform.Inputs);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public async Task AuthorizedExplicitOrAssociatedAutomaticFallbackProducesActualClipboardSource(bool explicitCommand)
    {
        var config=Config();var gate=Gate(config);var platform=new Platform();SelectionSourceSnapshot? source=null;await using var coordinator=Coordinator(gate,()=>config,platform,(value,_)=>source=value);
        if(explicitCommand)await coordinator.ExplicitAsync(Source,new(70,3));else{var stamp=coordinator.FreezeInputIdentity(Guid.NewGuid());await coordinator.InputAsync(new(true,new(3,3),1000,false,Source,stamp));await coordinator.InputAsync(new(false,new(70,3),1010,false,Source,stamp));}
        Assert.Equal(1,platform.Uia);Assert.Equal(1,platform.Copies);Assert.Equal(1,platform.Inputs);Assert.NotNull(source);Assert.Equal(SelectionSourceKind.Clipboard,source.SourceKind);Assert.Equal(ClipboardRecovery.Restored,source.Recovery);Assert.Equal("public copied",source.Text);
    }
    [Theory][InlineData("off")][InlineData("pause")][InlineData("mode")][InlineData("revoke")][InlineData("source")][InlineData("modifiers")]
    public async Task CopyInputUsesFinalFrozenGateAndSourceAfterSnapshotReady(string change)
    {
        var config=Config();var gate=Gate(config);var platform=new Platform();int accepted=0;platform.BeforeIssue=()=>{switch(change){case "off":gate.Disable();break;case "pause":gate.SetSuspension(Suspension.Moving,true);break;case "mode":gate.SetMode(TriggerMode.Drop);break;case "revoke":config=Config(false,"revoked-version");gate.ObserveSettingsMetadata(config);break;case "source":platform.Current=false;break;default:platform.Modifiers=false;break;}};
        await using var coordinator=Coordinator(gate,()=>config,platform,(_,_)=>accepted++);await coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(1,platform.Copies);Assert.Equal(0,platform.Inputs);Assert.Equal(0,accepted);
    }
    [Theory][InlineData("off")][InlineData("lock")][InlineData("settings")]
    public async Task CopiedTextReturningAfterRevocationNeverReachesDestination(string change)
    {
        var config=Config();var gate=Gate(config);var platform=new Platform();int accepted=0;platform.AfterIssue=()=>{if(change=="off")gate.Disable();else if(change=="lock")gate.SetSessionLocked(true);else gate.SetSuspension(Suspension.Settings,true);};await using var coordinator=Coordinator(gate,()=>config,platform,(_,_)=>accepted++);await coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(1,platform.Inputs);Assert.Equal(0,accepted);
    }
    [Fact]public async Task UiAReturningAfterOffDoesNotEvenStartClipboardService()
    {
        var config=Config();var gate=Gate(config);var platform=new Platform{UiaBarrier=new(TaskCreationOptions.RunContinuationsAsynchronously)};await using var coordinator=Coordinator(gate,()=>config,platform,(_,_)=>throw new InvalidOperationException());var capture=coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(1,platform.Uia);gate.Disable();platform.UiaBarrier.SetResult();await capture;Assert.Equal(0,platform.Copies);Assert.Equal(0,platform.Inputs);
    }
    [Fact]public void ConsentIsExactFullPathAndDefaultOff()
    {
        Assert.False(new PrivacySettings().ClipboardFallbackDefault);Assert.False(ClipboardApplicationConsent.Allowed(Source.Executable,new()));Assert.True(ClipboardApplicationConsent.Allowed(Source.Executable,new(){ClipboardAllowedApplications=[@"c:\public\PUBLIC.EXE"]}));Assert.False(ClipboardApplicationConsent.Allowed(@"C:\Other\public.exe",new(){ClipboardAllowedApplications=[Source.Executable]}));Assert.False(ClipboardApplicationConsent.Allowed(Source.Executable,new(){ClipboardAllowedApplications=["public.exe"]}));
    }
    [Fact]public async Task AuthorizedExplicitSelectionInDropCanCopyWithoutStartingAutomaticCapture()
    {var config=Config();var gate=Gate(config);gate.SetMode(TriggerMode.Drop);var platform=new Platform();int accepted=0;await using var coordinator=Coordinator(gate,()=>config,platform,(_,_)=>accepted++);Assert.Null(coordinator.FreezeInputIdentity(Guid.NewGuid()));await coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(1,platform.Inputs);Assert.Equal(1,accepted);Assert.Equal(TriggerMode.Drop,gate.Snapshot.Mode);}
    [Fact]public async Task UnconfirmedRecoveryAfterOffReportsOnlyDiagnosticAndNeverCopiedSource()
    {var config=Config();var gate=Gate(config);var platform=new Platform{Recovery=ClipboardRecovery.Unconfirmed,AfterIssue=()=>gate.Disable()};int accepted=0,warnings=0;await using var coordinator=new SelectionCaptureCoordinator(gate,()=>config,platform,new(4,4,500,4,4),999,new Context(),(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask,recovery=>{Assert.Equal(ClipboardRecovery.Unconfirmed,recovery);warnings++;});await coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(0,accepted);Assert.Equal(1,warnings);Assert.Equal(Activation.Off,gate.Snapshot.Activation);}
    [Fact]public async Task SameWindowWithChangedFocusedControlCannotDeliverCopiedText()
    {var config=Config();var gate=Gate(config);var platform=new Platform();platform.AfterIssue=()=>platform.CopyCurrent=false;int accepted=0;await using var coordinator=Coordinator(gate,()=>config,platform,(_,_)=>accepted++);await coordinator.ExplicitAsync(Source,new(70,3));Assert.True(platform.IsCurrent(Source));Assert.Equal(1,platform.Inputs);Assert.Equal(0,accepted);}
    private sealed class QueuedContext:SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback,object?)> callbacks=[];
        public override void Post(SendOrPostCallback callback,object? state)=>callbacks.Enqueue((callback,state));
        public void Drain(){while(callbacks.TryDequeue(out var item))item.Item1(item.Item2);}
    }
    [Fact]public async Task CancelClearsQueuedCopiedSourceWithoutWaitingForDestinationToDrain()
    {
        var config=Config();var gate=Gate(config);var platform=new Platform();var context=new QueuedContext();int accepted=0;
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>config,platform,new(4,4,500,4,4),999,context,(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask);
        var capture=coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(1,coordinator.PendingSourceCountForTest);Assert.False(capture.IsCompleted);
        coordinator.CancelPending(true);Assert.Equal(0,coordinator.PendingSourceCountForTest);await capture;context.Drain();Assert.Equal(0,accepted);Assert.Equal(1,platform.Inputs);
    }
    [Theory][InlineData("off")][InlineData("focus")]
    public async Task QueuedCopiedSourceIsRecheckedAndReleasedAtActualDestination(string change)
    {
        var config=Config();var gate=Gate(config);var platform=new Platform();var context=new QueuedContext();int accepted=0;
        await using var coordinator=new SelectionCaptureCoordinator(gate,()=>config,platform,new(4,4,500,4,4),999,context,(_,_)=>accepted++,_=>{},(_,_)=>Task.CompletedTask);
        var capture=coordinator.ExplicitAsync(Source,new(70,3));Assert.Equal(1,coordinator.PendingSourceCountForTest);
        if(change=="off")gate.Disable();else platform.CopyCurrent=false;
        context.Drain();await capture;Assert.Equal(0,accepted);Assert.Equal(0,coordinator.PendingSourceCountForTest);Assert.Equal(1,platform.Inputs);
    }

}
