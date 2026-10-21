# 当前验收报告（P0 独立切片）

2026-10-12 最新限定平台证据：已独审源码 `4ff83e510b326d1d1bffc49dec2fd06315871ee1` 的 [Windows CI 38247667426](https://github.com/T1doo/KuaiYi/actions/runs/38247667426) completed/success，真实四原生DLL编译/PE/加载/八导出、两个CTest、公开生产HWND九项合同通过，210次Rendering观察无违反、无丢弃、完整流、0模型调用、正常退出。详细环境、每个DLL哈希、CSV与完整日志见 [Windows CI报告](windows-ci.md) 和 [summary](test-evidence/2026-10-12-windows-ci-summary.json)。历史批次中“原生/GUI/远端CI未运行”仅指当时状态，由本段更新这部分限定证据；原Alpha包未补入DLL或生成安装器。

本轮Server2022/管理员/high完整性/EnableLua=1/单屏96DPI不是Windows11普通用户验收。Rendering观察不等于独立呈现帧，最大相邻观察间隔482.858ms，视觉品质/性能/混合DPI及DpiPlacement其他再入仍需实测。P0/E06仍Blocked、P1—P4仍InProgress、A—L的106条整体验收仍NotRun；跨应用划词、实际OLE/剪贴板/热键/DPAPI、截图OCR业务/包身份/现代菜单/安装卸载和真实模型语义均未获通过。本次没有上传artifact、覆盖历史包或改变main。


不是 v1.0 完整验收或发布报告。环境与构建入口见 environment.md / README。

- 原始规格 SHA256：`ea3aa6529405bcc703986988fd089f476ee8f991d867e2220b3b20c439126166`。
- 当前离线自动化：Core 733、Provider 77，共 810；另有 Node 扩展 63 项，全部通过；最新证据见 p3-selection-summary；截图修正证据见 browser-ownership-summary。首批 57 项、探针批 88 项、迟到错误批 118 项及修复前失败证据分别保留。
- 25 个托管项目 Release 交叉构建 0 警告/0 错误；五个探针 exe 已检查为 Windows PE32+ x64，框架依赖 ZIP 已生成。这是构建/打包部分证据，Windows 实际执行均未运行。
- A—L 的 **106 条整体用例均为 NotRun**；已验证子项不能代替完整用例。
- R01—R18 的产品完整实现均尚未完成；模块/阶段/用例组定义完整保存在 traceability.json。
- 真实 Windows UIA、剪贴板、OLE、热键、DPAPI、截图、混合 DPI、焦点、包身份、OCR、现代菜单及安装卸载：Blocked（Linux 无交互 Windows）。
- Live 两协议、视觉分类/解释、费用/usage、取消：NotRun，无授权真实调用。
- 100 文本/60 图片语义质量、人工评分与八小时浸泡：NotRun。
- 开发/正式安装包、签名/信任、浏览器安装或商店：NotStarted/Blocked，无发布。

已验证的子项包括：Off/未授权实际 HTTP 为零、旧身份隔离、取消与收起门控、布局帧不变量、IPC 无符号长度先验证、独立双协议结构、实际 usage、断流部分状态、200 流内 error、限次重试、重定向拒绝及错误体不回显；Native Messaging 独立进程两帧、严格 UTF-8/字节长度、图像分片/摘要/截止及 Anthropic 块生命周期与思考内容隔离。

安全门控、协议和布局仍需后续用例补齐；不能将当前子项概括为完整 R16/R17 或 F/C/B/L 组通过。

监督方复核指出首批测试未覆盖等待响应头/非流式读取时失效请求的错误。补充真实 HTTP 屏障测试后，修复前 10/10 失败，修复后全部通过；不能用之前 57/88 项通过证明所有迟到错误路径。新增完整 identity 信封和统一接收边界，在目标队列执行时原子复核 session/version/request/epoch，所有事件类型均适用。当前是可测试的接收合同，正式 WPF App 尚未集成，未声称真实界面曾串流或已通过 Windows 界面验收。GitHub CI **NotRun**；本报告仅记录云环境本地脚本的实际运行。

监督方另发现 192KiB 原始分片的 base64 已占满 256KiB，未计 JSON 外壳；完整帧测试修复前失败，现降为 190KiB 并按完整 UTF-8 帧再次检查，协议上限和 20MiB 总量未提高。生产信封入口还自动关联任务取消，默认 caller token 为空时 Off/Stop/Lock/换文也会终止已在等待的 HTTP。兼容矩阵恢复规格六核心应用及 18 个独立视图，包含 Obsidian 三视图；全部仍 NotRun，不将计划当实测支持列表。

P0-03 增加真实 OLE 宿主与公开故障来源，24 项纯策略/事务/截止/Unicode 子项通过，原始桌面行为未运行。COPY-only、optional async 和跨 STA 封送有实际调用代码，但绝不以跨编译或纯逻辑测试声称 COM ABI、焦点、异步生命周期或 E06 不卡已通过。同步宿主浮钮可能冻结是明确未解决假设；详见 ole-probe.md。

新增清理专项26项：旧End协议2项修复前失败、资源生命周期/确认门控及退出状态18项、独立控制进程6项。独立进程实际验证Linux进程终止/退出确认，不包含COM；Windows清理、强杀、焦点、重启UI均NotRun。之前OLE批新增24项仍只代表纯策略/Unicode/事务/截止，不因本轮增加测试变成Windows通过。

P0-05新增60项有界图片/语言/身份/协议/输出与真实Linux Worker负路径；合计244项通过。另有源码清单baseline+5种故障变更、两个EXE实际内嵌身份/PE、14条原生共用路径/argv检查和Linux脚本拒绝证据。未原生编译、未生成/签名/注册MSIX、未运行Windows OCR/现代菜单；这些仍Blocked/NotRun。托管身份ZIP缺两个原生DLL，是部分审阅产物，不是开发安装包。详见imageworker-identity.md与ADR0003。

监督方指出的图片错误/取消迟到与Process.Start实际仍在UI线程两项已修，新增67项（其中2项创建真实Linux Worker、1项真实启动失败、64项状态/队列合同），全回归311/311。模拟UI线程不是WPF桌面证据；Windows实际启动/取消/调度仍NotRun。见image-ui-review.md。

监督方另发现子进程已发布/退出但启动任务尚未终结的释放竞态。确定性屏障修复前1项失败；完善ObserveExit/ExitState和两处实际窗口TryFinalizeAsync后新增19项及完整330项通过。保存首轮测试清理时序错误的16项失败，修正等待实际启动任务后重跑通过；未削弱断言。真实Windows仍NotRun，见image-finalization-review.md。

P0-06新增真实MV3源码和生产NativeHost入口；51项.NET及31项Node新子项通过，正式生产入口在Linux/未注册/未授权下拒绝。正路径运行独立宿主实际生产循环及实际内部命名管道，桌面授权端和Chrome Port为明确测试fixture；当前App还没有生产角色授权接收端，真实Windows浏览器图片入口并不可用。完整.NET381及Node31通过，19托管构建。源码截图/权限/文件角色检查不能替代Windows授权/安装/图片交互验收，详见browser-native-host.md。

监督复核截图风险属实，实际旧measureImage在8合成DOM负例错误接受；已整体拒绝fallback，不作完整遮挡证明。扩展43项通过（保留31原子项，新增12）。本JS修复.NET源未变且未重跑381；下一资源竞态修复将重新回归。argv/allowed_origins/摘要只能验证配置，不能认证浏览器启动者；无生产receiver，保持拒绝。见browser-ownership-review.md。

ReadBounded新增7确定性截止/完成案例，旧检查下4失败/3通过，统一观察释放和交付前截止复核后7通过；完整.NET388/Node43重新通过，19托管工程零警告/错误。测试用实际编解码器和文档，受控时间/底层任务仅在测试中，不证明Windows stdin；原实际Linux宿主/管道测试保留并回归。截图完整拒绝，来源未认证、生产receiver未开放，106整体仍NotRun。见browser-read-ownership-review.md。

生产App receiver代码已接入P0实验窗口，缺实际安装配置继续默认拒绝。65项使用实际本地管道/生产接收状态机和合成peer/Decision，正路径用实际DesktopPipeChannel；逐次确认、单次grant、截止/队列/失联、网络隔离和缓冲清零子项已验证。完整.NET453、Node43及19托管构建通过，Windows SID/PID/DACL/真实确认仍NotRun。前文无receiver描述属于历史批次，最新范围见browser-app-receiver-review.md。

监督指出旧扩展五秒 Ready 等待与桌面三十秒确认冲突。基线实际扩展→独立宿主→生产接收端六秒等待测试失败；新增明确 ConfirmationPending 阶段后通过。当前新增20项（10项完整链路、10项扩展时间/字段边界），完整.NET453/Node63、19工程locked Release零警告/错误。真实六秒等待不包含Windows UI；peer、确认回答、Chrome API、图像与加速时钟是测试输入。所有106整体仍NotRun，详见browser-confirmation-wait-review.md。

主动区域截图源码接通 WGC/显式GDI、逐屏遮罩、物理裁剪/内存PNG及本地ImageWorker。新增38截图+2角色文件测试，最新.NET493/Node63通过，19托管locked构建零警告/错误，身份源静态6项通过。真实跨屏取图、热键、焦点和原生编译均未执行；CMake尝试退出127，MSVC/SDK缺失。不能把Linux的UnsupportedPlatform Worker结果签成OCR成功。完整范围/待测见screenshot-review.md；106整体NotRun，P0/E06未签。

截图同步捕获/DwmFlush已从父App迁入自己持有的CaptureHost，默认5秒单调预算/2秒实际退出等待；Closing捕获等待3秒，未确认owner保留并阻止复用。共享GDI helper同线程flush后才CPU访问，7项可执行合成契约通过。新增23宿主+2角色篡改用例，全回归.NET518/Node63通过，21托管locked构建通过。真实Windows排除像素/驱动终止/层叠WPF/界面恢复/原生构建仍未执行；106整体NotRun，P0 Blocked，E06独立未解。截图几何的监督限定签收不等于此隔离专项签收；待独审见capture-host-review.md。

E06新增App自有真实浮钮/独立registrar STA/远端native OLE目标候选接线与共享Off门控，48项新验证保留全部旧断言；最终.NET566/Node63及24托管locked构建通过。实际child/pipe已测，后端/registrar/门控/pointer为明确测试替代。最后Windows事件坐标输入修正仅交叉编译，真实鼠标捕获/跨STA注册/共享映射/COM释放/多屏均NotRun。原生桥未编译，42文件框架依赖包不是安装包。E06实现InProgress/验证NotRun、176完整/106整体NotRun、P0 Blocked。本地提交待监督独审，未推送；详见ole-presentation-review.md及summary。

OLE候选固定d4基线先复核源码/证据，再修原Run、原delivery读租约和最终Off落地门控。新3例真实child/明确合成注册与lease；旧代码新增2例均Fail，首轮全回归491/492失败及修复证据保留；等待真实清零任务，不降低旧断言。最终Core492+Provider77=569、Node63、24托管locked Release及身份源6通过。独立新42文件ZIP保留旧4包；原生DLL未包含、非安装包。全部Windows/106整体仍NotRun，E06/P0未签收，准备正常dev推送供监督独审。详见ole-lifetime-audit.md与summary。

独立P1配置切片新45例通过；最终.NET614/Node63、25托管locked Release、身份源6。实际文件/共享锁/child终止有证据，AES-GCM和policy明确合成；生产DPAPI/ACL/WPF只编译，位置只纯函数。取消/原子生效/未知迁移/坏配置及撤销引用等只签子项，A10/L01/L06整体NotRun。P0/E06未过，P1整体未完，详见settings-review.md与summary。

监督四项设置缺陷及旧journal恢复同类问题已修正；52项新增用例完整通过，基线51中42失败/9通过及额外1失败均保留。最终.NET666/Node63、25托管构建零警告/错误。WPF真实交互、DPAPI/ACL和P0/E06继续NotRun，106整体验收状态不变。详见settings-recovery-review.md。

绑定复审新增20例通过，基线14中8失败/6通过及额外本地准入1失败均保留；实际生产设置事件到runtime的A取消/B保留、迟到拒绝、失败保全与证明提交恢复通知已验证。最终.NET687/Node63、25托管构建；正式P4 API调度/Windows图形/D08整体未运行或未交付，106整体验收仍NotRun。详见settings-bindings-review.md。

最后复核补迟到初始化版本保护1例先失败后修复，21新增全部通过，最终.NET687/Node63；旧20及686证据保留。D08整体仍NotRun。

P1实际启动骨架新增24项便携测试；生产配置读写和绑定取消链、实际子进程互斥/崩溃释放通过。跨用户/会话仅测试合成scope输入，真实Windows账户/SID/DACL隔离仍NotRun。启动浮钮/托盘、鼠标/DPI/锁屏/退出仅源码和编译已验证；P0/E06未过、P1未整体验收。详见p1-shell-review.md。

P2新增34项核心用例，包含2000组固定种子合成事件序列（不是2000独立验收项），实际目的队列迟到事件、真实配置事务保存、原绑定取消链保持及缓存/opacity/拖动重基。全部便携用例通过；生产WPF及公开流同Window已交叉构建、未实机执行。P0/E06 Blocked、P1/P2 InProgress；B/G组、实际60fps/DPI/焦点/安装仍NotRun。

P2三项监督缺口新增15例，旧版第四版Begin实际失败证据保留。工具条与测量宽度为精确旧源码计算反例；修后生产计划/缓存49项通过，不代替Windows字号/菜单/动画验收。详见p2-review-fixes.md。

P3生产选区获取接线见[选区复核](p3-selection-review.md)：真实Broker调用已有实现、稳定等待/来源身份/模式暂停/排除及600ms自有子进程截止。原文和译文区分，未接P4发送，不称完整P3或文字Alpha；真实Windows兼容仍NotRun。

P3两项监督修复：旧版5/5反例失败；本轮新增22例，生产输入桥channel/UI双队列、冻结来源/epoch与取消读取单所有者实测通过。定向72/72，完整.NET832/832、Node63/63。这里只验证可移植链路和合成受控流，Windows低级hook/真实UIA与106整体用例仍NotRun。详见[p3-input-read-fixes-review.md](p3-input-read-fixes-review.md)。

P3硬截止补审纠正此前仅相对timer的证据范围：入口单调起点覆盖启动、读取、退出及最终校验，>=600ms不交付文本，已取消完成read不转移所有权。旧5项4失败/1成功，新15例后定向87/87、完整.NET847/847和Node63/63通过。真实Windows及106整体用例仍NotRun，见[p3-absolute-deadline-review.md](p3-absolute-deadline-review.md)。

P3-04限定源实现：默认关闭/完整路径逐应用授权、真实STA有限全格式快照与Win32一次Ctrl+C、350ms归属校验、同sequence+owner条件恢复、撤销epoch与取消/迟到所有权。新118例；26工程locked Release通过，Core888+Provider77=965/965、Node63/63，无失败/跳过。定向及Linux自有进程不替代真实Windows/人工授权；106整体用例仍NotRun，P0/E06 Blocked、P1/P2/P3 InProgress。审查和精确冻结见[p3-clipboard-review.md](p3-clipboard-review.md)。开发审查包含ClipboardHost但无安装器/原生DLL/凭据，完整OLE文本Drop/全热键/P4尚未交付。

## 正式文本 Drop 监督限定切片

- 签收基线a870389599a6fe046d3cef62ea14a4647b58bbb1，复用原真实OLE隔离/注册/生命周期，正式Shell接入SourceKind.OleDrop来源快照。出生冻结receiver/epoch/ticket/version，COPY-only、只在Drop取有限HGLOBAL；Unicode/HTML/RTF惰性纯文字提取，进入/离开不UIA/复制/请求。提示最多96×48DIP，失败旧来源保留。未推进P4。
- 原DropBegin1500ms绝对预算延伸到最终队列与原生末次门控，同投递恰一次；Off/模式/设置/暂停/锚点/来源失效与取消撤销，读取/交付/注册/实际进程退出未settle不释放或重建。迟到IPC唯一观察清零，取消即释放排队来源引用，未用token伪称COM终止。
- 新144专项均通过；完整Core1032+Provider77=1109/1109，Node63/63，26工程locked Release零警告/错误。旧888Core/77Provider逐项名称保留、26锁/产品书字节不变。编译与测试初次失败完整保留，修正fixture非法样本/属性surrogate/已释放Process查询与交付发布时序，原禁止错投/不清零断言未放宽。
- 独立text-drop审查ZIP及逐文件manifest/PE，15旧包最终摘要全部核对；前轮误覆盖后恢复事故仍记录，不宣称全过程没动。运行代码测试最终CI后未改，文档/staging随后冻结。详见text-drop-review.md、ADR0013、本轮summary与Windows清单。
- Windows原生DLL未构建，包缺DLL时正式Drop明确停用。真实来源/COM/hover/DPI/E06仍NotRun/Blocked；176追踪/106整体NotRun，P0/E06 Blocked、P1/P2/P3 InProgress，R09/P5-01…04源码InProgress而完整P5未交付。尚无正式模型请求闭环/安装器/日常可用软件，没有安装、扩权、凭据、真实模型调用或新协议接受。

## 正式 Drop 原生截止父审补正

- 基线8023c71c03899ada517d162de032e1426fdad8f7。父审发现ReceiverHost先Stopwatch<1500再SourceCurrent、后不复核，原“来源检查后再查/共享原DropBegin”宽声明不能签收，属实。原生产顺序提取共用资格策略红测17项7失败/10通过，旧Host/helper与日志/TRX保留；不是Windows原生执行。
- ReceiverHost真实路径使用该策略，SourceCurrent后最终gate/activation/原本地elapsed检查，编码/验证后enqueue明文前再次检查。17新专项通过、原144保留共161/161；完整Core1049+Provider77=1126/1126、Node63/63，26工程locked Release零警告/错误，原1032Core/77Provider逐项及26锁/规格不变。
- 原生group Stopwatch、父收到合法DropBegin的TimeProvider、Core接受Begin的TimeProvider分别是各自起点；协议未传native start，不比较不同clock的绝对值，没有证明原生起点至最终App交付总计1500ms。父receiverCurrent不是原生计时证明，参数/ADR/报告已收窄；跨进程排队端到端预算仍缺证据。
- 独立admission审查包，16份旧包最终摘要核验；旧误覆盖恢复事实保持。源码/测试最终回归后未改，供限定独审；实际nativeBridge未编译、WinCOM/共享映射/来源/E06仍NotRun/Blocked，176追踪/106整体NotRun，不推进P4/安装/扩权/真实调用。详见text-drop-admission-review.md及独立summary。

## P3-05 事务热键切片

基线5b9b23e85102787a24833dd7fa6111d92887137c；同一校验/聚焦录制、九动作清除/建议值、保留及内部重复检查、真实Win32专用STA注册与占用器、成组互换和回滚、真实配置原子保存/取消/重启与日志恢复已接线。原单一选区热键迁移统一服务；Off与锁屏/epoch/version门控不自动采集或发送。队列围栏重入三例原断言失败，修复后新99专项全部通过；真实配置文件故障测试与模拟注册器清楚区分。最终27托管工程locked Release零警告/错误，Core1148+Provider77=1225/1225、Node63/63，原1049Core/77Provider逐项保留，26旧锁字节不变，仅新增无包引用占用器锁。

正式截图热键仅本地准备/预览，不启用或上传；P6完整确认与模型尚未接，重新生成明确P4调度未接入。Win32消息泵/实际占用/焦点/AltGr/锁屏/Explorer/异常重启/截图仍NotRun。P0/E06 Blocked、P1/P2/P3 InProgress、176追踪/106整体验收NotRun，无安装器/native DLL/真实凭据或模型调用。独立热键审阅包与17旧包最终摘要、13精确commit历史冻结见[p3-hotkeys-review.md](p3-hotkeys-review.md)、[ADR0014](adr/0014-transactional-hotkeys.md)、[Windows清单](p3-hotkeys-windows-checklist.md)及[summary](test-evidence/2026-10-11-p3-hotkeys-summary.json)。前轮误覆盖后原字节恢复事故继续保留。

## 热键前级队列与提交安全通知父审补正

93b538a尚未签收：当前epoch在Pressed才捕获不能隔离积压WM_HOTKEY，热键Commit异常也曾跳过已提交profile失效通知。生命周期红测33项32失败/1通过，通知红测6失败，等待原Commit的通知时序再红测2失败；原源码/断言/TRX保留。现在RuntimeGate状态变更锁内同步记录uptime/revision，Native Host按原GetMessageTime核验后携带不可变身份入UI，取词/截图入场继续核验；On不改原epoch也推进独立revision。隐藏截图首按仅显示，下一新按键框选，完整P6未接。已发布设置先通知全部安全订阅者再等待原Commit任务，错误和journal保留、磁盘不假回滚；A取消而共享B保留，原任务仍阻止草稿释放。故障围栏恢复成功须验证自有准备绑定后才清除faulted。

最终155专项（99原有+56新）、Core1204+Provider77=1281/1281、Node63/63、27工程locked Release零警告/错误。全部原1148Core/77Provider名称和27锁保留。18旧包最终摘要核验；14历史精确commit摘要包含未签收93b，字节审查不代表签收；旧误覆盖后恢复事故继续记录。P0/E06 Blocked，P1/P2/P3 InProgress，176追踪/106整体NotRun；Native输入队列/时钟/实际占用、WindowsUI/跨应用/DPI/安装/远端CI仍NotRun，不推进P4，无安装/扩权/真实调用。详见[p3-hotkey-admission-review.md](p3-hotkey-admission-review.md)、[补测清单](p3-hotkey-admission-windows-checklist.md)与[summary](test-evidence/2026-10-11-hotkey-admission-summary.json)。

P3-06 生产接线限定验证：66 新用例、Core1216+Provider131=1347、Node63，27 locked Release；来自受控采集端口的生产快照协调器→真实 loopback Provider→纯结果/Frames 可验证，WPF/native/跨应用/焦点/多 DPI 尚 NotRun。176 追踪项/106 整体验收仍 NotRun，P0/E06 Blocked，P1/P2/P3 InProgress；不把本轮当 P4 Alpha 或真实模型质量验收。见 p3-text-translation-review.md 与本轮 summary。

测试模式边界父审修复限定证据：旧25新反例22失败/3通过，修后39专项均通过；完整Core1216+Provider170=1386、Node63、27 locked Release。旧测试名/断言保留、27锁/原规格不变。其他父审仍未完，Windows/完整P3/P4/106整体不签收；见p3-text-boundary-review.md与本轮summary。

P4-01限定代码/离线证据：新增Core40/Provider43，完整1256+213=1469、Node63、27 locked Release；真实配置文件/AES合成密文到两协议loopback请求和返回，以及Off固定样本限制已验证。Windows设置UI/焦点/关闭等待/DPAPI、跨应用/动画/DPI、安装/真实服务仍NotRun；106整体NotRun，不签收完整P4 Alpha。

P4授权/预算父审补正：新30项、完整Core1262+Provider237=1499/1499、Node63、27locked Release；旧授权17中11失败与旧预算3中2失败原证据保留，原1469名称/27锁/规格不变。其他父审未验，106整体验收NotRun；Windows/native/DPAPI/UI编辑/焦点/动画/DPI/安装/真实模型/远端CI不签收。

P4命令补正：05dc对已保存密钥空PasswordBox.Clear漏编辑失效。真实按钮命令体/preset秘密步骤先统一DraftEdited再清引用；新6同体/实际临时密文+HTTP+lease测试通过，原红6中5失败及初夹具失败保存。完整Core1262+Provider243=1505、Node63、27locked Release通过。WPF事件和DPAPI NotRun，106整体验收不改；详见p4-secret-command-review及summary。

P4-05源码补齐：会话四风格/预览版本N/事实保真重生，原35专项通过（Core8/Provider27）；原3反例与实施/夹具失败保存。最终Core1270+Provider270=1540，Node63，27locked Release通过，旧1505/锁/产品书/四模板保留。WPF/真实服务语义/J评分/P4文字Alpha/106整体NotRun，见p4-session-style-review和本轮summary。

P4-05模板实际设置→保存重启→生产Compose→两协议HTTP闭环：原6反例保存，修后通过，新67专项/全1607 .NET/63Node，27locked Release。完整预览只用公开样例，不能替代真实模型语义和WPF验证；source_text不把原文插system，发送/密钥/测试权限独立于模板。旧1540/规格/模板/27锁保留，106整体NotRun，见p4-custom-templates-review与summary。

P4-04新增61阶段专项，全1668 NET/63Node及27locked Release通过，原1607保留。原Core4/修正确HTTP屏障后旧传输6期限红测及首次夹具/生产2/2失败记录保全；真实loopback/TLS验证不代表Windows代理/DNS/完整P4/106整体通过。配置四字段/旧快照、SSE有效期限/JSON真正读取有实际路径，见p4-phase-timeouts-review/summary。

P4-04截止竞争补正：ccf998未签收，新44生产路径竞争回归通过（原41红例36失败保全），94专项通过；完整Core1315+Provider397=1712/1712、Node63，27locked Release零警告/错误，原1668全部保留。绝对期限/父取消在阶段转换和逐事件交付复核，当前源审阅见[p4-completion-races-review.md](p4-completion-races-review.md)。本地证据和父签收分开；106整体验收仍NotRun、P0/E06仍Blocked，Windows实机/安装/真实模型/远端CI未执行。

P4-04实际消费者队列补正：893ebc未签收，新增124生产HTTP/两个消费者/真实Receive-Apply延迟队列用例（原124红80失败/44通过保留），修后124及旧94专项218/218；完整Core1315+Provider521=1836/1836、Node63，27locked Release零警告/错误，原1712保留。总预算与事件冻结阶段资格在实际目标应用前复核，Current自身跨期也不越过期限；原ZIP精确字节保护/旧事故记录保持。见[p4-queued-delivery-review.md](p4-queued-delivery-review.md)。父签收待完成，106整体验收NotRun、P0/E06仍Blocked，Windows/安装/真实模型/远端CI未执行。

P0—P4工程交付补齐：自包含文字Alpha/13独立RID锁/六实际宿主/1199文件与有界启动入口完成云端发布核验，普通CI1836 .NET/63 Node、27managed Release0warning/error，27原锁/原测试保留。9包完整性/拒绝实验不算平台微测通过；真实Windows程序没有启动，106整体验收NotRun、P0/E06Blocked、P1—P4InProgress、安装器/产品原生DLL仍缺。无用户桌面/真实key模型/安装注册或新SDK协议，见[p4-text-alpha-review.md](p4-text-alpha-review.md)。

Alpha退出合同修复：原24脚本收尾/三层委托红例失败，真实receipt形态最终24/24通过，实际包预检成功、Linux桌面拒绝未变；Windows/真实GUI/模型没有运行。1836/63为044已验生产回归，本轮未重复且不称本次新增功能测试。大包及32旧包保全，仅小脚本补正；总体NotRun/P0Blocked保留，见[p4-alpha-exit-review.md](p4-alpha-exit-review.md)。

## 2026-10-20 · GIF首帧组件与P0本地标注增量

基于完整父SHA `0fafe74760958b78302bbc58ae51dcdfd7d9d55e`，正常分支 `dev/p6-gif-first-frame`；受界GIF首帧、父metadata绑定、实际P0“使用首帧”预览接入，详见[验证记录](validation/gif-first-frame.md)及[证据](test-evidence/2026-10-20-gif-first-frame.json)。Linux x64 / SDK10.0.402 / runtime10.0.13 / 固定SkiaSharp4.153.1，writer实际Codec453P、Provider传输37P、Core相关165P，均0F/0skip；Worker/App交叉构建各0W/E。上述为I08/I09及R11/R18部分工程证据，不将其计为完整I组、P6-02/P6-06、Windows桌面或图像质量通过。Windows原生加载/完整生命周期/可见标注交互、生产打开投放、真实画质和250/600ms仍NotRun。


## 2026-10-20 · 生产打开图片来源工程增量

从完整GIF父SHA `0b6d1c835f7f9baed79d7320ac485331fc2bbf88` 在正常 `dev/p6-open-image-source` 接续，唯一writer、最小设计独审后交付正式Shell“打开图片”→原安全本地读取/受界Worker→独立可信文件owner→实际本地预览/可见“使用首帧”→原生产确认与两阶段发送门。文件无截图crop/桌面anchor/proof；Off打开不启用不发送，明确启用调用原Toggle并重查同一owner/version。清除/停止/失效及退出保留原Task/Worker到实际结算，未确认保留owner可重试并阻止新采集。投放及文件OCR后续复用，当前OCR仍只接原截图owner。

最终writer完整Codec470P（新17+原453）、完整Provider2381P（新20+原2361）、Core相关限定165P，各0F/0skip，Worker/App交叉0W/E，五locked audit restore0；依赖/许可/原Codec测试/SPEC原字节不改。旧截图CTS回归1F和新Anthropic夹具位置假设1F已保留修正；旧BMP取消结算未确认1F保留，同构建定向2P与同源码完整470P通过，未证实根因且未改退出门/旧断言。详见[行为与边界](validation/open-image-source.md)/[直接证据](test-evidence/2026-10-20-open-image-source.json)。

Windows实际文件handle/dialog/WPF/Toggle/native/字体质量、250/600ms/600MiB、完整P6-06/P6仍NotRun，不当图像Beta验收；WebP odd拒绝、PackageHold、生产复用Off及全部安全发送清理门保持。无真实模型/私人图/新依赖许可权限费用凭据/CI/用户电脑/发布部署/main修改/强推。


## 2026-10-20 · 图片取消清理受界诊断

从9a38249接续正常dev/p6-image-settlement。确定性EOF原Task与cleanup双TCS场景证明首取消异常会被旧finally/Dispose诊断覆盖，保留r02红1F；现原Code保持、Primary/Cleanup分别保存，safe快照仅固定状态与类型/观察序号，展示64/ring32不截断真实任务跟踪。原IO/cleanup未结算仍阻止交付；实际清理成功可同owner显式重试，cleanupfault继续保留owner而不冒充成功。独审实际cleanup异常未观察P2和unused/Running误记exit诊断精度问题均已闭合，不降低真实退出或发送门。

最终Codec475P（原470+新5）、Provider限定57P、Core限定97P，均0F/0skip；Worker/App交叉0W/E。原BMP历史469P1F→2P→470P保留且根因未证，不以新诊断反推已修复或断言均过。新测试初编译0执行保留，原470Codec源/锁/依赖/许可/产品书不改，无sleep凑绿。详见[范围与边界](validation/image-settlement.md)/[原件索引](test-evidence/2026-10-20-image-settlement.json)。Windows实机/画质/250600/600MiB与完整P6仍NotRun；PackageHold、生产复用Off及原安全门保持。


## 2026-10-20 · P6-06单个本地图片文件投放

先独审并推送取消诊断闭环d1b9d459f967847df47f83406d7f5324fc4dfb5e，再于正常dev/p6-single-image-drop先设计独审、唯一writer实现。正式Shell本次运行默认关闭的“接收单张图片文件投放”，On+Drop下只接CF_HDROP单wide本地文件；原隔离OLE清理/身份/1500ms门后有限path转移，安装既有StartOpenImage自有Task→原安全文件/Worker/独立file owner→可见预览/首帧→原发送确认，无新decoder/provider支路，不自动启用或发送。receiver撤销不取消已转移fileTask，optout仅取消exact droppedTask。

最终Core相关261P（新61+原200）、完整Provider2393P（新12+原2381）、完整Codec475P，各0F/0skip；Receiver/App最终交叉0W/E，两locked audit restore0。首编译0执行、clear专用port及Dispose后Process查询的测试失败原件保留；独审不可达DropEnd拒绝提示P2已闭合并回归，FileContents的index0查询缺项也已补齐。Windows格式query介质最后收窄后仅交叉构建，不称真实COM通过。详见[支持范围/验证](validation/single-image-drop.md)/[直接证据](test-evidence/2026-10-20-single-image-drop.json)。

真实Windows/OLE/filehandle/WPF/来源密码完整性/字体画质/250600/600MiB、位图/虚拟文件/多图和完整P6仍NotRun或未交付，完整P6-06/Beta不签收。旧BMP根因未证、WebP odd拒绝、PackageHold、生产复用Off和全部安全发送清理门保持。无用户内容/真实模型/新依赖许可权限费用凭据/CI/用户电脑/发布部署/main修改/强推。


## 2026-10-20 · 文件图片显式OCR-only

从已独审31ee5704fe1e210556a4c2d70e80b811d3a75634在正常dev/p6-file-image-ocr接续，唯一writer、先设计独审。复用无desktop的file owner与原OCR coordinator/本机语言选择/文字预览/显式批准及发送门；按实际归一化PNG尺寸和min(nativeMax,2560)检查，不二次缩图，旧截图>2560拒绝保持。验证job/维度/语言/词界后才生成≤50000个immutable数值原图映射，可信文件/GIF首帧bool沿预览到结果和版本标签；不发送图片/路径/映射。晚modal改为仅撤销same preview与exact request，原识别Task未返前保留PNG，finalize未确认保留原owner阻止替换。

最终完整Provider2427P（原2393+新34）、完整Codec478P（原475+新3），均0F/0skip；Core相关限定114P（非全Core）、Worker/App交叉0W/E、两次locked audit restore0；实况见[行为与边界](validation/file-image-ocr.md)/[直接证据](test-evidence/2026-10-20-file-image-ocr.json)。实际Linux decode→file owner→原OCR coordinator的成功识别端口明确合成；实际Linux Worker文件OCR拒绝UnsupportedPlatform。保留首次App变量重名和新Provider类型歧义的编译失败日志，旧OCR/Codec测试不改。

Windows OCR/OLE/COM/WPF/真实来源身份/最终文件handle、字体画质、250/600ms/600MiB与完整P6仍NotRun，位图/虚拟文件/多图不扩展。旧BMP根因未知、WebP odd拒绝、PackageHold、生产复用Off和原安全门保持；无真实模型/用户内容/新依赖许可权限费用凭据/main/强推/部署。


## 2026-10-21 · P7-03正常主浮窗浏览器图片本地预览

从完整1eeb40444d718d8d92529207220cdc17ca0c076e核单writer/worktree/远端0/0及main51d0774后，正常dev/p7-browser-image-preview推进原SPEC13.2/P7-03的实际缺口。先设计独审，再把原已安装策略/peer/来源/分片接收接到正常运行Shell；实验与离线窗口不启动。接收专用门不授予Shell启用/发送，原会话finally/读取实际结束后精确一次交接，借用PNG跨原Worker准备与异步取消回调结束才清零。Kind=Browser无filehandle/截图坐标/proof，本地预览、确认/版本/失败恢复明确浏览器来源；Off接收不自动启用、识别或发送。后台设置撤销间隙用持续permit及原epoch/Activation锁内比较封闭，Cancel/Dispose异常不跳过原任务；独审确定P2均闭合。

最终完整Provider2436P、Codec481P均0F/0skip；完整Core两轮均2069P/1F/0skip，旧CaptureHost waitFailure:true在Cleanup69 finalize断言失败，间隔定向2P，根因UNKNOWN，finally可能覆盖body错误，不称全Core通过或旧问题已修复。新增14个Core用例均通过。App/NativeHost/ImageWorker各0W/E，两正确locked审计restore0；Worker两条错误项目路径MSB1009原件保留，随后正确路径通过。383项旧测试/锁/项目依赖/许可/扩展/工作流/安装数据/SPEC字节不改，Provider项目仅加Compile link，独立源码只读ACK；真实测试由writer执行。详见[范围与验证](validation/browser-image-preview.md)/[证据索引](test-evidence/2026-10-21-browser-image-preview.json)。

当前只接已运行正常Shell；冷启动/Explorer认证转交、跨域/blob/可见裁切/其他浏览器格式仍缺。Windows实际Chrome/Edge/peer/WPF/OLE/COM/OCR/安装身份/来源密码完整性、字号画质、250/600ms/600MiB及完整P6/P7/K组未验，未把阶段整体或Beta签收。旧BMP根因未知、WebP odd明确拒绝、PackageHold、生产复用Off和原来源/取消/实际退出/发送门保持；无真实模型/用户内容/用户电脑/新依赖许可权限费用凭据/CI/main/强推/发布部署。


## 2026-10-21 · Core CaptureHost 红门的测试清理错序

接完整浏览器候选0a333765061261c4d93befed67f77e2f123eb301，正常dev/core-capture-finalization，唯一writer/先独审。原r13/r24均2069P1F，最终栈为CaptureHost Cleanup69 finalize断言，finally109可能覆盖body，旧轨迹不补写。最小同路径屏障实证：原theory正文过、真实child退出/另一次OS wait完成，生产持有的原观察Task仍pending，旧Cleanup即finalize断言红。仅测试Cleanup显式join原观察再保留finalAssert，原4s/668字节正文和生产315文件原字节不改；body/cleanup双异常保全，生产退出安全门不放宽。

r01诊断原方法2P（非自然失败复现），r02受控0P1F，r03修后原2+新1=3P，r04最终HostTermination+Capture35P/0F/0skip；红源/原XML/日志/TRX/摘要保全，独立只读源码ACK。未盲跑第三次全Core；本轮仅相关回归，不把旧两次完整Fail改绿或声称历史每次根因/body已完全追溯。无必要重建产品或重跑Provider/Codec。详见[机制与验证](validation/capture-finalization-root-cause.md)/[证据](test-evidence/2026-10-21-capture-finalization-root-cause.json)。无新功能/生产改动/依赖许可权限费用凭据/模型/用户内容/用户电脑/CI/main/强推/部署；原安全门、PackageHold/复用Off及Windows/画质/250600等未验边界保持。


## 2026-10-21 · 固定00f80840一次完整Core签收未通过

按父指令固定已审修复HEAD00f80840b8dede4019208dc0f4d8f2f60b7ce90a执行一次完整Core：2070P/1F/0skip，807输入源前后相同、执行后HEAD未变/dirty0。CaptureHost原两分支+新屏障均P；唯一新红为SelectionBroker最终单调边界599期望Success实际HardTimeout（line26，0.7353269s）。原TRX/失败逐字XML/DLL前后摘要与45处变化清单已保全。只控制timestamp/UTC的假时钟未控制真实CancelAfter600ms，是独立只读复核候选，原日志不足以证明hook到达/超时阶段/唯一根因，不认定产品缺陷。

完整Core状态Fail，未再次全套重跑或推进下一功能，未改任何产品/测试源码。先保全双轨及候选分析，后续定向诊断/测试seam需最小设计独审；生产600ms、原断言/取消/清理/来源门不放宽。见[补验分析](validation/capture-finalization-root-cause.md)/[完整证据](test-evidence/2026-10-21-core-post-fix-signoff.json)。Windows/P6/P7/画质/250600等未验保持，无main/强推/部署/用户电脑/真实模型/费用权限凭据或依赖许可变化。


## 2026-10-21 · SelectionBroker 599ms测试双计时机制定位及最小注入

从269dd3720c1f0288012509c1abc9f8fa7ea582a0核clean/远端0/0/单writer，正常dev/core-selection-deadline，先独审。原完整Core2070P1F的599失败原件保留，不倒推735ms历史阶段。复用同theory+原Start屏障确定性证明：Start已进入未返，fake0/hook未到，真实timer先HardTimeout；r02反例1P捕获原错误期望且实际child退出，非599自然通过。r01诊断末尾查询已Dispose Process导致0P1F，准确源/日志保全，独立witness修正不冒充产品红绿。

唯一产品差异为internal init测试deadline arm；null默认仍原CancelAfter，public构造/250/600/remaining/来源/取消/实际退出门保持。原599/600/601断言不改，手动deadline核arm1/due600/无回调；新增fake<600 startup/final fire核同CTS取消/空交付/实际清零及原owner结算。独立5s测试防挂guard触发只能令断言失败，原Capture任务与body/cleanup错误均保全。最终相关128P=原125全名多重集+新3、App交叉0W/E，820保护全真，独立源码ACK；未重跑全Core，不称完整P6/P7通过。详见[定位与合同](validation/selection-deadline-root-cause.md)/[原件证据](test-evidence/2026-10-21-selection-deadline-root-cause.json)。Windows/真实250600/画质等未验及原安全边界保持，无新权限费用依赖许可/用户电脑模型/main强推部署。


## 2026-10-21 · 固定5e95cd0b一次完整Core签收通过

按父侧独审ACK后的指令，仅在clean固定 `5e95cd0b3a4b3ef7dbb5bcce889dbe5c28f0b877` 执行一次Release完整Core（no-restore，正常编译），实际 **2074P/0F/0skip**，exit0；启动UTC03:29:36，命令总耗时103.602s，测试报告约1m5s。原2071案例全名多重集全部保留，加3个SelectionBroker案例；CaptureHost原两分支和原观察屏障、599/600/601、默认真实timer反例和手动fire两阶段共9个关注案例全部P。原失败归档保留，不追认历史唯一根因。

807项输入源前后逐字相同，HEAD保持5e95cd0b、执行后dirty0。Release DLL前后各330项，正常编译改变45项，分别保留摘要及变化表，未宣称二进制相同或全部DLL均被执行；生成AssemblyInfo绑定测试HEAD。[完整命令、日志、TRX、源码/DLL摘要与原件ZIP索引](test-evidence/2026-10-21-core-selection-fixed-signoff.json)。此次后续提交仅证据/报告，无产品、测试、依赖或锁文件变化；未重复Provider/Codec/交叉构建。

下一项具体建议（尚未实施）：SPEC13.1/P7-01的正常主浮窗冷启动单图预览。当前ExplorerCommand发出 `--prepare-image`，Program仍把它送到P0 ImageProbeWindow；应先独审正常安装会话中generation/launch/request绑定、原任务清理与取消/迟到/替换隔离，再复用现有文件Worker/独立file owner、GIF首帧标注和预览确认进入正常Shell。保持默认Off和显式启用/发送确认；已有实例的认证转交需单独设计，现有Show广播不可证明请求权限，不直接绕过安装受控子进程。可先做纯状态/IPC与交叉构建，不借此声称真实菜单已验收。

Windows真实WPF/OLE/OCR、安装来源身份/现代菜单、画质、250/600ms、600MiB及完整P6/P7/106用例/Beta仍NotRun或沿各自旧状态；不以本次Core通过代替。PackageHold、生产图片复用Off、WebP奇数payload拒绝及来源/密码/完整性/原SHA/实际退出/发送门保持。无新权限/费用/凭据/依赖许可、真实模型/用户内容/用户电脑、CI、main修改、强推或部署。
