# P7-06：设置中的本地集成状态

本片从父独审接受的 `b35a8d7cd3b6f07f80453395416ff0ca2cd27cc2` 正常建立 `dev/integration-status-page`。先核实际本地/远端 HEAD、main、clean 与单 worktree；root 为唯一代码写入者，既有 high_water_review 仅独立只读审查。此前高水位、诊断报告与 250ms 修复均保留，不重复交付。不访问已被拒绝的 GitHub Actions。

## 已交付的产品入口

设置 → 图片与集成增加手动刷新、取消和七个状态行。初始为 NotChecked，不自动扫描。只读未知配置下，所有编辑页及图片偏好继续禁用，集成只读刷新可用；保存仍禁用。这个入口不创建独立诊断程序，不写注册表、策略、权限、配置或报告，也不启动浏览器、NativeHost、Worker 或模型。

状态模型区分 NotChecked、NotInstalled、Incompatible、PolicyDenied、UnsupportedPlatform、Configured 和 Unavailable。界面使用固定中文名称与固定建议；Snapshot 仅含组件、状态、原因枚举，不含路径、扩展 ID、包全名、异常文本、用户内容或秘密。

| 行 | 本次证据与限制 |
|---|---|
| 当前桌面进程身份 | 使用既有 PackageIdentity.Read 读取当前进程，缺失、不匹配和平台错误分开。仅支持既有开发身份约定，不推断子进程继承。 |
| Chrome 当前用户宿主 | 独立读取 Chrome 的固定注册与三项本地策略；当前用户注册匹配后，再核当前安装根的清单、receipt 和 23 个角色摘要。 |
| Edge 当前用户宿主 | 与 Chrome 分开读取，不能借 Chrome 的匹配宣称 Edge 已配置。 |
| Chrome / Edge 扩展 | 不读浏览器个人资料、Cookies 或扩展数据库，因此 Windows 下保持 NotChecked。allowed_origins 与分发清单不能证明扩展安装或启用。可靠证明“不存在”的能力尚未交付。 |
| Explorer 现代菜单 | NotChecked；包身份或程序集存在不能证明菜单真实注册可用。 |
| 图片 Worker / OCR | NotChecked；不启动 Worker，不据文件存在宣称 OCR 能力可用。 |

Configured 显示为“本地配置已核”。普通 Shell 的浏览器图像接收、浏览器实际采用的策略、宿主启动及图像翻译都未端到端验证，不能显示为整条链路就绪。非 Windows 实际入口返回 UnsupportedPlatform，且不读注册表、身份或安装文件。

## 只读证据与策略

每个浏览器分别读取 HKCU/HKLM × 32/64 四个固定来源。注册目标必须先等于当前安装根的固定清单路径；其他路径（包括 UNC/device）只报告不匹配，不读取其目标。当前用户注册缺失才报告本行 NotInstalled；拒读与 I/O 失败分别报告 Unavailable，不能伪装成未安装或策略拒绝。多来源冲突保持未核实。

