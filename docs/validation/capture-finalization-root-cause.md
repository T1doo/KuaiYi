# CaptureHost 测试清理器的原退出观察结算

从已推送浏览器候选 `0a333765061261c4d93befed67f77e2f123eb301`、干净工作区、单 worktree/唯一 root writer接续，正常分支 `dev/core-capture-finalization`。本轮主线只定位 Core 红门，不扩展功能。先给堆栈与候选设计，再独立只读审查、受控复现、最小修复及复审。

## 原始失败与已确认机制

原浏览器回执 r13 与 r24 均完整 Core 2069P/1F/0skip，唯一最终失败为 `CaptureHostTests.KillFailureOrUnconfirmedWaitRetainsOwnerAndBlocksFinalizeWithoutBlockingUi(waitFailure:true)`，分别持续4.1033761和4.1669420秒；原栈都是 `Cleanup:69` 的 `Assert.True(await client.TryFinalizeAsync())`，由原方法 `finally:109` 报出。原 UnitTestResult 文本与摘要在[本轮索引及原件](../test-evidence/2026-10-21-capture-finalization-root-cause.json)，完整旧 TRX 沿用[浏览器候选原件](../test-evidence/2026-10-21-browser-image-preview.json)。旧 finally 可能覆盖 body 异常，因此不能从这条最终栈推断旧运行正文的全部断言通过。

`FailedKill.WaitForExitAsync` 的确切返回 Task 被 `OwnedHostTermination` 保存为原 exitWait。预算超时只结束调用者等待；原观察未结算则不允许确认退出/释放 Process，这是已有生产安全合同。`OwnedProcessSession.TryFinalizeAsync(false)` 等待已完成 stopTask 后检查该原观察，不偷偷另建观察或放宽 pending 门。

旧测试 Cleanup 却另行调用 `child.WaitForExitAsync()`，随后立即要求 finalize=true。第二次 OS 等待结束不能证明更早的原观察 Task 已完成，故此测试清理假设错误。r01仅加原 Task/分阶段诊断，原方法2P；true分支正文完成时原观察确为 WaitingForActivation，但这次清理通过，没有冒称自然复现历史失败。

r02在同一 FailedKill 端口把**真实 child 退出后的原观察返回 Task**受控保持，直接运行原 theory(true)全部正文和同一 Cleanup；实测正文通过、实际 child 已退出、另一次 OS wait 已结束，而原 Task 仍 WaitingForActivation，最终原 finalize Assert.True 失败，0P/1F。外层 finally 释放屏障并等待同一原 Task，未遗留活 child。它确定性证明当前清理器错序机制，不是用泛化 HostTermination 用例代替当前路径。

目标方法没有随机分支或共享可变 static 状态；fixture模式只设置实际 ProcessStartInfo.Environment，不改全局环境。每个 xUnit实例持有自己的starter/child/观察及屏障，浏览器回归也未向该端口写共享状态。受控单路径已足以复现，不需要跨用例污染。自然并行调度可能改变两个退出 Task 的先后，但历史回执未观测其原状态，不能据此证明 r13/r24 的每次根因或排除被覆盖的其他 body 异常。

## 最小修复及验证

仅改 `CaptureHostTests.cs`。测试端口记录原观察 Task；Cleanup 对确有该 Task 的端口按**原4秒边界**显式 join，再保留原 `Assert.True(TryFinalizeAsync())` 与 Dispose。未建立原等待的 kill-failure 分支不伪造等待。生产315份文件与基线逐字节相同；原失败 theory 正文668字节完全不变，没有新增sleep、放宽超时、删除断言、skip或以HasExited替代原观察。

新增一个同路径屏障用例验证该 join，WaitCalls=1。独审发现新hook也不能凭第二次OS等待推断更早RealWait已完；最终hook先有界await确切RealWait，再保留真实等待成功/原观察未完断言，随后释放屏障。原方法分别保存 body/cleanup 异常，双错Aggregate保留两者、单错EDI重抛，不再让 finally 覆盖正文；诊断不会为读取Created另行无界await。

- r02受控红：**0P/1F**，准确源/日志/TRX保全。
- r03修后同目标：**3P/0F/0skip**（原方法2分支+新增1屏障）。包含独审要求的RealWait等待修正，不能说红绿源码只差一行。
- r04最终 HostTermination＋CaptureHost：**35P/0F/0skip**，`--no-build`，测试DLL前后摘要一致；旧HostTermination合同文件不改。独立源码审查无确定P1/P2，测试由writer实际执行。

未盲跑第三次完整 Core，未重跑未受影响的Provider/Codec或App/Worker构建。历史两轮完整Core仍各自Fail；本轮35P不能当完整2071用例已绿。完整P6/P7、Windows桌面/真实OCR/OLE/COM/安装身份、画质、250/600ms/600MiB及旧BMP历史根因不由本测试修复签收。

沿原SPEC退出/取消/原任务结算合同，无生产功能/依赖/锁/许可/权限变化。仅已有合成child与本地fixture，无用户桌面/私人内容/真实模型/CI/新费用凭据/安全网络变化/main/强推/发布部署。
