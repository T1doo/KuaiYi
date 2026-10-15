# 独立 owned Job 成员：单次 Windows 验证计划

本批只准备独立合成 fixture，不触发原configure、四case或其他workflow。已审 `aa96555 → 02043d2 → 33de717defa074aea93a6974dd0d7a3f0dc1f2dc` 三提交已正常非force保全到 `dev/job-member-diagnostic-local`；精确tree `4a96c0c53e170bfc0f627d62bf6a0331e1f2afb1`，推送前后该SHA的workflow查询均0。原configure分支保持4b29、原诊断分支bf033、main51d077。

## 精确来源与触发隔离

待执行候选只允许是33de717的一个直接child，唯一parent `33de717defa074aea93a6974dd0d7a3f0dc1f2dc` / parent tree `4a96c0c53e170bfc0f627d62bf6a0331e1f2afb1`。现有工具 `tools/ci/JobMemberDiagnostic.cs` 必须保持blob `227378ffa3a59e80c65da7fc43c0cffcd21d70f7`，不改原Run/runner/lease/collector/五个旧workflow或生产代码。最终候选SHA、tree及逐blob冻结由本批交付回执提供；推送前再次核对唯一写入者、完整tree/parent、九文件和远端祖先。

只新增 `.github/workflows/windows-job-member-fixture.yml`，事件仅push到 `dev/job-member-windows-fixture` 且变化命中该workflow自身路径；无dispatch、schedule、PR事件、matrix、retry、artifact或安装。其他五workflow的分支过滤不匹配此新分支。本次候选不推回原configure分支；原02043栈若推原configure分支会触发旧Windows job，不能用来源门随后拒绝来冒充未触发。

准确九文件范围：

1. `.github/workflows/windows-job-member-fixture.yml`
2. `docs/progress.md`
3. `docs/test-evidence/2026-10-15-job-member-fixture-offline.json`
4. `docs/validation/job-member-windows-plan.md`
5. `tests/scripts/configure-files-fixture.test.py`
6. `tests/scripts/job-member-fixture.test.py`
7. `tests/scripts/job-member-fixture-contracts.test.ps1`
8. `tests/scripts/JobMemberWindowsFixture.cs`
9. `tests/scripts/run-job-member-windows.ps1`

旧文件fixture合同只补识别新的隔离workflow，旧分支过滤断言不变；新合同完整核验新workflow的触发、权限、步骤和时限。

## 一次运行的资源、步骤及时间

