using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace KuaiYi.Core.Settings;

public sealed class SettingsImportData
{
    internal AppSettings Value{get;}
    internal SettingsImportData(AppSettings value)=>Value=value;
}
public sealed class SettingsImportPlan
{
    internal AppSettings Value{get;}
    public string Text{get;}
    public int AddedProfiles{get;}
    public bool RequiresHttpConfirmation{get;}
    internal SettingsImportPlan(AppSettings value,int added,bool http)
    {
        Value=value;AddedProfiles=added;RequiresHttpConfirmation=http;
        // Local credentials/rules/position remain in the draft, never in the public import preview.
        var visible=value with{WriteId=Guid.Empty,Profiles=value.Profiles.Select(p=>p with{SecretRef=null,Headers=ImmutableDictionary<string,string>.Empty}).ToImmutableArray(),Privacy=new(),Overlay=value.Overlay with{LastPosition=null}};
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(visible,SettingsJson.Options);
        try{if(bytes.Length>SettingsJson.MaximumBytes)throw new SettingsValidationException("import.fileSize");Text=Encoding.UTF8.GetString(bytes);}
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
}
public static class SettingsImportDocument
{
    private static readonly string[] Required=["schemaVersion","writeId","activeProfileId","visionProfileId","triggerMode","profiles","translation","overlay","images","privacy","hotkeys","launchAtSignIn"];
    public static SettingsImportData Read(ReadOnlyMemory<byte> bytes)
    {
        if(bytes.Length is <2 or >SettingsJson.MaximumBytes)throw new SettingsValidationException("import.fileSize");
        _=new UTF8Encoding(false,true).GetCharCount(bytes.Span);
        using var document=JsonDocument.Parse(bytes,new(){MaxDepth=16});var root=document.RootElement;
        if(root.ValueKind!=JsonValueKind.Object)throw new SettingsValidationException("import.document");
        SettingsJson.RejectDuplicates(root);
        if(!root.TryGetProperty("schemaVersion",out var schema)||schema.ValueKind!=JsonValueKind.Number||!schema.TryGetInt32(out int version)||version!=AppSettings.CurrentSchema)throw new SettingsValidationException("import.schemaVersion");
        if(root.EnumerateObject().Count()!=Required.Length||Required.Any(name=>!root.TryGetProperty(name,out _)))throw new SettingsValidationException("import.completeDocumentRequired");
        SettingsBackupDocument.Validate(bytes); // Reject references, headers, authorizations and machine-local metadata as a whole.
        var value=SettingsJson.Read(bytes.ToArray(),out bool migrated);
        if(migrated)throw new SettingsValidationException("import.schemaVersion");
        if(value.Translation.TargetLanguage!="zh-CN")throw new SettingsValidationException("import.targetLanguage");
        return new(value);
    }
    public static async Task<SettingsImportData> ReadAsync(Stream stream,CancellationToken cancellation=default)
    {
        byte[] bytes=new byte[SettingsJson.MaximumBytes+1];int count=0;
        try
        {
            while(true)
            {
                cancellation.ThrowIfCancellationRequested();int size=await stream.ReadAsync(bytes.AsMemory(count,bytes.Length-count),cancellation).ConfigureAwait(false);cancellation.ThrowIfCancellationRequested();
                if(size==0)break;count+=size;if(count>SettingsJson.MaximumBytes)throw new SettingsValidationException("import.fileSize");
            }
            return Read(bytes.AsMemory(0,count));
        }
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    internal static SettingsImportPlan Merge(SettingsImportBasis basis,SettingsImportData imported)
    {
        var local=basis.Value;var source=imported.Value;
        if(local.Profiles.Length+source.Profiles.Length>16)throw new SettingsValidationException("import.profileLimit");
        var ids=new HashSet<string>(local.Profiles.Select(p=>p.Id),StringComparer.Ordinal);var map=new Dictionary<string,string>(StringComparer.Ordinal);
        var added=source.Profiles.Select(p=>
        {
            string id;do{id=Guid.NewGuid().ToString("N");}while(!ids.Add(id));map.Add(p.Id,id);
            return p with{Id=id,SecretRef=null,Headers=ImmutableDictionary<string,string>.Empty,Capabilities=new(),AllowLoopbackHttp=false,AllowInsecureHttp=false};
        }).ToImmutableArray();
        // Source has passed complete validation. Only HTTP opt-in is deliberately withdrawn; the original Save validator still blocks it.
        var result=source with{WriteId=local.WriteId,Profiles=local.Profiles.AddRange(added),Privacy=local.Privacy,Overlay=source.Overlay with{LastPosition=local.Overlay.LastPosition},
            ActiveProfileId=local.ActiveProfileId??(source.ActiveProfileId is {} active?map[active]:null),VisionProfileId=local.VisionProfileId??(source.VisionProfileId is {} vision?map[vision]:null)};
        // Bound the real retained configuration too, including local headers hidden from the preview.
        // Do not use Write: the deliberate withdrawal of HTTP consent must still require later manual review.
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(result,SettingsJson.Options);
        try{if(bytes.Length>SettingsJson.MaximumBytes)throw new SettingsValidationException("import.fileSize");}
        finally{CryptographicOperations.ZeroMemory(bytes);}
        return new(result,added.Length,added.Any(p=>new Uri(p.BaseUrl).Scheme=="http"));
    }
}
