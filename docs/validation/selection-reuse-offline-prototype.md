# 有界复用协议离线原型（不接生产）

起点 `72b54c454dfde4c00d8b44997eb8f6f352bdf652`。监督方仅授权离线、默认关闭的协议/纯状态机原型和独审，禁止新的Windows实测、后台目标访问、预热/准备请求、自动重试及生产接线。本轮不触发CI、不推送远端、不修改生产客户端/worker/配置或安装清单。

## 证据与动机边界

上次代码/Actions `b6618b53579af403cf146c940e9e7e29b17caa35`，唯一run38404072468/job115318937348/attempt1：Observed，Correct0/1，SoftTimeout/FocusAncestors。Root224ms、Focus43ms、AutomationCreate5ms；不能声称已证明新进程初始化固定成本，也不能声称进程复用能省去Root/Focus时间。本原型验证候选协议的拒绝/生命周期规则，不是性能优化验收。

## 文件和运行

- `tools/KuaiYi.SelectionReusePrototype/ReuseProtocolPrototype.cs`：协议编解码与单个持有进程的状态机，**没有COM对象、进程启动/终止、UIA、目标访问或后台计时线程实现**。
- `tests/KuaiYi.Core.Tests/SelectionReusePrototypeTests.cs`：真实xUnit断言，只有合成身份、字节和单调时钟，不创建Windows目标。
- `tools/KuaiYi.SelectionReusePrototype/OfflineRunner.cs`及其csproj/空包锁：只运行该类的Fact/InlineData，不使用VSTest/socket或控制子进程。
- `tests/scripts/run-selection-reuse-offline.ps1`：在既有locked测试依赖和.NET10 SDK参考包已就绪后运行。脚本先no-restore构建测试，然后从本地空源locked-restore无NuGet包runner、构建并运行；不安装依赖或联网。

运行：`pwsh -NoProfile -File tests/scripts/run-selection-reuse-offline.ps1`。该runner不进入solution、生产ProjectReference、安装包或workflow；仅Core.Tests测试编译链接状态机源。即使显式enabled也只使纯状态机可用，没有运行时接线。

## 有界协议

固定96字节、版本1、Kind/Sequence/flags/outcome有限enum、最多32合成payload字节；未知enum/保留位、错误长度、额外隐藏字节、非法身份、nonce空、时刻越界拒绝。接收方取得帧所有权，正常/非法/迟到路径都清零原始字节和解码副本；结果在host内部只暂存，复位/退役清零，不对外交付文本。完成结果与Quiescent必须使用同一个请求nonce、序号、Reused及PID+启动时间+HWND。

默认Disabled；explicit enabled且有owned-child token才进入Fresh。首次Begin为序号1、Reused=false，原host单调起点从该次Begin开始，等待Initialized；不能在初始化前访问目标。首次初始化时间包含于250/600ms。下一次仅Idle、同PID+启动时间+HWND、全新nonce才允许序号2、Reused=true，独立起点，不能重新Initialized或后台预热。最多两次，第二次静止确认后立即退役；没有第三次/并发/重试。

状态链：Fresh → Running → ResetPending → Idle（最多一次复用）→ Running → ResetPending → Retiring → Exited。只有正常Complete不能变Idle；必须核nonce/序号/源身份、复位成功、零请求资源/缓冲、零未结算操作，且host自有缓冲擦除后才记录一次ConfirmedRequest。运行或ResetPending期间再次Begin退役，永不发第二请求；默认/Idle没有目标访问/时钟后台动作。

每次目标工作及Success完成保持原250ms软上限，首次初始化也计入；SoftTimeout可以在原600ms内清理并确认失败结果，不能变成功。600ms绝对上限同时覆盖等待初始化、结果和清理，延续同一origin不重置；所有入口再次查期限。清理/复位未能在600ms内结算，状态机请求终止**该持有自有进程**。`TerminateOwnedChildRequested`仅为待执行命令，不冒充已Kill。替换权限只在trusted host以同一owner token报告真实进程退出后开放；任何子进程frame无法报告退出。该原型没有owner watcher/真实Kill实现，因此“600ms内实际终止进程”仍NotRun，未来接线必须由原OwnedProcessSession所有权与退出观察实现，不能用token回执取代真实WaitForExit。

