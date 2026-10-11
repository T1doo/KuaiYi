# P0-03 OLE / 非激活联合实验

状态：真实 COM/OLE 实现已编译；Windows 运行 **NotRun**。这是独立实验工具，不是产品拖放入口或 P0 退出验收。只用随包 `selection-public.txt`，不调用模型、不读取剪贴板，不在磁盘写入掉落文本。异常文本只在私有继承管道和内存内比较，日志只有状态、字符数和布尔比较；请使用公开材料。

## 运行

Windows 11 x64 交互桌面、.NET 10 Windows Desktop x64 运行时、同一普通用户与同一完整性级别。解压 `KuaiYi-P0-probes-win-x64.zip` 到可写目录：

```powershell
pwsh -File ./run-ole-experiment.ps1 -Fault Sync -Offered CopyMove
```

脚本只启动本包两个进程，写 `NotEvaluated` 的启动记录，不自动操作桌面或标通过。源码目录运行先 `pwsh -File scripts/pack-probes.ps1`，再 `pwsh -File scripts/run-ole-experiment.ps1`。也可直接运行：

```powershell
./KuaiYi.OleProbe/KuaiYi.OleProbe.exe ./selection-public.txt ./target.jsonl
./KuaiYi.OleFaultSource/KuaiYi.OleFaultSource.exe ./selection-public.txt Sync CopyMove ./source.jsonl
```

监督窗口创建第三个私有子进程，子进程的 48DIP 浮钮注册真正的 `IDropTarget`；`OleInitialize`、WPF STA 消息泵、`RegisterDragDrop`/`RevokeDragDrop` 均真实调用。单击监督窗口 On 后绿色；启动和恢复均 Off。监督窗口允许 10 秒冷启动；一次格式查询或 Drop 的不可延长截止为 1500ms，包含全部自有COM资源清理、管道和调度开销，诊断不是精确实时保证。On 不能延长已开始的操作。Off 请求终止自有宿主，并异步等待最多2秒确认退出；退出未确认时保留所有权，禁止重启。确认后显式重启（Off）才能重新 On。

## 每场景核查与记录

1. Off 拖入/释放公开来源；来源 `QueryGetData`/`GetData` 计数都应为零，效果 NONE。On 后仅悬停/离开应允许一次有限格式查询，**GetData 零次**，提示不超过 96×64DIP。自己的按钮拖动是 WPF 鼠标移动，不经过 OLE 数据读取；检查无误点击。
2. Sync/Copy 与 Sync/CopyMove：拖放完成，返回 COPY，来源 `sourceWouldDelete=false`，target 的 `PublicFixtureComparison.matches=true`；无 optional async 接口也应成功。Drop 在返回之前复制 CF_UNICODETEXT，未用剪贴板替代。
3. 任意来源/MoveOnly：NONE，零格式查询/取数，来源不删除。LINK-only 的策略有离线验证，故障来源 UI 本轮尚未提供 LINK-only 枚举。
4. Async/Copy：GetAsyncMode=true，StartOperation 一次；目标从本 STA 创建 `CoMarshalInterThreadInterfaceInStream`，在 Drop 返回之后排队建立独立 STA，通过 `CoGetInterfaceAndReleaseStream` 获得本线程代理；GetData 与 EndOperation 在 worker 的 STA，成功效果 COPY。目标记录 WorkerUnmarshalBegin/WorkerReadBegin/WorkerEndAttempt 的 managedThreadId 与 apartment，应为不同于 DropBegin 的线程且 STA；源日志的 `item` 有 GetData/DoDragDropReturn/EndOperation，外层包含时间戳和线程/apartment。源 `DoDragDropReturn.active=true` 与快速 End 的消息调度可能竞争，不能硬性要求该瞬时布尔值；应有一次 EndOperation，最后 active=false。以实际时间顺序判定，不能以编译代表协商成功。
5. AsyncDisabled 与 StartRejected：同步回退、COPY、公开文本相同，不以不支持 async 为拒绝条件。StartRejected 的失败 Start 不调用 EndOperation；AsyncDisabled 不调用 Start。
6. GetDataFailure/Oversized/InvalidUtf16/MissingTerminator：明确错误、NONE、无文本比较通过。GlobalSize 在分配托管数组前检查，严格 UTF-16、NUL、50k scalar 限界；MissingTerminator 来源填充实际 HGLOBAL 分配大小，避免分配舍入的零填充造成假故障。
7. GetDataBlocked/QueryBlocked/ModeBlocked/AsyncGetDataBlocked/AsyncEndBlocked：来源故意阻塞其 STA 五秒；目标监督在固定截止后终止自有宿主，主监督计数持续增加，重启仍 Off。来源自己的坏 GetData 可能仍卡住五秒，杀目标不能保证修复来源进程。超时或强制结束无法保证已开始的 async operation 获得 EndOperation；需记录来源悬挂计数，不把进程退出冒充规范的正常完成。AsyncEndFailure 应记录明确失败、丢弃结果，EndOperation 不能因失败而重复调用。
8. 从来源窗口/浏览器拖入时，观察输入焦点与 `QueryEnd`、`DropEnd.foregroundUnchanged`。右键主动阅读必须允许键盘焦点；退出主动阅读记录实际前台恢复结果（Windows 可能拒绝 SetForegroundWindow，不能硬编码成功）。检查 NVDA 键盘遍历、Escape、100/150/200%DPI、多显示器与角落。
9. 每种来源/效果至少重复 20 次；重启、On/Off、快速离开和再次拖入；记录系统版本、程序版本、DPI、源应用、完整日志及人工结论。两个窗口正常关闭才写元数据文件。两份 JSONL 外层都有 utc/item；来源关闭记录 unfinishedAsync，监督记录 StopAttempt/StopOutcome/ExitConfirmed。只有实际持有进程 HasExited 确认才写 ExitConfirmed 并允许重启；未确认退出不能关闭监督窗口。若来源仍 active，强制退出该自有测试进程需单独记录。
10. 用 `-Fault QueryReleaseBlocked/QueryReleaseFailure/DropReleaseBlocked/DropReleaseFailure/AsyncReleaseBlocked/AsyncReleaseFailure` 分别运行。脚本将同名 `--cleanup-fault` 参数传给目标，来源保持真正 COM 传输。这是在 release 调用周围显式注入的探针钩子：Blocked 等5秒，Failure 在真实 release 返回后抛出测试异常；不是源 IUnknown 实际阻塞证明。核对 QueryEnd/DropEnd 只有在全清理完成后出现，CleanupFailed 后截止保留；清理阻塞和失败均终止并等待真实退出，不假成功。详细离线控制证据见 ole-cleanup-review.md。

