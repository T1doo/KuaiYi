# 既有 Accounting 两点快照：最小本地接入

环境命令、文件及Git可用，起点118981aead5dc230351efb8ff210963d87d5522c/treefaf47e06c6a9d20f598bf8589b4463ae9214912f，单worktree与独占写入锁已核。本地dev/configure-accounting-local不匹配六份现有workflow分支；本轮无push、Windows、原Run、CMake、build/restore或四生命周期。原始成功/失败证据与118981a可行性归档均保留。

## 最小接入与不变条件

只改三个运行相关文件：LifecycleOperation.cs、lifecycle-ci-evidence.ps1、run-configure-only-windows.ps1。

C# Receipt新增公开readonly Accounting成员，持有一个固定大小AccountingDiagnostic对象，只有两个值类型快照FirstWait、LastPreRescue及两个内部布尔标记，无增长容器、回调、线程或新句柄。快照只有Known、nullable uint ActiveProcesses/TotalProcesses。默认Unknown/null，不填0；真实查询返回0时才是Known/0。

原ObserveJobEmpty在root退出分支内调用原Empty；现在仅给Empty传该固定对象。既有QueryInformationJobObject之前将Last置Unknown并锁定是否首次，查询成功后复制原data.Active/data.Total，仍原样return data.Active==0。查询失败或异常不复制部分buffer，原NativeFailure与错误映射保持，最后一次失败即Last Unknown；首个失败则First也Unknown。

FirstWait是root退出后首次正常等待查询的结果，LastPreRescue是最后一次正常等待查询，可能为Empty且最终没有救援；不是“最后一次非空”。Unknown既可能尚未查询，也可能查询未成功。清理仍原样调用Empty(job)，默认不携带诊断对象，所以清理成功、失败或重查询都不能洗掉预救援值。

整个原Run方法、while短路表达式、Sleep、clock比较、catch/finally与关闭顺序逐字未改；PInvoke声明集合和原Accounting结构未改。零新增原生调用、重试、等待、线程、Stopwatch读取或进程句柄。固定字段复制和每receipt的固定对象仍有少量指令/分配耗时，不承诺绝对零耗时或新的硬预算保证。完全不接CaptureWindows，也不暂停/扣除原时钟。

原Get-LifecycleOperationEvidence保持原字节、原Operations/旧operation JSON投影schema和GetReason不改。configure入口只在NativeConfigure的Run返回后保存原receipt引用；所有操作与lease的finally结束后，另输出KUAIYI_JOB_ACCOUNTING。新输出只包含两个快照，缺失输出不能当零。新投影异常或仅新前缀的Write-Host失败返回独立Unavailable，不重试输出、不影响后续原summary与exit。Known仅代表原查询成功，不代表清理通过；清理状态仍以原receipt为准。

## 计数的准确含义

按[官方JOBOBJECT_BASIC_ACCOUNTING_INFORMATION](https://learn.microsoft.com/en-us/windows/win32/api/winnt/ns-winnt-jobobject_basic_accounting_information)：ActiveProcesses是当前Job关联计数，不能称“正在执行的子进程数”；退出与引用释放影响其递减。TotalProcesses是Job生命周期内关联的累计数量，包括已终止进程和某些关联失败情形。TotalTerminatedProcesses只统计因限制违规被终止的数量，本实现不读取它推算全部退出。两个计数不能识别进程、映像类别或根因。

离线合成样例，禁止回填历史日志：

```text
KUAIYI_JOB_ACCOUNTING {"Kind":"ExistingJobAccounting","Scope":"PreRescueWaitQueriesOnly","FirstWait":{"State":"Known","ActiveProcesses":1,"TotalProcesses":5},"LastPreRescue":{"State":"Known","ActiveProcesses":1,"TotalProcesses":6}}
```

后一次查询失败时，LastPreRescue为State Unknown、两个计数null；FirstWait已成功的值保留。未进入等待分支时两点均Unknown。只有固定状态/数值，不输出PID、句柄、路径、命令或异常。

## 针对性离线验证与调用轨迹

162断言/11场景通过。测试从真实父提交与当前源码分别生成只替换Accounting原生绑定的托管版本；执行原WaitJobEmpty片段，Sleep改成轨迹事件、clock改成虚拟值，不调用Run或Win32。逐项对照查询/clock/Sleep次数与顺序、旧receipt投影/原因、布尔返回和异常；全Run源码本身及PInvoke声明集合也核对相同。每个等待场景另以0/非0/失败/异常清理查询检查布尔/错误一致及预救援快照不被覆盖。uint32最大计数、未查询/查询失败Unknown和新输出故障保持原Exit3也覆盖。

| 注入场景 | 父/当前相同的等待轨迹 |
| --- | --- |
| 首次已空 | Q(0,5) → Q(0,5) → Clock |
| 后来为空 | Q(1,5) → Clock → Sleep10事件 → Q(0,6) → Q(0,6) → Clock |
| 预算到期非空 | Q(1,5) → Clock → Q(1,6) → Clock |
| 首次查询失败 | Q(F)；无新clock/等待 |
| 后次查询失败 | Q(1,5) → Clock → Sleep10事件 → Q(F) |
| 最后补查失败 | Q(1,5) → Clock → Sleep10事件 → Q(0,6) → Q(F) |

这证明注入绑定下调用轨迹和判定相同，不是实际Windows时序测量。原configure注入171项、旧公开证据45项回归通过。旧configure来源审计6项中5通过、1拒绝：其旧冻结合同要求LifecycleOperation.cs原字节，本次授权的Accounting改动因此被拒绝。完整失败日志保留，不删除或放宽旧门、不冒称全绿；它也证明不能直接执行旧候选来试本改动。

17份helper/lease/collector/旧runner/六workflow/历史证据及配置文件原字节保持；Directory.Build.targets在本地与父提交均缺失，未补造。两套旧私有证据manifest全项核验不变。准确hash、八条完整轨迹与输出计数见[固定证据](../test-evidence/2026-10-15-configure-accounting-local.json)。

## 后续一次 configure-only 计划（本轮不执行）

父核本次精确SHA/tree/唯一parent、七文件diff、独审与旧门拒绝后，另准备新的branch+self-workflow-path执行候选；只允许本次两计数快照，绝不重启成员Capture路线或原失败run。新source门需锁新的唯一parent/tree、locked祖先、产品运行源码不变和精确完整差异；按实际祖先链计算checkout深度，不能复用旧depth12或绕过旧拒绝。

只标准public windows-2022、contents:read、固定checkout action、来源1分钟/执行7分钟/job10分钟。保持原前置检查→一次NativeConfigure→原cleanup确认后四文件采集→停止；原每操作30秒、原救援/清理时限、configure前root lease至采集结束关闭、Python DuplicateHandle进程归属、每文件64KiB/总256KiB和缺失不补造不变。无安装、额外服务/权限/凭据/费用、build/restore/四生命周期或真实模型。

父复核新的可执行freeze后才正常推指定新分支一次，监测唯一attempt1至终态，保全完整日志及原始回执；失败/超时/取消不自动重跑。预期只获得两点有限计数，也可能Unknown，不保证找到原configure根因。原根因Unknown、ProductionReuse Off、ModelCalls0，250/600ms及来源/密码/完整性/取消/清理安全门和P0/E06验收状态保持。
