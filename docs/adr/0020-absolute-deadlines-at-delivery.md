# ADR0020：在阶段转换及事件交付处复核绝对期限

状态：Implemented locally / Windows NotRun；父级尚未签收。

父审指出 ccf9981091a808306be1b5d614dbe06c83bc17c9 在成功的 SendAsync 后只查身份，阶段 Check 未检查父取消，JSON 批量事件交付只查 gate。HTTP/读取已完成、计时器回调排队或消费者暂停时，底层取消不能替代交付处检查。

同一 ProviderRequestBudget 保留凭据访问之前的原始单调起点。其 Deadline.Check 同时查父取消和预算的真实 Remaining，IsCancellationRequested 包含已过绝对总期限；Link 安装 timer 后复核原预算，不另起计时。ProviderPhaseDeadline 保留构造时单调起点和 read activity 原始起点，Check 先查父取消再查自身绝对期限，读阶段 Effective 必须先证实旧阶段未过期。

实际连接回调开始/返回时检查共享 total 和 connect（失败释放已返回 stream）；最终发送资格检查之后再查 total/headers；await headers 后复核二者，并立即冻结正文首响应起点，随后才交付 Started。每次 read 前/完成后、解析后、有效活动更新前及每次事件交付前检查共享 total、取消和活动期限。消费者暂停的时间计入原总预算及原内容期限，JSON 一次解析出的 Usage/Completed 与 SSE 缓冲事件均不得绕过检查。GenerateEnvelopesAsync 使用同一 budget，并在外层实际交付再查总预算/请求取消。错误交付允许报告截止原因；gate 已失效则仍丢弃全部迟到事件。

若用户取消与期限同时出现，原因优先 CANCELLED；否则总期限优先 REQUEST_TIMEOUT，再区分 connect/headers/first/idle。明确的失败事件不代表翻译成功，也不刷新期限。原错误处理、受限重试、输出限制、不可变本地授权和默认 Off 合同继续保留。

测试在真实 Kestrel/Socket HTTP 路径给实际读取完成之后加 test-only Stream 屏障，或直接暂停真实 async iterator 消费者；PhaseClock 可推进单调时间而延后 timer callback，模拟“截止已到，取消通知尚在排队”。它不改变正式网络流、TLS 信任或模型行为。新 44 例包括上述边界及连接完成后不得发送；原 41 例先在原正式代码运行，36 失败/5 通过，保留全部原断言及失败证据。没有用 MockHandler 或真实模型。

依据：[Microsoft cooperative cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) 说明监听者需要自行响应取消；[TimeProvider.GetElapsedTime](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider.getelapsedtime?view=net-10.0) 提供 timestamp 间耗时计算。这里的边界检查策略是工程决策，真实生产路径测试是独立执行证据；不声称任意线程抢占下的硬实时原子截止。
