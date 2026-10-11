# P3 生产选区获取接线复核

基线为监督限定签收 `4630831a8bee20c0ea13697418922aa1122df03f`。开工实查HEAD、干净分支及ls-remote一致，main保持51d0774；仅本KuaiYi保存云环境。断连通知后2026-10-11T17:36:04Z实际安全命令成功、HEAD/工作区保留，未删锁、换环境或另建写入者。没有安装、新协议、真实密钥、上传或模型调用。

原CaptureBroker已有真实GetSelection有界读取，但正式Shell没有调用；旧CompatibilityProbe才启动它。本轮复用Broker，新增正式父调用、手势/来源/选区合同及Shell接入，未重建空UIA接口。[Microsoft要求UIA用独立无窗口MTA线程](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-threading)；Broker维持[MTAThread]主线程，父进程持有实际启动的子进程。[低级鼠标钩子应使用独立消息泵并快速返回](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc)，正式SelectionMouseHook只记录左键按下/抬起、位置/时间、注入标记与前台HWND，经64项有界队列传出，不在回调做UIA、布局、剪贴板或网络。

SelectionCaptureCoordinator按真实拖选阈值及多击时间/位置识别手势；普通单击、注入、身份破坏和队列丢失不能成为候选。稳定延迟来自设置，默认180ms，新的按下会取消前次候选。捕获前后和UI目的队列最终门控都检查captureEpoch/mode、设置版本、HWND/PID/进程启动时间/路径及排除规则。自动Selection/Drop互斥；Off、Drop、锁屏或设置/移动暂停取消候选并移除自动hook。Broker中控件/范围实际矩形须关联按下/抬起点，不用工具栏点击加保留旧选区凑候选；自绘或不可确认的情况拒绝。UIA实际范围获取接口依据[GetSelection合同](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.textpattern.getselection?view=windowsdesktop-10.0)。

旧8层/64元素/250ms软预算及100002 UTF-16安全界限保留，每范围迭代也检查软预算。仅GetSelection返回范围调用有限GetText，不使用ValuePattern/Name/DocumentRange读整页。按提供者逻辑顺序拼接，不按屏幕Y排序。回包有限控件runtime ID、范围/控件矩形与进程启动时间；父进程验证身份、类型、JSON/二进制界限、UTF-8、软预算、Unicode标量、几何和额外帧。原始选区与清理文本只在内存，保留代码缩进/段落；1标量可用、默认12000软上限只记录待确认、50000硬上限拒绝。复制兼容未接入，生产路径没有Ctrl+C/读取剪贴板。

SelectionBrokerClient使用已有OwnedProcessSession/OwnedHostTermination，600ms覆盖启动/读取/退出等待，WaitAsync在父进程放弃结果；只终止启动者持有的子进程。清理保持单一所有权，启动或退出未确认时返回CleanupPending，迟到创建的子进程仍由原会话回收。不能凭CancellationToken声称阻塞COM已中止。60秒三次崩溃/硬超时按应用暂停30秒，其他应用不受影响，成功清计数。清理超过退出预算明确失败并保持所有权，不能标已退出或无限启动。

Shell正式入口创建父客户端与协调器，启停/设置提交/锁屏/移动/退出接门控和清理。默认Ctrl+Alt+D主动读取键盘选区，支持当前已保存Ctrl/Alt+A-Z组合的注册替换/冲突保留旧键；不是完整R14录制/成组事务。有效源快照进入Shell内存，提示真实字符数，用户可显式查看标为原文的窗口；无效捕获不清旧缓存，锁屏/退出清内存引用/原文窗口。没有把原文冒充译文，P4模型请求工厂/发送调度尚未接入。本轮网络发送为零，不能称文字Alpha或完整P3。公开浮窗探针明确enableSelectionPlatform:false，避免公开测试工具启用真实取词。

自动化验证50新增项，含实际自有Linux公开子进程IPC、卡死/迟到/取消/崩溃、启动未终结时禁止第二子进程、来源/排除/Off/Drop/暂停/关闭、去抖和目的队列迟到、手势/Unicode/软硬上限。首次45项中9失败（7项协议InvalidDataException边界、2项测试运行器把无配对代理项替换成合法字符），修异常合同后剩2失败；在测试体内构造原始UTF-16后46通过，再补Windows路径/启动结算4例，完整Core733+Provider77=810/810、Node63/63无失败/跳过。原断言和全部失败TRX/log保留，不以删除或降低断言通过。

25托管工程locked Release零警告/错误。第一次构建2个编译错误（managed GetBoundingRectangles实际为Rect[]、char→uint条件表达式）及修后构建日志保留。完整回归后只补真实hook故障的Off视觉与残留所有权处理，再由独立审查发布locked重编；核心/测试源码在完整回归后未变。Windows hook/UIA、真实跨应用、多屏DPI、焦点、字体/像素、DPAPI/ACL与P0/E06全部NotRun。完整正式Drop文本落点、复制兼容授权、全热键和P4调度明确未交付。

源/原始证据/历史Git版本验证及独立新包见[test-evidence/2026-10-11-p3-selection-summary.json](test-evidence/2026-10-11-p3-selection-summary.json)，待执行矩阵见[p3-selection-windows-checklist.md](p3-selection-windows-checklist.md)。P0/E06 Blocked、P1/P2/P3 InProgress，176追踪项完整、106整体验收仍NotRun。

新审查ZIP：1607193字节、73文件（app 44、公开probe 25），SHA256 `f511b7e4298e5d8bf877007d0803370e94e6489f0e4f264c7833659a83dae980`；9个PE32+ x64入口及逐文件manifest检查通过，十一旧包摘要未变。
