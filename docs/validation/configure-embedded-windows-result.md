# EmbeddedExecV1 唯一 configure-only 对照结果

[运行38719840129](https://github.com/T1doo/KuaiYi/actions/runs/38719840129)，job116371397705，attempt1，最终 **failure**。只推送一次、只一个run/job，不重跑；标准windows-2022，来源1分钟/执行7分钟/job10分钟。来源门通过，实际job10:22:50Z—10:23:42Z。

执行SHA `5acccc75352499e0b0193cebb765584b208c6fa9`，tree `74a77d7b7e0b9ec3fffe63f89b9efec5fd9cf921`，唯一父 `b771f1e8442d453b3df65144f006f694a2b7a294`。分支dev/configure-embedded-windows，仅路径.github/workflows/windows-configure-embedded.yml的push触发；固定checkout action与完整25节点祖先。提交后真实浅clone24拒绝/25通过，六文件精确freeze及三runtime blob/SHA独审通过。

## 与基线的同组字段对照

基线为[38716240995](https://github.com/T1doo/KuaiYi/actions/runs/38716240995)，执行a0e8519e63d90e44560316478dc65358a18a8868。新runner相对该基线唯一运行配置变化是末尾Embedded参数和精确ReviewedParent同步；完整原C# Run、JobEmpty查询/30秒预算、救援清理、lease/collector/helper保持。七旧workflow和AccountingV1合同不改；新workflow/来源门/runner精确差异见[执行准备](configure-embedded-execution.md)，固定版本见[执行来源合同](../test-evidence/2026-10-15-configure-embedded-execution-source.json)。

| 有限字段 | 基线 | Embedded |
| --- | --- | --- |
| NativeConfigure Status / Stage | Timeout / WaitJobEmpty | Timeout / WaitJobEmpty |
| **根进程退出码** | **0** | **0** |
| **脚本summary退出码** | **3** | **3** |
| **Actions步骤实际退出码** | **3** | **3** |
| RootExitConfirmed | true | true |
| PreRescueJobState | NonEmpty | NonEmpty |
| BudgetExpired / RescueUsed | true / true | true / true |
| 最终JobEmpty / CleanupConfirmed | true / true | true / true |
| FirstWait State / Active / Total | Known / 2 / 33 | Known / 2 / 33 |
| LastPreRescue State / Active / Total | Known / 2 / 33 | Known / 2 / 33 |
| CollectorSettlement | CleanupConfirmed | CleanupConfirmed |
| DiagnosticStatus / 四文件 | Collected / 全Projected，无截断 | Collected / 全Projected，无截断 |
| CMakeVersion允许投影 | 3.31.6 | 3.31.6 |
| 原summary Status / Reason | Fail / OperationTimeout | Fail / OperationTimeout |

四文件Facts也完全相同：CMakeVersion3.31.6；NativeConfigure ConfigureDone/GenerateDone/CompilerIdSeen true、AbiResult Done、WorkingResult Skipped；CMakeCache VisualStudio2022 true；ConfigureEvents TryCompileEvent true。根进程正常退出及这些成功日志事实均不等于Job生命周期通过。九个前置操作均Completed/Exit0/清理确认；NativeConfigure原失败和整体Exit3未被诊断覆盖。

## 明确存在runner image漂移

| 官方基础设施有限元数据 | 基线 | Embedded |
| --- | --- | --- |
| Runner软件 | 2.337.0 | 2.337.0 |
| Runner image标签 | windows-2022 | windows-2022 |
| **Runner image版本** | **20261012.333.1** | **20261004.326.1** |
| **Image provisioner版本** | **20261012.601** | **20261002.596** |

两次CMake实测投影相同，但image/provisioner不同；不能声称工具环境完全一致。原前置约束均通过，VS/MSVC具体补丁、VS实例和最终编译/链接参数未由公开投影给出，保持NotObserved/Unknown。不能仅凭windows-2022标签与版本前置通过就掩盖该漂移。

## 能得出的结论与仍未证明部分

在这一轮存在环境漂移的单次对照中，Embedded配置未消除同一组可见超时症状。静态3.31.6依据支持其影响ABI try_compile编译端，但本有限采集未直接证明最终编译命令实际/Z7，也没有识别两个Job关联成员。Active不是正在运行子进程数，Total含已终止，两端相同不证明中间不变。

不能据此排除CompilerId独立模板、链接器PDB或其他保留关联来源，也不能宣称PDB假设彻底证伪/根因已确定。/DEBUG与原MSBUILDDISABLENODEREUSE=1保持。继续失败后已按授权停止，不增加探针、不改变预算、不再次配置。

完整GitHub工具响应和解码日志仅私有保全；本次解码UTF8日志28235字节，SHA256 `a8eb2955fbb9a34e1629e3aca1a83dd29383da6aebdce1e8b810d975f92427cd`，基线日志哈希原样保持。公开[固定实测证据](../test-evidence/2026-10-15-configure-embedded-windows-result.json)只含闭合字段与允许基础设施版本，不导出原路径/命令/日志/异常。

新来源独立8项通过，旧AccountingV1实际2pass/2FAIL/1ERROR、旧六来源4pass/2拒绝和旧configure after163拒绝均保留；171通过仅是owned临时副本精确更新命令期望后的注入结果，不能称原合同原样通过。七旧workflow/历史证据保持，结果归档只本地提交，不再次推送。

未执行build/restore/四真实案例或成员探针。原四案例、P0/E06和生产验收仍未通过；原根因Unknown，250/600ms及来源/密码/完整性/取消/清理安全门保持，ProductionReuse Off，ModelCalls0。main及原诊断远端分支不变，无强推或部署。
