# Windows Core：测试阶段失败、最小修复与保留项

父级正常登录桥接的[真实CI39226078160/job117972581384](https://github.com/T1doo/KuaiYi/actions/runs/39226078160/job/117972581384#step:3:43)针对源码32116577a34d9f914f93dc715261e99da7d33868，终态Fail（run4m1s／job3m56s）。双runtime正常在线验签、job-local SDK402／host13、固定ZIP全SHA512、恰两项runtime13库存／audit与Core locked restore均通过。约xUnit5.22s已有断言失败，Core命令随后180s超时，无实际CoreTRX／最终计数、cleanupNotConfirmed、EvidenceStatusIncomplete。可确定已进入测试阶段，不能当作纯restore或编译慢，更不能只加时。后续专门App／Worker构建门、publish、真实WindowsApp／模型均NotRun，Package Hold。

父级完整提取为45次失败调用、25个方法名，绝非完整测试最终失败数：Reuse25、DesktopReceiver9、OLE2、InstallationApplicationControl1、InstallationHandoff3、IsolatedSessionLayout1、DiagnosticDispose2、InstallationSession2。完整日志未记录具体child启动路径、stderr或底层Crash异常。父级核验14件gzip/raw大小与hash；本地长base64转录未过完整性校验，没有将其纳入已回收CI原件。预期Core raw65376B／SHA256 cdccd22476d8a7cff884b794e4e57cce29703af51c39362351e9f02d7b9d615f。需后续精确小分片／文件桥接，不能重试本机Actions或换路。

本轮仅五份源码变化。隔离布局先检查用户提供的原始路径组件，再做GetFullPath及原规范化路径／reparse／来源数据不重叠检查，阻止PUBLIC.／尾空格／短名／ADS等拼写被规范化抹去。新增四个可跨平台重现的别名+..反例，在原实现实测4F；相同测试字节修后布局类49P。Windows原PUBLIC.直接用例尚未重新执行，不将Linux结果当Windows验收。微软[路径规范化说明](https://learn.microsoft.com/en-us/dotnet/standard/io/file-path-formats#path-normalization)提供语义背景。

两处复用测试曾把Environment.ProcessPath无条件当作dotnet。新测试专用选择器只接受明确的绝对DOTNET_HOST_PATH，或当前进程本身确为dotnet；拒绝apphost／相对路径，不扩大生产子进程环境allowlist，也不搜索其他SDK。真实Linux对照得到：选定SDK派发固定公开fixture DLL退出0、产生默认JSON；将同DLL交给已有fixture apphost退出2、双流为空。收据保存实际可执行路径／SHA、当前process／声明host、退出和原任务状态、输出字节／SHA／最多4096字节前缀；未结算保留实际Process和所有原任务，只有实际退出且原任务完成才释放，异常不转成功。这证明启动机制差异，不能证成本次Windows25次Crash的根因。[DOTNET_HOST_PATH](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-environment-variables#dotnet_host_path)与[当前进程路径](https://learn.microsoft.com/en-us/dotnet/api/system.environment.processpath?view=net-10.0)语义应分别使用。

OLE最终cleanup断言可能覆盖try中此前异常；TryFinalize是多个原结算门的合取，不能由False认定具体哪个任务卡住。该门、250/600ms、所有生产取消／清理路径均未放宽。Desktop／handoff／session各失败组仍待实证。

| 实际本地执行（安全Linux SDK402／host13） | 结果与边界 |
| --- | --- |
| r01／r02 | 同字节四项别名反例4F → 布局类49P |
| r03 | 定向118P1F；新诊断器错用JSON字典顺序比较，改精确结构比较；保留失败。该轮ZIP漏新untracked helper，不声称完整绑定 |
| r04 | 完整1918P；之后还修两处新探针partial-setup归属，不能代最终字节 |
| r05最终完整 | **1917P1F／0skip，完整回归未通过**；531源逐项绑定；父1904全名保留，新增14例全部P |
| r06同DLL诊断 | 3P，不能替代r05或当根因修复；三DLL hash为此轮实采，未倒填r05 |
| r07隔离App | 实际交叉构建Exit0／0W／0E；无restore／publish／Windows运行；28输出文件hash为构建后采样 |

r05旧失败是InstallationSessionTests.ChangedExactRecordsAreRetainedEvenAfterCleanActualExit(current.json)在Healthy信号等待8.015s超时；current.json改写在该await之后，不能称改写／归档行为失败。实际cleanup退出137、原IO结算有收据，不能替代健康成功。原run若早已fault而Healthy不置异常，8秒信号等待可遮住更早故障；本次确切原因Unknown。最小后续是在再现时同时观察原run终态与Healthy，保留真实owned child／IO状态，不增预算、不反复全量跑绿。

源码与结果经独立只读审查无剩余P1/P2；这不代表完整回归通过。九个workflow、runner安装器、安全SDK／在线验签／审计、锁、生产环境白名单和预算全部保持。当前普通开发分支提交不改变专用YAML路径，不触发第二次该SDK WindowsCI；任何第二次需父级另行确认。包Hold、图片间歇等根因Unknown、生产图片复用Off及250/600ms／来源／密码／完整性／取消／清理门保持；真实WPF／键盘／WindowsApp／模型与完整P6P7未验收。无main／强推／部署／费用／新Skia许可包／安全权限变更。

[源码、全部实际成功／失败原件及校验索引](../test-evidence/2026-10-19-session-windows-core-diagnosis.json)。
