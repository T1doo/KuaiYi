using System.Text;
namespace KuaiYi.Core;
public sealed record CachedOverlayVersion(RequestIdentity Identity,string Text,bool Complete,bool Stopped);
/// <summary>Session memory only. Call on the UI destination; each incoming mutation is serialized by the canonical request gate.</summary>
public sealed class OverlayResults(RuntimeGate gate)
{
    public const int MaximumCharacters=1048576;
    private readonly StringBuilder text=new();private readonly List<CachedOverlayVersion> versions=[];
    private RequestIdentity? identity;private bool complete,stopped;
    public string Text=>text.ToString();public bool HasText=>text.Length>0;
    public RequestIdentity? Identity=>identity;
    public IReadOnlyList<CachedOverlayVersion> Versions=>versions.ToArray();
    public bool Begin(RequestIdentity current)
    {
        bool applied=false;gate.TryApply(current,()=>
        {
            if(identity==current)return;
            if(identity?.SessionId!=current.SessionId)versions.Clear();else{if(!complete)stopped=true;SaveVersion();}
            identity=current;text.Clear();complete=stopped=false;applied=true;
        });return applied;
    }
    public bool Append(RequestIdentity current,string delta)
    {
        ArgumentNullException.ThrowIfNull(delta);if(delta.Length==0)return false;bool applied=false;
        gate.TryApply(current,()=>{if(identity!=current||complete||stopped)return;if(delta.Length>MaximumCharacters-text.Length)throw new InvalidOperationException("Result memory limit exceeded.");text.Append(delta);applied=true;});return applied;
    }
    public bool Complete(RequestIdentity current)
    {bool applied=false;gate.TryApply(current,()=>{if(identity!=current)return;complete=true;SaveVersion();applied=true;});return applied;}
    public bool TryAutoReveal(RequestIdentity current)=>identity==current&&HasText&&gate.TryRevealDelta(current);
    public void Collapse()=>gate.Collapse();
    public bool Reopen()=>gate.TryReopen(HasText);
    public void MarkStopped(){if(identity==null)return;stopped=!complete;SaveVersion();}
    public void Clear(){identity=null;text.Clear();versions.Clear();complete=stopped=false;}
    private void SaveVersion()
    {
        if(identity is not {} id||text.Length==0)return;
        versions.RemoveAll(v=>v.Identity.VersionId==id.VersionId);versions.Add(new(id,text.ToString(),complete,stopped));while(versions.Count>3)versions.RemoveAt(0);
    }
}
public enum OverlayResultEventKind{Begin,Delta,Complete,Stopped}
public sealed record OverlayResultEvent(OverlayResultEventKind Kind,string Text="");
