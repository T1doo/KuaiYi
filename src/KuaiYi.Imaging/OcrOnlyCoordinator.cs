using System.Text;
using System.Collections.Immutable;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
namespace KuaiYi.Imaging;

public interface ILocalOcrWorker:IAsyncDisposable
{
    Task<ImageReply> RunAsync(ReadOnlyMemory<byte> image,ImageJob job,CancellationToken cancellation=default);
    ValueTask<bool> TryFinalizeAsync();
}
public interface ILocalOcrCapabilitiesWorker:ILocalOcrWorker
{Task<OcrDiagnosticReply> DiagnoseAsync(OcrDiagnosticJob job,CancellationToken cancellation=default);}
public readonly record struct OcrWordRegion(int LineIndex,int WordIndex,NormalizedImageRectangle Normalized,OriginalImageRectangle Original);
public sealed record OcrTextPreview(Guid Id,Guid SourceVersion,Guid SourceId,string Text,string Language,string SettingsVersion,RequestIdentity Identity,
    ImageSourceKind SourceKind=ImageSourceKind.Screenshot,string? SourceNotice=null,ImageTransform? Transform=null,ImmutableArray<OcrWordRegion> WordRegions=default);
public sealed record OcrCapabilitiesSnapshot(Guid Id,Guid SourceVersion,Guid SourceId,ImmutableArray<string> Languages,uint MaxImageDimension,string SettingsVersion,RequestIdentity Identity,ImageSourceKind SourceKind=ImageSourceKind.Screenshot,string? SourceNotice=null);

