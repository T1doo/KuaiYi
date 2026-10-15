# Configure-only 首次 Windows 实测结果

2026-10-15，按本轮单次执行授权，精确源码 `4b29c3f95e9f5e5d16dfc501c445a7428fe74f27` / tree `50ceebb928f553640e1027b36799a7ced05392fa` / 唯一 parent `b4592d5d60d38cbbf186a38dbba2b1ae07b0482c` 正常非 force 推送至 `dev/configure-only-diagnostic`。推送前核验单一写入锁、九个 blob、完整 tree/parent、远端祖先和旧文件字节；此执行 SHA 的 workflow 查询仅有一条结果。

[run38690235831](https://github.com/T1doo/KuaiYi/actions/runs/38690235831) / job116274201714 / attempt1 为 `completed/failure`，事件为 push，workflow 为 `windows-configure-only.yml`。来源身份步骤通过；configure/采集步骤失败；后置 checkout 和 job 收尾完成。未自动重跑，未启动其他 workflow。完整固定回执见 [实际证据 JSON](../test-evidence/2026-10-15-configure-only-windows.json)。

## 原结果与独立诊断

前九项来源/工具操作均 Completed、Exit0、无 rescue 且清理确认。NativeConfigure 保持 `Timeout / WaitJobEmpty / FailureKind=Timeout / ErrorCode=None`，根进程 `Exit=0` 且确认退出，救援前 `PreRescueJobState=NonEmpty / BudgetExpired=true`；随后 `RescueUsed=true / JobEmpty=true / CleanupConfirmed=true`。runner 保留 `Fail / OperationTimeout / ExitCode=3`，采集成功没有覆盖原失败。

原 configure 返回并确认清理后，独立 collector 的 `CollectorSettlement=CleanupConfirmed`，有限诊断 `Collected`。四行均为 `Projected / Truncated=false`：

| 固定文件类型 | 实际允许事实 |
| --- | --- |
| CMakeVersion | CMakeVersion=3.31.6 |
| NativeConfigure | ConfigureDone=true；GenerateDone=true；CompilerIdSeen=true；AbiResult=Done；WorkingResult=Skipped |
| CMakeCache | VisualStudio2022=true |
| ConfigureEvents | TryCompileEvent=true |

这些事实说明本次 owned 输出中的完成标记和事件被识别，不能证明救援前 Job 已空，也不能识别残留成员或确认其根因。ABI/try_compile 事实不是目标 NativeBuild 成功证据。旧失败 run38669606027 的四原文件在当前环境仍不可得；本次四文件仅从真实 runner 输出读取，没有用 fixture 补造。未取回四文件原文；本地保存的是固定投影和 GitHub job 解码日志，不是四文件副本。

每文件64KiB、总256KiB是本次未变采集代码及既有测试的上限；回执不含各文件实际读取字节计数，不将上限冒充测量。根租约在 configure 前建立、在有限采集结束后关闭；此次结果通过该受控路径，但不把代码次序声称为新的逐句柄时序遥测。原30秒操作预算、救援/清理语义、job10分钟/来源步骤1分钟/执行步骤7分钟均未改变。

## 退出值、证据与范围

三个层级分别保留：原 CMake 根进程 Exit0、runner 摘要 ExitCode3、Actions 日志实际 `Process completed with exit code 1`。外层1与 GitHub 内置 pwsh 的 `-Command` 包装及 PowerShell 对末尾脚本非0/1退出值的归一化规则一致；这是对 shell 行为的解释，不回填或改写原回执。[GitHub shell 规则](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idstepsshell)和 [PowerShell -Command 规则](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_pwsh?view=powershell-7.5)提供依据。独审使用现有 Linux Blocked 入口做纯 shell 复核，没有运行 Windows/CMake。

完整 connector 响应、run/jobs 元数据及解码日志私有保存在 ignored `artifacts/configure-only-ci-result`。解码 job 日志24,820字节，SHA256 `552b43efcb52871cfacd904c73c629f84743782e8d225470f0033438a3c1d489`；这是 connector 解码后 UTF-8 文本的 hash，不是 GitHub 原 ZIP hash。公开证据仅含固定事实/回执，没有原 CMake 路径、命令、日志正文或异常。归档提取首次因 CRLF 行尾匹配失败，改为逐行匹配后核对成功，原日志字节未变；这不是产品或 Windows 测试失败。

执行前480项本地检查的原始输出和哈希保持，见 [离线证据](../test-evidence/2026-10-15-configure-only-offline.txt)；本次没有把它们重新计为 Windows 断言。原 SDK setter105仍为缺 SDK 的 NotRun。真实 NativeBuild、ManagedRestore、ManagedBuild、FourCases、UIA/Match/Performance 全 NotRun；ProductionReuse Off、ModelCalls0。250/600ms及来源/密码/完整性/取消/清理门未变，P0/E06/普通 Windows11/多屏/安装/生产验收未推进。

剩余缺口是救援前 Job 成员身份及其未及时结束的原因；本次有限事实不能定位到具体进程或组件。没有据此增加猜测性修复、放宽预算或清理门。此次单次执行已消耗；本归档只改证据文档，不改执行源码、workflow 或旧 runner，不触发新 Windows 运行。

归档核验：原诊断闭合 schema 接受本次实际四行；完整日志/两条回执/来源身份/job steps逐字段一致，`git diff --check`通过。独立只读审查通过这三份证据文档，CI结论仍failure；审查回执私有保存。结果文档仅本地提交，远端保持执行SHA，不追加push。
