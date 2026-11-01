using KuaiYi.Core.Settings;
namespace KuaiYi.App;

// Navigation advice only. The original lifecycle remains the sole enable/consent gate.
internal enum SettingsEnableTarget{Recovery,CreateProfile,ChooseTextProfile,Model,Key,Address,Parameters}
internal sealed record SettingsEnableLocation(SettingsEnableTarget Target,string? ProfileId=null)
{
    internal int Page=>Target==SettingsEnableTarget.Recovery?0:1;
    internal string Notice=>Target switch
    {
        SettingsEnableTarget.CreateProfile=>"请先新增 API 配置，填写服务地址和模型，再选择默认文字配置。",
        SettingsEnableTarget.ChooseTextProfile=>"请选择默认文字配置。配置只进入草稿，保存后仍需手动启用。",
        SettingsEnableTarget.Model=>"请填写默认文字配置的模型 ID。模型可手动输入。",
        SettingsEnableTarget.Key=>"请为默认文字配置输入并保存 API Key；已有密钥不会读入表单。",
        SettingsEnableTarget.Address=>"请核对默认文字配置的服务地址、地址模式和最终端点预览。",
        SettingsEnableTarget.Parameters=>"请核对默认文字配置及其请求参数。保存不会发送请求，也不会自动启用。",
        _=>"请先按原配置恢复提示处理；文件和密钥保留。"
    };
}
internal static class SettingsEnableGuide
{
    internal static SettingsEnableLocation Plan(SettingsSnapshot snapshot)
    {
        if(snapshot.ReadOnly||snapshot.RequiresRecovery)return new(SettingsEnableTarget.Recovery);
        var data=snapshot.Data;
        if(data.Profiles.Length==0)return new(SettingsEnableTarget.CreateProfile);
        var profile=data.Profiles.FirstOrDefault(p=>p.Id==data.ActiveProfileId);
        if(profile==null)return new(SettingsEnableTarget.ChooseTextProfile);
        if(string.IsNullOrWhiteSpace(profile.Model))return new(SettingsEnableTarget.Model,profile.Id);
        if(profile.Authentication!=ApiAuthentication.None&&profile.SecretRef==null)return new(SettingsEnableTarget.Key,profile.Id);
        try{_=ApiProfilePolicy.Resolve(profile);}catch(SettingsValidationException){return new(SettingsEnableTarget.Address,profile.Id);}
        return new(SettingsEnableTarget.Parameters,profile.Id);
    }
}
