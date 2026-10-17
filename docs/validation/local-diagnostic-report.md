# SPEC 14.4 / P1-06：会话诊断与本地报告

本次从实际 clean HEAD `9765f0bc7f2ef6ee099cb8d06d2fb80f405a933e`、`dev/blocker-diagnosis` 正常接续，开发分支为 `dev/local-diagnostic-report`。默认网络 `ls-remote` 确认原远端同 SHA；与先前 9765 相同，0 ahead/0 behind。8b902 是其祖先，原高水位交付不重做。仅一个 worktree，root 为唯一观测到的活动写入者，high_water_review 只读；未发现本轮前的其他更新，不将本地观察当作对所有外部实例的全局证明。

## 源码确认的缺口与本次交付

不是根据 P1-06 的 NotStarted 标签推断：父版 `ShellWindow.Report` 只把异常类型写 stderr，设置“隐私与诊断”页只有复制授权和内容清理。生产源码没有统一日志记录模型、用户报告预览或导出入口。已有 OLE/UIA 诊断工具是有界实验工具，不是这个生产用户功能；已有 MockProvider/DevelopmentTranslationBoundary 已实现，不重复实现。

现在设置底部提供“预览脱敏诊断”，即使配置只读也可用；打开正常的只读报告窗口，查看后可选择“将所示报告导出到新文件”，或清除本会话诊断。报告入口不运行能力探针、不捕获内容、不读秘密、不访问模型或上传诊断。界面代码已交叉构建；Linux 不证明真实 WPF 按钮、键盘或可见性已通过。

`DiagnosticLog` 默认在本地会话内存中保留最近最多256条元数据，退出清空，不默认写磁盘日志、不建立内容历史或遥测。SPEC 14.4 限定允许的日志内容，没有要求默认持久化日志；本切片选择会话保留。只有用户看过报告并明确选择本地新文件，才把所示快照写出。

数据 API 只接受固定事件/错误枚举、UTC 时间、数字、临时 GUID 和 Version；没有原文/译文/OCR/图片/剪贴板/标题/路径/endpoint/model/密钥/服务端正文等自由文本参数。异常按固定类型分类，不读取 Message、ToString、StackTrace 或 InnerException。当前生产接入启动、Shell错误、捕获完成/错误及文字生成开始/结束；未测的耗时/字节数/重试次数显示 Unknown，不把重试未知写成0。报告列出 OS 数字版本、屏幕工作区与DPI、五个组件程序集版本；所有尚未执行的能力测试明确 NotRun，不把“组件存在”当成功。

`DiagnosticReportSession` 冻结所示报告，预览撤销/关闭与发布共用门；原取消回调在独立任务中实际结算，未结算时保留导出所有权，拒绝第二个导出。外部取消仅桥接到这个任务，发布时仍直接检查外部 token。取消/回调失败只保留枚举。导出使用自己新建的临时文件和 no-overwrite 移动，检查本地路径、链接和打开后的实际路径；取消只清理确认归属的临时文件，归属未知则保留。路径检查与 Move/Delete 是分步操作，不宣称抵御同账户恶意进程在两步之间换名的原子隔离。

## 本轮执行记录

- r01：28项新增便携/实际本地文件专项通过。
- r02：App首构建1项C#声明语法错误，精确源码与失败log保留；r03修正后0警告/0错误。不是产品红绿。
- r04：当时Core完整1584P。独审后继续加取消回调反例，不把此批当最终源码回归。
- r05：新诊断代码自身的2条取消反例实跑2F（同步回调抛错/阻塞）；r06修后30项专项P。不是历史父版功能的红绿。
- r07：当时Core完整1586P；随后继续外部取消审查。
- r08：外部取消“导出提前完成”的初始假设实际1P，未证实；保留原件，不算产品红。
- r09：补准确的预览锁响应断言后1F，证明 linked CTS 的释放等待取消回调时持有预览锁；r10同测试文件字节31项专项P。

