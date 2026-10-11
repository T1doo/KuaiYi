# 热键前级生命周期与提交通知补审

父审基线 `93b538a301529247cac82b1c79eec75888cbc354` 尚未签收。发现两个真实缺口：HotkeyFlow 在 Pressed 时读取当前 epoch，注册映射的 uptime fence 不覆盖普通 Off→On、模式往返和暂停恢复，已有 HWND 队列旧消息可能绑定新时代；配置发布后 await 热键 Commit 先于 PublishCommitted，提交异常会跳过 profile 失效通知。上一轮 99 项测试没有证明这两个边界，本轮不沿用其安全闭环声明。

保留 93b 原 Host/Flow/RuntimeGate/SettingsCoordinator 精确源码及重构前序策略。Native Host 接入共用生产 HotkeyInputAdmission 后，先保持旧“仅初始/映射围栏 + 消费时当前身份”行为：生命周期 33 项 **32失败/1通过**，选区与截图覆盖 15 种变更，包含不改变旧 epoch 的启用和两级队列；真实配置事务/生产 Registry 注入 FenceInput 失败的六项撤销通知测试 **6/6失败**。随后还发现等候未结束的原 Commit 会延迟通知，两个真实配置与原任务屏障测试 **2/2失败**。这些红测是可移植的生产策略和真实文件验证，未执行 Windows HWND/消息泵，未假称 native 队列实测。

RuntimeGate 现在在自己的锁内记录生命周期输入 revision 与 uptime 界线。所有既有 epoch 写入统一经属性 setter 推进围栏，覆盖 partial class 的请求、设置元数据、复制授权、模式、暂停、隐藏和显式截图；不改变旧 epoch 的普通启用、本地启用、正式 ConfirmEnable 与锁屏/解锁也显式推进输入 revision。没有给 native Dispatcher 排一个可能迟到的“更新 gate”任务。Windows Environment.TickCount64 调用 GetTickCount64，与 GetMessageTime 的开机毫秒同源；只比较同进程的低32位回绕投影，不比较 Stopwatch 或进程外时钟。[.NET 10 Windows 实现](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Environment.Windows.cs)、[GetMessageTime](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessagetime)。

真正的 HotkeyHost 持有同一 RuntimeGate。WM_HOTKEY 的原始 GetMessageTime 必须晚于已记录生命周期界线，再检查自有注册映射，且交付前复核 GateId/revision/epoch；出生身份装入 HotkeyInputDispatch，由原 native 队列带入 UI 队列。UI 不再从 Pressed 回调重取 epoch。时间同 tick 保守拒绝，映射围栏、待提交及故障门控继续保留。47 项新生命周期测试还覆盖暂停期间产生的消息、启用 epoch 不变、低32位回绕、跨 gate 拒绝、失败启用不误失效，以及最终 UI 检查之后取词/截图原子入场不得刷新过期身份。

实际 ReadCurrentSelection→SelectionCaptureCoordinator 的显式入口携带这份 HotkeyInputIdentity，冻结对应 CaptureTicket；Start、读取后及最终提交复核原身份。ScreenshotIntent 原子入场也接收该身份。隐藏浮窗时，当前本地截图入口首按只显示红色浮钮，提示再次按键框选，避免把显示自身推进的 epoch 当作原按键的新授权；这是尚未完成 P6 的保守行为，未把完整截图体验标为通过。实际 Windows 抓屏、完整确认/模型翻译与 P4 重新生成继续未交付。

已发布配置的安全通知不等待原热键提交结束。SaveCore 启动并保留原 Commit Task，调用 PublishCommitted（仍遍历全部订阅者），然后等待原 Task 的真实结果。提交和通知异常均收集并抛出，单错误保留原堆栈，多错误明确聚合，保留恢复 journal；已提交磁盘/effective 不回滚。挂起的原操作仍阻止草稿 Dispose，发布后的取消不会捏造失败回滚。六项实际文件/注册器故障测试证明 clear/delete/replace A 都及时取消旧 A、禁止迟到 Apply/Send、失效旧 capture 并通知模式变化；共享引用和 B 的请求/发送资格保留；独立观察者抛错也不跳过后续通知。两项 pending-original 屏障证明通知先于原提交结束，成功及延迟失败均保留真实结果与所有权。

原“提交未完成必须等待日志重放”测试更新为真实生产 Registry FenceInput 持续失败的故障点，保留原“暂停、日志、恢复后可执行”全部断言；不能再用非 native 的一次 FailCommit 布尔值声称后续通知不能完成恢复。新测试另验证一次提交失败即使后续观察者完成映射恢复，原异常仍可见、日志仍保留。此复核还实际暴露 Commit 的恢复成功后 faulted 没清除（初次扩展 72项71通过/1失败）；只有准备绑定全部仍属本实例且最终围栏成功时才清除此标记，其他回滚/所有权故障保持阻塞。未删除测试或降低断言。

最终专项 **155/155**（上一轮99 + 新56），完整 **Core1204 + Provider77 = 1281/1281**、Node **63/63**；27 托管工程 locked Release 零警告/错误。原1148 Core/77 Provider 名称多重集、27锁文件与两份 SPEC 原字节保留。初次编译失败、全部红测及修复后真实 TRX/log 见 [冻结摘要](test-evidence/2026-10-11-hotkey-admission-summary.json)。源码与测试最终 CI 后未修改。初次 staging 在清单文件创建前运行而失败，其原日志保留；清单生成后以相同独立路径重跑成功，未覆盖旧包。

独立 `stage-p3-hotkey-admission.ps1` 生成新的框架依赖 x64 审阅包，不覆盖上一轮热键包或其他历史包。18 旧包最终摘要核验；14 精确 commit 的历史摘要包含待签收 93b，字节审核不代表产品签收。前轮绝对截止包误覆盖后恢复原字节的事实与证据继续保留。P0/E06 Blocked，P1/P2/P3 InProgress；176 追踪/106 整体验收 NotRun；实际 Native HWND/消息队列/占用、跨应用、AltGr/锁屏/Explorer/DPI、截图、安装与远端 CI 仍 NotRun。本轮无安装、扩权、真实凭据/模型调用、新协议或 P4 推进。
