# 旧 idle 超时失败：现有原件只读调查

历史根因仍 **Unknown**。本调查只读取现有源码、TRX、日志及 DLL 观察，不启动额外 idle 诊断轮次，不改旧测试/PhaseClock/Provider，也不把重复绿色称为修复。另片 HeldContext 测试清理的完整 Provider 回归包含旧用例，但该执行属于夹具改动回归，不能补造旧失败时的因果轨迹。

## 原件能够证明的事实

目标是 `PhaseDeadlineTests.IdleDeadlineKeepsPartialTextAndIgnoresNoContentActivity(protocol: AnthropicMessages, activity: "empty")`。

| 既有执行 | 全轮结果 | 目标 UTC 开始→结束 | TRX duration | 目标结果 | 重叠的其他用例窗口 |
| --- | --- | --- | --- | --- | --- |
| budget r02 | 2275P1F | 10:57:45.4613034→45.9948785 | 529.4585ms | Fail | 84 |
| budget r03 --no-build | 旧 idle 族6P | 10:58:07.0766040→07.2473176 | 162.2439ms | Pass | 0 |
| budget r04 --no-build | 2276P | 10:58:32.7513679→32.7936508 | 35.7654ms | Pass | 5 |

日期均为 2026-10-17。duration 是 TRX 记录值，start/end 墙钟差分别 533.575/170.713/42.283ms，不能混算。重叠数排除目标自身，包含自身分别为85/1/6；窗口重叠只是相关性，不能据此断言 CPU/线程池/GC 或排程根因。新预算四例不与该失败窗口重叠；r02/r04 此目标均为该类第三项、idle 族第一项，没有已证实的顺序变化。

实际失败位于 `Rig.AssertTimedOut` 内：fake clock advance 后等真实500ms，`Job.IsCompleted` 仍 false。随后的 READ_IDLE_TIMEOUT、PUBLIC 正文和 Requests1 断言均未到达，不能声称失败当时已验证部分正文或请求数。Dispose 随后 disable/dispose 并等原 Job6s；TRX 没有另一个清理错误，但也没有详细清理轨迹。

四次预算执行实际五个 Provider 目标输出 DLL SHA 完全相同，成员是 **Core / ImageWorker / Imaging / Provider.Tests / Providers**。旧 analysis.json 的 `sameDllObservation` 文字曾误列 NativeHost，实际摘要映射正确。本调查追加勘误，旧原件及其 SHA 不改；这不影响五项摘要相等的结论。相同 DLL 有过失败与通过，不能推出历史失败已修复。

当前 PhaseDeadlineTests/PhaseClock/PhaseHttpFixture/ProviderClient/ProviderPhaseDeadline/ProtocolAdapters/SseReader 七源文件与 c775 原字节相同。证据链、各时刻、失败堆栈、原件 SHA 与勘误见[独立调查索引](../test-evidence/2026-10-17-idle-timeout-readonly-investigation.json)。

## 源码支持的可达时序，尚非历史根因

`PhaseHttpFixture.Script.Write` 的 ack 只证明服务端 flush；`ModelEvent.Started` 只证明响应头已接受，均不证明客户端已处理正文。Anthropic `message_start` 被标为有效事件，会启动 idle 计时。`WaitDeadline(2)` 只找任意剩余2秒定时器，没有绑定到 PUBLIC。正常 Dispose 会把 Due 设为 long.MaxValue，**不能说 disposed timer 仍匹配2秒**。

一个可达安排：fake0 客户端处理 message_start 并创建有效 idle2，此时尚未消费 PUBLIC；测试 WaitDeadline2 因该活跃定时器返回并 advance 到fake1；客户端随后在fake1处理 PUBLIC，将真正 idle 截止延至fake3；空 delta 按现有代码不是有效事件，不重置计时；测试只再推进到fake2便要求完成。生产此时尚未过期，Job 未完成可以符合其正确行为。这个安排解释失败形状，旧原件却没有 timer identity/Due、last-effective、逐事件时间或运行时栈，无法证明当次发生了它。其他排程延迟亦未排除。

最小后续候选是仅在 Rig 入队实际 PUBLIC TextDelta 后发完成信号，测试先 await 该信号再首次推进 fake clock；另用分段 message_start→受控延迟 PUBLIC 证明锚点。保留原2秒/500ms和生产代码，避免先泛改整个 PhaseClock。该候选**尚未实施，待父另行确认范围**，本片未以此解释生产图片间歇失败。

## 独立的用户功能缺口建议

优先较小片：**未发送截图的“本次风格”选择**。现 `OverlayImageSource.SelectedNextGeneration` 对图片返回 null，`AddSessionStyleMenu` 因此禁用；`ScreenshotTranslationFlow` 只有 Automatic/TranslateText/ExplainImage 三种模式。Coordinator 冻结真实保存设置，Planner 的 TranslateText 使用全局 Preset，结构图自动路由到 Explain。SPEC9.9 要求浮窗会话风格和生成前显示风格，当前文字链有、图片未发送链没有独立选择。

建议只对尚未发送的保留截图添加 source 绑定的 requested preset，offer/确认文本冻结并显示，合成该次请求时应用；真实保存配置及 profile/secret binding 不伪造、不改变全局默认。Automatic 的结构图仍 Explain，模式语义保留。用两协议×四风格真实回环检查实际 prompt/label/template；拒绝确认、Off、来源/设置变化均0HTTP；Clear/新来源清掉 override；已消费图仍不得重发。它无需新解码、Skia 或真实模型；Windows 可见性仍另验。本轮未扩功能。

第二个更大片是 P7-04/05 安装升级事务：traceability 仍 NotStarted/NotRun，`packaging/text-alpha.json` 的 installer=false。现有 staging/probe/身份脚本不等于完整每用户安装器。可另行做清单/摘要/原子版本切换/失败回滚及保留加密设置的真实临时文件系统测试，再单独取得 WindowsVM/MSIX/注册/签名/卸载证据；本轮未实施或抬状态。

通用图片文件/Drop/PNGJPEGWebPGIF 解码仍受 Skia 四包许可待批及403限制；同图重新生成/结果后模式切换仍受生产复用Off约束，不能借上述建议开启。真实 Windows/WPF、物理250/600ms和模型质量验收仍未执行；不查询已拒 Actions，不改变进程/权限/凭据或环境。
