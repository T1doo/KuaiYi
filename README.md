# 快译 / KuaiYi

Windows 11 x64 AI 翻译软件，完整产品与验收规格见 [SPEC.md](SPEC.md)。原始中文产品书保留原字节。

当前处于 P0 平台验证与独立核心实现阶段，尚不是可日常使用的完整桌面产品。当前实现包括状态/发送门控、强类型浮窗几何、流式高度跟随、有界 IPC/Native Messaging 帧、图像分片重组、独立 OpenAI Chat / Anthropic Messages HTTP 适配器、版本化提示词及测试专用本机 HTTP 故障服务。另有交叉编译的真实 UIA Broker/CompatibilityProbe 和 HWND GrowthHarness，以及真实 OLE 隔离宿主/同步与异步故障来源；它们的 Windows 实际运行尚未完成。

已运行的检查、失败修复与平台阻塞见 [开发进度](docs/progress.md)、[验收状态](docs/acceptance-report.md) 和 [需求追踪](docs/traceability.json)。不得将这些离线测试等同于真实 Windows UIA、OLE、焦点、截图、多屏 DPI 或安装验收。

最新离线 .NET 测试 1668 项、扩展 Node 测试 63 项通过；旧错误晚到缺口见 [专项审查](docs/late-event-review.md)，最大分片 JSON 外壳缺口见 [协议边界审查](docs/protocol-boundary-review.md)。OLE 联合实验与未证实的宿主假设见 [OLE 探针说明](docs/ole-probe.md)。清理终结与退出确认修复见 [清理监督审查](docs/ole-cleanup-review.md)。新增独立 ImageWorker、真实原生 OCR/Shell 源码、双 EXE 开发身份清单与安装态验证脚本，见 [图片与身份说明](docs/imageworker-identity.md)。原生编译和 Windows OCR/菜单仍未执行。图片窗口的迟到状态与实际启动线程修复见 [UI 收尾审查](docs/image-ui-review.md)。最新子进程发布/启动终结竞态修复见 [清理竞态审查](docs/image-finalization-review.md)。最小 MV3 扩展、实际 NativeHost 分片/取消与授权边界见 [浏览器协议审查](docs/browser-native-host.md)。真实安装与浏览器取图未运行。GitHub CI 未运行。

## 构建与离线测试

要求锁定的 .NET SDK **10.0.401** 和 PowerShell 7。已有环境专用 SDK 时先将其加入 PATH，不需要修改系统安装。

```powershell
pwsh -NoProfile -File ./scripts/bootstrap.ps1
pwsh -NoProfile -File ./scripts/build.ps1 -Scope Offline -Configuration Release
pwsh -NoProfile -File ./scripts/test.ps1 -Suite Offline
```

首次新增依赖才用 `build.ps1 -InitializeLocks`，审核锁文件后提交；普通构建使用 locked-mode。离线套件启动独立进程的 ASP.NET Mock Provider，仅绑定回环动态端口，运行测试后清理。测试不需要真实 API Key，不调用付费模型；生产项目不引用 Mock 工具。

完整原生构建入口为 `build.ps1 -Scope All`，缺少 Windows/MSVC 或尚未实现的原生组件时明确失败，不将托管子集称作完整构建。`test.ps1 -Suite Windows` 与 `-Suite Live` 目前分别报告平台/尚未交付套件和授权阻塞，不返回伪成功。

当前 Linux 环境的已下载工具位于仓库之外的 `/workspace/.kuaiyi-setup`。PowerShell 需使用可写的 XDG 缓存目录；见 [环境记录](docs/environment.md)。这些路径是此云环境说明，不是 Windows 安装路径。

`build.ps1 -Scope Managed` 构建全部 25 个托管项目（Linux 自动启用 Windows targeting）。`pack-probes.ps1` 生成有 x64 PE 头校验及摘要的框架依赖探针 ZIP；它不是产品安装包。执行与证据采集见 [Windows 探针说明](docs/windows-probes.md)。

## 安全与范围

正常启动必须 Off；每次采集及 HTTP 发送检查开关、任务身份和目标 origin 授权。请求凭据只添加到该请求的头，不进入提示词或共用默认头；禁止自动重定向。原文、图像和译文默认不持久化。当前 `SecretLease` 仅是内存凭据访问契约，Windows DPAPI 持久化尚未验证或交付。

主平台是 Windows 11 x64；Linux 用于可独立验证的逻辑与协议。正式签名、开发证书信任、真实模型调用和商店发布均有独立授权条件。

