# 独立合成诊断host：真实进程与96字节协议

本轮基线 `cdbe3299ba9669a97833432a9a6b3cd72a87d2bd`。监督授权独立tools host、真实合成控制子进程及独审/正常开发推送；没有Windows UIA、COM、实际应用划词或性能实验，生产SelectionBrokerClient的每请求EOF+自然exit0交付门不改。

## 追加保全映射阶段（旧冻结证据不改）

本轮实际执行 `git rev-parse 780d83872709127a093a59b4a6d358b6979a0781^{tree}`，输出 **`497006f96a19e11797f491e6cd3bdcafad4fad99`**；远端cdbe3299提交的tree字段也为同值。原代码a317a0e2a583f99158b1ded71920454e684cd148映射f26e89c542f3858ab6e0a1b20a100b2820f7cd0e，同tree582e6f8fd812ed0e11d00824603ab1bc1471f6cf。原最终780d838映射cdbe3299，原作者/时间/SHA由审阅分支与已验证bundle保全。标准git因云代理失败，连接器重建提交身份；不声称原780SHA已在GitHub提交对象中可读。原源码1:1，正常lease推送/no force/未触发CI。追加此说明，不修改旧冻结报告。

旧纯模式57/57准确口径：**18个Fact +39个InlineData案例**；OfflineRunner默认只执行这57。此前217/217为旧160（含合成控制子进程）+新增纯57，不能把217写成OfflineRunner自己的通过数。本轮不将以前217计为新执行：实际新脚本分别输出纯57/57与真实合成host21/21，无合并或Windows替代结论。

## 文件和可复现执行

新增 `tools/KuaiYi.SelectionReuseDiagnostic`：独立.NET10 exe与诊断host；仅引用既有Core的OwnedProcessSession及链接原协议文件，没有生产引用、solution、包装或workflow接线。exe只能显式`--synthetic-child`加有限角色启动，否则返回2。没有模型、网络、凭据或平台权限改变。测试项目引用此诊断程序集，纯模式原断言/用例保持，新增内部FinishRound/ProtocolFault/擦除接口只在诊断生命周期使用。

`pwsh -NoProfile -File tests/scripts/run-selection-reuse-host.ps1`：既有锁定依赖/SDK就绪下no-restore构建；无包runner从本地空源locked restore；先独立纯57，再`--host-tests`真实合成21。new ProjectReference锁文件同步，无新增外部包/升级/网络安装。VSTest本地socket限制仍Blocked；直接调用真实xUnit方法/InlineData及断言，0跳过，未伪称VSTest通过。在线NuGet审计NotRun。

## 运行与交付门

默认disabled，明确enabled才允许一次round、1或2顺序请求；首个Begin及600ms真实单调起点在OwnedProcessSession.Start之前，启动等待计入预算。Start未结算/迟到返回时仍持有session，stopRequested使后来创建的自有进程被终止；不能重启/发第二轮。请求2仅第1完整复位后Idle，同源、全新nonce/序号、Reused=true、独立原250/600ms。不会提前准备请求/后台访问目标/预热/自动重试。

96字节固定帧流式填充：循环短读、零字节EOF与半帧断流分开，有限编码验证仍保留。每个IO只用一个固定帧缓冲；写入/解码副本/原帧finally清零。Completion只比较合成三字节，host仅保留FirstMatch/SecondMatch布尔和固定计数/状态，协议内部临时payload在同一同步交付片段立即擦除，不等下次请求。读取/写入等待取消后可能仍迟到，持有observer直到原IO任务结束并清零；未结算或faulted observer禁止放行所有权/发布summary。测试专用缓冲观察回调默认null，host不持久化原缓冲数组或stderr。

CancellationTokenSource按原单调起点余量安装**真实600ms看门狗**；每次IO返回/结果处理之后又核真实期限，timer延迟不能让迟到交付成功。取消/父端断开通知/Dispose/协议失败/崩溃/到期停止交付并请求终止**持有的自有Process**。清理等待/观察是独立结算，不宣称Kill在600ms这一瞬间必然完成。Kill发出不等于退出：OwnedProcessSession检查start/stop任务结算与实际HasExited，TryFinalize成功前不ConfirmOwnedProcessExit；owner token由host生成，只在内存保留，不传子进程。退出或迟到IO未确认保留owner与model退役状态，CanReplace=false；Dispose无法结算时抛固定错误，不能丢弃所有权。

Idle仅有120ms有界租期（默认直接第二请求，测试用取消感知屏障），超时退役不准备后续请求。独立host single-use；即使已退出，重复Run不会启动新子进程。标准子进程在stdin EOF退出；父端断开通知取消并Kill持有子进程。本轮没有检验父进程被OS强杀后所有fault角色的孤儿进程保障，不据此承诺该能力。

整轮停止后仍读取stdout尾部，额外整帧/半帧/未知帧拒绝；实际观察退出、start/stop和迟到IO全部结算且host自有缓冲清理后才发布固定ReuseRoundSummary。正常诊断round也主动退役/终止该合成进程；这不同于生产自然exit0保证，不允许替换生产门。固定match只是合成payload匹配，Confirmed只是协议确认次数；summary/测试通过都不是实际取词正确性或COM静止证明。

