using System.Globalization;
using System.Windows;
using KuaiYi.Core;
using KuaiYi.Core.Settings;
using KuaiYi.Providers;
namespace KuaiYi.App;
// Real settings button command; Linux tests replace only modal/control/data ports.
internal sealed partial class SettingsWindow
{
    private object? apiTestCommand;private bool apiTestViewClosed;
    private void RevokeApiTestView(){apiTestViewClosed=true;apiTests?.DraftEdited();}
    private void CancelApiTest(){apiTests?.Cancel();status.Text="已取消本次测试。服务端是否已停止处理无法保证。";}
    private async Task TestApi(ApiTestPurpose purpose=ApiTestPurpose.TextConnection)
    {
        if(apiTestViewClosed||apiTestCommand!=null||apiTests==null||selectedId==null||!apiTestTask.IsCompleted)return;
        object command=new();apiTestCommand=command;
        bool OwnsUi()=>!apiTestViewClosed&&ReferenceEquals(apiTestCommand,command);
        try
        {
            CaptureProfile();if(!OwnsUi())return;var saved=coordinator.Effective?.Data.Profiles.FirstOrDefault(p=>p.Id==selectedId);var edited=draft.Data.Profiles.First(p=>p.Id==selectedId);
            if(saved==null||draft.HasPendingKey(selectedId)||!ApiProfilePolicy.RequestEquivalent(saved,edited)){status.Text="请先保存此配置和密钥，再重新打开设置测试。";return;}
            string reason;var intent=purpose==ApiTestPurpose.VisionFixture?apiTests.PrepareVision(selectedId,out reason):apiTests.Prepare(selectedId,out reason);
            if(intent==null)
            {
                status.Text=reason switch{"STOP_GENERATION_FIRST"=>"请先停止当前生成，再测试。","VISION_TEST_LOCAL_FIXTURE_ONLY"=>"视觉测试本批仅支持已授权的本机 fixture；没有发送，不改变普通图像能力。",_=>"无法测试配置 · "+reason};return;
            }
            string sample=purpose==ApiTestPurpose.VisionFixture?ApiVisionTestContent.Description+"\n仅发送程序内置 PNG；不获取选区、截图、剪贴板或用户图片。\n本地 fixture 仅验证请求合同，不能证明视觉理解能力。":$"仅发送以下公开测试内容，不获取选区或截图：\n{ApiConnectionTestContent.Source}";
            bool confirmed=MessageBox.Show(this,$"{sample}\n\n目标：{intent.Plan.Transport.ResolveEndpoint()}\n模型：{intent.Profile.Model}\n"+(purpose==ApiTestPurpose.VisionFixture?"本次只发送一次本机 fixture 请求，不调用真实模型。":"服务可能产生费用。")+"是否发送一次测试？",purpose==ApiTestPurpose.VisionFixture?"快译 · 内置图视觉测试":"快译 · 固定内容文字连接测试",MessageBoxButton.OKCancel,MessageBoxImage.Information)==MessageBoxResult.OK;
            if(!OwnsUi()||!confirmed){apiTests.Reject(intent);return;}
            testText.Clear();testResult.Clear();testResult.ToolTip="服务未返回用量，用量未知";testConnection.IsEnabled=testVision.IsEnabled=false;stopTest.IsEnabled=true;status.Text="正在测试已保存配置…";
            bool response=false,output=false,complete=false,terminal=false;string? failure=null;TranslationUsage usage=TranslationUsage.Unknown;
            var pending=apiTests.Start(intent,true,update=>
            {
                if(!OwnsUi()||update.Purpose!=purpose)return;
                var value=update.Event;
                if(value.Kind==ModelEventKind.TextDelta){response=true;output|=!string.IsNullOrEmpty(value.Text);testText.Append(value.Text);testResult.Text=testText.ToString();}
                else if(value.Kind==ModelEventKind.Completed){response=complete=terminal=true;status.Text=(update.Plan.Transport.IsDevelopmentFixture?"本地测试服务返回完成；不是模型质量验收。":"固定内容请求完成。")+string.Join("；",update.Plan.Notices);}
                else if(value.Kind==ModelEventKind.Failed){terminal=true;failure=value.Reason;status.Text="测试失败 · "+failure+"；已收到的部分内容不算完成。";}
                else if(value.Kind==ModelEventKind.Usage)
                {
                    response=true;usage=new(value.Usage?.InputTokens is >=0?value.Usage.InputTokens:usage.InputTokens,value.Usage?.OutputTokens is >=0?value.Usage.OutputTokens:usage.OutputTokens);
                    testResult.ToolTip=$"服务返回用量：输入 {usage.InputTokens?.ToString(CultureInfo.InvariantCulture)??"未知"}，输出 {usage.OutputTokens?.ToString(CultureInfo.InvariantCulture)??"未知"}";
                }
                if(purpose==ApiTestPurpose.VisionFixture)status.Text=VisionTestStatus(intent,response,output,complete,failure);
            });
            if(pending==null){status.Text="配置、请求或测试身份已改变，未发送。";return;}apiTestTask=pending;await pending;
            if(!terminal&&apiTests.IsIntentCurrent(intent)&&OwnsUi()&&ReferenceEquals(apiTestTask,pending))status.Text="测试已取消或身份已失效；没有完成，视觉能力仍为 Unknown（未知）。";
        }
        catch(Exception error){if(OwnsUi())status.Text=error is SettingsValidationException validation?"请检查配置项："+validation.Field:"测试未完成 · "+error.GetType().Name;}
        finally{if(ReferenceEquals(apiTestCommand,command)){apiTestCommand=null;if(!apiTestViewClosed){testConnection.IsEnabled=testVision.IsEnabled=apiTests!=null;stopTest.IsEnabled=false;}}}
    }
    private static string VisionTestStatus(ApiTestIntent intent,bool response,bool output,bool complete,string? failure)=>
        $"本地 fixture · 认证：无需认证（未验证）\n连通/协议：{(response?"已收到有效协议事件":"未知")} · 普通输出：{(output?"已收到文字":"未知")}\n流式：{(!intent.Plan.Transport.Stream?"未测试（请求非流式）":"已请求流式；实际响应形式和流式能力未知")}\n视觉能力：Unknown（未知）；不验证模型理解，不修改已保存能力\n{(failure!=null?"服务/请求错误："+failure:complete?"请求完成；仅本地合同验证":"等待完成；服务错误未知")}";
}