浏览器截图 fallback 已因像素归属不能完整证明而关闭；原图限同源无凭据 PNG、JPEG、BMP、受支持的静态 WebP 和 GIF（首帧），经原 Worker 准备本地预览，见[浏览器原图格式](docs/validation/browser-original-formats.md)。来源参数/安装配置匹配不等于浏览器调用者认证。App已实现逐次桌面确认后的单次本地接收代码；当前缺实际安装配置，默认不开放接收。见 [截图与来源复核](docs/browser-ownership-review.md)。

NativeHost 截止与读取完成的资源竞态已修复，见 [读取所有权复核](docs/browser-read-ownership-review.md)。

生产 App 本地接收与网络隔离边界、65项新增离线验证见 [接收端独审](docs/browser-app-receiver-review.md)。Windows原生角色和真实确认仍未运行。

确认等待阶段的跨模块预算修复、真实六秒等待及完整链路负例见 [确认等待审阅](docs/browser-confirmation-wait-review.md)。当前没有真实 Windows 安装、浏览器或确认界面的运行证据。

主动区域截图到本地 ImageWorker 的实际代码、38项共享截图测试和原生/Windows未验证边界见 [截图审阅](docs/screenshot-review.md)。框选界面仍接入 P0 实验窗口，完整翻译浮窗与模型发送未完成。

截图 CaptureHost 隔离、真实子进程故障测试与 GDI 同线程同步修复见 [宿主隔离审阅](docs/capture-host-review.md)。Windows 自身像素排除、原生编译和真实驱动退出仍未验证。

E06浮钮/远端OLE候选接线、实际子进程测试与精确Windows gate见 [分离审阅](docs/ole-presentation-review.md)。接收原始任务与结果租约复核修复见 [生命周期复核](docs/ole-lifetime-audit.md)，正常dev分支提供监督独审；真实注册组合/输入捕获/多屏/COM仍NotRun，缺原生桥DLL的审阅ZIP不能用于产品安装或宣称拖放可用。

P0实机受阻时独立推进的配置/设置切片见 [设置审阅](docs/settings-review.md)：不可变草稿、原子保存、迁移/恢复与秘密引用事务，真实WPF入口已交叉编译。Windows DPAPI/ACL/界面与位置接线仍未验收，P1整体未完成。

设置恢复与共享秘密引用的监督修正及先红后绿证据见 [恢复复核](docs/settings-recovery-review.md)。

共享秘密的profile绑定失效、定向任务取消与提交恢复通知见 [绑定复核](docs/settings-bindings-review.md)。

P1 正常启动现为红色实体浮钮与原生托盘，启动元数据加载、同用户同会话互斥、统一启停、位置保存/恢复和严格profile发送门控已接线；完整文字翻译与Windows桌面验证仍未完成。旧P0面板用 `--platform-review`，先退出已有主实例。见 [P1复核](docs/p1-shell-review.md) 与 [Windows待执行清单](docs/p1-shell-windows-checklist.md)。

P2 已接生产浮窗的逐帧几何、流式文字目的队列、缓存回看和跨DPI拖动重基；实际Windows品质未验证。审查：[P2复核](docs/p2-overlay-review.md)。本地CI入口为 `pwsh scripts/ci-offline.ps1`；公开流探针独立于正式程序，使用与操作见 [待执行清单](docs/p2-overlay-windows-checklist.md)。

P2监督发现的设置入口、生成中版本计数及窄条正文测量缺口已修，见[三项复核](docs/p2-review-fixes.md)；Windows仍未执行。

P3生产选区获取接线见[选区复核](docs/p3-selection-review.md)：真实Broker调用已有实现、稳定等待/来源身份/模式暂停/排除及600ms自有子进程截止。原文和译文区分，未接P4发送，不称完整P3或文字Alpha；真实Windows兼容仍NotRun。

P3 输入积压及取消读取所有权两项监督修复见 [修复报告](docs/p3-input-read-fixes-review.md)：旧版五个反例均失败，修复后定向72/72、完整.NET832/832与Node63/63。真实Windows仍NotRun。

P3硬截止补审见[绝对截止报告](docs/p3-absolute-deadline-review.md)：单调起点覆盖启动/三次读取/退出/最终校验，不依靠定时器准时触发。新15例，定向87/87；完整.NET847/847、Node63/63。Windows仍NotRun。

P3-04 源实现与审查包见[复制兼容报告](docs/p3-clipboard-review.md)：默认关闭、逐应用明确授权，独立STA完整有限快照及条件恢复，350ms等待和自有子进程绝对截止。新增118例，.NET965/965、Node63/63；26托管工程locked构建通过。真实Windows剪贴板/Ctrl+C/安装仍NotRun，完整P3与P4未完成。

正式文本Drop来源已接入Shell，144专项与完整1109 .NET/63 Node离线通过，见[限定独审](docs/text-drop-review.md)、[Windows待测](docs/text-drop-windows-checklist.md)。独立审查ZIP缺原生DLL和安装器，不是可日常使用软件；缺DLL会明确停用Drop。实际跨应用/COM/提示动画/DPI与P0/E06仍未验，P4模型调用闭环尚未接入。

