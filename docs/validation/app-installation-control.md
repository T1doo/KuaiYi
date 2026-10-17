# App 受控启动、健康与原关闭任务接线

从父 `e36b53954d746f94a253e5d5579bb58977cd889b` 正常建立 `dev/app-installation-control`。父先授权探索观察者，后明确收窄为既有 App 接线；本片没有实现长期 observer、守护进程、自动重启、stale 接管或 committed 标记释放。原 SPEC/P7 范围与许可、网络拒绝边界保持。

## 真实接线与准入

Core 新增 opt-in `IInstallationControlledStartProtocol`。只有使用该协议的 coordinator 在实际 Start 之前、持既有 upgrade admission 与 execution lease 时，写单次 launch permit。它绑定固定 App 角色、launch、实际 generation、既定 userdata、完整版本 digest 和精确 owner/current/可选 upgrade 字节。实际 child 经继承 stdio 收到 canonical/bounded START 证明，核 actual AppContext generation、固定 userdata、精确记录、两把 lease 正被持有，再 atomic claim permit。错误、缺失、旧版、重复证明不能创建 Shell。launch/fingerprint 仅关联，不是凭据；本模型不是防同账户恶意伪造或换名的安全边界。

App.Main 的受控入口在创建 WPF Application/Shell 及初始化配置前完成证明。userdata 固定为原 LocalApplicationData/KuaiYi，不取 wire 提供的任意配置根。受控第二实例直接拒绝，不向未知旧实例转送健康或关闭请求。普通重复启动仍先沿原 SessionSingleInstance/ShowExisting 唤回，不创建新 Shell；普通首次创建 Shell 才进入安装门。review/test-provider 等所有可创建 Shell 的分支同样经过此门，OCR probe 启动 Worker 前也经过门，--diagnose 仍只是只读身份诊断。

普通安装布局只识别 store 的 versions/generation；损坏的该布局、旧 generation 或未知 owner 拒绝，不回退为开发入口。非安装开发布局保持原入口，不把它算作 installed-role gate 验收。NormalEntry 持 admission/owner；自退只关闭 lease，保留 owner，不能凭自报退出放行下一 fresh controller。未实现观察者之前，这个正常退出后的持久阻断是明确边界。

controlled permit 仅由持有实际 Start 返回 Process 的原 coordinator 在确认该 child 实退、原 IO/task 结算后归档。交接后仍保留 accepted permit 和既有 owner/pending/ledger；未知记录不覆盖或清理。

## 健康、STOP 与原任务

App 已绑定真实 Shell 的 Loaded、startup、geometry、Settings 与 Close 路径。LoadStartup 原来捕获错误仍会完成 Task；新增独立 startupHealthy，健康还要求未退出、Loaded/Visible/非零 HWND、geometryReady、配置非 ReadOnly/RequiresRecovery。合法初装 Off 不要求已有模型或凭据，不启用发送、不读新秘密、不调用模型。**这些 WPF 检查在本环境只做源码审查和交叉构建，未执行 Windows 可见健康验证。**

STOP 只通过 Dispatcher 请求原 Close，不等待 Closed。原 ClosingShell/SettleExitContent 继续结算 startup、position、Drop、Selection、截图、热键、Translation/OCR；旧内容清理失败先保留窗口与控制通道，不进入 control shutdown。原 reader 保持同一循环，第二次 STOP 可重试，不新造 reader 假结算。内容成功后才 await installation control。

标准 stdin 实测无法仅靠取消令牌结算。App 退出因此发送绑定 admitted launch/fingerprint 的 CLOSING；客户端只关闭该精确 child 的 stdin，再取消并 join 原 reader/callback。EOF 只证明该 IO 路径结束，不能代替实际 Process exit。输出 READY/ACK/CLOSING 由同一 semaphore 串行；先启动 cancellation 再等原 write，保留原 cancellation 与 loop 任务。阻塞原 callback/read 时退出仍 pending，窗口端口与所有权保留。

协议 fault 与未结算任务分开：原 loop 已结束时记录有界 FailureCode 并观察原异常，允许后续正常资源清理；仍 pending 的 loop/cancellation 不能跳过。App 用现有 session-only DiagnosticLog 分类记录，不记消息、路径、配置正文或秘密。提交是单调状态：COMMITTED 输出异常、丢 ACK，甚至已知提交后 ledger 变成未知字节，也不能回到自动关闭 UI；unknown fence 保留。Committed EOF 保持运行；控制对象在 Program 静态持根到 OS 退出。

ImageWorker/Imaging/Providers 生产源码保持父原字节，Worker 一次性二进制协议保持；不声称本轮 Worker/Core/Imaging 构建 DLL 与父 hash 相同，也不把差异臆归为仅元数据改变。Worker 继续原一次性二进制 private-stdio、App 的实际 Process 所有权及 OCR 原任务/IO 结算；没有把 Worker 改成常驻接管角色，也没有混入文本安装控制帧。

## 实际验证与原件

新22例中20例执行自有合成 child；剩余2例为普通安装门文件/lease与容器识别验证。合成 child 在真实选定 generation 中执行与 App 相同的 Core production client/server；只有 WPF readiness/close/content-cleanup 是明确测试端口。CLI/继承stdio、完整 package 校验、单次 claim、真实 Process exit 与原 IO 并非 Mock。测试不按 PID 打开或 Kill；仅原 Start 返回对象可终止。committed 后由私有测试通道请求 child 自退；fixture-only40秒界避免无人清理。根测试没有在 transferred 句柄已释放后冒称握有 child WaitForExit。

