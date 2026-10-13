# 祖先优先与按需 BFS：有限 SDK COM 候选优化

起点为 `98c1711ec44947d9d4bb4ea7de179d9f1f9fa74c`。上一 SDK 六场景执行 SHA `670d2a6020deefdaaf4cbf9018a74694dbdbc2b4`，run38391659923/job115276674098，Observed 5/6，Chrome HTML 为 SoftTimeout；该失败与旧四个 AV、两个硬超时完整保留。

## 实际实现

原实现先收集已绑定原 HWND 根的焦点/命中祖先，再完成最多64候选BFS，最后才评估任何文本。现在将同一个原候选评估体抽为lambda，真实生产用 `CandidateOrder.h` 调度：按焦点路径、命中路径原序先评估祖先；只有全为非终态空/unsupported且预算尚余时才创建BFS队列，从原root开始逐节点先评估、无结果再展开。任何Success/Sensitive/Stale/TooLarge/SoftTimeout等终态立即返回；祖先成功零BFS provider调用，64额度耗尽不调用expand。

`bound_ancestors` 是生产使用的同一函数：每路径最多8层，PID必须匹配，必须到达原root才整条admit，部分路径、跨PID、同PID不同root、9层root均不admit。两阶段共用原capture时钟origin、64候选发现池与同一evaluator，无重试、预热、换序或跨请求UIA缓存。ranges64、rawrect256、UTF16 100002哨兵和50000标量仍按原每候选的同一个GetSelection批次累计，不在range/阶段切换处重置。

原排除/高完整性门在CoCreateInstance和取词前；焦点/命中密码在祖先评估前，每候选密码在pattern/文本前。前台原HWND/PID、成功前held source identity、自动几何、有限text/Unicode、空文本copy metadata、原最终source复核保留。新调度复用原评估体，不增加文本提取渠道。软超时用 `soft_observation` 保存第一次检测的既有stage；收尾不会改成Completed。RuntimeId返回后再次检查原预算，最终快照边界也不交付超预算Success。stage是首次检测位置，不是已证实耗时根因/故障指令。

## 已执行验证与边界

- g++ C++20 `-Wall -Wextra -Werror` + AddressSanitizer/UndefinedBehaviorSanitizer：生产调度/祖先绑定 **46/46 Pass**；实际文本/几何/源身份策略 **51/51 Pass**。覆盖祖先成功零BFS、focus/hit原序、BFS层序先eval再expand、终态停、共享预算在phase边界不重置、64合计额度/耗尽不expand、root绑定/跨PID/8层边界、首次softstage保留、move-only结果/异常传播、250ms、256原始空矩形计数、异常/坏shape几何、50000标量/100002哨兵/坏surrogate/嵌入NUL。回调/合成节点仅在测试使用；生产回调为真实SDK COM。
- LeakSanitizer因当前ptrace环境fatal（不支持ptrace）**Blocked**；只在测试进程设置ASAN_OPTIONS=detect_leaks=0继续ASAN/UBSAN，不声称泄漏检查通过。
- 原托管源码定向构建 **0 warning / 0 error**。原135项Selection xUnit方法/InlineData及断言用无端口隔离runner实际执行，**135/135 Pass**；含真实CaptureControlFixture子进程600ms硬截止、599/600/601最终交付、取消迟到读与唯一清理所有者、真实三次崩溃暂停/30秒恢复、协议/文本上限、Off/来源/身份/自动几何/排除等控制回归。无端口runner仅测试工具，不替代生产功能。
- VSTest因沙箱禁止本地socket而 **Blocked/Aborted**。NuGet原缓存NU1900/多节点restore未成功；单节点`--locked-mode`使用既有本地package cache恢复，不安装依赖、不改变锁或警告策略。该离线restore仅关闭在线审计请求，NuGet在线审计 **NotRun**，未声称通过。
- PowerShell原脱敏 **91/91 Pass**、选择摘要 **62/62 Pass**；diff检查通过。没有修改原600ms父预算、失败暂停/清理、marker或生产默认后端。

以上纯函数、回调/控制子进程验证不是实际Windows密码UIA提供者、提升源、跨窗口、多屏DPI或物理划词证明；SDK原生编译及本轮原六视图仍待一次CI。Windows定向安全实测与P0整体验收保持NotRun，不用Linux纯测试替代。

## 本轮一次固定 SDK 合成验证

精确冻结独审后，仅一次现有免费windows-2022、15分钟、read-only任务；明确`-DiagnosticOnly -ComDiagnostic`，Chrome HTML仍第一项，浏览器/视图/用例完全不换序、不预热、不自动重跑。只原六项，不追加Managed六次/120/GetData、不剪贴板回退、不切生产、不调用模型、不安装或扩大权限。

实际结果待测。若仍超时，仅按实际既有固定stage分析，保留失败，不提高250/600ms预算或宣称已确定根因；即使全匹配也只证明本合成范围候选可行。原GetData代码与Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 继续独立保留、未重跑。
