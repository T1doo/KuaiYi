# 当前验收报告（P0 独立切片）

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
