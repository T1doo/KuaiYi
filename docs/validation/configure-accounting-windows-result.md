# AccountingV1 唯一 Windows configure-only 实测结果

执行提交 `a0e8519e63d90e44560316478dc65358a18a8868`，tree `712d5a9f67884b14d497cff41620552cb31da66a`，唯一父 `9d75368aa246b40e2bfc9517eb390384c6faf470`。六文件最终冻结经过独立只读审查；真实已提交版本的 owned shallow clone depth21 拒绝、depth22 完整来源门通过。源代码版本与准备证据保持，旧合同及全部旧日志原样保留。

按本阶段授权，普通推送新开发分支一次，触发[唯一运行 38716240995](https://github.com/T1doo/KuaiYi/actions/runs/38716240995)，job `116359501515`，attempt1，标准 `windows-2022`。同SHA仅1个run/1个job。来源门成功；运行最终 failure，没有重跑、安装、build/restore、四生命周期、成员探针或真实模型调用。来源1分钟/执行7分钟/job10分钟限制保持；实际job为09:53:15Z—09:54:09Z。

## 原始结果与有限诊断

九项前置操作均 Completed/Exit0/清理确认。NativeConfigure仍为 Timeout，Stage WaitJobEmpty，root Exit0且退出确认，PreRescueJobState NonEmpty，BudgetExpired true，RescueUsed true；最终 JobEmpty true、CleanupConfirmed true。整体原summary为 Fail/OperationTimeout/ExitCode3，Actions步骤实际也收到exit3。新的Accounting输出与采集未覆盖这个结果。

| 原有查询快照 | State | ActiveProcesses | TotalProcesses |
| --- | --- | --- | --- |
| FirstWait | Known | 2 | 33 |
| LastPreRescue | Known | 2 | 33 |

这两个点只来自root退出后原等待分支的既有成功Job Accounting查询，清理查询不回写。ActiveProcesses是当前Job关联计数，不能称“两个仍在运行的子进程”；TotalProcesses包括已终止进程，不能将33减2推断全部退出数。没有PID、映像类别或身份证据，两个点相同也不能证明整个间隔内计数恒定；原configure根因仍Unknown。

原救援/清理确认后完成有界读取，CollectorSettlement CleanupConfirmed，DiagnosticStatus Collected；四文件均为真实 Projected、Truncated false，没有Missing补造：

| 允许文件类别 | 允许事实 |
| --- | --- |
| CMakeVersion | 3.31.6 |
| NativeConfigure | ConfigureDone/GenerateDone/CompilerIdSeen true，AbiResult Done，WorkingResult Skipped |
| CMakeCache | VisualStudio2022 true |
| ConfigureEvents | TryCompileEvent true |

公开证据仅有闭合投影，不导出原路径、命令、原始日志或异常。每文件64KiB/总256KiB、configure前建立并持有至采集结束的根租约、Python正确跨进程DuplicateHandle、原30秒operation预算及救援/清理语义保持。这些投影证明存在对应有限事实，不构成原生命周期通过或根因结论。

## 证据保全与交付边界

完整GitHub工具响应和解码日志仅存私有artifacts；解码UTF8日志28213字节，SHA256 `49ad10ff2d6e29e51893a23229b6266ba6072145aed20761c457c1cfaa76be77`。完整原summary/两点Accounting/四文件投影见[固定实测证据](../test-evidence/2026-10-15-configure-accounting-windows-result.json)。本地离线测试162断言/11场景、configure171、旧证据45及新来源5项均通过；旧来源六项4通过2版本拒绝，拒绝完整保留，不冒称全绿。

本结果归档仅本地新分支提交，不再次推送。main及原诊断远端分支未改，没有强推、部署、新服务/权限/凭据/费用或真实模型。250/600ms与来源/密码/完整性/取消/清理安全门保持；原CMake/四生命周期未修复、P0/E06及生产验收未通过，ProductionReuse Off，ModelCalls0。
