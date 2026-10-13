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


## 本轮唯一实测：5/6，首次软超时检测为 FocusAncestors

代码与Actions执行SHA均为 `455199584d6d49ddfa36286a01e53518e189e829`，tree `463f543b0d9f3808e080e36c1e0269cf8256eed9`，唯一父提交 `98c1711ec44947d9d4bb4ea7de179d9f1f9fa74c`。精确独审本地冻结 `84e1b37f1ed4f03ddf19622bf04862d0b4876f2f` 为同树。[run38394742051](https://github.com/T1doo/KuaiYi/actions/runs/38394742051)，job115286971967，attempt1，**success**（有界诊断完成，不能解释为全匹配）。无重跑或额外job。

真实摘要 **Observed / SdkCom / DiagnosticOnly / Planned6 / Correct5 / ExitCode0**。Chrome154.0.8037.98 Html为 **SoftTimeout / FocusAncestors / None / HRESULT null**；Chrome ReadOnly/Input和Edge152.0.4191.66 Html/ReadOnly/Input共五项Success、精确Ordinal匹配，stage Completed。全部视图Mismatches0、SequenceCompleted/BrokerCleanup/ApplicationCleanup/ProfileDeleted true；RunnerSettled true。marker setup全Ready，原退出码及ReachedBoundary全null；没有Crash或HardTimeout。

本轮实际SDK原生构建成功（runner通过原CMake成功与exe门后才执行六场景），保留/W4/WX；Windows脱敏91/91、selection摘要62/62、真实WPF几何4/4。纯native97项与托管135项仍仅是上述限定的本地验证，不追加Windows安全验收通过结论。

### 按真实固定stage继续诊断

旧版本Chrome HTML的SoftTimeout/Completed被改进为本次首次检测 **FocusAncestors**。结合本执行源码：info.stage=7在焦点祖先收集前设置，bound_ancestors的循环条件调用共享expired；一旦首次expired为true，单调原时钟不会在后续阶段恢复额度，因此本次在候选评估/GetSelection/GetText和BFS之前已经耗尽软预算。这是代码门控与实际固定stage的有限推断，不是额外provider调用日志或指令追踪。

此stage **不能证明耗时全部在祖先枚举**：首次检查之前已执行source排除/完整性、COM初始化/CoCreateInstance、root、focus、hit、密码属性和ControlViewWalker；第一次stage7检查可能已经过期，也可能在该路径的后续父节点读取后过期。现有字段不能区分这两种情况，也没有每调用耗时证据。因此不能把BFS先行成本或GetText签名宣称为本次Chrome HTML超时根因，不能证明祖先优先已修复该失败。

下一最小诊断选择是沿用既有stage2—6，在其现有调用返回后检查同一原deadline并锁定首次stage（不改250/600ms、不重置origin、不增加字段/原文、不预热/换序/重试）；仍须保留来源复核、拒绝优先级和原空文本流程。该进一步改变 **未实现/未执行**；本轮一次SDK六场景授权已消费，精确实测代码保留以供下一门审阅。定向Windows密码/完整性/跨HWND等安全实测、物理划词/PDF/Markdown/DPI/安装卸载及P0整体继续NotRun，生产后端保持default-off，模型0/剪贴板回退Disabled。

公共job日志本地忽略保存 `artifacts/windows-selection/windows-com-38394742051.local.log`：**21706 bytes**，SHA256 `2d4f19f29028f9672d5e43e7e32b238677512b97fcd14c37de7196b88705b783`。旧5/6、四AV与两硬超时不覆盖不删除。原GetData代码/Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 保留独立、未重跑。
