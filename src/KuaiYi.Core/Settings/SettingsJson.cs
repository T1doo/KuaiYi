using System.Collections;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
namespace KuaiYi.Core.Settings;
public sealed class SettingsValidationException(string field):Exception("Invalid setting: "+field){public string Field{get;}=field;}
public sealed class UnknownSettingsSchemaException(int version):Exception("Unsupported settings schema."){public int Version{get;}=version;}
public static class SettingsJson
{
    public const int MaximumBytes=262144;
    static SettingsJson(){Options.MakeReadOnly();}
    public static JsonSerializerOptions Options{get;}=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,MaxDepth=16,UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow,RespectNullableAnnotations=true,TypeInfoResolver=new DefaultJsonTypeInfoResolver(),Converters={new JsonStringEnumConverter(allowIntegerValues:false)}};
    public static AppSettings Read(byte[] bytes,out bool migrated)
    {
        if(bytes.Length is <2 or >MaximumBytes)throw new SettingsValidationException("fileSize");
        using var document=JsonDocument.Parse(bytes,new(){MaxDepth=16});Check(document.RootElement.ValueKind==JsonValueKind.Object,"document");RejectDuplicates(document.RootElement);
        if(!document.RootElement.TryGetProperty("schemaVersion",out var schema)||!schema.TryGetInt32(out int version))throw new SettingsValidationException("schemaVersion");
        migrated=false;AppSettings settings;
        if(version==AppSettings.CurrentSchema)settings=JsonSerializer.Deserialize<AppSettings>(bytes,Options)??throw new SettingsValidationException("document");
        else if(version==0)
        {
            // Explicit initial-layout draft format; never claim an earlier released product used it.
            if(document.RootElement.EnumerateObject().Any(p=>p.Name is not ("schemaVersion" or "triggerMode" or "fontSizeDip" or "preferredWidthDip")))throw new UnknownSettingsSchemaException(0);
            var old=JsonSerializer.Deserialize<LegacyLayout>(bytes,Options)??throw new SettingsValidationException("document");
            settings=new(){TriggerMode=old.TriggerMode,Overlay=new(){FontSizeDip=old.FontSizeDip,PreferredWidthDip=old.PreferredWidthDip}};migrated=true;
        }
        else throw new UnknownSettingsSchemaException(version);
        Validate(settings);return settings;
    }
    private sealed record LegacyLayout
    {
        public int SchemaVersion{get;init;}
        public TriggerMode TriggerMode{get;init;}
        public double FontSizeDip{get;init;}=new OverlaySettings().FontSizeDip;
        public double PreferredWidthDip{get;init;}=new OverlaySettings().PreferredWidthDip;
    }
    internal static void RejectDuplicates(JsonElement item)
    {
        if(item.ValueKind==JsonValueKind.Object){var names=new HashSet<string>(StringComparer.Ordinal);foreach(var p in item.EnumerateObject()){if(!names.Add(p.Name))throw new SettingsValidationException("duplicateProperty");RejectDuplicates(p.Value);}}
        else if(item.ValueKind==JsonValueKind.Array)foreach(var value in item.EnumerateArray())RejectDuplicates(value);
    }
    public static byte[] Write(AppSettings settings){Validate(settings);byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(settings,Options);if(bytes.Length>MaximumBytes)throw new SettingsValidationException("fileSize");return bytes;}
    public static JsonNode Schema()
    {
        var schema=Options.GetJsonSchemaAsNode(typeof(AppSettings),new(){TreatNullObliviousAsNonNullable=true,TransformSchemaNode=(context,node)=>
        {if(context.PropertyInfo?.AttributeProvider?.GetCustomAttributes(typeof(SettingsRangeAttribute),true).OfType<SettingsRangeAttribute>().FirstOrDefault() is {} range&&node is JsonObject o){o["minimum"]=range.Minimum;o["maximum"]=range.Maximum;}return node;}});
        schema["required"]=new JsonArray(JsonValue.Create("schemaVersion"));schema["$schema"]="https://json-schema.org/draft/2020-12/schema";schema["default"]=JsonSerializer.SerializeToNode(new AppSettings(),Options);return schema;
    }
    public static void Validate(AppSettings s)
    {
        Check(s!=null,"document");Ranges(s!,"settings");
        Check(s!.SchemaVersion==AppSettings.CurrentSchema,"schemaVersion");Check(Enum.IsDefined(s.TriggerMode),"triggerMode");
        Check(!s.Profiles.IsDefault&&s.Profiles.Length<=16,"profiles");var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var p in s.Profiles)
        {
            Check(p!=null,"profiles");Text(p!.Id,64,"profile.id");Check(p.Id.Length>0&&ids.Add(p.Id)&&p.Id.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'),"profile.id");
            Text(p.Name,80,"profile.name");Check(!string.IsNullOrWhiteSpace(p.Name),"profile.name");Text(p.Model,256,"profile.model");Check(!string.IsNullOrWhiteSpace(p.Model),"profile.model");Text(p.BaseUrl,2048,"profile.baseUrl");
            Check(Uri.TryCreate(p.BaseUrl,UriKind.Absolute,out var uri)&&uri.Scheme is "https" or "http"&&uri.UserInfo.Length==0&&uri.Fragment.Length==0&&uri.Query.Length==0,"profile.baseUrl");
            Check(p.SecretRef==null||SecretFileStore.ValidReference(p.SecretRef),"profile.secretRef");Check(Enum.IsDefined(p.Protocol)&&Enum.IsDefined(p.AddressMode)&&Enum.IsDefined(p.Authentication),"profile.protocol");
            Check(p.Capabilities!=null&&Enum.IsDefined(p.Capabilities.Streaming)&&Enum.IsDefined(p.Capabilities.Vision)&&Enum.IsDefined(p.Capabilities.Temperature)&&Enum.IsDefined(p.Capabilities.StructuredJson)&&Enum.IsDefined(p.Capabilities.UsageInStream),"profile.capabilities");
            Check(p.Headers!=null&&p.Headers.Count<=2,"profile.headers");foreach(var h in p.Headers!){Check(h.Key=="anthropic-version"&&System.Text.RegularExpressions.Regex.IsMatch(h.Value,"^[0-9]{4}-[0-9]{2}-[0-9]{2}$")||h.Key=="OpenAI-Beta"&&h.Value=="assistants=v2","profile.headers");}
        }
        Check(s.ActiveProfileId==null||ids.Contains(s.ActiveProfileId),"activeProfileId");Check(s.VisionProfileId==null||ids.Contains(s.VisionProfileId),"visionProfileId");
        Check(s.Translation!=null&&Enum.IsDefined(s.Translation.Preset),"translation");Text(s.Translation!.TargetLanguage,32,"targetLanguage");Text(s.Translation.Domain,512,"domain");Text(s.Translation.ReaderBackground,2048,"readerBackground");
        Check(s.Overlay!=null&&Enum.IsDefined(s.Overlay.ReduceMotion)&&Enum.IsDefined(s.Overlay.Theme),"overlay");if(s.Overlay!.LastPosition is {} position){Text(position.MonitorId,256,"monitorId");Check(position.MonitorId.Length>0,"monitorId");}
        Check(s.Images!=null&&s.Privacy!=null,"images/privacy");Check(!s.Privacy!.PersistHistory&&!s.Privacy.TelemetryEnabled&&!s.Privacy.ClipboardFallbackDefault,"privacy.fixedDefaults");
        Strings(s.Privacy.BlockedApplications,128,260,"blockedApplications");Strings(s.Privacy.ClipboardAllowedApplications,128,260,"clipboardAllowedApplications");Strings(s.Privacy.ConsentedEndpointOrigins,128,2048,"consentedEndpointOrigins");
        foreach(var origin in s.Privacy.ConsentedEndpointOrigins){Check(Uri.TryCreate(origin,UriKind.Absolute,out var endpoint),"origin");Check(new EndpointOrigin(endpoint!).Value==origin,"origin");}
        Check(s.Hotkeys!=null&&s.Hotkeys.Count==AppSettings.DefaultHotkeys.Count&&s.Hotkeys.Keys.All(AppSettings.DefaultHotkeys.ContainsKey),"hotkeys");foreach(var key in s.Hotkeys!.Values)if(key!=null)Text(key,64,"hotkeys");
        // These require later OS transactions; this slice does not pretend to apply startup, hotkeys or authorizations.
        Check(!s.LaunchAtSignIn,"launchAtSignIn");
    }
    private static void Ranges(object value,string path)
    {
        if(value is string)return;
        if(value is IEnumerable list){foreach(var item in list)if(item!=null&&item.GetType().Namespace==typeof(AppSettings).Namespace)Ranges(item,path);return;}
        foreach(var p in value.GetType().GetProperties(BindingFlags.Instance|BindingFlags.Public))
        {
            object? item=p.GetValue(value);if(p.GetCustomAttribute<SettingsRangeAttribute>() is {} range&&item!=null){double n=Convert.ToDouble(item);Check(double.IsFinite(n)&&n>=range.Minimum&&n<=range.Maximum,path+"."+p.Name);}
            if(item!=null&&item.GetType().Namespace==typeof(AppSettings).Namespace&&!item.GetType().IsEnum)Ranges(item,path+"."+p.Name);
            else if(item is IEnumerable e and not string)foreach(var nested in e)if(nested!=null&&nested.GetType().Namespace==typeof(AppSettings).Namespace)Ranges(nested,path+"."+p.Name);
        }
    }
    private static void Strings(ImmutableArray<string> values,int count,int length,string field){Check(!values.IsDefault&&values.Length<=count,field);foreach(var value in values)Text(value,length,field);}
    private static void Text(string value,int length,string field)=>Check(value!=null&&value.Length<=length&&!value.Any(char.IsControl),field);
    private static void Check(bool valid,string field){if(!valid)throw new SettingsValidationException(field);}
}
