using KuaiYi.Core;
namespace KuaiYi.Imaging;

// A local decision, never an HTTP authorization. Only the exact coordinator can issue one.
// One transition from the original OCR epoch to one image request; no text/image cache or replay.
public sealed class OcrImageApproval:IDisposable
{
    internal readonly OcrOnlyCoordinator Owner;
    private OcrTextPreview? preview;
    internal readonly RequestIdentity OriginalIdentity;
    internal RequestIdentity BoundIdentity;
    internal RequestProfileBinding BoundProfile;
    private int state; // 0 prepared, 1 bound, 2 revoked
    internal OcrImageApproval(OcrOnlyCoordinator owner,OcrTextPreview value)
    {Owner=owner;preview=value;OriginalIdentity=value.Identity;SourceVersion=value.SourceVersion;SourceId=value.SourceId;PreviewId=value.Id;Language=value.Language;SettingsVersion=value.SettingsVersion;SourceKind=value.SourceKind;SourceNotice=value.SourceNotice;}
    public Guid SourceVersion{get;}
    public Guid SourceId{get;}
    public Guid PreviewId{get;}
    public string Language{get;}
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
    public bool TryReadText(out string? text)
    {text=null;if(!IsBoundCurrent)return false;text=Volatile.Read(ref preview)?.Text;return text!=null&&IsBoundCurrent;}
    public void Dispose(){Interlocked.Exchange(ref state,2);Interlocked.Exchange(ref preview,null);Owner.ReleaseImageApproval(this);}
}

public sealed partial class OcrOnlyCoordinator
{
    private OcrImageApproval? imageApproval;
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
    internal void ReleaseImageApproval(OcrImageApproval value)
    {lock(sync){if(ReferenceEquals(imageApproval,value))imageApproval=null;}}
    private bool ImageApprovalMetadataCurrent(OcrImageApproval value)
    {
        bool live=sourceCurrent(value.SourceVersion,value.SourceId);var config=settings();var state=gate.Snapshot;
        return live&&!disposed&&ReferenceEquals(imageApproval,value)&&ownedWorker==null&&
            config is {ReadOnly:false,RequiresRecovery:false}&&config.Version==value.SettingsVersion&&
            state.Activation==Activation.On&&state.Suspension==Suspension.None&&state.Presentation!=Presentation.Hidden;
    }
    internal bool ImageApprovalCurrent(OcrImageApproval value,bool bound)
    {
        bool current=false;var identity=bound?value.BoundIdentity:value.OriginalIdentity;
        gate.TryApply(identity,()=>
        {
            lock(sync)current=value.State==(bound?1:0)&&ImageApprovalMetadataCurrent(value)&&gate.Snapshot.Epoch==identity.Epoch&&
                (!bound||gate.IsRequestProfileBindingCurrent(identity,value.BoundProfile));
        });
        if(!current)value.Dispose();return current;
    }
    internal bool BindImageApproval(OcrImageApproval value,CaptureTicket ticket,RequestIdentity identity,RequestProfileBinding binding)
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
