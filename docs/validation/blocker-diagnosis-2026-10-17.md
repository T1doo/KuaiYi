# 快译阻塞诊断：2026-10-17

从 `81f4e3f357374f018e8d3cb56a56a2189c629451` 原指定环境接续。实际HEAD/远端一致、工作区clean、单worktree、root唯一写入者；原成果及全部生产源码保全。本次只读取已有失败回执并做一次零下载的本地验证，没有新解码器、外围诊断工具、CMake/Windows运行或Actions访问。

## 逐项结论与最小处理

| 卡点 | 具体证据及已查清部分 | 本次处理 / 仍需的最小一步 |
|---|---|---|
| SDK命令入口 | 默认PATH无dotnet；既有`/workspace/.kuaiyi-setup/activate.sh`及SDK存在，激活后精确10.0.401、runtime10.0.12。App `--no-restore`交叉构建0警告/错误。 | 本轮显式使用已有激活入口，托管构建可用；无需下载SDK、改凭据或生产代码。这不提供Windows原生工具链。 |
| PowerShell入口 | 旧环境说明记载的7.6.6路径、默认PATH及已知工具目录目前没有pwsh，也未找到可复用的本地deb；旧记录不是本次安装证明。 | 已追加当前环境状态；未下载/安装。若必须执行原ps1入口，由环境提供方恢复已有获准的PowerShell，或在下述已有Windows环境执行。直接.NET验证已完成，不把它说成ps1成功运行。 |
| Skia明确授权 | 四份原始4.153.1 nuspec实际重读，均`requireLicenseAcceptance=true`、MIT表达式；随原生包的第三方声明还含DNG/LGPL/eCos等文本，不能将整个原生资产概括为纯MIT。 | 尚待用户明确是否授权这固定四包及随包条款；未运行restore/安装/加载/包代码。**这是用户授权门，不是已观察到的NuGet许可报错或403原因。** |
| 源码访问403 | 旧结构化回执仅记录两个`api.github.com/repos/mono/SkiaSharp/...` tree/release请求遭`Proxy tunnel403 Forbidden`。此前NuGet元数据/包曾取得，当前普通Git远端也可读；不能泛称整个网络或所有GitHub访问失败。 | 本轮零重试/零镜像/零替代下载。原回执没有完整HTTP/代理规则原件，无法确定具体策略规则或推断缺GitHub凭据；在当前禁止绕拒绝/扩权的范围内不能解除。需访问策略管理方对原拒绝给出合规处置结论；未明确允许前保持Blocked。**批准包许可不会自动解除403。** |
| Linux与Windows原生/桌面 | 当前Debian13 x64、无DISPLAY/WAYLAND，runtime只有NETCore/AspNetCore，无WindowsDesktop；默认PATH无cmake/cl/Windows SDK工具。原CMake明确要求WIN32/MSVC，旧本地原件分别是`cmake: command not found`及Linux平台拒绝。真实worker本轮仍返回UnsupportedPlatform。 | 无权限内代码修复可把Linux变成Windows/COM桌面。最小条件是一个已有获准SDK/MSVC/PowerShell的Windows x64环境；WPF/真实取词验收还需交互桌面。只装Linux CMake或.NET引用包不能闭合此项。 |
| 旧Windows NativeConfigure超时 | 原`tools/KuaiYi.ReuseLifecycleNative` configure-only夹具的已保存真实Windows回执：CMake根进程Exit0，configure/generate完成，ABI Done；失败是原30秒预算内WaitJobEmpty仍NonEmpty，Accounting两点Active2/Total33，随后救援清理确认，整体Exit3。尚未进入NativeBuild/四真实案例；不是Linux错误，也不是已证编译语法错误。 | 尚未修复。现有源码已经设置MSBUILDDISABLENODEREUSE=1；Embedded候选也真实失败，不能再把这两项当未试的确定修法。缺超时前成员身份/实际编译链接参数，不能断言是MSBuild或PDB服务。下一步应在获准的已有Windows环境，针对原步骤取得这组缺失的脱敏事实；本次不触发已拒Actions、不重跑CMake、不增探针或延长预算。 |
| P6完整隔离解码实现 | 当前项目/锁/已还原assets无Skia，包缓存也无Skia；现有ImageWorker仍为PNG检查后调用NativeOcr。完整隔离PNG/JPEG codec尚未交付。早期大nupkg/原生二进制artifacts本环境未见，只保留已提交元数据、许可原件及摘要。 | 依赖授权、源码/组件审查可用后，仍需实际实现与损坏/尺寸/取消/崩溃验证。批准许可、恢复下载或提供Windows三者中任一项，都不会自动补上这段实现。 |

固定四包为 `SkiaSharp`、`SkiaSharp.NativeAssets.Win32`、`SkiaSharp.NativeAssets.macOS`、`SkiaSharp.NativeAssets.Linux.NoDependencies`，均4.153.1。macOS包是当时候选托管依赖图中的传递项，不表示产品支持macOS。可审阅[原始许可证](../test-evidence/2026-10-16-image-isolated-decode-audit-originals__LICENSE.txt)、[原生第三方声明](../test-evidence/2026-10-16-image-isolated-decode-audit-originals__THIRD-PARTY-NOTICES.txt)和[原依赖核验](image-isolated-decode-dependency-audit.md)。许可决定只解除授权门，组件审查与实现仍各有证据要求。

## 本次实际验证

- 读取现有SDK信息一次、执行现有worker `--diagnose`一次；后者Exit0但诊断值明确是UnsupportedPlatform，不是OCR通过。
- 不重编译、不还原地运行3个既有平台/真实worker测试，3P/0F/0跳过；只证明显式平台失败及owned进程协议/退出符合预期，不计作Windows验收。
- App Release `--no-restore -p:EnableWindowsTargeting=true`一次，0警告/错误；未新请求restore、下载或运行Skia。测试前实际DLL摘要与构建后摘要分开记录，不把重编译程序集冒充旧执行构件。
- 对原Skia nuspec/许可、Windows Accounting/Embedded回执与本地Linux失败日志核父版字节/SHA；核项目assets无Skia、现有NodeReuse配置及旧Embedded失败。没有证据支持一个可在当前Linux上验证并解决原Windows Job残留的生产代码补丁，因此未猜测性改源码/配置。

关键原件：[Skia审查与403回执](../test-evidence/2026-10-16-image-isolated-decode-dependency-audit.json)、[Accounting失败](../test-evidence/2026-10-15-configure-accounting-windows-result.json)、[Embedded失败](../test-evidence/2026-10-15-configure-embedded-windows-result.json)、[旧本地平台拒绝](../test-evidence/2026-10-11-imageworker-native-blocked.log)。本次[固定证据及原始运行日志](../test-evidence/2026-10-17-blocker-diagnosis.json)包含当前环境、具体命令、实际DLL/源码摘要和旧证据完整性。

本轮只补环境说明、此诊断与证据，未声称Skia/403/Windows或原NativeConfigure卡点已解决。历史图片间歇失败根因仍未知，81f4e3f的测试同步修正保持；生产图片复用Off，P6/真实Windows/WPF/COM/模型验收及250/600ms、来源/密码/完整性/取消/清理门不变。无新费用、凭据配置、安全权限扩大、许可接受、真实模型、main/强推/部署；未知锁、数据、日志、进程均保全。
