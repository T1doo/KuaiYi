using System.Text;
namespace KuaiYi.Core;
public sealed record CachedOverlayVersion(RequestIdentity Identity,string Text,bool Complete,bool Stopped);
/// <summary>Session memory only. Call on the UI destination; each incoming mutation is serialized by the canonical request gate.</summary>
public sealed class OverlayResults(RuntimeGate gate)
{
    public const int MaximumCharacters=1048576;
    private readonly StringBuilder text=new();private readonly List<CachedOverlayVersion> versions=[];
    private RequestIdentity? identity;private bool complete,stopped;
    private Guid? selectedVersionId,rollbackVersionId;
    public string Text=>selectedVersionId==identity?.VersionId?text.ToString():versions.FirstOrDefault(v=>v.Identity.VersionId==selectedVersionId)?.Text??"";
    public bool HasText=>selectedVersionId==identity?.VersionId?text.Length>0:Text.Length>0;
    public RequestIdentity? Identity=>identity;
    public Guid? SelectedVersionId=>selectedVersionId;
    public Guid? RollbackVersionId=>rollbackVersionId;
    public IReadOnlyList<CachedOverlayVersion> Versions=>identity is {} id?[..versions,new(id,text.ToString(),complete,stopped)]:versions.ToArray();
    public bool Begin(RequestIdentity current)
    {
        bool applied=false;gate.TryApply(current,()=>
        {
            if(identity==current)return;
            if(identity?.SessionId!=current.SessionId){versions.Clear();rollbackVersionId=null;}
            else{if(!complete)stopped=true;SaveVersion();rollbackVersionId=selectedVersionId;}
            identity=current;text.Clear();complete=stopped=false;selectedVersionId=current.VersionId;
            // Reserve the generating slot immediately. Protect the displayed predecessor for rollback.
            while(versions.Count>2){int victim=versions.FindIndex(v=>v.Identity.VersionId!=rollbackVersionId);if(victim<0)throw new InvalidOperationException("No evictable version.");versions.RemoveAt(victim);}
            applied=true;
        });return applied;
    }
    public bool Append(RequestIdentity current,string delta)
    {
        ArgumentNullException.ThrowIfNull(delta);if(delta.Length==0)return false;bool applied=false;
        gate.TryApply(current,()=>{if(identity!=current||complete||stopped)return;if(delta.Length>MaximumCharacters-text.Length)throw new InvalidOperationException("Result memory limit exceeded.");text.Append(delta);applied=true;});return applied;
    }
    public bool Complete(RequestIdentity current)
    {bool applied=false;gate.TryApply(current,()=>{if(identity!=current||stopped||complete)return;complete=true;rollbackVersionId=null;applied=true;});return applied;}
    public bool TryAutoReveal(RequestIdentity current)=>identity==current&&selectedVersionId==current.VersionId&&HasText&&gate.TryRevealDelta(current);
    public void Collapse()=>gate.Collapse();
    public bool Reopen()=>gate.TryReopen(HasText);
    public bool SelectVersion(Guid versionId)
    {
        var candidate=Versions.FirstOrDefault(v=>v.Identity.VersionId==versionId);
        if(candidate==null||!gate.TryReopen(candidate.Text.Length>0))return false;
        selectedVersionId=versionId;return true;
    }
    public void MarkStopped(){if(identity==null)return;stopped=!complete;}
    public void Clear(){identity=null;selectedVersionId=rollbackVersionId=null;text.Clear();versions.Clear();complete=stopped=false;}
    private void SaveVersion()
    {
        if(identity is not {} id)return;
        versions.RemoveAll(v=>v.Identity.VersionId==id.VersionId);versions.Add(new(id,text.ToString(),complete,stopped));
    }
}
public enum OverlayResultEventKind{Begin,Delta,Complete,Stopped}
public sealed record OverlayResultEvent(OverlayResultEventKind Kind,string Text="");
