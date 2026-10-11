# P0 Windows 探针使用与验证边界

需要 Windows 11 x64、交互桌面和 .NET 10 Windows Desktop x64 运行时。探针是框架依赖开发工具，不是快译产品或安装包；不注册系统集成，不配置密钥，不连接模型。当前所有 Windows 实际运行结果为 **NotRun / Blocked**。

在开发仓库用 `pwsh -File scripts/pack-probes.ps1` 生成 `artifacts/KuaiYi-P0-probes-win-x64.zip`。解压后含三个各自完整的程序集目录、公开文本、内容摘要清单和本说明。打包会校验每个 exe 的 PE32+ x64 头，避免 Linux 交叉编译默认生成 ELF 启动器的问题。需要另行完成 Windows 实际启动验证。

## 真实选区探针

```powershell
./KuaiYi.CompatibilityProbe/KuaiYi.CompatibilityProbe.exe ./KuaiYi.CaptureBroker/KuaiYi.CaptureBroker.exe ./selection-public.txt
```

五秒内切换到被测应用，选中公开文件中的全文并将鼠标留在选区内。探针快照前台 HWND/PID/鼠标位置，启动无窗口 MTA Broker，读取 UIA `TextPattern.GetSelection`，在内存中比较公开文件，stdout 仅打印状态/数量/耗时/PID/前台是否保持。原文通过子进程私有 stdio 帧传回，不打印到日志或落盘。禁止拿私人文件作为此兼容性测试输入。

搜索上行最多八层、候选最多 64；候选的焦点/鼠标祖先必须到达原始窗口，不能仅依靠同 PID。软预算 250ms；父进程 600ms 硬截止实际终止子进程，此截止包含冷启动，因此首次运行可能超时，应记录首次与重复运行，不移除截止来制造通过。密码候选、终端、快译进程和高完整性目标拒绝。没有用 Name/Value/DocumentRange 或剪贴板兜底。范围读取有 UTF-16 与 Unicode 标量上限，不能把截断文本视为成功。

这只是用户显式运行的 P0 探针；尚未连接正式鼠标手势、epoch、授权复制、熔断恢复、后台重启 Broker 或生产 IPC ACL，不能据此宣布 R03 完成。

至少记录 Chrome、Edge、Adobe Acrobat、SumatraPDF、VS Code 编辑视图及预览视图、Notepad 的实际 OS/应用版本、窗口/文档类型、前台状态、选区性质与 JSON 结果。PDF 分别测试含文本和扫描件；预览、跨行与多范围分别记录。扫描件不应被伪造为 UIA 文本成功；需要后续截图链路。

## HWND / 生长探针

```powershell
./KuaiYi.GrowthHarness/KuaiYi.GrowthHarness.exe ./growth-probe.csv
```

初始红色 48 DIP 按钮可拖动；单击切换绿色并展开公开固定文本流。右键菜单可收起、重开内存结果、重新生成公开流、切换主动阅读及退出。只处理内置公开文本，不自动获取前台内容。

原生 HWND 使用 PMv2 manifest、TOOLWINDOW/NOACTIVATE、物理 `SetWindowPos` 和圆角 Region；WPF 内容按 DIP 排布。展开 220ms、收起 180ms，文本每 50ms 合并测量，高度跟随常数 70ms；降低动画设置时即时落位。文本固定为最终宽度。方向/屏幕冻结；显示拓扑/DPI 事件取消该布局并重新夹紧锚点。帧监听在稳定后停用。

CSV 对每个实际设置记录预期/实际 HWND、锚点包含、工作区包含与前台比较。最多保留 100000 条，超限在 summary 中记录丢弃数量。关闭时才写磁盘，避免文件 IO 改变帧节奏。记录的是布局设置/读取采样，**不是屏幕实际呈现延迟或 FPS 验收**。摘要明确写 `desktopAcceptance=NotEvaluated`，不能把生成文件本身当作测试通过。

测试四角/四边、负坐标副屏、100/125/150/175/200% 混合 DPI、不同任务栏、跨屏拖动、显示器移除、读取模式和前台编辑输入。需要人工视觉评审及真实呈现/帧时间证据；尚未接入生产重开/版本控制、工作区变化覆盖、OLE Drop 或键盘焦点归还完整合同。

## Native Messaging 离线证据

`KuaiYi.NativeMessagingProbe --probe` 是仓库测试工具，通过独立进程 stdio 接受有界 Ping 并返回 Pong；stdout 没有普通日志，产品命令明确拒绝。离线测试真实启动进程验证双向两帧。另测 UTF-8 字节上限、未完成帧、无符号伪造长度和 Begin/Chunk/Commit 的顺序/总量/摘要/截止。

它未注册为浏览器 NativeHost，也未实现生产来源 allowlist、桌面 ACL 连接、扩展真实传图或模型调用。浏览器原生宿主和协议编码器不能混为一谈，实际 Chrome/Edge 集成仍为 NotRun。
