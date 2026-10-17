# 真实 Shell 关闭任务与一个显式 pending retry

从已推送 `f8b22ce31c8244a4597d6f9ca803649b87b5a038` 正常建立 `dev/app-close-pending-retry`。按父独审要求先修实际接线，暂停后续安装功能定位；不新增 observer、服务、长期守护或安装功能。

## 对父片证据的明确勘误

父片 r18→r19 的容量1 lane 仅在 `InstallationAppControlFixture`，真实 `ShellWindow.ClosingShell` 仍 `if(exiting)return`。所以父片 fixture 的重试绿证据不能证明真实 App 在清理等待中收到 STOP2、随后 STOP1 清理失败时会重试。本片保留父报告/1549原件原字节，不回写历史；本勘误限定该行为的旧结论。父片 Windows/WPF 验收仍为 NotRun。

## 生产接线与任务责任

`ClosingShell` 从 ShellWindow 原方法抽到生产 `ShellCloseFlow.cs`。现在真实所有关闭入口仍进入同一 WPF Closing handler；退出成功的 `allowClose` 递归直接放行，opening/settings 仍沿原 `exitRequested` 延迟关闭。清理等待期间的 Close 不再因 `exiting` 丢弃。

每个 Shell 的 `ShellCloseRequests` 只保存同一个原 attempt Task 和一个 pending bool。先发布真实原 Task，再执行调用线程的 admitted 回调：原 QueuePositionSave、立即 exiting/禁用按钮及同步 lifecycle.Hide。位置保存/Hide的异常按同一次attempt保留到原SettleExitContent，继续结算其余任务后汇总，不被后续Hide成功覆盖。因此同步重入也只能记一个 pending。启动新 attempt 的 admitted 回调若直接抛错，异常归入该原 Task，并恢复可操作窗口；原异常仍可观察。pending 时 exiting 分支直接返回，不重新初始化正在关闭的状态。清理实际委托及 Dispatcher operation 的 Task 由原 attempt await，不以关闭消息或自报代替任务完成。

原 Task 真正完成后，continuation 才能消费一个已收到的显式 pending；多次等待中请求合并为一次。失败而无新请求不会自动重试；成功关闭丢弃 pending。没有 idle worker/Channel 循环，不维护无界任务队列。TryCloseAttemptAsync 仍执行原 SettleExitContent：内容清理失败保留窗口、控制循环与所有权，成功后才结算安装控制；失败恢复 exiting/button。真实 Dispatcher delegate 用 Unwrap 持有异步 attempt，安装 STOP callback 只 await dispatch，避免 control loop 等待自己的 shutdown。

fixture 删除独有容量1实现，csproj 直接链接这两个生产文件。新增 InstallationShellPort 仅代替 HWND/STA/Dispatcher/设置窗口表面，实际 Closing handler 和任务/重试算法不另写一份。原 Core client/server、实际 Start/exit/IO、owner 与 package 校验仍执行。明确不把线程池端口称作 WPF Dispatcher 实测。

## 实际反例与验证

- r01：真实选定合成 child 链接父 ClosingShell 原方法字节，等待清理时 STOP2 已收到；释放后读取失败/CLOSED序列超时，实际1F；该行两次读取未独立分轨，finally仅清理本次真实 Start 返回 child，Exit137/原IO结算。
- r02：中间构建 CS0414（fixture字段只赋值未读取），无TRX，不算行为红绿。
- r03：增加 WAIT_CLEANUP 明确屏障后，再次链接父原方法字节；确认第一次 cleanup 已进入/未完成、第二 STOP 已到达，释放后读取失败/CLOSED序列超时，实际1F；TRX该行包含两次读取，不能独立判断是哪一次Row超时。最终新方法与本轮字节完全相同。红测目标是执行原生产 partial 的实际 child，不声称本轮运行或验证了 Windows App 二进制。
- r04：共享生产 partial/原任务候选安装108P；随后收紧 owner发布后立即 exiting 的 admitted 回调，所以这是中间结果。
- r05：生产 ShellCloseRequests 直接链接测试5P，核32请求最多一次重试、无并行cleanup、原Task真实完成后才下一次、成功丢 pending、同步 admitted 重入先见owner、fault原Task保持、admitted异常0cleanup且无自动重试。

- r06：admitted先立exiting版相关108P，仍是同步Hide修正前中间结果。
- r07：直接链接真实ClosingShell与RuntimeGate，扣住Dispatcher attempt尚不执行，实际2F（期望Off、实际On），证明仅exiting不足。
- r08：同两例方法字节绿，加原管理器5例共7P；在Close返回前Off/取消、TryBeginSend callback0，取消callback故障不吞、继续真实content cleanup后仍失败保留窗口。该测试是canonical send gate和WPF调用端口，未启动HTTP或真实WPF。

最终安装、Core/Provider完整回归、App/Worker交叉构建、源码与实际child DLL绑定、父文件保全、独审见[机器索引与原件](../test-evidence/2026-10-17-app-close-pending-retry.json)。r09最终相关108P；r10完整Core1810P=父1809全名多重集+1；r11完整Provider2332P=父2325全名多重集+7。旧断言不删除、不降低。r12/r13 App/Worker实际交叉构建各0W/E；五轮最终各63源、共315绑定相同，23目标DLL与实际child执行另核。

## 保留边界

本片修生产接线并以同源端口复现，实际 Windows 可见窗口、Dispatcher/STA、设置窗口退出、原生 Worker/OCR/Job、真实键盘、DPAPI/ACL、物理250/600ms与模型仍 NotRun。普通 installed/committed 自退的持久 owner 拒入继续保留，没有退出观察者或 stale 接管，也不凭PID/EOF/锁释放清记录。

未推进安装器调用入口、完整payload、身份/NativeHost注册及回退、Inno/自启/卸载或完整P7/K组。历史图片/idle/Core/Job及父r17/r20原因各仍Unknown，重跑通过不等于根因修复。生产图片/分类复用Off，来源/密码/完整性/取消/清理门不变。无main/强推/部署/费用/真实模型/凭据权限改变或新依赖许可；Skia4.153.1四包待批，source403及拒绝Actions不查询不绕过，未知锁/数据/日志/进程保全。
