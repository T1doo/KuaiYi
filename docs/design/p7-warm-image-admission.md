# P7-01 已运行实例单图认证转交：最小设计候选

状态：**DesignReview / 未实施**。固定基线 `71f69225accb8978160f59481414621ea312c466`，`dev/p7-fixed-signoff`，开始时单worktree、dirty0、远端同SHA/0ahead/0behind；远端main为 `51d077481cf6303602fcf5d2718213da9cf05178`。前轮固定9877完整Core2093P只属于其已封存代码树；本轮不重跑测试、不新增功能或宣称Windows验收。

依据：SPEC3/7.1/12.4/13.1/13.4/14、当前冷启动接线及文件所有权实现。目标仅为一个正常安装controller仍持有原App子进程时，下一次显式 `--prepare-image <单一本地路径>` 进入该App的原文件预览。默认Off，不自动OCR、启用或发送；不扩展截图、位图、虚拟文件、多图、浏览器角色/权限、升级提交后失去controller的runtime、跨会话或无人值守入口。

## 1. 当前真实缺口与最小拓扑

`Program` 普通安装入口先走 `InstallationSessionEntry`；`RunActiveSessionAsync`持有admission/execution、原Start返回的Role.Process及stdio直到实际退出。再次调用只能遇到Busy并Show后返回3。Show不带受信请求，也不是授权。`InstallationApplicationClient.ImageSent`、child的 `imageRequest`/`imageStop` 是cold IMAGE一次消费状态，不能清空或重新使用。

新增的外部连接只到**原常驻controller**，不寻找/接管或终止现有PID。controller仅沿其同一原stdio将独立warm请求送给它实际启动并持续持有的App子进程。child不新增外部监听。浏览器NativeHost管道/allowlist完全不复用。

只有normal会话READY且live Role、admission、execution仍有效时，coordinator内部才能创建一个不可序列化的live-session capability并启动监听。capability绑定同一Role/Process/原健康Task/launch proof/租约对象，提供受界Verify/Reserve/Commit/CancelAndJoin；不能由任意PID、proof字节、文件名或锁冲突构造。升级、handoff、关闭、控制失联及非安装开发布局无此capability。冷启动IMAGE的原一次性流程先结束登记，随后才开放warm端点。

## 2. Windows真实peer与安装身份

端点名为协议版本+当前用户/session scope+规范安装root摘要，仅作发现。实际pipe使用本机 `\\.\pipe\...`、duplex/overlapped/first-instance、maxInstances=1、拒绝远程；显式DACL只给当前logon SID必要的连接读写权限，创建者保留本次原server handle，descriptor不以同SID的GA授权把FILE_CREATE_PIPE_INSTANCE又给客户端。不使用默认DACL/Everyone、SYSTEM服务、提权或改变系统安全设置。同一原server handle在会话内Disconnect/重新等待下一连接，避免每请求重建留下名称抢占空档。绑定失败不接管未知端点、不自动改名降级、不重试自旋；具体descriptor权限兼容性仍需Windows合成实测。

双方在发送路径前各自从**当前已连接pipe handle**取对端PID（server查client、client查server），以QUERY_LIMITED_INFORMATION|SYNCHRONIZE打开并持续持有实际process handle。该handle仅用于核验和等待/观察，不授予kill/所有权。核创建时间、仍存活、实际token User SID、logon SID/AuthenticationId、TokenSessionId与OS pipe session、TokenIntegrityLevel/TokenElevation、非AppContainer、规范映像路径；失败或access denied即拒绝。最小策略是双方同当前用户、同logon、同Windows session、同medium且未提升，不接受low/high/其他账户或session。每次关键接纳及耗时验证后重核原pipe关联与同一held handle/创建身份，不重新按PID认领，防PID复用。

两端预期角色均是**当前选中generation的确切 `KuaiYi.App.exe`**，但角色权限不同：requester只能一次SubmitImagePath；server的controller权限来自上述live对象。各自从自己固定安装root读取并验证选中generation完整catalog/role路径和已有完整性策略；peer提供的Root/Path/Generation不可决定策略。完整包hash走受保留后台Task，前后重查有界current/owner/accepted/pending记录。controller还核same original child仍健康未退出、其实际路径/创建身份、原launch、accepted记录，以及自己真正持有的两把租约；child独立核原Proof.RequireImageRecords。请求者仅见“锁被占用”不足以确认server拥有它；server必须由唯一合法normal代码路径的live capability创建，且连接peer映像确为已验证产品角色。此处不宣传抵御已完全控制同账户或注入产品进程的攻击，也不能证明用户确实从Explorer点击菜单。

