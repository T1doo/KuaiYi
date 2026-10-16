extern alias ImageProduction;
extern alias WorkerProduction;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using KuaiYi.Contracts;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Xunit;
using P=ImageProduction::KuaiYi.Imaging;
using Session=WorkerProduction::KuaiYi.ImageWorker.ImageWorkerSession;
namespace KuaiYi.Provider.Tests;

public sealed class OcrCapabilitiesDiscoveryTests
{
    internal static P.WorkerDiagnostic Good(string[]? languages=null,uint max=2048)=>new(new(true,true,"KuaiYi.Development_synthetic","KuaiYi.Development_synthetic!ImageWorker",P.ImageFailure.None),languages??["en-US","zh-Hans"],max,P.ImageFailure.None);
    private sealed class Immediate:SynchronizationContext{public override void Post(SendOrPostCallback callback,object? state)=>callback(state);}
    private sealed class Deferred:SynchronizationContext
    {
        private readonly ConcurrentQueue<Action> queue=new();public int Count=>queue.Count;
        public override void Post(SendOrPostCallback callback,object? state)=>queue.Enqueue(()=>callback(state));
        public void Drain(){while(queue.TryDequeue(out var action))action();}
    }
    private sealed class Worker:P.ILocalOcrCapabilitiesWorker
    {
        public P.WorkerDiagnostic Diagnostic=Good();public bool Hold,Finalized=true,Disposed,WrongJob;
        public int Diagnostics,Recognitions;public string? Language;public ReadOnlyMemory<byte> Borrowed;
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<P.OcrDiagnosticReply> DiagnoseAsync(P.OcrDiagnosticJob job,CancellationToken cancellation=default)
        {Diagnostics++;Entered.TrySetResult();if(Hold)await Release.Task;return new(WrongJob?job with{Epoch=job.Epoch+1}:job,Diagnostic);}
        public Task<P.ImageReply> RunAsync(ReadOnlyMemory<byte> image,P.ImageJob job,CancellationToken cancellation=default)
        {Recognitions++;Language=job.Language;Borrowed=image;return Task.FromResult(new P.ImageReply(job,P.ImageFailure.None,new(8,8,job.Language,"PUBLIC SYNTHETIC OCR CAPABILITY TEXT",[])));}
        public ValueTask<bool> TryFinalizeAsync()=>ValueTask.FromResult(Finalized);
        public ValueTask DisposeAsync(){Disposed=true;return ValueTask.CompletedTask;}
    }
    private sealed class Rig:IAsyncDisposable
    {
        public readonly PhaseHttpFixture Server=new();public PhaseHttpFixture.Script Script=null!;public readonly RuntimeGate Gate=new(requireProfileBinding:true);
        public SettingsSnapshot Settings=null!;public readonly ProviderClient Provider=new();public OverlayResults Results=null!;public TextTranslationCoordinator Text=null!;public P.OcrOnlyCoordinator Local=null!;
        public P.OwnedSourceImage Image=new(new P.ScreenshotPixels(new(0,0,8,8),Enumerable.Repeat((byte)255,256).ToArray()));public Guid Version=Guid.NewGuid();public bool Current=true;
        public Worker DiagnosticWorker=new(),RecognitionWorker=new();public int Factories;public Func<P.ILocalOcrWorker>? Factory=null;public Action? SourceProof=null;
        public static async Task<Rig> Create(KuaiYi.Core.Settings.ApiProtocol protocol=KuaiYi.Core.Settings.ApiProtocol.OpenAIChat,SynchronizationContext? destination=null)
        {
            var r=new Rig();await r.Server.InitializeAsync();r.Script=r.Server.Add("runs","application/json");var prefix=new Uri(r.Server.BaseUri,$"runs/{Guid.NewGuid():N}/fragmented/");var boundary=DevelopmentTranslationBoundary.FromPrefix(prefix);
            r.Settings=new(new(){ActiveProfileId="caps-fixture",Profiles=[new(){Id="caps-fixture",Protocol=protocol,BaseUrl=prefix.AbsoluteUri,Model=DevelopmentTranslationBoundary.FixtureModel,Authentication=ApiAuthentication.None,Streaming=false,Capabilities=new(){Vision=Capability.Unsupported}}]},"caps-synthetic",SettingsLoadState.Current);
            r.Gate.ObserveSettingsMetadata(r.Settings);r.Gate.ConsentTo(new(prefix));Assert.True(r.Gate.TryEnable(new(prefix),true));r.Results=new(r.Gate);
            r.Local=new(r.Gate,()=>r.Settings,(version,source)=>{r.SourceProof?.Invoke();return r.Current&&version==r.Version&&source==r.Image.SourceId;},destination??new Immediate(),()=>{r.Factories++;return r.Factory?.Invoke()??(r.Factories==1?r.DiagnosticWorker:r.RecognitionWorker);});
            r.Text=new(r.Gate,()=>r.Settings,(_,_)=>true,r.Provider,new Immediate(),r.Results,_=>{},developmentBoundary:boundary,ocrSourceCurrent:r.Local.IsApprovedSourceCurrent);return r;
        }
        public async Task<P.OcrCapabilitiesSnapshot> Discover()
        {Assert.True(Local.Discover(Version,Image));await Local.Running.WaitAsync(TimeSpan.FromSeconds(6));return Assert.IsType<P.OcrCapabilitiesSnapshot>(Local.Capabilities);}
        public async ValueTask DisposeAsync()
        {Gate.Disable();DiagnosticWorker.Release.TrySetResult();DiagnosticWorker.Finalized=RecognitionWorker.Finalized=true;await Local.DisposeAsync();await Text.DisposeAsync();Image.Dispose();Provider.Dispose();await Server.DisposeAsync();}
    }
    [Theory][InlineData(KuaiYi.Core.Settings.ApiProtocol.OpenAIChat)][InlineData(KuaiYi.Core.Settings.ApiProtocol.AnthropicMessages)]
    public async Task InstalledLanguageDiscoveryAndSelectionFeedExistingExplicitTextFlow(KuaiYi.Core.Settings.ApiProtocol protocol)
    {
        await using var rig=await Rig.Create(protocol);var caps=await rig.Discover();Assert.Equal(new[]{"en-US","zh-Hans"},caps.Languages);Assert.Equal((uint)2048,caps.MaxImageDimension);
        Assert.True(rig.DiagnosticWorker.Disposed);Assert.Equal(1,rig.DiagnosticWorker.Diagnostics);Assert.Equal(0,rig.DiagnosticWorker.Recognitions);Assert.Equal(0,rig.Script.Requests);Assert.False(rig.Gate.HasRunningGeneration);
        rig.DiagnosticWorker.Diagnostic.Languages[1]="changed";Assert.Equal("zh-Hans",caps.Languages[1]);Assert.False(rig.Local.Start(caps with{},"zh-Hans"));
        Assert.False(rig.Local.Start(caps,"not-installed"));Assert.Equal(1,rig.Factories);Assert.True(rig.Local.Start(caps,"zh-Hans"));await rig.Local.Running;
        Assert.Null(rig.Local.Capabilities);Assert.Equal("zh-Hans",rig.RecognitionWorker.Language);Assert.Equal(0,rig.RecognitionWorker.Diagnostics);Assert.Equal(1,rig.RecognitionWorker.Recognitions);Assert.Equal(0,rig.Script.Requests);Assert.True(rig.RecognitionWorker.Borrowed.Span.IndexOfAnyExcept((byte)0)<0);
        var preview=rig.Local.Preview!;Assert.NotNull(preview);var source=rig.Local.Approve(preview)!;var offer=rig.Text.PrepareOcr(source,TranslationPreset.Explain)!;
        Assert.Equal(TextSubmission.ConfirmationRequired,rig.Text.Submit(offer));Assert.Equal(0,rig.Script.Requests);Assert.Equal(TextSubmission.Started,rig.Text.Submit(offer,true));await rig.Script.Received.Task.WaitAsync(TimeSpan.FromSeconds(6));
        using var json=JsonDocument.Parse(rig.Script.Body);var user=json.RootElement.GetProperty("messages")[protocol==KuaiYi.Core.Settings.ApiProtocol.OpenAIChat?1:0].GetProperty("content");Assert.Equal(preview.Text,user.ValueKind==JsonValueKind.String?user.GetString():user[0].GetProperty("text").GetString());Assert.DoesNotContain("image_url",rig.Script.Body);Assert.DoesNotContain("base64",rig.Script.Body);
        string reply=protocol==KuaiYi.Core.Settings.ApiProtocol.OpenAIChat?"{\"choices\":[{\"index\":0,\"message\":{\"content\":\"PUBLIC SYNTHETIC TRANSLATION\"},\"finish_reason\":\"stop\"}]}":"{\"type\":\"message\",\"content\":[{\"type\":\"text\",\"text\":\"PUBLIC SYNTHETIC TRANSLATION\"}],\"stop_reason\":\"end_turn\"}";
        await rig.Script.Write(reply,true);await rig.Text.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("PUBLIC SYNTHETIC TRANSLATION",rig.Results.Text);Assert.Equal(2,rig.Factories);
    }
    [Theory][InlineData(P.ImageFailure.UnsupportedPlatform)][InlineData(P.ImageFailure.IdentityMissing)][InlineData(P.ImageFailure.IdentityMismatch)][InlineData(P.ImageFailure.NativeUnavailable)][InlineData(P.ImageFailure.LanguageUnavailable)]
    public async Task UnavailableDiagnosticBlocksRecognitionAndAllHttp(P.ImageFailure failure)
    {
        await using var rig=await Rig.Create();rig.DiagnosticWorker.Diagnostic=new(new(failure!=P.ImageFailure.UnsupportedPlatform,false,null,null,failure),[],0,failure);
        Assert.True(rig.Local.Discover(rig.Version,rig.Image));await rig.Local.Running;Assert.Null(rig.Local.Capabilities);Assert.Null(rig.Local.Preview);Assert.Equal(failure.ToString(),rig.Local.Failure);Assert.Equal(0,rig.DiagnosticWorker.Recognitions);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.Script.Requests);Assert.False(rig.Gate.HasRunningGeneration);
    }
    [Fact] public async Task EmptyInstalledListIsLanguageUnavailableWithoutGuessingOrRecognition()
    {await using var rig=await Rig.Create();rig.DiagnosticWorker.Diagnostic=Good([]);Assert.True(rig.Local.Discover(rig.Version,rig.Image));await rig.Local.Running;Assert.Null(rig.Local.Capabilities);Assert.Equal("LanguageUnavailable",rig.Local.Failure);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.DiagnosticWorker.Recognitions);Assert.Equal(0,rig.Script.Requests);}
    [Theory][InlineData(7)][InlineData(8)]
    public async Task NativeMaximumIsConsumedBeforeRecognitionAndExactContactIsAllowed(int maximum)
    {
        await using var rig=await Rig.Create();rig.DiagnosticWorker.Diagnostic=Good(max:(uint)maximum);var caps=await rig.Discover();bool started=rig.Local.Start(caps,"en-US");Assert.Equal(maximum==8,started);
        if(started){await rig.Local.Running;Assert.Equal(1,rig.RecognitionWorker.Recognitions);Assert.NotNull(rig.Local.Preview);}else{Assert.Equal("OcrDimensionExceeded",rig.Local.Failure);Assert.Equal(1,rig.Factories);}Assert.Equal(0,rig.Script.Requests);
    }
    [Theory][InlineData("settings")][InlineData("mode")][InlineData("off")][InlineData("locked")][InlineData("hidden")][InlineData("source")][InlineData("clear")]
    public async Task ExpiredInstalledLanguageOfferCannotStartRecognition(string reason)
    {await using var rig=await Rig.Create();var caps=await rig.Discover();Invalidate(rig,reason);Assert.False(rig.Local.IsCapabilitiesCurrent(caps));Assert.False(rig.Local.Start(caps,"en-US"));Assert.Null(rig.Local.Capabilities);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.Script.Requests);}
    private static void Invalidate(Rig rig,string reason)
    {
        switch(reason){case "settings":rig.Settings=rig.Settings with{Version="changed"};rig.Gate.ApplyCommittedSettings(rig.Settings,null);break;case "mode":rig.Gate.SetMode(TriggerMode.Drop);break;case "off":rig.Gate.Disable();break;case "locked":rig.Gate.SetSessionLocked(true);break;case "hidden":rig.Gate.Disable(hide:true);break;case "source":rig.Current=false;break;case "clear":rig.Local.Clear();break;}
    }
    [Theory][InlineData("settings")][InlineData("mode")][InlineData("off")][InlineData("locked")][InlineData("hidden")][InlineData("source")][InlineData("clear")]
    public async Task QueuedDiagnosticCompletionCannotRestoreExpiredCapabilities(string reason)
    {
        var destination=new Deferred();await using var rig=await Rig.Create(destination:destination);Assert.True(rig.Local.Discover(rig.Version,rig.Image));using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(6));while(destination.Count==0)await Task.Delay(5,limit.Token);
        Invalidate(rig,reason);destination.Drain();await rig.Local.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Null(rig.Local.Capabilities);Assert.False(rig.Gate.HasRunningGeneration);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.Script.Requests);
    }
    [Fact] public async Task CancelWaitsForOriginalDiagnosticThenDiscardsLateCapabilities()
    {
        await using var rig=await Rig.Create();rig.DiagnosticWorker.Hold=true;Assert.True(rig.Local.Discover(rig.Version,rig.Image));await rig.DiagnosticWorker.Entered.Task;
        var clear=rig.Local.ClearAsync();Assert.False(clear.IsCompleted);Assert.False(rig.Local.Discover(rig.Version,rig.Image));Assert.False(rig.DiagnosticWorker.Disposed);rig.DiagnosticWorker.Release.TrySetResult();await clear;
        Assert.True(rig.DiagnosticWorker.Disposed);Assert.Null(rig.Local.Capabilities);Assert.False(rig.Gate.HasRunningGeneration);Assert.Equal(0,rig.Script.Requests);
    }
    [Fact] public async Task UnconfirmedDiagnosticOwnerPreventsSelectionAndNewWorker()
    {
        await using var rig=await Rig.Create();rig.DiagnosticWorker.Finalized=false;Assert.True(rig.Local.Discover(rig.Version,rig.Image));await rig.Local.Running;Assert.Null(rig.Local.Capabilities);Assert.True(rig.Local.CleanupPending);Assert.Equal("CLEANUP_UNCONFIRMED",rig.Local.Failure);
        Assert.False(rig.Local.Discover(rig.Version,rig.Image));Assert.Equal(1,rig.Factories);Assert.False(rig.DiagnosticWorker.Disposed);Assert.False(rig.Gate.HasRunningGeneration);rig.DiagnosticWorker.Finalized=true;await rig.Local.ClearAsync();Assert.Equal(0,rig.Script.Requests);
    }
    [Fact] public async Task RejectingOldCapabilitiesCannotStopANewerCanonicalRequest()
    {
        await using var rig=await Rig.Create();var caps=await rig.Discover();Assert.True(rig.Gate.TryGetProfileBinding("caps-fixture",out var binding));Assert.True(rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out var newer,out var token));
        rig.Local.RejectCapabilities(caps);Assert.True(rig.Gate.CanApply(newer));Assert.True(rig.Gate.HasRunningGeneration);Assert.False(token.IsCancellationRequested);Assert.Null(rig.Local.Capabilities);Assert.Equal(0,rig.Script.Requests);
    }
    [Fact] public async Task SourceProofClearReentryCannotRestoreCapabilitiesOrRecognition()
    {
        await using var rig=await Rig.Create();var caps=await rig.Discover();rig.SourceProof=()=>{rig.SourceProof=null;rig.Local.Clear();};Assert.False(rig.Local.Start(caps,"en-US"));Assert.Null(rig.Local.Capabilities);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.Script.Requests);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CapabilitySourceProofCreatingCThenReturningOrThrowingCannotStopC(bool throws)
    {
        await using var rig=await Rig.Create();var caps=await rig.Discover();Assert.True(rig.Gate.TryGetProfileBinding("caps-fixture",out var binding));RequestIdentity newer=default;CancellationToken token=default;var original=new IOException("synthetic capability proof failure");
        rig.SourceProof=()=>{rig.SourceProof=null;Assert.True(rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out newer,out token));if(throws)throw original;};
        if(throws)Assert.Same(original,Record.Exception(()=>rig.Local.IsCapabilitiesCurrent(caps)));else Assert.False(rig.Local.IsCapabilitiesCurrent(caps));
        rig.Local.RejectCapabilities(caps);Assert.True(rig.Gate.CanApply(newer));Assert.True(rig.Gate.HasRunningGeneration);Assert.False(token.IsCancellationRequested);Assert.Null(rig.Local.Capabilities);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.Script.Requests);
    }
    [Theory][InlineData("none")][InlineData("gate")][InlineData("local")][InlineData("cleanup")]
    public async Task DiscoveryAdmissionFailureSettlesOnlyBAndPreservesOriginalCleanupAndC(string successor)
    {
        await using var rig=await Rig.Create();Assert.True(rig.Gate.TryGetProfileBinding("caps-fixture",out var binding));Assert.True(rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out _,out var oldToken));
        var original=new IOException("synthetic discovery admission failure");var cleanup=new InvalidOperationException("synthetic discovery cleanup failure");RequestIdentity newer=default;CancellationToken token=default;Task? newerRunning=null;CancellationTokenRegistration cleanupRegistration=default;var kickoff=new Deferred();
        using var registration=oldToken.Register(()=>
        {
            if(successor=="gate")Assert.True(rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out newer,out token));
            if(successor=="local"){Assert.True(rig.Local.Discover(rig.Version,rig.Image));newerRunning=rig.Local.Running;}
            if(successor=="cleanup")
            {
                var cts=(CancellationTokenSource)typeof(RuntimeGate).GetField("activeCancellation",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.GetValue(rig.Gate)!;
                cleanupRegistration=cts.Token.Register(()=>{Assert.True(rig.Gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),null,out newer,out token));throw cleanup;});
            }
            throw original;
        });
        try
        {
            var previous=SynchronizationContext.Current;Exception? error;
            try{SynchronizationContext.SetSynchronizationContext(kickoff);error=Record.Exception(()=>rig.Local.Discover(rig.Version,rig.Image));}finally{SynchronizationContext.SetSynchronizationContext(previous);}
            var failures=Assert.IsType<AggregateException>(error).Flatten().InnerExceptions;Assert.Contains(failures,e=>ReferenceEquals(original,e));if(successor=="cleanup")Assert.Contains(failures,e=>ReferenceEquals(cleanup,e));
            Assert.Equal(0,rig.Factories);Assert.Equal(0,rig.Script.Requests);
            if(successor is "gate" or "cleanup"){Assert.True(rig.Gate.CanApply(newer));Assert.True(rig.Gate.HasRunningGeneration);Assert.False(token.IsCancellationRequested);}
            else if(successor=="none")Assert.False(rig.Gate.HasRunningGeneration);
            else{Assert.Same(newerRunning,rig.Local.Running);kickoff.Drain();await rig.Local.Running;Assert.NotNull(rig.Local.Capabilities);Assert.Equal(1,rig.Factories);}
        }
        finally{cleanupRegistration.Dispose();kickoff.Drain();}
    }
    [Fact] public async Task InstalledNativeLimitDoesNotBypassExisting2560PixelPreparationGate()
    {
        await using var rig=await Rig.Create();rig.Image.Dispose();rig.Image=new(new P.ScreenshotPixels(new(0,0,2561,8),Enumerable.Repeat((byte)255,2561*8*4).ToArray()));rig.DiagnosticWorker.Diagnostic=Good(max:4000);var caps=await rig.Discover();
        Assert.False(rig.Local.Start(caps,"en-US"));Assert.Equal("SMALLER_SCREENSHOT_REQUIRED",rig.Local.Failure);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.RecognitionWorker.Recognitions);Assert.Equal(0,rig.Script.Requests);
    }
    [Fact] public async Task WrongDiagnosticJobNeverProducesAnOffer()
    {await using var rig=await Rig.Create();rig.DiagnosticWorker.WrongJob=true;Assert.True(rig.Local.Discover(rig.Version,rig.Image));await rig.Local.Running;Assert.Null(rig.Local.Capabilities);Assert.Equal("OCR_FAILED",rig.Local.Failure);Assert.False(rig.Gate.HasRunningGeneration);Assert.Equal(0,rig.Script.Requests);}
    [Theory][InlineData("zero-size")][InlineData("null-languages")][InlineData("many")][InlineData("long-language")][InlineData("duplicate")][InlineData("bad-language")][InlineData("dash-language")][InlineData("unknown-failure")][InlineData("null-identity")][InlineData("not-windows")][InlineData("not-present")][InlineData("identity-failure")][InlineData("null-fullname")][InlineData("null-aumid")][InlineData("wrong-role")][InlineData("wrong-package")][InlineData("long-identity")][InlineData("control-identity")][InlineData("surrogate-identity")][InlineData("failed-with-capabilities")]
    public void MalformedDiagnosticIsRejectedBeforeAnyCapabilityCanBeTrusted(string fault)
    {
        var d=Good();d=fault switch
        {
            "zero-size"=>d with{MaxImageDimension=0},"null-languages"=>d with{Languages=null!},"many"=>d with{Languages=Enumerable.Range(0,257).Select(i=>"x-"+i).ToArray()},"long-language"=>d with{Languages=[new string('a',65)]},"duplicate"=>d with{Languages=["en-US","EN-us"]},"bad-language"=>d with{Languages=["../en-US"]},"unknown-failure"=>d with{Failure=(P.ImageFailure)999},"null-identity"=>d with{Identity=null!},"not-windows"=>d with{Identity=d.Identity with{Windows=false}},"not-present"=>d with{Identity=d.Identity with{Present=false}},"identity-failure"=>d with{Identity=d.Identity with{Failure=P.ImageFailure.IdentityMismatch}},"null-fullname"=>d with{Identity=d.Identity with{FullName=null}},"null-aumid"=>d with{Identity=d.Identity with{ApplicationUserModelId=null}},"long-identity"=>d with{Identity=d.Identity with{FullName=new string('x',4097)}},"control-identity"=>d with{Identity=d.Identity with{FullName="synthetic\nidentity"}},"surrogate-identity"=>d with{Identity=d.Identity with{FullName=new string('\ud800',1)}},"failed-with-capabilities"=>d with{Failure=P.ImageFailure.NativeFailure},_=>d
        };if(fault=="dash-language")d=d with{Languages=["-"]};if(fault=="wrong-role")d=d with{Identity=d.Identity with{ApplicationUserModelId="KuaiYi.Development_synthetic!Other"}};if(fault=="wrong-package")d=d with{Identity=d.Identity with{FullName="OtherPackage"}};Assert.Throws<InvalidDataException>(()=>P.OcrCapabilitiesPolicy.ValidateDiagnostic(d));
    }
    [Fact] public void MaximumBoundedInstalledLanguageListIsAcceptedWithoutTruncation()
    {var languages=Enumerable.Range(0,256).Select(i=>"x"+i.ToString("D3")+new string('a',60)).ToArray();var d=Good(languages);P.OcrCapabilitiesPolicy.ValidateDiagnostic(d);Assert.Equal(256,d.Languages.Length);Assert.Equal(64,d.Languages[0].Length);}
    internal static string WorkerAssembly()
    {
        string configuration=new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;string path=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/KuaiYi.ImageWorker/bin",configuration,"net10.0/win-x64/KuaiYi.ImageWorker.dll"));Assert.Equal(SHA256.HashData(File.ReadAllBytes(typeof(Session).Assembly.Location)),SHA256.HashData(File.ReadAllBytes(path)));return path;
    }
    internal sealed class Starter:P.IImageProcessStarter
    {public Process? Child;public Process Start(ProcessStartInfo info){info.Environment["DOTNET_GENERATE_ASPNET_CERTIFICATE"]="false";return Child=Process.Start(info)!;}}
    [Fact] public async Task ActualLinuxDiscoveryUsesOriginalWorkerAndBlocksWithoutAnyRecognitionOrHttp()
    {
        Assert.False(OperatingSystem.IsWindows());await using var rig=await Rig.Create();rig.Factory=()=>new P.ImageWorkerClient(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH")??"dotnet",WorkerAssembly(),new Starter());Assert.True(rig.Local.Discover(rig.Version,rig.Image));await rig.Local.Running.WaitAsync(TimeSpan.FromSeconds(6));Assert.Equal("UnsupportedPlatform",rig.Local.Failure);Assert.Null(rig.Local.Capabilities);Assert.Null(rig.Local.Preview);Assert.False(rig.Gate.HasRunningGeneration);Assert.False(rig.Local.CleanupPending);Assert.Equal(1,rig.Factories);Assert.Equal(0,rig.Script.Requests);
    }
}
