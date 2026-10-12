# P4-04 截止、取消与实际事件交付父审补正

基线 `ccf9981091a808306be1b5d614dbe06c83bc17c9` 暂未签收。父审发现成功响应头转换未查 headers/总期限，read phase.Check 未查父取消，JSON 已解析事件逐次交付只查身份，消费者暂停到期后仍可能接 Usage/Completed。承认缺口，原阶段实现/失败/成功证据未改，原 ZIP 在本轮打包事故后已恢复原字节；历史字节核验不代表父签收。

新生产路径测试先运行原代码：29 例 24 失败/5 通过；补终止事件竞争后 41 例 36 失败/5 通过。错误明确为截止后 Started、TextDelta、Usage 或 Completed，而不是测试夹具等待。真实 Kestrel/Socket 返回字节后，test-only Stream 屏障推进单调时钟或取消，再返回同一实际读取结果；可以延后 timer callback，证明绝对截止不能依赖取消通知的调度。消费者真实 async iterator 在 TextDelta/Usage/Started 交付后暂停再继续；没有 MockHandler、合成 ModelEvent 或真实模型。原测试体、3 份正式源码、2 份红 TRX 与日志保存，原断言/时间值/原因不弱化。随后加入 3 项连接完成边界回归，新 44 项；修改夹具连接配置为 2 秒用于这 3 项，原 41 项断言和 headers/total/first/idle 参数未改。

ProviderRequestBudget 的起点仍是原凭据访问前的单调时间；Deadline.Check 查父取消及同一 budget.Remaining，IsCancellationRequested 也识别 timer 回调未执行的绝对到期。安装 timer 后立即复核原预算。ProviderPhaseDeadline 保存构造起点/原活动起点，Check 查父取消及自身绝对期限；activity.Effective 先检查旧阶段，过期响应不能刷新期限。

正式连接回调在开始/成功返回处复核同一 total 与 connect，迟到成功 stream 先释放，不准写 HTTP；最终发送资格回调后复核 total/headers；await SendAsync 后复核 total/headers。首响应 origin 在接受响应头时创建，早于 Started，消费者暂停不会另起正文预算。打开 stream、每次实际 read 前/完成后、解析后/有效活动刷新前，以及 JSON/SSE 每个事件交付前，都检查 total、父取消及活动期限；读取依旧传真实 linked token。GenerateEnvelopesAsync 携带同一预算并在外层交付前再查 total/请求取消；不重置、不加长总限，不以“HTTP 应自行取消”证明安全。

取消优先 CANCELLED；没有用户取消则 total 优先 REQUEST_TIMEOUT，再保留阶段独立原因。到期/取消只交付相应 Failed，绝不再发成功；失效 gate 仍丢弃全部迟到事件，保留已交付正文。HTTP 错误/输出限制/部分正文同样在实际错误交付处复核取消及总期限。默认 Off、不可变开发回环授权、固定连接测试许可、身份/来源/旧快照规则、TLS/代理策略、有限重试不变。

新 44 全过；包含新竞争测试及 50 个既有预算/阶段/消费者回归的专项 94/94。第一次修复 41/41、扩展旧阶段检查 91/91，原 TRX 保存。完整本地 offline CI：Core1315 + Provider397 = **1712/1712**、Node **63/63**，27 工程 locked Release 零警告/错误。原 1668 测试按名称逐项保留；27 锁文件、规格及内置 6 份提示词资源不变。完整回归后不再改运行源码/测试，仅补文档、独立 staging 与证据冻结。

新审查 ZIP 使用独立路径，27 份历史 ZIP 最终摘要逐份核对，23 份精确历史 commit 摘要核验；当前发布 App/Provider DLL 必须匹配本次实际构建。旧误覆盖恢复事故继续保留记录，不声称历史从未被修改。全部原 ccf998 证据文件在磁盘保持与 commit 一致。

本轮新 staging 脚本复制时大小写匹配遗漏 ZIP 目的名，首次把新包写到 ccf998 原 ZIP 路径。冻结核验因新路径缺失立即失败，未改变原摘要或接受新 hash；把该新包移至独立路径，再由原未动 staging 目录重打原包，恢复精确 2165159 bytes / SHA256 231ed070c8baeb4bf5017be2083b014b092c48b80f627cabad3a4bfc6c442b96。本轮事故和恢复证据保留；修正路径，并在 staging 开始、结束核对全部27历史ZIP，拒绝当前输出选中任何历史路径。修正脚本随后再次实际执行，不声称从未覆盖。

没有真实 key/模型/付费、依赖安装、扩权、证书/代理变更、main 修改或发布。托管 Windows x64 framework-dependent 包无安装器/原生 DLL；P0/E06 Blocked、P1—P4 InProgress、176 追踪/106 整体验收 NotRun。Windows WPF/跨应用/多屏 DPI/DPAPI/安装卸载/真实代理 DNS/真实模型语义/远端 CI 仍 NotRun，见 [补验清单](p4-completion-races-windows-checklist.md)。[ADR0020](adr/0020-absolute-deadlines-at-delivery.md) 附官方依据；本补正等待父级独审，未冒称整体通过。