Windows adapter放App；平台无关策略/状态机放Core。现有WindowsNativePeer只示范server-side handle/SID/path核验，缺server对端与IL/logon等检查，不能直接当成双向认证完成。使用已有Win32 API/托管库，不加NuGet依赖或新SDK许可。client以identification级SQOS连接，不允许server借本功能任意模拟客户端；不依赖impersonation执行文件读取。

## 3. 有界协议、一次消费与UI stamp

外部pipe复用Contracts.IpcFrameCodec的28字节固定头/版本/unsigned长度先验与64KiB JSON上限，仅接受Ping握手阶段和SubmitImagePath一个业务命令；阶段/方向白名单必须在读payload前拒绝未知或BinaryChunk等类型（必要时给codec加不放宽旧调用的受限类型参数）。握手的hello/challenge/拒绝/结果是该命令的协议阶段，不增加任意文件读取/执行/URL等业务。JSON严格字段、UTF8长度、深度、规范编码，路径仍经原OleImageFileDrop.ValidatePath；不在握手期间读图片。stdio继续原私有ASCII编码，新WARM_*使用单独canonical JSON≤32768 UTF8字节、规范Base64≤49152字节且整行<65536字节的消息，先验后分配/解码；不改变原cold字节协议。

一个controller全局**一个warm slot、零队列**，每连接一个request；身份/握手读也只一个有界连接，不创建无限Task或seen集合。请求ID由server生成，另有server本launch单调递增ordinal（溢出拒绝）和双方各32随机字节nonce。所有grant绑定连接、原held双peer身份、generation、live owner/launch、server request/ordinal、两nonce；client不能用旧requestId重新申请。结束/拒绝/取消都烧掉该ordinal，保留最高消费序号与当前slot即可；new connection旧grant无效。随机challenge与fingerprint只关联/防重放，不替代OS peer认证。

流程：

1. requester先从自己安装root验证当前generation/角色，连接固定端点并核真实server；server核真实client/live capability。认证前不发路径。Hello只给版本/新client nonce，header的非空GUID仅为client hello correlation，不是接纳request；Challenge另给server生成的request，后续header/body必须一致。Hello不能选择root或child。
2. server生成request/ordinal/server nonce并通过原stdio发送 `WARM_RESERVE`。child在独立warm slot记录该身份，排队UI健康/busy检查；锁屏/关闭/设置模态/来源任务或清理未确认即拒绝。若只是隐藏，可在已认证显式请求下沿原ShowOverlay显示本地Off浮窗，确认可见后再冻结stamp；不靠Show消息授权、不解锁、不Toggle。stamp包含原Epoch/Activation/Input/Result/SettingsVersion，保留在child内存；返回不透明grant nonce和ExpectedEpoch。reserve只冻结，不创建Worker、不替换来源。
3. server重核peer/原Role/记录后将challenge+grant+ExpectedEpoch发给同一client。client核generation/原peer仍同一，发送**一次**SubmitImagePath，原样绑定全部身份/grant/epoch和一个路径。server以同slot原子消费，重复、改ID、改grant/epoch、跨连接、过期均拒绝；不能替换已消费path。
4. child收到独立 `WARM_COMMIT`，以same slot原子消费，比较原完整UI stamp与current live记录/取消token，后台验证完整包，复用StartInstalledImage/StartImagePreparation的原安全打开→Worker→真实finalize→AcceptFileImage。连续current permit与原ImageAdmission双重拒迟到。正常结果沿原source版本和GIF可信首帧标注进入预览。新请求不能夺取未结算旧owner；原已结束来源只按既有显式文件来源替换规则处理。
5. client连接保持到原准备/验证/Worker清理得到明确终态，返回 `PreviewReady` / `Rejected` / `CleanupUnconfirmed` 的受限结果。Queued仅代表消费/排队。`PreviewReady`必须由child基于同request已实际Accept且Worker精确finalize成功产生；不得根据Task正常返回推断（现有LoadPreparedImage会正常返回拒绝/取消/报告错误）。需要一个小的warm专用结果回报，不重写通用所有者。

