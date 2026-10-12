# 实际目标队列截止 Windows 补验（全部 NotRun）

使用独立临时配置和公开本地 HTTP fixture；不读真实 key、不调用真实模型。原阶段和截止清单仍适用。

1. 实际 WPF Dispatcher 排队 Started/TextDelta/Usage/Completed/错误时暂停目标线程，分别过 total、first、idle 后恢复；只能报告对应失败，晚到正文/usage/成功不应用，已到达正文和原回退版本保留。
2. 对普通翻译及 Off 状态的固定公开连接测试分别执行 JSON/SSE；冻结活动 origin 跟随事件，重复 usage 不延长 idle，总预算未到但 phase 已过也必须拒绝。
3. 设置读取/Current(intent) 自身延迟至截止，实际 Apply 处仍拒绝；队列阻塞与设置/草稿变更、Off、锁屏、Cancel、身份替换竞争时旧事件丢弃。
4. 将旧完成回调置于新真实请求前/后执行，旧回调和旧 finally 不终止新请求；旧缓存正文、当前请求身份及失败原因不串版。
5. 验证实际线程池计时器通知延后及已发取消通知两种情形；错误处理和关闭操作不导致额外请求/重新启用或秘密读取。

Windows UIA/跨应用、多屏 DPI、焦点/动画、DPAPI、安装卸载、原生组件、真实代理/DNS/模型及远端 CI 继续各自补验。本地测试的调度端口不是 WPF 实机证据；托管审查 ZIP 无安装器/原生 DLL。