注册位置和视图参照 [Chrome Native Messaging 官方文档](https://developer.chrome.com/docs/extensions/develop/concepts/native-messaging)。策略只解释本地 NativeMessagingUserLevelHosts、NativeMessagingBlocklist、NativeMessagingAllowlist 的受支持子集；明确的宿主拒绝且无同一来源允许例外，或明确禁用当前用户宿主且无机器宿主证据，才显示“检测到本地策略拒绝”。语义参照 [Edge 用户级宿主策略](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/nativemessaginguserlevelhosts)、[拒绝列表](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/nativemessagingblocklist)和[允许列表](https://learn.microsoft.com/en-us/deployedge/microsoft-edge-policies/nativemessagingallowlist)，并保留 [Chrome 对应策略入口](https://chromeenterprise.google/policies/?policy=NativeMessagingUserLevelHosts)。

不推断 HKLM/HKCU、两视图、云策略或浏览器配置文件的优先级。不同已配置来源的三字段整体有差异，即 NotChecked/EvidenceConflict，不能跨来源拼接例外。策略列表只接受最多 64 个、连续唯一 1..N 的数字名称与 ≤256 字符的受支持宿主名；非支持类型、名称或模式保持未知。没有本地拒绝证据也不等于浏览器允许。侧载、截图、扩展安装等其他策略及浏览器有效策略尚未读取；界面建议用户到浏览器策略页核实并联系管理员，不建议关闭安全功能。

注册值使用固定 64KiB 的 RegQueryValueExW 缓冲，不按原始报长扩大分配；策略值名称使用固定 3 字符 RegEnumValueW 缓冲，最多 64 次，前后核数。只支持严格终止且无嵌入空字符的 REG_SZ 和 4 字节 DWORD；超界、MORE_DATA、坏 UTF-16、ExpandString/其他类型保持未知或不兼容，不扩展环境变量。参照 [RegQueryValueExW](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regqueryvalueexw) 与 [RegEnumValueW](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regenumvaluew) 的缓冲/返回值约定；新增原语在 Linux 仅验证解码与错误映射，Windows P/Invoke 尚未实机执行。跨值/多键读取亦非原子快照，实际生效策略仍以浏览器为准。

安装文件仅固定两个元数据文件，各 ≤64KiB；严格字段、类型、重复/转义重复键、版本、精确允许来源和固定 exe 路径。receipt 必须包含既有 InstalledRoleFiles.Required 的全部 23 项，无多余/重复键，摘要为 64 位十六进制。各角色 ≤256MiB、合计 ≤1GiB，64KiB 分块哈希并检查取消；不加载程序集或执行 native 文件。读取前后逐层拒绝链接/重解析点。这是本地观察，不改既有授权 loader，不宣称能防御恶意并发换名的原子快照。只有 FileNotFound/DirectoryNotFound 归缺失；权限异常归无法核实。

## 生命周期与实际验证

刷新任务在后台进行。Invalidate/关闭先使结果失效，再由唯一后台 CancelAsync 发出取消；pending 一直保留到真实读取、取消回调和 caller bridge 都结算。取消不能启动第二个并行哈希任务；迟到 Configured 不发布。回调异常只记固定布尔状态并给出提示，不输出异常字符串。WPF 续体还核当前 Snapshot，避免发布后取消被“完成”提示覆盖。

本片是新增功能，无父生产对应类型，未把缺少 API 的编译失败当作父红测，也未制造红绿对。实际执行：r01 首版 57P；r02 补跨来源策略和缺目录等 61P；r04 前候选 68P；封存自检补原始注册值有界读取后，r10 最终 79P。前一轮 1661Core/2157Provider/AppWorker0W/E 全部保留为前候选，不能作为原始有界注册读取修订后的最终证据。最终 r11 Core1672P=父1593+新79、r12 Provider2157P，r13/r14 AppWorker各0W/E。自有临时目录使用真实清单/receipt/23 个合成文件；覆盖坏格式/版本/重复键、缺失角色/摘要变化/超界、链接保全、浏览器独立性、明确策略/例外/来源冲突、权限与 I/O 分类、取消/关闭/迟到/单任务/阻塞回调/回调异常。注册表和身份端口明确替代，未写真实 registry。

最终适用回归与构件、父结果多重集保留及全部 SHA-256 原件索引见 [固定证据](../test-evidence/2026-10-17-integration-status-page.json)。构建采用已有 SDK 10.0.401，所有 dotnet 命令在激活脚本前后显式禁用 ASP.NET 证书生成，全部 --no-restore；无新依赖、包、许可接受或环境安装。

独立只读审查未发现确定 P1/P2；两条建议已收紧：不支持的策略 value 名称保持未知、发布后取消时以当前快照决定提示。最终代码/测试与封存证据复审另附原件。真实 WPF 可见性、键盘、只读配置交互及 Windows 注册/ACL/包身份/策略/菜单/OCR、安装后的端到端与完整 P7/K 验收仍 NotRun。P7-06 只改 InProgress，validationStatus 仍 NotRun，其他项目状态不变。

旧图片间歇失败根因未知，生产复用仍 Off；250/600ms、来源/密码/身份/完整性/取消/清理门未改。Skia4.153.1 四包许可待批准，403 与 Actions 先前拒绝不重试、不换路。无 main/强推/部署/费用/凭据/安全权限变更或真实模型调用；未知锁、数据、日志与进程保全。
