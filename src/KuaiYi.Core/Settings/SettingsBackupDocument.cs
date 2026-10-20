using System.Collections.Immutable;
using System.Text;
namespace KuaiYi.Core.Settings;

/// <summary>An exact, private UTF-8 snapshot for explicit local export. No SecretStore or draft access.</summary>
public sealed class SettingsBackupPreview
{
    private readonly byte[] document;
    public string Text {get;}
    internal SettingsBackupPreview(byte[] document){this.document=document;Text=Encoding.UTF8.GetString(document);}
    internal byte[] CopyDocument()=>document.ToArray();
}
public static class SettingsBackupDocument
{
    public static SettingsBackupPreview Create(SettingsSnapshot saved)
    {
        if(saved.State is not (SettingsLoadState.Current or SettingsLoadState.Migrated))throw new InvalidOperationException("Saved settings unavailable.");
        SettingsJson.Validate(saved.Data);
        var value=saved.Data with
        {
            WriteId=Guid.Empty,
            Profiles=saved.Data.Profiles.Select(profile=>profile with{SecretRef=null,Headers=ImmutableDictionary<string,string>.Empty}).ToImmutableArray(),
            Privacy=new(),Overlay=saved.Data.Overlay with{LastPosition=null}
        };
        return new(SettingsJson.Write(value));
    }
    internal static void Validate(ReadOnlyMemory<byte> document)
    {
        var value=SettingsJson.Read(document.ToArray(),out bool migrated);
        if(migrated||value.WriteId!=Guid.Empty||value.Overlay.LastPosition!=null||value.Profiles.Any(p=>p.SecretRef!=null||p.Headers.Count!=0)||
            value.Privacy.BlockedApplications.Length!=0||value.Privacy.ClipboardAllowedApplications.Length!=0||value.Privacy.ConsentedEndpointOrigins.Length!=0)
            throw new SettingsValidationException("backup.privateFields");
    }
}