每次授予使用controller和child各自TimeProvider的单调截止，拟定**10秒reserve→commit授予窗口**（在两端记录本地开始时间，提交消费后不再按grant过期取消已接纳准备）；wire不信任client UTC，子进程不比较跨进程timestamp。握手/认证另用10秒本地上限；新增prepare阶段以同一原请求的15秒单调上限覆盖完整包校验和文件准备，Worker自身原15秒通信/进程预算继续保持，两者均只撤销不伪称退出；不能另起无界验证。grant到期与提交消费在锁内仲裁；prepare并非grant延长。250/600ms取词合同不变。grant到期/取消只能撤销接纳，不能宣告原Task、取消回调或进程已退出；清理未确认可超出该窗口继续占slot/阻止替换。设计测试用可控TimeProvider/受控deadline，所有新增timer实际经该provider创建，不能仅用其GetTimestamp却另调真实CancelAfter。

## 4. 撤销、断线、成功提交与退出门

每个warm slot单独CTS与缓存的CancelAsync Task，既不复用也不reset cold imageStop。每端的Reserve/Commit/Cancel/终态由该端状态锁仲裁，child消息仍由其原读循环排序；后台验证、Dispatcher与Worker均不能阻塞该读循环读取STOP/EOF。controller同一ChannelState的**所有**START/IMAGE/OFFER/WARM/STOP写使用同一串行写门；原Output仅一个reader，按request/ordinal把WARM结果送到唯一有界slot的completion，不能让多个Receipt并行抢Lines.Reader；CLOSING/EXIT_READY原特殊处理优先并保留。原Role.Track只新增一个贯穿会话的endpoint复合Task；其内部有界registry精确持有当前slot的原连接/验证/取消/dispatch任务，全部确认settled并观察异常后才能退休并清slot。不能把每次warm任务永久追加到原RetainTasks=true的Role列表而造成无界增长。child原settlement同样join该当前slot，历史只保留高水位序号与有界错误类别，不保留图片/路径/无界Task历史。不用WaitAsync结束冒充原工作结束。写锁只跨同一次原write/flush，绝不跨receipt/hash/Dispatcher/Worker；取消先锁内置Revoked并启动原CancelAsync，再等待写CANCEL/STOP。RunActiveSession贯穿生命周期持有coordinator.serial，warm路径不能调用Enter/等待这把锁；它只使用该session capability自己的单slot锁与live permit。

认证失败、准备中连接EOF/peer退出、未消费grant到期或已消费准备的原deadline到期、UI epoch/设置/来源变化、锁屏隐藏、STOP、控制EOF/fault或升级意图均先关闭接纳并撤销same warm request。child缓存并join原取消回调Task、验证Task、原dispatch、原fileImageTask及原fileImageCleanup/Worker exit；超时/异常保持slot和原owner、报告CleanupUnconfirmed，拒绝下一请求，不能删未知锁或kill peer。

**成功边界必须明确**：只在原Worker finalize成功后，warm专用同步提交wrapper于UI线程持WarmSync锁，核same request/Preparing/原token与原UI admission，再在该锁内调用原AcceptFileImage；成功后同步记录实际sourceId/version及AppOwned，失败记录Rejected。不得先发布再取锁记终态。完整包验证在保留的后台Task；其后、wrapper之前立即执行有界Proof.RequireImageRecords/当前记录与租约复核（允许沿既有UI有界校验，绝不完整包hash），不跨await再进入wrapper。记录检查失败不调用Accept。wrapper内的warm current只读该slot的volatile不可变permit/原UI admission，不调用旧control.IsImageCurrent或RequireRecords/RequireImageRecords；memory permit版本若已被quiesce/撤销改变则拒绝。若原Accept内部有间接IO或render异常，实施源码独审必须逐调用核实；异常只清理同candidate image/sourceId，保全正文和清理错误，未确认不报PreviewReady或清slot。后台EOF/expiry/close先取同WarmSync锁，只有未提交时才能置Revoked并在锁外启动缓存的CancelAsync；已AppOwned则不再取消它的原warm token。锁内无await/包或文件IO/跨线程Dispatcher调用。锁顺序只允许WarmSync→RuntimeGate内部锁，普通current查询用不可变/volatile快照，不从Gate持锁回调反向取WarmSync。取消与提交以这个本地锁的先后为准；跨进程断线只以原reader实际观察并递交撤销的顺序生效，不声称知道远端未被观察到的退出瞬间。提交前收到撤销则禁止发布并清零原图；提交后收到旧连接断线/取消，不清掉已经归App来源生命周期的预览，更不能清掉新source。终态收据丢失对client是结果不确定，request已烧掉、禁止自动重放/重启cold请求；不能靠“重发同路径”凑成功。提交前即使已Queue也未脱离warm撤销权。终态已发生时WriteAsync成功并不证明client收到；仅child本地AppOwned决定所有权，controller记录其同request终态而不自行推断。若该wrapper无法保持原Accept契约与锁顺序，实施前必须重审，不能留先发布后判断撤销的空档。

