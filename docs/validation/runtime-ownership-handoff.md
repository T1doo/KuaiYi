# 选定版本运行期所有权交接（合成进程）

父 `0137420b56c75500ba03cce36bbb5f57e2f9d7dd` 有界升级协调核心获父独审准入后，正常分支 `dev/runtime-ownership-handoff` 继续 SPEC13.4/P7。此片实现、验证可移植的明确接收者交接；生产 Windows 入口、可见健康、Job、身份和 NativeHost 注册均未接线或执行，不抬完整安装器/P7/K组状态。

## 提交与责任

`UpgradeAndHandoffAsync` 复用现有完整版本校验、健康握手、精确 pointer 回退和实际 Start 所有权。新协议仅向自身实际 Start 返回的 Process 发送 offer；offer 绑定 transfer、receiver、launch、根、数据根、选定版本及精确 owner/current/upgrade 字节。canonical/bounded 解码拒绝未知字段和非原生成编码，launch 只是关联值，不是凭据。

parent 持 upgrade admission 写 Prepared，receiver 在实际选定 generation 内确认准备；parent 放 execution OS lease，receiver 取得同锁、复核精确记录并回 ACK；parent 检查真实已持锁及自己的 child 未退出，原子写唯一 Committed 决定。普通 Apply/Recover/RoleAdmission 的要求没有放松。缺失锁文件、非锁冲突 IO、错 receipt/身份、未知 ledger 都不能证明接管成功。

提交前失败，必须先确认自有候选实际退出、原 IO/协议/取消任务及 startup 已结算，才能 reclaim 同 owner lease、归档自己的 Prepared 并精确恢复旧 pointer，实际重启旧版本。未结算保持 pending；同实例可在原任务释放后 RetryRollback。未知字节不覆盖、不清理、不凭 PID 推定所有权。

Committed 是唯一不可逆点。提交系统调用结果不确定时探测精确 Committed/Prepared，未知结果保持 CommitUncertain；Committed/CommitUncertain 之后任何 ACK、取消、控制 IO 或归档失败均不能走 Kill/回退。结果区分 Committed 与 CommittedUnconfirmed，后者不是成功回执。旧 controller 的 Start/Upgrade/Retry 被拒绝。`Transferred` 与 Exited/Finalized/disposed 分开，Stop 返回 OwnershipTransferred，ObserveExit 为 false，Finalize 不声称退出。默认旧 UpgradeAsync/Dispose 仍终结自身持有 child，显式新入口才交接。

receiver 提交后容忍 controller stdio EOF，根据精确 durable Committed 继续；控制 IO 使用独立 live-control 操作，关闭 stdin、取消并 await 原 stdout/stderr reader 与取消工作。原 SettleAsync 仍要求实际 child 退出，未降低断言。阻塞的原控制任务保留，Dispose 不能跳过。receiver Abort 也保留 execution lease 至 OS 退出，避免 Prepared 检查与 parent 提交间的释放窗口；此处实际测试证明 Abort 阶段仍占锁后回退，未直接复现指令级 commit 竞态。

receiver 无法证明自己已经退出。因此 committed runtime 的 owner、upgrade pending、handoff ledger 保留，fixture 将 lease 静态持根到 Environment.Exit；STOP_SELF 不清 marker，不据自报/EOF开放 fresh controller。正常自退后 fresh 仍拒入。自动正常重启、stale runtime 接管、完成标记清理没有实现，后续若实现需要另外持真实 Process 句柄的退出观察者，不能凭 PID/锁释放/EOF替代。

## 实际进程和测试边界

仅项目自有合成进程：controller 由测试直接 Start 并保持 Process，选定 generation 内真实 driver DLL/依赖由 controller Start，测试 EXE 占位映射明确仅用于 fixture。根测试不按 grandchild PID 打开或 Kill。receiver 自身是明确的运行期与清理接收者，独立 private named pipe 响应 PING/STOP_SELF，测试专用 40 秒自退上限避免无人负责进程。它不依赖 parent stdin 保活。

测试先确认实际 controller Exit，然后 PING 接收者、STOP_SELF、公开 cleanup 收据、pipe EOF 和静态持根 lease 的 OS 释放；这几项分开记录，不能写成根测试握有 grandchild Process 的 WaitForExit。每个 controller 和 selected-health 行的实际 driver SHA、两侧 Core DLL 与执行元数据绑定。新测试只涉及合成配置 sentinel，不冒称新 DPAPI/真实 SecretStore 验收；父 SecretStore 原例保留。

