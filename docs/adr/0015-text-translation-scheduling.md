# ADR0015：文字快照、生产调度与显式测试入口

选择复用现有双协议 Provider、统一 RuntimeGate 与目的队列，增加目的线程的 Prepare/Submit/Confirm/Regenerate 和异步传输。捕获提交锁中只准备快照，实际提交离开捕获栈；网络所有尝试在最终 Gate 锁中检查资格、来源/配置与检查后的身份，UI 所有事件携带原四项身份。最新来源替换旧请求；失败恢复缓存，不重新采集。

软限额确认先使旧请求失效，但没有 HTTP 和密钥读取；确认沿用当时快照，Off/来源/profile/设置/权限变化使之失效。初次自动发送复核原前台窗口，主动确认和重新生成只验证保留来源窗口的 HWND/PID/进程时间、会话和完整性权限，不强制切换用户焦点，也不把新选区冒充旧版本原文。窗口已销毁或不可验证时保守拒绝；此行为需要真实桌面复核。

UI 可显示至多三版本；失败回滚保护只在本次会话内存保留。在尚未完成的来源连续替换时，不把最后可读缓存替换成空生成槽。来源元数据按当前与回滚版本引用裁剪（生成期间最多四个，完成后最多三个），防止取消后持续积累。重新生成使用所示版本的配置；当前设置重翻译独立命令，记录实际模板版本。

.NET `SendAsync` 是异步 HTTP；`ResponseHeadersRead` 使首阶段在响应头可用时完成，后续流读取仍需自己传递取消并关闭阻塞读取。WPF UI 工作通过 Dispatcher 目的线程，不能在 Gate 的同步回调中等待异步操作。本轮使用这些官方约束，实际 Windows 调度/焦点仍 NotRun。[Microsoft SendAsync](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.httpclient.sendasync?view=net-10.0)、[WPF threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model)。

测试服务继续留在 tools；正式程序没有 Mock 引用。显式测试命令只接收回环 fixture URL，用独立随机临时配置、无密钥 profile 和可见“本地测试输出”标识，不默默替代真实 Provider。没有创建真实模型成功证据，Native/Windows/P0/E06 阻塞保持。
