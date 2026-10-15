# Configure-only 本地准备

本批仅实现、离线测试和独审，不运行实际 CMake、不启动新 CI。已通过的 bridge 执行源码为 `66d671e526d81a1013e6401f2664b436a1468533`，其唯一 Windows attempt 的15项检查不重复。结果文档已正常交付 `b4592d5d60d38cbbf186a38dbba2b1ae07b0482c`，该文档提交的 workflow-run 查询为0。

## 来源与范围

独立入口 `tests/scripts/run-configure-only-windows.ps1` 复用已有 C# operation envelope、root lease、Python collector 和严格投影。保留原九个来源/工具前置操作及原 NativeConfigure 全部参数；configure 之后仅采集并结束，成功也不会进入 NativeBuild、ManagedRestore、ManagedBuild 或四个 case。旧 runner、四份旧 workflow、生产及底层采集实现字节不变。新 workflow 仅匹配 `dev/configure-only-diagnostic` 上自身文件的 push，尚未推送。

未来可执行候选必须恰为 `b4592d5d60d38cbbf186a38dbba2b1ae07b0482c` 的唯一直接 child，审定父完整 tree 为 `0b420e3ffe04d332422f9f1aa16b6a09331f2f39`；锁定祖先 `3ae69419a1152ea9694a3a9cf11130fbae645fa3` / tree `870a72ed3ef2ee31252c10e2d63cbcb859e7fd7b` 及全部 runtime 输入比较保留。实际本地 owned Git 浅克隆证明此拓扑 depth11 缺锁定祖先、depth12 可验证。未来若增加提交或改变 parent，必须重审拓扑及 depth，不放宽身份门。workflow 核对 event HEAD、唯一 parent、parent tree、locked ancestor/tree、精确九文件清单，并输出完整执行 tree；runner 再核 HEAD/parent/祖先/runtime 输入。

九文件为新 workflow、新入口、新 PowerShell 注入测试、新 Python 来源测试、本说明、progress、本地测试证据、旧失败固定回执，以及旧文件 fixture 测试中显式识别新 workflow 的一行。新测试单独断言新隔离条件，旧分支过滤断言保留。

## 结果与租约边界

根 lease 在任何 configure 前建立。原 configure Run 返回后先检查原 CleanupConfirmed；未确认时零 collector 调用。原结果、30秒预算、JobEmpty、救援和清理语义不改，失败仍失败。collector 是独立 operation，不进入原 Operations；仅在其 Completed/Exit0/cleanup确认后读取至多8KiB的严格 JSON。四个原文件各64KiB、总256KiB，只投影版本和阶段事实，缺失不补造。

新增有限 `CollectorSettlement` 为 NotStarted、Unknown 或 CleanupConfirmed。collector 清理未确认或调用抛出时不读其 JSON、不接受文件事实，不称采集已结束；父只释放自己的 lease 一次。若 Python 已取得 duplicate，它和 adapter 的副本继续持有同一根对象直至自身读取结束；若源先关闭，正常复制失败时不进入文件 API。这是句柄引用的连续性，不是 collector 结算证明。现有实现不会扫描进程、重新取得根路径或增设等待/权限。[DuplicateHandle](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-duplicatehandle) 与 [CloseHandle](https://learn.microsoft.com/en-us/windows/win32/api/handleapi/nf-handleapi-closehandle) 的官方对象引用语义是依据；两种先后次序另有内存替身合同，不能当作新的 Windows 实测。

原九个前置操作、configure、collector 共最多11个30秒预算，名义成功预算合计330秒；configure/collector 各自原有救援观察仍独立，不能因新增诊断延长原 configure。新 workflow step7分钟/job10分钟；同步调用和清理没有新的硬墙钟保证，step 超时不等于清理确认。无自动重跑、安装、artifact、服务、权限、模型或费用配置。

## 已有失败与本地验证

本轮通过已授权连接器实际取回旧 [CI38669606027](https://github.com/T1doo/KuaiYi/actions/runs/38669606027) 的 job116209346264：attempt1/failure，源码 bf033；NativeConfigure root Exit0、WaitJobEmpty Timeout、PreRescueJobState NonEmpty、BudgetExpired true，救援后 JobEmpty/CleanupConfirmed true。完整解码日志私有保存，20,354字节，SHA256 `527af95557fbdde838bd5eb2b8256d2e1644da98adecac8b2714bc4da0987457`；[固定回执](../test-evidence/2026-10-15-original-configure-timeout.json)不回填旧未知值。原四文件仍均缺失，没有从 fixture 或摘要重构。记录不含滞留进程身份，具体根因仍 unknown。

新 PowerShell 171项注入、新 Python 6项来源/引用次序测试通过；详细复核见[本地证据](../test-evidence/2026-10-15-configure-only-offline.txt)。注入包括原清理未知、collector清理未知/超时但清理确认/非0退出/抛出/缺失/超限/敏感输出，原 receipt、原因、退出值及一次父关闭；成功 configure 后静态终止边界与原参数一致。独立 Linux 入口只返回 Blocked，原CMake/四case不执行。原问题未修复，P0/E06/生产验收不推进；250/600ms与来源/密码/完整性/取消/清理安全门保持。