覆盖缺失 proof、错 launch/role/directory/data/current/owner、未持两 lease、重放零额外 Shell；ready barrier、错误 ready 回退、真正 STOP 受阻禁止 publish、同 controller Retry、内容清理失败保留并第二次 Close/STOP、原取消 callback/read 被扣、committed EOF、精确 COMMITTED write IOException 和 knownCommitted 后 unknown ledger 保活。synthetic sentinel 不代替 DPAPI/用户配置验收。

所有尝试保留原 command、时间、源码快照、DLL hash、log/TRX，不倒填：

- r01：CS0119 方法 Channel 与类型名冲突，构建失败，无 TRX。
- r02：App 首次交叉构建成功。
- r03：fixture CS8602 可空分析构建失败，无 TRX。
- r04：派生 Fingerprint 未排除 JSON 导致序列化递归，testhost StackOverflow 中止；TRX counters0，不计完整红绿。加 JsonIgnore 修正，中止原件完整保留。
- r05：12P2F。一例为标准输入原读未被取消结算的真实缺口；另一例新测试错误期待默认 Dispose 优雅退出旧健康 App，原合同实际 Kill137。后者改为明确请求 Close+真实 WaitExit0，未降低断言，不充作产品红绿。
- r06：15P 中间验证。
- r07：临时恢复首次候选的 unconditional close，精确 COMMITTED 输出故障用例实际1F（Broken pipe，接收者误关闭）；恢复提交保护后 r08/r13 同方法绿。它是受控候选缺陷反例，不声称 e36 已有 production server 的回归。
- r08：相关102P；r09：相关104P；r10：Provider 构建成功。
- r11：首次 STOP 结束 control loop，第二次 STOP 的同方法实际1F Timeout；修改为同原 loop 继续接收，r12 同方法绿。
- r12/r13：相关105P。r13 加输出串行、原取消任务先启动及静态 runtime 持根；r14完整Core1807P/r15完整Provider2325P为随后容器修正前闭包。
- r16：大小写变体 VERSIONS 错当开发目录，同方法实际1F（未抛异常）；改为大小写不敏感识别、非canonical明确拒绝；r17该例通过。

- r17：相关105P1F，二次STOP间歇超时；没有当次完整时序，不凭后续通过宣称其根因已证。
- r18：新增独立受控反例。首次失败回执后扣住同原Close task，确认第二次STOP已经进入callback，再释放原task，仍超时，实际1F；证明fixture的IsCompleted判断会丢重试。改为容量1的关闭请求lane，保留并逐个await原Close task，至多合并一个pending retry；不并行新造退出结算，不修改生产合同或旧断言。r19同方法绿，安装相关107P。

- r20：完整Core1808P1F，全部22新例通过；旧OleReceiverTests.RealChildProductionServerDeliversOnlyAfterCleanupAndClearsActualArray在Cleanup line73的TryFinalizeAsync(true)为false。相关OLE生产/fixture、OwnedProcessSession/Termination及旧测试均与父原字节一致；失败时序未捕获，根因Unknown，失败完整保留。不与父r10/历史图片等混算。

- r21：同DLL、不重建的旧OLE整类27P；r22同DLL完整Core1809P。复核前另记25个目标DLL，封存时仍同字节；CoreTests/Core/安装fixture三主DLL在r19/r20/r21/r22执行metadata一致，相关五份旧源码与父相同。补观察utc=2026-10-17T14:01:33.631779+00:00，在r20结束后约14.46秒、r21开始前；raw note的‘while test run active’为错误表述，在此及最终index勘误而不改原件。这不是运行中或失败时序快照，不能反推原任务状态。仅有界复核，未修改旧用例或生产OLE，不把重跑绿当原因证明。

最终r19安装107P、r22完整Core1809P=父1787全名多重集+22，r23完整Provider2325P与父全名多重集相同；r24/r25 App/Worker实际交叉构建均0W/E。五轮各57源、共285源码绑定完全相同；目标DLL及实际fixture child SHA另核。五个实际候选同方法红绿分别为stdin结算、COMMITTED输出错误、第二STOP、容器case变体、扣住失败原关闭task时的重试。独立源码终审无剩余P1/P2；封存另审。

完整回归、构建、精确名字多重集、源码/DLL绑定、父原件保全及独审结果见[机器证据与原件](../test-evidence/2026-10-17-app-installation-control.json)。父 Core1787与Provider2325名字/旧断言保留；新Core22，无删除或弱化旧用例。

## 保留缺口

本片源码接线不等于 Windows 验收。真实 HWND可见性、Dispatcher/STA、普通重复启动唤回、关闭失败窗口行为、原生 Worker/OCR、Job、身份/NativeHost注册及rollback、完整payload/Inno/自启/卸载/K组仍未执行或未完成。后续必须在授权 Windows 形态验证。

没有退出观察责任链，因此 committed 与普通 installed 自退后的 owner 都继续保留。fresh 不消费自报、PID、EOF、锁释放或任意 terminal receipt 清 stale。分阶段 budget 非整体硬时限；不证明断电原子事务、发行者信任、恶意同账户防护或累计磁盘有界。r10（父片）原因及历史图片/idle/Core/WindowsJob根因各自 Unknown。

生产图片/分类复用 Off，来源/密码/完整性/取消/清理/250600门保持，真实WPF键盘/截图/DPAPIACL/物理250600/真实模型/完整P6 NotRun。无main/强推/部署/费用/凭据安全权限改变；无新依赖或许可接受。Skia4.153.1四包待批，source403和拒绝Actions不查询、不换路绕过；未知锁/数据/日志/进程保全。
