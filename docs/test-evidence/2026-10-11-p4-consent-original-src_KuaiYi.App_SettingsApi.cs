using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
using Profile=KuaiYi.Core.Settings.ApiProfile;
using Protocol=KuaiYi.Core.Settings.ApiProtocol;
namespace KuaiYi.App;
internal sealed partial class SettingsWindow
{
    private readonly ApiConnectionTestWorkflow? apiTests;
    private readonly TextBox endpointPath=new(),temperature=new(),contextTokens=new(),imageBytes=new(),authHeader=new(),headers=new(){AcceptsReturn=true,MinHeight=60};
    private readonly ComboBox outputParameter=Choice<OutputTokenParameter>(),streamCapability=Choice<Capability>(),visionCapability=Choice<Capability>(),temperatureCapability=Choice<Capability>(),jsonCapability=Choice<Capability>(),usageCapability=Choice<Capability>();
    private readonly CheckBox localHttp=new(){Content="允许已明确确认的本机回环 HTTP"},insecureHttp=new(){Content="风险开关：允许远端 / 局域网非加密 HTTP"},storeFalse=new(){Content="发送 store:false（仅明确支持此字段的 OpenAI 服务）"};
    private readonly Button testConnection=new(){Content="测试已保存配置（固定公开内容）",Margin=new Thickness(0,8,0,4)},stopTest=new(){Content="停止连接测试",IsEnabled=false};
    private readonly TextBox testResult=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
    private Task apiTestTask=Task.CompletedTask;private readonly StringBuilder testText=new();private bool testClosePending;
    private void SetupApiAdvanced(Panel page)
    {
        var presets=new StackPanel{Orientation=Orientation.Horizontal};page.Children.Add(presets);
        foreach(var pair in new[]{("OpenAI 官方预设",Protocol.OpenAIChat),("Anthropic 官方预设",Protocol.AnthropicMessages)})
        {var button=new Button{Content=pair.Item1,Margin=new Thickness(4)};presets.Children.Add(button);button.Click+=(_,_)=>ApplyApiPreset(pair.Item2);}
        Field(page,"端点路径（空为协议默认；完整端点模式不追加）",endpointPath);Field(page,"输出 token 参数方式",outputParameter);Field(page,"温度（空为省略）",temperature);Field(page,"上下文 token 上限（可空，非精确计数）",contextTokens);Field(page,"图像字节上限（65536—4194304）",imageBytes);
        Field(page,"流式能力",streamCapability);Field(page,"图像能力",visionCapability);Field(page,"温度能力",temperatureCapability);Field(page,"结构化 JSON 能力",jsonCapability);Field(page,"流式用量能力",usageCapability);
        Note(page,"Unknown=未知，Supported=已确认支持，Unsupported=不支持。更改协议、模型或地址会重置能力。温度仅在明确支持时发送；图像能力未知不会作为已支持。");
        Field(page,"自定义认证头名（仅 CustomHeader；值使用上面的加密 API Key）",authHeader);Field(page,"非敏感自定义头（每行 Name: value；凭据只能用加密认证字段）",headers);
        page.Children.Add(localHttp);page.Children.Add(insecureHttp);page.Children.Add(storeFalse);page.Children.Add(testConnection);page.Children.Add(stopTest);page.Children.Add(testResult);
        foreach(var checkbox in new[]{localHttp,insecureHttp,storeFalse})checkbox.Click+=(_,_)=>{apiTests?.Cancel();PreviewEndpoint();};
        endpointPath.TextChanged+=(_,_)=>ApiIdentityEdited();headers.TextChanged+=(_,_)=>{if(!loading)apiTests?.Cancel();};authentication.SelectionChanged+=(_,_)=>{if(!loading)apiTests?.Cancel();};
        testConnection.IsEnabled=apiTests!=null;testConnection.Click+=async(_,_)=>await TestApi();stopTest.Click+=(_,_)=>{apiTests?.Cancel();status.Text="已取消连接测试。服务端是否已停止处理无法保证。";};
        Closing+=async(_,e)=>{if(apiTestTask.IsCompleted)return;e.Cancel=true;if(testClosePending)return;testClosePending=true;apiTests?.Cancel();try{await apiTestTask;}finally{testClosePending=false;Close();}};
    }
    private void ApplyApiPreset(Protocol value)
    {
        if(selectedId==null){status.Text="请先新增或选择一个配置。";return;}apiTests?.Cancel();draft.ClearKey(selectedId);password.Clear();
        protocol.SelectedItem=value;authentication.SelectedItem=value==Protocol.OpenAIChat?ApiAuthentication.Bearer:ApiAuthentication.ApiKey;authHeader.Text="";
        addressMode.SelectedItem=ApiAddressMode.BasePrefix;url.Text=value==Protocol.OpenAIChat?"https://api.openai.com/v1":"https://api.anthropic.com/v1";endpointPath.Text="";model.Text="";
        outputParameter.SelectedItem=OutputTokenParameter.ProtocolDefault;temperature.Clear();headers.Clear();localHttp.IsChecked=insecureHttp.IsChecked=false;storeFalse.IsChecked=value==Protocol.OpenAIChat;
        status.Text="预设已载入草稿；请手动填写模型并保存新密钥。能力保持未知，未发送请求。";
    }
    private void ApiIdentityEdited()
    {
        if(!loading){apiTests?.Cancel();foreach(var field in new[]{streamCapability,visionCapability,temperatureCapability,jsonCapability,usageCapability})field.SelectedItem=Capability.Unknown;}
        PreviewEndpoint();
    }
    private void LoadApiAdvanced(Profile? profile)
    {
        endpointPath.Text=profile?.EndpointPath??"";authHeader.Text=profile?.AuthenticationHeader??"";temperature.Text=profile?.Temperature?.ToString(CultureInfo.InvariantCulture)??"";contextTokens.Text=profile?.MaxContextTokens?.ToString(CultureInfo.InvariantCulture)??"";imageBytes.Text=(profile?.ImageByteLimit??4194304).ToString(CultureInfo.InvariantCulture);
        outputParameter.SelectedItem=profile?.OutputLimitParameter??OutputTokenParameter.ProtocolDefault;localHttp.IsChecked=profile?.AllowLoopbackHttp??false;insecureHttp.IsChecked=profile?.AllowInsecureHttp??false;storeFalse.IsChecked=profile?.SendStoreFalse??false;
        streamCapability.SelectedItem=profile?.Capabilities.Streaming??Capability.Unknown;visionCapability.SelectedItem=profile?.Capabilities.Vision??Capability.Unknown;temperatureCapability.SelectedItem=profile?.Capabilities.Temperature??Capability.Unknown;jsonCapability.SelectedItem=profile?.Capabilities.StructuredJson??Capability.Unknown;usageCapability.SelectedItem=profile?.Capabilities.UsageInStream??Capability.Unknown;
        headers.Text=profile==null?"":string.Join("\n",profile.Headers.Select(p=>p.Key+": "+p.Value));
        foreach(var control in new Control[]{endpointPath,authHeader,temperature,contextTokens,imageBytes,outputParameter,streamCapability,visionCapability,temperatureCapability,jsonCapability,usageCapability,headers,localHttp,insecureHttp,storeFalse})control.IsEnabled=profile!=null;
    }
    private Profile CaptureApiAdvanced(Profile profile)
    {
        var publicHeaders=ImmutableDictionary.CreateBuilder<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in headers.Text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries))
        {int split=line.IndexOf(':');if(split<=0||publicHeaders.ContainsKey(line[..split].Trim()))throw new SettingsValidationException("profile.headers");publicHeaders.Add(line[..split].Trim(),line[(split+1)..].Trim());}
        var result=profile with{EndpointPath=profile.AddressMode==ApiAddressMode.ExactEndpoint?"":endpointPath.Text,AuthenticationHeader=profile.Authentication==ApiAuthentication.CustomHeader?authHeader.Text:"",
            OutputLimitParameter=(OutputTokenParameter)(outputParameter.SelectedItem??OutputTokenParameter.ProtocolDefault),Temperature=string.IsNullOrWhiteSpace(temperature.Text)?null:Number(temperature.Text,"profile.temperature"),MaxContextTokens=string.IsNullOrWhiteSpace(contextTokens.Text)?null:Integer(contextTokens.Text,"profile.maxContextTokens"),ImageByteLimit=Integer(imageBytes.Text,"profile.imageByteLimit"),AllowLoopbackHttp=localHttp.IsChecked==true,AllowInsecureHttp=insecureHttp.IsChecked==true,SendStoreFalse=storeFalse.IsChecked==true,Headers=publicHeaders.ToImmutable(),
            Capabilities=new(){Streaming=(Capability)(streamCapability.SelectedItem??Capability.Unknown),Vision=(Capability)(visionCapability.SelectedItem??Capability.Unknown),Temperature=(Capability)(temperatureCapability.SelectedItem??Capability.Unknown),StructuredJson=(Capability)(jsonCapability.SelectedItem??Capability.Unknown),UsageInStream=(Capability)(usageCapability.SelectedItem??Capability.Unknown)}};
        ApiProfilePolicy.ValidateParameters(result);_=ApiProfilePolicy.Resolve(result);return result;
    }
    private async Task TestApi()
    {
        if(apiTests==null||selectedId==null||!apiTestTask.IsCompleted)return;
        try
        {
            CaptureProfile();var saved=coordinator.Effective?.Data.Profiles.FirstOrDefault(p=>p.Id==selectedId);var edited=draft.Data.Profiles.First(p=>p.Id==selectedId);
            if(saved==null||draft.HasPendingKey(selectedId)||!ApiProfilePolicy.RequestEquivalent(saved,edited)){status.Text="请先保存此配置和密钥，再重新打开设置测试。";return;}
            var intent=apiTests.Prepare(selectedId,out string reason);if(intent==null){status.Text=reason=="STOP_GENERATION_FIRST"?"请先停止当前生成，再测试连接。":"无法测试配置 · "+reason;return;}
            bool confirmed=MessageBox.Show(this,$"仅发送以下公开测试内容，不获取选区或截图：\n{KuaiYi.Core.ApiConnectionTestContent.Source}\n\n目标：{intent.Plan.Transport.ResolveEndpoint()}\n模型：{intent.Profile.Model}\n服务可能产生费用。是否发送一次测试？","快译 · 固定内容连接测试",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK;
            if(!confirmed)return;testText.Clear();testResult.Clear();testResult.ToolTip="服务未返回用量，用量未知";testConnection.IsEnabled=false;stopTest.IsEnabled=true;status.Text="正在测试已保存配置…";
            var pending=apiTests.Start(intent,true,update=>
            {
                var value=update.Event;
                if(value.Kind==ModelEventKind.TextDelta){testText.Append(value.Text);testResult.Text=testText.ToString();}
                else if(value.Kind==ModelEventKind.Completed)status.Text=(update.Plan.Transport.IsDevelopmentFixture?"本地测试服务返回完成；不是模型质量验收。":"固定内容请求完成。")+string.Join("；",update.Plan.Notices);
                else if(value.Kind==ModelEventKind.Failed)status.Text="连接测试失败 · "+value.Reason+"；已收到的部分内容不算完成。";
                else if(value.Kind==ModelEventKind.Usage)testResult.ToolTip=$"服务返回用量：输入 {value.Usage?.InputTokens?.ToString()??"未知"}，输出 {value.Usage?.OutputTokens?.ToString()??"未知"}";
            });
            if(pending==null){status.Text="配置、请求或启用状态已改变，未发送。";return;}apiTestTask=pending;await pending;
        }
        catch(Exception error){status.Text=error is SettingsValidationException validation?"请检查配置项："+validation.Field:"连接测试未完成 · "+error.GetType().Name;}
        finally{testConnection.IsEnabled=apiTests!=null;stopTest.IsEnabled=false;}
    }
}