最终r11完整Core **1587/1587**，r12完整Provider **2157/2157**，均0失败/跳过；r13 App与r14 ImageWorker Release交叉构建各0警告/0错误。父Core1556结果行全部保留，仅新增31；父Provider2157结果行完全保持。代码独立只读审查通过。最终适用回归、构建、原件SHA与DLL绑定见 [本轮证据索引](../test-evidence/2026-10-17-local-diagnostic-report.json)。全部旧测试与旧证据保留；未把新增功能不存在时的编译缺类型当红测，也未降低断言制造通过。

## 完整 SPEC 核查后仍未交付的部分

| 原范围 | 当前源码依据 | 未完成部分 / 实际依赖 |
|---|---|---|
| P0、P3/P5真实跨应用取词/复制/OLE | `CaptureBroker/Program.cs`、`Windows/SelectionDesktop.cs`、`App/SelectionFlow.cs`、`Windows/OleTargetRegistrar.cs` | 实际UIA provider、密码/完整性、窗口来源、剪贴板和COM需要对应Windows桌面验收；有源码不是已通过。250/600ms原门保持。本轮另看到父客户端 `ElapsedMs>250` 与Broker `>=250` 的边界候选，尚未复现/修改，不报已解决。 |
| P1/P2浮窗与设置 | `App/OverlayPresentation.cs`、`Windows/MonitorTopology.cs`、`Windows/WindowsSecrets.cs:CurrentUserDpapiProtector` | 混合DPI/焦点/高对比/屏幕阅读器/真实DPAPI等不能在Linux实测；本次诊断WPF入口同样待验。 |
| P4完整文字Alpha/质量 | `App/TranslationFlow.cs`、`Providers/TextTranslationCoordinator.cs`、两协议Provider | 已有真实HTTP适配与严格本地fixture；跨应用到真实桌面、真实模型语义/质量/费用验收未完成，未授权真实模型。 |
| P6-02完整图片解码和常规缩放 | `Imaging/ImagePolicy.cs`、`LocalImageFile.cs`、`ScreenshotNormalization.cs` | 仍是PNG头检查；普通多格式解码、EXIF和任意图像缩放尚未交付。大截图非恒色缩放仍拒绝并要求小截图。Skia许可/源码403门保持，不换路或新安装。 |
| P6生产图片翻译/Beta | `App/TranslationFlow.cs:RegenerateResult`、`App/OfflineScreenshotCoordinator.cs`、`Providers/DevelopmentTranslationBoundary.cs` | 正常Shell对图片仍明确“尚未接入翻译”；图片协调器只接受无凭据loopback fixture，不能把它接到真实端点冒充已完。图片复用Off；旧图片间歇失败根因未证。 |
| P6-03本地OCR | `Imaging/PackageIdentity.cs`、`NativeOcr.cs`、`ImageWorker/ImageWorkerSession.cs` | 本地OCR辅助路径已有实现；真实Windows包身份、语言包与原生桥验收仍需真实环境，不等同图片理解。 |
| P7安装后右键与完整安装链 | `ShellExtension/ExplorerCommand.cpp`、`extensions/chromium/*`、`packaging/identity/AppxManifest.xml`、`scripts/package-identity.ps1`、`scripts/test-installed-identity.ps1` | 菜单/扩展/宿主及身份包源码已有；仓库没有完整Inno每用户安装、升级回滚、卸载事务链。安装注册/签名/干净Windows验证未交付，不能仅凭NotStarted称已有入口全无实现。 |
| P8完整验收 | SPEC 16.10与17章，`docs/traceability.json` | 100文本/60图片人工语义基准、8小时浸泡、完整A—L与Windows11普通用户交互验收未完成；未提升整体状态。 |

这里只交付14.4/P1-06的可移植有界诊断切片，P1-06更新为InProgress，整体验证仍NotRun；并非完整P1或v1.0验收。GitHub Actions访问先前拒绝，本次不查询、不调度、不换路；运行数Unknown。没有main/强推/部署/费用/真实模型/凭据或安全权限变更，没有接受新许可、安装或运行Skia，没有重复环境诊断，没有删除未知锁、数据、日志或进程。所有dotnet命令在activate前后显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`。
