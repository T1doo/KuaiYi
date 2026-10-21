# A片退出错误保全与诊断截止夹具

基线 `72b61be503317fa0b733da55464aec14ee55044e`，开发分支 `dev/p7-fixed-signoff`，唯一writer。父侧确认退出错误覆盖P2并授权同片修复原crash语义夹具；不接B/Windows/UI。

## 原错保全

原SettleForExitAsync中warm错会被finally的stop/image CancelAsync错覆盖；await Task.WhenAll也可能只暴露一个fault。r10两种warm/cancel屏障先后及stop+image双错 **0P/3F**，原对象断言准确复现丢错。修复后分别join同一原warm、stop、image、loop；读取各Task完整InnerExceptions，递归至原错误对象，按reference去重。独立清理单错EDI、多错Aggregate；warm已实际结算即使fault，也先尝试CLOSING再等取消/loop，避免stdin关闭互等。不得发EXIT_READY或释放warm slot/owner。

兼容原协议合同：单独loop/既有协议write错误仍置FailureCode、保留原loop，允许Shell关闭但绝不EXIT_READY；旧真实child0/controllerUnconfirmed/原owner fence断言不改。与清理错并存时，所有loop/write原错一起聚合。r11初版3反例+原loop-only实际child **4P**；随后独审要求递归leaf及single/both Assert.Same，最终另有loop/read+close双原错与取消错三者聚合反例。

## 统一可控截止，仅诊断故障语义

ReuseDiagnosticHost内部init-only TimeProvider接缝。默认null仍原Stopwatch/CancelAfter600ms；原真实watchdog、lateIO/start/exit测试仍default，idle120ms/清理2s/生产250600均未改。controlled路径同provider覆盖请求origin、绝对Check、模型单调毫秒、timer；持有timer CTS及caller/lifetime/timer的linked CTS，所有退出路径释放，原lateIO/actual child退出清理门照旧。native+fake在任何Start前拒绝。fake synthetic wire仍提供真实Stopwatch origin，不能将假时钟伪作跨进程时间。

只有原7个fault semantic theory改用冻结父时钟，精确reason及清零/退出等原200字节断言全部保留。3秒只是测试watcher，失败finally撤销并join原Run，不放大生产预算。新增同真实child原inner EOF返回屏障：599ms不投timer为Crash；恰600ms不投timer由绝对检查拒绝；恰600ms投timer时原read未返不得summary/replace，释放后才结算。child真实23退出单独观察，原marker全清；另核native拒绝不调用starter。

冻结父时钟不控制child帧内声明的真实ElapsedMs；模型仍验证child帧600ms/Success250ms。crash EOF无帧，这个夹具不据此宣称性能达标或控制所有子进程时序。旧r09的624.2773ms失败及原TRX保持Fail；机制可发生不等于定位其历史阶段/唯一根因。

## 实际验证与证据

r12定向 **15P/0F/0skip**；r13完整相关 Installation+SelectionReuseHost **255P/0F/0skip**、exit0、46.582s。旧245案例多重集均保留，新增10个有区分力案例，不累计轮数。823输入中818旧输入不变，仅原Core退出结算、诊断Host与旧语义测试夹具3文件变化，新增两个测试文件。App/Windows/Contracts/Worker/图片格式/依赖/发送门未改。

独立只读源码及已执行原件审查；[完整命令/日志/TRX/红源/输入产物摘要/原始失败XML](../test-evidence/2026-10-21-exit-errors-deadline.json)。实现检查点尚未跑修复后完整Core；独审后固定提交只跑一次，失败先分析，不盲循环。当前不称Windows/P7完成，不操作用户电脑、不真实模型/新许可权限费用凭据/CI/main/强推/部署。
