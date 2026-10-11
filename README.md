# 快译 / KuaiYi

Windows 11 x64 AI 翻译软件，完整产品与验收规格见 [SPEC.md](SPEC.md)。原始中文产品书保留原字节。

当前处于 P0 平台验证与独立核心实现阶段，尚不是可日常使用的完整桌面产品。当前实现包括状态/发送门控、强类型浮窗几何、流式高度跟随、有界 IPC/Native Messaging 帧、图像分片重组、独立 OpenAI Chat / Anthropic Messages HTTP 适配器、版本化提示词及测试专用本机 HTTP 故障服务。另有交叉编译的真实 UIA Broker/CompatibilityProbe 和 HWND GrowthHarness，以及真实 OLE 隔离宿主/同步与异步故障来源；它们的 Windows 实际运行尚未完成。

已运行的检查、失败修复与平台阻塞见 [开发进度](docs/progress.md)、[验收状态](docs/acceptance-report.md) 和 [需求追踪](docs/traceability.json)。不得将这些离线测试等同于真实 Windows UIA、OLE、焦点、截图、多屏 DPI 或安装验收。

最新离线 .NET 测试 847 项、扩展 Node 测试 63 项通过；旧错误晚到缺口见 [专项审查](docs/late-event-review.md)，最大分片 JSON 外壳缺口见 [协议边界审查](docs/protocol-boundary-review.md)。OLE 联合实验与未证实的宿主假设见 [OLE 探针说明](docs/ole-probe.md)。清理终结与退出确认修复见 [清理监督审查](docs/ole-cleanup-review.md)。新增独立 ImageWorker、真实原生 OCR/Shell 源码、双 EXE 开发身份清单与安装态验证脚本，见 [图片与身份说明](docs/imageworker-identity.md)。原生编译和 Windows OCR/菜单仍未执行。图片窗口的迟到状态与实际启动线程修复见 [UI 收尾审查](docs/image-ui-review.md)。最新子进程发布/启动终结竞态修复见 [清理竞态审查](docs/image-finalization-review.md)。最小 MV3 扩展、实际 NativeHost 分片/取消与授权边界见 [浏览器协议审查](docs/browser-native-host.md)。真实安装与浏览器取图未运行。GitHub CI 未运行。

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

浏览器截图 fallback 已因像素归属不能完整证明而关闭；只保留同源无凭据 PNG 原图。来源参数/安装配置匹配不等于浏览器调用者认证。App已实现逐次桌面确认后的单次本地接收代码；当前缺实际安装配置，默认不开放接收。见 [截图与来源复核](docs/browser-ownership-review.md)。

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