仓库本次已通过连接器确认public且未归档；只使用标准 `windows-2022` 单job，timeout5分钟、contents:read。公开仓库的标准GitHub-hosted runner属于[官方免费范围](https://docs.github.com/en/billing/concepts/product-billing/github-actions)，不用larger/custom runner、artifact/cache、额外服务或账单设置。

| 步骤 | 范围与边界 |
| --- | --- |
| Checkout | 已固定checkout action提交；精确github.sha，depth2，persist-credentials=false、set-safe-directory=false |
| 来源门，1分钟 | 精确HEAD/event SHA、唯一33de parent、完整parent tree、原helper blob和九文件差异；输出固定Head/Tree/Parent |
| 合成fixture，2分钟 | 用已存在pwsh/Add-Type编译现有helper与独立fixture，无SDK安装/restore/build；custom `pwsh -File "{0}"` 保留ExitCode与停止错误行为 |

fixture从资源建立前启动同一个30秒Stopwatch，两个捕获共享原截止，余量不足停止。其后统一清理观察窗口5秒，所有Close后再次检查清理截止。故正常逻辑预算为30+5秒；同步Win32调用、编译和调度仍无硬耗时保证，step/job timeout只结束运行，不证明清理确认。编译属于独立合成fixture，没有构建原native或managed目标。

fixture创建A/B两个非继承、KILL_ON_JOB_CLOSE Job。A最多8个、B1个自有pwsh进程，共9个，每个均CreateProcessW suspended → Assign → IsProcessInJob → ResumeThread严格返回1。固定完整PSHOME/pwsh.exe，无profile/noninteractive/no window，无句柄继承；固定公开命令只Thread.Sleep(60000)，白名单环境不复制GitHub凭据并关闭遥测。失败分配不breakaway、不提权，进入统一清理。

## 11项成功检查

1. TwoOwnedJobs：两个新Job由fixture持有，非继承与kill标志设置成功。
2. NineSuspendedBoundBeforeResume：9个自有进程都在执行前绑定并验证。
3. OwnedJobCounts：A8/B1。
4. JobLimitsBeforeProbe：两个Job的flags精确为0x2000，句柄无继承。
5. OwnedRootsLiveBeforeProbe：9个原始自有进程句柄Wait0明确返回WAIT_TIMEOUT；WAIT_FAILED或其他未知结果拒绝。
6. NativeEightMemberAbi：原helper原生入口真实读取A8，固定72字节，8个Other映像basename、诊断句柄关闭确认。
7. OwnedPidReplacementRejected：通过现有IProbe seam提供一个受控B候选PID，真实OpenLimited/IsProcessInJob(A)拒绝。
8. NonMemberZeroImageRead：该单候选只1次列表/1次Open/1次成员检查/1次Close，0次Wait/0次Image请求。
9. BorrowedJobsPreserved：A8/B1、精确Job flags及非继承属性未变。
10. OwnedRootsLiveAfterProbe：9个原始自有进程再次明确返回WAIT_TIMEOUT。
11. OwnedCleanupConfirmed：两个Job计划终止，必要时只对绑定未确认或所属Job终止未成功的已创建自有root作直接终止；9个root退出、两Job AccountingEmpty、查询与fixture全部句柄关闭、清理未过期全部确认。

第7/8项是“Open前PID已变成指定Job非成员”的受控替换反例，B也是fixture自己创建的已知进程。它不强迫或声称真实Windows PID恰好复用，不读取其他进程；A8真实列表不被说成只读一个成员。映像类别也不认证发布者或超时根因。两次捕获全程保持借用Job有效且不并发关闭/复用，helper不复制/关闭/改变它们。

## 失败停止门与证据

任一来源、环境、位宽、创建/绑定/恢复、数量、成员拒绝、映像或预算检查失败即结束该次业务路径并统一清理，无重试。嵌套Job不支持或Assign拒绝也固定失败，不绕过。所有已创建process/thread立即登记；Create/Assign/verify/resume中途失败仍清理。Job关闭只是兜底，不能用最后Close后的自动终止来代替此前已观察到的root退出和JobEmpty。

输出只有一条 `KUAIYI_JOB_MEMBER_WINDOWS` 闭合回执，含源SHA、固定stage/checks、状态、原生/受控反例状态、资源计数和清理确认。只有整体Pass且清理确认、主预算未过期时才发布有限观察及反例计数；失败/清理未知保留固定状态并清空公开观察。不输出PID、句柄、路径、命令或异常。成功Exit0，受限环境Blocked2，其他失败3；外层custom shell保留原退出码。

父任务核对最终精确freeze、独审和既有授权后，才正常推该候选一次，监测该SHA仅一条新workflow/run attempt1至终态并保全完整tool回执与解码日志；任何失败/超时/取消不自动重跑，后续只本地分析。此准备批尚未推送workflow候选、没有启动Windows。完整离线计数/输出hash见[固定证据](../test-evidence/2026-10-15-job-member-fixture-offline.json)。

当前实际本地验证：新fixture的992项注入断言/49场景、5项来源合同、原helper411项断言/63场景与旧文件fixture16项合同通过。真实owned浅克隆完整来源门depth1拒绝、depth2通过、篡改helper pin拒绝；五个旧workflow和原helper/runner字节保持。Linux入口Blocked2、Created0、无Win32调用。新增ABI断言首遇PowerShell选错Marshal.SizeOf泛型重载，失败输出完整保留；改用C#强类型Type重载后5个布局尺寸通过，不把此harness失败改称Windows失败。独审还要求前后live检查仅接受WAIT_TIMEOUT，已增加未知等待反例并复测通过。

实际Windows验证只证明此标准host上的自有合成Job ABI、有限成员查询、受控非成员拒绝与句柄清理。原NativeConfigure超时原因仍Unknown，不接原runner、不延长原超时/救援时序、不运行CMake/四case/真实模型，ProductionReuse Off，250/600ms及来源/密码/完整性/取消/清理安全门和P0/E06/生产验收状态保持。