自然UI关闭使用明确的早期私有phase **WARM_QUIESCE**：child在StopInstalledImageDispatch时先封本地warm登记/撤销未提交slot，发出并持有该单向写Task；不等待controller回执或endpoint退出。controller唯一Output reader一收到此phase即同步封live permit、取消endpoint接纳/连接读，绝不在reader内等待join。child独立join其原warm验证/dispatch/file/取消/Worker任务，并发送同request的WARM_RESULT(Cancelled/Rejected/CleanupUnconfirmed)，随后才运行原SettleForExit的CLOSING/EXIT_READY。controller endpoint registry据原result和自身原IO结算；其结束由controller在child实际退出/原IO结算路径join。两端分别join自己任务，child不await server endpoint、server reader不await child cleanup，故无child→server→child等待环。旧/故障peer不能提供早期phase时按CLOSING/EOF关闭入口并保留不确定门，不据此伪造warm成功。

正常App关闭时：先封warm登记→server停止接纳/断开等待→撤销未提交slot→join原连接read/write/peer验证/callback/dispatch/file owner→原control settle及child真实退出/原IO结算→controller原FinishLaunch/execution.Finish。失败catch同样join新增原任务后才能清lease，未确认保留原安全门。原STOP不因warm串行操作被屏蔽；新客户端断线仅撤销它的请求，不能关闭整个已有App。升级/handoff未开放warm端点，已运行normal会话未实际退出及清理前仍阻挡升级。

## 5. 最小生产修改面（拟议，尚未改）

| 文件/层 | 必要变化 |
| --- | --- |
| Core新增InstallationWarmImageAdmission/Offer | 单slot、ordinal/nonce/grant/单调deadline、不可重放状态机；严格受界编码；live capability不能由claims构造 |
| InstallationUpgradeCoordinator | 仅normal健康Role创建capability/endpoint生命周期；持有并join全部新增原Task，关闭先封入口；不扩展RoleNames/升级adopt |
| InstallationApplicationControl（含client） | 独立WARM_RESERVE/COMMIT/CANCEL/结果；每请求CTS/原结算；一个Output demux/全写串行；保留ImageSent/imageRequest原one-shot，并在首次warm reserve后永久禁止后到cold IMAGE/OFFER |
| App新增WindowsInstallationImagePipe | local-only/first-instance/显式SID与logon ACL、双向真实handle/token/IL/会话/路径验证、保留验证Task；复用已有包校验，错误不降级 |
| Program / InstallationSessionEntry | 仅明确单图命令尝试warm握手；只有明确NoEndpoint且原cold入口随后实际获admission/execution并通过原无owner残留门时才能cold启动，发现端点不存在本身不授予启动权。其间另一实例赢得租约则Busy拒绝。Busy/timeout/untrusted/失联/不确定结果均不得自动cold fallback、Show传path或接管owner |
| InstallationEntry / InstalledImageCommands / FileImageCommands | 新warm UI reserve与原stamp/current permit、明确Accept结果及原任务/owner结算回报；冷入口不改语义；退出join新增dispatch。为可靠报告仅加最小可选结果hook及warm同步提交wrapper，不扩通用处理能力 |
| Contracts.IpcFrameCodec（若需要） | 可选每端点消息类型限制，在分配payload前拒绝；旧协议长度/版本不放宽，不新增依赖 |

无需改ExplorerCommand、注册表/manifest、安装器、浏览器扩展、图片格式解码、Worker协议、OCR、发送门或模型调用。旧generation客户端只拒绝，不替用户启动升级或旧版本。首次最小交付不支持已经完成runtime handoff的无controller实例，该边界需UI明确提示重新启动正常会话，不能借发现PID补齐。

