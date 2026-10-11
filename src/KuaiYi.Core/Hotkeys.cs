using System.Collections.Immutable;
using KuaiYi.Core.Settings;
namespace KuaiYi.Core;
[Flags]public enum HotkeyModifiers:uint{None=0,Alt=1,Control=2,Shift=4,Windows=8}
public readonly record struct HotkeyChord(HotkeyModifiers Modifiers,uint Key)
{
    public string Text=>string.Join("+",new[]{Modifiers.HasFlag(HotkeyModifiers.Control)?"Ctrl":null,Modifiers.HasFlag(HotkeyModifiers.Alt)?"Alt":null,Modifiers.HasFlag(HotkeyModifiers.Shift)?"Shift":null,HotkeyPolicy.KeyName(Key)}.Where(s=>s!=null));
    public bool AltGrRisk=>Modifiers.HasFlag(HotkeyModifiers.Control)&&Modifiers.HasFlag(HotkeyModifiers.Alt);
}
public sealed class HotkeyValidationException(string action,string problem):Exception(problem){public string Action{get;}=action;public string Problem{get;}=problem;}
public static class HotkeyPolicy
{
    public static string KeyName(uint key)=>key switch{0x20=>"Space",0x0d=>"Enter",0x1b=>"Escape",0x09=>"Tab",0x08=>"Backspace",0x2e=>"Delete",0x2d=>"Insert",0x24=>"Home",0x23=>"End",0x21=>"PageUp",0x22=>"PageDown",>=0x70 and <=0x87=>"F"+(key-0x6f),>=0x30 and <=0x39 or >=0x41 and <=0x5a=>((char)key).ToString(),_=>throw new HotkeyValidationException("","unsupportedKey")};
    public static HotkeyChord Parse(string text,string action="")
    {
        if(text.Length is <1 or >64)throw new HotkeyValidationException(action,"invalidChord");var parts=text.Split('+',StringSplitOptions.TrimEntries);if(parts.Length is <2 or >4)throw new HotkeyValidationException(action,"modifierRequired");HotkeyModifiers modifiers=0;
        foreach(var part in parts[..^1]){var flag=part.ToUpperInvariant() switch{"CTRL" or "CONTROL"=>HotkeyModifiers.Control,"ALT"=>HotkeyModifiers.Alt,"SHIFT"=>HotkeyModifiers.Shift,_=>throw new HotkeyValidationException(action,"unsupportedModifier")};if(modifiers.HasFlag(flag))throw new HotkeyValidationException(action,"duplicateModifier");modifiers|=flag;}
        string name=parts[^1].ToUpperInvariant();uint key=name.Length==1&&char.IsAsciiLetterOrDigit(name[0])?name[0]:name switch{"SPACE"=>0x20,"ENTER"=>0x0d,"ESCAPE" or "ESC"=>0x1b,"TAB"=>9,"BACKSPACE"=>8,"DELETE"=>0x2e,"INSERT"=>0x2d,"HOME"=>0x24,"END"=>0x23,"PAGEUP"=>0x21,"PAGEDOWN"=>0x22,_=>name.StartsWith('F')&&uint.TryParse(name.AsSpan(1),out uint f)&&f is >=1 and <=24?0x6f+f:throw new HotkeyValidationException(action,"unsupportedKey")};
        return Validate(new(modifiers,key),action);
    }
    public static HotkeyChord Validate(HotkeyChord chord,string action="")
    {
        if((chord.Modifiers&~(HotkeyModifiers.Control|HotkeyModifiers.Alt|HotkeyModifiers.Shift))!=0)throw new HotkeyValidationException(action,"windowsOrUnknownModifier");
        if((chord.Modifiers&(HotkeyModifiers.Control|HotkeyModifiers.Alt))==0)throw new HotkeyValidationException(action,"modifierRequired");
        _=KeyName(chord.Key);if(chord.Key==0x7b)throw new HotkeyValidationException(action,"f12Reserved");
        if((chord.Modifiers.HasFlag(HotkeyModifiers.Alt) && ((chord.Key is 0x09 or 0x1b) || (!chord.Modifiers.HasFlag(HotkeyModifiers.Control) && (chord.Key is 0x20 or 0x73)))) || (chord.Modifiers.HasFlag(HotkeyModifiers.Control|HotkeyModifiers.Alt) && chord.Key==0x2e) || (chord.Key==0x1b && (chord.Modifiers==HotkeyModifiers.Control || chord.Modifiers==(HotkeyModifiers.Control|HotkeyModifiers.Shift))))throw new HotkeyValidationException(action,"systemReserved");return chord;
    }
    public static ImmutableDictionary<string,HotkeyChord> ValidateMap(IReadOnlyDictionary<string,string?> values)
    {
        if(values.Count!=AppSettings.DefaultHotkeys.Count||values.Keys.Any(k=>!AppSettings.DefaultHotkeys.ContainsKey(k)))throw new HotkeyValidationException("","actionSet");var map=ImmutableDictionary.CreateBuilder<string,HotkeyChord>();var seen=new Dictionary<HotkeyChord,string>();
        foreach(var pair in values.OrderBy(p=>p.Key,StringComparer.Ordinal)){if(pair.Value==null)continue;var chord=Parse(pair.Value,pair.Key);if(seen.TryGetValue(chord,out string? other))throw new HotkeyValidationException(pair.Key,"duplicate:"+other);seen.Add(chord,pair.Key);map.Add(pair.Key,chord);}return map.ToImmutable();
    }
}
/// <summary>Only focused WPF controls feed this recorder; no global keyboard hook.</summary>
public sealed class HotkeyRecorder
{
    private bool active;public void Begin()=>active=true;public void End()=>active=false;
    public HotkeyChord? Record(HotkeyModifiers modifiers,uint key,bool repeat=false){if(!active||repeat||key is 0x10 or 0x11 or 0x12 or 0x5b or 0x5c or 0xa0 or 0xa1 or 0xa2 or 0xa3 or 0xa4 or 0xa5)return null;return HotkeyPolicy.Validate(new(modifiers,key));}
}
public readonly record struct HotkeyRegistrationResult(bool Success,int Error);
/// <summary>Bounded synchronous port; Windows implementation runs on its own HWND/message thread. Test implementations are tests only.</summary>
public interface IHotkeyRegistrar
{
    HotkeyRegistrationResult Register(int id,HotkeyChord chord);HotkeyRegistrationResult Unregister(int id);uint FenceInput();
}
public sealed class HotkeyRegistrationException(string operation,int error,bool rollbackUnconfirmed=false):Exception(operation+" · Win32 "+error){public string Operation{get;}=operation;public int Error{get;}=error;public bool RollbackUnconfirmed{get;}=rollbackUnconfirmed;}
public sealed class HotkeyRegistryStateException(string problem,bool recoveryRequired=false):Exception(problem){public string Problem{get;}=problem;public bool RecoveryRequired{get;}=recoveryRequired;}
public sealed record HotkeyStatus(string Action,HotkeyChord? Chord,bool Registered,int Error,bool RecoveryRequired=false);
public sealed record HotkeyDispatch(string Action,int Id,HotkeyChord Chord,long Revision,string SettingsVersion);
public sealed record HotkeyPrepared(Guid Id);
/// <summary>Group swaps reuse owned physical bindings. New combinations are registered before obsolete ones are released. Old queued signals are fenced on every mapping change.</summary>
public sealed class HotkeyRegistry(IHotkeyRegistrar registrar)
{
    private readonly object sync=new();private readonly Dictionary<int,HotkeyChord> owned=[];private ImmutableDictionary<string,(int Id,HotkeyChord Chord)> active=ImmutableDictionary<string,(int,HotkeyChord)>.Empty;private ImmutableDictionary<string,int> errors=ImmutableDictionary<string,int>.Empty;
    private ImmutableDictionary<string,HotkeyChord> requested=ImmutableDictionary<string,HotkeyChord>.Empty;private long revision;private uint fence;private int nextId=0x2000;private string version="";private bool suspended,faulted,fencing;
    private sealed record Change(Guid Id,ImmutableDictionary<string,(int Id,HotkeyChord Chord)> Before,ImmutableDictionary<string,(int Id,HotkeyChord Chord)> After,ImmutableDictionary<string,HotkeyChord> Requested);
    private Change? pending;
    public IReadOnlyList<HotkeyStatus> Status{get{lock(sync)return AppSettings.DefaultHotkeys.Keys.Order().Select(a=>new HotkeyStatus(a,requested.TryGetValue(a,out var chord)?chord:null,active.ContainsKey(a)&&owned.ContainsKey(active[a].Id),errors.GetValueOrDefault(a),faulted||pending!=null)).ToArray();}}
    public void Suspend(bool value){lock(sync){suspended=value;revision++;fence=InputFence();}}
    private uint InputFence(){fencing=true;try{return registrar.FenceInput();}catch{faulted=true;throw;}finally{fencing=false;}}
    private int Register(HotkeyChord chord)
    {
        if(nextId>0xbfff)throw new HotkeyRegistryStateException("registrationIdsExhausted");int id=nextId++;var result=registrar.Register(id,chord);if(!result.Success)throw new HotkeyRegistrationException("RegisterHotKey",result.Error);owned.Add(id,chord);return id;
    }
    private void Release(int id){var result=registrar.Unregister(id);if(!result.Success)throw new HotkeyRegistrationException("UnregisterHotKey",result.Error);owned.Remove(id);}
    public HotkeyPrepared Prepare(IReadOnlyDictionary<string,string?> values)
    {
        lock(sync)
        {
            if(pending!=null||faulted)throw new HotkeyRegistryStateException("recoveryRequiredOrBusy",true);var desired=HotkeyPolicy.ValidateMap(values);var before=active;var target=ImmutableDictionary.CreateBuilder<string,(int Id,HotkeyChord Chord)>();pending=new(Guid.NewGuid(),before,active,desired);
            try
            {
                foreach(var pair in desired.OrderBy(p=>p.Key,StringComparer.Ordinal)){var existing=owned.FirstOrDefault(p=>p.Value==pair.Value);int id=existing.Key!=0?existing.Key:Register(pair.Value);target.Add(pair.Key,(id,pair.Value));}
                var after=target.ToImmutable();pending=pending with{After=after};var kept=after.Values.Select(v=>v.Id).ToHashSet();foreach(int id in owned.Keys.Where(id=>!kept.Contains(id)).ToArray())Release(id);return new(pending.Id);
            }
            catch(Exception error){try{Rollback(new(pending.Id));}catch(Exception rollback){throw new AggregateException("Hotkey rollback unconfirmed; owned registrations retained.",error,rollback);}throw;}
        }
    }
    public void Commit(HotkeyPrepared ticket,string settingsVersion)
    {lock(sync){var change=Require(ticket);if(change.After.Values.Any(v=>!owned.TryGetValue(v.Id,out var chord)||chord!=v.Chord))throw new HotkeyRegistryStateException("preparedOwnershipUnconfirmed",true);fence=InputFence();faulted=false;revision++;active=change.After;requested=change.Requested;errors=ImmutableDictionary<string,int>.Empty;version=settingsVersion;pending=null;}}
    private Change Require(HotkeyPrepared ticket)=>pending is {} change&&change.Id==ticket.Id?change:throw new InvalidOperationException("Wrong hotkey transaction.");
    public void Rollback(HotkeyPrepared ticket)
    {
        lock(sync)
        {
            var change=Require(ticket);List<Exception> failures=[];var ids=change.Before.Values.Select(v=>v.Id).ToHashSet();
            foreach(int id in owned.Keys.Where(id=>!ids.Contains(id)).ToArray())try{Release(id);}catch(Exception e){failures.Add(e);}
            foreach(var item in change.Before.Values.Distinct())if(!owned.ContainsKey(item.Id))try{var result=registrar.Register(item.Id,item.Chord);if(!result.Success)throw new HotkeyRegistrationException("restoreOldHotkey",result.Error,true);owned.Add(item.Id,item.Chord);}catch(Exception e){failures.Add(e);}
            try{fence=InputFence();}catch(Exception error){failures.Add(error);}revision++;active=change.Before;pending=null;faulted=failures.Count>0;if(faulted)throw new AggregateException("Hotkey rollback unconfirmed; retained own IDs require recovery.",failures);
        }
    }
    public void Restore(IReadOnlyDictionary<string,string?> values,string settingsVersion)
    {
        lock(sync)
        {
            if(pending!=null)throw new HotkeyRegistryStateException("transactionBusy",true);var desired=HotkeyPolicy.ValidateMap(values);List<Exception> cleanup=[];foreach(int id in owned.Keys.ToArray())try{Release(id);}catch(Exception e){cleanup.Add(e);}
            active=ImmutableDictionary<string,(int,HotkeyChord)>.Empty;requested=desired;errors=ImmutableDictionary<string,int>.Empty;version=settingsVersion;revision++;fence=InputFence();faulted=cleanup.Count>0;if(faulted)throw new AggregateException("Own registration cleanup unconfirmed.",cleanup);
            foreach(var pair in desired.OrderBy(p=>p.Key,StringComparer.Ordinal))try{int id=Register(pair.Value);active=active.Add(pair.Key,(id,pair.Value));}catch(HotkeyRegistrationException error){errors=errors.SetItem(pair.Key,error.Error);}fence=InputFence();
        }
    }
    public void Reconcile(IReadOnlyDictionary<string,string?> values,string settingsVersion)
    {
        lock(sync)
        {
            var desired=HotkeyPolicy.ValidateMap(values);
            if(pending is {} change){if(!change.Requested.OrderBy(p=>p.Key).SequenceEqual(desired.OrderBy(p=>p.Key)))throw new HotkeyRegistryStateException("transactionOutcomeMismatch",true);Commit(new(change.Id),settingsVersion);return;}
            if(!requested.OrderBy(p=>p.Key).SequenceEqual(desired.OrderBy(p=>p.Key))){Restore(values,settingsVersion);return;}
            version=settingsVersion;revision++;fence=InputFence();
        }
    }
    public void Recover(string settingsVersion)
    {
        lock(sync)
        {
            if(pending!=null||faulted)throw new HotkeyRegistryStateException("recoveryRequiredOrBusy",true);
            // A surviving HWND still owns its successful registrations across Explorer/lock changes. Do not drop working keys just to probe them.
            foreach(var pair in requested.OrderBy(p=>p.Key,StringComparer.Ordinal))if(!active.ContainsKey(pair.Key))try{int id=Register(pair.Value);active=active.Add(pair.Key,(id,pair.Value));errors=errors.Remove(pair.Key);}catch(HotkeyRegistrationException error){errors=errors.SetItem(pair.Key,error.Error);}
            version=settingsVersion;revision++;fence=InputFence();
        }
    }
    public HotkeyDispatch? Resolve(int id,HotkeyChord chord,uint eventTick)
    {lock(sync){if(suspended||faulted||fencing||pending!=null||unchecked((int)(eventTick-fence))<=0)return null;foreach(var pair in active)if(pair.Value.Id==id&&pair.Value.Chord==chord&&owned.ContainsKey(id))return new(pair.Key,id,chord,revision,version);return null;}}
    public bool Current(HotkeyDispatch signal){lock(sync)return !suspended&&!faulted&&!fencing&&pending==null&&signal.Revision==revision&&signal.SettingsVersion==version&&active.TryGetValue(signal.Action,out var item)&&item.Id==signal.Id&&item.Chord==signal.Chord&&owned.ContainsKey(item.Id);}
    public void Close(){lock(sync){if(pending!=null)throw new HotkeyRegistryStateException("saveStillPending",true);active=ImmutableDictionary<string,(int,HotkeyChord)>.Empty;suspended=true;revision++;List<Exception> failures=[];foreach(int id in owned.Keys.ToArray())try{Release(id);}catch(Exception e){failures.Add(e);}faulted=failures.Count>0;if(faulted)throw new AggregateException("Own hotkey cleanup unconfirmed.",failures);}}
}
public interface ISettingsHotkeyTransaction{Task CommitAsync(SettingsSnapshot saved);Task RollbackAsync();}
public interface ISettingsHotkeyParticipant{Task<ISettingsHotkeyTransaction> PrepareAsync(IReadOnlyDictionary<string,string?> hotkeys,CancellationToken cancellation);}
public enum HotkeyActionDecision{Run,HintEnable,Blocked}
public static class HotkeyActionPolicy
{
    public static HotkeyActionDecision Evaluate(string action,Activation activation,Suspension suspension)
    {
        if(!AppSettings.DefaultHotkeys.ContainsKey(action)||suspension.HasFlag(Suspension.SessionLocked))return HotkeyActionDecision.Blocked;
        if(suspension!=Suspension.None&&action is "toggle" or "translateSelection" or "captureRegion" or "regenerate")return HotkeyActionDecision.Blocked;
        return activation==Activation.Off&&action is "translateSelection" or "regenerate"?HotkeyActionDecision.HintEnable:HotkeyActionDecision.Run;
    }
}
