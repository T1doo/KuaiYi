# ADR 0014：热键注册与配置提交使用可恢复事务

基线 5b9b23e85102787a24833dd7fa6111d92887137c。范围为 SPEC §2.3、§8.4、P3-05；不推进 P4，不将 Windows 实测替换为可移植策略测试。

`HotkeyPolicy` 是读取配置、录制草稿和生产注册的同一语义校验器。固定九动作；允许 Ctrl/Alt 至少一种，可加 Shift，支持字母、数字、F1—F24 和常用导航键。拒绝纯修饰键、Windows 键、F12、已知系统保留组合及规范化后内部重复。JSON Schema 继续由 AppSettings 导出结构/范围/默认值；跨动作重复、保留组合与 OS 占用属于同一运行时语义策略，不能靠 Schema 证明可注册。录制仅由聚焦 WPF 控件提供 PreviewKeyDown，无全局键盘录制 hook。Ctrl+Alt 提示 AltGr 风险，移焦、Tab、Escape 结束录制。

生产 `HotkeyHost` 的专用 STA 创建 message-only HWND，所有 Register/Unregister 在所属 Dispatcher 上运行；设置工作线程异步提交操作，不等待 WPF UI 回调。使用 MOD_NOREPEAT，失败保存真实 GetLastError，不猜进程。Windows 明确要求注册窗口属于调用线程、F12 保留，且同 HWND/id 重复注册会保留多项，因此使用独立自有 ID，按实际成功记录撤销。[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)、[UnregisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-unregisterhotkey)。

Prepare 校验整组组合、先取得所有新组合，再撤销未保留的旧组合。两个动作交换时复用现有物理绑定，只在提交时交换映射。Prepare 期间不派发动作；磁盘写入失败回滚实际持有的 ID，原组合复原失败明确标为未确认并禁止派发，保留托盘与实际所有权，不声称成功。Cancel/录制不调用注册器。常规 SaveAsync 仍拒绝热键变化；设置窗口经 SaveWithHotkeysAsync 明确加入真实注册事务，不能顺带授予复制或 endpoint 同意。

保存沿用真实临时文件 flush、原子替换、WriteId 与持久化 journal。写入前异常或取消恢复旧映射；原子发布后异常先读取磁盘，WriteId 确认已提交则提交新映射。提交后不把取消说成回滚。结果无法证明则保留 journal 与 pending 绑定，暂停动作；重新打开设置时，已提交日志通过 Committed 回调 Reconcile 完成映射，之后才删除日志。不同进程重启从实际已保存配置调用 RegisterHotKey，失败保留未注册与实际错误，绝不自动换建议值。未知配置版本仍只读。

WM_HOTKEY 的 id、组合、消息 uptime、动作映射 revision、settings version 与主线程执行时 gate epoch 共同校验。注册 ID 退役后不再分配；交换保留 ID 前清理本 HWND 的旧队列并建立时间围栏。GetMessageTime 和 GetTickCount64 低 32 位比较用回绕减法；同 tick 消息保守拒绝，可能丢弃紧邻提交的首次按键。PeekMessage 会派发 sent 消息且即使过滤也取出 WM_QUIT：围栏期间禁止重入派发；WM_QUIT 重新投递并报告关闭，不能吃掉退出。[WM_HOTKEY](https://learn.microsoft.com/en-us/windows/win32/inputdev/wm-hotkey)、[GetMessageTime](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getmessagetime)、[GetTickCount64](https://learn.microsoft.com/en-us/windows/win32/api/sysinfoapi/nf-sysinfoapi-gettickcount64)、[PeekMessageW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-peekmessagew)。计时与消息泵实际行为仍需要 Windows 实证。

锁屏先同步撤销 gate epoch，再暂停 native 派发；解锁与 Explorer 重启只重试失败项，保留仍由本 HWND 持有的工作组合。这个策略是代码恢复路径，不是系统已成功恢复的验收证据。unexpected 消息线程退出使 IsCurrent 为 false；没有承诺自动重建意外死亡的 HWND。退出等待原操作和线程实际结束；失败保留可重试所有权，清理不会触碰其他 HWND/进程的组合。

Off 下翻译选区、重新生成只提示启用；缓存、显示/隐藏、设置、聚焦和停止遵循现有 gate。截图热键只提供实际本地准备与预览，完整确认及模型链路留待 P6，重新生成正式调度留待 P4。完整动作体验、H01—H06、跨应用、DPI、安装与 E06 均未因这一事务核心交付而通过。

父审补正（93b未签收）：仅注册映射围栏与Pressed当前epoch不足以隔离HWND前级旧消息。RuntimeGate在状态变更锁内记录同源uptime/revision；Host按原MSG time资格分配不可变GateId/revision/epoch，UI及取词/截图入场复核。Epoch统一setter使partial-class所有写入同样推进输入围栏；On/正式确认不变旧epoch的路径显式更新输入revision。已发布设置安全通知不等待原native提交结束，先保留原Commit Task、遍历通知、再观察提交结果；所有异常与journal保留，已发布事实不回滚。详见[p3-hotkey-admission-review.md](../p3-hotkey-admission-review.md)，真实Windows队列与事务仍NotRun。
