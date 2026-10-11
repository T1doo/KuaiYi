# 快译 / KuaiYi

Windows 11 x64 AI 翻译软件，完整产品与验收规格见 [SPEC.md](SPEC.md)。原始中文产品书保留原字节。

当前处于 P0 平台验证与独立核心实现阶段，尚不是可日常使用的完整桌面产品。当前实现包括状态/发送门控、强类型浮窗几何、流式高度跟随、有界 IPC/Native Messaging 帧、图像分片重组、独立 OpenAI Chat / Anthropic Messages HTTP 适配器、版本化提示词及测试专用本机 HTTP 故障服务。另有交叉编译的真实 UIA Broker/CompatibilityProbe 和 HWND GrowthHarness；它们的 Windows 实际运行尚未完成。

已运行的检查、失败修复与平台阻塞见 [开发进度](docs/progress.md)、[验收状态](docs/acceptance-report.md) 和 [需求追踪](docs/traceability.json)。不得将这些离线测试等同于真实 Windows UIA、OLE、焦点、截图、多屏 DPI 或安装验收。

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

`build.ps1 -Scope Managed` 构建全部 11 个托管项目（Linux 自动启用 Windows targeting）。`pack-probes.ps1` 生成有 x64 PE 头校验及摘要的框架依赖探针 ZIP；它不是产品安装包。执行与证据采集见 [Windows 探针说明](docs/windows-probes.md)。

## 安全与范围

正常启动必须 Off；每次采集及 HTTP 发送检查开关、任务身份和目标 origin 授权。请求凭据只添加到该请求的头，不进入提示词或共用默认头；禁止自动重定向。原文、图像和译文默认不持久化。当前 `SecretLease` 仅是内存凭据访问契约，Windows DPAPI 持久化尚未验证或交付。

主平台是 Windows 11 x64；Linux 用于可独立验证的逻辑与协议。正式签名、开发证书信任、真实模型调用和商店发布均有独立授权条件。
