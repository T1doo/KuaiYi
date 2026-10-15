# 原 runner 成员诊断接入：可行性与待决契约

本批基于1dfafc36da5c393b511c2143ac3894fac3f90e9a/tree4d8b18904d843b2e38598f2ecf232002328f24da。该成功实测归档、原始日志以及更早失败归档原字节保持。Windows fixture run38709320255/attempt1的11门通过已接收，仅证明合成自有Job，原configure根因Unknown。当前无新Windows、CMake、原Run、build/restore或四生命周期执行。

## 可满足与不可同时承诺的边界

建议位置是原Run的WaitJobEmpty循环：root退出已确认、首次既有Accounting观察NonEmpty之后、同一个原Stopwatch余量>250ms时，最多调用一次CaptureWindows，借用同进程自有Job并在调用返回前持续持有。不得重置时钟、暂停计时、扣除诊断耗时、复制Job或另起后台观察器。

在软预算解释下，默认关闭的最小同步once bridge可以实现：进入前置attempted，预算不足零调用；只传Job和原clock，不传原receipt；读者的普通异常仅更新独立有限状态，返回或抛出后继续原循环及finally；原finally救援与句柄关闭代码、次序不变。HandleCloseUnknown也不能直接覆盖原receipt，但诊断必须单独可见且不发布事实。原Run本身没有CancellationToken，也已有同步Accounting/Flush等调用；不能额外声称新的强制取消协议。

但是以下两项不能随该实现一起承诺：

1. 新同步调用不返回时，仍保证原finally及时进入。Guard只在API之间检查，不会中断正在进行的Query/Open/Image/Close；catch也要等调用返回或抛异常。
2. 诊断不直接改receipt就能保证反事实运行结果完全相同。有限迟返也能消耗共同预算，改变原最终超时判定。

第二项有无需挂起的确定性反例：

| 时间/条件 | 无诊断 | 同步诊断 |
| --- | --- | --- |
| 29700ms，首次NonEmpty，余量300ms | 继续原10ms轮询 | >250ms，允许进入一次Capture |
| Job在29900ms变空 | 在原预算内完成 | 仍在诊断调用中 |
| 诊断在30100ms返回 | 已Completed，无救援 | helper清事实为SkippedBudget |
| 原CompleteJobWait | empty=true / expired=false | empty=true / expired=true，Timeout并救援 |

这不是实测原configure事件，也不推断真实API曾迟返；它证明“同一预算+异常隔离”不足以消除调度影响。不能通过吞异常、假报空Job、停表、排除诊断耗时或改原超时判定来制造不变。

## 本批结论与最小替代

真实hook暂HOLD，生产源未改。这里没有擅自把现有软预算说成原来就有硬耗时保证；待父任务明确的是“原结果保持”仅指不直接改receipt/失败映射，还是也要求边界时序结果和救援及时性不受新增同步调用影响。前一解释可继续准备默认关闭的once bridge；后一解释与内联同步成员查询冲突，应保持不接入。

如果不能接受新增同步探针的风险，最小替代是在原来已经执行的Accounting查询返回后，只记录其既有返回值的有限状态，不新增任何原生查询、扫描或后台资源。它不能给出成员映像类别，不能冒称成员诊断已完成；可用于确认首次非空的有限计数。原Empty/PreRescueJobState/CompleteJobWait与最终receipt判定不得改。救援后再读成员列表也不能重建救援前身份，不是替代证据。

后台Task借用原Job会与finally关闭/句柄复用竞态；复制Job到后台又延长最后句柄寿命，违反本批约束。外层step/job超时也不能证明内部救援或清理已确认。

## 离线证据

新增一个提案可行性合同测试，编译原字节的LifecycleOperation.cs和JobMemberDiagnostic.cs，调用真实Capture/CompleteJobWait/GetReason，使用确定性虚拟时钟与注入IProbe，不调用Run或任何Win32 API，不创建Job/子进程。它是提案调度反例，不是已接入runner测试。

42断言/13场景通过：最多一次；同一虚拟原截止不重置；余量<=250ms零调用；空Job零调用；返回异常/读者取消异常/CloseUnknown后模拟原救援和关闭；失败或迟返事实清空；有限迟返改变原结果的反例。读者取消异常是IProbe抛OperationCanceledException并由helper映射ReaderFailure，未证明原操作响应取消。未返回调用阻止同线程finally是控制流分析，本测试没有把它标成实际native挂起或强制取消证明。首版PowerShell long::MaxValue语法失败日志保留，修正为[long]::MaxValue后通过，没有削弱断言。

原helper、operation、configure入口、既有fixture、六个workflow与成功结果文件共11份保护文件逐字节未改；成功和失败两套原始证据manifest全部核验。完整输出hash见[固定证据](../test-evidence/2026-10-15-job-member-runner-feasibility.json)。

## 后续单次 configure-only 验证计划（当前不可执行）

先由父任务选择上述契约或最小Accounting替代，再实现并独审真实接入，冻结新的完整SHA/tree/唯一parent/精确文件清单。当前四文件可行性提交不含可执行接入，原configure来源门仍锁旧parent，不能直接推去试跑。

若接受软预算并审定真实once bridge，后续候选应只给NativeConfigure启用；其余原操作和file collector不启用。成员观察保存在独立状态，原操作返回且CleanupConfirmed、root退出/JobEmpty及诊断查询句柄关闭均确认后才允许发布此前预算内的有限快照；诊断采集自身SkippedBudget/查询失败/关闭未知事实为空，不能将诊断成功映射为原操作成功。原操作随后Timeout不应追溯抹去已在预算内采集且最终清理确认的独立快照；早期快照通过不意味着原操作随后不能超时，类别不等于超时原因。

另行新增隔离branch+self-workflow-path的configure-only一次候选；不得修改或推送旧configure分支以规避来源门。只标准public windows-2022、contents:read、固定checkout action、足够深度的精确父链/locked祖先/产品运行源码不变/差异清单门。来源步骤1分钟，执行7分钟，job10分钟；原每操作30秒、原救援与清理时限、configure前root lease和采集后关闭顺序均保持。同步API无硬耗时保证，外层超时不是清理证明。

仅原前置检查→一次NativeConfigure→原cleanup确认后有限四文件采集→停止；每文件64KiB/总256KiB，缺失不补造，DuplicateHandle进程归属不改。无build/restore/四生命周期、真实模型、安装、服务、权限/凭据/费用变化或自动重跑。父核最终新freeze/独审/范围后才执行唯一attempt1，保全完整原始回执日志；失败/超时/取消只本地分析。

当前ProductionReuse Off、ModelCalls0，250/600ms与来源/密码/完整性/取消/清理安全门、原失败和P0/E06状态保持。
