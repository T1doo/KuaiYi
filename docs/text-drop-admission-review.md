# 原生来源检查后的截止补审

基线8023c71c03899ada517d162de032e1426fdad8f7。父审指出原ReceiverHost.CanCommit先检查Stopwatch<1500ms，再调用SourceCurrent，随后不复核；源码与“来源检查后再查”的声明不一致，问题属实。

原CanCommit的检查顺序提取为生产OleReceiverCommitAdmission，ReceiverHost真实路径调用它；先按原顺序运行17项可执行反例，7失败/10通过，before Host/helper字节及原TRX/log保留。并未在Linux执行Windows ReceiverHost/COM，红测证明被提取的同序生产资格策略缺口。失败覆盖1499→1500/1501和0→1500、SourceCurrent期间gate关闭、epoch变化、共享激活撤销与token取消。修正后17通过，原断言保持，不删测试。正常1499允许、1500/1501拒绝，已无资格不做来源检查，来源无效不交付。

实际ReceiverHost在持有原group的锁内调用该策略：检查原group epoch/开始时间，来源验证后再次检查gate/共享activation/原Stopwatch elapsed；Result编码和严格UTF8校验完成后，再调用同一最终复核才enqueue BinaryChunk。拒绝则不发明文，DropEnd effect/length为0；原清理与EndOperation、所有权/退出和父600ms/1200ms逻辑保持。纯策略使用测试可控elapsed和activation端，不冒充Windows共享内存或焦点实测。

时间来源明确分开：

| 层 | 起点与钟源 | 已有保证 |
|---|---|---|
| 原生ReceiverHost | 自身Emit QueryBegin/DropBegin建立group时Stopwatch.GetTimestamp | 来源检查后、编码后最终资格检查使用同group本地1500ms |
| 父OleReceiverClient | 父收到并验证DropBegin时自身TimeProvider.GetTimestamp | 父本地解码/交付队列截止，收到反馈不能刷新 |
| Core DropCapture | Core接受Begin时自身TimeProvider.GetTimestamp | Core本地目的队列截止及来源/版本/epoch门控 |

协议没有传递原生起点；本轮没有假定跨进程或不同TimeProvider绝对值可比。父receiverCurrent只是父接收客户端的资格，不是远端native Stopwatch证明，已重命名参数避免误读。三层是独立本地预算，没有证明共享原生DropBegin→App最终交付总计1500ms，IPC排队时间仍是端到端预算缺口。原text-drop-review/ADR0013现已收窄；历史8023版本按精确commit保留供审阅，先前的宽声明不能签收。

最终专项161/161（原144+新17），完整Core1049+Provider77=1126/1126、Node63/63，26托管工程locked Release零警告/错误。原1032Core/77Provider名称多重集保留，26锁/规格字节不变。独立审查ZIP、源/证据/历史摘要由[本轮summary](test-evidence/2026-10-11-text-drop-admission-summary.json)记录；Windows清单继续全部NotRun。NativeBridge未编译，实际ReceiverHost/COM/共享映射、E06响应性仍Blocked/NotRun，不将可执行纯策略红绿测视为平台通过。不推进P4，不安装依赖，不接受新法律协议，不真实调用模型。
