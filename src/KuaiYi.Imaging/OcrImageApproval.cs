using KuaiYi.Core;
namespace KuaiYi.Imaging;

// A local decision, never an HTTP authorization. Only the exact coordinator can issue one.
// One transition from the original OCR epoch to one image request; no text/image cache or replay.
public abstract class SmartImageApproval:IDisposable
{
    internal readonly OcrOnlyCoordinator Owner;
    internal readonly RequestIdentity OriginalIdentity;
    internal RequestIdentity BoundIdentity;
    internal RequestProfileBinding BoundProfile;
    private int state; // 0 prepared, 1 bound, 2 revoked
    internal SmartImageApproval(OcrOnlyCoordinator owner,Guid version,Guid source,string settings,RequestIdentity identity,ImageSourceKind kind,string? notice)
    {Owner=owner;OriginalIdentity=identity;SourceVersion=version;SourceId=source;SettingsVersion=settings;SourceKind=kind;SourceNotice=notice;}
    public Guid SourceVersion{get;}
    public Guid SourceId{get;}
    public string SettingsVersion{get;}
    public ImageSourceKind SourceKind{get;}
    public string? SourceNotice{get;}
    internal int State=>Volatile.Read(ref state);
    public bool IsPreparedCurrent=>State==0&&Owner.ImageApprovalCurrent(this,false);
    public bool IsBoundCurrent=>State==1&&Owner.ImageApprovalCurrent(this,true);
    public bool TryBind(CaptureTicket ticket,RequestIdentity identity,RequestProfileBinding binding)
        =>Owner.BindImageApproval(this,ticket,identity,binding);
    internal bool Bind(RequestIdentity identity,RequestProfileBinding binding)
    {if(State!=0)return false;BoundIdentity=identity;BoundProfile=binding;return Interlocked.CompareExchange(ref state,1,0)==0;}
    public void Dispose(){Interlocked.Exchange(ref state,2);if(this is OcrImageApproval text)text.ReleaseContent();Owner.ReleaseImageApproval(this);}
}
public sealed class OcrImageApproval:SmartImageApproval
{
    private OcrTextPreview? preview;
    internal OcrImageApproval(OcrOnlyCoordinator owner,OcrTextPreview value):base(owner,value.SourceVersion,value.SourceId,value.SettingsVersion,value.Identity,value.SourceKind,value.SourceNotice)
    {preview=value;PreviewId=value.Id;Language=value.Language;}
    public Guid PreviewId{get;}
    public string Language{get;}
    public bool TryReadText(out string? text)
    {text=null;if(!IsBoundCurrent)return false;text=Volatile.Read(ref preview)?.Text;return text!=null&&IsBoundCurrent;}
    internal void ReleaseContent()=>Interlocked.Exchange(ref preview,null);
}
public enum VisionContinuationReason{NoInstalledLanguage,EngineUnavailable,EmptyResult}
// A coordinator-owned local outcome, not an authorization. No text, bytes, paths or desktop proof.
public sealed class VisionContinuationCandidate
{
    internal VisionContinuationCandidate(Guid version,Guid source,string settings,RequestIdentity identity,ImageSourceKind kind,string? notice,VisionContinuationReason reason)
    {SourceVersion=version;SourceId=source;SettingsVersion=settings;Identity=identity;SourceKind=kind;SourceNotice=notice;Reason=reason;}
    public Guid SourceVersion{get;}
    public Guid SourceId{get;}
    public string SettingsVersion{get;}
    public RequestIdentity Identity{get;}
    public ImageSourceKind SourceKind{get;}
    public string? SourceNotice{get;}
    public VisionContinuationReason Reason{get;}
}
public sealed class VisionImageApproval:SmartImageApproval
{
    internal VisionImageApproval(OcrOnlyCoordinator owner,VisionContinuationCandidate value):base(owner,value.SourceVersion,value.SourceId,value.SettingsVersion,value.Identity,value.SourceKind,value.SourceNotice)=>Reason=value.Reason;
    public VisionContinuationReason Reason{get;}
}