身份变化（含Stale）、取消、协议异常/重复nonce/迟到帧、复位失败、硬超时或崩溃都立即退役、清零host缓冲并阻止后续请求。退役不能变Idle，退出未确认不能替换/第二请求；wrong owner token不能解锁。host/child时间倒退拒绝，child伪造小时间也不能绕过host绝对期限。

## 请求引用与真实隔离差别（阻止上线的核心风险）

未来运行时仅允许跨请求保留**无窗口MTA apartment与IUIAutomation**。Root/focus/hit/pattern/range、属性缓存、用户文本与缓冲均必须请求局部，不得跨请求保留。这里persistent只允许这两个固定声明位，Transient涵盖上述8类且Quiescent必须全零，pending operations必须0；这是协议和状态机声明，**尚未实现或验证真实COM引用的释放/缓冲擦除顺序**。

现有生产每请求一个进程，仍要求EOF +自然exit0 +nonce/完整双帧及原600ms/取消校验后交付。相关生产源码完全未改。复用候选用请求完成/复位确认代替每次进程退出，隔离保证更弱：即使所有显式请求引用销毁、host和worker缓冲清理、零待处理任务，**也不能证明UIAutomationCore/提供者内部没有残留、回调或对象缓存**。Quiescent只是可验证协议声明和host缓冲清理，不是COM内部静止证明。不能拿本原型通过替换生产EOF/exit门，更不能据此开启默认复用。MTA生命周期、实际UIA对象释放、provider callback、进程退出/崩溃后清理、Windows安全/跨应用仍全部NotRun。未来需监督方单独决定可验证方案。

## 实际验证

离线脚本本轮 **57/57 Pass**，实际xUnit方法/InlineData断言，no skipped；测试/runner构建均 **0warning/0error**。覆盖默认Off、无owner拒绝、两次顺序独立起点、首次初始化入预算、软/硬边界、重复nonce/错误序号/Reused/源身份、迟到与额外帧、取消、跨PID/启动时间/HWND、并发、复位失败、零8类请求引用、未结算操作、host buffer清零、协议保留位/长帧、崩溃与真实exit attestation门。还运行既有本地Selection回归：**217/217 Pass = 新纯57 + 原160**；原160包含合成控制子进程，全部离线且无Windows/UIA目标，不是新Windows实测。VSTest因环境socket限制仍Blocked；独立no-port runner是本次可复现执行证据，不伪称VSTest通过。在线NuGet审计NotRun。

生产引用扫描与git diff检查通过；没有生产源/solution/workflow/包装/默认设置变更，没有新Windowsrun，也没有模型/费用/凭据/权限变化。请求资源释放和terminate/exit仅在纯状态机中注入合成声明，均不能冒充真实运行时实测。独立审阅待精确源码冻结；最终报告补冻结SHA和审阅结论。

旧两个SDK5/6、managed四AV/HardTimeout和两次首HTML Correct0均保留。GetData代码与Actions **`220060f813a3640487d593920a1b1824aa779a00`** 独立保留、不重跑；物理划词、Windows11、真实密码/提权/不同HWND、PDF/Markdown、多屏DPI/安装卸载及P0整体验收仍未通过。


## 精确冻结独审结论

本轮协议/测试/脚本源码冻结 **`a317a0e2a583f99158b1ded71920454e684cd148`**，tree **`582e6f8fd812ed0e11d00824603ab1bc1471f6cf`**，唯一parent **`72b54c454dfde4c00d8b44997eb8f6f352bdf652`**，独审时工作区干净。独立只读审阅核对该精确冻结、源码与用例，**无阻断本次离线原型留存/报告的问题**；未独立重跑测试、Windows或CI，57/217为执行方实际证据。两次顺序、独立nonce/预算、同源、复位/清零、退役及退出确认均符合纯状态机约束。

该结论**不允许接生产或宣称真实进程复用可用**：MTA/IUIAutomation只是允许保留的声明位，真实引用释放、Kill和WaitForExit均未实现；owner token和Quiescent是trusted-host/合成输入，不证明实际退出或COM内部静止。生产EOF+自然exit0隔离仍保持。本文结论补记只改文档；本轮未远端推送、未启动新的Windows/CI，交监督方依据精确源码及这些风险决定后续，不自发追加请求对照或改安全设置。
