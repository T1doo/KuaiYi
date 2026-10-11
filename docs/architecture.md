# 当前实际架构与生产缺口

本文件描述已落地的 P0 切片；正式产品仍以 SPEC 的模块和阶段为准。

```mermaid
flowchart LR
  Test[离线自动化] --> Gate[Core RuntimeGate]
  Gate --> HTTP[Providers / 真实 HTTP]
  HTTP --> Mock[测试专用本机 Mock Provider]
  Growth[公开文本 GrowthHarness] --> Geometry[Core 物理像素布局]
  Geometry --> Win[Windows Win32 HWND]
  Probe[显式 CompatibilityProbe] --> Broker[无窗口 MTA CaptureBroker]
  Broker --> UIA[目标应用 TextPattern 选区]
  Broker --> Frames[Contracts stdio IPC 帧]
  NativeTest[独立 NativeMessagingProbe] --> NativeCodec[Contracts Chromium 帧编码]
```

- Core：纯状态/身份门控与布局，net10.0，无 WPF、模型请求或用户内容持久化。请求替换/停用取消旧身份；实际每次发送由统一门控原子启动，响应再检查身份。还未交付完整多任务 coordinator/会话版本库。
- Contracts：内部 IPC 和 Chromium Native Messaging 是两个独立编码器，先校验无符号长度再分配。图像重组属于单连接串行所有权，有界内存、索引、摘要与 30 秒绝对截止；Commit 仅交付字节，不启动模型。实际 named pipe ACL/session/角色授权尚未实现。
- Providers：每请求独立凭据头，禁自动重定向；双协议 JSON/SSE、有限重试、总/首包/空闲截止、输出限界与晚到门控。Anthropic 按开始/活动块/最终原因/结束校验，只暴露 text 块。SecretLease 仅内存访问，不替代 DPAPI；端点风险设置、能力选项、完整错误 UI 和两阶段图片流水线尚未接入。
- Windows：Win32 坐标/监视器/窗口 Region/完整性级别实际 PInvoke。WPF 及 UIA 属于 Windows 框架，桌面实际验证不能由交叉编译代替。
- CaptureBroker：当前为 P0 一次性进程协议，真实 UIA 在 MTA 获取；父进程以硬终止处理无法取消的 COM 阻塞。仍需连接生产采集手势、状态 epoch、长驻 broker 重启/熔断、授权复制兼容与安全 IPC。
- tools：Mock Provider 只供测试；GrowthHarness 是公开固定文本视觉实验；CompatibilityProbe 对公开文件实际选区比对；NativeMessagingProbe 是显式 echo 实验。均不作为完整快译 App 的替代品。生产项目不引用测试工具。

解决方案有 11 个实际可构建项目。未创建空 App/Shell/OCR/扩展接口来冒充完成：正式 WPF App、托盘/设置/热键/DPAPI、OLE、ImageWorker、ShellExtension、浏览器扩展/NativeHost、安装/身份包仍是明确待实现模块。P0 真实平台退出门槛与其必要的 Windows/原生工具链证据仍为 Blocked。

可运行的检查及失败修复见 progress.md，Windows 操作和限制见 windows-probes.md，176 个规格条目见 traceability.json。所有 A—L 整体用例保持 NotRun。