/// <summary>Destination-thread commands; bounded local OCR only. No provider or HTTP dependency.
/// Original worker/IO ownership must settle before any preview can be approved.</summary>
public sealed class OcrOnlyCoordinator:IAsyncDisposable
{
    public const string Warning="当前模型不支持图像理解，图形关系未分析";
    private readonly RuntimeGate gate;
    private readonly Func<SettingsSnapshot?> settings;
    private readonly Func<Guid,Guid,bool> sourceCurrent;
    private readonly Func<ILocalOcrWorker> createWorker;
    private readonly IdentityEventReceiver<Completion> receiver;
    private readonly object sync=new();
    private long revision;
    private bool disposed;
    private Operation? active;
    private ILocalOcrWorker? ownedWorker;
    private SelectionSourceSnapshot? approved;
    private sealed record Completion(OcrTextPreview? Preview,string? Failure,OcrCapabilitiesSnapshot? Capabilities=null);
    private sealed record Operation(long Revision,Guid Version,ImageSourceOwner Source,SettingsSnapshot Settings,RequestProfileBinding Binding,RequestIdentity Identity,CancellationTokenSource Cancellation,bool Discover);
    public OcrOnlyCoordinator(RuntimeGate gate,Func<SettingsSnapshot?> settings,Func<Guid,Guid,bool> sourceCurrent,
        SynchronizationContext destination,Func<ILocalOcrWorker> createWorker)
    {this.gate=gate;this.settings=settings;this.sourceCurrent=sourceCurrent;this.createWorker=createWorker;receiver=new(gate,destination);}
    public Task Running{get;private set;}=Task.CompletedTask;
    private OcrTextPreview? preview;
    private OcrCapabilitiesSnapshot? capabilities;
    private ImageSourceOwner? capabilityImage;
    public OcrCapabilitiesSnapshot? Capabilities
    {
        get
        {
            var value=Volatile.Read(ref capabilities);
            if(value!=null&&!IsCapabilitiesCurrent(value)){lock(sync){if(ReferenceEquals(capabilities,value)){capabilities=null;capabilityImage=null;}}return null;}
            return value;
        }
    }
    public OcrTextPreview? Preview{get{var value=Volatile.Read(ref preview);if(value!=null&&!IsPreviewCurrent(value)){Interlocked.CompareExchange(ref preview,null,value);return null;}return value;}private set=>Volatile.Write(ref preview,value);}
    public string? Failure{get;private set;}
    public bool CleanupPending{get{lock(sync)return ownedWorker!=null;}}
    public bool Discover(Guid version,ImageSourceOwner source)=>StartCore(version,source,null);
    public bool Start(Guid version,ImageSourceOwner source,string language)=>language!=null&&StartCore(version,source,language);
    public bool Start(OcrCapabilitiesSnapshot snapshot,string language)
    {
        if(snapshot==null||!IsCapabilitiesCurrent(snapshot))return false;ImageSourceOwner? source;
        lock(sync)
        {
            if(!ReferenceEquals(capabilities,snapshot)||capabilityImage is not {} image)return false;
            if(!snapshot.Languages.Contains(language,StringComparer.OrdinalIgnoreCase)){Failure="LanguageUnavailable";return false;}
            if(image.Width>snapshot.MaxImageDimension||image.Height>snapshot.MaxImageDimension){Failure="OcrDimensionExceeded";return false;}
            if(image.Width>ImageTransform.LongEdgeLimit||image.Height>ImageTransform.LongEdgeLimit){Failure="SMALLER_SCREENSHOT_REQUIRED";return false;}
            source=image;
        }
        return StartCore(snapshot.SourceVersion,source,language,snapshot);
    }
    private bool StartCore(Guid version,ImageSourceOwner source,string? language,OcrCapabilitiesSnapshot? expected=null)
    {
        if(source.Kind==ImageSourceKind.Bitmap){Failure="BitmapOcrUnsupported";return false;}
        // Recognition never silently resizes a screenshot or a prepared file to fit OCR.
        if(language!=null&&(source.Width>ImageTransform.LongEdgeLimit||source.Height>ImageTransform.LongEdgeLimit))
        {Failure="SMALLER_SCREENSHOT_REQUIRED";return false;}
        if(version==Guid.Empty||source.IsDisposed||!sourceCurrent(version,source.SourceId)||
            language!=null&&(string.IsNullOrWhiteSpace(language)||language.Length>64||language.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))||
            settings() is not {} config||config.ReadOnly||config.RequiresRecovery||!gate.TryBeginPassiveCapture(gate.Snapshot.Mode,out var ticket)||
            config.Data.Profiles.FirstOrDefault(p=>p.Id==config.Data.ActiveProfileId) is not {} profile||!gate.TryGetProfileBinding(profile.Id,out var binding))return false;
        if(expected!=null&&(ticket.Epoch!=expected.Identity.Epoch||config.Version!=expected.SettingsVersion))return false;
        long command;
        lock(sync)
        {
            if(disposed||active!=null||ownedWorker!=null||expected!=null&&(!ReferenceEquals(capabilities,expected)||!ReferenceEquals(capabilityImage,source)))return false;
            command=++revision;Preview=null;capabilities=null;capabilityImage=null;Failure=null;approved=null;
        }
        RequestIdentity identity=default;
        try
        {
            // The gate writes this out identity BEFORE synchronously cancelling the previous
            // request. Even when that callback throws, this caller owns settlement of B only.
            if(!gate.TryStartProfileRequest(binding,Guid.NewGuid(),Guid.NewGuid(),ticket,out identity,out _))return false;
            bool started=false;
            // All nested locking is gate -> local state, including queued delivery. Never invert it.
            gate.TryApply(identity,()=>
            {
                bool liveSource=sourceCurrent(version,source.SourceId);var currentSettings=settings();
                lock(sync)
                {
                    if(!liveSource||command!=revision||disposed||active!=null||ownedWorker!=null||!gate.CanApply(identity)||gate.Snapshot.Epoch!=identity.Epoch||
                        currentSettings?.Version!=config.Version||source.IsDisposed||
                        !gate.TryLinkRequestCancellation(identity,CancellationToken.None,out var linked))return;
                    var operation=new Operation(command,version,source,config,binding,identity,linked!,language==null);
                    active=operation;Running=RunAsync(operation,language);started=true;
                }
            });
            if(!started)gate.StopRequest(identity);return started;
        }
        catch(Exception original)
        {
            try{if(identity!=default)gate.StopRequest(identity);}
            catch(Exception cleanup){throw new AggregateException("OCR admission and cleanup failed.",original,cleanup);}
            throw;
        }
    }
    private bool Current(Operation operation)
    {
        bool liveSource=sourceCurrent(operation.Version,operation.Source.SourceId);var config=settings();
        return liveSource&&!disposed&&ReferenceEquals(active,operation)&&operation.Revision==revision&&
            !operation.Cancellation.IsCancellationRequested&&gate.CanApply(operation.Identity)&&gate.Snapshot.Epoch==operation.Identity.Epoch&&
            gate.Snapshot.Suspension==Suspension.None&&gate.Snapshot.Presentation!=Presentation.Hidden&&config!=null&&!config.ReadOnly&&!config.RequiresRecovery&&config.Version==operation.Settings.Version&&
            config.Data.ActiveProfileId==operation.Binding.ProfileId&&gate.IsRequestProfileBindingCurrent(operation.Identity,operation.Binding)&&!operation.Source.IsDisposed;
    }
    private async Task RunAsync(Operation operation,string? language)
    {
        await Task.Yield();Completion completion;ILocalOcrWorker? worker=null;
        try
        {
            gate.TryApply(operation.Identity,()=>{lock(sync){if(Current(operation))ownedWorker=worker=createWorker();}});
            if(worker==null)throw new OperationCanceledException();
            bool current=false;gate.TryApply(operation.Identity,()=>{lock(sync)current=Current(operation);});if(!current)throw new OperationCanceledException();
            if(operation.Discover)
            {
                if(worker is not ILocalOcrCapabilitiesWorker diagnosticWorker)throw new ImageJobException(ImageFailure.NativeUnavailable);
                var job=new OcrDiagnosticJob(operation.Identity.SessionId,operation.Revision,operation.Identity.Epoch);
                var reply=await diagnosticWorker.DiagnoseAsync(job,operation.Cancellation.Token).ConfigureAwait(false);
                if(reply.Job!=job||reply.Diagnostic is not {} diagnostic)throw new InvalidDataException("OCR diagnostic job mismatch.");
                OcrCapabilitiesPolicy.ValidateDiagnostic(diagnostic);
                if(diagnostic.Failure!=ImageFailure.None)throw new ImageJobException(diagnostic.Failure);
                if(diagnostic.Languages.Length==0)throw new ImageJobException(ImageFailure.LanguageUnavailable);
                completion=new(null,null,new(Guid.NewGuid(),operation.Version,operation.Source.SourceId,diagnostic.Languages.ToImmutableArray(),diagnostic.MaxImageDimension,operation.Settings.Version,operation.Identity,operation.Source.Kind,operation.Source.Notice));
            }
            else
            {
                using var png=await Task.Run(()=>operation.Source.PrepareRequest(4194304,operation.Cancellation.Token)).ConfigureAwait(false);
                ValidatePrepared(operation.Source,png);
                var job=new ImageJob(operation.Identity.SessionId,operation.Revision,operation.Identity.Epoch,language!,png.Bytes.Length);
                var reply=await worker.RunAsync(png.Bytes,job,operation.Cancellation.Token).ConfigureAwait(false);
                if(reply.Job!=job)throw new InvalidDataException("OCR job mismatch.");
                if(reply.Failure!=ImageFailure.None)throw new ImageJobException(reply.Failure);
                if(reply.Output is not {} output)throw new InvalidDataException("OCR output absent.");
                ImageWorkerClient.ValidateOutput(output,ImagePolicy.InspectPng(png.Bytes.Span),language!);
                if(!ValidText(output.Text,operation.Settings.Data.Translation.HardLimitScalars))throw new InvalidDataException("OCR text invalid.");
                completion=new(new(Guid.NewGuid(),operation.Version,operation.Source.SourceId,output.Text,language!,operation.Settings.Version,operation.Identity,png.Kind,png.Notice,png.Transform,MapWords(output,png.Transform)),null);
            }
        }
        catch(OperationCanceledException){completion=new(null,"Cancelled");}
        catch(ImageJobException error){completion=new(null,error.Code.ToString());}
        catch(Exception)
        {completion=new(null,"OCR_FAILED");}
        try
        {
            if(worker!=null)
            {
                try
                {
                    if(await worker.TryFinalizeAsync().ConfigureAwait(false)){await worker.DisposeAsync().ConfigureAwait(false);lock(sync){if(ReferenceEquals(ownedWorker,worker))ownedWorker=null;}}
                    else completion=new(null,"CLEANUP_UNCONFIRMED");
                }
                catch(Exception){completion=new(null,"CLEANUP_UNCONFIRMED");}
            }
            await receiver.ReceiveAsync(operation.Identity,completion,value=>
            {lock(sync){if(!Current(operation))return;Preview=value.Preview;capabilities=value.Capabilities;capabilityImage=value.Capabilities==null?null:operation.Source;Failure=value.Failure;gate.MarkRequestTerminal(operation.Identity);}},operation.Cancellation.Token).ConfigureAwait(false);
        }
        finally
        {
            // Display eligibility can expire without cancelling this canonical identity.
            // Settlement is unconditional and identity-specific, so it cannot mark C terminal.
            gate.MarkRequestTerminal(operation.Identity);
            lock(sync){if(ReferenceEquals(active,operation))active=null;}operation.Cancellation.Dispose();
        }
    }
    public bool IsCapabilitiesCurrent(OcrCapabilitiesSnapshot snapshot)
    {
        bool current=false;gate.TryApply(snapshot.Identity,()=>
        {
            bool liveSource=sourceCurrent(snapshot.SourceVersion,snapshot.SourceId);var config=settings();
            lock(sync)current=liveSource&&!disposed&&ReferenceEquals(capabilities,snapshot)&&capabilityImage is {IsDisposed:false} image&&image.SourceId==snapshot.SourceId&&ownedWorker==null&&
                gate.CanApply(snapshot.Identity)&&gate.Snapshot.Epoch==snapshot.Identity.Epoch&&gate.Snapshot.Suspension==Suspension.None&&gate.Snapshot.Presentation!=Presentation.Hidden&&
                config!=null&&!config.ReadOnly&&!config.RequiresRecovery&&config.Version==snapshot.SettingsVersion;
        });return current;
    }
    public void RejectCapabilities(OcrCapabilitiesSnapshot snapshot)
    {
        lock(sync){if(!ReferenceEquals(capabilities,snapshot))return;revision++;capabilities=null;capabilityImage=null;}
        gate.StopRequest(snapshot.Identity);
    }
    /// <summary>Reject only this held preview; a late modal must not stop a newer request.</summary>
    public void RejectPreview(OcrTextPreview preview)
    {
        lock(sync){if(!ReferenceEquals(this.preview,preview))return;revision++;Preview=null;}
        gate.StopRequest(preview.Identity);
    }
    public bool IsPreviewCurrent(OcrTextPreview preview)
    {
        bool current=false;gate.TryApply(preview.Identity,()=>
        {
            bool liveSource=sourceCurrent(preview.SourceVersion,preview.SourceId);var config=settings();
            lock(sync)current=liveSource&&!disposed&&ReferenceEquals(this.preview,preview)&&ownedWorker==null&&gate.CanApply(preview.Identity)&&gate.Snapshot.Epoch==preview.Identity.Epoch&&
                gate.Snapshot.Suspension==Suspension.None&&gate.Snapshot.Presentation!=Presentation.Hidden&&config!=null&&!config.ReadOnly&&!config.RequiresRecovery&&config.Version==preview.SettingsVersion;
        });return current;
    }
    /// <summary>Call only for the preview's explicit confirmation. Text dispatch still requires confirmed Submit.</summary>
    public SelectionSourceSnapshot? Approve(OcrTextPreview preview)
    {
        SelectionSourceSnapshot? source=null;gate.TryApply(preview.Identity,()=>
        {
            lock(sync)
            {
                if(!IsPreviewCurrent(preview))return;
                approved=source=new(new(0,0,0,""),preview.Text,preview.Text,[],[],preview.SettingsVersion,preview.Text.EnumerateRunes().Count(),true,
                    SelectionSourceKind.OcrOnly,Ocr:new(preview.SourceVersion,preview.SourceId,preview.Id,preview.SourceKind==ImageSourceKind.LocalFile,preview.SourceNotice=="使用首帧",preview.SourceKind==ImageSourceKind.Browser));Preview=null;
            }
        });return source;
    }
    public bool IsApprovedSourceCurrent(SelectionSourceSnapshot source)
    {return !Volatile.Read(ref disposed)&&ReferenceEquals(Volatile.Read(ref approved),source)&&gate.Snapshot.Activation==Activation.On&&gate.Snapshot.Suspension==Suspension.None&&gate.Snapshot.Presentation!=Presentation.Hidden;}
    public void Clear(bool clearApproved=true)=>Invalidate(clearApproved);
    private (Task Pending,ILocalOcrWorker? Worker) Invalidate(bool clearApproved)
    {
        Operation? operation;OcrTextPreview? previous;OcrCapabilitiesSnapshot? previousCapabilities;(Task,ILocalOcrWorker?) captured;
        lock(sync)
        {
            revision++;operation=active;previous=preview;previousCapabilities=capabilities;Preview=null;capabilities=null;capabilityImage=null;Failure=null;if(clearApproved)approved=null;captured=(Running,ownedWorker);
        }
        if(operation!=null)gate.StopRequest(operation.Identity);else if(previous!=null)gate.StopRequest(previous.Identity);else if(previousCapabilities!=null)gate.StopRequest(previousCapabilities.Identity);
        return captured;
    }
    public async Task ClearAsync()
    {
        var captured=Invalidate(true);await captured.Pending.ConfigureAwait(false);
        ILocalOcrWorker? worker;lock(sync)worker=ReferenceEquals(ownedWorker,captured.Worker)?captured.Worker:null;
        if(worker!=null)
        {
            if(!await worker.TryFinalizeAsync().ConfigureAwait(false))throw new ImageJobException(ImageFailure.SettlementUnconfirmed);
            await worker.DisposeAsync().ConfigureAwait(false);lock(sync){if(ReferenceEquals(ownedWorker,worker))ownedWorker=null;}
        }
    }
    public async ValueTask DisposeAsync(){lock(sync)disposed=true;await ClearAsync().ConfigureAwait(false);}
    private static void ValidatePrepared(ImageSourceOwner source,PreparedImageRequest png)
    {
        var size=new ImageSize((uint)source.Width,(uint)source.Height);
        if(png.SourceId!=source.SourceId||png.Kind!=source.Kind||png.Notice!=source.Notice||
            png.Transform.NormalizedSize!=size||ImagePolicy.InspectPng(png.Bytes.Span)!=size)
            throw new InvalidDataException("OCR source preparation mismatch.");
        if(source is OwnedFileImage file)
        {
            if(png.Transform!=file.Metadata.Transform||png.Transform.HasDesktopAnchor||png.NormalizationProof!=null)
                throw new InvalidDataException("OCR file mapping mismatch.");
        }
        else if(source is not OwnedSourceImage||!png.Transform.HasDesktopAnchor||png.Transform.Orientation!=ImageOrientation.ScreenshotIdentity||
            png.Transform.OriginalSize!=size||png.NormalizationProof!=ScreenshotNormalizationProof.NativeSizeWhiteComposite)
            throw new InvalidDataException("OCR screenshot mapping mismatch.");
    }
    // Output is fully validated before allocating this bounded immutable, text-free mapping.
    private static ImmutableArray<OcrWordRegion> MapWords(OcrOutput output,ImageTransform transform)
    {
        int count=output.Lines.Sum(line=>line.Words.Length);
        var regions=ImmutableArray.CreateBuilder<OcrWordRegion>(count);
        for(int line=0;line<output.Lines.Length;line++)
            for(int word=0;word<output.Lines[line].Words.Length;word++)
            {
                var value=output.Lines[line].Words[word];var rectangle=new NormalizedImageRectangle(value.X,value.Y,value.Width,value.Height);
                regions.Add(new(line,word,rectangle,transform.NormalizedToOriginal(rectangle)));
            }
        return regions.MoveToImmutable();
    }
    private static bool ValidText(string text,int limit)
    {
        if(string.IsNullOrWhiteSpace(text)||text.Length>50000)return false;
        int scalars=0;for(int i=0;i<text.Length;i++,scalars++){if(char.IsHighSurrogate(text[i])){if(++i>=text.Length||!char.IsLowSurrogate(text[i]))return false;}else if(char.IsLowSurrogate(text[i]))return false;}
        return scalars<=limit;
    }
}
