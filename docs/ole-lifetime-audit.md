# OLE 接收原始任务与结果缓冲区复核

本轮在 `d4fbc7e432247d5babd4a34f11d9b22012ec3af2` 固定基线上先只读核对源码、236 项源码摘要和 52 项原始证据摘要，再修复发现的问题。这是工程自查及回归；监督方的独立签收仍待执行。成果准备通过正常 `dev/p0-foundations` 推送提供审阅，不修改 main，不强推，不部署或发布。E06 实现仍 InProgress、验证 NotRun，P0 Blocked，176 项追踪及 106 条整体验收保留。

## 发现与修复

1. 注册器尚在 `RegisterAsync` 内、原 task 尚未发布时，实际 child 已退出和注册器报告尚无注册，不能证明整个原接收操作结束。此前终结器遗漏原接收路径，此时可提前释放 activation lease。现以一次性 Run admission 状态覆盖整个原始启动、读、注册、清理路径；重复 Run 不改变已有状态。原路径实际返回之前，终结和重用均拒绝。
2. 对 `deliver(...).WaitAsync(cancel)` 的等待被取消，不表示原回调结束。此前 finally 会立即清空回调仍持有的真实数组。现将读租约交给原回调 settlement observer，实际原任务成功或失败后才清零；原循环取消可以返回，owner/lease 保留到 observer 结束。当前循环也先等待清零再接下一段结果，避免覆盖仍待清理的租约。迟到失败只保存有限异常类型名并被观察；活跃调用仍收到原异常。
3. 最终落地在 `TryApplyLocal` 内核对 ready/epoch，并与 Cancel/共享 Off 写入使用同一锁，再核原 App owner、token、独立同意、模式及暂停。锁内只做有界本地解码与显示，不等待或调用外部 COM。已接受的本地动作先完成；Off 生效后旧回调不能接受。没有 Provider 接线或网络发送。

这也符合 WPF 的限制：已经开始执行的操作不能靠 Abort 强制结束，因此等待取消不能替代真实任务终结。[Microsoft DispatcherOperation.Abort](https://learn.microsoft.com/en-us/dotnet/api/system.windows.threading.dispatcheroperation.abort?view=windowsdesktop-10.0)

## 归属与清理核对

- App Dispatcher 仅持有并呈现真实 HWND、鼠标输入和本地结果；来源 Query/GetData、标准目标对象和来源清理在 receiver OLE/协商 worker STA；导入、注册、撤销和代理 Release 在独立 registrar STA。此为源码责任核对，不证明 Windows 实际回调线程或注册组合成立。
- registrar 在启动线程前发布原 done task，先 RequestStop，再等待原工作完成；原撤销/Release/uninitialize 未确认则保留 owner。receiver origin export 在原 STA 清理，先 CoReleaseMarshalData，再 stream Release、target Release；native 引用仍存在时保留 callback GC root 到单次 receiver 退出。协商异步提取原引用和 EndOperation 的严格 LIFO 清理及失败不交付保留。
- 父端 Cancel 先原子写共享 Off/终端 epoch，再 best-effort 独立控制管道和 owned child 终止；enable publication 与 Off 串行。只能终止自己 Start 返回的 child。原启动、原注册、原 Run、原 delivery、remote revoke 和实际退出均须终结；未知退出/撤销继续禁止重启。实际 pipe/kill 失效下的 Windows 共享映射行为未运行。
- [RegisterDragDrop](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-registerdragdrop) 要求注册线程持续泵消息；本实现用 OleInitialize 与独立完整 Dispatcher。官方要求及源码不能替代同进程不同 HWND-owner STA、远端标准目标的真实注册/回调验证。
- 自身按下仍来自真实鼠标事件坐标，Raw Input 只移动/释放；来源捕获期间是否能移动、取消及保持焦点，以及低层钩子时限和静默移除，都仍是 Windows gate。没有把纯 pointer 或 synthetic registrar 当成实机证据。

## 执行证据

固定旧生产代码上的新增两用例 **0/2，通过数为零、失败 2**，证明原注册 admission 和原 delivery 尚未结束时过早终结。修复初版相关 44/44 通过；加重复 Run、错误 epoch、Cancel 后本地提交拒绝和迟到失败覆盖后，专项 45/45 通过。新增不同执行用例共 3 项，均实际启动独立 fixture child、走生产 Client/Server 的继承 stdio；registrar、activation 和回调延迟/异常是明确测试替代，没有解封送合成 packet 或运行 Windows COM。

首轮全回归 Core491/492，旧 queued-delivery 用例在回调已结束但 observer 清零续体尚未结束时读取了数组。原失败 TRX/log 保留。改为等待真实清零 settlement，增加未终结时 lease 不释放断言；原零写入、逐字节零值、禁止发送与清理全部断言保留。最终完整回归 **Core492 + Provider77 =569/569，Node63/63，无跳过**；24 项托管 locked Release 构建零警告/错误，身份源静态检查 6/6。Provider 仅访问本机测试 HTTP 服务，没有 API Key 或真实模型调用。

原始日志、TRX、Node XML、基线源码快照、各文件及源码摘要均见 [本轮 summary](test-evidence/2026-10-11-ole-lifetime-audit-summary.json)。复核全部摘要可在仓库根执行 `python3 docs/test-evidence/2026-10-11-ole-lifetime-audit-verify-evidence.py`；ZIP 未在 Git 中时该脚本明确列为本地可选核对，不伪报下载或验证通过。

独立新包 `artifacts/KuaiYi-P0-ole-lifetime-audit-managed-win-x64.zip`：717653 字节，SHA256 `6e0526471435b5a99311406ca8ea7c672cddaef89807a60524f08d469de588bf`。42 文件，含 39 运行/资产、2 原生桥源码和 manifest；五个 exe 的实际 PE32+/x64 头及全部文件摘要验证通过。旧 E06、CaptureHost、截图、浏览器确认等待四个审阅包摘要不变；暂存脚本禁止用其名字覆盖。

复跑 `build.ps1 -Scope Managed -Configuration Release`、`test.ps1 -Suite Offline -Configuration Release`、`test-identity-source.ps1`；新包使用 `stage-ole-presentation.ps1`，默认新文件名。测试输入仅为公开 fixture。未记录真实用户文本、截图、密钥或凭据。

## 未完成的实机门槛

此 Linux 云无 MSVC/Windows SDK/交互桌面。原生桥构建、真实 COM/Release、HWND 跨 STA 注册、共享门控、来源捕获期间自身输入/取消、WPF 关闭/重启及多屏 DPI/焦点/帧时延均 **NotRun/Blocked**。原生 DLL 不在包中，包为框架依赖审阅件，**不能安装或宣称拖放可用**。继续使用 [E06 最小实机 gate](ole-presentation-review.md#最小-windows-gate-与运行方式)，增加原回调执行中 Off、晚失败及关闭/重启验证；未执行前不得关闭 E06/P0 或进入下一产品阶段。
