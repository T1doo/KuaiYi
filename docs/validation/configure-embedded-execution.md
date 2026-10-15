# EmbeddedExecV1 精确执行来源门与单次对照

父已明确授权静态候选复核后，最终独审通过即正常推新开发分支并执行唯一一次configure-only对照。准备快照Windows尚NotRun，实际结果另档，不回填旧结果。

唯一直接父为b771f1e8442d453b3df65144f006f694a2b7a294/tree300daf380032eaeecd476f90111055075b51b231。原失败基线执行a0e8519/run38716240995/attempt1与a1291b99归档均原样保留；静态候选和30份精确CMake3.31.6来源见[静态说明](configure-embedded-static.md)。

## 三处精确差异

- 新workflow `.github/workflows/windows-configure-embedded.yml` 只响应新branch `dev/configure-embedded-windows` 的自身路径push，显式EmbeddedExecV1，repo/ref/event精确门，contents:read，标准windows-2022。来源1分钟、执行7分钟、job10分钟；固定checkout action/ref/persist-credentials=false/set-safe-directory=false，custom pwsh -File原样传递退出码。七旧workflow原字节/分支保持，无dispatch、matrix、重试、额外artifact/cache、服务或安装。
- 新来源门复用已验证的Git/Pwsh字节检查机制，锁b771唯一父/tree、locked3ae69419/tree870a72ed，完整六文件大小写scope及三运行blob/规范SHA/工作内容。SHA读取git show的BaseStream，不经PowerShell行编码；工作内容只按仓库既有text规则归一。新执行直接child的祖先链包含locked需25节点，实际owned浅clone24拒绝/25通过后才固定checkout depth，不沿用静态候选24。
- runner相对b771仅将ReviewedParent a129→b771。相对基线run a0e8519，其余唯一运行配置差异仍是末尾增加`-DCMAKE_MSVC_DEBUG_INFORMATION_FORMAT=Embedded`及来源身份同步。整个C# Run/原生调用/JobEmpty/30秒预算/救援清理、helper和collector原字节保持。VS生成器/x64/toolset/实例/SDK、编译器识别/ABI探测/链接原样；不切Release、不跳检查、不延时、不增加成员探针。

六文件范围为新workflow、progress、新本说明、新执行来源JSON、新来源测试、入口。三运行版本和全部六文件最终blob/SHA、完整执行SHA/tree/parent由最终交付回执固定。

## 原合同与离线测试口径

AccountingV1旧合同、原workflow和既有日志保持，当前新版本按原门拒绝，不宣称旧门全绿。新增第八workflow也超出旧门冻结世界，因此旧AccountingV1审计可能同时出现新workflow不在旧parent的ERROR；按实际完整结果记录，不把所有拒绝归为运行字节改变。

新独立合同验证完整来源门、真实浅clone、CRLF接受、runtime working/committed drift、错误parent/eventSHA/hashpin、extra/大小写scope拒绝。新合同另对比baseline a0确保仅Embedded+身份改变。原configure合同仍拒绝严格旧命令；171条通过只指owned临时副本精确更新该命令期望后的注入合同，原测试不修改、不删断言，绝不称原合同原样通过。Accounting162/11及旧证据45的既有回执保留，运行源码未变。

## 对照字段、环境与停止边界

只与baseline同组有限回执对照：NativeConfigure Status/Stage/Exit/RootExitConfirmed/PreRescueJobState/BudgetExpired/RescueUsed/JobEmpty/CleanupConfirmed，Accounting FirstWait/LastPreRescue，CollectorSettlement、四文件ReadStatus/Truncated/Facts，以及summary Status/Reason/ExitCode。根进程Exit与脚本/Actions步骤退出码分别列明；原基线分别0与3。两点相同不证明区间内恒定，也不识别成员。

实际CMake版本取四文件允许投影（基线3.31.6）。原版本门仅要求>=3.28，不增加精确版本阻断；若实测漂移，3.31.6静态来源不能作为新环境的精确归因。同时对照GitHub runner image/version、runner软件版本等有限官方基础设施元数据；公开回执未给出的MSVC/toolset精确补丁、VS实例差异保持Unknown，不能仅因前置条件相同就声称工具环境完全一致。无原路径、原命令、原日志/异常文本公开导出。

只一次标准免费public windows-2022 attempt1，成功、失败、超时、取消均停止、不自动重跑。原每operation30秒/救援清理、configure前根租约至采集结束、Python正确跨进程DuplicateHandle、四文件64KiB/256KiB上限和未清理零读取保持；失败诊断不覆盖原结果。唯一终点configure，不build/restore/四真实案例或成员probe。

静态依据只支持ABI编译端Embedded干预，CompilerId独立模板不受此变量直接控制，链接/debug和原MSBUILDDISABLENODEREUSE=1保持。成功不证明两个成员身份或排除环境/时序因素；继续失败也不能排除CompilerId或链接器PDB。原根因Unknown，250/600ms及来源/密码/完整性/取消/清理安全门保持，真实四案例/P0/E06及生产验收仍未通过，ProductionReuse Off、ModelCalls0。

实际来源8项已通过；owned浅clone24拒绝/25通过，CRLF及全部漂移反例实际执行。旧AccountingV1实际5项中2通过、2FAIL、1ERROR（第八workflow不存在于旧冻结父），旧六来源合同4通过2拒绝；旧configure合同after163拒绝原文保留，owned精确新命令期望171通过。首轮来源7通过1ERROR是新fixture漏定义ENTRY，修正常量后8通过，失败日志完整保留。
