# P3-05 事务热键审阅

基线 `5b9b23e85102787a24833dd7fa6111d92887137c`；保存云环境 `/workspace/KuaiYi`，正常分支 `dev/p0-foundations`。仅本切片，无安装、权限改变、新协议、真实凭据/模型调用或 P4 推进。

已实现同一热键语义策略、聚焦控件录制、清除/建议值、规范化内部重复、保留组合及 AltGr 风险提示。九动作的配置表完整；生产专用 STA/message-only HWND 调用真实 RegisterHotKey/MOD_NOREPEAT、UnregisterHotKey 与实际错误，内部 busy/recovery/ID 耗尽采用独立状态异常，不伪装成 OS 错误。错误不能识别占用程序，注册成功也不能识别应用内部局部冲突。正式 SelectionFlow 原单一选区注册已移除，统一服务按动作分发；公开 GrowthHarness 不创建真实热键宿主。

组事务先注册全部新组合，再撤销不再使用的旧组合；互换复用已持有组合。准备期间暂停动作，失败恢复自有 ID，回滚无法确认时保留实际所有权并禁止动作。所有清理仅本 HWND 自有 ID。设置草稿保存通过实际 SettingsCoordinator/SettingsStore 原子配置与 journal，Cancel/录制不触碰 OS 或持久配置。文件发布前失败/取消回滚；发布后以 WriteId 观察实际结果，已提交不假装取消；提交或结果未知保留日志与 pending。提交日志重放真实调用 Reconcile；重启从保存配置实际注册，首次占用保留真实未注册状态，不悄悄改组合。普通保存仍拒绝热键变化与敏感授权变化。

主线程执行动作前再次核对 settings version、映射 revision、gate epoch 与锁屏/暂停/Off；WM_HOTKEY 排队前验证自有 id、完整组合与 uptime 围栏。计时回绕与同 tick 保守拒绝纳入测试。复核发现 PeekMessage 派发 sent 消息造成重入窗口：三项生产策略反例（Reconcile/解锁/Recover）原断言均失败，保留原源码、测试、TRX 和日志；围栏执行期间显式禁止 Resolve/Current 后全部通过。围栏失败报告未确认，不继续旧消息资格；WM_QUIT 重新投递，不吞关闭。

九动作下游状态也明确区分：Off 选区/重新生成仅提示；缓存展开不请求、隐藏停用、显示红色、聚焦/设置沿用现有真实行为，空任务停止不做操作。区域截图热键接实际 CaptureHost/区域框选/本地预览，保持 Off，不启用、不上传；完整“启用并翻译”与图像模型链路留待 P6。重新生成 On 时明确显示 P4 正式调度尚未接入。本切片不能证明完整 §8.4、全部 H01—H06 或正式模型翻译可用。

锁屏同步失效 gate 后暂停热键；解锁和 Explorer 重启重试未成功组合，保留 HWND 尚持有的工作组合。生产事件恢复代码已接线，但 Windows 真实注册、AltGr、焦点、锁屏、Explorer 及异常重启均 **NotRun**。消息线程异常退出禁止继续分发，没有承诺自动重建该 HWND。退出等候原操作、持有对象清理和线程实际终止；尚未确认时保持退出阻塞并允许重试。

最终专项 **99/99**，包含录制/校验/组事务/故障注册器策略和真实临时配置文件的保存、取消、原子边界、异常、日志重放、恢复选择、重启及隐私授权边界。注册器测试使用测试专用端；配置文件真实 flush/替换/读取，不以此冒充 Native Win32 验收。中间编译失败（MSG 字段、xUnit 分析器与测试嵌套构造参数作用域）及重入红测完整保留，没有删除测试或降低断言。最终完整离线 **Core1148 + Provider77 = 1225/1225**，Node **63/63**；27 托管工程 locked Release 零警告/错误，原 1049 Core/77 Provider 测试名多重集和 26 旧锁文件保留，新增无包引用的占用器锁文件。实际结果与摘要由 [冻结证据](test-evidence/2026-10-11-p3-hotkeys-summary.json) 记录。

新增真实 Windows `KuaiYi.HotkeyOccupier`，明确注册公开命令行组合、message-only HWND/pump、Enter 撤销和非零清理失败。独立 `stage-p3-hotkeys-review.ps1` 输出正式 App、隔离公开浮窗 probe 和独立占用器；ZIP 为框架依赖、多文件、无原生 DLL/安装器/凭据，未在 Windows 执行。全部 17 旧包最终摘要核验，13 份历史摘要按精确签收 commit 审查。前轮误覆盖绝对截止包后恢复原字节的事故与恢复证据继续保留，不能说历史包全过程未被修改。

R02/R14/P3-05 源码 InProgress、Windows 验证 Blocked；H01—H06 的源码有实现但整体仍 NotRun。全部 176 追踪条目及 106 整体验收不变，SPEC 两份原字节保留。P0/E06 Blocked，P1/P2/P3 InProgress，P4 未接；真实跨应用、多屏 DPI、截图、安装卸载及远端 CI 未运行。实机清单见 [Windows 清单](p3-hotkeys-windows-checklist.md)，设计与官方依据见 [ADR0014](adr/0014-transactional-hotkeys.md)。

父审93b未签收：积压native队列和热键Commit跳过安全通知的缺口已另行复现与补正，以上99项范围不能证明这两个边界。最新源码、155专项与1281全量证据以[p3-hotkey-admission-review.md](p3-hotkey-admission-review.md)为准；93b原字节按精确commit保留。
