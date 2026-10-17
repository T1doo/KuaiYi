# 诊断工具 Dispose：真实结算与有界清理

父有限准入 `04d6831cd874c13a83ac0df4df3bc6842c3547ee`，正常开发分支 `dev/diagnostic-dispose-settlement`；初始实际 HEAD/默认远端/main、单 worktree/唯一 root 写入者已核，high_water_review 仅只读独审。本次只修已有诊断工具，不扩产品功能；原 retained-source 高水位与后续已接受代码保持。

父批 display-save 的 r06 是实际完整 Core **1687P1F**，旧 `Dispose_during_active_request_retires_and_settles_actual_child` 报 `Owned child or IO settlement unconfirmed; ownership retained.`；r14 同 Core.Tests/Core/NativeHost DLL 完整1688P，诊断 DLL 后读摘要亦同。历史失败原件仍保存在 `2026-10-17-display-save-originals__r06-*`，没有追踪到当次哪个结算条件未满足，故不能以复测或本次实验宣称那次根因已确定。它与 Windows Job 未清空问题没有同根因实证。

## 可控反例与修复

`ReuseDiagnosticHost.DisposeAsync` 原先等待 running 后只调用一次 `TrySettleAsync`；此时子进程可以已经真实退出，但 stderr drain、stdout late IO 或退出等待仍持有未完成任务。单次 false 即抛异常，调用方无法在同一 Dispose 等待这些既有任务正常结算。迟到 startup 亦有同样问题。

复用现有实际 owned 托管合成子进程与端口，分别持住 stderr、stdout、exit-wait、startup 四个明确 gate；先等 running 完成。非 startup 例另核真实子进程已退出、owner ExitConfirmed；startup 例未创建子进程。四例父生产原字节实际 **4F**：gate 未释放，原 Dispose 已 fault 完成。只换最小修复后，同测试源字节/同四名 **4P**，释放 gate 后同一次 Dispose 成功。Core DLL 相同；Core.Tests 与诊断 DLL 有变化，不称同测试二进制。

工具仅改 `ReuseDiagnosticHost.cs`：在 running 完成后启动独立 **2秒清理预算**，join 原先跟踪的 IO 任务；晚到 startup 成功时启动并 join stderr drain；待 startup/stop/真实退出与所有 IO 完成，再走原 finalization/清零/summary/所有权释放条件。退出观察先于 finalization，避免每轮叠加原 stop 的2秒等待；settlement semaphore 也用同一截止。Task.WaitAsync 超时不会取消或替换原 IO，任务引用及 owner 留存；异常/超时不发布成功，保留原 IOException 消息与 inner exception，后续可在同一 host 真正结算后重试 Dispose。非超时的原 IO fault 原样传播，不能当成功或清除 fault 记录。

这2秒从 **running 完成后** 开始，不是整个 Dispose 的全局上限。现有 `DiagnosticCaseRunner.Registry.SettleAsync` 的外层重试/留守行为未改，不能声称整个 runner 有了全局退出上限。250/600ms 请求门、协议、来源/密码/完整性/身份/取消条件、生产 Core/native/Job 均未改；生产图片复用仍 Off。

## 最终覆盖与实际回归

最终新增15例：四种 gate 的同次等待4例、超过预算保留 owner 后同 host 重试4例、真实退出与报告 ExitConfirmed 独立2例、settlement semaphore 预算1例、并发/重复 Dispose1例、faulted late IO 不伪成功1例、启动前 Dispose/预取消无 child2例。检查实际退出、buffers 清零、Summary/CanReplace/ownership 与无新 child；永久持门不得提前释放，fault 不抹去。

fault 负例只有在实际退出、原 faulty IO 已完成和清零后，由夹具直接关闭已持有 session 的句柄；Host 仍无 Summary/CanReplace，不冒称成功 Dispose。独审指出该 finally 对任意更早断言失败的清理健壮性未全面证明，保留为非阻断测试夹具边界；正常 fault 路径已经实测通过。此类合成端口/托管 child 不能充当 Windows UIA/COM/native Job 证据。

| 原件 | 实际结果 |
|---|---|
| r01-parent-red → r02-green | 父工具原字节，同四名/测试源 **4F→4P**；精确源快照、执行 DLL 哈希、log/TRX 保全。 |
| r03-focused | 中间候选15P；随后仅测试名称/故障任务状态断言/并发 finally 观察完善，最终15例以 r05/r06 为准，不能称 r03 最终测试字节。 |
| r04-related | 命令生成时未引用 filter 中的 `\|`，shell exit127，无权威 TRX；首个 dotnet 分支是否执行不可从现有输出确认。不计零执行、产品失败或通过，原命令及输出保全；修正命令为引用后的 r05。 |
| r05-related | **66P、0失败/跳过**＝最终新增15＋原 SelectionReuseHost/HostTermination 51；原 active Dispose 用例通过，原文件字节未改。 |
| r06-core-full | 无筛选完整 **1703P、0失败/跳过**＝父 r14 的1688结果名多重集＋最终新增15，原 active Dispose 通过。此 r06 是本批新前缀，勿混历史失败 r06。 |
| r07-provider-full | 无筛选完整 **2212P、0失败/跳过**，与父 r28 全名多重集完全一致。 |
| r08-app-build / r09-worker-build | Release --no-restore 交叉构建，各 **0警告/0错误**。 |

独立源码、测试及 TRX/DLL 审查未发现 P1/P2；最终封存另有终审回执。每次最终运行源绑定一致；原件与索引见[固定证据](../test-evidence/2026-10-17-diagnostic-dispose-settlement.json)。所有旧测试、SPEC/原产品书、traceability及全部旧证据保持原字节，追踪状态不抬。

历史图片间歇根因、历史 Core r06 当次具体结算条件和 Windows Job 未清空根因仍 Unknown；真实 WPF/键盘/Windows/COM/模型/物理250/600ms与完整 SPEC 验收未执行。Skia4.153.1四包许可待批、源码403及 GitHub Actions 先前拒绝不查不绕。仅已有 SDK、全部 --no-restore、激活前后禁 ASP.NET 证书生成；无新包许可环境、main/强推/部署/费用/凭据安全权限或真实模型，未知锁/数据/日志/进程保全。