## 6. 有意义的针对验证（拟议，未执行）

| 层 | 必需反例/屏障与正确结果 |
| --- | --- |
| 连接身份 | 两方向错误peer、合法路径错误进程创建身份/PID复用、同SID不同session/logon、low/high/elevated/AppContainer、server冒名占管道、远程客户端、API拒绝；path发送前拒绝，无Worker/无Show授权/不操作对方进程 |
| 安装live身份 | 旧generation、hash/role路径不符、current/owner/accepted变更、pending upgrade/handoff、无原Role或仅文件锁冲突、child已退/原IO fault；后台核验hold期间改变记录/peer后必须再拒绝 |
| 一次消费 | 相同/不同request替换、重复Submit、重复cold IMAGE、warm之后cold/OFFER、跨连接旧challenge、旧ordinal、错误nonce/epoch、grant10秒刚前/恰到/刚后（prepare沿原15秒，不被已消费grant反杀）、序号溢出、oversize/unknown类型/截断帧；不能开始第二任务/无界存储 |
| UI隔离 | reserve后取消/隐藏/锁屏/设置/source/result改变、迟到Dispatcher/loader/校验返回，原cold任务未结算、新请求并发busy；原source不被旧回调覆盖，Off本地GIF预览保留首帧标签，OCR/HTTP调用计数0 |
| 断线/原任务 | 分别在认证/等reserve/提交前/验证hold/Worker hold断开；缓存同CancelAsync，原read与verify未返、callback被hold、TryFinalize=false时slot/owner仍持有、下一请求被拒；释放精确屏障后才清，body+cleanup双异常保全 |
| 提交边界 | AppOwned之前取消必不publish；提交先赢后的收据丢失只报告不确定且不可重试，旧取消不能清同AppOwned或后来source；PreviewReady要求实际Accept+原Worker退出而不是Task成功返回 |
| 控制退出 | warm回包与STOP并发不交错写/不抢receipt；CLOSING及时处理，join所有原read/write/verify/cancel/UI/file/实际child IO后才释放launch与lease；hold任一原任务必须阻止提前finish |

Linux可先验证纯状态/受界编解码、合成安装目录+真实合成子进程的原stdio与确定性清理屏障，Provider以现有生产文件命令链接验证UI stamp/明确结果，App Windows目标只交叉构建。**不**把合成peer凭据当Windows身份验收。真实Windows需单列执行双向pipe process/token/logon/IL与防占管道、原角色安装完整性、现代Explorer→再次CLI→同一WPF→实际安全文件handle/Worker/取消清理链；本环境尚未执行，也不操作用户电脑。已有免费Windows CI只有后续确认其现有授权/能力后再单列方案，本轮不运行或新增CI。

## 7. 可实施顺序与审查边界

A：先实现Core单slot/phase/有界编码与唯一reader/统一writer、真实合成child的确定性断线/取消/held清理回归；生产外部入口不启用任何synthetic身份。B：补Windows双向身份adapter和仅normal live-session endpoint，再接上述Shell wrapper与原文件链，交叉构建及Provider生产源码链接回归。两步均先独审最终差异再推进；A不是完整warm产品交付，B无真实Windows证据仍不能标P7-01 Pass。无新增外围协议框架、自动修复、服务或全局权限工作。

## 8. 官方依据与本轮边界

本候选只交可实施设计；生产src/tests/工具、SPEC/依赖/锁/权限均未改。独审重点为真实server bootstrap、原Role capability与对端路径/完整性结合，跨进程撤销和AppOwned的单一提交边界、原stdio reader不阻塞及无多reader抢收据。若这些无法在最小修改面闭合，应先缩小交付，不用指纹/Show/第二次cold IMAGE代替。

已只读查Microsoft官方文档，作为拟用API的依据，不是实机实验：[server PID](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeserverprocessid)、[client PID](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid)、[pipe安全/权限与logon SID](https://learn.microsoft.com/en-us/windows/win32/ipc/named-pipe-security-and-access-rights)、[first instance/overlapped/reject remote](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createnamedpipea)、[token查询](https://learn.microsoft.com/en-us/windows/win32/api/securitybaseapi/nf-securitybaseapi-gettokeninformation)。这些API只提供证据部件；完整认证策略与失败关闭是本设计的工程推导，不能由单个PID或挑战回显推出权限。
