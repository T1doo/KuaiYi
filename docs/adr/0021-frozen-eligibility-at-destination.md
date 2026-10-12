# ADR0021：冻结事件资格，在实际目标应用点复核

状态：Implemented locally / Windows NotRun；等待父级独审。

父审否决 893ebc 的队列覆盖声明：GenerateAsync/GenerateEnvelopesAsync 交付处有检查，两个消费者却只传 lifetime.Token 到 UI 接收队列。成功事件可在截止前入队、截止后应用；当前 envelope 没有相应 first/idle 原始期限，阶段截止也会被绕过。

ModelEvent 携带内部不可变 ProviderEventAdmission，ModelEventEnvelope 保留它。资格记录只引用原 ProviderRequestBudget、请求取消 token，以及本事件原阶段 clock/origin/limit/reason；不含原文、译文或密钥，不写配置/缓存/日志。正文资格来自 ProviderReadActivity 的已有 last/first 值；不是 enqueue 或 apply 时间的新预算。响应头阶段错误携带该 attempt 的原 headers 期限；重试清除上一次正文活动，只创建当前 attempt 的阶段资格，原总预算始终不动。

ProviderEventReceiver 在 IdentityEventReceiver 的目标回调中、canonical gate 身份/取消检查之后，复核共享 total 和 envelope 的冻结资格。过期的 Started/TextDelta/Usage/Completed/Failed 转为同 identity 的明确 Failed，丢弃该事件的正文和 usage。总期限优先于阶段期限；失效身份及 caller cancellation 由原接收器直接丢弃。使用 lifetime.Token 保持到期失败能在仍有效的目标 UI 应用；total/token/phase 资格不会因此被忽略。

TextTranslationCoordinator 将原 budget 传到正常和错误 Receive，截止转出的 Failed 经原 Results.Fail/Publish/MarkFinished，保留已有正文及既有回退规则。当前终止事件应用后立即结束该生成循环，不重复提交晚到事件。ApiConnectionTestWorkflow 同样传原 budget；Current(intent) 可能调用外部设置读取代码，所以它返回后再次用 gate.TryApply 验身份/取消，并在真实 apply 前用同一 Admit 复核 total/冻结阶段。只结束该 identity；其 finally 不能停止新 identity。

测试直接运行真实 HTTP→ProviderClient→实际 Coordinator/Workflow→实际 ProviderEventReceiver/IdentityEventReceiver→受控 SynchronizationContext.Post→真实 Results/测试 UI 回调。只有目标调度和时钟是测试控制端口；不合成 ModelEvent、不代造结果状态机。延迟 timer callback、正常 timer 已取消、queue 顺序、设置检查自身跨过期限、重复 usage 在更晚 enqueue 不能重置 idle 等分别验证。采集来源窗口/当前性是公开固定测试端口，不冒称真实 UIA 或 Windows WPF。

依据：[Microsoft SynchronizationContext.Post](https://learn.microsoft.com/en-us/dotnet/api/system.threading.synchronizationcontext.post?view=net-10.0) 定义异步投递；[Microsoft cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) 说明监听者负责响应取消。资格冻结与目标应用复核是工程决策，官方说明不代替执行证据，也不声称任意线程抢占下的硬实时原子截止。
