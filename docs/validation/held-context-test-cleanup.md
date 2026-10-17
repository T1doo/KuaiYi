# HeldContext 测试失败路径的有限清理

从已推送 `2219c3b10d69c31af6d8726744b32b042b9125b5` 正常建立 `dev/held-context-test-cleanup`。仅修改 `ScreenshotRecaptureRecoveryTests.cs`，落实父明确授权的测试夹具残留；未改生产代码、旧 idle 测试或时钟。默认远端/HEAD/单 worktree 和父 4800 路径摘要见 preflight/parent-manifest 原件。

原测试在 `Task.Yield` 被 HeldContext 持住后，若手动 drain 前的断言失败，会直接进入 Rig.Dispose，原截图任务可能仍等队列。现 `WithHeldCapture` 内层 finally 恢复原 SynchronizationContext 并取得实际原任务，外层 finally 无条件 drain，嵌套 finally 最多 6 秒 await 同一任务。原 guard 的十段断言保留。没有以新任务代替原任务、超时当成功或降低原断言。

新增两个受控分支在实际任务尚未完成时触发真正的 xUnit 前置断言失败，分别保留原显示 revision / 实际选择版本改变 revision。测试捕获并核验原失败标记，证明失败没有被清理错误替换，原任务已成功结算、Capture suspension 已释放；native 合成端口分别调用 1/0 次，A 的正文、identity、原图和原两次 HTTP 保持。正常分支仍验证原 guard。实际 Shell 菜单/managed 捕获协调及本地 Kestrel SSE 沿用父夹具，native/WPF 与菜单表面仍是显式合成端口。

r01 编译因新测试使用常量 `Assert.True(false)` 被已有 xUnit2020 拒绝，0 测试、无 TRX；只把受控失败改成此前已验证为 false 的实际 `task.IsCompleted`，无 analyzer 豁免。r01 所有源快照/日志保留，不能称产品红测。本片没有生产红绿声明。

r02 最终专项 **3P/0F/0skip**；r03 一次无 filter 完整 Provider **2300P/0F/0skip**，全名多重集精确为父 2298 加新两例，没有删除。两次最终执行各 32 份源码绑定，共 64/64 与当前源码及原件一致；各五个 Provider 目标输出 DLL 摘要一致。run.py 也记录其他预存 DLL，只有命令所指目标闭包实际执行。Core1703 与 App/Worker 各0W/E 引用未变父闭包的 r08/r09/r10 原件，**本片未重跑或重建这些目标**。

独立只读 high_water_review 源码/测试审查无 P1/P2；最终封存包审计另附 receipt。完整命令、TRX、日志、执行时刻、源码快照、DLL 摘要和封存脚本见[证据索引](../test-evidence/2026-10-17-held-context-test-cleanup.json)。父原件 499+130+374=1003 份保持原字节，父 4800 路径除本测试、progress、gitattributes 三项外其余 4797 原字节不变；无删除。

[旧 idle 失败只读调查](idle-timeout-readonly-investigation.md)单独记录，生产根因仍 Unknown，完整回归绿色不等于修复它。历史图片间歇、Core 当次结算、Windows Job 各独立 Unknown；生产图片/分类复用仍 Off。Windows/WPF 可见键盘/焦点/真实裁剪/锁屏/DPAPIACL/物理250/600ms/真实模型质量及完整 P6 未验。

只用已有 SDK10.0.401 和 --no-restore，激活前后禁生成证书。无新包/许可/Skia 安装、403 或 Actions 先前拒绝重查绕过、main/强推/部署/费用/真实秘密或安全权限变化；未知锁/数据/日志/进程保全。
