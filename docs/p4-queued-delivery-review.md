# P4-04 实际消费者目标队列资格父审补正

基线 `893ebc73717ae8e60fdc604806c55705db1d2a22` 尚未签收。父审发现生成器边界检查不能覆盖真正的 UI 应用：TextTranslationCoordinator/ApiConnectionTestWorkflow 的 Receive 使用 lifetime.Token，闭包未检查原 total，envelope 未包含阶段 origin。截止前入队、截止后执行的内容/usage/成功会越过期限；总预算未到但 first/idle 已过同样成立。原 893 正式源码、测试结果/摘要/失败证据不改，原 ZIP 继续保持精确原字节；旧暂时覆盖后恢复事故不抹去。

新增测试完全经过真实回环 Kestrel HTTP、公开无密钥 profile、生产 ProviderClient、两个实际消费者、ProviderEventReceiver/IdentityEventReceiver 以及真实 Results/固定测试 apply。测试用 SynchronizationContext.Post 保存实际生产回调，在受控截止/取消/新身份之后执行；不合成 ModelEvent、不另造状态机、不使用 MockHandler。时钟是测试控制端口，采集窗口和 sourceCurrent 是固定公开端口，明确不代表实际 Windows UIA/WPF。

原 893 路径红例：104 例 68 失败/36 通过；补真实新请求两种 drain 顺序后 112 例 68 失败/44 通过；再补 timer 已触发、Current 自身跨期、重复 usage 不能以更晚 enqueue 起时后 124 例 80 失败/44 通过。最后一轮临时恢复六份原正式源码的精确字节（核验记录保存），新资格类型当时未被这些原路径引用，运行后恢复修复；不是类名 replay 或测试模型代替。原失败是实际晚到应用/错误原因断言，未降低期限/原因或删除原用例；最终 124 测试体与保存原断言逐字一致。

内部不可变 ProviderEventAdmission 携带原 budget、请求 token 和该事件已存在的 phase clock/origin/limit/reason，不含原文/输出/密钥、不持久化。正文快照从 activity 的已有 last/first 捕获；headers 错误使用本 attempt 的原期限，retry 不沿用上一响应的 body activity，total 绝不重启。资格随 ModelEvent/Envelope 到接收器，在 canonical gate 身份/取消检查之后、真实 apply 前复核；到期事件转为同 identity 的明确 Failed，移除该事件未应用的正文/usage，已应用正文及既有回退规则保留。Off/Cancel/替换旧身份仍直接丢弃，不错误结束新 identity。

Coordinator 的正常/异常 Receive 都传原 budget，转出的 Failed 经原 Results.Fail/Publish/MarkFinished，实际终止后立刻结束循环。Workflow 在 Current(intent) 外部设置检查返回后，再 gate.TryApply 验身份/取消、用同一 Admit 检查 budget/冻结阶段，随后才调用实际 apply。Current 自身跨期的生产用例明确覆盖这个顺序。终止后不继续消费；旧 finally 的条件身份检查继续保留。

新 124 通过，加上上一轮/原阶段/总预算 94 回归，专项 **218/218**；第一轮修复 112/112 也保留 TRX。完整本地离线 CI：Core1315 + Provider521 = **1836/1836**、Node **63/63**；27 工程 locked Release 零警告/错误。原 1712 按名称逐项保留，27 锁、产品书及六份内置提示词不变。完整回归后运行源码/测试未再修改；文档、独立 staging 和证据冻结随后完成。

新打包脚本在运行前核对目的名，并在开始/结束核对全部 28 历史 ZIP，当前输出使用独立路径；原恢复的 ccf ZIP SHA256 231ed070c8baeb4bf5017be2083b014b092c48b80f627cabad3a4bfc6c442b96、893 ZIP SHA256 d148a96bb8f29d919d74db2f124e05df608132c7f2155e9481f995d82016a8f1 继续准确。当前包与 28 旧包/24 历史精确 commit 摘要核验，不把历史字节审查当作父签收。

无真实 key/模型/付费、依赖安装、扩权、代理/证书修改、main 修改、部署或发布。托管 x64 framework-dependent 审查包无安装器/原生 DLL；P0/E06 Blocked，P1—P4 InProgress，176 追踪/106 整体验收 NotRun。Windows真实 Dispatcher/UIA/跨应用/多 DPI/DPAPI/安装/真实代理 DNS/模型语义/远端 CI 仍未验。[ADR0021](adr/0021-frozen-eligibility-at-destination.md) 给出依据与边界，[Windows清单](p4-queued-delivery-windows-checklist.md) 列补测；本补正等待父级再次独审。
