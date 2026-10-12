namespace KuaiYi.Core.Settings;
/// <summary>Immutable per-profile phase limits. The independent total budget always remains an upper bound.</summary>
public sealed record RequestTimeouts
{
    public static RequestTimeouts Default{get;}=new();
    [SettingsRange(1,60)]public int ConnectSeconds{get;init;}=10;
    [SettingsRange(1,180)]public int HeadersSeconds{get;init;}=30;
    [SettingsRange(1,300)]public int FirstResponseSeconds{get;init;}=30;
    [SettingsRange(1,180)]public int IdleSeconds{get;init;}=20;
    public void Validate()
    {
        if(ConnectSeconds is <1 or >60)throw new SettingsValidationException("profile.timeouts.connectSeconds");
        if(HeadersSeconds is <1 or >180)throw new SettingsValidationException("profile.timeouts.headersSeconds");
        if(FirstResponseSeconds is <1 or >300)throw new SettingsValidationException("profile.timeouts.firstResponseSeconds");
        if(IdleSeconds is <1 or >180)throw new SettingsValidationException("profile.timeouts.idleSeconds");
    }
}
