# Configure-only 外层退出码本地补正

基于本地证据归档 `aa965555ca681161e334a13540e4c7c906c94b43`，在本地开发分支 `dev/configure-shell-exit-local` 补正已确认的 shell 退出码传递问题。原执行 `4b29c3f95e9f5e5d16dfc501c445a7428fe74f27` / run38690235831 / attempt1 的历史回执保持：NativeConfigure 根进程 Exit0、runner Fail/OperationTimeout/ExitCode3、Actions 外层进程1。

## 变更与依据

仅 configure/采集步骤将内置 `shell: pwsh` 改为官方支持的 custom shell `pwsh -NoLogo -NoProfile -NonInteractive -File "{0}"`，并显式设置 `$ErrorActionPreference='Stop'`。原步骤正文继续调用原入口并 `exit $LASTEXITCODE`，在脚本自身明确停止错误行为和退出码传递。当前官方 runner 的内容修整函数按解析后的 shellCommand=pwsh 处理，也可能对 custom template 注入首尾语句；测试同时验证带该修整和不依赖该修整的正文，不声称 custom template 必然取消全部注入。

[GitHub shell 文档](https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#jobsjob_idstepsshell)说明内置 pwsh 使用 `-Command` 点调用临时脚本，也支持 `pwsh -File {0}`；[PowerShell 文档](https://learn.microsoft.com/en-us/powershell/module/microsoft.powershell.core/about/about_pwsh?view=powershell-7.5)说明 `-Command` 与 `-File` 对脚本显式退出码的不同处理。此次实际 Linux pwsh 进程复现内置包装将3映射为1；修正后0/1/2/3均保留。只对这层退出码问题作补正，不回写原 Windows 实测结果。

原 runner、LifecycleOperation、lease、采集器及四个旧 workflow 共12个文件与基线逐字节一致。来源步骤、审定 parent/tree、depth12、contents:read、windows-2022、job10分钟/来源步骤1分钟/执行步骤7分钟和每操作30秒保持。生产源码、250/600ms及来源/密码/完整性/取消/清理安全门保持。

## 实际本地验证

新增9项测试使用已存在 PowerShell7.5.3真实进程，提取当前 workflow 的执行步骤正文和 custom shell 模板，按照 GitHub runner 的格式化参数字符串，经真实 `.NET ProcessStartInfo.Arguments` 启动子 pwsh，使用含空格/单引号的 owned 临时路径：

- 复现内置包装3→1，同时保持合成摘要ExitCode3。
- custom shell 分别保留0/1/2/3。
- 未给占位路径加双引号的参数字符串真实退出64；加引号后返回原退出码。
- 合成 cleanup=false/Fail 仍保留原摘要并返回3。
- `Write-Error` 和 `throw` 均失败为1，后续标记不执行。
- 不添加 runner 首尾修整时，显式正文仍保留失败3。
- 实际 Linux入口只返回Blocked/HostedWindowsRequired及2，Operations为空、collector未开始、构建/四case未运行。
- workflow安全边界和原来源锁未变；当前本地后代不匹配已冻结来源锁，不能直接作为下一次Windows候选。

原6项来源/引用次序/旧文件一致性检查及171项注入失败隔离断言复核通过。已有PyYAML解析通过、`git diff --check`通过。原始输出保存在 ignored `artifacts/configure-shell-exit-local`，固定证据见[本地检查记录](../test-evidence/2026-10-15-configure-shell-exit-local.txt)。没有运行CMake、Windows原生调用、构建或四生命周期，也未重新计算旧Windows fixture检查数。

首版7项测试用 argv 列表启动子进程，独审指出这会绕过 runner 参数字符串解析，不能证明模板的空格路径行为。该版输出保留为 `shell-exit-argv-only-before-review.log`；随后给占位路径加双引号、改用真实参数字符串启动并补两个反例，最终9项全部通过。[官方 ScriptHandler](https://github.com/actions/runner/blob/main/src/Runner.Worker/Handlers/ScriptHandler.cs)的参数格式化及[内容修整函数](https://github.com/actions/runner/blob/main/src/Runner.Worker/Handlers/ScriptHandlerHelpers.cs)是本地复核依据；不是 Windows 实测。

## 原超时的剩余缺口

原代码先等待根进程，再通过 Job accounting 查询等待 Active归零。实际失败回执在WaitJobEmpty阶段记录NonEmpty和预算到期，随后救援/清理确认；有限四文件事实记录configure/generate标记。当前回执没有救援前Job成员身份，无法从现有材料追溯具体成员或确定其持活原因。外层退出码补正不会让该等待提前结束，也不构成原超时修复。

本轮仅本地实现、测试和审查。来源锁仍属于已执行的4b29候选，不将此变更称为已冻结的下一运行方案。若继续准备现场成员诊断，必须单独形成有界、有限输出及不影响原救援/清理的可审查实现；本轮未新增进程扫描或成员查询。远端保持原执行SHA，没有push、新CI或重跑，P0/E06及生产验收未推进。
