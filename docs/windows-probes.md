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

必须按 SPEC 17.2 分别记录 Chrome、Edge（包括内置 PDF 文本层）、Adobe Acrobat Reader、VS Code 源码编辑/内置预览、Obsidian 源码/实时预览/阅读视图、记事本。待测逐视图矩阵见仓库 `docs/compatibility-matrix.json`（探针包根目录同名文件）；实际 OS/应用版本、窗口/文档类型、选区性质与证据仍为空，所有视图未运行。SumatraPDF 只属于扩展矩阵，不能替代核心应用或 Obsidian 任一视图。

核心可选/可复制文字视图各至少 20 次固定动作，覆盖单词、段落、长文、混合 Unicode 与位置；U/C 正确获取率目标 ≥95%，错误内容发送目标 0。不排除失败视图来提高分母表现，也不以截图替换可复制文字的划词验收。扫描 PDF 单列截图，不混入文字成功率；路径分别记录 U/C/D/S/N/B 或未运行，不使用一个应用一枚绿色勾选。

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

Chunk 原始上限为 190KiB，为 base64 与规范 JSON 外壳保留空间；完整序列化后的 UTF-8 JSON 仍不得超过 256KiB。新增 ImageChunkWire 完整帧编解码/重组测试，包括最大值、超一字节、ID/索引/多字节字段和完整 20MiB 输入。输入字节限界不代表图像格式/像素验证或浏览器已接入。

它未注册为浏览器 NativeHost，也未实现生产来源 allowlist、桌面 ACL 连接、扩展真实传图或模型调用。浏览器原生宿主和协议编码器不能混为一谈，实际 Chrome/Edge 集成仍为 NotRun。
