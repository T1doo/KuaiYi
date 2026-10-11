using System.Collections.Immutable;
namespace KuaiYi.Core.Settings;

[AttributeUsage(AttributeTargets.Property)]
public sealed class SettingsRangeAttribute(double minimum,double maximum):Attribute
{public double Minimum{get;}=minimum;public double Maximum{get;}=maximum;}
public enum ApiProtocol{OpenAIChat,AnthropicMessages}
public enum ApiAddressMode{BasePrefix,ExactEndpoint}
public enum ApiAuthentication{Bearer,ApiKey,None}
public enum Capability{Unknown,Supported,Unsupported}
public enum MotionPreference{FollowSystem,Reduce,Full}
public enum ThemePreference{FollowSystem,Light,Dark}
public enum TranslationPreset{Faithful,Plain,Professional,Explain}
public sealed record ModelCapabilities
{
    public Capability Streaming{get;init;}
    public Capability Vision{get;init;}
    public Capability Temperature{get;init;}
    public Capability StructuredJson{get;init;}
    public Capability UsageInStream{get;init;}
}
public sealed record ApiProfile
{
    [System.Text.Json.Serialization.JsonRequired]public string Id{get;init;}="";
    public string Name{get;init;}="新配置";
    public ApiProtocol Protocol{get;init;}
    public ApiAddressMode AddressMode{get;init;}
    public string BaseUrl{get;init;}="";
    public string Model{get;init;}="";
    public string? SecretRef{get;init;}
    public ApiAuthentication Authentication{get;init;}
    public ModelCapabilities Capabilities{get;init;}=new();
    [SettingsRange(1,131072)]public int MaxOutputTokens{get;init;}=4096;
    [SettingsRange(1,10000000)]public int? MaxContextTokens{get;init;}
    [SettingsRange(1,600)]public int TimeoutSeconds{get;init;}=60;
    public bool Streaming{get;init;}=true;
    public ImmutableDictionary<string,string> Headers{get;init;}=ImmutableDictionary<string,string>.Empty;
}
public sealed record TranslationSettings
{
    public TranslationPreset Preset{get;init;}=TranslationPreset.Faithful;
    public string TargetLanguage{get;init;}="zh-CN";
    public string Domain{get;init;}="";
    public string ReaderBackground{get;init;}="";
    [SettingsRange(80,800)]public int SelectionDebounceMs{get;init;}=180;
    [SettingsRange(1000,50000)]public int AutoSoftLimitScalars{get;init;}=12000;
    [SettingsRange(50000,50000)]public int HardLimitScalars{get;init;}=50000;
}
public sealed record OverlayPosition
{
    public string MonitorId{get;init;}="";
    [SettingsRange(0,1)]public double X{get;init;}
    [SettingsRange(0,1)]public double Y{get;init;}
    [SettingsRange(-10000,10000)]public double OffsetXDip{get;init;}
    [SettingsRange(-10000,10000)]public double OffsetYDip{get;init;}
}
public sealed record OverlaySettings
{
    [SettingsRange(12,22)]public double FontSizeDip{get;init;}=15;
    [SettingsRange(240,960)]public double PreferredWidthDip{get;init;}=420;
    public MotionPreference ReduceMotion{get;init;}
    public ThemePreference Theme{get;init;}
    public bool RememberPosition{get;init;}=true;
    public OverlayPosition? LastPosition{get;init;}
    public bool ShowSource{get;init;}
    [SettingsRange(3,3)]public int MemoryVersions{get;init;}=3;
}
public sealed record ImageSettings
{
    public bool SmartRouting{get;init;}=true;
    public bool PreviewScreenshotBeforeSend{get;init;}
    public bool MixedContentExplain{get;init;}=true;
    [SettingsRange(2560,2560)]public int DefaultLongEdgePixels{get;init;}=2560;
    [SettingsRange(4194304,4194304)]public int MaxPreparedBytes{get;init;}=4194304;
}
public sealed record PrivacySettings
{
    public bool PersistHistory{get;init;}
    public bool ClipboardFallbackDefault{get;init;}
    public bool TelemetryEnabled{get;init;}
    public ImmutableArray<string> BlockedApplications{get;init;}=[];
    public ImmutableArray<string> ClipboardAllowedApplications{get;init;}=[];
    public ImmutableArray<string> ConsentedEndpointOrigins{get;init;}=[];
}
public sealed record AppSettings
{
    public const int CurrentSchema=1;
    [SettingsRange(CurrentSchema,CurrentSchema)]public int SchemaVersion{get;init;}=CurrentSchema;
    // Durable transaction identity; no activation or content is stored.
    public Guid WriteId{get;init;}
    public string? ActiveProfileId{get;init;}
    public string? VisionProfileId{get;init;}
    public TriggerMode TriggerMode{get;init;}
    public ImmutableArray<ApiProfile> Profiles{get;init;}=[];
    public TranslationSettings Translation{get;init;}=new();
    public OverlaySettings Overlay{get;init;}=new();
    public ImageSettings Images{get;init;}=new();
    public PrivacySettings Privacy{get;init;}=new();
    public ImmutableDictionary<string,string?> Hotkeys{get;init;}=DefaultHotkeys;
    public bool LaunchAtSignIn{get;init;}
    public static ImmutableDictionary<string,string?> DefaultHotkeys{get;}=new Dictionary<string,string?>{
        ["toggle"]="Ctrl+Alt+T",["translateSelection"]="Ctrl+Alt+D",["captureRegion"]="Ctrl+Alt+S",["toggleResult"]="Ctrl+Alt+Space",["regenerate"]="Ctrl+Alt+R",["hideOverlay"]=null,["focusResult"]=null,["openSettings"]=null,["stopGeneration"]=null}.ToImmutableDictionary();
}
