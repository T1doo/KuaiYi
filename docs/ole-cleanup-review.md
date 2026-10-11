# 清理终结与退出确认专项

基线 `b8568c4791008d47102fc2dfc9e20e51960b2a93`。独审发现实际错误：QueryEnd 在 `using reference` 的 Dispose 之前；同步 Drop 的 Result/DropEnd 在 reference/capability 清理之前。监督端看到 End 就撤销截止，后面的 COM Release 可能无保护。之前 158 项没有证明这条路径。

## 复现与修复

将原监督接收规则提取为生产探针实际共用的 OleCallWatch，然后通过真正 IPC 编解码回放原 JSON（无 CleanupCompleted 属性）。Query/Drop 两项 **修复前 2/2 失败**，原始 TRX 与该接收代码/测试摘要见 test-evidence/2026-10-11-ole-cleanup-before-fix.*。这证明原控制规则会提前撤销；没有在 Linux 执行 Windows COM，也没有制造源 Release 的 Windows 运行证据。

发送端现在显式注册自有资源到 OleCleanupScope，反序释放，全部尝试并聚合清理/正文错误；一次不明失败释放不重复调用。QueryEnd 在资源清理返回以后发出；同步 Drop 先完成 capability/reference/medium 清理，再发 CleanupComplete、私有结果、最后 DropEnd。异步仍经正确封送；worker 引用及 OleUninitialize 完成、origin 的 stream/EndOperation/capability 清理完成后才发布终结。失败只发 CleanupFailed，不发终结；监督原绝对截止仍有效。

IDropTarget 参数改为借用原生 IDataObject 指针，调用期间显式 AddRef、当前 STA 内释放；没有由参数 RCW 的最终 GC 承担延后释放。跨 STA 仍使用 CoMarshalInterThreadInterfaceInStream/CoGetInterfaceAndReleaseStream，不能把借用指针送到 worker。系统/OLE 调用方自己持有的引用不属于探针资源，也不能通过该清理确认声称第三方应用整体已恢复。

接收端验证 ID/epoch/阶段及 CleanupCompleted；Drop 还必须先有匹配 CleanupComplete，才能接受私有 payload。该非终结确认不撤销截止；清理失败撤销 payload 许可，终结须最后到达。正常窗口关闭也有 ShutdownBegin/ShutdownEnd，RevokeDragDrop 与 OleUninitialize 同样受保护；这三种终结的旧格式/阶段/迟到规则均有自动化测试。超时的终结不能抢在下一次计时器 tick 前撤销超时。

## 有界退出确认

OwnedHostTermination 持有本任务 Process.Start 返回的对象，只请求终止这个子进程，不从外部 PID 打开进程，不终止未验证的后代。Kill 返回只进入请求状态。异步等待预算 2 秒，以该持有进程的 HasExited 判断实际退出；状态区分 Running、TerminationRequested、ExitConfirmed、TerminationFailed、ExitUnconfirmed。

失败或未确认时保持所有权，禁止重启新宿主和关闭监督窗口；后续迟退观察可转为 ExitConfirmed。确认后才释放持有对象并允许显式重启，启动仍 Off。旧事件在终止等待期间全部忽略。退出预算控制等待，不代表已退出；没有确认时不会写 ExitConfirmed。

## 可执行证据边界

本轮新增 **26** 项：20 项资源作用域/实际协议编解码/退出状态，6 项独立控制进程。总 Core107 + Provider77 = **184/184**，0 失败/0 跳过。OleControlFixture 是测试专用 net10 进程，无 COM/模型实现；以生产共用 Scope/Watch、真实继承管道和 Process 请求终止及确认 Linux 退出。它不进入 Windows 探针包。

独立进程覆盖 Query/Drop 正常、阻塞清理（等待控制命令，无超时自解）、异常清理。正文完成不等于清理完成；阻塞/失败没有 End，监督固定截止到达后真实终止并等待退出。状态测试另覆盖已退出、等待期间迟退、终止失败、未确认、等待任务完成但 HasExited 仍 false，以及无法观察持有进程时不请求终止。

Windows 故障来源与运行脚本新增 Query/Drop/AsyncRelease Blocked/Failure，显式给目标探针 `--cleanup-fault NAME`。来源仍为实际 COM IDataObject；目标在真实 release 调用周围注入 5 秒延迟/异常。这是控制路径注入，**不是第三方 IUnknown.Release 实际阻塞或 HRESULT 失败的证明**。默认 None 无注入；正式软件不能引用这些测试工具。真实 Windows COM 清理、代理、强杀退出、焦点与恢复 UI 全部 NotRun。

E06 的同步子浮钮可能冻结问题仍未解决，独立监督可响应不能代替 E06 验收。P0-03 未关闭，全部 106 整体验收仍 NotRun。详细 Windows 操作见 ole-probe.md，下一可推进项为原 P0-05 身份包/ImageWorker OCR 工程与安装态脚本准备；实际安装、证书信任另需授权。

## 官方依据

[IUnknown::Release](https://learn.microsoft.com/en-us/windows/win32/api/unknwn/nf-unknwn-iunknown-release) 会执行引用释放/最终对象释放；[ReleaseStgMedium](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-releasestgmedium) 根据 medium 所有权释放存储或调用提供方的 IUnknown::Release。因此不能把清理前的正文完成当作原生调用组结束。
