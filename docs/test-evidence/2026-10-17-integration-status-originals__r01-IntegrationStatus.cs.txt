using System.Collections.Immutable;
namespace KuaiYi.Core.Integration;

public enum IntegrationComponent { DesktopIdentity, ChromeHost, EdgeHost, ChromeExtension, EdgeExtension, ExplorerMenu, ImageWorkerOcr }
public enum IntegrationState { NotChecked, NotInstalled, Incompatible, PolicyDenied, UnsupportedPlatform, Configured, Unavailable }
public enum IntegrationReason { NotChecked, Platform, IdentityMissing, IdentityMismatch, RegistrationMissing, RegistrationMismatch, EvidenceConflict, PermissionDenied, ReadFailed, ManifestMissing, ReceiptMissing, InvalidManifest, InvalidReceipt, FileIntegrity, LocalPolicyBlocked, UserHostsDisabled, PolicyUnknown, LocalConfigurationOnly, ExtensionNotInspected, WindowsAcceptanceNotRun }
public sealed record IntegrationRow(IntegrationComponent Component, IntegrationState State, IntegrationReason Reason);
/// <summary>No free text, paths, extension IDs or exception messages enter the displayed snapshot.</summary>
public sealed record IntegrationSnapshot(ImmutableArray<IntegrationRow> Rows)
{
    public static IntegrationSnapshot Initial {get;}=new(Enum.GetValues<IntegrationComponent>().Select(c=>new IntegrationRow(c,IntegrationState.NotChecked,IntegrationReason.NotChecked)).ToImmutableArray());
    public static IntegrationSnapshot Unsupported {get;}=new(Enum.GetValues<IntegrationComponent>().Select(c=>new IntegrationRow(c,IntegrationState.UnsupportedPlatform,IntegrationReason.Platform)).ToImmutableArray());
}

public static class IntegrationPresentation
{
    public static string Component(IntegrationComponent value)=>value switch
    {IntegrationComponent.DesktopIdentity=>"当前桌面进程身份",IntegrationComponent.ChromeHost=>"Chrome 当前用户宿主",IntegrationComponent.EdgeHost=>"Edge 当前用户宿主",IntegrationComponent.ChromeExtension=>"Chrome 扩展",IntegrationComponent.EdgeExtension=>"Edge 扩展",IntegrationComponent.ExplorerMenu=>"资源管理器现代菜单",_=>"图片 Worker / OCR"};
    public static string State(IntegrationState value)=>value switch
    {IntegrationState.NotChecked=>"未检测",IntegrationState.NotInstalled=>"未安装",IntegrationState.Incompatible=>"不兼容",IntegrationState.PolicyDenied=>"检测到本地策略拒绝",IntegrationState.UnsupportedPlatform=>"平台不支持",IntegrationState.Configured=>"本地配置已核",_=>"无法核实"};
    public static string Advice(IntegrationReason value)=>value switch
    {
        IntegrationReason.Platform=>"需要 Windows；本次未执行 Windows 安装验收。",
        IntegrationReason.IdentityMissing=>"当前进程没有包身份。请通过已安装的应用入口启动；不会自动注册。",
        IntegrationReason.IdentityMismatch=>"当前身份与本版本支持的身份不符，请核对安装版本。",
        IntegrationReason.RegistrationMissing=>"没有当前用户宿主注册。请核对桌面安装；扩展需单独安装。",
        IntegrationReason.RegistrationMismatch=>"注册指向其他位置，未访问该目标。请核对安装版本。",
        IntegrationReason.EvidenceConflict=>"注册或策略来源冲突，未推断浏览器实际采用的值。",
        IntegrationReason.PermissionDenied=>"读取被拒绝，不能据此认定未安装或策略禁止；请联系管理员核实。",
        IntegrationReason.ReadFailed=>"读取未完成，安装状态未知。可稍后手动刷新。",
        IntegrationReason.ManifestMissing=>"当前安装目录缺少宿主清单，请核对桌面安装。",
        IntegrationReason.ReceiptMissing=>"当前安装目录缺少安装校验记录，请核对桌面安装。",
        IntegrationReason.InvalidManifest=>"宿主清单的格式、版本或路径不符合本版本约定。",
        IntegrationReason.InvalidReceipt=>"安装校验记录的格式或版本不符合本版本约定。",
        IntegrationReason.FileIntegrity=>"安装角色文件缺失、超界或摘要不符，请核对桌面安装。",
        IntegrationReason.LocalPolicyBlocked=>"本地宿主拒绝列表明确限制此宿主。请在浏览器策略页核实生效策略并联系管理员。",
        IntegrationReason.UserHostsDisabled=>"本地策略禁用当前用户宿主。请在浏览器策略页核实生效策略并联系管理员。",
        IntegrationReason.PolicyUnknown=>"本地策略格式或来源无法确定，未推断允许或拒绝。",
        IntegrationReason.LocalConfigurationOnly=>"只核本地配置；浏览器有效策略、启动、接收与图像翻译尚未端到端验证。",
        IntegrationReason.ExtensionNotInspected=>"未读取浏览器个人资料。请在浏览器扩展页核实安装及启用状态；宿主清单不能证明扩展已安装。",
        IntegrationReason.WindowsAcceptanceNotRun=>"没有真实 Windows 运行证据；程序集或清单存在不能证明此功能可用。",
        _=>"点击刷新进行只读检测。"
    };
}