## 需要真实实验才能成立的假设

| 假设 | 当前证据 | Windows 判据与失败处置 |
|---|---|---|
| 自定义 COM IID/BOOL/STGMEDIUM/POINTL 的 ABI 与系统代理相容 | 官方签名核实、交叉编译 | Sync/Async 真实来源完成且不崩溃；失败保留日志并修复，当前不宣称通过 |
| optional async 接口可由数据对象代理在 worker STA 再次 QI | 官方生命周期允许该方式 | Async 的 Start/End 计数与实际时间序列；QI 失败退为明确错误并由 origin EndOperation 清理，不静默成功 |
| WPF Dispatcher 在此 STA 能泵送系统 OLE 消息和来源回调 | 真实调用代码 | 悬停、取消、拖放和 async End 均能完成；若不能须独立原生消息泵方案 |
| 强制终止隔离宿主能保护控制 UI | 固定截止逻辑测试，Windows Kill 尚未运行 | 监督窗口仍可操作，超时进程确实退出；失败不显示成功恢复 |
| 非激活和显式阅读不会误夺源应用焦点 | NOACTIVATE/style/实际前台采样代码 | 全过程前台/键盘观察与 NVDA 证据；只读回 flag 不够 |
| 同步来源阻塞时产品浮钮还能拖动 | **本方案没有证明** | 同步 GetData 在子宿主 STA 阻塞，子浮钮也可能冻结；独立监督仍活跃不等于 SPEC E06 浮钮可拖动，需要进一步宿主/窗口设计实验 |

本探针只提取 Unicode。可靠 HTML/RTF 清洗、图片、URL 错误交互、生产端 epoch/RuntimeGate、拖放前台快照/应用拒绝策略、真实 UIA 拖放结合、并发恢复与产品动画仍未交付；未接正式 Provider。未来 UI 消费者只能使用 GenerateEnvelopesAsync + ProviderEventReceiver。私人 stdio 继承管道不代表生产命名管道 ACL 已实现。

## 官方依据

- [RegisterDragDrop 与 OleInitialize、消息泵](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-registerdragdrop)
- [异步拖放协商及同步 Drop 生命周期](https://learn.microsoft.com/en-us/windows/win32/shell/datascenarios)
- [GetAsyncMode 的 BOOL 签名](https://learn.microsoft.com/en-us/windows/win32/api/shldisp/nf-shldisp-idataobjectasynccapability-getasyncmode)
- [跨线程封送流](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-comarshalinterthreadinterfaceinstream)
- [CoGetInterfaceAndReleaseStream 失败时也释放流](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-cogetinterfaceandreleasestream)
- [未消费 marshal packet 必须由 origin apartment 清理](https://learn.microsoft.com/en-us/windows/win32/api/combaseapi/nf-combaseapi-coreleasemarshaldata)

这些是实现约束与待测方案，不是对所有来源兼容性或性能的保证。