16 个交接用例覆盖成功交接、自退后仍拒 fresh、拒绝/取消/无 lease 假 ACK/错 ACK/迟到、丢 commit ACK、原控制任务持有、真实竞争 admission child、Abort 持锁、precommit 原 task→同实例 Retry、postcommit cancel 与三旧入口拒绝、持活角色的提交前/后 controller Exit81/82，以及未知 ledger 保留。旧 Exit79 启动前 checkpoint 仍只证明 pre-start durable fence，不冒充带活角色恢复。

另新增 1 个受控 stop-task 用例：真实 child OS 退出后暂扣原 lifetime Stop task；Retry 必须仍 pending，不能恢复旧版，释放后才精确回退。StopGracefully 现在独立确认同 child 已退出后，有界 await 缓存的原 Lifetime.StopAsync，再走原 IO/协议/Exited/Finalize 门。超时/取消仍保留 owner/task，不用 stop 返回枚举替代真实退出。

## 红绿及失败原件

所有尝试的 command、执行时间、源码原字节、DLL hash、log/TRX 原件均封存，失败不删除、不倒填。

- r03：xUnit2031 分析器编译失败，无 TRX，不算行为红。
- r04：新测试 pipe 尚未连接就 AutoFlush，fixture 失败，不算产品红。
- r05：旧入口合同断言被 broken-pipe cleanup 异常遮蔽；stdout 保留 Upgraded/None 与健康新 child Exit137，不用作干净的红绿。清理改为对已关闭自有 pipe 的窄 IOException 处理，primary 断言后续保留。
- r06：实际 2F。旧 UpgradeAsync 入口得到 None、Dispose 终结新 child；最终同例改显式入口，属于入口对照，不称同参数同方法字节的产品修复。缺锁假 ACK 用例 expected controller Exit5、actual0，证明 IOException 包含 FileNotFound 的误放行；修正后拒绝并保 Prepared。该用例后来仅添加 controller 的 ITestOutputHelper 诊断参数，名字及所有断言语义保持，不称全方法原字节完全相同。
- r07：11P2F，丢 ACK 后原控制任务重试及 fixture 窗口失败；r08：12P2F。fixture 原 `Controls()` 直接调用 Console.In.ReadLineAsync 会同步占住独立 pipe 循环，窗口直到 budget 到期；用 Task.Run 独立控制读取后 r09 交接 16P。此为新 fixture 的实证，不用来解释历史图片/idle/Core/Job。
- r10 完整 Core：1785P1F，旧 LateActualStartOwner 在 Retry L151 报 Busy，记录 child Exit137 和 IO settled。该次确切根因 Unknown，未以之后通过倒推。
- r11：新增 stop-task 受控例实际 1F，原任务被扣时 Retry 提前 IsCompleted=True；r12 同方法原字节绿，相关安装 85P。该独立反例证明等待缺口，不等于证明 r10 的具体原因。

最终 r12 相关安装 **85P**、r13 完整 Core **1787P**、r14 完整 Provider **2325P**；r15 App/r16 Worker 各 **0 Warning / 0 Error**。五轮各50份源码共250绑定，880份运行/采集原件加1份独立审计回执，共881份原件；6458父路径仅9项预定修改，其余6449原字节保持，无删除。

最终完整回归与构建、源码绑定、全名多重集、父文件保全和独立审查结果见[机器证据](../test-evidence/2026-10-17-runtime-ownership-handoff.json)。父完整 Core1770 名字/原断言保留，新增交接16+原 Stop join1；完整 Provider2325 名字保持。原 Core 升级测试只追加新 case/helper，旧方法不删不改。

## 保留边界

分阶段 budget 不是整个操作硬时限；package hash 不是发行者签名信任；原子替换/Flush/进程 Exit 不证明断电、恶意同账户换名或 OS 注册原子事务。旧/失败 generation 和历史记录保留，无累计磁盘有界声明。只控制本 controller 自己 Start 的角色，不证明系统全部角色已停。

Windows 正常入口、真实关闭/可见健康/Job/交接、identity/NativeHost 派生绝对路径与注册 rollback、完整 payload/Inno、登录启动/卸载/K组仍未执行或未完成。真实 WPF 键盘可见性、焦点、截图、DPAPI/ACL、物理250/600ms、模型和完整 P6 验收 NotRun。图片/idle/Core/WindowsJob 历史根因各自 Unknown。生产图片/分类复用 Off，来源/密码/完整性/取消/清理门不变。

没有 main/强推/部署/费用/真实模型/凭据或安全权限改变。Skia4.153.1 四包许可待用户批准，没有安装/运行新许可包；source403 和此前拒绝的 GitHub Actions 不重查、不换路绕过。未知锁/数据/日志/进程保持。
