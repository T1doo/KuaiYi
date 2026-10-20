using System.Collections.Immutable;
using System.Security.Cryptography;
namespace KuaiYi.Core.Settings;
public sealed class SettingsDraft(SettingsSnapshot basis):IDisposable
{
    private readonly object sync=new();private AppSettings data=basis.Data;private bool closed,saving;private long revision;
    private readonly Dictionary<string,byte[]> keys=[];private readonly HashSet<string> cleared=[];
    public SettingsSnapshot Basis{get;}=basis;
    public AppSettings Data{get{lock(sync)return data;}}
    public bool IsClosed{get{lock(sync)return closed;}}
    public bool HasPendingKey(string profileId){lock(sync)return keys.ContainsKey(profileId)||cleared.Contains(profileId);}
    public void Update(Func<AppSettings,AppSettings> edit){lock(sync){Available();data=edit(data);revision++;}}
    public SettingsImportBasis CaptureImportBasis()
    {
        lock(sync)
        {
            Available();if(Basis.RequiresRecovery||keys.Count!=0||cleared.Count!=0)throw new SettingsValidationException("import.unsavedDraft");
            byte[]? current=null,original=null;
            try
            {
                current=SettingsJson.Write(data);original=SettingsJson.Write(Basis.Data);
                if(!System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(current),System.Text.Json.Nodes.JsonNode.Parse(original)))throw new SettingsValidationException("import.unsavedDraft");
                return new(this,revision,data);
            }
            finally{if(current!=null)CryptographicOperations.ZeroMemory(current);if(original!=null)CryptographicOperations.ZeroMemory(original);}
        }
    }
    internal bool TryApplyImport(SettingsImportBasis expected,SettingsImportPlan plan)
    {
        lock(sync)
        {
            if(closed||saving||Basis.ReadOnly||Basis.RequiresRecovery||!ReferenceEquals(expected.Owner,this)||expected.Revision!=revision||keys.Count!=0||cleared.Count!=0)return false;
            data=plan.Value;revision++;return true;
        }
    }
    public DisplayPreferencesDraftSnapshot CaptureDisplayPreferences()
    {lock(sync){Available();return new(this,revision,new(data.Overlay.FontSizeDip,data.Overlay.PreferredWidthDip,data.Overlay.Theme,data.Overlay.ReduceMotion));}}
    public bool TryApplyDisplayPreferences(DisplayPreferencesDraftSnapshot expected,DisplayPreferences value)
    {
        DisplayPreferencesDocument.Validate(value);
        lock(sync)
        {
            Available();if(!ReferenceEquals(expected.Owner,this)||expected.Revision!=revision)return false;
            data=data with{Overlay=data.Overlay with{FontSizeDip=value.FontSizeDip,PreferredWidthDip=value.PreferredWidthDip,Theme=value.Theme,ReduceMotion=value.ReduceMotion}};revision++;return true;
        }
    }
    public void SetKey(string profileId,ReadOnlySpan<byte> plain)
    {
        lock(sync){Available();if(!data.Profiles.Any(p=>p.Id==profileId)||plain.Length is <1 or >8192)throw new SettingsValidationException("apiKey");if(keys.Remove(profileId,out var old))CryptographicOperations.ZeroMemory(old);keys[profileId]=plain.ToArray();cleared.Remove(profileId);revision++;}
    }
    public void ClearKey(string profileId)
    {
        lock(sync){Available();if(keys.Remove(profileId,out var old))CryptographicOperations.ZeroMemory(old);cleared.Add(profileId);data=data with{Profiles=data.Profiles.Select(p=>p.Id==profileId?p with{SecretRef=null}:p).ToImmutableArray()};revision++;}
    }
    internal (AppSettings Data,Dictionary<string,byte[]> Keys,HashSet<string> Cleared) BeginSave()
    {lock(sync){Available();saving=true;revision++;return(data,keys.ToDictionary(p=>p.Key,p=>p.Value.ToArray()),new(cleared));}}
    internal void EndSave(bool committed){lock(sync){saving=false;if(committed)Dispose();}}
    internal byte[]? PendingKeyForTest(string profileId){lock(sync)return keys.GetValueOrDefault(profileId);}
    private void Available(){if(closed||saving)throw new InvalidOperationException("Draft is closed or saving.");if(Basis.ReadOnly)throw new UnknownSettingsSchemaException(-1);}
    public void Dispose(){lock(sync){if(saving)throw new InvalidOperationException("Original save is still running.");if(closed)return;closed=true;foreach(var value in keys.Values)CryptographicOperations.ZeroMemory(value);keys.Clear();cleared.Clear();}}
}
public sealed class DisplayPreferencesDraftSnapshot
{
    internal SettingsDraft Owner{get;}internal long Revision{get;}
    public DisplayPreferences Value{get;}
    internal DisplayPreferencesDraftSnapshot(SettingsDraft owner,long revision,DisplayPreferences value){Owner=owner;Revision=revision;Value=value;}
}

public sealed class SettingsImportBasis
{
    internal SettingsDraft Owner{get;}internal long Revision{get;}internal AppSettings Value{get;}
    public string SavedVersion=>Owner.Basis.Version;
    internal SettingsImportBasis(SettingsDraft owner,long revision,AppSettings value){Owner=owner;Revision=revision;Value=value;}
}
