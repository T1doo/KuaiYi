# idle 测试以实际 PUBLIC 正文入队为时钟锚点

从已准入 e14789ff897e7a8aa13ee0fcef042ca0f427fff5 正常建立 dev/idle-public-anchor。仅改 PhaseDeadlineTests：Rig 在精确 TextDelta/PUBLIC 已入 Events 后完成 RunContinuationsAsynchronously 信号；原六个 idle 用例先等该信号，再原 WaitDeadline(2)/首次推进。原2秒 idle、真实500ms完成门、所有旧断言和清理逻辑保留。生产代码及 PhaseClock 原字节不改。

新增单个实际 Anthropic 回环受控例：只发 message_start/content_block_start 后已有活跃idle2，而 PUBLIC 信号未完成；fake1才发 PUBLIC 并等实际入队，截止成为fake3；空delta后fake2等真实500ms仍pending，fake3经原 AssertTimedOut(1)得到 READ_IDLE_TIMEOUT，PUBLIC 与单次HTTP保留。它证明旧同步点不足及生产正文锚点的可达安排，**不能证明历史失败当时采用了该安排**。empty 的 Write 仍只证明服务端flush，本片没有新造客户端empty消费收据。

最终 r01 整个 PhaseDeadlineTests **28P/0F/0skip**，r02 一次无筛选 Provider **2301P/0F/0skip**，全名多重集精确为父2300+新1，无删除。两执行各33份源码绑定，共66份匹配，Provider目标五DLL观察一致；其他预存DLL不是其他目标执行证据。无生产红绿声明。Core1703/AppWorker0W/E沿用未改2219父闭包，本片未重跑或重建。

独立只读源码审查无P1/P2，最终封存另审；命令、TRX、日志、执行时间、源码与DLL/父路径摘要、回执见[原件索引](../test-evidence/2026-10-17-idle-public-anchor.json)。前次[只读调查](idle-timeout-readonly-investigation.md)及其旧失败/同DLL通过原件保持原字节，历史根因仍Unknown。旧图片间歇/Core当次结算/WindowsJob各独立Unknown。生产复用Off，Windows/WPF可见键盘/焦点/真实截图/DPAPIACL/物理250600/模型质量/完整P6未验。未扩功能或抬阶段状态。

仅已有SDK10.0.401 --no-restore，激活前后禁生成证书。无main/强推/部署/费用/真实模型/凭据权限/新包许可Skia变化，不重查或绕过403/Actions先前拒绝；未知锁数据日志进程保全。
