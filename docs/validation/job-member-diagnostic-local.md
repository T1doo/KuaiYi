# Job 成员有限诊断：独立本地实现

基线是 `02043d2f8bb429d416b54586f30f17d54bb38d5a` / tree `200584315c9a5aebd72ef823aa22e88d2c681aa0`，其独审和五文件 freeze 已重新核对通过。本批只在 `dev/job-member-diagnostic-local` 完成独立诊断工具及注入测试，没有接到原 `LifecycleOperation.Run`、configure-only 入口或 workflow。

## 当前证据与可区分的缺口

真实 run38690235831 的四文件有限事实说明 configure/generate 完成标记、MSVC ABI Done、Working Skipped、VS2022 generator 和 try_compile 事件存在；原 NativeConfigure 仍 root Exit0、WaitJobEmpty Timeout、救援前 NonEmpty/BudgetExpired、救援后 JobEmpty/CleanupConfirmed。原 Accounting 查询只返回 Active 是否为0，现有事实无法识别救援前成员，也无法判断具体程序为何存活。shell 退出码补正未改变这个根因缺口。

新工具 `tools/ci/JobMemberDiagnostic.cs` 可把采样时仍属于指定 Job、且未退出的成员映像 basename 投影为 MSBuild、PdbServer、Compiler、Linker、CMake、Other 计数，另保留 OpenUnavailable、MembershipUnconfirmed、RunningUnconfirmed、ImageUnavailable。dotnet.exe 归 Other，不推测它运行了什么组件。成功未知名称 Other 与读取/验证失败的 Unknown 状态分开。

三个实际离线注入反例拥有相同 AccountingActive=1、原 OperationTimeout，分别产生 MSBuild=1、PdbServer=1、Other=1。它们证明这一观察可以区分原布尔信息不能区分的类别；三个结果全部标为 SyntheticInjectedOnly。类别仅证明检查时持有句柄对应的映像 basename，不认证发布者、安装来源、二进制内容或造成超时的因果关系；也不能绑定为 Job 列表查询时的那个历史进程身份。

## 自有 Job、上限与竞态

- 只接受受控调用者传来的、本进程持有的自有 Job 句柄；零和-1拒绝。调用者必须在整个同步Capture期间保持该句柄有效，禁止并发关闭/复用。数值有效性不证明所有权或生命周期，未来调用点必须在创建/持有原 Job 的作用域中受审。工具不提供读取外部句柄整数的命令行入口。
- 借用 Job，不复制、继承、关闭或改变它，也不枚举全系统进程或打开其他 Job。对该句柄只调用一次 `JobObjectBasicProcessIdList`，固定最多8成员/72字节，不扩容、不重试。列表还可能包括嵌套子 Job 的成员。
- 失败调用的部分 buffer 不解析；容量不足、数量/长度不一致、重复PID、0或超过DWORD的PID拒绝，之后零Open。空列表只是该次查询的观察，不能追认原等待已正常完成。
- 每个PID仅来自这一成功完整列表。OpenProcess仅请求 `QUERY_LIMITED_INFORMATION | SYNCHRONIZE`，无继承；先核对同一 Job 成员资格与Wait0未退出，再读取固定1024字符映像buffer，再对同一持有句柄复核成员及未退出。查询失败不扩权、不重试，不读取命令行或用户文件。
- Open前PID复用为非成员会被拒绝；若新进程已进入同一 Job，只能记录本次持有句柄观察，不能声称捕获原历史身份。成员关系/退出状态改变时不保留该类别。
- 每个有效Open句柄在finally尝试关闭一次。关闭失败/抛出后停止后续查询，清除全部类别和列表事实，独立记录HandleCloseUnknown；不改原operation receipt或原清理结论。DiagnosticHandlesClosed只描述本工具的进程句柄，不证明原Job清理。
- 公开JSON只含闭合状态和有界计数，不含PID、句柄、路径、命令或异常。这是一次非原子观察，不是完整稳定快照，零类别计数不证明不存在该类成员。

[列表定义](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_process_id_list)、[QueryInformationJobObject](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-queryinformationjobobject)、[IsProcessInJob](https://learn.microsoft.com/en-us/windows/win32/api/jobapi/nf-jobapi-isprocessinjob)和[映像查询](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew)提供上述布局、Job查询及最低进程权限的官方依据。原生入口同时要求Windows、IntPtr.Size=8、进程X64和OS X64；32位进程运行在64位Windows时仍拒绝，避免误按8字节解析ULONG_PTR。

## 预算与为何尚不接原 runner

工具不创建新的30秒预算，Windows入口必须接收原operation的运行中Stopwatch，计算距原30000ms截止的余量。每次查询前检查余量>250ms，返回事实前再次检查；预算不足清事实为SkippedBudget，余量不足后仅执行已取得句柄的finally关闭。250ms是这个独立诊断的余量保留条件，不改变产品的250/600ms门。

一次最多1个列表查询和8组Open、两次成员查询、两次Wait0、映像查询、Close，共57次显式成员探针API调用。固定次数和buffer上限不能保证同步Win32调用的硬耗时，单次调用仍可能跨越原截止。新后台观察器若持有Job副本，也可能延长最后句柄寿命并改变KILL_ON_JOB_CLOSE时序。因此本轮不增加后台线程/进程，不将此工具内联到原Run，原30秒/JobEmpty/救援/清理语义没有新调用影响。

后续的最小平台验证应先在独立owned synthetic Job fixture验证真实布局、成员拒绝、映像读取与句柄释放，继续禁止CMake、四case及生产调用；验证后才能审议原等待观察点的时序设计。当前没有冻结fixture入口或新workflow，不能把本提交称为下一次可执行Windows候选。本轮不请求或启动新的Windows运行；若后续确需运行，须先提交精确源码/单次时长/触发范围及独审结果，由父任务核对授权。

## 实际本地验证与交付边界

既有PowerShell7.5.3编译完整C#工具，纯注入411断言/63场景通过：列表容量/畸形/部分buffer、PID复用与成员变化、退出/未知、映像失败/敏感路径、预算前/中/最终耗尽、关闭失败、原timeout receipt逐字节及GetReason保持。六个平台布局断言覆盖32位进程、错误指针宽度、ARM64和非Windows。Linux原生入口明确Unsupported，没有调用Windows API。另6项既有来源/引用次序回归通过；16份旧runner、采集器、五workflow及历史证据字节未变。[固定证据](../test-evidence/2026-10-15-job-member-local.json)保存准确计数、哈希和合成反例，原始输出在ignored artifacts。

初次测试harness的PowerShell预算数组拼接因op_Addition失败；原始log保留，修正数组构造后通过，未降低断言。中间通过版本也完整保留。独审要求增加进程ABI门，另加最终预算复核以丢弃晚到事实；这些都是本地工具补正，不回填旧失败或Unknown。

远端仍为configure-only分支4b29、原诊断分支bf033、main51d077。仅aa965文档归档推到原configure分支不会命中workflow路径；02043含该workflow自身路径，若推到原configure分支则会启动Windows job，即使随后来源门拒绝。02043正常推到自己的shell本地开发分支，或当前完整stack推到新的成员诊断开发分支，均不匹配现有五workflow分支过滤。触发分析只证明路径/分支匹配，不代替执行授权；本轮没有push。

原故障仍Unknown；Windows ABI/权限/实际句柄行为NotRun，原CMake及四生命周期不重跑，ProductionReuse Off/ModelCalls0，P0/E06及生产验收不推进。所有生产源码、250/600ms和来源/密码/完整性/取消/清理安全门保持。