## 实际测试与缺口

最终可复现脚本 **纯57/57 Pass +真实合成21/21 Pass**，测试与runner构建均0warning/0error，diff与生产无变更检查Pass。21包含默认Off；真实1/2请求；逐字节短读；断流/半帧/额外帧；复位失败/未结算操作/匹配失败/崩溃；忽略取消的hang/late角色被真实看门狗Kill并观察退出（总观察<3s）；取消/父端断开；Dispose；Idle租期；并发；真实迟到启动；真实Kill但观察门刻意延迟时禁止替换；迟到读取在测试屏障后改写缓冲，擦除/退出确认前禁止summary。除专用迟到读取/退出观察屏障为test-only注入外，子进程均真实创建、Kill和实际退出；屏障测试不能冒充真实Windows观察故障。

初轮21中10Pass/11Fail，保留失败事实；测试定位错把worktree的.git文件当成目录，选错fixture路径，导致正常请求超时。修复定位而未改变600/250/断言/门槛，修复后21全部通过；临时固定调试状态输出已移除。收发InvalidDataException独立处理、结算串行化及迟到IO清零也已修正。没有删测试/弱化成功断言。

子进程persistent MTA/Automation位只是**合成协议声明**，没有创建真实COM apartment/IUIAutomation，更没有实现验证root/focus/hit/pattern/range/属性缓存引用释放。BuffersCleared只指host持有的编码/解码/结果缓冲和迟到IO清理，不证明CLR/OS管道/提供者内部内存擦除。UIAutomationCore内部残留、Windows Kill/exit、实际提供者兼容、安全桌面以及性能均NotRun。本轮不宣称解决取词，原Root224/Focus43/AutomationCreate5不能证明固定进程初始化成本或复用收益。

旧两次SDK5/6、managed四AV/HardTimeout与两次首HTMLCorrect0保留；GetData代码与Actions `220060f813a3640487d593920a1b1824aa779a00`不重跑；P0/物理划词、Win11、PDF/Markdown、密码/提权/DPI/安装卸载无新通过。冻结独审和正常推送映射待补充。现有唯一workflow仅开发分支+变更.github/workflows/windows-contracts.yml触发，本轮该文件不变；不得启动新的Windows/四请求实验。


## 独审发现、修复与最终26项口径

上文21/21是初冻结 **`60f26305f9bb209329f6bd2158953c493fcda734`** 的实际本地执行记录；独审随后暂不放行该冻结，指出启动fault/cancel无子进程分支无法结算，以及runner12s只停止等待而可能遗留原测试；另要求Idle返回后复核绝对期限。保留这份历史，不把初冻结说成已审通过。

新增独立NoChildStartupSettled分支：必须startup已fault/cancel/从未调用启动且OwnedProcessSession.TryFinalize/ObserveExit已结算空owner，才释放无子进程的持有状态。它**不调用ConfirmOwnedProcessExit、不发布ReuseRoundSummary、不伪填ExitConfirmed**；CanReplace来自“确认空owner”，不同于成功启动后的真实退出门。真实迟到启动仍等待原Task，不能走空owner分支。默认SystemOwnedProcessStarter与测试starter受“Start返回自有Process或无创建失败”的既有契约约束；未向子进程索取owner token。

runner改用DiagnosticCaseRunner的用例registry（最多一个host），测试超时先停止该host交付/取消/终止，再**await原任务本身与所有host结算**，始终保留超时失败。忽略取消的原任务或清理尚未完成时，不继续下一例、不发布成功统计；此时runner可能持续等待，不能用退出测试进程伪造清理。新增屏障实测在超时通知后原任务仍pending，registry持有host；解除原任务屏障、真实进程已退出与清理完成后只返回TimedOut/Failed。No-child及重复Dispose也能完整收尾。

Idle等待前冻结单调起点，120ms timer作为唤醒；同步/异步回调返回后检查实际已耗时间。新增150ms同步屏障证明不能借timer回调迟到启动第二请求。尾部额外帧新增单请求测试，保证不仅第二请求旧nonce检查能拒绝，也覆盖整轮最后Quiescent之后的尾帧。

最终可复现脚本实际输出 **SELECTION_REUSE_OFFLINE 57/57**（原18Fact+39InlineData不变）、**SELECTION_REUSE_HOST 26/26**（原21+新增5），两种口径独立。新增五项：最终尾帧、真实无效可执行路径、预启动取消且starter0调用、Idle返回越绝对期限、runner超时原任务/自有进程收尾。新增首轮26曾25Pass/1Fail：预取消后测试未等异步启动Task完成便断言空owner；补await真实结算后保持原断言，26全部Pass，不提前放行pending启动。测试/runner均0warning/0error，原期限、断言和生产门不变。

本轮仍无真实COM、Windows UIA/Windows子进程退出/取词或性能验收；父进程突然被OS强杀的fault角色孤儿保障未测，不能据正常stdin EOF或显式断开通知宣称已支持。源码修复后的精确冻结待再次独审；只放行修复后同树正常开发推送，不启动Windows实验。
