# 已安装浏览器 UIA 验证边界

本次承接 P0-02、R03 的来源兼容性工作，前置代码来源为 `220060f813a3640487d593920a1b1824aa779a00`，最新文档提交为 `0fafe7254f6a79aba96e1567388c690d4ae0477f`。已通过的 hosted GetData 子门不重复执行；P0 整体与 E06 物理桌面验收没有因此通过。

使用既有免费 `windows-2022` runner。只启动固定安装路径、Authenticode 状态 Valid 且发布者 Google LLC / Microsoft Corporation 的现有 Chrome / Edge；不安装浏览器、驱动、扩展，不接受软件许可，不使用账号或既有浏览器 profile。版本由实际文件读取。无 headless、sandbox 关闭、强制 accessibility、剪贴板或模型调用。

| 来源 | 固定分母 | 当前状态 | 覆盖边界 |
| --- | ---: | --- | --- |
| Chrome HTML / readonly textarea / editable textarea | 每视图 20 | NotRun | 真实可见浏览器与生产 UIA Broker |
| Edge HTML / readonly textarea / editable textarea | 每视图 20 | NotRun | 同上 |
| Chrome/Edge 图片、Edge 内置 PDF | 未执行 | NotRun | 此批不验证图片入口或 PDF provider |
| Acrobat、VSCode、Obsidian | 未执行 | NotRun | 未核实安装身份，不假定缺失或可用 |
| 用户 Win11 物理划词、E06 外部拖动、焦点、多屏 DPI | 未执行 | NotRun | CI 管理桌面不能替代 |

120 个固定动作包括词、单字、段落、长文、混合 Unicode（含代理对和组合字符）、公式、首中尾位置及跨段落。公开离线 HTML 的 JavaScript 仅设置真实 DOM/control selection、focus 和本次随机 marker；actual 文本唯一来源为未经修改的生产 `SelectionBrokerClient` → `CaptureBroker` → `TextPattern.GetSelection()`。严格比较选择子集，只统一 CRLF/LF；不得 trim、读取 DOM 返回选中文本、读取 UIA Value/Name 全文或复制回退。

每个动作只取一次，不重试过滤失败；超时与原有暂停规则保留，同一视图复用 client，下一取词前确认原始 broker owner 已清理。版本、20/20 分母、正确数、错取数、有限状态分布与清理证据写入白名单摘要。每视图须观察正常 DONE，且至少 19/20 正确、错取为 0，确认 broker、应用 Job 和临时 profile 清理才可 Pass。整体超时、异常、未完成序列不得以 95% 阈值通过。缺软件/签名/前台/选择/清理不能削减分母。

浏览器以 suspended 进程创建，先绑定本次独占 Job 再 resume，失败不允许 breakaway 或继续启动。只操作 Job 所属 HWND，终止仅作用于持有的 Job/启动失败时持有的 process handle；Job ActiveProcesses=0 且原始 root exit 才确认清理。原始 stdout/stderr 丢弃任务须实际完成。若未确认清理，保留 owner/profile、停止后续视图并报告 Fail，不能靠“已请求终止”判为通过。

依据：[Microsoft TextPattern.GetSelection](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.textpattern.getselection?view=windowsdesktop-10.0)、[Job Objects](https://learn.microsoft.com/en-us/windows/win32/procthread/job-objects)、[官方 windows-2022 镜像清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md)。清单仅用于选测试目标，实际存在与签名仍在 runner 核实。

本地源码类型检查、xUnit 和 PowerShell receipt 测试仅验证源码及边界逻辑，不能作为 Windows UIA 结果。原有历史 progress/raw evidence 保留；本轮仅新增本文件和脱敏结果，不向公开仓库上传原始运行日志、进程 ID、安装路径、标题或取词内容。

本地验证：实际探针源码及生产 SelectionBrokerClient 的独立类型检查 0 warning / 0 error；原 xUnit 断言直接执行 125/125（含新 fixture 的 5 个用例）；PowerShell 白名单摘要 26/26，含 19 次正确后整体超时、异常和未 DONE 的拒绝用例。Windows locked restore、构建和真实 UIA 结果仍待本分支 runner 执行。