父审确认原生来源检查后截止有缺口，已补最终本地资格检查，原17例7失败保留，修后17通过；全回归1126 .NET/63 Node。三层各自起点计时，没有共享原生起点或端到端1500ms证明，见[截止补审](docs/text-drop-admission-review.md)。

P3-05 热键事务、真实设置保存与公开占用器切片见[审阅报告](docs/p3-hotkeys-review.md)。99专项、27托管项目已验证；实际Windows占用/锁屏/Explorer/AltGr仍NotRun。独立框架依赖审阅包由 `scripts/stage-p3-hotkeys-review.ps1` 生成，含真实热键占用器，不是安装器；P4/P6模型动作尚未接入。

热键前级生命周期与已提交配置安全通知父审补正见[补审报告](docs/p3-hotkey-admission-review.md)：155专项通过，独立补审包由 `scripts/stage-p3-hotkey-admission.ps1` 生成；上一轮93b待签收，实际Native HWND与Windows消息队列仍NotRun。

P3-06 正式来源→生产 Provider→流式浮窗已接线；仅显式本地测试命令使用无密钥隔离配置，持续标识测试输出。调度/限额/取消/版本与真实 loopback HTTP 回归见 [文字闭环审查](docs/p3-text-translation-review.md) 与 [Windows 待测清单](docs/p3-text-translation-windows-checklist.md)。尚未完成真实 Windows 闭环、安装器或日常可用验收。

P3-06 父审指出测试配置可变为远程而仍标本地，已修不可变授权与 client 兜底；39 新反例/专项，旧失败保留，见 [测试模式边界审查](docs/p3-text-boundary-review.md)。其他父审及真实 Windows 验证未签收。

P4-01 已接统一地址/参数映射、能力编辑与明确确认的固定公开内容连接测试；测试不启用主开关、不采集内容、已有生成时拒绝。原本地测试模式边界保留，见 [配置闭环审查](docs/p4-profile-review.md)、[Windows待测](docs/p4-profile-windows-checklist.md)。完整P4与Windows文字Alpha未验。

P4父审补正：固定样本许可不再扩张普通内容会话授权，测试成功后真实上传仍独立确认；统一冻结总预算与全部草稿编辑撤销。见 [授权与预算审查](docs/p4-consent-review.md)。其他父审和完整Windows验收尚待完成。

已保存密钥空密码框的清除命令与预设清引用现先显式撤销测试再修改草稿，见[命令入口补正](docs/p4-secret-command-review.md)。生产命令体六项回归使用明确测试控件端口；实际WPF按钮和DPAPI仍NotRun。

P4-05会话内四风格、版本N预览与事实保真重生成已接源码，原版本/配置快照及三版本保护保留，见[会话与版本审阅](docs/p4-session-style-review.md)。实际WPF/语义质量/完整P4仍未验。

P4-05模板设置已接查看/复制/恢复/完整预览及真实配置→生产请求，见[自定义模板审阅](docs/p4-custom-templates-review.md)。固定base与发送权限独立于模板，原文仅user；真实WPF/模型语义/完整P4仍未验。

P4-04阶段预算高级配置、有效活动SSE/JSON读取已接生产路径，见[阶段超时审阅](docs/p4-phase-timeouts-review.md)。61专项与真实loopback/TLS部分验证不替代Windows/真实服务验收。

父审发现阶段转换与事件交付的截止竞争，已补绝对总预算/父取消/阶段期限检查，见[截止交付补正](docs/p4-completion-races-review.md)。新44项竞争回归通过，完整1712 .NET/63 Node；原阶段切片及本补正仍待父审，Windows整体验收未完成。

实际消费者延迟队列的总预算/阶段资格补正已接生产 Receive/Apply，见[目标队列补正](docs/p4-queued-delivery-review.md)。124新生产消费者专项与完整1836 .NET/63 Node通过，仍待父审及Windows整体验收。

已补自包含文字 Alpha 发布与受控本地启动入口，见[使用说明](docs/text-alpha.md)及[P0—P4交付缺口复核](docs/p4-text-alpha-review.md)。包内含固定.NET10.0.12运行时，无需在目标机安装.NET；仍无安装器/产品原生DLL，Windows闭环和整体验收未运行。

文字Alpha启动退出合同已补正：使用新仓库/独立小补正脚本指向原大包，旧大包保持原字节。子失败/原文比较Fail非零，NotRun为2，预检与限定会话分开，见[退出合同补正](docs/p4-alpha-exit-review.md)。Windows实际功能仍未验。