public sealed partial class OcrOnlyCoordinator
{
    private SmartImageApproval? imageApproval;
    private VisionContinuationCandidate? visionCandidate;
    public VisionContinuationCandidate? VisionCandidate
    {get{VisionContinuationCandidate? value;lock(sync)value=visionCandidate;return value!=null&&IsVisionCandidateCurrent(value)?value:null;}}
    public bool IsVisionCandidateCurrent(VisionContinuationCandidate value)
    {
        bool current=false;
        gate.TryApply(value.Identity,()=>
        {
            bool live=sourceCurrent(value.SourceVersion,value.SourceId);var config=settings();var state=gate.Snapshot;
            lock(sync)
            {
                if(!ReferenceEquals(visionCandidate,value))return;
                bool metadata=live&&!disposed&&config is {ReadOnly:false,RequiresRecovery:false}&&config.Version==value.SettingsVersion&&
                    gate.Snapshot.Epoch==value.Identity.Epoch&&state.Activation==Activation.On&&state.Suspension==Suspension.None&&state.Presentation!=Presentation.Hidden;
                if(!metadata){visionCandidate=null;return;}
                // Receive has applied the outcome before Run's final settlement. Pending is not rejection.
                current=ownedWorker==null&&active==null&&Running.IsCompletedSuccessfully;
            }
        });
        if(!gate.CanApply(value.Identity)){lock(sync){if(ReferenceEquals(visionCandidate,value))visionCandidate=null;}}
        return current;
    }
    public VisionImageApproval? ApproveVision(VisionContinuationCandidate value)
    {
        VisionImageApproval? result=null;
        gate.TryApply(value.Identity,()=>
        {
            lock(sync)
            {
                if(!IsVisionCandidateCurrent(value))return;
                imageApproval?.Dispose();visionCandidate=null;
                imageApproval=result=new(this,value);
            }
        });return result;
    }
    public void RejectVision(VisionContinuationCandidate value)
    {
        lock(sync){if(!ReferenceEquals(visionCandidate,value))return;revision++;visionCandidate=null;}
        gate.StopRequest(value.Identity);
    }
    // Deliberately separate from Approve(): independent OCR-only text survives image release;
    // this capability always requires the original live image and exact settings.
    public OcrImageApproval? ApproveForImage(OcrTextPreview value)
    {
        OcrImageApproval? result=null;
        gate.TryApply(value.Identity,()=>
        {
            lock(sync)
            {
                if(!IsPreviewCurrent(value))return;
                imageApproval?.Dispose();Preview=null;
                imageApproval=result=new(this,value);
            }
        });return result;
    }
    internal void ReleaseImageApproval(SmartImageApproval value)
    {lock(sync){if(ReferenceEquals(imageApproval,value))imageApproval=null;}}
    private bool ImageApprovalMetadataCurrent(SmartImageApproval value)
    {
        bool live=sourceCurrent(value.SourceVersion,value.SourceId);var config=settings();var state=gate.Snapshot;
        return live&&!disposed&&ReferenceEquals(imageApproval,value)&&ownedWorker==null&&
            config is {ReadOnly:false,RequiresRecovery:false}&&config.Version==value.SettingsVersion&&
            state.Activation==Activation.On&&state.Suspension==Suspension.None&&state.Presentation!=Presentation.Hidden;
    }
    internal bool ImageApprovalCurrent(SmartImageApproval value,bool bound)
    {
        bool current=false;var identity=bound?value.BoundIdentity:value.OriginalIdentity;
        gate.TryApply(identity,()=>
        {
            lock(sync)current=value.State==(bound?1:0)&&ImageApprovalMetadataCurrent(value)&&gate.Snapshot.Epoch==identity.Epoch&&
                (!bound||gate.IsRequestProfileBindingCurrent(identity,value.BoundProfile));
        });
        if(!current)value.Dispose();return current;
    }
    internal bool BindImageApproval(SmartImageApproval value,CaptureTicket ticket,RequestIdentity identity,RequestProfileBinding binding)
    {
        bool accepted=false;
        gate.TryApply(identity,()=>
        {
            lock(sync)
            {
                var config=settings();
                if(value.State!=0||value.OriginalIdentity.Epoch!=ticket.Epoch||identity.Epoch<=ticket.Epoch||identity.Epoch-ticket.Epoch!=1||
                    !ImageApprovalMetadataCurrent(value)||gate.Snapshot.Epoch!=identity.Epoch||
                    config?.Data.VisionProfileId!=binding.ProfileId||!gate.IsRequestProfileBindingCurrent(identity,binding))return;
                accepted=value.Bind(identity,binding);
            }
        });if(!accepted)value.Dispose();return accepted;
    }
}
