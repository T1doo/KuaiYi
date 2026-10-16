using System.Text;
namespace KuaiYi.Core;
public sealed record CachedOverlayVersion(RequestIdentity Identity,string Text,bool Complete,bool Stopped,int Number=1);
/// <summary>Session memory only. Call on the UI destination; each incoming mutation is serialized by the canonical request gate.</summary>
public sealed class OverlayResults(RuntimeGate gate)
{
    public const int MaximumCharacters=1048576;
    private readonly StringBuilder text=new();private readonly List<CachedOverlayVersion> versions=[];
    private RequestIdentity? identity;private bool complete,stopped;
    private Guid? selectedVersionId,rollbackVersionId;private int versionNumber,sessionSequence;
    private CachedOverlayVersion[] fallback=[];private Guid? fallbackSelected;
    public string Text=>selectedVersionId==identity?.VersionId?text.ToString():versions.FirstOrDefault(v=>v.Identity.VersionId==selectedVersionId)?.Text??"";
    public bool HasText=>selectedVersionId==identity?.VersionId?text.Length>0:Text.Length>0;
    public RequestIdentity? Identity=>identity;
    public Guid? SelectedVersionId=>selectedVersionId;
    public Guid? RollbackVersionId=>rollbackVersionId;
    public IReadOnlyList<CachedOverlayVersion> Versions=>(identity is {} id?versions.Append(new(id,text.ToString(),complete,stopped,versionNumber)):versions).OrderBy(v=>v.Number).ToArray();
    public IReadOnlySet<Guid> RetainedVersionIds=>Versions.Concat(fallback).Select(v=>v.Identity.VersionId).ToHashSet();
    public int NextVersionNumber(Guid session)=>identity?.SessionId==session?checked(sessionSequence+1):1;
    public bool Begin(RequestIdentity current)
    {
        bool applied=false;gate.TryApply(current,()=>
        {
            if(identity==current)return;
            // Rapid source replacement must retain the last readable fallback, not replace it with an empty generation.
            if(complete||fallback.Length==0){fallback=Versions.OrderBy(v=>v.Number).ToArray();fallbackSelected=selectedVersionId;}
            if(identity?.SessionId!=current.SessionId){versions.Clear();rollbackVersionId=null;versionNumber=sessionSequence=1;}
            else{if(!complete)stopped=true;SaveVersion();rollbackVersionId=selectedVersionId;versionNumber=sessionSequence=checked(sessionSequence+1);fallback=[];fallbackSelected=null;}
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
    {bool applied=false;gate.TryApply(current,()=>{if(identity!=current||stopped||complete)return;complete=true;rollbackVersionId=null;fallback=[];fallbackSelected=null;applied=true;});return applied;}
    public bool Fail(RequestIdentity current)
    {
        bool applied=false;gate.TryApply(current,()=>
        {
            if(identity!=current||complete||stopped)return;stopped=true;
            if(rollbackVersionId is {} previous&&versions.Any(v=>v.Identity.VersionId==previous))selectedVersionId=previous;
            else if(fallback.Length>0)
            {
                // A different source must not destroy the last readable result when its request fails.
                var prior=fallback.FirstOrDefault(v=>v.Identity.VersionId==fallbackSelected)??fallback[^1];
                versions.Clear();versions.AddRange(fallback.Where(v=>v.Identity.VersionId!=prior.Identity.VersionId).TakeLast(2));
                identity=prior.Identity;versionNumber=prior.Number;sessionSequence=fallback.Max(v=>v.Number);text.Clear();text.Append(prior.Text);complete=prior.Complete;stopped=prior.Stopped;selectedVersionId=prior.Identity.VersionId;
            }
            fallback=[];fallbackSelected=null;rollbackVersionId=null;applied=true;
        });return applied;
    }
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
    public void Clear(){identity=null;selectedVersionId=rollbackVersionId=fallbackSelected=null;text.Clear();versions.Clear();fallback=[];versionNumber=sessionSequence=0;complete=stopped=false;}
    private void SaveVersion()
    {
        if(identity is not {} id)return;
        versions.RemoveAll(v=>v.Identity.VersionId==id.VersionId);versions.Add(new(id,text.ToString(),complete,stopped,versionNumber));versions.Sort((a,b)=>a.Number.CompareTo(b.Number));
    }
}
public enum OverlayResultEventKind{Begin,Delta,Complete,Stopped}
public sealed record OverlayResultEvent(OverlayResultEventKind Kind,string Text="");
