# 开发进度

2026-10-23 晚间桌面精修候选：白浅蓝原生设置工作台/分组偏好行/焦点与状态反馈，原浮窗同HWND生长链保持；底板圆角与同帧原生裁切半径统一。快译无任何吉祥物。Core相关109P、Provider配色2P，非全套/非Windows。产品b89a925固定App交叉0W/E，原件Library libfile_ec54e1fd2a74819192bd5d1918ec5f52/v0。当前file://预览被Chromium组织策略阻断，不能签视觉或动态验收；未改原动效220/180/70ms和250/600ms门。见[范围、动效实现和明确未验](validation/desktop-refinement-2026-10-23.md)。

2026-10-21 额外server操作性实测独审限定ACK：源ef51719单次Windows run39572086629 success，两个普通继承token的独立child在无client／调用者自己持bit4请求client时，第二CreateNamedPipe去FIRST且max2均返回invalid/Win32 5；首server原handle全程持有，owner/user/logon/session只读核对及全部原IO/child/handle结算成功。仅Pass_LimitedTwoDeniedAttempts，不回填旧客户端bit4实授红、不宣告无风险/跨身份安全。无客户端测量/图片链/UI/Worker重复，产品/锁/系统权限不改。原件Library `libfile_6b161863e82481919114e39599f68fd9`，medium warm仍Blocked。见 [操作性原件与边界](validation/server-instance-windows.md)。

2026-10-21 管道权限实测独审ACK：源9d90a1f的单次Windows run39567803017确认原客户端请求/实授0x12019f、bit4=true，安全红门保持，整轮failure/Blocked/Fail/exit3；未解释无风险或断定生产DACL漏洞。独立exact-mask段实授0x12019b不含bit4，双方真实PID/session匹配及四项Elevated/组合负例通过，两段全部原清理确认。仅测客户端实授，额外服务器实例/跨身份DACL拒绝未验；无UI/Worker/图片链重复。原件Library `libfile_98055d2e0c7881919817c99b05a88e31`，产品/锁不改。见 [实际观测与安全红门](validation/warm-pipe-access-windows.md)。

2026-10-21 Windows 实际分段独审 ACK：源10d5的 run39565330615 中，三例真实安全文件→Worker→默认同进程Shell WPF预览、GIF可见首帧与原内存/位图清零、原关闭join均Pass；首次实现了此限定真实链证据。整轮仍failure/exit3：客户端bit4实验得到有效句柄触发保留断言，未查GrantedAccess，不能据此推导服务器权限或跨身份访问。High Current拒绝、原DACL结构与second bind Win32 5通过；后续双方peer分支NotRun。两轮红件完整保存到Library `libfile_c32e11ee722c8191b4303512b346a51e`，不循环刷绿。固定92e产品与锁不改，warm/Explorer/安装/250600ms/画质仍未验。见 [实际分段和原件](validation/warm-components-windows.md)。

2026-10-21 最新接续：B 固定产品源码仍为 `92e3548845bd07ee7717b53df9588c412d128f9d`，完整证据 `ab3bf269f956a7b19f6d8ead1383ff80495ad95c` 已独审。新增五文件只读前置检查独审后一次投递 `e36c92f8c978cd6871e3bbc30a99623d60ef9b64`，免费标准 Windows run39559953411/job119088533514实际 success；原始日志48,731B与唯一JSON已回收独审。已观测桌面前置项，但当前token High12288/elevated，linked查询1312未取得受限token，当前正向warm执行入口未满足；Same* false为未比较而非身份不匹配。未改生产代码/依赖/安全权限，未执行 SDK/App，产品链路全部NotRun，不将 prerequisite 当认证或 P7 验收。源码 CRLF 摘要、证据及下一分段路径见 [Windows 前置检查](validation/warm-image-windows.md)。

日期：2026-10-11。分支：`dev/p0-foundations`；初始完整 HEAD：`51d077481cf6303602fcf5d2718213da9cf05178`。

P0 整体：**InProgress，真实 Windows 退出门槛 Blocked**。后续整阶段均未验收。

原始产品书与 SPEC 均为 1826 行、146706 字节，SHA256 `ea3aa6529405bcc703986988fd089f476ee8f991d867e2220b3b20c439126166`；保留原文不覆盖，176 个追踪条目完整。

| 任务 | 当前实际工作 | 实现状态 | 验证状态 |
|---|---|---|---|
| P0-01 | 开发分支、SPEC/AGENTS、精确 SDK、集中最小测试依赖、锁文件、检测/构建/测试脚本 | InProgress | Linux 部分 Pass；完整 Windows 工具链 Blocked |
| P0-02 | 实际 UIA MTA Broker、限定窗口搜索、父进程硬终止、公开文件选区比对探针 | InProgress | 交叉编译 Pass；实际应用选区运行 Blocked |
| P0-03 | 实际非激活/拖动 HWND 生长、真实 OLE 探针及App HWND/远端native目标/独立注册STA候选；E06实机gate未过 | InProgress | 交叉编译 Pass；Windows HWND/OLE 实测 Blocked |
| P0-04 | 强类型布局、合法方向/平衡、帧级边界、单调高度跟随、实际 HWND 帧采样代码 | InProgress | 10000 组纯逻辑 Pass；混合 DPI HWND 测量 Blocked |
| P0-05 | 独立Worker/原生OCR与Shell源码、双EXE身份、开发打包/安装态脚本 | InProgress | 托管/协议/静态部分Pass；原生构建、Windows安装/OCR/菜单Blocked |
| P0-06 | 双协议真实本机 HTTP/SSE、分片/错误/断流/取消、IPC/Native Messaging、独立 stdio echo、图像重组 | InProgress | 离线协议部分 Pass；截图/浏览器实际传图未运行 |
| P1-03（独立部分） | 配置草稿、原子保存、迁移/恢复、位置纯函数、设置接线 | InProgress | 可移植部分 Pass；Windows界面/位置真实接线 Blocked |
| P1-04（独立部分） | 秘密引用事务、掩码输入、当前用户DPAPI/ACL源码 | InProgress | 合成加密文件部分 Pass；Windows DPAPI/ACL NotRun |

本轮还完成独立的启停/暂停/模式/请求身份门控、精确 origin 授权、七份原文提示词资源；属于可独立实现的基础合同，不表示 P1/P4 完整阶段通过。

## 真实运行结果

- `dotnet test KuaiYi.sln --no-restore` 首次编译暴露 IPC int→uint 错误，修复后 Core 22/22 通过。
- `pwsh -File scripts/bootstrap.ps1` 已运行；SDK/Pwsh 确认，WindowsDesktop=Blocked。
- `pwsh -File scripts/test.ps1 -Suite Offline`：Release 构建 0 警告/0 错误；Core **22/22**、Provider **35/35**，总 **57** 项，0 失败、0 跳过。
- 布局单项覆盖固定种子 10000 个合法拓扑/锚点/缩放及 210000 帧采样；这不是 10000 个独立 xUnit 用例，也不是实际 HWND/DPI 验收。
- 协议测试通过真实 TCP/ASP.NET 进程执行；独立 SSE 测试强制 1/2/3/7/4096 字节分片，覆盖中文、代理对、CR/LF/CRLF、BOM、多行 data 与未完整 EOF。
- 首次 Provider 测试 34 通过/1 失败：`explain@1` 后缀匹配误撞 `image-explain@1`。已改为精确逻辑资源名并完整重跑通过，没有删除或弱化测试。

## Windows 探针与第二次协议检查

- 首批完整提交 `60e894a3b3b6f5989fd89626098ce9aa6459e600` 已推送 `origin/dev/p0-foundations`，main 保持启动基线。
- 第二批 `test.ps1 -Suite Offline`：Core **45/45**、Provider **43/43**，共 **88**，0 失败/0 跳过。新增独立 stdio 进程、字节限界、部分帧、图像顺序/总量/摘要/截止、所有权清零，以及 Anthropic 开始/块/结束生命周期与思考隔离测试。
- `build.ps1 -Scope Managed` 与 `pack-probes.ps1` 实际运行：11 项托管工程 Release 0 警告/0 错误；三个 Windows 探针发布为框架依赖 PE32+ x64。ZIP 为 `artifacts/KuaiYi-P0-probes-win-x64.zip`，摘要见本轮 evidence；无签名或安装器。
- 初次 Broker 编译发现 WPF 隐式 using 不含 System.IO，补明确引用后通过。核查发现默认交叉构建 apphost 为 ELF，改为精确 win-x64 并在打包脚本校验 PE 签名/架构。
- 代码复核修复同 PID 多窗口候选、JSON 清零前数据所有权、拖动误触发点击、帧中磁盘写入。实际窗口读回与计划矩形分别记录；未执行的图形行为保持 NotRun。
- WinRT SDK 投影包对当前 Win32/WPF 探针不必要，其许可证元数据要求接受；已从有效依赖及输出移除，没有执行显式接受新协议。有效引用包版本/许可证与 NuGet 审计见 dependencies 与 test-evidence。原生 SDK/MSVC 仍 Blocked。

运行产物：`artifacts/test-results/offline/KuaiYi.Core.Tests.trx`、`KuaiYi.Provider.Tests.trx`；持久化的无隐私运行证据见 `docs/test-evidence/`。

## 监督方指出的迟到错误缺口（已修复）

- 以 `838377b466424eef28027f1890b17dcaf5943af8` 为基线，补确定性的真实 HTTP 屏障：等待响应头时换文/Off、非流式 body 中途换文后断开、旧 401/503 在新请求完成后返回。修复前双协议 **10/10 失败**，原始 TRX 与源摘要保留。之前 57/88 项没有覆盖这些路径，不能代表所有迟到错误已隔离。
- ProviderClient 的配置/发送/HTTP 状态/非流式读取/内容类型错误统一先复核身份；响应头到达后立即阻止旧身份继续重试。复核还发现截断 delta 发出后紧邻的限额错误必须再检查，已补真实双协议测试。当前身份限额错误仍明确返回，不吞正常失败。
- 增加完整 identity 信封与统一接收端，实际目标队列执行时原子复核 session/version/request/epoch；测试五种事件、四个身份字段、Off/Stop/Lock/取消、回调线程与异常，以及真实双协议 A/B 全链路排队后旧事件全部丢弃。
- 最新本地 `test.ps1 -Suite Offline`：Core **45/45** + Provider **73/73** = **118/118**，0 失败、0 跳过。11 项托管交叉构建/探针打包已同步复核。GitHub CI NotRun，所有 Windows/A—L 完整验收状态保持不变。
- 新增截断测试曾 1 项失败：期望遗漏 Anthropic message_start 的合法 Usage；已改为协议对应的精确事件序列并断言 input_tokens=12，保留该失败 TRX，没有删测试或放宽旧身份隔离断言。

详细路径复核及证据见 late-event-review.md、test-evidence/2026-10-11-late-events-summary.json；正式 App 尚未接入，不能宣称 Windows 界面串流实测通过。

## 分片外壳与默认取消边界（已修复）

- 信封入口自动在身份锁内关联 Gate/调用方取消；默认 caller token 为空时，Off/Stop/Lock/换文也会终止等待响应头的 HTTP。四项真实服务端屏障测试不释放响应，只靠实际取消使请求结束。
- 监督方发现 192KiB 原始 chunk→base64=262144 字节，JSON 外壳溢出。新增完整帧到重组用例 **修复前 1/1 失败**；原始 TRX 与源码摘要保留。现 raw chunk=190KiB、消息仍 256KiB、总输入仍 20MiB，不提高上限绕过。
- ImageChunkWire 完成规范外壳写入、严格解码后重组、失败清空；Native JSON 采用适合二进制管道的编码，避免 base64 '+' 被 HTML 转义膨胀，仍以最终序列化 UTF-8 字节判断完整消息。最大值/超1/全加号/random/最长合法 GUID/int 索引/非法长 ID/多字节头/完整 20MiB Commit 均验证。
- 新测试的 int/uint Assert 编译类型不匹配已修正；Unicode 测试曾假设 emoji 必须直接编码，但 .NET 可合法转为 surrogate escape。已改成真实 payload 字节长度、中文字节/字符差异与解析语义三重断言，保留该失败 TRX；协议字节上限与拒绝超限断言未削弱。
- SPEC 17.2 待测矩阵恢复六核心应用、18 个独立视图，含 Obsidian 源码/实时预览/阅读；SumatraPDF 仅扩展项。各文字视图计划至少20次动作，实际均0次/NotRun，版本/比率/证据为空，未声称支持或 ≥95% 已达标。
- 最新离线 **Core57/57 + Provider77/77 = 134/134**，0 失败/0 跳过；本轮证据见 protocol-boundaries-summary。Windows 与 GitHub CI 保持 NotRun/Blocked，生产捕获与完整产品仍未交付。

## P0-03 OLE / 非激活联合探针（本轮）

- 监督方已放行上一轮专项，按原 P0-03 继续；基线 de8aa6ebc834f419034797d845f51c6df38895ee 与远端开发分支一致，main 未动。新增两个真正 Windows 工程，解决方案现 13 项，未安装系统集成或更改真实凭据。
- 实际 IOleDropTarget / IOleDropSource / IDataObject / optional IDataObjectAsyncCapability、OleInitialize/消息泵、Register/Revoke、HGLOBAL 和 ReleaseStgMedium 已落地。Hover 只有限格式查询，Off/MOVE-only 不查格式/不取数；Drop 事务只消费一次。
- 同步来源无 async IID 也能在 Drop 生命周期内复制；async 必须 GetAsyncMode + StartOperation，成功后 origin STA 创建 marshal stream，释放原 IDataObject 引用后再排队独立 STA 解封送、取数和 EndOperation。原始接口从不在 ThreadPool 上调用；未消费包由 origin 清理；已尝试的失败 EndOperation 不重复，已 Start 但未交付 worker 的异常路径也尝试 End。
- 主监督 UI 与 OLE 宿主独立进程，私有继承管道含 instance/epoch/事务；冷启动10s，原生操作固定1500ms截止，进展不续期，Off 直接请求终止自有宿主，恢复仍 Off。TerminationRequested 不是实际退出证据；真实 Windows Kill/焦点/COM 代理均 NotRun。
- 独立真实 COM 公开故障来源含 Sync/Async/disabled/Start失败、MOVE-only、格式/取数/mode/async取数/async End 阻塞、End失败、超大/坏 UTF-16/无NUL；只记状态/时间/线程/计数，不持久化掉落文本，不接模型。Windows 实验脚本与所有设计假设见 ole-probe.md。
- 离线新增24项：COPY-only、同步回退、事务、不可续期截止与严格 Unicode；总 Core81 + Provider77 = **158/158**，0失败/0跳过。13项目全托管交叉构建0警告/0错误，五个 Windows PE32+ x64 探针打包；最终原始证据见 ole-probe-summary。PowerShell 实验脚本解析通过，Linux 守卫明确阻止实际启动，没有生成 Windows Pass。
- 初次编译发现 SDK 的 Marshal.QueryInterface Guid 参数为 in，ref 触发 CS9191（warnings-as-errors）；已改正确传递方式，未禁用警告。复核还修复：按真实 HGLOBAL 大小填满无NUL故障、空 deadline Completion、已 Start 后失败清理、End失败不重复、COM Release 在排队 worker 之前、重入时固定 Drop ID 与冷启动前台采样。
- **未解决：同步 GetData 阻塞时子浮钮可能冻结。** 独立监督可响应不是 SPEC E06 的“浮钮同时可拖动”证据；P0-03 整体仍 Blocked/NotRun，E01—E06 和全部106整体用例均未验收。可靠 HTML/RTF/图片、生产 Gate/动画与真实编辑器/浏览器 Drop 仍待实现/实测。

## 清理终结与退出确认专项（本轮）

- 独审指出 b8568 的 QueryEnd 在 reference.Dispose 之前发出，同步 DropEnd 在 capability/reference 清理之前发出；确认属实。将接收规则提取到实际共用控制器后，旧终结 JSON 的 Query/Drop **2/2 失败**，原始 TRX/源码摘要保留，不以之前158项替代这两条路径。
- 发送端显式资源作用域，正常关闭Revoke/OleUninitialize也有独立受限组；终结只在全部自有COM引用、medium、stream、EndOperation与worker apartment清理完成后发出。失败只发非终结CleanupFailed，原截止保留；聚合清理与正文异常，不重复不明Release/End。移除回调参数RCW，借用原生指针显式AddRef并在本STA释放；跨STA仍正确封送。
- 接收端实际共用OleCallWatch检查终结确认位、请求/epoch/阶段与绝对截止；CleanupComplete只开放当前Drop的私有payload，不撤销截止，DropEnd最后撤销。清理失败、提前/旧格式/超时终结均不能恢复或应用结果。
- 恢复持有本任务实际创建的Process；只终止该子进程，异步等待最多2秒，以HasExited确认退出。失败/未确认时保留所有权并禁止重启/关闭监督窗口；迟退后可确认。新旧宿主不以Kill返回作交接，未打开外部PID或终止未知进程。
- 新增26项：20项资源/确认协议/退出状态与6项独立进程控制测试；Core107 + Provider77 = **184/184**，0失败/0跳过。14工程托管构建0警告/0错误，5个Windows PE32+ x64再次打包。实际进程测试运行在Linux，只证明共用协议和Linux进程退出，不是Windows COM或恢复UI验收。
- 故障来源/脚本支持Query/Drop/AsyncRelease Blocked/Failure；这是显式probe-only钩子，在真实release周围注入5秒延迟或错误，不能冒充真实第三方IUnknown.Release阻塞。原有GetData/async End来源故障也保留。Windows清理/ABI/退出/焦点实测仍NotRun，E06同步浮钮冻结缺口不变。
- 证据见 ole-cleanup-review.md 与 test-evidence/2026-10-11-ole-cleanup-summary.json；该专项45697474c1da60858d0967d18b66232f5c74f304已获监督方独审放行（不含Windows/ZIP复验）。下一可推进项为原P0-05身份包/ImageWorker OCR真实工程与安装态脚本准备；实际安装、证书信任和真实API未授权、未执行。

## P0-05 身份 / 独立 OCR / 现代菜单工程（本轮）

- 从已放行 `45697474c1da60858d0967d18b66232f5c74f304` 开始，dev/p0-foundations 单写者正常开发；远端main仍 `51d077481cf6303602fcf5d2718213da9cf05178`，原SPEC字节未变。完整核对7章、11—13章、P0-05及18章后实施，未把开发签名与正式发布条件混为一谈。
- 新增 Imaging、独立 ImageWorker 和明确标记的App P0审阅入口，方案现17托管工程。真实C++/WinRT OcrEngine / BitmapDecoder /语言能力/最大尺寸/词框调用已写入；独立包身份诊断、有限内存传输、请求身份、错误与取消/退出确认闭环已接通。主程序WinExe、Worker由父进程隐藏启动；主线程不做解码/OCR。
- 主程序与Worker各自嵌入KuaiYi / ImageWorker身份；实际读取两个PE的RT_MANIFEST id=1后核对Name/Publisher/AppId，变更真正资源中的AppId也会拒绝。只有编译静态身份，未假定已注册或继承。开发稀疏清单含原生SurrogateServer STA/IExplorerCommand的六类型注册；class factory、单选状态、固定相邻EXE待确认Invoke真实源码，不加载CLR或读取图片。
- 实际文件句柄验证包含网络盘/UNC/device/ADS、目录、leaf重解析、最终本地路径及有限读取；原生解码前后各检查20MiB/16384单边/24MP、引擎尺寸。P0只收PNG；规格完整SkiaSharp、EXIF/透明/缩放、其它格式与视觉路由仍待P6，未声称完整图片翻译或R12已完成。
- 本轮新增60自动化子项（含8项真实Linux Worker进程/协议负路径，其余52项策略/数据/边界/诊断），最终Core167 + Provider77 = **244/244**，0失败/0跳过。Linux平台实际返回UnsupportedPlatform，不是Windows OCR通过。17托管Release交叉构建0警告/0错误。GCC编译并执行真实Shell共用argv/路径14检查，Release也有效，不含COM。
- 源清单baseline+5种变更检查、两个实际EXE内嵌资源/GUI与Worker子系统、已验证20文件摘要的框架依赖审阅ZIP均有证据。首次全局RID还原引发NU1004，改为每个EXE本地RID和对应锁文件后成功。一次故障测试因缺XDG环境使pwsh崩溃，标为InvalidHarnessRuntimeCrash而非正确拒绝；重跑须exit=1且匹配具体错误，未仅按非零退出算通过。
- 新的开发打包、默认Validate注册、安装位置双EXE诊断与公开合成PNG实验脚本已准备。Linux守卫、缺原生DLL及Release缺正式交付失败均实际验证。**没有生成/签名/注册MSIX，没有生成/信任证书，没有系统菜单安装、Explorer重启、真实图片或API操作。** 开发MSIX/native构建实际阻塞是缺Windows SDK/MSVC/MakeAppx；正式签名仅是发布条件。
- 新ZIP `artifacts/KuaiYi-P0-identity-managed-win-x64.zip` SHA256 `be68fb2d28f2b0e00cb4bbb017e34199b6dd9be9acf0dfb3065e6dfb5d140055`，273487字节；缺两个原生DLL，不是可OCR的完整包或安装包。原五探针ZIP本轮未重新生成，历史证据保持原摘要。
- 完整证据见 test-evidence/2026-10-11-imageworker-summary.json、imageworker-identity.md与ADR0003。补Hello/Begin请求ID一致、Commit后非法控制的真实拒绝，结果等正常退出确认后才交付；OCR先完成不撤销输入终结检查。本轮待独审；Windows WinRT/ABI/语言/OCR/取消、文件句柄重解析、现代菜单、焦点、安装/升级/卸载仍NotRun，P0-05总体Blocked；106条整体验收均NotRun。原E06同步浮钮冻结缺口保持未解决。

## 监督收尾：图片事件一致性与真实后台启动

- 基线2c99f9ed76b99443dff9159d6b2627f0af0e6548：监督方已核244/17托管工程及原8项LinuxWorker证据；确认catch/finally/轮询状态检查不一致和Process.Start仍在首次await前的两项问题，按要求修复。
- Provider及图片共用Core.IdentityEventReceiver<T>，完整身份/Activation在目标队列原子复核；本地On复用RuntimeGate，不授予网络origin。实际App接入ImageUiWorkflow，成功/错误/取消/退出未确认统一交付，资源清理不以结果资格跳过，也不写旧状态。
- 真实Process.Start放入后台任务；15秒包含启动，取消等待保留pendingStart，晚建子进程由持有对象终止/2秒确认，晚失败无伪造子进程；未确认保持所有权/禁止关闭重启。未碰Windows桌面或安装/注册。
- 新增67项：64项状态/队列/任务屏障，本地配置/lock/origin与异常传播；3项实际启动边界，其中2项创建真实Linux Worker、1项真实启动尝试失败。独立模拟UI线程能在受控启动阻塞期间处理队列，不代表真实WPF桌面验证。A→Off/B→A迟到成功/失败/取消含入队前后两种时序；当前状态、新结果及写入计数全部保持。
- 最新Core234 + Provider77 = **311/311**，0失败/0跳过；Provider旧77项全部回归；17托管Release构建0警告/0错误。证据见image-ui-review.md及test-evidence/2026-10-11-image-ui-summary.json；新审阅ZIP另存identity-ui-fix名称，仍缺原生DLL，不是安装包。
- 本轮两项待独审，依监督方要求等待其余结论后再推进P0-06浏览器到NativeHost真实Begin/Chunk/Commit接线，不安装扩展或改注册。真实Windows UI/启动/退出、OCR/菜单/安装均NotRun，106整体仍NotRun，P0整体未完成、E06缺口未解。

## 监督收尾：已发布子进程与启动任务终结竞态

- 基线2609bf86c86f1466f24f30f25898c81811580b69：监督方确认311项主要修复，另指出ObserveExit提前true后UI DisposeAsync未处理ExitUnconfirmed。实际Process已发布/退出、启动任务被屏障保持的测试，旧谓词未改，修复前1/1失败；精确源码/插桩说明/原始TRX已保存。
- ObserveExit、ExitState、RunAsync finally统一要求启动/停止终结与退出确认；两处实际窗口改用TryFinalizeAsync，false不清空所有权、不允许新任务/关闭，严格DisposeAsync继续拒绝未知状态。清理错误仅存有限类型码，身份Gate未放宽。
- 新增19项均创建真实Linux诊断Worker：1项直接就绪门控，18项成功/失败/取消×当前/Off/B×finally/轮询含排队事件。模拟UI不是WPF证据。首轮测试清理错误假设释放屏障即启动已完成，19中16失败；保留失败证据，改为等待实际Task，全部断言和用例保留，修正后19通过。
- 最新Core253 + Provider77 = **330/330**，0失败/0跳过；17托管Release构建0警告/0错误。139源摘要、原始TRX/日志与20文件新独立finalization审阅ZIP校验见image-finalization-review.md及test-evidence/2026-10-11-image-finalization-summary.json。ZIP缺原生DLL、框架依赖，不是安装包。
- 本轮待监督独审；P0-06接线未提前开始。远端main保持51d077481cf6303602fcf5d2718213da9cf05178；SPEC字节未变。Windows启动/退出/WPF/OCR/原生菜单/安装均NotRun，106整体仍NotRun，P0/E06缺口不变。

## P0-06 浏览器到NativeHost分片闭环（本轮）

- cb54673dc9686059a5b5f632c23aebb42eb3e1ad清理专项已获监督独审放行；重新核对SPEC 11.3/12.4/13.2/14/P0-06及Chrome/Edge官方平台权限，新增19工程中的NativeHost与明确测试fixture，MV3仅四项权限，不安装、注册或新增账户/凭据。
- 显式菜单且桌面Ready后才取图，原图同源HTTPS/omit credentials/拒绝重定向；原图失败才验证唯一可见目标/DPR/滚动/位置后裁切，仅发送裁后PNG。frame/重复目标/导航/不能验证时拒绝并说明区域截图。真实捕获与桌面截图热键未验收。
- 实际生产BrowserSession连接共享ImageChunkWire，Offer/Begin/Chunk/Commit/Cancel、30秒截止、20MiB/190KiB/完整256KiB UTF-8、字段/身份/摘要/PNG头均接通。内部DesktopPipeChannel实际IpcFrameCodec连接流，完整grant绑定一次身份/nonce，验证IPC回复、独立拷贝JSON和分片摘要；没有模型/网络URL操作。
- 生产入口须Windows真实origin/当前用户注册/固定清单/11运行模块完整性/同用户同session已安装App角色；无fixture CLI。实际ID/注册/receipt/当前App授权发放端尚未就绪，明确拒绝，不能在真实浏览器使用；正确授权正路径只在测试authority上验证，未放宽生产条件。
- 首轮实际stdin超时40中1失败，补显式等待截止并观察迟到读取；首次观察诊断导致5个测试计数格式失败，已保留并修；真实IPC首轮51中32失败，定位JsonDocument引用清零帧，改独立副本，全部断言保留。原始失败和精确相关源快照见browser-summary。
- 新.NET51（44独立宿主进程/真实管道或正式入口负路径+7文件完整性）；Node31含真实扩展发送模块到宿主/管道三分片SHA闭环。当前Core304+Provider77=**381/381**，另Node**31/31**，零失败/跳过；19托管locked Release构建零警告/错误。仅框架依赖宿主/扩展18文件审阅ZIP，不含fixture/实际ID/信任receipt，不是安装包。
- 本轮待监督独审；原E06、截图DPI、Windows取图/注册/身份/安装仍NotRun，106整体NotRun、P0-06总体Blocked。main与SPEC未改；证据见browser-native-host.md及test-evidence/2026-10-11-browser-summary.json。

## 监督修正：浏览器截图像素归属

- c83dde56b58039d337bcb3ba9b15379bd7a71a58独审指出中心命中不能证明全矩形归属；旧实际measureImage在8合成DOM负例错误接受，原始8失败/源码保存，非真实浏览器或真实内容外传。
- 全面拒绝截图fallback，任何目标均在注入/截图/解码/发送前ScreenshotOwnershipUnverified；仅保留同源无凭据PNG原图，移除不使用的scripting权限，原数字边界测试不是可见性许可。
- Node43/43通过，新增9实际acquireImage拒绝与3实际Service Worker操作用例，失败只发Offer/Cancel，成功原图字节保持；.NET381源未变，本JS修复未重跑其测试。未安装注册/API/截图。
- origin参数、allowed_origins和文件摘要仅证明配置，不能证明浏览器来源；当前无生产receiver/配置持续拒绝。生产接入前须系统客户端角色校验和明确威胁模型，来源不可信须逐次桌面确认；本轮只记录设计，不开放。
- 截图路径未完成，Windows/106整体/E06不变。ReadBounded截止与底层完成竞态下一独立修复；本轮两项修完再独审。见browser-ownership-review.md和browser-ownership-summary。

## 监督修正：ReadBounded截止与完成资源竞态

- 基线截图修复7d176397d035f4409d105d1764683c6062d7dae8；旧catch的IsCompleted检查可跳过已完成文档/故障观察。测试只加内部屏障和TimeProvider，旧检查保留，6案例3失败/3通过，实际文档释放断言也失败；追加完成读取但截止已过的交付边界，7中4失败/3通过，精确源/TRX保存。
- 所有等待失败统一观察同一任务/using释放真实文档/记录有限异常；成功等待归正常using所有者，取得后复核deadline，过期不调用桌面Offer。不重读、不重复注册/交付/释放。测试屏障只有friend Core.Tests使用，生产默认System时间与30秒不变。
- 7确定性测试没有Sleep，真实Native Messaging解析文档覆盖success/实际截断fault/EOF及两种观察顺序；已完成结果遇过期也拒绝。模拟不可取消底层只用于测试，原真实Linux stdin/JSON/IPC均保留全回归。最新Core311+Provider77=**388/388**，另Node**43/43**；19托管locked Release零警告/错误。
- 新审阅ZIP同时含截图完整拒绝和读取修复，18文件逐项摘要；没有真实网页、API、安装注册。原始日志/JUnit空白保留，代码范围格式检查通过。源/证据见browser-read-ownership-review.md与browser-read-ownership-summary。
- origin参数/安装配置/文件摘要不认证浏览器启动者；系统客户端角色校验/逐次确认设计已记录，当前不开放生产receiver。两bug修毕待独审，Windows截图/来源/浏览器/安装、106整体及E06缺口不变。

## P0-06 生产 App 单次本地接收

- 基线7d2da9b693ccf00f0d6773931193ae5f4de58db3两项局部独审已放行。当前只实现代码和合成离线测试，未安装/注册/授真实权限/API调用。
- App只读安装配置、路径/注册/11模块摘要；缺配置不监听。原生CreateNamedPipeW用SID保护DACL、first-instance、overlapped、拒远程；持有实际PID进程句柄，核token用户/session/创建时间/实际路径/存活/模块，Offer/Begin/Commit复查。Windows调用NotRun，Linuxpeer是明确合成输入。
- 生产WPF逐次询问，拒绝默认/焦点；声明origin不作来源认证，已On仍确认，不自动代答或记住。完整单次grant绑定连接/operation/identity/nonce/30秒总截止，Begin/Commit/目标队列复查；Off/失联/替换/锁定/截止清零，旧失败不取消新请求，本地失败恢复Off。App新增始终可用的关闭清空按钮，处理锁屏与退出任务所有权。
- 本地模式禁止被动采集与模型发送，已有endpoint consent也不继承网络资格。仅保留本次内存PNG，无OCR/Provider接线/持久化；源认证仍未证明。
- 65新测试使用实际IPC/NamedPipe/接收状态机/发送端，只有peer/Decision/队列/时间受控，无Sleep；完整Core376+Provider77=453/453、Node43/43，19locked Release零警告/错误。最终自审补界面读取/后台Clear所有权锁，新增第65项并完整回归。源/原始TRX/打包证据见browser-app-receiver-review.md及summary。
- 最新App/宿主/扩展34文件审阅ZIP无fixture/config/receipt/native DLL，框架依赖非安装包。Windows真实核验/确认/浏览器/安装NotRun，106整体NotRun、P0截图/E06缺口不变，本轮待独审。

## 监督修正：确认等待的跨模块预算

- d25092acc907a16a7636dd95656fdac5a071da51独审指出：扩展Offer→Ready固定5秒，桌面明确确认30秒。旧生产代码搭配真实Native Messaging/IPC与合成确认，6秒等待1/1失败；完整基线源与原始日志保存。
- 新增单次ConfirmationPending，握手5/明确确认30/取图传输30/普通确认5/总65秒；均单调复核，重复通知不能续期。只在Ready后取图。宿主等待确认时监视Cancel/EOF，关闭接收通道，唯一观察迟到读取/授权。
- 新20项包括真实6秒等待、9个实际ServiceWorker→独立宿主→生产receiver场景及10个扩展边界。peer、回答、Chrome API和加速时钟是合成测试，不冒充Windows真实授权。拒绝/无回答/取消/Off/失联/过期允许/迟到Ready均不取图、不传图，清理后无保留。
- 中间扩展链路9中1失败系测试把Ready后正常Cancel的宿主exit0误期望3；保留原始失败，只改为该场景精确0，仍断言零取图/零Commit/清零。最终专项20/20，全回归Core376+Provider77=453/453、Node63/63，19托管locked Release零警告/错误。原65接收端/7读取竞态测试保留全部断言，仅更新明确阶段元数据/握手预算。
- 新34文件独立确认等待审阅ZIP，不含fixture/config/receipt/原生DLL，框架依赖非安装包。无安装/注册/实际确认/凭据/API调用。Windows/106整体仍NotRun，P0与E06缺口不变；此专项提交监督独审，未推进下一产品阶段。详见browser-confirmation-wait-review.md与summary。

## P0-06 用户主动区域截图切片

- 8d11a811c1813303d64bd9085b4b60e0f50ccd0c确认等待接线获监督限定签收。完整重读截图/门控/坐标/热键/任务/验收相关条款；现有Worker复用，无浏览器fallback开放，无模型/真实授权。
- 新C++/WinRT WGC首帧+明确GDI源码、逐屏HWND遮罩、物理鼠标/负坐标/混合DPI不缩放裁剪、内存无元数据PNG、本地预览及已有Worker接线。Off默认取消；On松开本地处理；热键明确注册Ctrl/Alt+A-Z子集且占用保留旧值。本轮不是完整设置/翻译产品。
- 原子截图租约暂停被动取词，取消不替换旧会话；锁屏/Off/屏幕或epoch变化阻止迟到捕获/预览/Worker结果。源缓冲、WPF位图、PNG临时及扩容旧内存清理；原生未返回时保留任务所有权，不声称后台线程可强制终止。
- 新38截图+2安装角色篡改测试，5 DPI逐像素/CRC/PNG解压、1000固定种子、边界/取消/实际分配缓冲清零/并发租约/晚到帧/真实独立Worker协议均通过。首次测试编译CS1503原始失败保留，仅修Uri输入类型；先32、再43专项通过。最终Core416+Provider77=493/493，Node63/63，19locked托管零警告/错误，身份源6静态子项通过。最后仅App可见owned-dialog保护/滚动/热键ID修改，再全托管构建/打包；共享逻辑测试无需重复。
- CaptureNative编译尝试CMake缺失退出127，MSVC/SDK/桌面同样缺失；NativeBuild Blocked，真实WGC/GDI/热键/多屏/自身排除/内存峰值NotRun。Screenshot总5秒及WGC首帧2秒不保证Map/驱动/BitBlt同步调用截止退出；CaptureHost隔离尚无，明确记录。
- 新截图ZIP27运行/资产+2原生源+manifest，缺Capture/OCR/Shell DLL/安装配置/receipt，框架依赖非安装包。现有浏览器角色摘要增13，缺配置持续拒绝。全106仍NotRun、P0 Blocked、E06缺口不变，提交专项监督独审。证据见screenshot-review.md和2026-10-11-screenshot-summary.json。

## 监督修正：截图进程隔离、隐藏元数据与 GDI 同步

- 64cba6584a85a011d1cbe87c78b009f949449f2b仅获几何/PNG/本地身份限定签收；App Dispatcher DwmFlush、进程内 Map/BitBlt 与无界 Closing 属另一个截图生命周期缺口，和 E06 同步OLE冻结分别记录。
- 实际 CaptureHost exe/生产私有stdio协议/自己启动的子进程监督已接入；复用严格启动settlement+实际exit机制，默认5秒单调流程/2秒退出预算，取消投递窗口恢复、Closing等待3秒。退出未确认保留owner并阻止新截图/OCR/浏览器确认；严重当前失败原子Off，旧身份失败不能影响新请求。未终结启动/退出不能假报已终止。
- 实际父进程与全部可见自身窗口、layered/WDA元数据核对；无法核实拒绝。P0窗口创建前AllowsTransparency/None并保留拖动/关闭。子进程DwmFlush不是父绘制fence，元数据不是实际像素排除证据；WPF/WDA兼容和自身消失仍Windows NotRun。
- 生产GDI读取/清零前同绘制线程GdiFlush且核返回；失败不CPU访问、清理失败拒绝输出。共享真实C++ helper合成7项通过，NDEBUG仍保留断言，故意读前未flush变体实际退出3。Windows真实GDI/WGC/DLL仍NotRun。
- 原有Worker启动/终结22项先通过；首轮专项30/31（测试清理访问已Dispose Process）保留失败后修正31/31；另有缺using/API拼写编译失败均原样保存，未削弱断言。扩展宿主23/23（真实child，栅格、阻塞、Kill/Wait故障为合成），新增2文件角色篡改。全回归Core441+Provider77=518/518，Node63/63；21托管locked Release零警告/错误，身份源6静态子项通过。全部旧测试保留。
- 新36文件CaptureHost审阅ZIP已生成并验实际x64 PE/PMv2/asInvoker，不含Fixture/配置/receipt/原生DLL，不是安装包。旧截图/确认ZIP摘要未变。角色要求13→17。完整源摘要、基线六文件、原始TRX/JUnit/构建/错误/变体证据见capture-host-review.md及2026-10-11-capture-host-summary.json。
- 原生工具链/Windows桌面仍缺，真实驱动卡顿与kill时延、界面取消恢复/多屏/自身像素排除未验证；106整体NotRun、P0 Blocked、E06未解。本专项提交监督独审，未进入后续阶段。

## E06 主浮钮与 OLE STA 分离候选（待独审）

- 421ecfe92a731e298ff94113989535e800bf0969获监督限定签收CaptureHost/GDI子项后，回到原E06；先写ADR0010论证标准封送/真实HWND/独立STA及捕获输入约束，再接线。App唯一浮钮与OLE接收进程分离，原生目标对象正确STA；无假按钮、代理覆盖或拒绝全部无async来源。
- 生产私有stdio/真实child/注册原任务监督和32字节父直接写Off门控已实现；保留旧严格启动/已发布退出但未settle/unknown owner门槛，失败不重用。关闭与启用串行；自身移动暂停采集，独立同意不由OCR On授予；不发模型。主App显示线程归属元数据以供Windows核对。
- 输入复核发现Raw Input后采当前cursor会误判原始外部按下，改为鼠标低层钩子原事件坐标仅确认新按下，Raw Input只移动/释放；不注入/消费源事件，无键盘/轨迹保存。此Win32接线仅编译，钩子时限/静默移除/注入输入与触摸/辅助技术为精确未验收限制。
- 最后P0评审OLE/OCR/截图/浏览器接收限定串行，避免共享状态串扰；自身DPI拖动保留手势，严重当前receiver失败撤销自身同意。以上最后UI接线只交叉编译，不虚构Windows测试。
- 新48项（接收/监督/手势21（19项真实child）、协议/Unicode/终端Off12、独立同意9、角色篡改6）；专项50/50另同意9/9，全部旧测试保留。全回归Core489+Provider77=566、Node63通过；最终原生线程元数据后Core489重跑通过；24托管locked Release零警告/错误。后端/registrar/门控/阻塞/kill/pointer为明确测试替代，不当Windows COM/UI证据。
- 保留CS7036、CS8602和首轮3失败原记录；按实际合同等原注册task、观察已断言的decode错误、对localOnly断言CanSend=false后通过。首次缺Linux targeting及archive保留文件名误判来自工具观察，日志被复用的明确标出，不伪造原日志。
- 新42文件独立审阅ZIP含39运行/资产、原生桥2源码和manifest；5实际x64 PE apphost与逐文件hash核对。无fixture/config/receipt/nativeDLL，不是安装包，缺桥DLL不能运行拖放；原三个ZIP摘要保持。角色要求17→23，无自动配置或注册。
- Windows/MSVC/桌面缺失：真实同进程跨STA HWND注册、远端COM来源、捕获期间自身输入/取消/Off、共享门控、DPI/焦点/帧时间、真正IUnknown Release故障仍NotRun；原探针release适配不冒充native Release。完整最小gate/运行入口/源码摘要/失败证据见ole-presentation-review.md与summary。
- E06 InProgress/NotRun，P0 Blocked、176/106保留。只在正常dev分支本地提交，按监督要求独审后再推送，不等待重复用户确认、不越过实机gate推进产品阶段。

## 当前边界与继续目标

当前没有可安装的完整主产品或已编译原生Capture/Shell/OCR DLL；已有CaptureHost监督源码和托管审阅产物，Windows实际角色/像素排除/驱动终止未验证；已有主动区域截图源码到本地Worker，但无真实捕获/热键/跨屏证据；有最小MV3扩展源码与宿主审阅分发，但已有生产App逐次本地授权接收代码，但没有实际浏览器安装/注册/授权闭环；有真实OCR/菜单源码及部分托管产物，但没有DPAPI、Windows OCR、真实跨应用与动画帧时间通过证据。真实 API 和质量人工评审未运行，未配置真实凭据。

本轮形成可交付审阅的 Windows 探针和离线协议切片。探针运行说明、实际限制和下一步真实矩阵见 windows-probes.md。P0 尚缺真实 UIA/焦点/DPI/OLE、截图、身份/OCR/菜单及原生工具链门槛；后续生产阶段不越过这些退出门槛。还需生产 coordinator/配置/DPAPI、broker ACL、授权复制、OLE、图片 Worker、浏览器/Explorer 和安装卸载链路。未实现项保持独立缺口，不能以探针 ZIP 代替用户要求的完整产品。

## E06 候选固定基线复核与原任务修复

- 固定d4fbc7e432247d5babd4a34f11d9b22012ec3af2先只读复核236源码/52证据摘要及UI、STA、owned child、共享Off、迟到注册/退出和LIFO清理。工程自查不等于监督独审或Windows验收。
- 修原Run尚未返回却过早释放、原delivery未终结却清空其真实缓冲区，最终本地落地与Cancel共锁核epoch；原Run/delivery/remote revoke未确认仍阻止复用。迟到失败被观察，记录有限类型码，无外部COM进入App呈现。
- 基线新增2例均失败，初修44/44、扩充45/45。新3个执行用例走实际child/生产stdio，注册器、映射和回调故障明确合成。首全回归491/492原queued断言遇清零续体调度竞态；保留失败证据，等待真实settlement并新增lease保留断言，旧断言全保留。
- 最终Core492+Provider77=569/569、Node63/63，24托管locked Release零警告/错误，身份源静态6/6。新717653字节/42文件审阅ZIP独立命名，旧4包摘要不变。不是安装包，缺原生桥DLL不能拖放。
- 原生工具链/Windows COM、真实注册与Release、鼠标捕获期间移动/Off、共享映射、关闭恢复、DPI/焦点/帧时间仍NotRun；176/106保留、P0 Blocked、E06 InProgress/NotRun。正常dev分支准备普通push供监督读取精确commit，main/部署/发布不操作；未推进下一阶段。完整原始证据与重跑方式见ole-lifetime-audit.md及test-evidence/2026-10-11-ole-lifetime-audit-summary.json。

## P1 配置与设置独立切片（P0未退出）

- 7c89618可移植生命周期获监督限定签收；E06/P0实机仍未过。现有合法环境只有Linux g++，无CMake/MSVC/Windows SDK；未安装或重试空探针。按遇阻继续独立工作授权实现配置部分，非阶段提前签收。
- AppSettings/Schema/默认值、不可变草稿、同目录刷新/替换与备份、主备字节摘要及共享锁、未知版本只读、有限已知schema0布局草稿迁移、明确损坏恢复/原字节保留已实现。
- 密文先存、引用journal、配置WriteId核对、提交后撤销/删除、备份不能复活旧引用；不能证明时保全并阻止继续。生产当前用户DPAPI及SID ACL真实P/Invoke只编译，未实机运行。测试AES-GCM/policy明确合成，无真实凭据。
- 正常WPF设置窗口接到App，草稿保存/取消、捕获暂停、Off不启用或发送、掩码新输入、多API档及基础偏好真实接线。全部IO后台。热键/授权修改不能绕过后续事务，P4高级/模型调用未连接。位置纯恢复函数已测，真实浮窗位置持久化/恢复仍未接线。
- 新45例含2真实独立child终止边界；最终Core537+Provider77=614、Node63，25托管locked Release零警告/错误，身份源6通过。首次Schema属性与WPF using错误日志保留，无删测试/弱断言。
- 新770609字节/42文件设置审阅ZIP独立命名，旧5包摘要保持；无真实配置、密钥、receipt、fixture或原生DLL，非安装包。Windows界面/DPAPI/ACL/磁盘满/位置、原E06全NotRun，176追踪/106整体NotRun保持，P0 Blocked、P1 InProgress。详见settings-review.md和2026-10-11-settings-summary.json。

## 设置恢复与共享引用监督修正

- 固定0db0267独审四项：恢复备份/默认值切换错配、非数值schema异常逃逸、保存中footer可变、共享SecretRef删一档撤销其他档。只修现有问题，没有推进新功能。
- 抽取同语义生产恢复所有者后首次50例41失败/9通过；等待原保存终结的测试清理及clear-only扩充后51例42失败/9通过；修复51/51。旧已提交日志再补1例先失败后修复，52项新增于全回归通过。原日志/源码/TRX与旧断言全部保留。
- 备份与默认草稿各自保留编辑/待存密钥，实际保存锁定当前来源；footer禁用且迟到事件有准入，失败实际返回再可编辑。所有JSON值类型、主备组合、取消/保存保全已测；共享引用保存与已提交journal恢复扣除仍使用集合，最后移除才撤销。
- 完整Core589+Provider77=666/666、Node63/63，25托管locked Release零警告/错误。新增2个真实child终止与原2个保留；protector/policy仍合成，Windows WPF/DPAPI/ACL未运行。
- 独立772856字节/42文件修复ZIP，六个历史包摘要不变。176追踪/106整体NotRun，P0 Blocked/P1 InProgress，待监督独审。证据见settings-recovery-review.md与2026-10-11-settings-recovery-summary.json。

## 共享秘密的profile绑定撤销与任务取消

- 固定9914620复审发现共享密文未退休时A旧绑定漏撤销。物理DeletedSecrets与RevokedProfileBindings分开；清除/删除/更换引用均发绑定通知，最后物理退休规则保留。
- 实际App SettingsRuntimeLink初始化/订阅/退出接线至定向RuntimeGate：当前profile/ref/GUID身份核对，A取消及迟到拒绝，B共享密文/On/原任务保留；本地OCR不受API删除影响。正式P4设置到协议和模型UI调度仍缺，不签D08整体。
- journal新schema2存通知，逐个尝试订阅者且异常不吞掉；已提交保留日志、证明恢复后重复通知；旧1可读、未来版本保全，未提交不通知。
- 基线14中8失败/6通过；初修14中3失败为新测试容器比较问题，改逐项内容比较保留原数量/值/取消断言；额外纯本地许可准入1例先失败后修复。扩充20/20，实际2child终止、实际linked原异步A取消/B保持，原日志全保留。
- 完整Core610+Provider77=687、Node63，25托管locked Release零警告/错误。777721字节/42文件独立审阅ZIP，7旧包摘要不变；合成protector/policy、Windows WPF/DPAPI/ACL与P0/E06未运行，176追踪/106整体NotRun。待监督独审，证据见settings-bindings-review.md和summary。

- 最后源码复核追加迟到初始化1例先失败后修复，在Gate最终锁内核对当前配置版本及Link关闭准入；最终21新例/Core610+Provider77=687/Node63。首20例及686回归/初包元数据作pre-stale-init原始证据保留。

## P1 实际启动骨架接线（监督限定继续）

- 正常启动为一个红色实体浮钮和原生托盘；旧P0面板显式 `--platform-review`。启动SettingsCoordinator元数据加载始终Off；无选择/剪贴板/网络/秘密解密。
- ShellLifecycle与严格RuntimeGate统一按钮/托盘启停、隐藏/锁屏/退出；EnableIntent最终锁核版本/epoch/绑定；正式API无绑定不能准入或发送。完整P3/P4尚未交付，无假翻译。
- 单实例当前Windows SID+SessionId命名Mutex；只无载荷Show通知。便携测试使用真实子进程与合成scope，竞争/正常退出/崩溃释放通过，Windows账户/DACL不冒充通过。
- 现有OverlayPositionMapper接入实际HWND/当前拓扑：存设备名/归一化锚点，恢复夹紧；DPI/任务栏/显示器移除代码接线，真实Windows多屏仍NotRun。位置事务先等待启动，与正常设置草稿打开串行。
- 新测试初20/20、扩23/23、再补同会话授权缓存1例，共24/24；全回归Core634+Provider77=711/711，Node63/63，无失败/跳过。25托管工程locked Release零警告/错误；三次开发编译错误及修复后日志保留。最后设置退出重复Close收紧后App/全托管重编通过，测试代码/核心行为未变。
- 环境断连提示后实际只读、已有会话结果及远端查询均成功；未另建写入者或删锁。本地fetch refspec只取main，开发分支以ls-remote实核。
- 独立审查包796238字节，SHA256 `96f031f13c1cb549b1da660497e935ab2ad7ad627d785cbe578659629eb183ba`，43文件及5真实PE32+ x64 apphost，逐文件manifest通过；非安装器、无原生DLL/真实凭据/注册。八旧审查ZIP摘要逐一未变。
- 176追踪项完整，106整体验收仍NotRun；P0/E06 Blocked、P1 InProgress。详见p1-shell-review.md、p1-shell-windows-checklist.md及本轮summary/raw证据。

- 最后补同一会话/域名许可复用，Off不重复弹说明；换域名和重启需重新确认。新增1例后重新全测Core634+Provider77=711/711、Node63/63、零失败/跳过，并再次locked托管构建与独立发布；旧23例/710回归和初包摘要另存pre-consent证据。

## P2 实体浮窗与跨DPI拖动（监督限定继续）

- 基线359cf26；断连通知后安全只读命令与构建会话实际成功，不用通知推定永久停工/恢复，不另建写入者。
- 修WM_DPICHANGED取消drag导致不保存：保留手势、Moved和初始抓取比例，废弃旧动画后整体卡片夹紧/重基；松手不误触On并用原位置事务保存。监视器消失/取消/锁屏安全收束。
- OverlayFrames/Results接实际ShellWindow/Canvas/TextBox/同HWND：220ms展开、180ms收起、80ms淡出，几何/opacity反向连续，50ms测量、冻结宽度/方向与移动尺寸，达上限滚动；回看不采集/发送/启用。
- Hover、等待环/220DIP连接条、主题/高对比度/减少动画及键盘阅读接线。拖动时首批正文只缓存；普通主题变化不重开窗口。独立public-probe使用同一个生产Window，记录实际HWND/WPF按钮/前台，工具本身尚未在Windows运行。
- 初24/24、扩31/31；补opacity/原抓取比例2例，最终34新增项全部通过。CI实跑Core668+Provider77=745/745、Node63/63，无失败/跳过。25托管工程locked Release零警告/错误；补空delta不算首正文1例后重新执行完整CI，再次locked审查发布。原33例/744回归及首包摘要保留为pre-empty-delta证据。
- 2000合成事件序列为单个属性测试内循环，不冒充2000Windows验收项。原634+77+63用例和断言保持，唯一锁变更为GrowthHarness→App/NativeHost项目引用，无新增第三方包或安装。
- CI入口新建且实际本地运行；远端workflow不存在/未跑，CI/Windows/安装声明分开。
- 包1445739字节，SHA256 `0f59b4deb79b48fd34bb9fa790e3c48eb87eb9500768da8c1bbef0cdf4df6832`，68文件：生产app39、独立公开probe25；所有exe为PE32+ x64，manifest逐文件通过。非安装器，无原生DLL/真实凭据/模型调用。九旧审查ZIP大小/hash未变。
- 269既有源码 + 新文件/CI入口独审冻结；176追踪项完整、106整体验收NotRun，P0/E06仍Blocked、P1/P2仍InProgress。详见p2-overlay-review及本轮summary/raw。

## P2 三项监督缺口修复

- 5a55d04限定审查指出设置被工具条裁掉、第四版生成时超三项、窄条宽度错误测高。先保存旧版实际版本失败TRX与工具条/测量合同反例，再修生产逻辑。
- 设置优先与更多菜单；生成中立即占槽、回退/阅读版保护、真实版本菜单选择；正文同一最终Plan测量再展开。P4原文/API重新生成调度未交付。
- 新增15例，P2针对49/49；Core683+Provider77=760/760，Node63/63，零失败/跳过。25托管locked Release零警告/错误。新增测试初编译错误日志保留。
- 原P2包及九份更早包保持原样；新包、精确摘要与独审入口见p2-review-fixes.md及本轮summary。Windows全部仍NotRun，176追踪/106整体验收NotRun，P0/E06 Blocked、P1/P2 InProgress。

新审查ZIP：1456604字节、68文件，SHA256 `6df677eae7778ed8669b81c81dd065987b5a76124676dda9223aa31590f69ced`；逐文件manifest及8个PE32+ x64入口通过静态检查，十份旧包大小/摘要未变。

## P3 真实UIA选区生产调用（监督限定切片）

- 4630831限定签收后实查HEAD/远端/干净工作区；断连后17:36:04Z安全命令成功，未换环境/删锁/重复写入。
- 原Broker只有兼容探针调用；现在正式Shell使用同一GetSelection实现，有界手势、稳定等待、来源窗口/PID/进程时间/控件/选区身份、最终目的队列资格、排除/暂停与Selection/Drop互斥。父600ms持有进程截止/清理，不凭Token虚报阻塞COM取消，启动/退出未确认不得第二启动，三次故障应用暂停30s。
- 50新增便携用例及真实公开自有子进程；初45中9失败，协议异常修后剩2运行器代理项问题，改测试体构造原UTF16后46通过；再补路径/启动结算4例，完整Core733+Provider77=810/810、Node63/63。原失败TRX、日志保留。25托管locked Release零警告/错误。
- 实际原文快照到Shell内存，可显式查看，锁屏/退出清理；没有把原文冒充译文或调用模型。公开浮窗探针禁用真实取词平台。完整Drop文本落点、复制兼容授权、全热键和P4调度尚未交付。
- 独立新包包含实际CaptureBroker；十一旧包保持原字节。Windows兼容/字体像素焦点/多屏DPI及P0/E06全部NotRun；176追踪/106整体验收NotRun，P0 Blocked、P1/P2/P3 InProgress。见p3-selection-review及本轮summary。

新审查ZIP：1607193字节、73文件（app 44、公开probe 25），SHA256 `f511b7e4298e5d8bf877007d0803370e94e6489f0e4f264c7833659a83dae980`；9个PE32+ x64入口及逐文件manifest检查通过，十一旧包摘要未变。

## P3 输入积压与取消读取所有权监督修复

- 固定基线65bdbc7b8c340b46dd7427f9bba1eff4b7e68aa0，不推进P4。三种启停往返旧down/up可重获新epoch，以及取消时底层读取已完成被遗漏的成功/异常，五个旧反例均实际失败；原日志/TRX保留。
- hook出生即冻结CaptureTicket、设置版本和实例ID，回调另捕获HWND/PID/系统事件时间；同一生产SelectionInputBridge在原始channel解析前及UI派发端复核，旧hook在epoch/版本变化后退出并替换。回调不拿门控锁、不做UIA。旧事件不得取消新手势，当前注入事件清手势不捕获。
- ReadAsync按是否已交付明确所有权，放弃结果无论当时是否完成都由一个观察者清零/观察异常；六项受控生产读取边界用例检查敏感缓冲、所有者数量及故障类型。受控流为测试替身，不宣称Windows进程验证。
- 本轮新增22例：3旧队列反例、13实际生产输入桥用例、6读取所有权用例。定向72/72；完整Core755+Provider77=832/832、Node63/63，25托管locked Release零警告/错误。Linux自有公开子进程原16项仍通过。
- 独立修复ZIP、12旧包、旧审阅精确commit摘要、原规格/锁文件均核对。176追踪/106整体验收NotRun保持；P0/E06 Blocked，P1/P2/P3 InProgress。Windows实际hook/UIA/焦点/多DPI/安装未执行；OLE文本Drop/复制授权/全快捷键/P4调度尚未交付。证据见p3-input-read-fixes-review.md和本轮summary。

## P3 单调绝对交付截止监督补审

- 基线b07f2b556f7bf1aa214d81daaae92e76edff81ad。原相对CancelAfter600ms不能证明最终交付硬截止；已完成WaitAsync和解码/校验跨界可绕过。旧5边界用例4失败/1成功（599ms），日志/TRX保留。
- Capture入口一次GetTimestamp；剩余预算定时器只唤醒阻塞，启动后、三次ReadAsync、退出后及最终成功交付前均CheckDelivery同一起点和取消。>=600ms为HardTimeout，已取消为Cancelled，均不带文本；时间测量不依赖墙钟或timer回调。
- ReadAsync后置复核在delivered=true前，取消/绝对超时的已完成帧仍由唯一观察者清零。捕获校验后的payload由原finally清零，旧输入身份/双队列/清理单writer保持。
- 新15例；定向87/87。初扩87中2失败为已释放Process查询，改成独立只读Process观察对象保留实际退出断言，原失败证据保留。最终Core770+Provider77=847/847、Node63/63，25托管locked Release零警告/错误。
- 独立新审查ZIP，13旧包、25锁文件及原规格字节不变；294源/本轮证据/历史固定commit均冻结核对。Windows实际hook/UIA/跨应用/多DPI/安装及原生P0/E06仍NotRun/Blocked；176追踪、106整体验收NotRun，P1/P2/P3仍InProgress。未推进P4。详见p3-absolute-deadline-review.md及summary。

## P3-04 复制兼容限定切片

- 8a10edaa9743914059f32cd893d724d922a65614签收基线，未进入P4。正式Shell UIA失败证据→默认关/逐应用明确授权→真实STA ClipboardHost有限完整快照→父Gate锁内一次Ctrl+C→350ms归属/输入/Unicode复核→仅同sequence+owner条件恢复。不可完整保存格式整组拒绝，未覆盖用户后续复制。
- 实际Win32内存口与IPC实现；父1200ms绝对交付截止，EOF取消+400ms恢复宽限，持有进程未结算保持单writer。权限即时持久事务/journal通知重放推进epoch；普通设置草稿不能授权。迟到payload唯一所有者清零，排队快照取消立即释放，焦点在目的队列再检查。
- 新118例、定向118/118；完整Core888+Provider77=965/965，Node63/63；26托管locked Release零警告/错误。初76/77失败TRX及编译失败日志保留，修正测试gate资格并补生产撤销epoch，不弱化断言。
- 独立Clipboard审查ZIP、逐文件manifest/PE及14旧包核验；25旧锁不变，仅新Host锁增加。运行源码/测试最终回归后未改；文档/staging单独定稿，固定commit历史摘要冻结。见p3-clipboard-review.md、本轮summary、Windows清单。
- P3-04源实现InProgress，实际WindowsSTA/剪贴板/Ctrl+C/格式恢复/跨应用均NotRun/Blocked，不将测试替身当用户授权或Windows验收。P0/E06 Blocked、P1/P2/P3 InProgress、176追踪/106整体NotRun不变。完整OLE文本Drop、全热键及P4模型调度尚未交付。没有安装/真实凭据/模型调用/新协议，main未改。

打包初次路径替换遗漏了大写P3，新包短暂写入旧绝对截止包路径；已修独立目的路径，以未改旧staging恢复原ZIP，精确1615917字节及SHA256 cb8bdde0d6ed43de225082165c444725e43ece356bc24750ba9a92a847e8cb7d与原签收摘要一致。保留初次stage日志与恢复核验JSON，最终重新staging并逐一核对14份旧包。恢复首试因缺失XDG变量遇只读cache失败；使用既有工作区cache配置后恢复，不申请额外权限。

P3-04最后复核补未知UIA状态带CopyEvidence的真实child反例：原6项5通过/1失败，严格EmptyOrUnsupported检查后通过。新增总数118，重新全量Core888+Provider77=965、Node63与staging；原客户端字节及失败TRX保留。

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

## P3-06 文字快照至 Provider/流式浮窗限定接线

基线561ee577获父限定放行后接生产调度：真实 UIA/复制/Drop 来源快照、原始 ticket/settings/profile、四模板、每次 HTTP 最终来源与身份检查、UI 事件原身份、latest-wins/取消、Unicode 软限额明确确认和硬限额拒绝、语义去重/零调用回看、旧快照重生成与当前设置重翻译、三版本及失败回滚。连续替换的元数据在生成期间最多四个/结束最多三个；锁屏退出内存释放。显式本地测试命令独立临时配置、无真实 key、可见测试标识，正常启动无 Mock。

新66用例（Provider54/Core12），最终Core1216+Provider131=1347/1347、Node63，27工程locked Release零警告/错误；真实测试 HTTP/AES 临时密文与 fake采集端口各自注明。早期编译/7与2实际测试失败保留，修正fixture与读取方式，未弱化断言。源码/测试最终回归后未改；仅App/GrowthHarness锁加项目引用，无新NuGet/安装。独立新审查包和19旧包/15精确commit历史核验。窗口原生/焦点/跨应用/多屏DPI/动效/DPAPI/安装/远端CI未验；P0/E06 Blocked，P1/P2/P3 InProgress，176追踪/106整体NotRun。没有真实密钥读取、付费/真实模型、部署或新法律协议。详见p3-text-translation-review.md、ADR0015、Windows清单及本轮summary。

## P3-06 本地测试边界父审阻断修复

88d4d59 尚未签收，旧代码允许测试设置改任意HTTPS而凭HTTP开关标本地。旧25反例22失败/3通过，实际错误发送只至loopback公开fixture；外部地址只校验不传输，无用户密钥。原源码7份/失败TRX/旧包摘要保全。

从显式命令冻结端口/run/scenario授权，主程序/调度/client共享不可变边界；无凭据/自定义头、固定TEST模型，当前配置/确认/重生成/每次发送复核。越界保存/恢复立即Off，晚到UI不写；client防删/换标记，实际socket固定回环IP并关闭代理/cookies，不DNS回落，标签用授权证明。39专项均过；Core1216+Provider170=1386/1386、Node63，27 locked Release零警告/错误，27锁和规格字节不变。独立新包与20旧包/16精确历史摘要核验，原覆盖恢复事实保留。Windows/native/跨应用/焦点/动效/DPI/DPAPI/安装/远端CI NotRun，P0/E06 Blocked，106整体NotRun。其他父审尚待结论，不进入下一阶段。

## P4-01 配置与固定样本测试垂直闭环

父限定签收81bd670后核Spec10/P4-01现有缺口，接统一URI预览/校验/发送、端点路径/完整地址/风险HTTP、四token策略、温度/上下文/图像限额/五项三态能力、加密主自定义认证头与非敏感头、两官方预设/模型手输、固定已保存profile测试。测试明确确认，不On/不捕获，在生成中拒绝争抢，Off身份不许任意正文/system/image；流式/JSON/错误/取消真实Provider返回。测试模式固定回环无凭据边界未放宽。

初14协议失败（Anthro13默认枚举+1隐式测试入口）修协议默认/受限fixture保留，原断言不改；全局取消曾破坏1通用快照测试，改为正式消费者liveRequest自有取消，旧保证恢复。xUnit过滤重载编译错误日志保留。新83项（Core40/Provider43）以及原1386均保留，最终Core1256+Provider213=1469/1469、Node63、27locked Release零警告/错误，27锁/原规格字节不变，schema由实际模型生成。实际AES只为合成fixture，不读用户key，不实际模型/收费/部署/安装。新独立包/21旧包/17历史精确commit核验；原覆盖后恢复事实保留。P0/E06 Blocked，P1/P2/P3/P4 InProgress，106完整NotRun；Windows/UI/DPAPI/焦点/动画/DPI/安装/真实服务/远端CI未验。详见p4-profile-review、ADR0016、Windows清单与本轮summary。

## P4固定样本授权、总预算与编辑取消父审修复

7fecdf尚未签收：一次公开测试误写普通内容consent、传输120硬cap及相关表单编辑取消不全。授权17反例旧11失败/6通过；现在目标仅绑定测试identity，失败/取消不污染普通权限，Shell真实enable独立确认。总预算在秘密访问前一次单调冻结，传HTTP同一对象按剩余取消，30/20等阶段截止保留；旧源码计时重放3中2失败，修后同断言验证60/120/180。表单Text/Password/Selection/Checked/Unchecked全接DraftEdited，待确认/排队事件复核revision。

新30专项最终通过，Core1262+Provider237=1499、Node63、27locked Release零警告/错误。首套bounds旧进程测试因额外并行负载600ms硬截止失败不改原断言/截止；第二套旧test factory漏新timeout映射补夹具而非改预期；新shellfixture顺序/旧time观察编译和亚毫秒失误均保留日志。原1469/锁/规格保留，独立包及22旧包/18历史固定commit核验，原覆盖恢复事故不抹去。其他父审等待、P0/E06 Blocked、P1—P4 InProgress、106整体NotRun；Windows/实际UI编辑/DPAPI/焦点/动画/DPI/安装/原生/远端CI未验，没有真实key/模型/付费/扩权/安装或部署。

## P4 空控件密钥清除命令父审补正

05dc一次测试授权/共享总预算获父限定成立，编辑取消遗漏清除已保存key：密码框空Clear无事件。将真实Clear按钮命令体提至partial并与preset直接清引用步骤共享入口，先ApiFormEdited再改引用；原统一编辑体仅移动，其他设置流程不扩张。检查SetKey仅非空新输入、显示/Mask不读旧key；preset实际同类遗漏一并补。原05dc Window/Api两份字节、等价抽出旧命令、红测与相同断言保留。初队列fixture误读null Post状态导致超时，修夹具后原6中5失败/1通过，修复同6全过。测试Link真实生产命令体，控件明确测试端口；真实配置/AES合成密文、原gate/workflow/lease、HTTP及目的队列执行。草稿不永久删旧key，结算释放请求lease，保存才原事务撤销。

最终Core1262+Provider243=1505/1505、Node63/63、27locked Release零警告/错误；原1499逐项名称、27锁与规格不变。独立新包和23旧包/19历史精确commit核验，历史覆盖恢复事故继续保留。P0/E06 Blocked、P1—P4 InProgress、106整体NotRun，Windows真实Click/PasswordBox/DPAPI/原生/跨应用/动画/DPI/安装与远端CI未验；无真实key/模型/付费/安装/扩权/部署。详见p4-secret-command-review、Windows清单及summary。

## P4-05 会话风格、序号与重生成约束

322aea6获限定签收，断连后首命令真实成功，核HEAD/工作区后按Spec9.1—9.9/P4-05/D04—07/F/J补会话下次风格、菜单/标签版本N与regenerate@1。复用四模板原字节，普通所示快照+覆盖与当前已保存默认分流，运行中/旧版不重写。same-source fallback额外第四引用原1反例失败，删重复保护后当前来源真实三份；不同来源旧3+新1回退保持。序号与所示号分开，读旧/失败不回绕。原两协议约束2失败、首次拼接遗漏2失败、专项Last-event fixture竞态1失败保留；最后检查新版本首事件仍严格Begin+原快照。

新35（Core8/Provider27）均过，完整1270+270=1540 .NET/63Node，27locked Release0warnings/errors。旧1505、27锁、产品书与四旧模板保留。独立新包与24旧包/20历史精确commit核验，旧覆盖恢复事故保持。P0/E06 Blocked、P1—P4 InProgress、106整体验收NotRun，WPF菜单/显示时序/焦点/动画/DPI/DPAPI/跨应用/安装/语义J/远端CI未验。无真实凭据/模型/付费/安装/依赖扩增/扩权/发布；详见p4-session-style-review、ADR0017及summary。

## P4-05 模板设置与生产组合闭环

父限定签收222b057后核Spec8.2/9.1/9.9，补四模式查看/复制为自定义/恢复默认/完整预览；schema1可选字典，旧v1读取不写、v0迁移保留。统一单次白名单Compose，base不可配置，source_text固定独立user资料引用、原文完整user；模板8192/展开28000/system32768有界。自定义hash版本及旧全映射快照，普通/当前设置重生分流，结构相等去重。命令先DraftEdited再改草稿，不扩大普通当前流取消；固定连接测试忽略模板。

原4配置+2HTTP反例全失败保存，修后同断言通过；新67（Core30/Provider37），全Core1300+Provider307=1607、Node63、27locked Release零警告/错误。多余testfixture括号编译日志保留修语法，明确异常类型断言。断连后00:53:20Z真实命令和PID563098健康CI证据保全，没有重复启动；首1606/63通过，补组合顺序断言后最终回归。原1540、规格/内置模板资源/27锁保留；独立包及25旧包/21历史摘要核验，原旧包恢复事实保持。P0/E06 Blocked，P1—P4 InProgress，106整体NotRun；WPF/跨应用/焦点/DPI/DPAPI/安装/模型语义J/远端CI未验，无真实key/模型/付费/扩权/依赖安装/部署。详见p4-custom-templates-review、ADR0018与summary。

## P4-04 冻结阶段配置与有效内容截止

646c991获限定签收，核Spec10.7与官方.NET接口，补RequestTimeouts默认10/30/30/20及四有界字段/mapper/旧快照。真实连接池按秒数有限缓存、ConnectCallback原socket/固定回环+token，TLS系统ConnectTimeout；控clock dial明确seam。SSE底层每次Read用阶段token，JSON增量识别选中可解码正文/已知协议，保持完整结算后一次展示；无效/ping/空byte不刷新有效origin或total。原总预算仍凭据前起，旧测观测不弱化；旧快照/配置取消实际验证。

原Core4失败、正确flush下旧源改类名重放6期限反例全失败，初夹具6失败中4是headers屏障错，保留并收窄；首生产12中2comment-only挂起修真实ReadAsync token，后46中2异常族/clock安装交错保留修复。最终新61（15Core/46Provider），全1315+353=1668 .NET/63Node、27locked Release零警告/错误，原1607/27锁/规格/内置模板保持。断连01:44:46Z实命令/proc确认无活动test，未重复/扩权；独立包及26旧包/22历史摘要核验，旧包恢复事实保持。P0/E06 Blocked、P1—P4 InProgress、106整体NotRun；WPF/代理/DNS/跨应用/焦点/DPI/DPAPI/安装/真实模型/远端CI未验，无真实key/付费/依赖安装/部署，详见review/ADR0019/summary。

## P4-04 截止交付父审阻断修复

ccf998 尚未签收：响应头完成后未复核期限、phase.Check未查父取消、JSON逐个交付未查total，确有迟到成功缺口。原生产路径29例24失败/5通过、扩41例36失败/5通过，字节已完成/延迟timer callback/消费者暂停反例均保全；修后原41过，再补连接完成3例，共44新回归、94专项全过。连接/发送资格/headers/read/parse/Effective/每次交付复核原绝对total、父取消及阶段期限，first起点冻结在Started之前，outer envelope共享budget；迟到连接释放，不发送HTTP，失效身份仍丢弃。

完整Core1315+Provider397=1712/1712、Node63/63，27locked Release零警告/错误。原1668逐名保留、27锁/规格/内置提示词未变；当前独立ZIP与27旧包/23历史精确commit摘要核对，ccf原证据字节不改，旧覆盖恢复事故记录保持。最终回归后运行源码/测试未改；等待父独审。P0/E06 Blocked、P1—P4 InProgress、106整体NotRun，Windows/跨应用/DPI/DPAPI/真实代理DNS/安装/模型/远端CI仍NotRun，无真实调用/安装/扩权/发布。详见[p4-completion-races-review.md](p4-completion-races-review.md)、ADR0020及summary。

本轮打包复制脚本遗漏大写P4目的名，暂时覆盖ccf原ZIP；冻结检查失败后，由未动原staging恢复原2165159 bytes及SHA256 231ed070c8baeb4bf5017be2083b014b092c48b80f627cabad3a4bfc6c442b96。未改原摘要/哈希断言。新脚本修正独立输出并增加全部27历史ZIP前后哈希检查及历史路径拒绝，重跑证据保留。

## P4-04 实际消费者目标队列父审阻断修复

893ebc未签收：生成器检查未覆盖UI队列应用，原lifetime.Token与Current不能拦截止前入队/截止后apply，也无phase origin。实际HTTP→两个实际消费者→生产Receive/Apply→受控Post队列红例104中68失败，112中68失败，恢复六份原源码精确字节的最终124中80失败/44通过，源码/断言/TRX均保存。冻结事件资格携带同一budget、原阶段origin/limit及token，canonical身份/取消检查后复核；晚到内容/usage/成功/错误转对应Failed，已应用正文保留，旧身份丢弃。Workflow在Current返回后再次gate+Admit，终止只影响该identity，旧回调前后排空均不停止新真实请求。

最终124新+94旧专项218/218，完整Core1315+Provider521=1836/1836、Node63/63，27locked Release零警告/错误。原1712逐名保留、27锁/规格/提示词不变；回归后运行/测试未改。打包预核独立目的名、前后核28旧ZIP，ccf恢复字节及893旧包保持精确，旧事故不抹去；当前独立包与24历史精确commit核验，等待父审。P0/E06 Blocked、P1—P4 InProgress、106整体NotRun，实际WindowsUI/跨应用/DPI/DPAPI/安装/原生/真实模型/远端CI未验；无真实key/模型/付费/安装/扩权/发布。见p4-queued-delivery-review、ADR0021及summary。

## P0—P4云内交付缺口与文字Alpha发布链

父限定签收dc70后核30任务：已有生产取词/协议/浮窗接线，仍缺真正自包含Alpha交付与有界执行入口。补六宿主独立publish/相同摘要共用/完整归属清单、1199文件SC包/9实际完整性和执行拒绝检查，13独立RID图锁保持27普通锁和原NuGet条目，SDK10.0.401/runtime10.0.12固定；Windows suite接明确Bundle/Execute/模式与有界进程所有权，不假造Windows通过。首次NU1004保全修独立发布锁，初始化后实际locked发布；当前普通CI1836 .NET/63 Node和27工程0warning/error。

断连恢复05:13:07.806097Z真实命令/HEAD/文件/零活动任务证据保全，无重复启动或扩权；父再次问状态时工作已在进行，最后发布实际开始05:34:45Z。仅原云环境，未操作已连接桌面，未真实模型/密钥/注册/信任/新SDK协议；恢复日志/错误/包保全。Windows最小运行需求降到Windows11x64/普通解锁Default交互桌面/PowerShell7/受控公开fixture应用；源码原生构建、安装与混合DPI测试另需工具和环境。P0/E06仍Blocked，P1—P4InProgress，106整体NotRun；不跳P5/P6，等待有界Windows授权和实际验收。详见p4-text-alpha-review及summary。

## 文字Alpha退出合同父审补正

044发布准备限定接收，原launcher/desktop wrapper/Windows suite吞子退出/比较Fail。旧实际收尾AST+真实持有pwsh退出进程、完整wrapper及suite委托后语句红24/24失败（18退出码错误），夹具参数/子scope错误及真正Hashtable形态6失败均保全。最终实际形态24/24通过；退出5/6/7保留，比较3，探针timeout4，缺有效证据NotRun2，预检0/PreflightOnly，限定/有界会话0但WholeNotRun。三层明确exit，清理/finally仍生效，无Windows或模型执行。

10:06:35Z实际恢复HEAD/远端044、保留test文件、无活动写入；未重开Git/写入者或扩权。没有重复1836/63未变化生产回归、没有重打大包；29历史+3Alpha共32旧包字节保持，独立约7KB脚本补正供未来授权Windows使用。旧包内旧脚本不改，运行新脚本以BundleRoot指向旧包。P0/E06Blocked、P1—P4InProgress、106整体NotRun，Windows实际条件仍待落实，不进入P5/P6。见p4-alpha-exit-review及summary。

## 最小 hosted Windows 平台合同 CI（执行前）

用户通过父线程放行最小CI实现：核实公开仓库当前HEAD/main无工作流、Actions runs 0，正式预装windows-2025镜像可望补原生编译但不是Win11普通用户验收。新增限定dev分支+工作流自身路径push、固定checkout SHA/只读权限/无凭据持久化/15分钟入口；真实OS ProductType/管理员/完整性/UAC/桌面/DPI/工具事实探测，四既有C++ DLL的x64 Release及2既有CTest、4 PE/4加载/8导出；仅图形条件满足才真实ShellWindow公开fixture有界合同。Alpha守卫、系统安全/账户/证书/组件均不改，无模型/密钥/付费/安装/发布；不上传artifact，不重打包。

本地45/45脚本合同（43合成/解析+2实际入口进程）、49/49既有overlay纯逻辑、9工程GrowthHarness locked Release零警告/错误。辅助用例动态作用域参数错误原日志保留后修复；首次pwsh缺可写XDG在测试前退出134未计测试，使用既有云XDG恢复。正式入口Linux返回2/Blocked，ProbeOnly0/NotRun，Windows计数均0。生产核心未改，1836/63全量未重跑。远端Windows构建/桌面/CI仍NotRun，独审后才推送首次触发；无run URL不假报通过。P0/E06Blocked、P1—P4InProgress、106整体NotRun、不跳P5/P6。见windows-ci-review与windows-ci说明及本轮原始日志。

原环境独立只读审查者windows_ci_review已通过48ea291精确HEAD并独立45/45，按父明确授权正常推送，首真实CI38241358390/job114784483648在原规格字节守卫失败，Native/Overlay均NotRun和0计数。首轮log未输出实际hash，不伪造字节快照；本地实际Git用原*text=auto/autocrlf=true可重复得到148532字节/1826CRLF/SHA3749…，canonical146706字节/SHAEA3A…，仅换行差异。保留守卫/原规格/预期hash，加两文件-text与4项真实Git检出回归；45脚本再次通过，新增环境及原始文件字节诊断先于守卫。首实际runner是Server2025 VS2026镜像，核官方清单后切标准Server2022预装VS2022保持原工具链，不安装/降权限/扩权。此修复待同一独立审查后正常推送恢复CI；原失败日志保留，无原生/桌面通过声明，P0/E06/106整体状态不变。

be3817d已同一独立审查4/4Git+45/45后正常push，真实第二CI38242594071/job114788604937测得Server2022 build20348/ProductType3，管理员/high integrity12288，实际EnableLua=1（不把官方默认描述当本机事实），session2/WinSta0/Default/单屏1024×768工作区1024×728/96DPI/DWM及Shell存在。预装VS17.14.37710/MSVC14.44.35207/SDK26100/CMake+CTest3.31.6/.NETSDK10.0.401/桌面runtime10.0.12全部满足；两规格真实146706字节、0CRLF、原SHA守卫通过。首次真正MSVC编译OCR在IVectorView range-for迭代缺Collections定义(C3779)失败，NativeFail/所有测试计数0；按微软C++/WinRT要求补完整Windows.Foundation.Collections.h，保留/W4/WX与算法/断言，原失败日志保留，复核后继续真实CI。Win11验收及P0/E06/106门槛不变。

7eb28be已独审后推送，第三CI38243023779/job114790048182真正4DLL构建/4PE/4加载/8导出、2CTest全部Pass（其中GDI为7个合成顺序子情景，不是实际GDI像素验收）；原生Shell出现1条LIBRARY名未带.dll的LNK4070警告，Windows托管9工程0warning/error。公开真实HWND进程35秒超时，被自己持有的Process Kill确认退出，OverlayFail/0check/no receipt，原日志保全。独立代码诊断发现真实ShellWindow Closing同步完成所有清理后重入Close违反WPF_isClosing；现保留清理顺序后await Dispatcher.InvokeAsync(Close)，不延时/绕过Close/更改断言。新增公开probe稀疏phase与owned stdout/stderr有界诊断，修Shell.def的.dll输出名警告。初用BeginInvoke引发CS4014告警升级构建失败原日志保留，改await InvokeAsync后9工程0warning/error，73/73既有lifecycle+overlay逻辑及45/45脚本通过；实际关闭/几何仍需下一真实CI确认，P0/E06/Win11/106整体不变。

a5e734f独审后第四CI38245204651/job114797239356继续4DLL/4PE/4load/8export/2CTest Pass，原生LNK4070已消失，Windows9工程0warning/error；phase证实Closing两次→Closed→RunReturned约5.49s，35秒关闭超时已真实修复。公开HWND130帧/无丢帧/完整流/无异常，八项true，ActualButtonMatchesAnchor有2坏帧所以仍Fail/0整体check，不跳帧或弱化：anchor恒[956,307,1004,355]，Expanded HWND420×409旧48×48 Canvas默认居中得到actual[770,488,818,536]；Compact48×48 HWND仍旧Canvas.Left372得到[1328,307,1376,355]。独立诊断与WPF源码吻合：原生位置先同步，WPF Arrange延后。最小生产修复Canvas Left/Top，属性完整batch后finally在live/layout-invalid时UpdateLayout，覆盖compact提前return；探针仅追加expected anchor四列，getter/9断言保持原样。9工程交叉build0/0、73既有逻辑再次通过，下一真实run才判修复效果和开销。

第五CI[38246796562](https://github.com/T1doo/KuaiYi/actions/runs/38246796562)仍Fail：112帧保留、0丢帧、完整流、0模型调用、正常约5.56s退出，原生4DLL/4PE/4load/8export/2CTest继续Pass；仅两帧按钮保持旧Canvas.Left，说明左上对齐修掉居中但移动后的UpdateLayout晚于观察。独立诊断由[HwndTarget.OnResize](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/InterOp/HwndTarget.cs)与[MediaContext.Resize](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Media/MediaContext.cs)确认：SetWindowPos过程中同步Rendering，不能等它返回后才准备布局。现先完整准备Canvas并条件UpdateLayout，再执行原生bounds，之后条件确认布局；local PrepareVisuals的compact return只结束准备，不跳过提交，UI准备异常不会移动半成品。生产RenderOverlay在提交期间仅阻止递归Tick，观察器、getter、全部逐帧断言、时限保持原样。缓存仍只在native成功返回后更新。混合DPI期间非Rendering的DpiPlacement同步重入风险仍需独立桌面验证，未据单屏称全平台原子提交。首次本地build漏Linux交叉构建EnableWindowsTargeting参数而NETSDK1100失败，原失败日志保留；修正执行参数不改项目平台约束。

## 2026-10-12 Windows CI 限定实测完成

已独审并正常推送运行源码 `4ff83e510b326d1d1bffc49dec2fd06315871ee1`。第六轮 [Actions 38247667426](https://github.com/T1doo/KuaiYi/actions/runs/38247667426) 与 job 114805544563 均 completed/success；来源 clean，脚本与公开探针退出0。四个 DLL x64 Release 编译、4 PE、4实际加载、8导出、两个现有 CTest 全通过；Windows九托管工程0warning/error。原生编译/链接日志无warning/error。公开fixture真实生产ShellWindow的210次Rendering观察、0丢弃、完整流、0模型调用、九项检查全部true；独立解析CSV重新核对210行、全部按钮等于恒定anchor[956,307,1004,355]、实际生长、全部逐行布尔检查无违反。正常Closed/RunReturned约6853.374ms；所有原时限/断言/守卫不变。

210为Rendering回调观察次数，含resize同步观察，不能称210张独立呈现帧或60FPS。最大相邻观察间隔482.858ms；本轮不证明精致流畅动画、性能指标或八小时稳定性，视觉品质仍NotRun。实际环境Server2022 build20348/ProductType3、Admin/high12288、EnableLua=1、session2/WinSta0/Default、单屏96DPI、1024×768/工作区1024×728；不是Windows11普通用户验收。混合DPI的非Rendering同步重入风险仍未验证。本轮只通过限定合同，P0/E06 Blocked、P1—P4 InProgress、106整体NotRun；真实UIA/剪贴板/OLE/热键/DPAPI/业务截图OCR/现代菜单/安装卸载、模型语义均未借此通过。

小型证据：[最终JSON](test-evidence/2026-10-12-windows-ci-summary.json)、[210行公开CSV](test-evidence/2026-10-12-windows-ci-overlay-38247667426.csv)、[完整job日志](test-evidence/2026-10-12-windows-ci-run-38247667426.log)。日志是connector解码后LF规范化文本，摘要哈希仅描述保存文件，不假称GitHub原始压缩字节哈希。六轮日志均保留，不覆盖旧包/失败证据、不上传artifact、不打包安装器。源码CI SHA与后续纯文档证据提交分开；纯文档提交不命中workflow路径触发器，不把它声称重新编译通过。

## P0-03/E06 有界恢复与真实 OLE CI 准备（尚未运行）

2026-10-12容量失败后按父最新指令只作一次有界接续。恢复核对HEAD/远端dev均9764c1d085b7ce4adf9bae401c9c004312a39afd，main仍51d077481cf6303602fcf5d2718213da9cf05178；工作区无未提交/未推送差异，无活动构建/测试/推送，仅root写入、独审只读。没有重复启动原命令或更改模型设置。

现有E06物理路线未通过：OlePresentationWindow的RawMouse拒绝injected，真实ShellWindow只有WPF pointer路径；前六轮CI不包含实际OLE业务。新增仅显式CI构建属性编入的App公开实验入口，普通App不含此命令或测试partial。用真正App.exe/ShellWindow和ProductionTextDrop、原父身份校验/原registrar/NativeBridge/receiver；temp无密钥配置，仅Drop接线，selection/剪贴板/热键/Provider不接。公开故障源沿原PreviewDown→DoDragDrop→真实同步GetData路径Sleep5000ms，live stdout元数据在Sleep前flush，不写原文。SendInput只指向自己持有的公开源和浮钮，前台/窗口/按键/capture不符标Blocked，不绕过RawMouse injected守卫。

两场景：GetData进入后且1500ms原截止前观察主UI心跳与真实合成WPF拖动；自身移动可能取消lease，明确分开。执行生产取消后必须持有原receiver OS handle实际HasExited、原Run/Control结算、TryFinalize/registrar撤销全确认，才显式恢复新实例；第二次实际GetData阻塞时正常Close并核相同退出/注册所有权。仅held Process可终止，源进程关闭8s/自有强杀确认3s；App内部60s/外部75s+5s确认，不延长原OLE截止。源和App均为既有locked项目普通build，不publish/不zip/不安装/不上传artifact。

当前仅源码准备，独立精确SHA审核后才执行本地/Windows测试；没有新的COM/OLE/输入通过声明。实体鼠标/源捕获/混合DPI/普通Win11与整体E06仍NotRun/Blocked，P0门槛不变。

源码自查补正（未测试、未push）：f616880准备提交发现Invoke-Checked会向success stream输出构建文本，helper改将它们Write-Host，保证返回唯一typed receipt；CI peer改捕获原peer最新Control task而非Ready时快照，恢复必须原实际控制链结算。先前准备commit保留，不把静态发现标为真实测试失败或Windows通过。

独审6e6a0a6提出证据阻断，尚未测试/推送：补Source原QPC timestamp/frequency和GetDataBlockReturned状态，响应/Close前同时复核原DropBegin接收起点、源阻塞起点<1500ms、原receiver持有句柄未退出及Run未结束；新增Ctrl/Alt/Shift/Win/Escape空闲检测并在新按下前复核。CI记录所有receiver Created/Ready顺序及每个旧原Run/最新Control、held OS process HasExited、Client.ObserveExit和registrar撤销，任何提前新建禁止隔离Pass。合成pointer与隔离生命周期分别报告，IsolationPass绝不推导E06物理或pointerPass。

最新恢复命令在当前已有HEAD6e6a0a6/worktree clean/无活动构建测试下执行；git远端命令失败为Failed to connect to proxy port8080，并非再次模型容量报错。既有GitHub连接器只读GET refs成功，远端dev仍9764c1d、main仍51d0774；未扩权、未改网络/账户配置，也未启动或重跑Windows CI。

按父最新“不重复之前成功Windows结果”，工作流本轮显式-OleOnly：只为新的临时host构建现有OleNativeBridge（前次host已回收且无artifact上传，不能假称重用其不可取得DLL），不重跑四组件/两CTest/Growth210帧旧合同。原default模式保持原四组件/HWND逻辑，OleOnly才运行新E06公开探针；新report Scope=HostedOleFixtureContractsOnly，CTests/Overlay检查为0/NotRun，单独打印隔离/合成输入/E06PhysicalNotRun。当前代码尚未测试；先对精确源码与模式做独审。

独审58f5fbc静态发现fresh checkout的App assets缺口：OleOnly跳过GrowthHarness restore，补现有App项目--locked-mode restore，仍不安装SDK/新版本/来源软件。未启动CI，不把该静态发现记成实际NETSDK1004失败。job summary补分列OLE隔离/合成pointer/E06PhysicalNotRun和旧合同未重跑说明。

本地执行证据（非Windows）：9ec5356通过独审后，CI App默认并行build退出1（汇总0warn/0error、未完成编译）；既有OLE tests在MSBuild本地NamedPipe socket bind被当前沙箱拒绝（MSB1025/SocketException13），随后no-build单进程VSTest明确SocketServer/TcpListener Permission denied、Test Run Aborted，0项测试执行，不记Pass/Fail断言数。未扩权；改执行参数-m:1/-nr:false/UseSharedCompilation=false后，CI App8工程、FaultSource6、Receiver6编译各0warning/error；45既有PS合同通过、OLE helper Parse通过、真实Linux-OleOnly入口exit2/全部Windows计数0/Blocked，普通App MSBuild items明确无CI文件/define。所有失败log保留，未削弱原测试。关闭动作再在实际UI调用点复核live原GetData/Drop，排除排队后才关闭的时序误认；此一行待精确增量审后重新编译App。Windows E06仍未启动，实体鼠标NotRun/P0Blocked保持。

独审已通过9ad689e6/tree a8f58468的关闭调用点live复核与真实失败日志口径；此源码再次仅CI App增量构建8工程0warning/error，13.45s，原始输出另存不覆盖之前日志。Windows OLE尚未启动。云shell Git传输仍不可用，既有授权GitHub连接器已只读核原base/tree；准备通过普通非force/CAS更新同一dev分支。先核逐blob和完整tree与审过本地源码一致，再审实际远端commit身份；不改main/权限/网络/模型设置。


## 2026-10-13 非目标生命周期唯一 Windows 尝试：预启动失败

按父限定授权，仅开发分支执行一次 Windows2022/10min job，每外部操作30s有界，目标为编译新 lifecycle exe/DLL及 Normal、BoundParentDeath、BeforeBindParentDeath、Watchdog。初始包装d736独审发现depth1不含锁定parent，未启动即修至depth2并保留原freeze；修正freeze9894d58f4918712c3ebd40b8c842a133bcee73dd/tree da61ad6489a74ecb7c3dc90ab52f6f3a07fdeb07经同一只读独审放行、本地45/45脚本合同通过（无WindowsPInvoke）。普通CAS交付ae965898ad13c97d0b83651dded7f345847e0006与审定tree精确相同，唯一parent锁定3ae694；运行源码未改。

真实 [run38443925339](https://github.com/T1doo/KuaiYi/actions/runs/38443925339)/job115457262309/attempt1 completed failure。只有SourceHead执行，StatusFailure/ExitUnknown/RootExitConfirmedfalse/JobEmptyfalse/RescueUsedfalse/CleanupConfirmedtrue，未进入编译或任何四case；SourceCommitnull/SourceEquivalentfalse/Cases[]/RuntimeNotRun。Cleanuptrue来自created=false分支，不能称子进程实际退出或Job清空Pass；Reason组合标签不能推导为timeout。有限receipt未记录具体失败API阶段/Win32code，故不猜根因。没有重试、额外workflow推送、安装、扩权、模型调用或生产reuse。固定脱敏receipt和scope见 [lifecycle-single-windows-run.md](validation/lifecycle-single-windows-run.md)。不上传raw日志，不把CompilerFaults[]称编译通过。P0/E06Blocked、P1—P4InProgress、106整体NotRun；UIA原文、匹配/性能、Windows11普通用户/多屏DPI/安装卸载均未借此通过。


本轮证据提交04c5bb仅本地：自动审批拒绝向公开仓库创建完整progress blob，父进一步要求停止上传/不得绕过；远端仍ae965，无新失败证据文件路径更新。原始connector job日志私有保全，固定摘要不改。按父独立诊断边界仅本地补固定启动阶段/Created/Resumed/有限错误分类及WAIT_FAILED、实际timeout、cleanup未知区分；纯分类71/71、原包装45/45，无新Windows/PInvoke/模型执行。初次测试invalid enum注入被PowerShell提前拒绝，保留失败源码后用Enum.ToObject恢复真实projection断言。详见lifecycle-operation-local-diagnostics，不据分类补正猜SourceHead根因。

只读独审暂hold83ff的FlushFileBuffers错误读取：补现有导入SetLastError=true及13纯反射合同，另两条确保完整success字段仍不能救援/带已记录失败后Pass；最新86/86纯分类合同通过。新增reflection harness初用C#扩展方法被PowerShell实例调用拒绝，保留失败源后改实际GetCustomAttributes，不弱化断言。该补正仍仅本地，无新Windows/上传。


## 2026-10-14 交付状态更正及 SourceHead 专项本地准备

八文件已按用户直接批准正常交付4bc4ee3ea471ed1dc51f83c8d04827ad02f92ed2/tree ac8a07e77e1d74b314b3091028ec6fab61ba3b61；旧“只在本地/上传被拒”文字为历史状态。原三个提交、bundle和失败receipt保持，无新Windows/CI。父后续仅允许本地准备：新SourceHead入口一次owned Git，固定启动stage/error/Created/Resumed及退出清理字段，不编译产品、不跑四case/UIA/模型；Add-Type现有interop载入、原30s预算与救援观察范围、step2min/job10min明确列入待批准执行范围。未来单parent交付锁4bc，精确event SHA/depth4，未先push触发。

新59/59本地合同含真实浅克隆depth3缺locked/depth4可diff、Linux入口Blocked和纯输出/门控分类；无WinPInvoke调用。本地Get-Command发现4个Git程序候选，真实C#字符串参数会将数组拼成单字符串，新入口明确仅首个程序；不据此认定原Windows失败根因。初始数组乘法harness失败及随后多Git定位失败保留，再修脚本语法/定位，未减断言。详细scope及未来八文件清单见[SourceHead准备说明](validation/source-head-next-diagnostic.md)。原native/managed生命周期编译及四case均NotRun/P0E06Blocked；本轮无上传、远端ref修改或执行授权。


## SourceHead 唯一实测结果及完整 runner 后续本地准备

准备13414b51已按新明确批准交付b39a9076e69bec3c277e5664428ff142eaaef9f5/tree2bcfa3dd1a23c0da085f9d5303d491354e668686；唯一run38524993308/job115723148036/attempt1 success，SourceHead Exit0、Created/Resumed/held-root退出/Job空/cleanup全确认，无rescue，返回SHA匹配。旧授权/未上传文字是历史准备时点，本次授权已消耗，无重跑。原失败receipt不改，旧Windows具体根因仍unknown，native/managed生命周期编译及四case/UIA/匹配/性能均NotRun。

父仅允许后续本地准备：完整runner统一git/cmake/dotnet取首个ApplicationInfo，不拼多路径；新增精确HEAD、唯一审定parent、locked祖先/tree及完整runtime输入diff。未来一child锁b39→4bc→ae→3ae，真实local浅克隆depth4失败/depth5可验证；独立新workflow只匹配自身路径，不改旧SourceHead入口，job10min/step8min/每外部操作原30s及既有清理观察，不改250/600/120/2000ms生产门。新72/72合同（真实git/dotnet2、cmake缺失拒绝1）和原45/45通过；初始harness假设cmake存在失败已保全，改为缺失时明确拒绝，不安装/不弱断言。原源/Job/协议未改，无新Windows、上传、push、模型或安全网络权限操作。[完整下一验证合同](validation/lifecycle-full-next-validation.md)列准确八文件及未验范围；需后续独立批准才可推送触发。


## 2026-10-14 完整生命周期唯一尝试失败及本地补正

审定16d3ff37已正常交付1f78ac3cf2073895dab8c6a3ab3e936ce064d143，完整tree381bd52c与审定源码相同、唯一parent b39a907。run38595908121/job115962449373/attempt1 completed failure；九项来源与工具检查Completed0且无rescue，NativeConfigure root Exit0但WaitJobEmpty Timeout，救援后JobEmpty/cleanup确认。没有救援前具体子进程身份，具体根因unknown；CompilerFaults[]不证明目标编译通过。原生目标build、managed restore/build、四case全NotRun，UIA/Match/Performance NotRun，ProductionReuse Off/ModelCalls0。失败即结束该次授权，没有自动重跑。

仅本地补正ChildEnvironment固定MSBUILDDISABLENODEREUSE=1；官方及实际SDK setter证明开关语义，不据此声称修复Windows根因。原86加5合同91/91；未来parent1f78/depth6真实浅克隆合同72/72，原evidence45/45。未减断言、未放宽Job空/救援/预算/生产守卫；不安装、不push新准备、不启动Windows。固定receipt及完整口径见[失败报告](validation/full-lifecycle-configure-timeout.md)。P0/E06Blocked、普通Windows11/多屏DPI/真实取词/安装验收保持未验。


## 2026-10-14 救援前有限诊断与仅本地交接

父要求补正精确诊断：14534bd未单独记录救援前Job状态/BudgetExpired，旧receipt保留原字节且不回填。新有限Unknown/Empty/NonEmpty记录原等待查询，BudgetExpired记录原预算决策；finally仅更新旧清理字段，不覆盖前态。沿原!empty||expired、不新增查询或放宽预算/救援/隔离；不能认定MSBuild残留根因。受控ChildEnvironment固定禁节点复用仍为候选修复。新增14纯断言加原91=105/105，runner72/72、evidence45/45实际通过；本轮无Windows/上传/push。精确冻结身份、逐文件hash/diff及独审见本地handoff，后续唯一尝试需父核验和新授权。[最小方案](validation/lifecycle-safe-diagnostic-handoff.md)。


## 2026-10-15 独立Windows文件fixture一次准备

从已审30098抽取原字节parser/Windowsadapter与21合同到正常新分支dev/configure-files-fixture；最小十文件基于treece453400（本地2f125729/远端bf033等价），不上传全部本地历史。新16/16离线fixture合同与Linux入口Blocked已实测；Windows仍NotRun。既有两workflow仅原诊断分支且字节不变，新workflow仅新分支/新路径，公开标准windows-2022唯一job5min，先独审manifest再一次交付。只owned临时目录public数据的实际文件/metadata/share/junction/上限/close；不CMake/四case、无安装/权限/服务/进程扫描。任何失败停，不重跑；同步文件不称硬超时，不接救援。详情见[独立fixture合同](validation/configure-files-windows-single-run.md)。

0976预执行独审Hold，未执行Windows；补fixture专用SetFileTime共享兼容metadata变化、权限错误才Blocked、独立全资源清理与原stage保留、非法SHA不输出、两个adapter -text避免CRLF字节门误拒。原7加9反例16/16通过，旧freeze和全部原件保留；原adapter及旧运行路径不改，复审后才一次发布。

4ed复审Hold指出setup临时句柄登记遗漏；native_open取得即登记独立fixture_handles，关闭成功才移除，最终合并全部自有资源清理；补两反例至16/16通过，保持caller与adapter集合区别，不假cleanup确认。仍无Windows/上传，待精确复审。


## 2026-10-15 独立Windows文件fixture实际通过

14a43a01/tree8a7ee95b十文件已正常交付37fb5d5d63c27239567cf084c9109cdedcf1f504（唯一parentbf033）；新dev/configure-files-fixture，原main/诊断分支不变。唯一run38674331460/job116224363738/attempt1 completed success；精确HEAD/parent gate及11真实文件fixture检查全Pass，FixtureCleanupConfirmed true/CleanupFailed false。真实句柄/64KiB×4/share/目录变更/junction拒绝及关闭删除在自有public临时fixture范围通过；合成版本不称host CMake版本。CMake/四case/原Lifecycle全NotRun，ModelCalls0/SecurityChanges0，无安装/权限/服务/扫描/重试。原失败原件保留。WindowsServer2022 public文件范围，不是P0/E06/普通Win11/UIA/OLE/多屏/安装验收。[实际回执与范围](validation/configure-files-windows-result.md)。后续此三文档证据不会改变workflow，不再触发CI。

### 2026-10-15 四文件诊断本地接入（Windows 集成 NotRun）

新指定环境初始51d0774，从已核远端b62af2f建立dev/configure-files-local-integration，bf033为祖先；唯一root集成写入、独审只读，旧任务不恢复。原四日志在此环境均缺失，未补造；CI38669606027的失败事实来自本次交接，未声称重新取回原件。原LifecycleOperation/两adapter/生产/三workflow不变，独立根lease在configure前持有；原Run救援/清理返回后、原失败抛出前通过真实源进程DuplicateHandle复制到Python，再用原adapter采集。未清理/无lease零collector，诊断异常不改原结果；四固定文件各64KiB/总256KiB，闭合事实及独立回执，lease最后关闭。旧compiler扫描不再整读两个configure日志，其他日志也只在确认清理后读取；原CMakeVersion前置首行门保留。

实际本地新bridge14/14、新runner注入127/127，原adapter21/21、fixture离线16/16、静态no-target9、evidence45、runner72通过；原SDKsetter105因SDK缺失NotRun。独审补正重复关闭目标句柄和枚举数组类型绕过；新C#仅编译/布局/非法输入检查，没有Win32或原CMake/四生命周期执行。尚未验证新lease/跨进程桥的真实Windows行为，不能用旧fixture11检查代替。原退出码/30秒/JobEmpty/救援/清理/250/600ms及安全门不变；根因仍unknown、ProductionReuse Off/ModelCalls0、P0/E06Blocked。无推送/新CI/main/强推/部署/新费用凭据安全网络权限。见[接入与边界](validation/configure-local-integration.md)和[本地固定证据](test-evidence/2026-10-15-configure-local-integration.txt)。

### 2026-10-15 隔离 Windows 跨进程桥验证准备

仅在新开发分支 `dev/configure-bridge-fixture` 准备独立工作流：只对该分支新增工作流文件的 push 触发，精确审定 HEAD/唯一 parent `2798893`/八文件差异，标准 windows-2022 单 job 上限10分钟、步骤7分钟。自有临时目录与合成四文件；先持有真实 C# root lease，再通过原30秒/job救援和清理封套启动真实 Python 子进程，检查实际 DuplicateHandle/文件投影/64KiB×4上限/拒绝无效证明/缺失/关句柄/删除。原超时 receipt 仅离线注入，原 CMake 与四生命周期仍 NotRun，模型调用0。旧工作流源码不变；旧文件fixture的 workflow 枚举合同补识别新隔离工作流，不削弱原分支过滤断言。离线与 Windows 实测结果分别记录，不把 bridge 通过声称原故障修复。[范围与边界](validation/configure-bridge-windows-plan.md)。

### 2026-10-15 隔离 Windows 跨进程桥首次实测通过

精确独审 66d671e526d81a1013e6401f2664b436a1468533 / tree 427b65347b392b55d10ac2bc825813c8daef8d9a 通过后一次正常 push 到隔离分支；[run38686940470](https://github.com/T1doo/KuaiYi/actions/runs/38686940470) / job116263848345 / attempt1 completed success。HEAD/parent/八文件门通过，15项真实桥、受控读取、无效证明拒绝及清理检查全部通过，CleanupConfirmed true/CleanupFailed false/OriginalResultPreserved true/Exit0。完整固定回执见[Windows证据](test-evidence/2026-10-15-configure-bridge-windows.json)，完整解码job日志与303项离线复核原始输出保存在本地ignored artifacts。源码提交仅有这一条新workflow运行，未重试或再次推送运行源码，旧分支/main不变。

原CMake与四生命周期NotRun，原失败/救援receipt在本fixture中仅合成注入，原NativeConfigure Timeout/PreRescueJobStateNonEmpty/BudgetExpired具体根因仍unknown。真实bridge通过不表示原生命周期或P0/E06/生产验收通过；250/600ms、来源/密码/完整性/取消/清理门不变，模型调用0。此实际结果的三份文档单独归档，运行源码与workflow字节不变，不命中任何workflow触发路径。

### 2026-10-15 独立 configure-only 本地准备

bridge结果三文档已交付 b4592d5，查询无新workflow run。后续本地新分支 dev/configure-only-diagnostic 只准备 configure→清理门→有限采集→结束的独立入口；configure成功也不进入build/restore/四case。旧runner、四workflow和原operation/lease/adapter/生产不改。新来源锁b459唯一child、完整parent tree、locked3ae/tree及runtime输入；实际owned浅克隆depth11拒绝、depth12通过，新workflow只匹配新分支与自身路径，未push或执行。

实际取回旧run38669606027/job116209346264的原解码日志并保全，确认NativeConfigure root Exit0、WaitJobEmpty Timeout、救援前NonEmpty/BudgetExpired、救援后cleanup确认；四原文件仍全部缺失，不推断残留进程根因。新增171注入与6来源/句柄引用次序检查通过，collector清理未知时零JSON读取、独立Unknown状态及原结果保留；不是新的Windows实测。原30秒/救援/清理、安全门及P0/E06边界保持，详见[本地准备与剩余缺口](validation/configure-only-next-run.md)。

### 2026-10-15 Configure-only 唯一实测：原超时保留，有限采集完成

后续单次执行授权下，精确4b29c3f95e9f5e5d16dfc501c445a7428fe74f27/tree50ceebb928f553640e1027b36799a7ced05392fa/唯一parent b4592d5正常交付dev/configure-only-diagnostic。九文件blob、祖先、单一写入锁及旧文件字节核验通过；该SHA仅一条run38690235831/job116274201714/attempt1，completed failure，无重跑。前九项来源/工具操作完成；NativeConfigure仍root Exit0、WaitJobEmpty Timeout、救援前NonEmpty/BudgetExpired，救援后JobEmpty/CleanupConfirmed true。runner Fail/OperationTimeout/ExitCode3未被诊断覆盖；Actions外层实际进程exit1单独保留。

清理确认后collector独立CleanupConfirmed，四真实文件有限投影均Projected/未截断：CMake3.31.6，ConfigureDone/GenerateDone/CompilerIdSeen，ABI Done/Working Skipped，VS2022 generator，try_compile事件。旧四原文件仍不可得，无补造；本次不导出四文件原文，64KiB×4是代码上限而非实测字节计数。记录仍不含救援前残留进程身份，根因unknown，未据完成标记宣称原Job正常收尾或生命周期修复。NativeBuild/ManagedRestore/ManagedBuild/四case及UIA/Match/Performance全NotRun，ProductionReuse Off/ModelCalls0、安全门和P0/E06边界不变。[完整实际结果](validation/configure-only-windows-result.md)及[固定回执](test-evidence/2026-10-15-configure-only-windows.json)单独归档；仅文档变化，无新运行代码或Windows尝试。

### 2026-10-15 Configure-only 外层退出码仅本地补正

从本地归档aa96555继续，在dev/configure-shell-exit-local仅将执行步骤改为官方custom pwsh -File模板并对占位路径加双引号，显式保留ErrorActionPreference Stop和exit LASTEXITCODE。真实Linux shell进程复现旧包装3→1，新包装保留0/1/2/3、异常失败与cleanup=false原摘要；实际Linux入口Blocked2/零操作。首版7项argv测试独审Hold其空格路径证明，保留原输出后改为真实.NET Arguments字符串、补未引号exit64及显式正文无runner修整反例，最终9项通过；原6项来源测试和171项注入断言通过。原runner/operation/lease/采集器/旧workflow共12文件字节不变，已有YAML解析及diff检查通过。[本地说明与根因缺口](validation/configure-shell-exit-local.md)、[固定证据](test-evidence/2026-10-15-configure-shell-exit-local.txt)记录准确范围。

原来源锁/depth/预算不改，此本地后代仍被已执行候选的来源门拒绝，不作为下一Windows冻结候选。Job等待超时仍缺救援前成员身份，未猜测性修改编译选项或放宽Job空/清理要求；没有push、新CI、CMake/四生命周期或模型调用，原实测failure与P0/E06/生产验收边界保持。

### 2026-10-15 自有 Job 成员有限诊断：独立本地实现

核对02043精确独审/远端/未推送差异后，在dev/job-member-diagnostic-local新增独立成员观察器和注入合同。只借用调用者持有的自有Job，单次固定72字节/8成员列表；进程仅最小查询+同步权利、前后成员/Wait0复核、固定映像buffer，闭合basename类别与未确认计数，无PID/路径/命令/异常导出。预算前/中/最终检查，错误或关闭未知清事实，原receipt不变；不复制/关闭/改变Job，不扫描系统、不扩权。独审补进程X64+指针8+OSX64门；411断言/63注入场景、6来源回归通过，16原文件/历史证据字节保持。三个相同Active1/原OperationTimeout反例分别区分MSBuild/PdbServer/Other，均SyntheticInjectedOnly，不能认作真实残留身份或根因。

同步native查询不保证硬耗时，工具尚无原Run/入口/workflow调用点，未影响原30秒及救援/清理；Windows真实布局/权限/句柄验证NotRun，也不是下一可执行CI候选。下一步应先冻结独立owned synthetic Job fixture及单次范围交父核授权，不重跑原失败CI。02043推原configure分支会命中workflow路径，不能以来源门拒绝冒充不触发；新本地开发分支不匹配五workflow过滤。本轮仍无push/新CI/CMake/四case/模型，原超时Unknown和生产安全门保持。[实现、取舍与下一步](validation/job-member-diagnostic-local.md)及[固定本地证据](test-evidence/2026-10-15-job-member-local.json)记录完整边界。

### 2026-10-15 独立合成 Job 的 Windows 验证准备

按新授权先核33de精确freeze/独审/远端和五workflow分支门，正常非force将aa965→02043→33de三提交保全到dev/job-member-diagnostic-local；该SHA推送前后run查询均0，原configure/main分支未动。新本地dev/job-member-windows-fixture只新增独立A8/B1自有Job fixture与新branch+selfpath workflow，parent33de/tree4a96/helperblob227378锁定，depth2完整九文件来源门。标准windows2022单job5分钟、来源1分钟、fixture2分钟；内部30秒共同观察截止及5秒清理，固定公开sleep进程、最小权限/白名单环境，无继承/扫描/原runner调用。

真实A8 ABI与受控B单候选非成员拒绝分开验证，不声称实际发生PID复用；失败/预算/清理未确认均不发布观察事实。所有自有root退出、两Job空及全部句柄关闭才确认清理，最后JobClose不冒充已观察清理。离线反例与独审并行，完整计数及最终冻结见[单次计划](validation/job-member-windows-plan.md)和[准备证据](test-evidence/2026-10-15-job-member-fixture-offline.json)。本准备批不推workflow候选、不触发CI/CMake/四case；父核精确源码、范围与授权后再接续一次Windows。原超时仍Unknown、安全门及P0/E06/生产验收保持。

本地实回执：新fixture992断言/49场景、真实浅克隆来源门5测试、既有成员诊断411断言/63场景、旧文件fixture16测试通过；Layout x64五结构尺寸仅为本地布局检查，Windows仍NotRun。新增Marshal.SizeOf断言因PS泛型重载先失败，原log保全，再以C#强Type重载修正；独审补live状态只接受WAIT_TIMEOUT，UnknownWait在Observe前拒绝。所有关闭后再次检查5秒清理截止，整体Fail不发布观察事实；未为此重跑旧CI。

### 2026-10-15 独立合成 Job 单次 Windows 结果

父核既有授权与独审后，精确候选2d736642c32bfb9f9871c0fec81741fc519f1861/tree ff960d06f2a766dc7b9a678d5cd14a518e0604da正常推新分支；该SHA仅run 38699679076/job 116304547116/attempt1，来源门Pass，fixture在OwnedJobCounts处Fail/Exit3。已创建并绑定9个自有root，清理确认：root退出、两Job空、fixture句柄关闭；未用直接终止，主/清理预算均未过期。两个成员诊断NotRun，观察和计数null；未证明A8真实查询ABI或受控非成员反例。独审按C#求值顺序确认OwnedJobCounts数量表达式已完成且false；实际数量未导出，不能区分A不等于8或A等于8但B不等于1，不猜测数量不符或子进程退出原因。

完整日志与tool回执保全在本地artifacts/job-member-windows-result，固定[结果证据](test-evidence/2026-10-15-job-member-windows-result.json)含运行链接及日志hash。失败后未重跑、不修改或再次推送执行候选；原configure/诊断/main远端未变，五旧workflow、原runner、CMake与四生命周期未运行。原configure超时根因仍Unknown，生产验收不变。

### 2026-10-15 初始 suspended 的自有 Job fixture 本地修复

保留bb0fae原失败归档与全部原日志，从该唯一parent建立本地dev/job-member-suspended-fixture。独立核查确认旧循环在集合数量检查前逐个Resume会允许脚本执行改变成员集合；这属于设计缺口，实际A/B数量未导出，不能认定旧失败唯一原因。移除Resume接口/调用/PInvoke，九root保持初始suspended至自有Job统一清理；A/B查询分开设固定阶段并检查共同预算，数量不符不短路B，异常/预算仍立即停止。原helper/entry/runner/五旧workflow和旧结果不改。

新注入2056断言/96场景通过，覆盖执行后退出/派生模拟、A错仍查B、所有九个创建/绑定/验证边界协作取消及清理取消、预算和关闭/清理异常；原helper411/63、旧文件fixture16通过。首轮来源合同在progress未产生差异时正确拒绝七文件范围，原log保留，完成记录后继续精确来源验证。完整计划、官方依据、一次运行边界与未完成项见[初始suspended计划](validation/job-member-suspended-plan.md)及[离线证据](test-evidence/2026-10-15-job-member-suspended-offline.json)。本批仅本地，不push、不新Windows、不重跑；两原诊断NotRun和原configure根因Unknown不改，不把离线结果冒充Windows或生产验收。

最终来源合同5项通过：真实浅克隆depth1拒绝、depth2通过、篡改helper pin拒绝；来源门仍锁唯一bb0fae parent、完整parent tree、helper blob及七文件。14份原helper/entry/runner/旧workflow/历史结果文件和两套旧私有manifest全部核对原字节；当前无新Windows证据。

### 2026-10-15 初始 suspended 自有 Job 单次 Windows 实测

父核新阶段授权后，精确ddb62cb9fb2574ae9459161e9d7f52739c99a6e5/tree a4617837498c7f3c6f1522d0164c1e00f8551e23正常推dev/job-member-suspended-fixture，唯一run 38709320255/job 116336490490/attempt1 success，来源门与全部11项检查Pass/Exit0。Created9全程未Resume；A8/B1成立，原helper实际查询A8，Other8且所有未确认计数0；受控自有B非成员反例Partial/MembershipUnconfirmed1，单次List/Open/Member/Close各1，Wait/Image0。root退出、两Job空、fixture及查询句柄关闭均确认，主/清理预算未过期，无直接终止兜底。

完整tool回执和21419字节原日志保存在本地artifacts/job-member-suspended-windows-result，准确字节/hash与闭合回执见[固定实测证据](test-evidence/2026-10-15-job-member-suspended-windows-result.json)。只证明本标准host上的初始suspended自有合成Job ABI、有限查询、受控非成员拒绝与清理；不声称真实PID复用或旧数量失败唯一根因。MembershipUnconfirmed合并No/Unknown，本回执不能区分明确非成员与原生成员查询未确认，只证明拒绝并零Wait/Image。旧run38699679076及bb0fae归档原字节保持；原configure超时根因仍Unknown、原runner未接入，CMake/四生命周期NotRun、ProductionReuse Off、ModelCalls0。没有重跑、改main、强推或额外workflow触发。

### 2026-10-15 原 runner 成员诊断接入可行性审查

保全1dfafc成功Windows归档及两套原日志，从其唯一parent状态本地核查WaitJobEmpty接入。同步once probe可在软预算契约下实现返回/异常后的状态隔离与原finally顺序，但不能保证不返回时及时救援；29700→30100ms的有限迟返反例中，Job于29900ms空，原真实CompleteJobWait仍会由本可Completed变Timeout，即使helper已清事实为SkippedBudget。原Run本有同步API且没有CancellationToken，不能把“不直接覆盖receipt”混同为反事实时序结果不变。

新增纯提案可行性合同42断言/13场景通过，复用原Capture/CompleteJobWait/GetReason，虚拟时钟/注入IProbe，未运行原Run/Win32。首轮PowerShell long::MaxValue语法失败log保留，修正后通过。真实hook暂HOLD，11份原源/入口/六workflow/成功结果原字节保持，成功/失败两套manifest核对不变；无push或新Windows。请父核软预算风险是否在现有契约内，或采用只投影既有Accounting返回值、零新增native探针的最小替代，不能硬接或削弱原超时/清理门。[精确冲突、最小替代与条件执行计划](validation/job-member-runner-integration-review.md)和[固定离线证据](test-evidence/2026-10-15-job-member-runner-feasibility.json)记录范围。原configure根因Unknown，P0/E06及生产验收不变。

### 2026-10-15 既有 Accounting 两点快照最小接入

环境实际命令/文件恢复可用，核118981a精确parent和单一写入者后，仅复制既有成功Accounting查询的Active/Total，固定FirstWait/LastPreRescue两点；未查询/失败Unknown/null，清理Empty(job)不携带诊断对象，不能覆盖预救援值。原整个Run、while短路/clock/等待及finally字节保持，零新增native/Stopwatch读取/重试/线程/进程句柄；不承诺绝对零指令耗时。只在原操作与lease结算后另输出闭合计数行，旧Operations/operation JSON投影与原因不变，投影/新前缀输出失败独立Unavailable且原summary/exit继续。

针对性162断言/11场景、原configure171及旧证据45回归通过，父/当前等待轨迹逐项一致，清理布尔/异常及旧receipt原因一致。旧来源6项中5通过1拒绝当前Accounting改动，保留旧冻结门和完整失败日志，当前不是可直接执行CI候选。17份原文件/六workflow/配置原字节及两套旧manifest核对；原缺失Directory.Build.targets未补造。Active只是Job关联数、Total包括已终止，不用Terminated推断全部退出或根因。输出合成样例、轨迹、精确范围及后续单次计划见[本地接入说明](validation/configure-accounting-local.md)和[固定离线证据](test-evidence/2026-10-15-configure-accounting-local.json)。本轮无push/新Windows/CMake/四case；原根因Unknown，所有原安全门和生产验收保持。

### 2026-10-15 AccountingV1 独立来源合同准备

从9d75368精确父基线准备唯一child，旧门/旧workflow/日志保持。三runtime版本精确固定，新入口ReviewedParent同步9d75368；实际checkout深度门及离线反例验证中，本地尚未推送或执行CI。

AccountingV1准备实际验证完成：独立来源5项通过，真实浅clone21拒绝/22通过；三运行blob/canonical SHA精确固定、working text归一核验，错误父/event/hash、runtime漂移、额外/大小写scope全部拒绝。旧六来源合同原字节保留，当前4通过2拒绝，旧日志不回填。162/11、171、45本阶段实际复跑通过。新独立branch+selfpath workflow来源1分钟/执行7分钟/job10分钟，六旧workflow保持，最终freeze独审后按本阶段明确授权仅正常推精确SHA一次；此准备记录尚未执行Windows。[完整来源及唯一执行计划](validation/configure-accounting-next-run.md)与[准备证据](test-evidence/2026-10-15-configure-accounting-source.json)保留精确范围和失败harness日志。

### 2026-10-15 AccountingV1 唯一 Windows configure-only 回执

最终冻结独审后，a0e8519/tree712d5a9 唯一父9d75368正常推新开发分支一次；真实提交浅clone21拒绝/22通过，Windows来源门成功。同SHA唯一run38716240995/job116359501515/attempt1最终failure：NativeConfigure仍WaitJobEmpty Timeout，root Exit0，救援前NonEmpty/预算过期，救援后JobEmpty与CleanupConfirmed均true，summary及Actions步骤均Exit3。两次既有成功Accounting查询均Known Active2/Total33；仅Job关联/累计计数，不能识别进程或根因。四真实文件均Projected无截断，collector清理确认；有限诊断未覆盖原失败。完整回执及边界见[结果说明](validation/configure-accounting-windows-result.md)与[固定实测证据](test-evidence/2026-10-15-configure-accounting-windows-result.json)。不重跑，本结果仅本地归档；原根因Unknown、四case未执行、安全门及Off/0保持。

### 2026-10-15 EmbeddedV1 单变量静态候选准备

原失败结果a1291b99已归档。精确CMake3.31.6 peeled commit859ca5c4下，CompilerId独立VS模板不消费该变量，ABI source try_compile传播变量及CMP0141；现工程3.28 minimum在project前已使CMP0141 NEW。准备仅新增CMAKE_MSVC_DEBUG_INFORMATION_FORMAT=Embedded配置，ReviewedParent仅同步来源身份；编译端/Z7有明确机制，链接/debug和MSBuild节点复用保持，不能推断原两个Job关联成员身份或承诺修复。静态链接及不确定性见[候选说明](validation/configure-embedded-static.md)与[来源合同](test-evidence/2026-10-15-configure-embedded-source.json)。本批本地独审/来源测试后交父复核，Windows/CMake/四case/CI均NotRun，不push，原安全门和Off/0保持。

### 2026-10-15 EmbeddedExecV1 执行来源门准备

父已授权最终精确独审通过后唯一一次configure-only对照。以b771f1e/tree300daf为唯一父，新显式EmbeddedExecV1门、新branch+selfpath隔离；七旧workflow/AccountingV1合同/原日志保持。runner相对静态候选仅ReviewedParent同步，相对baseline仍唯一Embedded运行配置及身份差异。新child实际重测checkout深度，原30秒/JobEmpty/救援清理和全部采集安全边界保持；原171合同拒绝保留，owned精确更新期望后的171不能称旧门原样过。限制和对照字段见[执行计划](validation/configure-embedded-execution.md)。准备快照尚无新CI，实际结果另档。

### 2026-10-15 EmbeddedExecV1 唯一 Windows 对照回执

精确执行5acccc753/tree74a77d7，唯一父b771f1e，真实提交浅clone24拒绝/25通过、最终来源独审通过后正常推新branch一次。run38719840129/job116371397705/attempt1最终failure，根Exit0/脚本及Actions Exit3分列：仍WaitJobEmpty Timeout、救援前NonEmpty、预算过期，救援后JobEmpty/清理与collector清理均确认；两点Known Active2/Total33、四文件Projected/Facts及CMake3.31.6与baseline38716240995相同。存在明确环境漂移：image20261012.333.1→20261004.326.1、provisioner20261012.601→20261002.596，不能称完全同环境或排除CompilerId/链接器PDB。已停止不重跑、不加成员probe；根因Unknown/真实四案例与生产验收未通过。事实与边界见[结果说明](validation/configure-embedded-windows-result.md)和[固定实测证据](test-evidence/2026-10-15-configure-embedded-windows-result.json)。本结果仅本地归档，原安全门及Off/0保持。

### 2026-10-15 独立产品推进：P2-04阅读滚动回调

核SPEC P0—P8和当前代码/最近文字Alpha计划后，选择现有P2生产阅读缺口，不扩到未退出的P5/P6。原Loaded回调仅resultRevision不能拒绝已观察阅读动作后的旧滚动；新增UI滚动序号，接真实wheel/key/mouse/touch、捕获滚动、“有新内容”及隐藏/收起入口，保留结果/锁屏/退出守卫。相同最终11断言的owned旧回调6失败/5通过；修后Provider532/532（含11专项）通过，App locked Release交叉构建0警告/错误。FIFO与逆序端口测试明确区分，不能替代WPF输入/内部滚动/完整B组实机验收。

现有两次CI保全目录没有四原文件字节，MSVC版本/debug格式/ABI命令无法从有限投影恢复，不补造/不新增投影或CI。rootExit0/configure+generate完成与JobEmpty等待失败、脚本Exit3分开保留。详细阶段依赖、诊断可用性和测试边界见[修复说明](validation/overlay-reading-scroll.md)与[固定证据](test-evidence/2026-10-15-overlay-reading-scroll.json)。本批只本地实现/独审/测试，不push/Windows/CMake/成员probe，原250/600ms及全部安全门、P0/E06/生产验收状态和Off/0保持。

独立/tmp专项11/11通过；额外首次SDK启动报告安装ASP.NET Core HTTPS开发证书，未执行trust。按私人路径限制未核证书存储，实际变化Unknown，原输出保留，不宣称证书状态未变。

### 2026-10-15 P2-04实际阅读输入接线接续

新授权后已正常推送4a99e260及f741归档到不匹配保留CI分支的dev/overlay-reading-input；八workflow字节不变。继续核实际接线，移除把程序化ScrollChanged当阅读的follow写入，改由明确阅读输入/ScrollBar.Scroll立即pause，受修订号保护的Background观察当前几何后用原24DIP阈值恢复；新刷新抢先则保守保持pause。隐藏重显/Begin/版本切换已有失效门保留，不扩原纯队列fixture。新增6项生产policy测试，Provider538/538、App Release交叉构建0警告/错误，独立源码审查通过。Windows真实布局输入及P2/B组未验，原CMake根因Unknown与250/600ms/全部安全门保持。

原证书提示日志/TRX保全，旧精确独审命令未记录，不补造；实际存储/信任Unknown，不读库、不trust/delete。当前每次dotnet前显式DOTNET_GENERATE_ASPNET_CERTIFICATE=false并继承到子进程，临时CLI home不代表证书隔离。详情和限制见[接线说明](validation/overlay-reader-intent.md)及[固定证据](test-evidence/2026-10-15-overlay-reader-intent.json)。

### 2026-10-15 P2-04/B06/B07有限接续：新内容提示归位

指定环境仍Linux，无真实WPF能力；已有实际浮窗入口需Windows且不自动执行B06输入交错/B07连续反向，实际交互均NotRun。审查发现上读提示显示后切换缓存版本会follow=true，但Flush只显示不清除提示，展开态旧提示可跨版本残留；仅补齐Flush属性同步，有正文且暂停才显示，其他情况Collapsed。未改滚动守卫/收起动画、不增外围适配器或重复原队列测试；旧538/538仅前批基线，本轮不重跑。App交叉构建和独立源码审查的实际结果见[修复说明](validation/overlay-notice-reset.md)及[固定证据](test-evidence/2026-10-15-overlay-notice-reset.json)。Windows输入/布局/提示可达性、B07反向/帧时间、P0原CMake根因仍未解决，P2-04 InProgress；false证书变量、Unknown存储/信任和所有原安全门保持。

### 2026-10-15 §11.5受限Markdown接正式浮窗

原OverlayResults/50ms刷新链改用原生只读FlowDocument展示段落、ATX标题、顶层单行列表和围栏代码；有界模型保留精确Source，只重解析变动尾段，稳定块与原生块按引用复用。粗体/斜体/行内代码/表格/复杂列表/打开链接未实现，不支持部分保留完整字面文本；HTML/图片/链接不加载或执行。120000单元/2048块以上整份有限结果纯文本回退，工具栏复制仍取原始结果；三版本、失败回退、迟到隔离、取消/锁屏清空及阅读/提示守卫保留，无新依赖。

最终Provider566/566=新28+原538、App交叉构建0警告/错误；保留首assets缺失0执行、首26/27夹具误选已淘汰版本及中间565回执。WPF排版/选择复制/横向滚动/阅读锚点、B06/B07与完整§11.5均NotRun/未完成，P2-04仍InProgress。旧证书Unknown与false变量、250/600ms及所有原安全门保持，无新CMake/Windows实验。详见[生产切片说明](validation/restricted-markdown.md)及[固定证据](test-evidence/2026-10-15-restricted-markdown.json)。

### 2026-10-15 §11.5原生行内格式续接

新环境实际核文件/Git/默认网络/进程和祖先后，从250548896d4f1d61669f20e9b575ed9d85c23bde建立dev/overlay-inline-markdown；旧任务不恢复，本任务唯一写入者、独审只读，未知Git锁保留。正式RestrictedMarkdown→FlowView新增平面行内代码、粗体/斜体与ASCII标点转义，直接构造Run/Span；保留块和片段精确Source、原始结果复制、未闭/嵌套交叉/歧义整块字面回退、稳定前缀与变动尾段。新增4096累计行内片段上限，稳定计数随清空重置，超限整份纯文本不截断；无依赖或加载/执行入口。

实际专项72/72、完整Provider610/610（前566+新44）通过；6项直接链接生产FlowView/OverlayMarkdown的Linux数据端口测试加38项真实Core行内测试，覆盖实际三版本缓存/失败回退/原始文本/取消锁屏清空/迟到门。首轮71/72的非追加夹具误用保留原log/TRX，修为真实追加后通过，未改变生产契约。App Release交叉构建0W/E，独审无阻塞；WPF排版/选择/实际字体继承/阅读锚点及B06/B07仍NotRun，基础表格和代码横向滚动明确未完成，P2-04仍InProgress。

八workflow、43锁及既有证据原字节保持，250/600ms与全部安全门/生产离线复用Off保持。所有dotnet前显式false证书变量，旧存储/信任Unknown，不读库/配置/trust/delete。一次只读Actions查询Forbidden后停止、不绕拒绝；未主动发起CI，服务端运行数Unknown，不能声称已核0run。无新Windows/CMake实验、main/强推/部署/真实模型或权限费用变化。详见[续接说明](validation/restricted-markdown-inlines.md)及[固定证据](test-evidence/2026-10-15-restricted-markdown-inlines.json)。

### 2026-10-15 §11.5基础表格生产续接

从已接收063969062de54c6fda50bc9d683d185789e7d25c核工作树/单一写入者/默认网络及远端祖先后建立dev/overlay-markdown-tables，旧实例不恢复、独审只读。受限基础表格支持分隔行和左/中/右对齐、转义及代码内管道、平面行内样式；16列/128行、2048累计单元格与原4096片段/120000单元/2048块共同约束，超限整份保文回退，复杂/未闭单元格整表字面回退。精确块/行/分隔行/cell Source及原始复制路径保持；表头和分隔行前瞻，部分非pipe尾界不提前冻结，开放表整块尾部更新、确定边界后复用。

正文和测量均经生产FlowView实际构造Table/RowGroup/Row/Cell及原生Run/Span；无新依赖或图片/链接/资源执行。最终Provider657/657（前610+新47，41真实Core+6链接生产桥接）通过，App交叉构建0W/E；本轮无失败，保留72/117/中间657回执及前批71/72原失败hash。独审补强逐行列有序断言和转义奇偶覆盖后复跑全量，未把审查当父级重跑。WPF实际宽度/布局/选择/阅读锚点/开放表重建与B组仍NotRun；代码横向滚动仍未完成，P2-04仍InProgress。

250/600ms及全部安全门、生产离线复用Off、8workflow/43锁/旧证据保持。Actions访问拒绝继续停止、不再查询、不绕路，CIUnknown；dotnet前显式false，证书不读不改且原状态Unknown。无main/强推/部署/费用凭据权限/真实模型或Windows/CMake实验。详见[表格生产说明](validation/restricted-markdown-tables.md)及[固定证据](test-evidence/2026-10-15-restricted-markdown-tables.json)。

### 2026-10-15 围栏代码局部不折行滚动

从已接收93fa2eed5ab5287f58affb9daa3e8bf7bc7baace核单一写入者/HEAD/默认网络远端和干净工作树后建立dev/overlay-code-scroll。生产围栏代码改为本地只读BlockUIContainer/TextBox，NoWrap、双Visible局部滚动条、有限宽度和240DIP/正文高度上限；正文/测量使用共同有限页宽/高度及HDisabled/VVisible外层策略，真实MeasureMarkdown有限Measure调用，普通段落不设无限宽，拖动跨小屏夹紧。原文复制和惰性文本、无新依赖保持。

局部输入接阅读意图和原修订门；独审发现初版outer-bottom重开follow及开放代码重建丢状态后，补明确代码阅读暂停与显式恢复/版本重置、同版本追加复用控件及受局部/全局输入/结果/锁屏退出守卫保护的选择偏移恢复；测量不排队，替换/容量/清空解绑清文。最终Provider699/699=原657+新42，App交叉构建0W/E；首次端口可空性CS8622编译失败0执行原log、171/174专项和中间695均保留，旧19回执hash保持。真实WPF横滚/测量/选择/阅读锚点/父子路由均NotRun，不称B12或P2完成。

8workflow/43锁/旧证据及全部Core安全文件原字节保持，250/600ms和安全门/生产离线复用Off不变。Actions访问继续停止不绕，运行数Unknown；所有dotnet显式false，证书不读不改仍Unknown。无Windows/CMake实验、main/强推/部署/费用凭据权限/真实模型。详见[局部代码说明](validation/overlay-code-scroll.md)与[固定证据](test-evidence/2026-10-15-overlay-code-scroll.json)。

### 2026-10-15 长表格局部横向滚动

从已接收06d1df264c737a09bda81ac2e597cdc7dcec0e1d核文件/单一写入者/HEAD与默认网络远端祖先后建立dev/overlay-table-scroll，只接SPEC4.2长表格视口。原生Table/Row/Cell/Run/Span保持，外包只读RichTextBox/FlowDocument局部滚动，宽度与正文/测量共同有限页宽一致，高度min(320DIP,正文上限)，列96—480DIP、内部页宽最多8192；字体估宽不是实测。开放表追加复用原生表及稳定cells，局部阅读复用原暂停/修订守卫，替换/容量/清空解绑清文；原文复制与惰性处理保持，解析器和代码区实现不改。

独审修复全局symbol offset及末格闭符后移两个选区缺口，改稳定cell内offset并归一有效插入边界，合并追加和迟到恢复受局部/输入/结果/锁屏/退出门保护。最终Provider740/740=前699+新41，App最终交叉构建0W/E；首xUnit2031编译失败0执行原log、218专项和初版App回执均保留，旧29回执hash保持。Linux显式符号端口仅测生产协议，真实WPF表格宽度/横滚/排版/多格跨区域选择/系统复制/阅读锚点/路由仍NotRun，B12与P2-04未完成。

8workflow/43锁/1570旧证据及全部45Core生产文件原字节保持，250/600ms及全部安全门/离线复用Off保持。Actions停止查询不绕拒绝、CIUnknown；dotnet显式false，证书不读不改Unknown。无Windows/CMake实验、main/强推/部署/费用凭据权限/真实模型。详见[表格局部说明](validation/overlay-table-scroll.md)与[固定证据](test-evidence/2026-10-15-overlay-table-scroll.json)。

### 2026-10-15 原回执保全、末尾区域跟随及原生验收准备

从8226f11d66719afab2413e06166591c3e245af54核实际文件/远端/单一写入者后建立dev/overlay-tail-follow。七份原始回执重新独立逐字节/hash/XML核740/218及219/40/41，两次App0W/E、首xUnit2031零执行、22源blob与前29回执一致；七原字节复制唯一新证据路径，原产物不改，仅含合成测试/runner元数据。[独立核验](test-evidence/2026-10-15-table-scroll-independent-audit.json)和原副本持久化保全。

按SPEC4.6/B06优先修实际“有新内容只滚外层”缺口：唯一末尾代码/表格接全局follow与原局部24DIP判断、明确跟随、首次Loaded，保选区/H偏移和暂停阅读；多次追加合并、输入/版本/锁屏退出清空迟到门保持。独审补主题重载one-shot、incoming表尾提升和失去尾资格时降级原锚点恢复。最终Provider774/774=前740+32生产跟随+2语料，App/GrowthHarness交叉build各0W/E；保留247/248生产失败、772/774语料误用及各中间回执。冻结通道断连后同名TRX覆盖警告保留，首次执行/覆盖前字节Unknown且未保全，不补造；七原回执不受影响。

原生准备只扩既有Production ShellWindow工具的固定合成--actual-markdown入口，保存与源码SHA一致的公开语料并更新Windows清单；无新外围工具/CI/抓取/模型/凭据或加载入口。Linux缺WindowsDesktop，WPF真实横滚/测量/选择/阅读锚点/路由/剪贴板均NotRun，B12/P2-04仍未完成；Stop终态Flush/标签缺口明确留后续。8workflow/43锁/1571旧证据与45Core生产文件原字节、安全门/250/600ms/离线Off保持；Actions停止查询CIUnknown，dotnet显式false、证书Unknown且不读不改。见[本轮说明与失败边界](validation/overlay-tail-follow.md)及[固定证据](test-evidence/2026-10-15-overlay-tail-follow.json)。

### 2026-10-15 Stop终态Flush与真实版本标签

从已接收70c03a5f2d40d294ff04a3bdd9396526bf77cb7e核文件/单写入者/默认网络/HEAD和远端祖先后建立dev/overlay-stop-finalize，补上批明确留下的生产缺口：主动取消不发Stopped，原Stop没有dirty/Flush/标签更新。现原Stop先取消门控，标停止并清等待/自动展开，刷新真实所选版本标签，复用原50ms合并刷新；无任务/重复/已完成时无操作，不改阅读/收起/三版本与迟到门。为直接测试，原Apply/Flush/标签逐段精确迁移到链接的正式partial，不增第二套状态机。

最终相同28专项旧Stop7通过/21断言失败，修后完整Provider802/802=前774+新28，相关281；App Release交叉构建0W/E。覆盖排空/待刷、围栏/表格终态、未闭保文、空结果、取消不发事件、真实缓存标签、Stop排队前后版本切换的实际render、确认保留/连接测试边界、收起和阅读选区偏移。原r01的19生产失败+1夹具超时及r02/03夹具超时分开保留，仅修夹具自身等待的取消释放，不宣称真实HTTP/服务端时限。独审与实际回执见[修复说明](validation/overlay-stop-finalize.md)及[固定证据](test-evidence/2026-10-15-overlay-stop-finalize.json)。

前批16份现存原回执及本批19份成功/失败log/TRX复制唯一受-text保护路径持久提交；旧七份保持，前批frozen覆盖前字节Unknown/未保全不回填。8workflow/43锁/1581旧证据与45Core生产文件原字节，250/600ms及全部安全门/生产离线Off保持。WPF排版/选择/阅读位置/路由、取消时限和B组仍NotRun，P2-04/完整11.5未完成。Actions不查询不绕Forbidden、CIUnknown；dotnet显式false，证书Unknown且不读不改；无Windows/CMake实验、main/强推/部署/新依赖/凭据费用权限协议或真实模型。

### 2026-10-15 所选版本原文折叠与实时绑定

父签收3235cf86af6652addd032de03b6123498ec2d875后，核干净工作树/单worktree/唯一写入者/远端一致，从精确父建dev/overlay-original-source。指定环境仍Linux、仅NETCore/AspNetCore10.0.12、无WindowsDesktop/DISPLAY/WAYLAND；B06/B12/B07含代码/表格暂停Stop、历史重开和50次反向均NotRun。补充现有Windows清单的最小解阻与入口能力边界，不建外围工具/CI，不拿数据端口冒充原生。

推进SPEC4.2文本原文折叠：移除最近采集缓存+独立Window的错配路径，只读所选SelectedSnapshot.RawText，卡片内原生Expander/TextBox默认折叠，惰性纯文本、同版流式追加保原文选区。footer独立于Markdown模型与尾区，正文/测量共享有限预算、原文高度有界；展开/阅读接原暂停门，显式查看才排受保护的布局后定位。独审补长译文顶部命令可见性和B失败回退A时Flush前实时版本资格/同步清理，原文/发送Text、结果复制、Stop/Off本地回看分开保持。

最终Provider840/840=原802+新38，相关319；App Release0W/E。首52/53是新夹具漏计空段，保留原失败、精确修为6块并核末段；中间827/837/839及三次App构建回执均保留。13份本批原始log/TRX复制唯一精确-text路径，父42份已提交回执逐字节核准；38源码manifest和8workflow/43锁/1617旧证据/45Core及2344其余既有文件保持核验。见[实现/失败与原生限制](validation/overlay-original-source.md)和[固定证据](test-evidence/2026-10-15-overlay-original-source.json)。

原生Expander测量/样式、BringIntoView位置、系统选择/阅读锚点/路由/取消时限与完整P2仍未验；图片预览、生产一键清空入口明确留后续，本批内部Clear测试不冒充已有用户命令。250/600ms和全部安全门、生产离线Off保持；Actions不查询不绕Forbidden、CIUnknown，dotnet每次显式false，证书Unknown且不读不改。旧覆盖前字节Unknown不回填，无main/强推/部署/新依赖/费用凭据权限协议/真实模型或Windows/CMake实验。

### 2026-10-15 生产清除当前内容与图片任务释放

父签收74ad0c9b0266f33b3708bf3ecb55d870306aff70后核clean单worktree/唯一写入者/远端一致，建dev/overlay-clear-content。SPEC8/14.1清除入口现接主按钮菜单、浮窗更多和设置隐私页；每次同步epoch fence并复用Coordinator.Clear清所有版本/源/结果/控件/捕获与Drop意图，ClearAsync只等待本次旧jobs。On/Off/Hidden/Settings保留，不读删设置、凭据或未知磁盘文件；新任务不被旧清除完成提示覆盖。

复用并加固既有本地截图清理：原任务故障不吞，首次填充至UI构造/显示都在bitmap Own范围内，尝试detach并逐行zero、所有cleanup失败保留；selection/snapshot/intent/owner分步结算，单纯held owner可安全重试。共享截图cleanup给重复Clear/退出，旧Restore/observer隔离；Drop Clear等待原reconcile，退出await前锁存停止，Refresh/Clear不再发起结算，在途reconcile不能建立新peer；失败保held owner、显式退出重试，未确认不报完成。图片原文预览仍下一轮，无新功能绕行。

最终Provider884/884=原840完整+新41Clear+3Drop调度，App交叉构建0W/E。真实Coordinator/请求门、Source/Markdown清除及实际Snapshot/Crop/PNG缓冲归零执行；WPF位图、窗口、owner及Drop调用仅端口协议，真实平台/取消时限/B组仍NotRun。首17/29的12例共同提示状态缺失与修后29/29、两次881、最终884及三次成功App原回执全部保全；另保留r08漏EnableWindowsTargeting的NETSDK1100零执行/0W1E，r09修命令后通过。独审补退出与迟到Refresh对同peer重复finalize的最小latch，真实调度测试用原生端口而非OLE实机；完整884含原本地HTTP测试，无socket仅限新增Clear用例。新14份精确-text，父55份原件不变；独审/manifest/失败和限制见[说明](validation/overlay-clear-content.md)及[证据](test-evidence/2026-10-15-overlay-clear-content.json)。

250/600ms及全部安全门、生产离线Off保持，旧覆盖前字节Unknown不回填，dotnet每次false且证书Unknown不读不改。Actions不查询不绕Forbidden、CIUnknown，无main/强推/部署/依赖费用凭据权限协议/真实模型或Windows/CMake实验。

### 2026-10-15 所选本地图片来源折叠预览

父签收2a51a5ec06f24948cb412dcd5eb06e11cf8caaf3后核clean/单worktree/唯一写入者/远端一致，建dev/overlay-image-source。现有Shell没有图片译文版本，故只将真实截图最终裁剪图接为明确“本地图片·未翻译”的来源卡，独立版本ID、不造正文/请求/启用或上传；文件/浏览器/视觉调度及P6仍未交付。SelectedSource统一接正文/测量/来源/标签/菜单/回看/复制/风格再生成边界，原生Expander/Image默认折叠。

单owner转移裁剪BGRA，24MP/16384限制；原图不随版本或测量复制，展开才产生最多1024²的有界本地缩略图。折叠释放显示副本，清空/锁定/退出/真正替换释放原图，失败保held bitmap重试；读图及Loaded接版本/owner/阅读/Capture/Hidden门。取消新框选保旧会话但撤旧展开，文字失败优先可读文字rollback，再兜底原图片版本折叠。独审补事件异常/双故障聚合、空白重试、截图主try、锁屏/正常退出及等待设置退出的独立清理；不以取消令牌证明COM结束。

最终Provider923/923=父884完整+新39（28预览/接线、11像素所有权），App两次0W/E。r01的26/28为新夹具漏EndScreenshotRestore、r05的922/923为隐藏重显端口未映射真实Show，均保原log/TRX且补真实生命周期不降断言；r02 28/28、r03 920/920、r07最终通过。12份新原始回执精确-text保全，父69份保持；说明/独审/manifest/实际失败见[图片预览说明](validation/overlay-image-source.md)和[固定证据](test-evidence/2026-10-15-overlay-image-source.json)。

WPF布局/像素释放/阅读/选择/实际截图、COM/broker/所有权和取消时限及完整P2/P6/B组仍NotRun。250/600ms与全部安全门、生产离线Off保持，旧覆盖字节/证书存储信任Unknown不读改；每次dotnet显式false，无Actions查询/绕拒绝、Windows/CMake实验、main/强推/部署/依赖费用凭据权限协议/真实模型。

### 2026-10-15 受限HTTP/HTTPS显式链接

从已签收8fa5cb38cdd43467302c7ad8a27d28aad2fb7e91核clean/单worktree/唯一写入者/默认网络远端后建dev/overlay-explicit-links。SPEC11.5平面链接由Core产精确Source/显示Text/不可变规范目标，实际正文/测量接原生Hyperlink；无NavigateUri/自动加载，只有正文显式Click，经域名确认、所选版本/元素/reader/result/epoch及可见展开/取消/锁屏退出重验才请求系统浏览器。测量无动作，代码惰性、复杂或危险语法整块/整表保文，原始复制及稳定块/尾段保持；表格替换单元格及Clear解绑旧链接。URL/label及原文档预算有界，禁非HTTP(S)/userinfo/控制/格式字符/反斜杠等，域名ASCII IDN及混淆IPv4规范后明示；图片On提示改为“本地预览尚未翻译、未发送”。

最终Provider1014/1014=父923完整+新91（55解析/目标、34生产链路、2图片文案），App最终0W/E。真实Coordinator三版本/Stop/Clear/迟到门执行，仅本机确认/launcher及原生数据端口替换，不打开真实外部网址。r01定向186/188的两失败为新测试误将Capture/Settings暂停当取消已有请求，保原件并按原Gate契约修新额外断言；r02 219/219、r03 1007/1007及两次App成功均保全。独审无阻断，补确认中稳定尾段/换单元格、空userinfo与数字IPv4策略；10份新原始log/TRX精确-text、父81原件不变。详见[实现与失败边界](validation/overlay-explicit-links.md)及[固定证据](test-evidence/2026-10-15-overlay-explicit-links.json)。

WPF真实路由/测量/选择/阅读位置/确认框/浏览器启动、截图/COM/所有权与取消时限及完整P2/P6/11.5仍NotRun，无外围工具/Windows CI/CMake实验。250/600ms和全部安全门、生产离线Off保持；旧覆盖字节/证书Unknown不读改，所有dotnet显式false、Actions不查询/绕拒绝、CIUnknown；无main/强推/部署/依赖费用凭据权限协议或真实模型。

### 2026-10-15 P6-04/05独立离线截图分类与生成编排

从父97a17e7f4f3f201a15471936881681a26f73f4d9核实际文件/clean/单worktree/唯一写入者/默认远端及祖先，建dev/screenshot-image-routing。先读SPEC16依赖/退出门、7章及原双协议/截图合同；Linux无DISPLAY/WAYLAND/WindowsDesktop，P2/P4原生验收不可执行，B06/B07/B12、真实Alpha与图像质量继续NotRun。不造新验收工具/CI、不绕隔离；依AGENTS及SPEC16.11/18.1仅推进不依赖实机/凭据的离线切片。

新增严格有限ImageClassification、路由/模式覆盖及OfflineScreenshotCoordinator：只接现有截图owner，构造必须固定合成loopback边界、AuthNone/TEST_ONLY_FIXTURE、明确Supported视觉能力；Adopt不发送，仅On+显式用户动作启动两阶段。JSON须协议完整终态后严格验证，不入正文；本次用户要求非法/不完整fail-closed，直接停止而不按旧SPEC非法降级继续外发，合法uncertain才带提示解释。无可靠OCR的纯文字也带图按所选style翻译，混合/视觉阶段携同一图；复用原PNG编码与双协议序列化、共享单调总预算，原crop超过2560/4MiB/profile限制即拒绝，无UI缩略图替代/新规范化。会话模式与三版再生成/显式重判、source/version/epoch/binding及取消清除锁屏退出迟到门执行；正常Shell/截图/菜单没有接发送，生产离线复用Off及阶段门保持。

最终Provider1118/1118=父1014完整+新104（55分类/路由、45编排、4导出所有权），App两次0W/E。实际生产组件和原Provider/Kestrel双协议流式/非流式、同图MIME/base64、缺终止、排队A/B及clear/Stop/cleanup执行。独审找到Gate先安装新id再取消旧CTS抛错导致新Job无归属：r04实际2通过/1失败，原源码/测试和原回执保全；组件捕获后只结算实际out新id并保原错/清理错，Core不改。r01 100/100、r02 1107/1107、r05 115/115、r06最终通过及r03/r07App均保全。12 SDK原log/TRX+2红版源码精确-text，父91原回执不变。详见[阶段与实现边界](validation/screenshot-image-routing.md)和[固定证据](test-evidence/2026-10-15-screenshot-image-routing.json)。

P6-04/05仅InProgress、整体验证NotRun；真实截图/多格式规范化/OCR、视觉语义/质量、WPF测量选择阅读焦点、COM所有权/时限及完整P2/P4/P6仍未验，不能宣称图像Beta或生产通过。250/600ms及全部来源/密码/完整性/取消/清理门保持；无新依赖、模型、费用凭据权限协议、Windows/CMake实验/main/强推/部署。每次dotnet显式false，证书存储信任/旧覆盖字节Unknown不读改/重造，Actions不查询/绕拒绝、远端CIUnknown。

### 2026-10-16 P6接纳重入与实际分类提示契约修复

从04f4cdb85634d46726b4de78c3b18e018fa15932核文件、Git/default网络远端祖先与单一写入者，建立dev/image-admission-reentry，独审只读，main保持原SHA。本批先修两项P2，usage新增暂停。真实红测r01 10项8失败2通过：Gate安装B后取消A，Clear/Stop/Adopt/Dispose非抛错回调仍能被外层B恢复旧状态，回调启动C也会被B覆盖；实际分类system未给出parser要求的readability等合同。修为canonical Gate内按命令修订、原source/owner、settings/binding/epoch/lifecycle二次证明后才提交结果及缓存；拒绝仅结算B，保留C。修正尚未发布image-classify@1的精确字段、枚举/类型/上限及可解析示例，双协议实际wire验证；非法完整JSON仍不发final，无parser弱化。

初版修后专项135/135、全量1138/1138、App0W/E。独审发现拒B后已取消partial A仍可能显示生成中，r05真实6项4失败2通过保全；仅当active和结果仍是原A且Gate已不接受A时清active/标Stopped，保留partial、原图及分类，并断言C不被标停。最终专项139/139、Provider1142/1142（父1118完整+新24）、App0W/E；14份SDK log/TRX及5份两次红版源码精确-text保全，父105原回执与1685既有证据不改。详见[修复说明](validation/image-admission-reentry.md)及[固定证据](test-evidence/2026-10-16-image-admission-reentry.json)。

Core/Provider parser与序列化、正常Shell截图、其余7模板、8workflow/43锁不变。250/600ms及来源/密码/完整性/取消/清理门、生产离线复用Off保持，普通截图仍本地未翻译未发送；P6-04/05 InProgress/validation NotRun，真实Windows/WPF/COM/采集/OCR/模型质量及完整P2/P4/P6 NotRun。所有dotnet显式false，旧证书存储信任/旧覆盖字节Unknown不读改重造；Actions不查询调度绕拒绝，run数Unknown。无main/强推/部署/新依赖费用凭据权限协议、Windows/CMake实验或新CI。

### 2026-10-16 P6分类与生成独立usage回执

从a3b01123d452a9eb11c1f27d66b98a13a220e878核实际文件/干净Git/单worktree唯一写入者/默认远端祖先，建dev/image-stage-usage，独审只读，main不动。续接离线截图组件的两阶段usage：每阶段不可变观察回执绑定本地枚举ID、完整canonical身份、原图/来源版本与实际协议；字段仅取服务端实际报告，Unknown不补0、不猜token/费用。每字段最后有效值替换（含0/向下修正），重复不加、不取最大值；Usage事件与正文、完成/失败解耦。缺失/非法usage元数据保持未知但不破坏合法正文/协议终态，原choice/block严格解析、完整分类和取消门不改。

analysis与分类回执同canonical缓存，再生成引用原回执并标Reused，不造第二笔分类消耗；三版淘汰后只保有限原元数据，显式重判同时更新缓存。当前请求usage按job.Identity归属，浏览旧版本不串账，跨协议生成保留原分类协议并用新协议生成。失败/中断/停止保已接纳观察，Clear/换图/锁屏/隐藏清除引用；Usage通知重入及排队迟到沿原Eligible门拒绝后续写回，模式只影响下一次显式生成。本地InvocationId仅是一次Provider枚举尝试，不能证明发出/计费/覆盖服务端未知消耗；无价格/总额或费用零假设。

首轮数据契约已加而消费逻辑尚旧，实际双协议流/非流HTTP红测0过/4失败及3份红源码保全；接入后r02 73/73、扩展r03 155/155、r04全量1228/1228、r05归属4/4、r06及r08最终Provider1232/1232（父1142完整+新90：58编排、32parser）、App0W/E。r08仅收紧异步用例等Usage通知的同步条件，防止读取快照抢先，生产未改。覆盖实际请求body/阶段、缺失/坏数/重复/向下修正、usage不完成、两阶段失败中断/取消/迟到、Clear/Adopt/模式/C重入、三版重判/旧版阅读/跨协议复用。15 SDK log/TRX+3红源码精确-text，父124原件与1705既有证据不改。详见[合同与回执说明](validation/image-stage-usage.md)及[固定证据](test-evidence/2026-10-16-image-stage-usage.json)。

普通Shell/截图/菜单仍未接发送，On截图本地未翻译未发送，生产离线复用Off；250/600ms和来源/密码/完整性/取消/清理门保持。Core、ProviderClient/receiver/budget/请求序列化、8模板/8workflow/43锁不变，无新依赖。P6-04/05 InProgress/validation NotRun，真实WPF/Windows/COM/截图/OCR/模型和视觉质量及完整P2/P4/P6 NotRun；不造Windows/CMake实验/新CI/绕隔离。dotnet激活前后及命令前显式false，旧证书/覆盖字节Unknown不读改重造，Actions不查不调不绕拒绝、run数Unknown；无main/强推/部署/费用凭据权限新协议/真实模型。

### 2026-10-16 P6-02最终crop白底归一化与强类型ImageTransform

从f463dd419df2593eb0e799f64644c5b38d477458核实际文件/原实现、cleanGit、单worktree/唯一写入者/默认远端祖先，建dev/screenshot-normalization，独审只读。OwnedSourceImage新增受限PrepareRequest：既有最终straight BGRA crop白底合成，1:1保可视像素；只对白底后全图RGB逐像素严格恒定的极窄无结构例外等比fit到2560，其他需要缩小的图像保原预览并返回SMALLER_SCREENSHOT_REQUIRED、零HTTP。不能靠ratio/采样/平均亮度代替文字可读性，不能先发分类论证缩图；lossless PNG与工作缓冲4MiB/profile硬cap，超限重截，不循环降质/裁边/使用缩略图。旧原尺寸导出保超2560拒绝合同，正常截图入口仍只本地预览。

ImageTransform规划与可发送owner分开，记录原crop物理Bounds/尺寸、identity截图方向、整数输出与实际每轴比例。Original/Normalized/Desktop点和矩形分别强类型，UnitImageRectangle另表分类[0,1]，pixel-edge双向/实际bbox映射，拒绝非有限/越界。source锁内逐行复制，锁外合成/扫描/编码及取消检查，临时owned像素/PNG在finally清零；Prepared事件在原canonical提交点写映射后再Eligible，分类/生成只用同一PNG，analysis同步缓存原mapping并拒绝不等价复用，模式/三版/usage原回执保留。无EXIF/OCR/文件解码或新依赖。

实际r01白底红测1P/5F，原owner/encoder及初6测试保全；r02 CS0121重载歧义编译失败0执行、无TRX，原log/源码保全后修明确point类型。r03 185/185、r04 211/211；独审发现新mapping复用坐标类型，r05真实0P/1F及两份红源码保全，拆独立空间后r06 216/216。最终Provider1306/1306（父1232完整+新74：44归一化、30编排），App0W/E；实际PNG解码CRC/白底、原件清零/有限cap/取消、1000随机坐标往返/非法边界、双协议两阶段同图、Prepared无缓存/缓存Stop/C重入及排队迟到均执行。14份SDK log/TRX+6修前源码精确-text，父142原件和1724旧证据不改。详见[实现与可读性边界](validation/screenshot-normalization.md)及[固定证据](test-evidence/2026-10-16-screenshot-normalization.json)。

P6-02保持InProgress/原validation Blocked，P6-04/05保持InProgress/NotRun；完整多格式样本、SkiaSharp版本/许可证审查、EXIF、隔离解码、OCR/一般文字缩小可读性均未完成，真实WPF/Win32/COM/图像质量及完整P2/P4/P6/I/J NotRun。250/600ms和来源/密码/完整性/取消/清理门、生产复用Off、Core/Provider/8模板/8workflow/43锁保持。dotnet每次显式false，证书/旧覆盖字节Unknown不读改重造；Actions不查询调度绕拒绝、run数Unknown；无main/强推/部署/新费用凭据权限协议/真实模型或Windows/CMake/新CI实验。

### 2026-10-16 ImageTransform 贴边坐标与既有 bbox 错拒

从已签收1b658b598f96f4af1790411ec1b68774a6e80be8核clean/单worktree/唯一写入者/默认远端与祖先，建dev/image-coordinate-boundaries。先保留给定负原点.1/99.9往返及旧分类.8/.2错拒红测0P/2F。桌面矩形按分别平移的端点重建宽高；物理点与框先验原空间边界/剩余宽高再减原点，防真实外邻值被消去。已验宽度等于limit-start时恢复其确定贴边端点，不用epsilon；identity轴保值。单位bbox另共用有限double最短往返十进制R的BigInteger精确比较，旧parser错拒同批修，明确.7/(1-.7)的canonical十进制超界会有意拒，不宣称二进制或原JSON任意精度合同。

r02专项101/101；r03 170P/2F均为新增夹具错误要求跨零相加恒等，不是旧min生产失败证据，保4份原源码及失败回执，补精确End并将不可保证加法等式改为最大残差等式，外邻值及20轮断言保留；r04 172/172。最终Provider1379/1379=父1306完整+新73，App0W/E；24组crop含大正负/混合原点/两向缩放、四边/宽高立即邻值、跨零点反例、tiny/decimal精度反例与parser/transform共同单位合同执行。11SDK log/TRX+7红源码精确-text保全，父162原回执和1745旧证据保持。独审/源码manifest及限制见[修复说明](validation/image-coordinate-boundaries.md)和[固定证据](test-evidence/2026-10-16-image-coordinate-boundaries.json)。

既有Core/全部App、其他Provider/Imaging、8模板/8workflow/43锁不改；250/600ms与全部来源/密码/完整性/取消/清理门保持，普通截图仍本地预览、生产离线复用Off。P6-02仍InProgress/Blocked，P6-04/05仍InProgress/NotRun，多格式/EXIF/隔离解码/Skia/OCR/一般缩图可读性及真实WPF测量选择阅读/Windows/COM/质量/取消时限与完整P2/P4/P6/I/J未验。所有dotnet显式false；证书/旧覆盖字节Unknown不读改重造，Actions不查询调度绕拒绝、run数Unknown；无main/强推/部署/新依赖费用凭据权限协议/真实模型、Windows/CMake或新CI实验。

### 2026-10-16 P6-02隔离PNG/JPEG解码依赖前置审计（实现阻塞）

父fcf4271efa8e1283e3c0b803662e37f54d0a692e核clean/单worktree/唯一写入者/默认远端后建dev/image-isolated-decode，无同目标运行实例。官方NuGet4.153.1四个固定候选包及全部条目摘要已核，net10实际传递Win32/macOS，Linux.NoDependencies只拟测试；包声明源码4783f51448f9b070dda4f87b83e941c9599e466e。四包实际requireLicenseAcceptance=true，MIT及原生第三方原文保全；原生notices含DNG SDK、LGPL/eCos等，不能概括全MIT或称完整组件审查通过。源码API两次403停止，不绕拒绝；显式许可授权待答，未restore/安装/加载/执行包、未改版本/锁/生产代码/测试或安全门，哈希是候选审计而非已安装锁定。

本批只交付12份官方元数据/许可/库存原件与核验记录、原一次性worker/分片/尺寸/方向/取消/真实exit合同设计；没有解码实现、样本红绿/SDK/App/Windows实际执行，父1379与App0W/E不冒充本批测试。初始API/ZIP nuspec逐字节断言因BOM/换行失败，分别保原件后核规范化文本相同，未改原件；只读路径误猜与web/403失败完整注明。独审/版本许可与资产摘要及阻塞见[依赖前置说明](validation/image-isolated-decode-dependency-audit.md)和[固定证据](test-evidence/2026-10-16-image-isolated-decode-dependency-audit.json)。

P6-02保持InProgress/Blocked，P6-04/05 InProgress/NotRun。生产离线复用Off，普通截图本地未翻译未发送；250/600ms、Job/process及来源/密码/完整性/取消/清理门不改。Skia需获明确许可并解决实际源码组件审查才接入；全格式/EXIF/OCR/一般缩图可读性与真实Windows/WPF/COM及完整P2/P4/P6仍未验。无SDK/证书库配置trust删除、Actions查询调度绕拒绝、新协议接受/权限/费用/凭据/真实模型/WindowsCI/CMake实验、main/强推/部署。

### 2026-10-16 现有ImageWorker私有传输/取消/清理闭环

父83c821f47937b076889c64f881db68034d0f925c核HEAD/clean/单worktree/默认远端及唯一root写入者，建dev/image-worker-transfer-cleanup，worker_review只读独审。不接Skia，沿原私有worker实际缺口修终态Receiver复活、非协作读挡finally、hello异常未清nonce、control先失败清仍被recognize借用原图。Receiver锁存终态/清identity；Client取消等待与原I/O任务分开，晚读只清/观察，原write+Flush真正settle才清发送buffer，nonce独立比较副本。原调用/IO/进程或启动未结算拒最终释放，process已退但IO未清报SettlementUnconfirmed并保同owner；二次Run不重置active。实际WorkerSession保持Program原NativeOcr绑定，hello finally与不可跳过recognize/control双join，正常reply先于EOF，次要故障只记有界类型。

r01新夹具7诊断编译失败0执行/无TRX，源码原件保全后修夹具；r02真实0P/9F全部生产缺口，原代码及红源码保全。r03 9/9、r04 23/23、r05 locked restore/r06 35/35、r07 1414/1414；r08Worker及r09App交叉构建0W/E；独审补latecontrol真实3byte清零与2条线协议边界后r10最终Provider1416/1416=父1379完整+新37，无跳过。真实托管child与测试引用Worker DLL逐次按配置/hash绑定，另固定4对实际依赖DLL；合成识别/流屏障仅验生产协议所有权，Linux正常结果UnsupportedPlatform，不冒充PNG/JPEG/NativeOCR。17SDK log/TRX+10baseline/编译/红源码精确-text保全，父192原件/1777旧证据保持。详见[说明](validation/image-worker-transfer-cleanup.md)和[固定证据](test-evidence/2026-10-16-image-worker-transfer-cleanup.json)。

现有第三方版本/contentHash全保持，仅Provider测试锁增加3个自有Project节点，其余42锁/8workflow/8模板及Core/Contracts/App/Provider生产保持。所有dotnet激活前后、命令与测试子进程显式false；无新包或协议接受/权限费用凭据模型/main/强推/部署/CI/CMake/Windows探针。250/600ms、原15秒worker/client和来源/密码/完整性/Job/process/取消清理门保持，生产复用Off；Actions不查调不绕拒绝、run数Unknown，证书Unknown不读改trust删除。P6-02仍InProgress/Blocked，04/05 InProgress/NotRun；Skia四包许可待答/源码403、PNG/JPEG codec/EXIF/OCR、WPF真实测量选择阅读、Windows/COM/取消时限与完整P2/P4/P6仍未验。

### 2026-10-16 P6-03 OCR-only 本地预览与显式文字发送

从30425cc03130f3fa9ecc94777a54aac64219fd71核实际文件/cleanGit/单worktree/唯一root写入者及默认远端/main祖先，建dev/ocr-only-confirmation；只读ocr_review独审。核P0只有本地OCR、正常Shell只有图片预览后接正式截图PNG→原Worker/NativeOcr→本地只读预览→显式确认→既有TextCoordinator四风格。显示“当前模型不支持图像理解，图形关系未分析”、文字目标/模型/费用提示；独立provenance+当前已批准对象引用，默认拒OCR、普通取词排除门不变。无图片送文字模型、分类或非法分类自动降级，缺平台/身份/语言/取消零HTTP，不下载重试或伪置信度。

先保1×1/正subnormal贴边红测0P/2F，ValidateOutput改严格剩余宽高判界，无epsilon，零extent/精确剩余合法。原worker/IO终结前不批准；未确认保owner拒新任务，原Run持有PNG直到返回，旧Clear只结算捕获owner；App异步返回重验原图版本/引用及epoch/On/可见状态，不覆盖新源提示，捕获原协调器并核当前字段引用，退出不访问已清空字段。Gate→局部锁序；回调后复核revision/ref/identity/epoch/设置，r06新增来源证明重入真实1P/2F及原源码保全后修复。确认正常交接只清预览，已批准文字可在PNG释放后保三版和按当前设置/会话风格再生成；清空/换源/Off/锁/隐藏及迟到门保持。

r02 2/2、r03/r04各46/46；r05夹具编译3诊断0执行/无TRX保全，修字段/枚举/初始化；r07 59/59、r09 62/62；r10/r16 Provider1478/1478，最终r17 Provider1481/1481=父1416完整+新65（62OCR合同+3链接生产Shell清理端口）、0失败/跳过，双协议×四风格真实本地HTTP文字wire、合成OCR成功，实际托管worker仅Linux UnsupportedPlatform。补取消异常不跳过原清理：OCR Clear/Close分开汇总，Hide沿Attempt、Begin/Release finally推进状态/释放图像。r08/r11/r12/r13/r14/r15 App/Worker/最终App交叉构建各0W/E，独审无阻断。27SDK log/TRX+7原源码+3核验记录精确-text保全，父219原件/1805旧证据原字节；43锁/8workflow/8模板/项目引用不改，无新依赖。详见[说明](validation/ocr-only-confirmation.md)及[固定证据](test-evidence/2026-10-16-ocr-only-confirmation.json)。

P6-03 InProgress/NotRun，02仍InProgress/Blocked，04/05不改。Windows真实OCR/包身份/语言与质量、WPF窗口/测量/选择/阅读/焦点、COM/实际取消时限及完整P0/P2/P4/P6/I/J仍未验。生产离线复用Off、250/600ms及全部来源/密码/完整性/Job/process/取消清理门和原15秒保持。所有dotnet激活前后/命令/新子进程显式false；证书/旧覆盖字节Unknown不读改trust/delete。Skia许可待答、源码403与Actions拒绝门保持，不查询调度绕拒绝，CI run数Unknown；无真实模型、新费用/凭据/权限/协议接受、Windows/CMake探针/新CI、main/强推/部署。

### 2026-10-16 OCR 孤儿 canonical 身份终结

从f78401d8803f80f8302e19d8d88c7c0bf9b5e354核clean/单worktree/唯一root写入者与默认远端/main祖先，建dev/ocr-terminal-identity；UNKNOWN不恢复，ocr_terminal_review只读独审。先保真实r01 1P/6F：Settings/Mode失效完成跳终态却清局部active；Gate安装B后旧A取消回调抛错、B尚未保存也成孤儿。生产仅OCR协调器：Run finally无条件Mark本次identity，Start try覆盖Gate安装及后续证明，异常只Stop已out的B，不清共享owner/Running或全局Stop；原错bare throw，清理另错aggregate保两对象，后继C不伤。

r02 7/7、r03专项77/77；r04完整1499/1499=父1481完整+新18，无失败/跳过，独审逐项核测试名。覆盖启动前worker/factory/HTTP0及恢复连接测试准入、held/queued阶段Settings/Mode/C与原owner清理、A/B清理回调创建Gate或Local C后抛错、原错/清理错对象保全；Local C明确Drain后单独worker1预览、HTTP始终0。App/Worker交叉构建各0W/E。10SDK+3源码+1preflight原件精确-text保全，父37/累计256原回执和1843旧证据不变；43锁/8workflow/8模板/项目引用/依赖不改。详见[修复说明](validation/ocr-terminal-identity.md)及[固定证据](test-evidence/2026-10-16-ocr-terminal-identity.json)。

Core Gate/Provider/App/其余生产与原断言不改；P6-03 InProgress/NotRun，02 InProgress/Blocked、04/05不改。生产复用Off，250/600ms/原15秒及来源/密码/完整性/Job/process/取消清理门保持；Windows真实OCR/包身份/语言/质量、WPF测量选择阅读/窗口、COM/原生取消时限及完整P0/P2/P4/P6/I/J仍NotRun。所有dotnet激活前后/命令显式false；证书Unknown不读改trust/delete，Skia许可待答/源码403及Actions拒绝门不绕，Actions不查不调run数Unknown；无main/强推/部署/新依赖协议费用凭据权限真实模型、Windows/CMake探针或新CI。

### 2026-10-16 P6-03现有OCR菜单的已安装能力发现

父1ac32bede4ce7a9415c22b676c08ba071eb67412核实际文件/clean/单worktree/默认远端祖先及唯一root写入者，建dev/ocr-installed-capabilities；旧UNKNOWN不恢复，ocr_capabilities_review只读独审。现有“仅提取图片文字”实际先经原隔离Worker的NativeOcr.Diagnose，新增受限私有105/106帧复用nonce/job/15秒/进程和原I/O结算，无图无HTTP；替换手输语言为不可编辑已安装语言选择，显示并消费本机尺寸及原2560/4MiB门。成功不可变快照绑定原图/来源版本/设置/完整身份，实际识别前再验；缺平台/身份/语言明确阻断，取消/迟到/换配置来源及后继C精确身份结算，保父B异常/终结修复。正常截图仍只本地预览，识别后仍需只读文字预览和既有显式确认才按四风格文字发送，无安装组件/自动视觉发送。

最终Provider1575/1575=父1499完整+新76（58发现、18私有协议），App/Worker最终各0W/E。真实生产DLL及Provider/Kestrel双协议测试已安装语言/尺寸、错误能力、不可变快照、清理未确认、非协作原读写flush和worker诊断/control双join、精确缓冲清零与重入迟到；实际Linux托管子进程仅hello+diagnose，UnsupportedPlatform退出，四DLL与测试执行目录哈希相同。成功Windows能力为明确合成合同，不冒充实机。r04新测试CS8752/CS9174编译失败0执行无TRX，精确源码与log保全后修类型；r06后修新夹具default使三例实际测试非法成功报告，生产未变、中间测试源码未保全不重造，最终r07绑定当前源。24新原件精确-text，父270原回执/1858旧证据及未改父文件保持。详见[实现和验证边界](validation/ocr-installed-capabilities.md)及[固定证据](test-evidence/2026-10-16-ocr-installed-capabilities.json)。

P6-03 InProgress/NotRun、P6-02 InProgress/Blocked、04/05不变。Windows真实包身份/语言/原生上限与识别质量、WPF语言窗口/焦点/测量/选择/阅读、Win32/COM/截图/原生Job/时限及完整P0/P2/P4/P6/I/J仍NotRun。250/600ms与全部来源/密码/完整性/取消/清理门、生产离线复用Off保持，Core/Provider/8模板/8workflow/43锁/依赖引用不改。每次dotnet显式false，证书存储信任Unknown不读改；Skia许可及源码403门保持不接不绕，Actions不查不调不绕拒绝、run数Unknown，无Windows/CMake/新CI/main/强推/部署/真实模型/费用凭据权限新协议。

### 2026-10-16 文字翻译逐版本实际usage与正常结果展示

父49980109cc178292769f305eee5080dfe20b5dd4核clean/单worktree/唯一root写入者/默认远端及原祖先后建dev/text-version-usage，text_usage_review只读独审。接SPEC12.1 TranslationVersion/14.4/F04：正常选区、拖放文字及OCR预览确认后的TextTranslationCoordinator消费原双协议Usage，Core缓存按完整身份与正文同版保存不可变input/output；新版本Unknown，字段最后有效覆盖（含0和向下修正）、缺失保原知识、重复不累加不估算。原Provider队列预算/取消/source/settings/binding门再验，现有标签与版本菜单第二行显示所选版本完整数值/Unknown，不并入正文或费用。Usage只刷新标签不动reader/展开/等待/终态；三版/再生成独立、失败rollback/跨源fallback、停止/Clear/迟到及Clear/C通知重入保持，普通截图和生产图片复用仍Off。

最终Provider1737/1737=父1575完整+新119（95文字合同、24展示/生命周期）+原Core两文件43（35+8，源码不改），App/Worker各0W/E。实际Provider/Kestrel/双parser/receiver/Core和链接Shell运行；48三来源×双协议×流非流×四风格走真实本机文字wire，OCR成功仅合成native输出经实际本地预览批准/显式文字确认，不冒充Windows。r01新夹具CS0117编译0执行无TRX；r02 75P/22F为新Anthropic active-block顺序及模式旧请求错误预期，原log/TRX和精确失败源码保留，修夹具不改parser/Gate；r03 95/95、r04 119/119。r05 1690P/4F为旧deadline通知预期遗漏新增Usage，保原测试和项目/回执后严格扩事件序列并补actual/Unknown计数断言，无删除/滤事件/降低断言。Provider.csproj仅Compile Link原Core缓存回归，不新增引用/依赖/锁或运行其Windows/CMake链路。24新原件精确-text，父294原件/1883旧证据及未改父文件保持。详见[实现与失败边界](validation/text-version-usage.md)和[固定证据](test-evidence/2026-10-16-text-version-usage.json)。

F04 InProgress/NotRun，P4-04/05 InProgress/NotRun，所有P6不改。真实WPF标签菜单/测量选择阅读、Windows OCR身份语言/质量、截图/COM/原生Job/250/600ms和完整阶段、完整Core.Tests仍NotRun。原全部来源/密码/完整性/取消/清理/250600与15秒门保持，43锁/8workflow/8模板及真实模型/图片生产发送不动。每次dotnet显式false；证书库配置存储信任Unknown不读改，Skia许可/403及Actions拒绝保持不接不绕、run数Unknown，无main/强推/部署/新费用凭据权限协议/WindowsCMake或新CI实验。

## 2026-10-16 · SPEC8.3 设置内置图视觉测试（本机 fixture）

父444c2dd5dc166e26a6edab7c6eba8d81df66e909核文件/clean Git/单worktree/唯一root写入者/默认远端与250祖先后建dev/settings-vision-fixture-test，vision_probe_review只读独审。设置页分开文字/视觉测试，视觉只发固定内置公开2034byte PNG，显式单次确认、专用目的/完整endpoint+protocol身份、最终发送复验与私有数组快照；原开发fixture边界AuthNone/固定模型/无头密钥，普通远端准备零HTTP阻断。文字仍拒图，普通Unknown/Unsupported图片仍阻断，不改能力/不启用On/生产复用Off。结果按认证/连通/普通输出/流式/视觉/错误分别说明；Stream请求遇JSON回退不冒充SSE，视觉始终Unknown。模态命令占位、编辑/取消/Closed撤销、配置身份/总及分阶段超时/排队迟到与A/B/C抛错保全均接原链路。

最终Provider1857/1857=父1737全保留+新120（后台56、隔离34、实际设置命令30），App/Worker交叉构建各0W/E。r01/02/03新测试编译问题0执行无TRX，r04新锁屏夹具115P/2F；原log、精确失败源码和TRX保全，改夹具使用原API，无门控/parser弱化；r05新旧设置169/169。28新原件精确-text，父318累计原件/1908旧证据/未改父文件及43锁8workflow8prompt保持；无依赖新增，实际命令Compile链接和固定资源仅项目改动。详见[实现与失败记录](validation/settings-vision-fixture-test.md)和[固定证据](test-evidence/2026-10-16-settings-vision-fixture-test.json)。P4-01/F07/F10/F11及P6不抬整体验收；真实Windows/WPF/OCR/COM/Job/250/600ms和模型理解仍NotRun，证书存储/信任Unknown。所有dotnet显式证书生成false；Skia待授权/403及Actions拒绝门保持，Actions数Unknown。无真实模型/费用/凭据/权限/协议接受/main/强推/部署/新CI/CMake探针。

## 2026-10-16 · SPEC10.8/F06/F08 文字失败终态与恢复

父c5e4f695f4d9570e4c8221f9cf9f0454e3ba6d48核clean/单worktree/唯一root/默认远端后建dev/text-terminal-failures，terminal_failure_review只读独审。Core有界failure枚举与完整identity版本/原文/usage同寿命，不保原始错误/refusal/异常文；明确截断/拒答中性原因持久，跨源保失败尝试+所选旧正文+最多另一旧版仍总3，标另一来源且不把失败贴旧正文，空失败可选/回看/手动重新生成，旧来源ordinal不乱。原生可聚焦状态按钮、完整AutomationName、原菜单/版本/重新生成/API设置复用，32DIP同时计测量/正文；首个空失败受原门展开、手动收起/清空/新Begin/隐藏失效旧展开。所有destination事件复验source/settings/binding和回调后canonical gate，失效仅中性Abandon恢复最后可读fallback，finally仅oldid，不伤C；OCR确认文字失败保版本不再清回图片。新UI无自动HTTP/增token，原10.7无正文429/503有界策略保持。

最终1948/1948=父1857全保留+新91（87双协议/三来源/生命周期/展示、4发送前抛错零连接），App/Worker各0W/E。r01 125P2F为旧“丢弃跨源失败”断言；r02 224P1F为旧图片清缓存/late receiver断言；严格改为失败和旧正文元数据各自保留及terminal内容不变。r05完整1940P2F：真实回归来源失效漏fallback修中性Abandon，旧视觉HTTP先到事件后到竞态只加等待；均保原log/TRX/精确源码。r03 231/231、r04 241/241、r06 1942/1942；之后独审补无正文secret异常terminal返回缺口与4实际0dials用例，r07 1946/1946后复核OpenAI JSON拒答早退丢合法content/usage，保候选源码后修，强化48拒答部分正文组合并加2精确refusal分离例；r10最终1948/1948、r11/r12最终App/Worker各0W/E。42新原件精确-text，r01原TRX8行SDK原生行尾空格使首次cachedcheck失败，仅该精确回执-whitespace保原字节；父346累计原件/1937旧证据保持；43锁8workflow8prompt/依赖无变，测试项目仅1生产partial Compilelink。详见[实现和失败记录](validation/text-terminal-failures.md)、[固定证据](test-evidence/2026-10-16-text-terminal-failures.json)。

纠正当前缺口记法：基础表格、代码及表格局部横滚已在更早生产源码/便携测试接线，629/637等历史段仅指当时状态，restricted系列加历史注；父vision误沿用缺失句已纠正，其旧JSON原字节保留。真实WPF焦点/Tab/菜单/实际显示/选择/横滚/阅读锚点和B12/完整11.5仍NotRun，不把源审+端口/几何/交叉构建当实机。P4-04/F06/F08/P2-04及全部P6不抬整体状态；图片生产复用Off、Skia/403/Actions/Windows门及250/600ms保持。所有dotnet证书生成显式false，证书库/信任Unknown且不读改/trust/删，无真实模型/费用/凭据/安全权限/协议接受/main/强推/部署/新CI/CMake。

## 2026-10-16 · 父审P2：连续失败版本号与回退提示

父8666a62f93aeefa6b6cc96488c33bddb63a89709核clean/单worktree/唯一root/默认远端与250祖先后建dev/failure-version-sequence，failure_sequence_review只读独审。实际A1成功→B1失败回退A1→选B1手动B2→B2失败回B1，r02父生产10执行0P10F（6项next应3实2，4项旧失败notice遮新失败），红绑定核源码等父和执行DLL。Core仅fallback.Append(failed)按回退session取max，保旧高号也纳入刚失败号，不改显示掩盖；同源续到B5/next6、identity/source/usage归属/三版/晚到/取消/Abandon/Clear保持。notice默认公告最新失败、正文旧；手选旧失败才读其原因，新失败撤销手选意图，原控制/尺寸/门不改。

r01新测试少using编译0执行无TRX原件保留；r03同测试SHA10/10绿，r04完整1958/1958=父1948+新10。新增notice四例继续补手选B1→B3失败重置；r05完整1957P1F为旧离线图片重入C正文为空，根因未实证，原测试/回执保全，r06原样隔离4/4不当已证明。补等待时r07过宽测试编辑产生两项过早Complete失败，保精确源码/回执后仅原失败单方法补确切C完成身份事件等待和Complete/Completed断言，保全部原断言及其它方法原字节；未改图片生产。r08最终1958/1958、App/Worker最终各0W/E，独审最终源码与证据；28新原件、父388累计原件/1980旧证据/43锁8workflow8prompt及项目依赖不变。详见[红绿与范围](validation/failure-version-sequence.md)、[固定证据](test-evidence/2026-10-16-failure-version-sequence.json)。父批r05仍准确保一项来源失效丢正文真实回归、一项视觉事件等待竞态，绝不全称夹具。

本批只修Core序号与同路径notice，不推进新片；旧离线例等待/调度解释仍未证明，生产复用Off。所有dotnet证书生成显式false，证书存储/信任Unknown且不读改/trust/删；Skia/403/Actions/Windows门不动，Actions数Unknown；无main/强推/部署/付费/凭据/安全扩权/真实模型/待批许可接受。真实WPF可见/键盘路由与Windows/OCR/COM/nativeJob/250/600ms和整体验收仍NotRun。

## 2026-10-16 · 父审P2续修：淘汰后的来源高水位

从远端 d45ba9d4ab1af3675cb7d0a36cfe0f77618965d0 在 dev/failure-version-sequence 正常接续；新实例初始为main早期快照，经默认网络核远端/fetch/祖先后切开发分支，不恢复旧实例。A1/A2/A3成功→B1失败回A3→选B1/B2失败回B1→C1失败淘汰B2→D1失败后原next重用2，实际应3。生产仅OverlayResults改独立session高水位，Begin才消费，Fail/Abandon回退不重算；按仍保留版本/隐藏fallback来源维护与清理，Clear释放，最多四项GUID/int；原source/usage/identity/晚到/notice/三版规则不改。

本续修r01父红8F；r02初修6P2F是新增生命周期夹具缺A3而误断B2被淘汰，精确源码/log/TRX保留，补齐初始路径不降断言；r03最终候选父红8F、r04同测试字节8P。r05完整Provider1966P（父1958全名保留+新8），r07完整Core1551P，均0失败/跳过；Core仅Linux便携与owned托管夹具，Windows实机不抬；App/Worker Release交叉构建各0W/E，独立只读审查未发现需要修复的问题。证据见[追加报告](validation/failure-version-sequence.md)、[本续修原始证据](test-evidence/2026-10-16-retained-source-high-water.json)。旧测试、原证据、项目依赖/锁/workflow/prompt原字节保持，P4-04/F06/F08及全部P6整体状态不变。历史图片间歇根因仍未实证，未改图片生产，复用Off。Actions先前拒绝不查不绕；Skia许可/403待批；真实WPF/Windows/模型/250600/全SPEC仍NotRun，无main/强推/部署/费用/凭据/安全扩权/新许可。

## 2026-10-16 · SPEC10.8：失败版本等待建议

从已独审8b902b1在dev/retry-after-advice正常接续。每HTTP响应在typed读取前有界解析Retry-After（单值≤128字符，delta/date，0..86400秒），仅最终429/503失败绑定UTC建议时间；过期/非法/超长/多值/超界不保留，最终无值也覆盖前响应。ModelEvent→原身份/截止门→Core失败版本→原状态Tooltip/Automation和版本菜单显示明确UTC与手动重试，非倒计时；不留原始私密头。Begin/Clear/淘汰、失败回退及旧版source/正文/usage/建议归属保持，timeout/cancel改判清建议，Abandon不伪失败。原10.7条件/最多一次/15秒/默认1秒/Delay及发送门不改，没有新后台计时/重发。

r01父生产4F、r02同测试4P；r03两项新测试请求数假设错误（超长数字typed无有效值，原缺省重试一次，实际2），r04四项新测试把跨来源上下文前缀误认为B身份恢复后不变，精确失败候选source/log/TRX保全、不算产品红测；校正后r05最终81P。r07完整Provider2047P=父1966全名保留+81，r09完整Core1551P，均0失败/跳过；r11/r12 App/Worker各0W/E，独立代码/测试审查无P1/P2/P3；证据见[报告](validation/retry-after-advice.md)及[原件](test-evidence/2026-10-16-retry-after-advice.json)。唯一旧测试文件变更是PhaseHttpFixture新增逐响应头/状态供新测试，旧断言不改。旧图片根因未知与复用Off、Windows/WPF/模型/250600/全SPEC NotRun及Skia/403/Actions拒绝边界保持，无main/强推/费用/权限/真实模型。


## 2026-10-16 · SPEC8.3：四字段显示偏好本地导入导出

从已独审0bdf754df6b44b2764bea014c63e6e6d51424d60核clean、唯一root写入者/单worktree/默认远端与main祖先后，正常建dev/display-preferences-transfer。独立六键JSON文档只含format/schemaVersion及字号、首选宽度、主题、减少动画；手写白名单/4096字节/精确版本、类型、未知/重复转义键和有限范围校验，不用全配置SettingsJson，不遍历秘密或配置档。实际设置入口先预览，再明确应用草稿，原保存/取消和CAS/恢复不变；导出冻结四表单值预览，仅新文件不覆盖，文件已发布后不伪报撤回。对象+revision原子apply，编辑/BeginSave即使失败/只读/关闭/取消/迟到/新预览隔离，重复命令不抢UI归属；文件逐层本地/reparse与自身打开句柄路径复核，不能确认暂存归属就保留并报Cleanup，路径核对与Move/Delete两步不冒称恶意换名原子保证。

r01新测试xUnit2031编译0执行无TRX；r02候选3F→r03同三测试源码/名称3P（UTF8外逸、目录替换清理误删合成未知文件、导出成功提示被撤销覆盖）；不是父生产红。独审再发现命令Busy抢UI owner，r09→r10同测试1F→1P。最终r11专项110P，r12完整Provider2157P=父2047全名保留+新110；r08完整Core1553P1F为旧OLE QueryWritten观测缺失，原源码/log/TRX保留，旧测试/OLE未改，Core DLL因cleanup安全码处理不同；r13按最终候选完整复测1554P=父1551+新3，不能据通过宣称其根因解决。r15/r16 App/Worker各0W/E，锁定恢复无新依赖。详见[实现与边界](validation/display-preferences-transfer.md)及[红绿和回归原件](test-evidence/2026-10-16-display-preferences-transfer.json)。

真实store保存/取消/冲突、四字段投影保持其它引用、NeverProtector=0和linked命令不捕获密码有实测；WPF对话框/可见性/键盘/Windows句柄/DPAPI/ACL、模型与完整SPEC仍NotRun。P1-03/A10/L01/L06仅补子项证据，全部P6整体状态不变。旧图片间歇根因未知，图片生产源码未改、复用Off；250/600ms和来源/密码/完整性/取消/清理门保持。Skia许可/403与Actions先前拒绝不查不绕，无main/强推/部署/费用/凭据/安全权限/新许可/真实模型，未知锁数据日志进程保全。


## 2026-10-16 · r08 OLE QueryWritten 间歇回归调查

已核9bf335c clean/实际远端/单worktree及唯一root写入，正常建dev/ole-query-written-investigation，high_water_review只读独审。保全历史r08/r13精确log/TRX/binding及父源码Git blob/SHA、父子实际DLL映射。原目标在上次已验构件不重编译有界20次20P，未自然复现。串行stdout writer的QueryEnd诊断调用应早于后续DropEnd交付，但可选stderr队列/独立Task不保证在交付时写出；原测试交付后立刻Cancel/Kill持有子进程，drain EOF不能恢复未写记录。

新增显式fixture-only首字节屏障（stdout不拦，既有epoch+2 disabled明确release），真实owned child/原server/client旧清理顺序实际1P1F、失败集合与r08相同；收据顺序2P，负对照仍验证已交付/进程退出/drain正常EOF且缺Written时helper必须失败。仅原测试清理前有界等待父端实际解析收据，4秒上限/5ms谓词轮询，不是单纯sleep、不延长生产门或推断Written，原全部断言保留；生产src原字节，无协议/取消/终止变化。r02/r03同名称同原断言但测试正文不同，生产及fixture DLL相同，仅Core.Tests改变；初始旧构件与9bf元数据重编译SHA不同，不能混称同二进制或重造历史原件。

最终专项52P、完整Core1556P=父1554全名多重集+2、Provider2157P原集合保持，均0失败/跳过；App/Worker各0W/E，修后固定20次重复结果见[调查报告](validation/ole-query-written-investigation.md)与[精确证据](test-evidence/2026-10-16-ole-query-written-investigation.json)。独审确认同步缺陷及实验边界；历史r08那次具体调度/可选异常仍Unknown，未以绿测关闭根因。E06/P5真实Windows/WPF/COM未验，旧图片间歇根因未知、复用Off及全部P6状态不变。Actions拒绝不查不绕，Skia许可/403隔离门和250/600ms/来源/密码/完整性/取消/清理保持；无main/强推/部署/新费用凭据安全权限许可或模型，未知锁数据日志进程保全。


## 2026-10-17 · 一次聚焦阻塞诊断

从已独审81f4e3f核实际HEAD/远端、clean/单worktree/唯一root写入后建dev/blocker-diagnosis。只读重核Skia四份nuspec/许可原件及旧403、Windows Accounting/Embedded和Linux失败原件：许可授权门与Proxy tunnel403独立；已有Git/NuGet取得事实不等于源码API可用，批准许可不能解403或提供Windows。当前无Skia引用/缓存，完整PNG/JPEG隔离解码仍未实现，不写未验证占位或新外围工具。

现有SDK激活后10.0.401可用；不restore/不下载、不重编译地运行3个既有平台/worker例3P，实际worker诊断UnsupportedPlatform；App --no-restore交叉构建一次0W/E。默认PATH及旧记载路径无pwsh、cmake/cl/Windows工具与WindowsDesktop runtime未见，补当前环境说明，不重复下载安装。Windows原失败仍是根Exit0后WaitJobEmpty Active2/Total33超时，成员身份/实际flags不足；NodeReuse禁用已在原代码，Embedded候选亦失败，不能猜测性再改或把Linux缺口当同因。

本次没有已证且能在当前权限内闭合原Windows/源码访问卡点的生产补丁，故源码/配置/锁/工作流/所有原件保全；交付每项证据、可行路径及最小用户步骤，见[诊断](validation/blocker-diagnosis-2026-10-17.md)与[固定证据](test-evidence/2026-10-17-blocker-diagnosis.json)。Skia许可未接受、403未重试/换镜像、Actions拒绝不查不调，未增费用凭据安全权限、未真实模型/main/强推/部署。旧图片未知、生产复用Off、全部P6/Windows未验及250/600ms/来源/密码/完整性/取消/清理门不变。


## 2026-10-17 · SPEC14.4/P1-06会话诊断与本地报告

实际HEAD/远端dev/blocker-diagnosis均9765f0bc7f2ef6ee099cb8d06d2fb80f405a933e，与先前同HEAD；单worktree/唯一观测root写入，从其正常建dev/local-diagnostic-report。8b902祖先修复保留、不重复交付；250ms父子边界候选只查源码未实证/修改，按父补充优先诊断。源码核实Shell.Report仅异常类型、隐私页缺报告入口，已有Provider/Markdown等功能未重复。

新增最多256条会话内白名单元数据、异常固定分类、设置底部真实预览窗口与显式新本地文件导出。默认不落盘/不上传/不遥测，退出/用户清除释放；不接受正文/秘密/路径/查询串等自由文本，未知重试/指标Unknown，未运行能力NotRun。报告含OS数字版本/工作区DPI/组件版本；预览快照与publication门、取消和文件归属保全。首次App语法失败保留修正；自身取消2F→绿，caller初提前完成假设1P未证实，准确gate反例1F→绿，全部原件保留，未把新功能缺类型当父红。最终专项31P，完整Core1587P（父1556全部结果行+31）、Provider2157P原集合，App/Worker各0W/E；代码只读独审通过，真实WPF仍未验。

具体原范围剩余源码/依赖见[报告](validation/local-diagnostic-report.md)、[原始证据](test-evidence/2026-10-17-local-diagnostic-report.json)：P6普通图片解码/缩放和生产视觉链、P7完整安装升级卸载、Windows真实取词/COM/DPI/DPAPI与完整质量/浸泡仍未完，非仅重述标签。P1-06仅改InProgress/验证NotRun，其他整体状态保持；图片复用Off和旧间歇根因未知。无环境重复诊断/新依赖/Skia许可或403绕行/Actions访问调度/真实模型/main/强推/部署/费用/凭据安全权限变更，未知数据锁日志进程保全。


## 2026-10-17 · UIA父客户端250ms准入边界

父有限接受4f47990后按新优先核实实际HEAD/远端、clean、单worktree及唯一观测root写入，正常建dev/selection-soft-budget-boundary。250候选先前仅源码未实证，本次复用existingCaptureControlFixture和生产Client→SelectionCaptureCoordinator，在249.999/250/250.001ms各测Success及EmptyOrUnsupported+Copy：父4P2F，恰250文本接纳/复制替代端口事务输入各1并接纳来源，真实反例确认后才修改。

生产只Client两处>250→>=250，文本SoftTimeout/copy沿用ProtocolFailure，600ms及来源/密码/完整性/身份/取消/清理均不变。r02同六测试与fixture字节6P；红绿测试/fixture/Core等DLL完全相同，仅NativeHost DLL变。独立只读代码审查通过；最终Core1593P（父1587全结果+6）、Provider2157P原集合、App/Worker各0W/E。实际stdout帧/owned进程、coordinator与替代copy issue计数明确，不称真实UIA/Windows剪贴板/Ctrl+C或250/600ms实机验收。

见[边界报告](validation/selection-soft-budget-boundary.md)、[红绿与回归原件](test-evidence/2026-10-17-selection-soft-budget-boundary.json)。旧测试/证据/诊断/图片/usage/版本/项目/锁/workflow/prompt保全，所有整体状态不抬；P1-06仍InProgress。P7文件事务及首次启用模型说明只读线索暂不实施。Skia许可/源码403、Actions拒绝不查不绕，图片复用Off/旧间歇根因未知与Windows/WPF/模型未验保持，无新增工具/外围探针/包/许可/权限/凭据/费用/main/强推/部署/环境诊断，未知数据锁日志进程保留。


## 2026-10-17 · P7-06：设置中的真实本地集成状态

父独审接受 b35a8d7 后核实际 HEAD/默认远端/main、clean、单 worktree 和唯一 root 写入，正常建 dev/integration-status-page。设置“图片与集成”新增手动只读状态/刷新/取消，未知配置只读时仍可检测；编辑/保存不放开。七行明确区分未检测、未安装、不兼容、明确本地策略拒绝、平台不支持与无法核实。当前进程身份实读，Chrome/Edge 注册独立；不读个人资料、不因清单/程序集存在宣称扩展、Explorer/OCR 或图像链路已安装/可用。只核固定安装根的两个≤64KiB JSON 与 23 个角色摘要，单文件256MiB/合计1GiB/分块取消；不加载或执行文件。不同策略来源不拼接优先级或例外，不支持的类型/列表名称保持未知，权限异常不冒充策略禁止。单读任务一直保留到读/CancelAsync callback/bridge结算，取消和关闭拒绝迟到；真实UI提示仍需Windows实测。

实际首版57P、补充61P、前候选68P；封存自检发现托管GetValue先分配再限长，改固定64KiB/3字符原始只读缓冲，11个原始值解码例补入，最终79P；新功能无父API，不冒称父红绿。完整Core1672P=父1593全部结果多重集+新79，Provider2157P原集合保持，App/Worker最终各0W/E。独立代码审查无P1/P2，建议两点收紧并补测/复审；仅源码与交叉构建不当作WPF/Windows registry/ACL证据。详见[实现和范围](validation/integration-status-page.md)及[精确原件](test-evidence/2026-10-17-integration-status-page.json)。P7-06仅改InProgress、validation仍NotRun，禁止侧载/截图/扩展安装和浏览器有效策略、可靠扩展缺失检测、Explorer/OCR与安装后的完整链路仍未验，其他状态保持。

已有SDK激活前后显式禁ASP.NET证书生成，全部--no-restore、无新包/许可安装。旧图片间歇根因未知、生产复用Off，250/600ms/来源/密码/身份/完整性/取消/清理门保持；Skia4.153.1许可与403、Actions先前拒绝不查不绕。无main/强推/部署/费用/凭据/安全权限或真实模型，未知锁/数据/日志/进程保全。


## 2026-10-17 · SPEC14.1：首次启用的模型说明

父有限准入a876后核实际本地/远端、clean/单worktree/唯一root写入，正常建dev/enable-model-disclosure。先按父建议核P7脚本：仅开发身份pack/显式register及Installer=false的Alpha暂存，无安装升级事务消费入口；未执行安装、注册、签名或加孤立工具。按已有授权修此前定位的首次启用缺当前模型：同保存profile冻结Model，实际主按钮/托盘/快捷键共用确认框使用该意图的完整origin/模型/模式/内容发送说明；旧五参构造兼容，生产显式六参，ConfirmEnable/TryEnableShell原门及origin授权范围不改。

r01新增夹具初始化顺序错4F保全、不当产品红；修夹具r02父生产原字节实际4F缺Model属性；r03旧Hotkey五参调用CS7036、0执行无TRX保全，旧测试不改；兼容后r04同测试源/四名4P，测试DLL不同不混称同二进制。最终16专项P，Core1688P=父1672结果多重集+16，Provider2157原集合，App/Worker各0W/E。独立源码审查无P1/P2，冻结/陈旧确认/拒绝/隐藏锁屏退出/同origin授权保留/换origin或HTTP/secretref不显示不解密/重启Off及256模型字符串均有便携实测；实际WPF可见/键盘/排版未验。详见[范围与红绿](validation/enable-model-disclosure.md)及[精确原件](test-evidence/2026-10-17-enable-model-disclosure.json)。P1-05/L02只追加证据/说明，所有追踪状态保持，P7未抬；前a876独审94后加回执1共95原件全部原字节保全。

仅已有SDK/--no-restore，激活前后禁ASP.NET证书生成，无新包/许可/环境安装。旧图片间歇根因Unknown、生产复用Off及250/600ms/来源/密码/身份/完整性/取消/清理门保持。Windows/真实模型/完整验收NotRun，Skia/403/Actions拒绝不查不绕，无main/强推/部署/费用/凭据/安全权限，未知锁/数据/日志/进程保全。


## 2026-10-17 · SPEC3.6：显示保存不中断已启动文字生成

父有限准入528b2ba后核HEAD/默认远端/main祖先、clean/单worktree及唯一root写入，正常建dev/display-save-stream-continuation。实际首段SSE→字号/宽度/位置保存→尾段，父生产6F→同源六名6P。仅TextTranslationCoordinator生产文件改变：活动请求缓存真实保存基线，只归一化Overlay/WriteId，冻结请求快照不改；旧未提交/确认、来源/授权/profile/秘密/HTTP/identity门保留。独审CAS同S败者误取消2F→2P，终结按identity重试清除；OCR本次风格与保存默认不同4F→4P，保存投影前基线、不扩白名单，重生成旧冻结语义保留。

前候选41P/Provider2198P和51P/Provider2208P均保留；r13夹具49P2F仅把前置来源标记改为真实Local.Clear()，生产/断言不改。初次130原件/137路径终审前自审发现非profile unsafe立即Stop跳过原Abandon回退；独审确认后暂停提交。r23 OAI实际回归、Anthropic非法usage夹具不计双协议红；修正合法事件位置后r24两协议2F、r25父生产原字节同测试2P、r26修后2P。新增有界同owner永久否决位，只拒HTTP/交付并让下一destination保留原中性Abandon回A文本/usage/身份、保留B stopped；原profile Stop不改，恢复字段/原Version/已核Version和CAS败者均不能复活。

最终r27专项55P、r28完整Provider2212P=父2157全名多重集+55；r29/r30 App/Worker0W/E。r06完整Core1687P1F旧Dispose_during_active_request结算未确认，源码/log/TRX/DLL保留；r14完整1688P且Core.Tests/Core/NativeHost DLL相同、诊断DLL后读亦同，根因Unknown、不冒称解决。r14早于后续Provider/Provider.Tests修正，其源记录保留前候选；Core14项目闭包不引用它们，整个依赖字节不变，仍适用，无需非依赖变动重复Core；最终Provider/App已重新全量/构建。

见[修复与边界](validation/display-save-stream-continuation.md)及[全部红绿/回归原件](test-evidence/2026-10-17-display-save-stream-continuation.json)。真实store/Shell位置/Kestrel/交付链、同captured参数原语及显式settings-provider模拟分别注明，不称真实线程同点、WPF或Windows OCR。P1-03/A10仅追加证据/说明，全部状态不变。WPF设置可见性/键盘/焦点与前台来源、Windows/模型/完整验收未验，旧图片间歇Unknown/复用Off及250/600ms/来源/密码/身份/完整性/取消/清理门保持。仅已有SDK/--no-restore，激活前后禁ASP.NET证书生成；无新包许可环境、main/强推/部署/费用凭据安全权限或真实模型，Skia许可/403/Actions拒绝不查不绕，未知锁数据日志进程保全。


## 2026-10-17 · 诊断工具 Dispose 有界真实结算

从父有限准入04d6831正常建dev/diagnostic-dispose-settlement，先核实际HEAD/默认远端/main祖先、clean/单worktree/唯一root写入；high_water_review只读独审。依父明确诊断候选，用实际owned托管child将真实退出与stderr/stdout/exit-wait/startup结算分别持门，原一次TrySettle的Dispose在gate释放前已fault，同测试源四名实际4F→4P。仅诊断工具Host加入running完成后的2秒清理预算，join原任务/晚到startup drain，再核退出/原finalization；semaphore同截止，超时/故障不伪成功、不丢原任务/owner，可在真实结算后同host重试。生产Core/native/Job与250600门未改，外层Registry原留守重试未改，不能声称全runner有界。

最终15例涵盖同次等待/超时所有权/独立退出确认/信号量/并发重复/fault清零而不放行/无child。相关66P，完整Core1703P=父1688全名多重集+15，Provider2212P与父全名多重集完全相同，均0失败/跳过；App/Worker各0W/E。r04误引filter导致shell127、无权威TRX且首分支执行不明，原件保全不计结果；r03中间15P不当最终测试字节。独审无P1/P2；fault夹具任意早期失败cleanup健壮性未全面证明，正常fault路径实测，不当Host成功。见[报告](validation/diagnostic-dispose-settlement.md)及[原件索引](test-evidence/2026-10-17-diagnostic-dispose-settlement.json)。traceability/旧测试/全部旧证据原字节保持，状态不抬。

历史display-save r06真实1687P1F及同DLL r14完整1688P继续保留；本次确定性反例不证明历史当次未完成项。WindowsJob原未清空独立Unknown，旧图片间歇根因Unknown/生产复用Off；真实Windows/WPF键盘/COM/native/模型/250600/整体验收未执行。仅已有SDK/--no-restore、激活前后禁证书生成，无新包/许可/环境/main/强推/部署/费用凭据安全权限/真实模型；Skia许可/403/Actions先前拒绝不查不绕，未知锁数据日志进程保全。


## 2026-10-17 · P6 最终截图的正常 Shell 显式图译接线

从已推送78e7db0父正常建dev/production-screenshot-translation，先核默认远端/HEAD/祖先/单worktree；唯一root写入，既有high_water_review只读。按父明确P6裁剪范围，已保留的最终截图可在正常Shell逐次确认端点/模型/两阶段费用说明后，用显式VisionProfileId走普通两协议与SecretStore租约；默认不自动发送。仅现有受限截图PNG，独立正常协调器，离线边界原字节不改。每阶段独立usage/lease、同总预算与逐次来源身份/秘密/授权/取消晚到门；显示原图与正文及当前版本模式。生产图片/分类复用和旧图重新生成保持Off。

实测失效destination清理1F→1P、Clear取消回调重入C1F→1P、两协议阶段预算2F→2P；独审指出图A完成后已批准OCR文字B失败回A释放原图，加强精确B部分正文/原因/自动A/切A-B绑定，同源2F→2P，仅精确sourceversion+sourceid命中保留，旧文字分支保持。最终专项60P/完整Provider2272P=父2212全名多重集+60，完整Core1703P与父全名多重集相同；App/Worker各0W/E，21source绑定一致。独审无剩余P1/P2，封存另审。r01/r03/r10编译失败、r02仅取消精确owned测试且无TRX/当时等待根因未捕获、r08夹具预期修正及r14不足断言均保原件，不混算产品红绿。

见[接线/验证与边界](validation/production-screenshot-translation.md)及[原件索引](test-evidence/2026-10-17-production-screenshot-translation.json)。本地回环和公开合成OCR/像素/秘密、真实store及协调器/链接Shell端口分别注明，不称真实WPF/WindowsOCR/DPAPI/模型。所有旧测试/追踪状态/SPEC/原书/旧证据原字节保留，不称完整P6。通用文件/Drop/新解码/Skia及图片复用未启用，Windows可见键盘/真实模型质量/物理250600/完整验收NotRun；历史图片间歇/Core结算/WindowsJob根因各仍Unknown。仅已有SDK/--no-restore激活前后禁证书，无main/强推/部署/费用/真实凭据安全权限新包许可环境/模型，Skia待批/403/Actions拒绝不查不绕，未知锁数据日志进程保全。


## 2026-10-17 · 截图秘密 await 内预算到期补测

从父c775db正常建dev/screenshot-lease-await-timeout，仅测试helper观察和两协议×两阶段4例。真实await释放后内部Advance20(dispatch:false)才返回实际lease；同owned数组非零→全0、Disposed、目标阶段0新增HTTP和唯一RequestTimeout终态、原图保留，专项首次4P，生产无需改、不造红绿。r02完整2275P1F是旧Anthropic empty idle500ms Job未完成断言，新4均P；r03同DLL旧族6P，r04同DLL完整2276P，父2272+4名字精确，26源码×4=104绑定一致。首轮根因Unknown，失败原件保留、旧断言不改。生产/其他测试/SPEC/trace/旧证据含499原件全原字节，Core1703/AppWorker0W/E沿用父闭包证据，不冒称本片重跑。

见[报告](validation/screenshot-lease-await-timeout.md)与[四次执行原件](test-evidence/2026-10-17-screenshot-lease-await-timeout.json)。预算片单独推送后立即做已授权恢复菜单重新截图，证据另片；生产复用Off，Windows可见交互/DPAPIACL/物理250600/真实模型/完整P6仍NotRun，历史图片间歇/Core/Job根因仍Unknown。无新包许可环境/Skia/403/Actions拒绝绕过、main/强推/部署/费用/真实凭据权限；未知锁数据日志进程保全。


## 2026-10-17 · 图片失败/停止恢复菜单“重新截图”

父预算片1184969已独立推送130原件，从其正常建dev/screenshot-recapture-recovery。实际Shell更多/状态恢复菜单新增重新截图，冻结恢复identity再核来源context和可见/锁/暂停/原task/cleanup/hostowner；显式菜单Intent不伪造快捷键，真实hotkey守卫保留，共享原任务/取消/清理。native边界仍原隐藏/Exclude/显示器/框选/host结算，成功crop过nativecleanup后才交Shell；新有效裁剪才替换旧来源，取消保旧会话。先本地预览再明确模式/端点/模型/两阶段发送确认，Off不自启/发送，不复制旧图换ID，同图复用Off。

实际双协议SSE A部分正文停止→菜单新不同像素B→确认前0新HTTP→拒绝0→明确确认完整两阶段，PNG确不同；22例涵盖取消、六种撤销/迟到擦除及ClearExit join、旧菜单、Off启用确认、真实gate hotkey、known owner未清不替换/原retry。yield时实际选版本改显示revision而epoch同，旧候选仍native1次实测1F，完整admission+intent一行guard同源1P后native0；A及原任务保留。r01新hide期望错误18P1F→加强r02 21P不算产品红绿；r03构建中间，原件全保。最终r06专项22P/r07完整Provider2298P=父2276+22全名多重集、r08完整Core1703P同父名字、r09/r10 AppWorker0W/E，32source×5=160一致，独审无P1/P2；HeldContext任意早期assert失败finally健壮性未全面证明，正常绿色原任务已结算。

见[报告](validation/screenshot-recapture-recovery.md)与[独立证据索引](test-evidence/2026-10-17-screenshot-recapture-recovery.json)。合成native/菜单表面/Cancel token/known host端口准确披露，不冒称Windows真截图/可见确认焦点/锁屏DPAPIACL/物理250600/真实模型或完整P6。父预算旧idle首失败和历史图片/Core/Job根因各Unknown；旧499+130原件/全部旧断言/SPEC/trace/CoreProvidersImaging/Offline/锁/workflow原字节保持，状态不抬。仅已有SDK --no-restore激活前后禁证书，无main/强推/部署/费用/真实凭据安全权限新包许可/Skia/403/Actions拒绝绕过，未知锁数据日志进程保全。


## 2026-10-17 · HeldContext 测试清理与旧 idle 只读调查

父2219已独立验收，从其正常建dev/held-context-test-cleanup，仅补已授权测试夹具finally：恢复SynchronizationContext，始终drain并在6秒内await同原capture任务。原guard十段断言保持，两受控实际前置assert失败证明原异常保留、同task成功、Capture释放、native1/0及A/原图/HTTP2不变。r01 xUnit2020编译拒绝无TRX、不算红测；最终专项3P、一次完整Provider2300P=父2298全名多重集+新2，64源码绑定/目标DLL一致。Core1703/AppWorker0W/E仅沿用未变父闭包证据，不冒称重跑。独立源码审查无P1/P2，封存另审；无生产改动。见[夹具报告](validation/held-context-test-cleanup.md)与[证据](test-evidence/2026-10-17-held-context-test-cleanup.json)。父1003原件、其余4797父路径原字节保持。

[旧idle只读调查](validation/idle-timeout-readonly-investigation.md)分开记录：相同DLL2275P1F→族6P→2276P不等于修复；PUBLIC前缺客户端正文同步，可达message_start定时器锚点时序但历史轨迹不足、根因Unknown。disposed正常DueMax；旧analysis文字NativeHost误列追加勘误为ImageWorker，旧raw不改。未加idle诊断重跑/改生产，后续最小PUBLIC入队同步候选待父另定；未发送截图本次风格/P7安装事务仅列独立功能建议、不实施。历史图片/Core/WindowsJob各Unknown，生产复用Off及Windows可见键盘/物理250600/模型/完整P6未验。无main强推/部署费用/凭据权限/新包许可Skia/403或Actions拒绝重查绕过，未知锁数据日志进程保全。


## 2026-10-17 · idle 测试 PUBLIC 入队同步

父e14789已准入，正常dev/idle-public-anchor仅改PhaseDeadlineTests。精确PUBLIC TextDelta实际入队后异步signal，六旧idle先等再WaitDeadline2/首次advance；保留全部旧断言、2秒idle、真实500ms完成门及cleanup，生产/PhaseClock未改。受控Anthropic先message_start活跃idle2且无PUBLIC，fake1消费PUBLIC截止3，fake2真实500ms仍pending，fake3原门READ_IDLE_TIMEOUT，证明锚点/可达安排，不称历史根因。empty仍仅serverflush无消费收据。最终整类28P/完整Provider2301P=父2300全名多重集+新1，66源绑定；Core1703/AppWorker0W/E仅继承未改闭包，不重跑。独立源码审查无P1/P2，封存另审，见[报告](validation/idle-public-anchor.md)/[原件](test-evidence/2026-10-17-idle-public-anchor.json)。旧报告原件不动、历史Unknown、复用Off、Windows/模型/物理250600/完整P6未验，阶段状态不抬。无main强推部署费用/凭据权限新包许可Skia/403或Actions拒绝重查绕过，未知锁数据日志进程保全。


## 2026-10-17 · 未发送截图本次风格

第一片d7b5458已独立推送84原件；从其正常建dev/screenshot-session-style落实SPEC9.9/I03当前未消费截图四风格。单override精确绑定SourceVersion/SourceId，菜单旧源/消费后不可改；offer确认冻结显示，改风格撤销旧offer，Job/原有界版本记录保留枚举；只覆盖Planner该次Translation.Preset，保存Settings/Profile/secret-binding不变。新图/清除/来源释放/接纳消费复位，Automatic结构图仍Explain，同图复用Off。

最终新24P/无filter Provider2325P=父2301全名多重集+24，Core1703P名字与父闭包相同，实际AppWorker各0W/E；五轮34源=170绑定匹配。双协议四风格真实本地HTTP核system/template/确认及本地生成版本label、原保存对象与binding传两次合成lease；六种确认撤销每协议0HTTP，旧菜单/错身份/新源清除复位/旧图禁发。r01/r02编译失败无TRX，r03中间测试原件不混最终/红绿。独立源码审查无P1/P2、封存另审，见[报告](validation/screenshot-session-style.md)/[原件](test-evidence/2026-10-17-screenshot-session-style.json)。生产菜单方法由端口实际执行，顶部TranslationFlow/工具条分支只交叉构建，真实WPF可见键盘/焦点/截图/DPAPIACL/物理250600/模型/I03质量/完整P6未验、状态不抬。旧证据/生产CoreProvidersImaging/Offline/SPEC/trace/锁workflow原字节保持，历史idle/图片/Core/Job各Unknown。无main强推部署费用/新许可Skia/403或Actions拒绝查询绕过/真实凭据权限环境，未知锁数据日志进程保全。


## 2026-10-17 · P7 安装升级独立文件事务核心

父1341b已准入，先核现模块/原SPEC再按父补充授权正常建dev/install-transaction-core。原开发身份/managed staging/宿主只读校验不等于安装器；新增Core文件事务，复用WriteFlushed/RejectLink，caller固定catalog SHA与精确有界文件树校验，完整版本目录后原子current记录、ReadActive消费，失败恢复核验旧版/坏active恢复/不确定显式保留。用户数据根隔离，不读迁移秘密；无OS安装/注册/签名/启动改变/卸载永久删除，真实role停止适配器未实现。

候选清单错误类型绕恢复实测7F，case目录额外空别名实测1F，纯数据异常归一及seenDirs后8P；r01新测试误期望异常类型27P1F不算红绿。最终专项40P/完整Core1743P=父1703全名多重集+40，Provider2325P同父名单，AppWorker实际各0W/E。三个自有子进程真实Exit73 checkpoint与并发读者/排他锁、提交前后取消、故障回退、未知文件保留、真实SecretFileStore+合成AES密文及解密保持均核。源码采集漏tools驱动的旧35/37条不倒填；修正后仅3有界补执行，41源及相同driver/Core DLL绑定，六最终执行234源合计，作用域/原件勘误明确。独立源码复审无P1/P2，封存另审；见[报告与P7缺口](validation/install-transaction-core.md)/[原件](test-evidence/2026-10-17-install-transaction-core.json)。trace整体状态不抬，Windows安装/真实boot/签名/注册rollback/登录启动/卸载/断电/ACL-DPAPI及K组仍未验；旧版本累计存储未GC、同账户换名/早失败childIO边界如实保留。截图其余SPEC差异仅记录，不新增测试扩功能；历史Unknown/复用Off/许可403/Actions拒绝/未知锁数据日志进程保护不变，无main强推部署费用凭据权限新协议/环境。

## 2026-10-17 · P7 升级协调与启动健康回退

父e684fc3文件事务核心获有限准入，从其正常建dev/upgrade-health-rollback。复用原Verify/Publish/exact-pointer回滚及OwnedProcessSession，新增产品特定协调器：固定七角色只Own实际Start返回值，优雅退出后独立确认startup/exit/原IO，持writer+admission通过新健康；失败先结算自己候选再恢复确切旧pointer并实际重启旧版。持久execution-owner在actualStart之前创建并由原控制方跨操作持lease；普通Apply/Recover/RoleAdmission拒active/stale，未知journal保留，不能凭PID或锁释放当作所有角色已停。拒退/late startup/late IO/取消callback不确定均pending阻塞，只有原实例持精确记录与owner才可retry。Dispose会终结自有健康child，生产Windows健康后handoff尚未实现，不声称安装器升级可用了。

候选late health实际1F→同法1P；r02最终可见错误由Dispose结算抛出、Retry首错未独立保留，原任务等待缺口由源码/同方法对照支持；独审跨实例Start/Upgrade越过活A和自然exit漏stderr结算实际3F→同三法3P；阻塞同步Cancel实际1F→同法1P，异步Cancel原task及cleanup随role持有，重试复用原exit/IO。r01新增test var语法构建失败无TRX不算红测。最终相关68P/完整Core1770P=父1743全名多重集+新27，Provider2325P与父名单相同，AppWorker各实际0W/E，五轮47源共235绑定。合成generation内真实driver/依赖启动，目录+SHA+版本+nonce健康、真实自有App/Broker退出/拒退、OS启动失败/初装/旧重启失败、真实独立admission child、Exit79启动前owner持久checkpoint、late实际Start owner及原IO结算均核；另真实同摘要symlink包文件/目录/安装祖先/用户数据祖先保目标原字节、连续Recover两次幂等。旧40断言完整，新27不冒称Windows或历史根因。

独立只读源码复审无P1/P2，封存另审，见[报告及原P7下一对接链](validation/upgrade-health-rollback.md)/[原件](test-evidence/2026-10-17-upgrade-health-rollback.json)。完整payload/派生绝对路径元数据/正常Shell浏览器收件入口、Windows退出控制/实际可见健康及所有权交接/身份NativeHost注册rollback/Inno安装器/登录启动/卸载与K组仍未完成或未验；当前协调器只证明自己Start持有角色，非系统所有角色。分阶段budget不等于整体硬时限，原子替换不等于断电或OS注册原子事务，同账户恶意换名、累计旧版本存储、真实DPAPI/ACL/截图/物理250600/模型边界保留。无main强推部署费用真实安装签名注册/凭据权限/新包许可；Skia四包待批、403和Actions拒绝不查不绕，未知锁数据日志进程保全，图片/idle/Core/Job各历史Unknown及复用Off不变。

## 2026-10-17 · P7 选定版本运行期明确交接

父0137420有界协调核心准入后，正常dev/runtime-ownership-handoff只推进已授权合成进程交接。新增显式UpgradeAndHandoffAsync：精确owner/current/upgrade及receiver/launch/version绑定Prepared→接收者取得同OSlease→唯一Committed。提交前须实际自有child退出与原startup/IO/task结算才reclaim回退；提交后ACK/取消/控制结算失败明确Unconfirmed，不走Kill/旧回退，旧controller三入口拒绝。Transferred独立于Exited/Finalized/disposed；原Settle实退断言保留，交接单独cancel并join原reader/取消工作。receiver Abort也持lease到OS退出，避免Prepared读与提交间放lease。owner/pending/ledger自退后仍留，fresh拒入；自动重启/stale接管/完成记录清理未实现，不凭PID/EOF推断退出。

真实自有controller Exit后仍PING选定generation接收者，private pipe STOP_SELF明确自清理、pipeEOF及静态持根lease的OS释放；不冒称根测试持grandchild Process WaitForExit。合成fixture-only40秒界保证有清理接收者。交接16例含拒绝/取消/假错迟ACK/缺锁/丢ACK/阻塞原控制/竞争窗口/带活角色pre/postcommit崩溃/未知ledger/同实例Retry/postcommit三入口拒绝。缺锁假ACK实测红修为仅存在路径共享冲突；其用例仅追加诊断参数，非全方法同字节。legacy→新显式入口是行为对照，不充作同参数缺陷红绿。r03分析器/r04AutoFlush/r05cleanup遮蔽/r07r08fixture同步Console.In阻塞均原件保留，不包装历史根因。

完整首轮r10为1785P1F旧LateActualStartOwner Retry Busy，具体原因仍Unknown。独立受控OS已退而原Stop task被扣，新例实际1F→同方法1P；StopGracefully实退后有界join原缓存Stop再走旧IO/协议/退出门，不降低断言。最终相关85P=父68+交接16+stopJoin1，完整Core1787P=父1770名单+新17，Provider2325P同父名单；AppWorker实际构建和五轮50源250绑定详见[报告](validation/runtime-ownership-handoff.md)/[原件](test-evidence/2026-10-17-runtime-ownership-handoff.json)。独立源码终审无P1/P2，封存另审，父旧方法/原件保持。

仅可移植合成接线；Windows正常入口/真实关闭可见健康/Job/交接、身份NativeHost注册及rollback/派生绝对路径/完整payload/Inno/自启/卸载/K组未执行或未完成。真实WPF键盘/截图/DPAPIACL/物理250600/模型/完整P6 NotRun，分阶段budget非整体硬限，断电/恶意同账户换名/累计磁盘边界不抬。历史图片/idle/Core/Job各Unknown、生产图片分类复用Off及来源/密码/完整性/取消/清理门不变。无main强推部署费用真实模型凭据权限/新包许可；Skia四包待批，source403和拒绝Actions不重查绕过，未知锁数据日志进程保全。


## 2026-10-17 · App 受控启动健康与既有关闭任务接线

父e36b5395准入后正常dev/app-installation-control，仅接原App受控启动/可验证健康/关闭任务；按父收窄指令未造observer或terminal owner自动释放。固定generation/App角色/userdata/launch及精确owner/current/pending证明、两把已持OSlease与单次atomic claim在任何Shell前核；正常已有实例仍走原唤回，新Shell普通安装入口核选定generation并持门。静态持根committed runtime；退出只放lease不清持久owner，fresh继续拒入。ImageWorker/Imaging/Providers生产源码原字节、Worker原一次性二进制协议及App Process责任保持；本轮Worker/Core/Imaging构建DLL不声称与父hash相同或仅元数据差异。

健康绑定真实Loaded/startupHealthy/Visible/HWND/geometry/Settings，STOP经Dispatcher仅请求原Close；旧内容清理失败保留窗口/所有权/原control循环。成功清理后绑定CLOSING令精确client关闭该child stdin，再join同原reader/取消callback/task；EOF不当实退。commit单调，即COMMITTED输出IOException/未知ledger也不回到自动关窗。实际Windows可见健康、Dispatcher/STA/正常唤回/Job仅交叉构建未验。

最终安装107P/完整Core1809P=父1787全名多重集+新22/完整Provider2325P同父全名；AppWorker各实际0W/E，五轮57源共285绑定及实际fixture/Core DLL核定。22例中20例真实选定合成child、2文件准入，WPF/内容cleanup明确端口。五个候选同方法真实红绿：stdin结算、COMMITTED写失败、二次STOP、case容器、原失败Close task仍pending时保留重试。最后一例证明fixture IsCompleted丢重试，容量1单worker逐个await原task；非生产新守护。r17二次STOP间歇当次时序Unknown；r20完整1808P1F旧OLE Cleanup TryFinalize false，旧源码与父同、r20结束后/r21前25DLL补观察（raw误写运行中已显式勘误），r21同DLL旧类27P/r22同DLL完整1809P，不把重跑绿当根因修复；失败/中止/编译原件全保。

独立源码终审无剩余P1/P2，封存另审；见[接线报告](validation/app-installation-control.md)/[原件索引](test-evidence/2026-10-17-app-installation-control.json)。父7344路径除明确8处外7336原字节保持、旧测试不删不弱、SPEC/trace/锁/workflow/旧原件不改。无observer/自动重启/stale接管/owner自清；真实Windows健康/键盘/截图/DPAPIACL/物理250600/模型/身份注册rollback/完整payload/Inno/自启/卸载/K组及完整P6/P7未验。父r10、历史图片/idle/Core/Job原因各Unknown，生产图片分类复用Off及来源/密码/完整性/取消/清理/250600门不变。仅已有SDK --no-restore、激活前后禁证书；无main强推部署费用真实模型凭据权限/新依赖许可，Skia4.153.1四包待批，source403及拒绝Actions不查询不绕过；未知锁数据日志进程保全。


## 2026-10-17 · 真实 Shell 关闭任务与 pending STOP 修正

父f8b独审发现必须先修实际接线，暂停后续安装功能定位，正常dev/app-close-pending-retry。明确勘误：f8b r18→r19容量1只在fixture，不能用其绿证明真实App cleanup等待中第二STOP会在首次失败后重试；父原报告/1549原件完整保留，此追加不回写历史。

真实ClosingShell移为生产partial，Shell持同原attempt Task+最多1pending显式请求；先发布owner再运行QueuePositionSave/exiting/button与同步lifecycle.Hide，回调重入不并行开cleanup，失败无pending不自动重试，成功丢pending。启动新attempt的admitted直接异常仍在原Task；位置/Hide取消错误收进同次SettleExitContent并跑完原内容清理后汇总，失败窗口/控制通道/所有权保留。Dispatcher Unwrap实际async attempt，STOP只await dispatch避免control自等待；opening/settings原exitRequested、allowClose递归保持。fixture删除独有queue，直接链接同生产Flow+Requests；UI/Dispatcher仍是明确端口，不充WPF实测。

父原ClosingShell方法字节真实链接到自有child，r01实际1F；r03补cleanup-entered/STOP2-received屏障仍实际1F，释放后失败/CLOSED序列读取超时（该行两次Row未独立分轨，不声称已独立证明失败回执先到）；最终同测试方法字节绿。候选仅exiting留下GateOn，直接生产ClosingShell+真实RuntimeGate/TryBeginSend且扣住Dispatcher，r07实际2F→r08同字节2P；同步Off/ct取消/0send callback，取消错误不吞并继续原cleanup后保留窗口。另5例核32请求合一、原Task完成才下一次、无无请求重试、成功丢pending、admitted同步重入/原fault保持。r02 CS0414编译失败无TRX、r04/r05/r06中间结果均原件保留。

r09相关108P/r10完整Core1810P=父1809全名多重集+1/r11完整Provider2332P=父2325全名多重集+7，旧Core测试文件前缀/全部原方法逐字节保留。App/Worker各实际0W/E，五轮63源315绑定、目标DLL和真实child执行另封存，独立源码终审无剩余P1/P2，封存另审。见[修复与证据勘误](validation/app-close-pending-retry.md)/[原件索引](test-evidence/2026-10-17-app-close-pending-retry.json)。父8901路径除9处外8892原字节保留，含全部7991旧证据。

环境断连通知后仅一次有界只读检查于14:27:33Z实际完成：r01-r06原命令全有结算记录，无本写入者挂起测试，HEAD仍父f8b，修复未提交未推；确认本地工具可用后继续同写入者，不造重复实例/不删锁进程。Windows可见/WPF/实际Dispatcher/STA/设置窗口/Job/OCR/键盘/DPAPIACL/物理250600/真实模型仍NotRun；普通installed与committed自退持久拒入、无observer/stale接管不变。后续入口/完整payload/发布注册rollback/Inno/自启卸载/K组仍暂停未验，不称完整P7。历史图片/idle/Core/Job及父r17/r20各Unknown，生产图片分类复用Off/所有发送来源取消清理门保持。无main强推部署费用凭据权限/新依赖许可；Skia四包待批，403及拒绝Actions不查不绕，未知锁数据日志进程保全。


## 2026-10-17 · 既有 App 内的一次正常安装会话

父369796独核关闭真实关闭重试P2后，正常dev/installed-session-launch只接无参数installed App启动→用户退出→再次启动。Program在UI mutex/WPF前分流到同源InstallationSessionEntry并静态持根；只有精确controlled child创建原Shell。复用ReadActive/Client/协调器，必须全新coordinator，保留本次实际Start返回Process、原WaitExit(None)、原IO与已完成fault Task；精确单CLOSING＋实际Exit0＋原双流自然完成无额外行＋全部原任务成功＋原记录复核后才Finish自己owner。无运行总限，阶段budget不冒充整体硬限；取消/迟到/异常/清理失败锁存拒入，后来Dispose收完也不清marker。活会话并发Dispose立即busy；重复父只纯scope发原Show，不拿UI mutex、不当owner证明。未用COMMIT/释放句柄分支，无service/scan/restart/stale收养。

独审两P2已闭：旧StartActiveRole后混入session曾误Dispose旧execution，r04实际1F→同方法最终绿，现latch前拒绝非新协调器，旧角色仍可实退/IO收尾；拒入父曾短暂拿UI mutex，现纯ScopeFor，实际portable --instance child可获owner，Windows广播时序仍NotRun。新增24例含真实合成parent＋selected child两次正常会话/重复、首次内容清理失败后显式重关、收CLOSING仍活、无CLOSING Exit0/非零、取消/迟到Start/原health与adapterTask未结算/faultIO、错误owner/current/pending、未知锁记录、父观察方丢失、generation与strict输出。丢父仅证父实退及子私有CLOSED，childExit=Unobserved并保留公开fixture目录；HoldIo只扣原adapterTask，不称真实reader仍阻塞。

r01 6P12F为Peer连接前AutoFlush的harness错误，非产品红；r02/r03中间18P/127P未闭独审两项，r05名称final但129P后新增三例，均完整保留不作最终证据。r06最终相关132P/r07完整Core1834P=父1810全名多重集+24/r08完整Provider2332P全名同父；原所有测试文件和断言原字节。r09/r10 AppWorker各实际0W/E；五轮67源335绑定、23目标DLL与实际childSHA另核。独立源码终审无剩余P1/P2，封存另审，见[会话报告](validation/installed-session-launch.md)/[机器索引](test-evidence/2026-10-17-installed-session-launch.json)。

Windows/WPF/真实Dispatcher/STA/可见健康/键盘/Job/OCR/截图/DPAPIACL/物理250600/模型仍NotRun。既有committed自退仍持久拒入，显式review/probe另有原生命周期。真实注册/签名/扩展ID/绝对路径再生成/完整payload/稳定launcher/生产安装器Inno/自启卸载/K组与完整P7未验。历史图片/idle/Core/Job及父r10/r17/r20根因各Unknown；生产图片分类复用Off、来源/密码/完整性/取消/清理及250600门不变。无main强推部署费用真实模型凭据权限/新依赖许可；Skia四包待批，403及拒绝Actions不查不绕；未知锁数据日志进程保全。

本片封存独审Pass（727原件/743路径），依约只追加审查回执和既定stage-audit后最终729原件/14,228,145B；旧9775路径/8869旧证据原字节。源码与测试未再修改。


## 2026-10-18 · 恢复独审与正常会话最终结算收据

用户明确恢复后只用原保存环境，HEAD/远端e835/直接父369796及clean单worktree核实，既有reviewer仅只读、本任务唯一写入者；729旧原件14,228,145B/67源及原测试哈希重核保全，不恢复旧实例。正常dev/session-close-receipt，先独审e835差异与原测试，未先扩安装功能。

本次补充覆盖重新打开两个P2：CancelAsync先使原loop完成可跳CLOSING，真实Exit0误永久拒入；原loop协议故障与Close dispatch重叠仍发CLOSING，真实Exit0却错误归档owner。r03实际8例3F5P，两个主反例方法从红到最终绿逐字节相同；第三TERMINAL_MISSING仅最终合同加严，Theory清理期望后变字节，不冒充同方法红绿。旧e835“无剩余P1/P2”结论以本追加修正，旧报告原件不回写。

生产只改既有control wire结算：取消前快照notify、缓存整次原settlement Task，CLOSING只请求关闭精确child stdin；原CancelAsync和原loop全join且非committed/无FailureCode后才发唯一精确EXIT_READY。非预期EOF先记录ControlEof再派发原Close，不能借调度伪造健康终态。父strict正常session需有序唯一双收据、实际Exit0、原自然IO/原Task成功及精确记录，旧Generic/committed门保留。无服务/守护/扫描/自动重启/PID收养/新产品角色，原App入口同源接线继续。

新增9例，父1834个Core case完整保留，全名+9；原会话24例代码精确前缀不动，仅追加新方法。包括两次实际child取消竞争、真实stdin协议故障、EOF先完/关闭重叠、终态缺错重提前、最后owner归档File.Move冲突。可取消命令read为明确端口，START实际stdin，无遗弃underlying read，不充Windows Console取消实测；故障反例原stdin/stdout，仅调度屏障。EARLY/DUPLICATE原IO FaultObserved后实际0/DisposeUnconfirmed/owner与fresh门保持，公开失败根保留，未删失败锁。既有丢父仍不证child OS退出，HoldIo仍仅adapterTask。

r01 nullable/definiteassignment编译与r02 xUnit1031分析器失败无TRX，显式修正/await不关分析器；r03实际3F5P；r04相关139P2F是新readerfault用例错误期待成功Dispose，改为更准确拒入故障断言，未改生产finalizer或吞错。原件全保。最终r05相关141P/r06完整Core1843P=父1834+9/r07完整Provider2332P同父名单，App/Worker r08/r09各0W/E；五轮67源335绑定/23目标DLL与实际driver/Core收据核定。独立源码终审无剩余P1/P2，封存另审；见[恢复审查修复报告](validation/session-close-receipt.md)/[机器索引](test-evidence/2026-10-18-session-close-receipt.json)。

Windows/WPF/实际Dispatcher/STA/可见健康/键盘/Console stdin取消/Job/OCR/截图/DPAPIACL/物理250600/模型仍NotRun，生产安装器/注册签名扩展ID/稳定入口/完整payload/Inno/自启卸载/K组/完整P7未推进或未验。历史图片/idle/Core/Job及旧r10/r17/r20根因各Unknown，不能据新协议实证宣称旧间歇失败解决。生产图片分类复用Off和250600/来源/密码/完整性/取消/清理门保持。无main强推部署费用真实模型/新增凭据权限/新SDK条款；Skia四包许可与403未解决、不绕过降隔离，拒绝Actions不查不换路，未知锁数据日志进程保全。

本片最终封存独审Pass：审计653原件/660路径，依约只追加回执及既定stage-audit后共655份/13,668,794B；代码/测试未改，父10515路径及9599旧证据全保。

## 2026-10-18 · 独立目录 App 启动验收构建与自包含准备

父7f9df38有限准入后只读核P7真实链；本机回报四项前置Blocked/NotRun，固定KnownFolder不能靠LOCALAPPDATA进程改写隔离、仅NET7无NET10Desktop/SDK/已核闭包、桌面工具缺失且CIM拒绝已停。本轮按续授在原Linux环境唯一写入，正常dev/isolated-session-data，未访问本机。新增编译期显式验收App，默认生产不编译selector/不接通用root参数；只允许0参普通入口/原精确握手，从catalog descriptor/专用runId容器推导固定siblings user-data，父/child/Shell统一root，祖先link/Windows别名/生产双向包含/错布局拒绝，无回落或修复。原identity/owner/lease/mutex/取消清理门保持，test拒入不唤回生产窗口。

现有fixture仅增加真实发布输入→catalog→Store.ApplyAsync准备，独审P2输入生产重叠/输出包含漏门实测同测试源5F30P→35P，前置拒绝修复；脚本任意output取消、共享真实KnownFolder门先批固定新workspace、未知内容保全。包源/审计源均固定本地且Audit=true，失败Blocked，不下载/关审计/force-evaluate/改锁/降级。最终45新例全部包含于Core1888P=父1843多重集+45，Provider2332P父全名单；验收App/默认生产App/Worker实际交叉构建均0W/E。r01新测试调用编译错、r02新断言异常族14F、r05WPF缺IO编译44E全保，不冒称产品红。生产App和框架依赖验收App实际准备均拒绝且无输出，旧output参数也实际拒绝。

离线准备实际Exit2/Bundle=null：缺NETCore和WindowsDesktop Runtime.win-x64/10.0.12两个既有cache pack，无恢复/发布/ZIP、无SDK/系统安装；关键运行文件验证仅结构检查，不能声称完整闭包。源码及83份原件封存独审Pass；按许可只附回执后84份，源码/测试/已审83份未改。详见[实现/精确待验与边界](validation/isolated-session-data.md)、[运行和红绿原件](test-evidence/2026-10-18-isolated-session-data.json)。Windows/WPF/Console/键盘/安装器/完整P7仍NotRun，需已核自包含包及获准桌面工具后实际两次无参启动正常退出。旧各间歇根因Unknown、图片复用Off及250600/来源/密码/完整性/取消/清理门不变；无Skia许可403/Actions/CIM绕行、main强推部署费用模型凭据权限变更，未知锁数据日志进程保留。

## 2026-10-18 · 隔离包运行时安全补丁准备（Blocked）

父 b21 的隔离代码有限准入不等于包准入。后续官方公告确认 runtime10.0.12受影响、10.0.13修复；“缺缓存”不能作为唯一门，原空目录auditSources也不构成真实审计。正常dev/session-runtime-security准备精确SDK10.0.402/runtime10.0.13及官方有效审计源，保持审计all/low和warnings-as-errors；新增只恢复/审计的两runtime精确库存与官方hash锁，尚未实际locked restore。两份官方13包已按已有普通依赖授权下载，SHA512等catalog/lock、ZIP CRC和MIT声明核实，签名仍待安全SDK，未执行包。

独审先指出审计无项目/错误项目空库存误准与发布DLL未绑定包字节：真实无效审计2F→同输入拒绝；精确项目＋普通库存＋错误状态门已收紧，App零包须同项目assets纯Project证明，runtime仍恰两13。实际runtime档案离线映射又揭WindowsBase Core stub/WPF双声明冲突，合法合成输入拒绝红保留，现明确Desktop胜者、其他冲突拒绝；409唯一路径字节全符、逆序同图、篡改CoreLib拒绝。最终Python15方法通过；这不是.NET发布或Windows加载。新增C#16例、真实NuGet/签名、完整回归与App/Worker构建均NotRun。

现有SDK10.0.401宿主/私有XML仍为受影响12，准备器实际在任何dotnet进程前拒绝、Exit2/BootstrapExitCodes空/Bundle null；未下载/执行已知漏洞12。新SDK需先报告，云独立目录SDK10.0.402授权已请求但仍Pending，未下载SDK；当前源码未提交推送，包保持Hold。详见[安全修复准备报告](validation/session-runtime-security.md)/[原件索引](test-evidence/2026-10-18-session-runtime-security.json)。Windows/WPF/Console/键盘/完整P6P7等未验，历史各间歇Unknown、图片复用Off与250600/来源/密码/完整性/取消/清理门保持；Skia许可403、Actions/CIM拒绝不绕，未知锁数据日志进程保全，无main/强推/部署/费用/模型/凭据权限变更。


2026-10-18 09:22:58 UTC 用户明确批准 SDK402 云独立目录请求后，继续同唯一写入者：官方下载240,279,509B、完整SHA512等公布值，归档MIT与官方源码相同、77,436B第三方notices保留，无新显式接受步骤；新目录解压5,631项/640,467,796B，原SDK目录不替换。新SDK实际--info/--version均0，宿主和私有XML13核实；未运行旧12。r02/r03定向命令均在编译前NuGet首次使用初始化Exit1，无TRX，不作产品红/测试已跑。自有child文件追踪明确mkdir(/home/agent/.dotnet,0700)=EROFS，另用户NuGet Migrations/1不存在；共享tmp与未知锁保全。用户要求额外权限先停，未改HOME/伪造sentinel/跳迁移或改安全/审计，当前需父级决定合法用户级初始化环境或最小额外目录权限。SDK批准已完成，不再误报为当前门；真实NuGet/签名/C#回归/AppWorker/publish及ZIP仍Blocked/NotRun，无新提交推送。证据补充在同[报告](validation/session-runtime-security.md)/[索引](test-evidence/2026-10-18-session-runtime-security.json)，批准前34份原件原字节保留。

父级要求后续仅只读核路径来源，不重跑失败命令、不请求放宽home或试替代路径。本轮statvfs/findmnt确认home所在根overlay ro、workspace rw；原内核EROFS与外层工具审批拒绝区分，但readonly挂载仍是平台写入边界。原CLI_HOME已workspace，官方当前NuGet源码XDG_DATA_HOME可定位迁移记录，Run先建命名Mutex，未证明可移走已失败CLR同步目录；dev源码未与SDK精确源码绑定，字符串不充证明。一次GitHubAPI目录树GET的代理隧道403已停止，不重试/换路，与本地EROFS分记。产品源码哈希全不变、无新增SDK执行/测试/权限申请；只追加调查和原件，不能计新代码成果。当前等平台明确受支持初始化条件，包仍Hold。


## 2026-10-19 · SDK 正常初始化修复与安全门实跑

按继续解决初始化的明确授权，新增固定工作区 Linux 启动器：依据官方10.0.13新安全语义，以用户自有0700 XDG_RUNTIME_DIR承接默认用户级mutex，XDG_DATA_HOME/CLI_HOME承接真实迁移与CLI状态；HOME不变、既有权限不改、不跳first-use。原r02/r03 EROFS保留；r04实际完成迁移及61P，HOME只读且HOME/.dotnet仍不存在。原SDK不替换、不执行旧12。

实际恢复发现原拟锁混淆NuGet内容哈希与完整签名归档哈希，现独立完整性清单继续固定官方完整SHA512、lock使用实际签名内容哈希，独审内存重算一致；r11 locked restore＋双runtime13库存＋真实漏洞查询通过。verify虽Exit0却有NU3018/NU3028撤销未知，新增实际警告拒绝及在线环境前置门，offline/禁验证拒绝，Bundle=null，未publish/ZIP。没有把Exit0或离线Python当签名准入。

最终Core1904P=父1888+16、Provider2332P父全名单；Python22方法通过；默认/隔离App及Worker均实际0W/E。App正常locked restore与实际库存/漏洞查询通过，零包由同项目七项纯Project assets绑定。r06路径拼写、r08 NU1403、r13/14全局RID锁不符、r19缺WindowsTargeting上下文与初始Python fixture错误均保留；修同上下文后实际绿，不降安全检查。原已有测试/8workflow与前56份原件全保；构建后另采两个-o目录56文件哈希，不倒填原运行元数据。源码独审Pass，无剩余P1/P2。

详见[当前结果及历史调查](validation/session-runtime-security.md)/[实际原件索引](test-evidence/2026-10-19-session-sdk-initialization.json)。初始化不再Blocked；包仍Hold，最小缺少正常在线签名撤销状态无警告。旧Actions/CIM/API树403不重试不换路，现有CI门不适用、服务端状态Unknown。Windows真实验收及完整P6P7仍NotRun，历史间歇根因Unknown；生产图片复用Off及250600/来源/密码/完整性/取消/清理门保持，Skia四包待批，未知锁数据日志进程保全，无main/强推/部署/费用/模型/凭据权限变更。

封存独审指出原NU1403日志一处尾空格；保留所有原件，.gitattributes仅逐精确原件路径-text、该单日志-whitespace。完整暂存diff检查实际通过，不泛化豁免源码或改变日志。


## 2026-10-19 · 父审 RID/锁上下文疑点核验（无生产代码改动）

实际原样提取已提交 prepare AST：r26带Alpha＋全局win-x64的locked restore成功8项目；r27仅删全局RID反而六库NU1004；r28原样恢复再次成功，8assets字节复原。prepare选text-alpha-locks（含库级RID），父审引用r13/r15用普通锁，不能混同。该具体P2未复现，restore/publish同RID和12props，所有生产源/锁不改。此为核验成果，不冒充产品修复；[原件索引](test-evidence/2026-10-19-session-rid-verification.json)。不重试在线签名、不publish，当前首个已知阻塞仍签名；后续打包/Windows尚未运行，不能预报仅剩此门。


## 2026-10-19 · 一次免费 Windows 运行时验签 CI 准备

按父级最新一次有界CI授权评估：当前仓库根页仍Public，GitHub官方说明标准windows-2022在公开仓库免费；不使用larger runner、cache或artifact上传。现8workflow均旧分支/自身路径及旧源码门，lifecycle仍SDK401/关闭审计，不能借用。本次仅新增独立dev/session-runtime-signature-windows的自身YAML触发：精确单父fffb37f、四文件范围、attempt1、contents read、固定checkout、单job15分钟/step14分钟，无自动重试/安装/权限凭据信任更改/部署/publish。

官方镜像20261012.333.1清单预装NuGet7.9.0.83与.NETFramework4.8.1，SDK仅401/runtime12。先由Windows PowerShell5.1及既有Python检查预装nuget.exe的Framework标记、实际版本及哈希，再下载原已批准两份MIT/runtime13，严格归档hash、license/identity、NuGet内容hash和原作者/仓库签名，正常在线-All验签，任何NuGet诊断/警告/撤销未知/降offline都拒绝；不执行旧SDK401。只有预装SDK402、host13及私有XML13元数据通过才执行dotnet，保留locked/audit-all-low/warnings-errors并继续完整Core/Provider和App/Worker构建。缺安全SDK明确SignaturesPassedTestsBlocked，无降级或SDK安装。全程不运行App或真实模型，不生成包。

旧gh API仅证明actions/runs读取Forbidden，未证明CI调度被拒；最新授权下的正常新分支push与重试被拒读取是不同动作。不会查询该API或换连接器/浏览器读取Actions结果。push若成功只证明投递，实际run/签名/测试结果在没有合法原件前仍Unknown，签名Hold不解除；不能据此宣称Windows App/完整P6P7验收。新配置先做离线合同与独审，通过后仅普通推送一次。生产源码/所有锁及8既有workflow不改。

离线合同33方法实际通过，含两个合成官方形状包先完成验签门再因缺SDK明确阻断测试、超时保留输出且不确认清理、Python3.9兼容reparse门。此为纯离线fixture，不包含真实NuGet下载、签名命令或Windows执行；源码独审未发现剩余阻断P1/P2，最终原件哈希另行封存。

官方依据：[公开仓库标准runner免费](https://docs.github.com/en/billing/concepts/product-billing/github-actions)、[标准Windows规格](https://docs.github.com/en/actions/reference/runners/github-hosted-runners)、[当前仓库Public](https://github.com/T1doo/KuaiYi)、[Windows2022工具清单](https://github.com/actions/runner-images/blob/main/images/windows/Windows2022-Readme.md)、[NuGet.exe verify-All](https://learn.microsoft.com/en-us/nuget/reference/cli-reference/cli-ref-verify)、[Windows CLI使用Framework4.7.2+](https://learn.microsoft.com/en-us/nuget/install-nuget-client-tools)。本片只是CI验证准备，不把离线合同称为Windows验签通过；历史图片间歇等根因仍Unknown，生产复用Off及全部250/600ms/来源/密码/完整性/取消/清理门保持。

源码 c4ec07e80c2eec46476528c5afab6f06a78a8bfc 已普通推送新开发分支一次，Exit0、远端精确SHA一致；远端62→63 heads仅新增该分支，main51d0774与其余62 refs不变。该投递不等于CI运行成功，未查询Actions、未重试任何已拒路径，实际Windows签名/测试均Unknown，Package Hold保持。[离线合同、独审、源码快照及投递原件索引](test-evidence/2026-10-19-session-runtime-windows-ci.json)。后续证据提交仅docs/.gitattributes，不触碰自身YAML路径，不再次请求该CI。


## 2026-10-19 · 预装 NuGet Chocolatey 布局兼容与 Windows 环境键修复

父级正常登录桥接run39216695459/job117942108656的可见日志：源码c4ec07e，33秒在verifier Framework前置检测Blocked，Reason="Preinstalled verifier does not declare the expected Framework target"；签名、managed/build/publish/真实Windows验收均NotRun，RuntimePackages空、模型无、包Hold。此为验签器前置失败，未证明坏包或产品回归失败。原日志缺实际lookup路径，不能据此认定该run根因就是shim；本地未重试Forbidden接口。

官方toolset将NuGet.CommandLine列为Chocolatey包，官方shim文档说明bin是native启动器。现仅将准确的既有CHOCOLATEYINSTALL/bin/nuget.exe映射到同root/lib/NuGet.CommandLine/tools/nuget.exe，lookup/root/target全部在resolve前查链接/reparse祖先，拒绝checkout内或未知映射，不搜索或执行shim。实际payload仍过原Framework标记和实际NuGet7.9.0.83版本门；缺标记继续Blocked，不用MZ/banner替代。成功失败记录lookup/target类别、path、bytes/SHA/Framework标记及拒绝阶段。另据Python官方Windows环境键大写语义，修正dict(os.environ)后CHOCOLATEYINSTALL/PROGRAMFILES查找，未修改环境或系统安装。

实际离线r01为旧实现36例1失败；r02切Windows大写环境fixture后38例4失败；最终38方法全通过，保留两次红日志和各自源码快照。新增用例只在合成fixture中运行mock端口，证明不运行shim、只调已检payload，并覆盖Core目标/未知布局/缺payload/链接/reparse/checkout路径拒绝；没有本机NuGet下载/执行或Windows实测。源码独审后仅一次新有界免费CI，15分钟job/14分钟step、attempt1、公有标准windows-2022、精确父1567018及四文件范围。正常在线验签/警告拒绝、归档与内容双hash、MIT范围、安全SDK402/host13/privateXML13、locked/audit与完整managed/build门保持；无安装/离线撤销/信任或权限变更/部署/真实模型。实际新CI由父级合法页面桥接，在原件返回前Unknown/Hold，不自动重试。

依据：[官方Windows toolset](https://raw.githubusercontent.com/actions/runner-images/main/images/windows/toolsets/toolset-2022.json)、[Chocolatey shim](https://docs.chocolatey.org/en-us/features/shim/)、[NuGet Framework项目](https://raw.githubusercontent.com/NuGet/NuGet.Client/dev/src/NuGet.Clients/NuGet.CommandLine/NuGet.CommandLine.csproj)、[Framework目标定义](https://raw.githubusercontent.com/NuGet/NuGet.Client/dev/build/common.project.props)、[Windows环境键](https://docs.python.org/3/library/os.html#os.environ)。生产源/锁/8旧workflow与历史原件不动，图片间歇根因仍Unknown，生产复用Off和250/600ms/来源/密码/完整性/取消/清理门保持；Skia四包仍待批，WPF/模型/完整P6P7未验收。

最小修复源码1662f6526147a98e5b6d42e148d18294b5397e55经源码/原件独审Pass后已单次普通推送同开发分支，Exit0、远端精确SHA一致；63heads仅该分支从1567018前进，其余62refs含main51d0774不变。[三轮红绿、源ZIP、父级旧CI摘录与投递原件索引](test-evidence/2026-10-19-session-runtime-verifier-compatibility.json)。新CI实际运行/签名/测试Unknown、包Hold，后续仅由父级正常登录桥接；本地无Actions读取/旧job重跑/替代绕路。后续证据提交仅docs/.gitattributes，不改YAML路径，不再次触发本CI。

## 2026-10-19 · Windows 在线验签通过与 SDK 实际许可核查

父级正常登录桥接新run39221162999/source1662f65：两runtime13实际WindowsFramework正常在线verify Exit0/SignaturePass；前置验签器已打通。预装SDK402缺失导致后续测试/build/publish/WinAppNotRun，包Hold，不把旧NoSDK窗口当永久禁令。按最新授权查微软非admin CI ZIP安装路线，实际官方WinSDK402 ZIP300755826B/SHA512精确匹配既有官方metadata，只读许可与SDK402/host13/privateXML13/sharedCoreDesktop13元数据，程序未解压安装执行。根许可9519B是使用即接受的Microsoft.NET Library条款，与已有明确批准LinuxMIT1116B、两runtimeMIT均不同；notices仅CRLF全文相同，不能消除根条款差异。缺的最小批准仅该固定Windows归档条款及一次临时标准Windowsrunner job目录构建测试，不需系统安全权限／费用，不含Skia或发布。具体可实施方案与确切原文／hash已封存，当前未改代码／workflow、未启动新CI。一次本机blobmetadata403已停，用已提交原件，不重试或换源；WindowsZIP是独立目标200，全hash通过，无Actions请求。详见[许可及临时CI范围](validation/session-windows-sdk-assessment.md)和[证据索引](test-evidence/2026-10-19-session-windows-sdk-assessment.json)。


## 2026-10-19 · 已批准 Windows SDK402 临时目录安装与一次完整托管 CI

用户2026-10-19 16:31北京明确“接受”6df固定Windows归档条款（Sentinel_e978556d48a48191aa940cf146bdb928），此前具体许可Pending解除；仅限一次临时标准Windowsrunner job目录安装402并审计／测试／构建，不含系统／安全／main／发布／费用／Skia／真实模型。

新增stdlib安装器，固定原官方ZIP300755826B／完整SHA512及已接受LICENSE/notices双SHA256，在HTTP handler层拒redirect且无重试／换源。全部Windows路径、大小写重名、尾点空格／ADS／设备名、file-parent冲突、symlink/reparse、加密／条目数／展开体积在写程序前检查，只写fresh job SDK目录，已存在或部分失败全部保留。真实官方归档5577条目807617264B实际只读预检通过；许可和SDK402／host13／privateXML13／CoreDesktop13 metadata全符，没有本地解压或执行WindowsSDK。

原双runtime正常在线签名先行，安装后原require_safe_sdk仍先于dotnet，明确child DOTNET_ROOT/HOST_PATH/_X64、job内CLI和NuGet缓存；不改父环境／系统PATH／注册表／信任／权限、不跳first-use，不用401。实际SDK --version/--info后继续原locked audit-all-low/warnings-errors、完整Core/Provider及默认／隔离App、Worker构建，任何SDK／审计／测试／build失败不继续后续阶段；无publish／App启动。

离线旧实现39例1失败保全，安装编排接入后当前51方法全通过；合成SDK与子进程端口不冒充Windows执行。覆盖SDK hash/license/host拒绝、已存在／linked／过期预算、403单次调用、HTTP层redirect拒绝、WindowsZIP危险路径／类型／膨胀限界及全编排各阶段失败停止。仅显式登记command logs及两份实际TRX做有界gzip/base64分片写标准job日志，保存raw/gzip hash、size、片号／总数；32MiB raw／4MiB gzip／每件16MiB限界，缺件／变动／导出失败标Incomplete且不掩盖原失败；完整原件需父级正常登录取齐分片并核hash，无artifact/cache上传费用。

本轮六文件源码精确父6df5f91、attempt1、Public标准windows-2022、job15／step14分钟，源码独审后仅一次正常push请求新CI；不重跑旧job，不请求Forbidden CLI/API或blobmetadata。新真实结果在父级合法桥接前Unknown，Package Hold保持。生产C#／锁／8旧workflow／全部历史原件不动，图片间歇根因仍Unknown，复用Off和250/600ms／来源／密码／完整性／取消／清理门及真实WPF／模型／完整P6P7未验收边界保持。

独立源码审查Pass后，32116577a34d9f914f93dc715261e99da7d33868已单次普通推送，同63heads仅该分支从6df5f91前进，main与其余62refs不变。红绿为旧39例1F→最终51P；最终主反例追加3断言，不称同方法字节红绿。源码ZIP／19输入hash／归档预检／独审消息／批准与投递原件见[索引](test-evidence/2026-10-19-session-windows-sdk-ci.json)和[交付记录](validation/session-windows-sdk-ci.md)。新实际CI链接、SDK／全测试／三构建仍Unknown，待父级正常登录桥接完整JSON及分片raw/gzip双hash核验；Package Hold，无本机Actions查询或自动重试。此证据提交仅docs/.gitattributes，不再改变专用YAML，不再次投递本专用CI。


## 2026-10-19 · Windows Core真实失败后的最小诊断与修复

父级正常登录核实source32116577的run39226078160/job117972581384 Fail：两在线签名／job SDK402-host13／runtime库存audit／Corelockedrestore Pass，进入xUnit后45失败调用／25方法（非最终计数）再180秒超时，无CoreTRX，EvidenceIncomplete／cleanupNotConfirmed，包Hold；后续专门AppWorker构建门／publish／真实App模型NotRun。没有本机Actions读取、第二次CI或加时。

五源最小变化：原路径组件先于GetFullPath检查，四同字节别名反例实际4F→布局49P；两旧测试不再把Environment.ProcessPath当dotnet，测试专用选择器只用明确绝对SDKhost或当前确为dotnet。新增真实Linux SDK／已有apphost派发公开fixture对照Exit0／2，保留bounded输出hash／exit／原任务收据，未结算实际owner原task不释放；不能据此认定WindowsCrash根因。生产环境白名单、预算、清理门不改。

r03新诊断JSON字典顺序断言118P1F及漏untracked快照如实保留，结构比较修正；r04完整1918P不替代两处最终探针归属修正后的r05完整1917P1F0skip。531最终源绑定，父1904全名保留+14新增全P；旧Healthy等待8.015秒在current.json改写前超时，cleanup137／IOSettled有据，根因Unknown。r06同DLL定向3P仅诊断，不作完整绿或修复；r07隔离App实际交叉构建0W/E。独立源码／结果审查Pass，整体回归Fail与包Hold保持。长base64本地转录未过hash，未当CI原件；可信父级摘录与分组另有明确来源。九workflow／安装runner／锁及全部保护不改，普通push不命中专用YAML，不再投递第二次SDKCI。详见[诊断报告](validation/session-windows-core-diagnosis.md)与[成功／失败原件索引](test-evidence/2026-10-19-session-windows-core-diagnosis.json)。

封存独立审查Pass：36份原件11,641,931B／五ZIP／最终531输入／46精确路径与31保护输入已核，完整回归仍1917P1F，包Hold；无第二次WindowsSDK CI请求。


## 2026-10-19 · Healthy等待观测与诊断异常保全

仅既有会话夹具同时观察原run与Healthy，保留client admission／health receipt任务、真实owned child退出和原IO状态；诊断采样／写入出错单独保留，不覆盖原异常。生产1秒协议预算、记录／归档断言和所有门不变，无新探针框架。受控held-ready同方法字节实际1F→1P；日志写入反例同方法字节1F→最终两新例2P。r01执行0例和r05CS0649无TRX均保留且不算红。r06会话类35P为中间字段形式；唯一完整r04诊断1919P在最终guard前，未复现旧自然Healthy失败且未再全量跑绿，不冒充最终完整。旧1917P1F保留、自然根因Unknown。

父级八片完整Windows日志已过每片ASCII及gzip／raw双hash，65376B原文直接核45失败调用／25方法，非最终计数；ReuseCrash／OLE／其他Windows底因仍Unknown，包Hold。四alias/../用例也命中..guard，不单独证明alias分支。09:44:54UTC同一实例命令可执行，修复与原件保全；无未知进程操作。369保护输入保持，无被拒Actions查询或第二次WindowsSDK CI投递。531最终输入及全部失败／无效尝试见[证据](test-evidence/2026-10-19-session-startup-handshake.json)与[报告](validation/session-startup-handshake.md)。

独立源码及封存审查Pass，无剩余P1/P2；53路径／47原件12,356,734B已核，审查回执另封存，不把诊断绿当自然根因修复或Windows验收。

## 2026-10-20 · 原退出等待任务的生产生命周期缺口

独立定位共享OwnedHostTermination的实际P2：外层预算超时后原Wait未保留，HasExited可授权提前Dispose实际Process。现缓存唯一原Wait(None)，保留调用者预算和未结算owner；先发布重入门，late fault/cancel先观察再结合实际退出，重试不重复Kill/Wait。六旧合同保持，永不结算原任务不冒充有界或成功。

同测试字节真实4F→10P，实际child退出137且originalWait pending时finalized从True转False。八类定向r03为205P1F，唯一旧Capture无限Delay(None)夹具无法结算；只把该端口接回同一真实child退出，原拒入／cleanup断言不改，最终两类34P／531输入绑定。未重复1919全套；不将新P2归因为旧Windows45calls／25methods，Windows／图片间歇底因Unknown、包Hold。父级转达用户批准同范围必要免费WindowsCI，待源修复独审保存后以精确父提交四文件CI增量运行，原SDK／隔离／预算及保护门不变。详见[报告](validation/owned-exit-observation.md)与[失败／成功原件](test-evidence/2026-10-20-owned-exit-observation.json)。

源码与封存独审Pass：28路径／21原件5,128,429B／四轮2124来源绑定／374保护输入均核，审查回执另列第22原件；新CI配置仍另审。


## 2026-10-20 · 已批准WindowsCI的精确源码接续

生产修复98c3f27d07ee392bf98e647861596812c2d63a39已独审并普通推送，未改YAML。父级可信委派转达用户明确批准同范围必要免费WindowsCI：原已接受SDK10.0.402条款、固定archive／双在线签名／audit／public标准windows-2022／job15min step14min内总13min attempt1／无model publish userPC或新权限许可。

本CI增量严格四文件：workflow仅改名称以请求新run，runner精确PARENT=98c3f27及四文件scope，源门测试补拒绝错误event SHA和dirty checkout，本文进度。SDK安装器／其测试／锁／packaging／生产源码／旧8workflow全部保持。离线两Windows脚本测试模块29P（真实Windows未执行），完整Linux套件未重跑。新结果待独审后单次正常push及实际CI日志核验，不补签旧45calls／25methods失败，不将本地34P当Windows验收；包Hold。失败先完整读日志，必要同范围免费修复CI已获授权，无盲目重跑。

已审CI源码00ecaae6def945cf39f3bdacf5fca7f8dc2d69e9以98c3f27为精确父提交，真实Git四文件源门Pass后单次普通push请求获批验证；离线29P不代表Windows。新授权后唯一Actions读取仍Get Forbidden／Exit1，无数据，停止且不换路。实际run/job链接、终态、各阶段、原始日志/TRX均Unknown，不能认定成功、失败或未启动；待父级正常登录桥接确切SHA，先查已有运行，不再触发。此后仅docs封存不命中YAML。详见[投递与阻断](validation/owned-exit-windows-ci.md)和[原件](test-evidence/2026-10-20-owned-exit-windows-ci.json)。

投递与读取阻断封存独审Pass：24路径／20原件138,206B／事件四源Git00ec匹配；审查回执另列，不补签实际Windows。仅docs封存push，不再投递CI。


## 2026-10-20 · 本次CI终态与静态首因核验

父级正常连接确认run39347646426/job118384016034，源码00ecaae，00:26:24—00:30:24UTC实际Fail；checkout Pass、第3步整个Verify/SDK/managed流程失败，首个内部命令／阶段未知。详细页credential_observation_restricted在一次工具规定验证后仍受限并停止；本机Forbidden也未重试，不换路、不盲重跑。

仅Git/AST只读核父98c3f27与四文件范围匹配、workflow仅name差异、runner除source_gate文案外函数AST不变、18安装／SDK／锁／旧workflow输入一致。无可证静态早期缺陷，无源码／门／SDK／测试／CI新执行。需现run的完整step3 raw、最终JSON及首个失败命令raw/gzip/TRX（若生成）定位；不由步骤名称或4分钟时长推断。原件与旧Unknown状态保留，包Hold，详见[真实终态与静态核验](validation/owned-exit-ci-static-followup.md)和[证据](test-evidence/2026-10-20-owned-exit-ci-static-followup.json)。

只读静态及封存独审Pass：8路径／3原件6,405B，旧证据零回写，无不当阶段推断；只docs正常保存，不命中CI触发路径。


## 2026-10-20 · 浏览器原图读取的取消与缓冲清理

从远端ade0b4a基线新建dev/browser-image-read-cleanup；main未动。原计划P7-02/P7-03真实缺口已核：HTTP/MIME/声明大小拒绝未取消body，超限末块未清零，取消/返回边界与reader锁释放不足。只改原original读取：统一reader所有权范围、取消前置/逐read/最终复核、late/oversize块先接管、原cancel单任务结算后释放锁、失败结果清零与双异常保全。保留同源HTTPS/PNG/20MiB/不带凭据/拒redirect、桌面Ready先行、原65/30/5秒门与未经证明截图拒绝。

最终同测试字节基线12项1P11F，修后独审12P，纯Node既有+新增64P；真实native Streams/loopbackHTTP/生产ServiceWorker+受控Chrome端口有据，不充真实浏览器/宿主/Windows。初次筛选误匹配祖先致64P11F（缺dotnet ENOENT）、基线导航timeout后本任务Node终止、中间夹具预取断言失败均保留；最终测试有界清理自身流并join。11项原.NET宿主测试本轮未完成；无旧401执行。脚本登记新测试，PowerShell未运行；75项首次尝试64P11F，未取得完整75项通过。源码与范围独审无P1/P2；见[修复及实际范围](validation/browser-image-read-cleanup.md)/[证据索引](test-evidence/2026-10-20-browser-image-read-cleanup.json)。

P7源码子项InProgress，完整P7/R13/真实浏览器与Windows仍NotRun；CI39347646426第3步首因Unknown、PackageHold，拒绝日志路径不重试不绕。未改C#/锁/SDK/workflow/安全权限/代理/凭据/模型/发布或用户电脑。

本轮原始失败日志自带尾空格，最终stage检查明确报告后依仓库原件惯例仅对该新日志目录设-text/-whitespace，原日志与SHA不改；源码空白检查保持，未降产品断言。


## 2026-10-20 · 补齐既有11项真实宿主集成用例

从已接受64745d23f689d4fc9507507fb580679d6a8de7f4建dev/browser-host-integration，产品源码/原测试/锁/脚本/workflow原字节保持。核历史Linux402云workspace明确批准及当前续授，官方同归档240279509B全SHA512、MIT/notices逐字节等已批原件，无新协议。只解原项目隔离SDK402，复用既有正常初始化launcher，HOME/既有权限/旧401/安全网络与凭据不动，禁证书生成。--info实际SDK402/host13，静态私有XML13；locked restore审计true/all/low＋warnings-errors，五项目实际Release0W/E。

只跑上轮ENOENT的同名11项，实际Exit0/11P0F0skip0cancel（8.109秒）；137输入等647/16宿主输出SHA/4依赖DLL与实际项目输出相同，5assets纯Project/audit属性和原件核实。生产BrowserSession/DesktopPipeChannel/真实stdio+localpipe，10例经DesktopReceiverSession/Inbox/Gate；ChromePort/peer/decision及9例clock合成，6秒等待实elapsed。此前64P/11F保持，不重复扩大纯Node数字或声称单次完整75全绿。只读独核通过；见[实际范围](validation/browser-host-integration.md)/[证据索引](test-evidence/2026-10-20-browser-host-integration.json)。

原WindowsCI39347646426第3步首因Unknown，拒绝读取不重试/换路；无新CI/模型/费用/发布/用户电脑。真实Chrome/Edge/Windows/P6P7、签名安装及250600实际平台验收仍另列NotRun/PackageHold，来源/密码/完整性/取消清理及生产图片复用Off保持。

## 2026-10-20 · 浏览器图片操作的原窗口绑定

新环境实际fetch核到远端c7b3396e80871b64266dace0b44d1904d17b2583，初始文档HEAD及64745d23清理修复均为祖先，工作区干净；唯一新writer在dev/browser-source-window推进原SPEC P7-02/P7-03、R13/R16、K05/K06来源子项。旧active/url检查无法阻止切到另一浏览器窗口后继续读图/提交，移窗也未撤销。最小设计独审后，Ready后读图前及读图后传输前固定原tab/window/page并在每个await后复核取消/current；其他浏览器窗焦点、原tab移窗或原window关闭永久取消，已有PNG即刻清零。NONE允许真实桌面确认取得焦点，不要求浏览器focused；无新权限、预算续期或截图降级放宽。

最终同字节基线1P15F，修后16P且源码独审独立16P、无剩余P1/P2。完整浏览器91P0F0cancel0skip（8.390秒），包含原75与新16、原11项真实stdio/localpipe宿主集成；原断言保留，仅补窗口API端口。新测试使用生产Worker/真实Streams，窗口事件和Port明确合成，不冒充真实浏览器。初轮基线夹具一次2s超时与中间16F保全，最终同字节另存。五项目以同一已批准归档hash/license的隔离SDK402 locked restore＋Release构建0W/E；原401未执行，HOME/权限/SDKlauncher/锁/C#/workflow/manifest权限不改，未重跑整套.NET。

141输入/16宿主输出绑定及日志见[报告](validation/browser-source-window.md)/[证据](test-evidence/2026-10-20-browser-source-window.json)。P7/R13源码InProgress，106整体NotRun/PackageHold；真实Chrome/Edge须安装回执、精确extensionID注册和同用户同session Windows生产receiver及实际焦点/移窗交互。250/600ms、来源/密码/完整性/取消/清理/隔离门保持。旧CI39347646426 step3首因Unknown，被拒日志未读取/绕路，无新CI/模型/费用/发布/用户电脑操作；未启动当前sandbox属主不合要求的Chromium或改变安全设置。

## 2026-10-20 · 已保存普通配置的冻结预览与安全导出

从已推送且独审的31e0abba70a7a9db3ee5bc6def4b8ec1ae9a6870核clean/唯一writer/单worktree/远端与main，正常dev/settings-backup-export推进原SPEC8.3及P1-03/P4-01导出子项。设置常规新增完整普通配置JSON预览及用户选择新文件导出；只读有效已保存snapshot，不读草稿PasswordBox/SecretStore，不改源配置、不启用/发送。全部SecretRef/自定义头、应用规则授权/端点同意、位置与事务ID剔除，遗漏说明明示HTTP风险偏好和必要头重填；完整配置导入仍未做。

新session持原IO/CancelAsync到实际结算；父子Window关闭等待相同导出task。复用已打开文件路径/重解析检查及不覆盖发布，原四字段公开白名单/4096限额不放宽。新23P＋旧草稿3=最终26P，原四字段Provider110P（最终新类P2前，旧通路/断言随后不改）；最终App8工程Release交叉0W/E。独审发现“已发布＋Dispose错＋取消回调错”误报未导出P2，同方法最终字节真实1F→1P，published事实独立保留并保两原错；独立23P后无剩余P1/P2。

初两轮新夹具19项4P15F/17P2F、Linux首次App restore缺EnableWindowsTargeting的NETSDK1100、中间25P与组合红例原件保全；不当旧产品红或实机证据。最终测试文件与组合红测同字节，locked restore审计和原锁不改，无新依赖。详见[产品行为/范围](validation/settings-backup-export.md)/[运行证据](test-evidence/2026-10-20-settings-backup-export.json)。完整导入可继续独立实施，真实WPF/Windows文件对话框及关闭/句柄验收NotRun；106整体NotRun/PackageHold及生产图片复用Off保持。250/600ms、来源/密码/完整性/取消/清理/隔离门不动；旧CI首因Unknown及被拒日志边界保持，无新CI/模型/费用/凭据/安全权限/发布/用户电脑操作。


## 2026-10-20 · 完整普通配置的校验、合并预览与草稿导入

从已推送且独审的f854eb616e8dad0fdaba4c0e281bc9af8ea869c2核单worktree/唯一writer/clean/远端原分支和main，建dev/settings-config-import，继续原SPEC8.3/13.1及P1-03/P4-01导入子项。设置常规新增完整schema1普通备份导入：严格UTF8/256KiB/16层/重复未知及缺模块整拒，精确zh-CN；密钥引用/全部请求头/授权和本机元数据非空整拒。保留本机profiles/凭据/规则/位置/既有默认，新档freshID、能力Unknown、HTTP双开关清除待手动确认；普通偏好模板与热键组合仅预览确认进入草稿，不启用/发送/注册。

原有脏草稿/未保存密钥/恢复/只读/并发保存拒绝开始；owner/revision/保存版本绑定，迟到或ABA不可应用。取消持有原read和CancelAsync到真实结算，父子关闭join相同task；取消设置放弃导入，显式保存复用原事务与CAS，不覆盖未知或选择的源文件。最小设计和最终源码独审通过，无剩余P1/P2；首次Yield前取消1F→同方法过，完整合并超限及非法输出语言4F→同方法过，真实原件均保全。新导入最终独立47P（含生产export→真实文件→import确认回路）；旧导出23/旧草稿3/原四字段Provider110各自通过，非单轮计数。App8项目交叉Release0W/E；locked restore审计与锁保持，无新依赖。

首次CS0649及回路参数CS1503均为新测试编译0执行，保留日志和可用当时源码；中间65/68/69P及因果失败不抹去。见[产品行为/验证边界](validation/settings-config-import.md)/[证据索引](test-evidence/2026-10-20-settings-config-import.json)。Windows原生对话框/句柄/WPF父子关闭、DPAPI及真实热键注册仍NotRun；交叉构建不替代实机。106整体/生产NativeHost安装及同用户session精确ID/真实浏览器/P6P7/250600仍NotRun/PackageHold。来源/密码/完整性/取消清理隔离及生产图片复用Off保持；旧CI39347646426 step3内部首因Unknown、拒绝日志未请求或换路，无新CI/模型/费用/凭据/安全权限/发布/用户电脑操作，main未改。


## 2026-10-20 · P6固定四包许可获批与源码审查门核实

从clean且远端一致ef26c900b8fb5291a8ab1979803c240a34974086核Linux/单worktree/唯一writer/main，正常dev/p6-codec-source-review。用户明确接受SkiaSharp4.153.1及Win32/macOS/Linux.NoDependencies四包原MIT和原生第三方条款，限指定云开发隔离解码与合成测试；许可门已解除，不重复询问。四个此前未拒的官方nuspec本轮各一次有界HTTPS，4×HTTP200/exit0且SHA全等已存原件；仅确认声明commit4783f51448f9b070dda4f87b83e941c9599e466e和net10.0依赖，非源码取得。

当前network enabled/既有proxy:8080/普通Git成功不能证明原api.github.com两个精确目标解除；原回执无可比proxy策略原件，独立复核不足以重试。本轮精确目标请求0、替代源码路线0，不将旧403说成本轮新403或全网失败。仍缺该commit可核源码及子模块、实际native构建配置/组件与资产对应证据，以及合规获取条件。详见[状态与精确最小材料](validation/p6-codec-source-review.md)/[本轮证据](test-evidence/2026-10-20-p6-codec-source-review.json)。

源码/供应链审查未通过前不install/restore/改锁/加载Skia或实现codec；无生产代码变更、dotnet/构建/测试/CI/模型/费用/发布/用户电脑操作。完整多格式/EXIF/一般缩放及打开投放仍未交付，Windows验证仍NotRun；P6-02 Blocked、PackageHold/生产图片复用Off、250600/frame/资源/来源/密码/完整性/取消/清理/隔离门保持，旧CI拒绝日志不查询不绕路。


## 2026-10-20 · P6-02隔离PNG/有限baseline JPEG到owned栅格

在a1da3de6ecfa2626540b1c63dc50a2880348b978之后，收到用户重新发布指定环境并启网络的受信新证据，核此前精确目标请求0后，对原GitHub tree/release各一次有界原路请求均200，未改代理/凭据/安全权限或换路。取得固定SkiaSharp4783f51448f9b070dda4f87b83e941c9599e466e源码及Skia固定DEPS，23正文Git blob复核；四个已批准4.153.1包与旧清单相同且nuget verify全部成功，独立正常开发准入通过。此事实接续而不改写上条历史Blocked；不等于原生可复现/完整安全审计/Windows或发布通过。

正常dev/p6-isolated-raster-decode实际实现私有WorkerPNG/有限8bit baselineJPEG到owned紧密straightBGRA：完整摘要/身份、分配前限量、顺序分块/唯一commit、EOF/实际exit0/原IO结算后交付，取消/额外输出/退出失败实际清零。Skia只在Worker/隔离测试，App与Imaging实际依赖和输出无Skia；完整原许可证/第三方声明随Worker输出保持字节。发现截断JPEG被原生修补Success的真实缺陷，保留36P1F并加有界熵流验证，同方法最终通过；旧Worker有效PNG/JPEG两条2F→同方法最终通过。新codec最终49P及原产物no-build重跑同49P不累加，原相关Core74P/Provider37P，Worker/App Release交叉0W/E，5locked restore审计成功。独立只读源码及最终回执审查无剩余P1/P2；初FrameCount误判和扫描器编译0执行失败原件均保全。

详见[组件、支持边界和本批结果](validation/isolated-raster-decode.md)/[原始证据索引](test-evidence/2026-10-20-isolated-raster-decode.json)。P6-02仍InProgress：全格式/完整JPEG/EXIF坐标变换/一般缩放白底/P6-06UI及Windows真实native/Worker/OCR/WPF和整P6仍未验收。PowerShell接入口只静态审查；250600/原资源限额/来源密码完整性取消清理隔离门/PackageHold及生产图片复用Off保持。旧CI被拒日志未请求或绕路；无CI调度/模型/费用/发布/用户电脑操作，main不改。


## 2026-10-20 · P6-02受界EXIF方向、一般面积缩小及白底合成

从clean/远端一致且独审110afa78aefaf0b49443887e2cb0b6c673084984核单worktree/唯一writer/main，正常dev/p6-image-orientation-resize接原SPEC7.3/P6-02。最小设计独审后新增Worker PrepareAsync：原JPEG baseline范围内IFD0 EXIF1–8实际方向、默认2560且可更严的完整area缩小/白底合成，64bit累计末端一次舍入；原raw接口/JSON兼容保持。ImageTransform显式原/oriented/normalized映射，encoded拒桌面锚点；record安全打印不读拒绝属性。父独立EXIF+精确job/maxedge/sourceInfo/plan在分配前校验，实际alpha/块序/摘要/唯一commit、EOF/exit0/原IO结算后交付，取消晚返/失败实际清零。PNG eXIf主动Unsupported，头segment/chunk/IFD项各有256界，不称全格式。

最终codec103P=新54+原49，原49源字节不改；新54原产物复跑同54P不累加，原相关Core74/Provider154各限定filter，Worker/App交叉Release0W/E，5locked audit restore0且所有依赖版本/锁/许可不改。真实3840×2160非恒定白边PNG→2560×1440/14,745,600byte多块及代表角/边/中心像素过，不当600MiB或600ms测量。110afa旧child9F是新选项不支持的对照，不当旧像素缺陷；同方法与Order表保持。首xUnit编译0执行及中间绿回执/源码保全。最终独立只读源码/回执审查无剩余P1/P2，测试writer执行。

详见[行为、支持边界与实际验证](validation/image-orientation-resize.md)/[原件索引](test-evidence/2026-10-20-image-orientation-resize.json)。全JPEG/其他格式、PNG EXIF、4MiB编码/可读性、生产截图一般缩小及打开投放接入、Windows真实native/Worker/OCR/WPF与完整P6仍未完成；旧截图路由保其原常量缩小限制。生产复用Off/PackageHold、250600和全部安全/资源门保持；无用户文件/真实模型/CI调度/费用/凭据安全权限/发布/用户电脑操作，main未改，旧被拒CI日志未请求或绕路。


## 2026-10-20 · P6-02受界4MiB请求PNG编码前置组件

从clean/远端一致且独审9edc5bb2fd2e84a02449ca8144319fb14d137fc0核单worktree/唯一writer/main，正常dev/p6-bounded-png-request接原SPEC7.3/P6-02。最小设计独审后新增PreparePngAsync：原隔离Worker完整EOF/exit0/IO结算后单次lossless RGB PNG，默认4MiB、可更严64KiB..4MiB，精确总字节/摘要/源元数据/Transform绑定，无缩图或质量重试，超限要求局部。实际白底、方向/面积缩小及encoded无桌面锚点保持；取消和总15s协作预算后仍等待原编码Task结算，active/owner不提前放，晚PNG及全部自有实际buffer清零，不当硬超时或框架内部清零。

独审发现共享编码器派生MemoryStream base span写入经ArrayPool副本，当前批准runtime公开IL及自有known-slice探针确认后改直接受界自有buffer写入；P2闭合。官方固定runtime源码URL一次404/ref不存在，未获得源码、未重试或换路；探针首解析器错误及一次修正回执保全。首编译0执行、首23P3F（两输入chunk超限/一异常后分配数假设）保留，产品256chunk限及全部清零断言保持；受影响11P后最终codec129P=新26+旧103。原Core限定74P/Provider限定154P，Worker/App交叉Release0W/E，五locked audit restore0；锁/旧测试/SPEC/许可字节不改，App/Imaging无Skia。最终源码及回执独立只读审查收口，测试writer执行。

详见[组件与实际验证边界](validation/bounded-png-request.md)/[原件索引](test-evidence/2026-10-20-bounded-png-request.json)。下一核心缺项为生产截图一般缩小和4MiB结果的显式桌面锚点/坐标适配接入及可读性拒绝；当前截图仍原常量图缩小限制。PNG eXIf/全格式/打开投放UI、Windows实机及全P6仍未完成。250600/来源密码完整性取消清理隔离资源门、PackageHold/生产复用Off保持，无模型/新依赖/费用/权限/CI/发布/用户电脑操作；main未改，旧被拒CI日志未请求或绕路。


## 2026-10-20 · P6-02生产截图一般面积缩小和物理坐标适配

从clean/独审/远端一致866c86a75beee13044b56fcd7377a822d7d1735e核单worktree/唯一writer/main，正常dev/p6-screenshot-area-routing接原SPEC7.3/P6-02。最小设计独审后抽取原Worker整数area+白底内核共享给可信截图BGRA，生产PrepareRequest一般非恒定图可缩到默认2560；保留native-size/精确constant fastpath和旧原尺寸CreateRequestPng拒超长边。新AreaWhiteCompositeDownscale仅是处理proof，不当可读性证明。原物理crop/负原点/实际舍入轴映射保留，Prepared owner绑定source/anchor/方向/PNG尺寸/proof；单次PNG精确4MiB或更严APIcap，不改尺寸/质量重试。原Worker实际退出及两个Coordinator来源/权限确认/绑定/预算/secret/逐次发送/取消晚返清理/复用Off不改。

原实现同一非恒定截图方法实际1F，最终通过；完整瓦片RGB、纵横非整数末边和6个单级差异用独立预期/decimal面积量化核，允许合法舍入归零。copy/目的/row/编码真实数组成功/取消/异常/严格超限清零，锁外copy可独立于原sourceDispose。最终Provider相关372P、原Codec129P且旧129源字节不改；Worker/App交叉Release0W/E、4locked audit restore0。首CS1503为测试编译0执行；351P3F是2条新测试取错保留旧选择和1条遗漏旧拒绝预期，修正测试后同3条3P，不冒称三个生产缺陷，原件保全。最终源码和回执独立只读审查无剩余P1/P2，测试writer执行。

两协议实际本地fixture证明一般截图同PNG两阶段和物理map；Unreadable只分类一次/一次secret，拒正式生成/第二secret且保留旧complete结果。此为已有可读性门对新路由的集成验证，**不是本地字号检测或真实画质验收**；本地OCR字号/冻结样本质量、PNG eXIf/全格式/打开投放UI及Windows真实截图/native/Worker/OCR/WPF仍缺，下一独立核心项为PNG eXIf。详见[接入范围和实际验证](validation/screenshot-area-routing.md)/[证据索引](test-evidence/2026-10-20-screenshot-area-routing.json)。69项基线保护/原SPEC/锁/许可及App/Imaging无Skia核对通过；250600/资源/全部安全门、PackageHold/生产复用Off保持，无真实截图/私人文件/模型/CI/费用/新凭据安全权限/发布/用户电脑操作，main未改、旧被拒日志未请求或绕路。


## 2026-10-20 · P6-02 PNG eXIf 受界IFD0方向支持

从clean/独审/远端一致0dfb16f37f65be61a1cd08502ddf62b640f7f0b4核单worktree/唯一writer/main，正常dev/p6-png-exif接原SPEC7.3/P6-02。最小设计独审后实现单eXIf/首IDAT前/裸TIFF、II/MM/IFD0唯一Orientation1–8、缺省identity；PNG结构/长度/CRC及256chunk/256项/65527byte产品子集界、64bit值偏移与完整目录保护、声明尺寸匹配原IHDR，指针/非零nextIFD循环/冲突/动画直接拒绝，不遍历嵌套链。Worker原job/hash/全部CRC和raw非identity拒绝前移到native前；JPEG原语义不变。

原功能同方法旧基线父拒绝1F；严格parser后真实child仍1F，独立native探针1P精确定位固定Linux Skia忽略有效eXIf（origin1/原3×2像素），没有假称native支持。调整设计独审后，唯一native copy直接移除已验证eXIf，其他字节不变；native精确要求TopLeft，sourceOrientation只进入既有area内核一次，原job仍绑定未删改源长度/SHA。实际copy在成功/native失败/取消/observer异常/受控原task晚返后清零，真实child额外输出/错源/exit7/完整栅格后取消均不交付。首新76P后最终Codec214P=新85+原129组，原128案例源/helpers不改，仅一旧全eXIf拒绝合同改nextIFD自环仍拒绝/Starts0；Provider传输限定37P、Worker/App交叉0W/E、4locked audit restore0。所有失败/探针/中间及最终原件保全不累计，独立最终源码/回执审查无剩余P1/P2。

详见[支持/拒绝合同和实际验证](validation/png-exif.md)/[原件索引](test-evidence/2026-10-20-png-exif.json)。本IFD0子集范围Pass，不是完整Exif/PNG或全格式/真实画质通过。完整JPEG/BMP/WebP/GIF、打开投放UI、本地字号/冻结质量与Windows截图/native/Worker/OCR/WPF仍缺；下一独立候选为受界BMP接入另行最小设计独审。71项基线保护核对、锁/许可字节及App/Imaging无Skia通过；250600/资源及来源密码权限完整性取消清理隔离门、PackageHold/生产复用Off保持，无真实图片/截图/私人文件/模型/新依赖许可安全权限/CI/费用/发布/用户电脑操作，main未改，旧被拒日志未请求或绕路。


## 2026-10-20 · P6-02原格式清单内的受界24位BMP

从clean/独审/远端一致2835f12887283b42cbecd677e5270d766286235c核单worktree/唯一writer/main，原SPEC7.1确有BMP，正常dev/p6-bounded-bmp不扩新格式范围。最小设计独审后新增BM14+INFO40/24bpp BI_RGB/planes1/无palette/offset54子集，顶向下/底向上由native统一成TopLeft，不再EXIF翻转；父和Worker在native copy前核签名/尺寸/checked64 stride与精确总长，SizeImage0或exact，其他header/depth/compression/palette/gap显式Unsupported，畸形/溢出/截尾拒绝。native分派明确Bmp，必须原尺寸/格式/TopLeft/静态帧/fullSuccess；实际alpha255由Worker raw/prepared前及父rawBMP commit双核，不靠metadata伪造。

原同方法1F→实际child PNG1P，16种宽1–4/行序/SizeImage组合独立逐BGR核非零padding不进像素，灰度一般缩小/侧界/39拒绝/原SHAjob/错source/EOF退出/取消晚返及实际copy清零过；native失败是内部观测口扰动copy前两byte的受控故障注入，未冒充普通validBMP坏图。首BMP71P后最终72+旧214=Codec286P，旧四测试文件原字节不改，Provider传输限定37P，Worker/App交叉0W/E、4locked audit restore0。78项保护全真，PNG/JPEG方向除显式Bmp分支外原字节相同；独立最终源码/回执审查无剩余P1/P2，测试writer执行，中间不累计，原件保全。

详见[本轮合同及原五格式精确支持表](validation/bounded-bmp.md)/[证据索引](test-evidence/2026-10-20-bounded-bmp.json)。当前隔离接口仅静态受界PNG/IFD0 eXIf、有限8bit单scan baseline JPEG、此24位BMP子集；WebP及GIF首帧+标注未接入，各已支持格式也不当全合法变体通过。下一候选限原清单的受界静态WebP另做设计独审，不向TIFF/HEIC/SVG/PSD/PDF无限扩范围。Windows截图/native/Worker/OCR/WPF、打开投放、本地字号/真实质量/整P6仍未完成；250600及全部安全/资源门、PackageHold/生产复用Off保持。固定依赖/锁/许可及App/Imaging无Skia保持，无新许可权限费用凭据/CI调度/真实用户图或模型/用户电脑/发布，main未改、旧被拒日志未请求或绕路。


## 2026-10-20 · P6-02原格式清单内的受界静态WebP

从clean/独审/远端一致767af3ee45a077f0a5c9216aec60f10fcc3ca73b核单worktree/唯一writer/main及原c7b3396e/64745d23祖先，正常dev/p6-static-webp接原SPEC7.1。最小设计独审后新增RIFF精确总长/checked64 chunk与zero padding、原20MiB/24MP/16384界、单静态VP8/VP8L及唯一首位最小VP8X。显式nativeWebp/原尺寸/TopLeft/静态帧/fullSuccess，真实raw alpha及原area白底/4MiB PNG保持；全部动画/EXIF/ICC/XMP/ALPH/unknown与flags拒绝，不默默忽略方向色彩。

同步修正长度后46字节VP8L删1仍nativeSuccess且像素变化；独立探针和固定Skia/libwebp普通源码各HTTP200确认demux把RIFF pad交给decoder。独审后保守拒绝所有odd image payload（包括合法odd），只接even payload精确EOF；不以0/FF双解码两点当完整性证明，不加额外raster/资源/依赖。VP8删尾零像素不变与真实损图分开；alpha hint/VP8X flag实测不改变实际alpha，无需改copy。原SHA/job/来源/权限/取消清理/EOF实际exit0及IO结算门不改。

最终Codec362P=新WebP76+旧286，旧五文件原字节不改；实际child完整非恒定画布/非灰度半透明/白底/面积缩小、43结构拒绝及合法odd拒绝/原SHA/nativecopy清零/错source/额外输出/exit7/栅格后取消通过。16383×1确定性彩色长图65532byte逐像素相同，不证明16384解码；此前encoder拒生成16384及16383规则样本仅odd的夹具失败、一次提前整组361P1F均保全，非生产缺陷。首编译0执行、基线功能1F+probe1P、首51P9F和75P1F及因果源码不抹去，中间不累计。Provider传输限定37P，Worker/App交叉0W/E，四locked audit restore0；80项保护与原许可/依赖/锁/AppImaging无Skia核对通过。独立只读源码/回执审查，测试writer实跑。

详见[产品子集与实际边界](validation/static-webp.md)/[原件索引](test-evidence/2026-10-20-static-webp.json)。原五格式中WebP仅even静态子集，odd/metadata/ALPH/动画仍缺；GIF首帧与“使用首帧”标注仍未实现，下一独立候选另行设计独审，不全格式承诺或扩新格式。打开投放UI、本地字号/真实质量、Windows截图/native/Worker/OCR/WPF及整P6仍NotRun/未完成；250600未实测通过，PackageHold/生产图片复用Off和全部安全资源门保持。无新依赖许可权限费用凭据/CI调度/私人图或真实模型/用户电脑/发布/main修改/强推；旧被拒CI日志未请求或绕路。


## 2026-10-20 · P6-02受界GIF首帧及实际P0可见标注

指定保存环境初始仅work/51d0774快照；核文件/进程/单worktree/完整HEAD/远端及祖先后fetch精确0fafe74760958b78302bbc58ae51dcdfd7d9d55e接续，唯一root writer，不恢复旧实例、不删未知锁。完整原SPEC/相关验证读后先设计独审，再在正常dev/p6-gif-first-frame实现完整GIF87/89容器、palette/GCE/每帧rect/子块/精确EOF及256帧/4096block/131072subblock界；原20MiB/24MP/16384不变。Worker仅首帧固定4096词条LZW长度/码宽/Clear/KwKwK/EOI完整性，后续仅结构检查不解码；native仅首帧精确前缀+trailer，显式frame0/prior-1、逻辑画布/偏移/透明背景及原白底area/4MiB PNG。SourceFrames仅结构计数；EOI零padding为更严子集，非全GIF承诺。

父从原源独立绑定numericGifInfo、原job/SHA，分配前核对；Frames仍1，verified owners固定Notice“使用首帧”。实际P0 --platform-review窗口新增本地GIF预览按钮/标题/TextBlock/可访问名，单帧也标注；原身份/取消/实际退出门保持，不伪造截图坐标。独审两UI P2（取消回调绑定原owner、清理未确认阻止所有新采集并保持状态）闭合，bitmap失败owner可重试。生产Shell打开/投放及模型链未接，不把此P0动作冒充P6-06或Windows实测。

最终Codec453P=新GIF91+原362，原六测试源字节不改；Provider传输37P、Core相关限定165P、Worker/App交叉0W/E，五locked audit restore0。实际16384×1、palette/interlace/透明/offset/最大256帧/普通8bit字典及3–12码宽/4096饱和/重置/跨subblock EOI、43结构拒绝/8首熵拒绝/原SHA/错metadata/EOF退出/取消清零过。原同方法基线1F→1P、83/89/91中间绿回执均保留不累计；保存SDK仅.401首次拒执行，按官方SHA512还原既有固定.402/runtime.13而不改锁。85保护全真，App/Imaging无Skia、原许可证输出匹配；独立设计/源码/回执复审ACK，测试writer执行。

详见[范围、真实结果与未验边界](validation/gif-first-frame.md)/[证据索引](test-evidence/2026-10-20-gif-first-frame.json)。真实Windows/native/Worker/WPF/OCR/画质/250600/600MiB、生产打开投放和整P6仍NotRun/未完成。WebP even-only/奇数明确拒绝、所有安全资源发送门、PackageHold/生产复用Off保持；无模型/私人图/新依赖许可权限费用凭据/CI/发布部署/用户电脑/main修改/强推，旧被拒日志未请求或绕路。


## 2026-10-20 · P6-06生产打开图片与独立文件来源

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


## 2026-10-21 · P7-01 正常安装冷启动单图预览接线

从d6b776b84a8b804e1cb5619c40c2befb9b40ab96 clean/远端0/0接续，先设计独审再实施。`--prepare-image`不再进入P0，沿原安装generation/owned child/stdio在READY后交接单个一次性IMAGE；严格Base64 JSON、launch/proof/request绑定及live记录/held leases，IMAGE/OFFER双向互斥。主浮窗排队前冻结身份stamp，关闭原dispatch登记后join，再结算原file任务；完整包验证后台执行，取消或验证失败清零原图，持续token与原admission拒迟到/替换。Off只预览，GIF首帧标注及原启用/OCR/逐次发送确认保持，warm实例仍拒绝未认证转交。

r01 Installation154P，r02完整Provider2449P（原2436全保留+13），r03交叉0W/E；独审补正Base64别名后r04 Session+Offer51P，补充r05原取消回调hold观察1P，r06最终App交叉0W/E，均0F/0skip。r05屏障早于control join，不声称缺join必红；生产精确join已静态核。各轮有重叠，不累计，r01初版源/局限保留。原807输入797项未变；产品源码最终限定ACK，reviewer只读。详见[生产差异与验证边界](validation/shell-image-preview.md)/[原件索引](test-evidence/2026-10-21-shell-image-preview.json)。

P7-01仍InProgress：本轮没有Windows实机身份/现代菜单/完整CLI-WPF-fileWorker验收，没有新树完整Core/Codec重跑，上一5e95完整2074P不转记到本树。Windows/OLE/OCR/画质/250600/600MiB/完整P6P7等原未验保持；历史失败不回填。无新依赖许可/费用/权限/凭据、用户电脑/真实模型、CI/main/强推/部署。


## 2026-10-21 · 固定e2cbe6f5完整Core/Codec签收：Core失败，Codec通过

固定父侧已审HEAD `e2cbe6f597fb628e98ee939e6ecd67a271fddbcc`、clean/远端0/0/单writer，严格各一次完整Release Core和Codec。实际 **Core2090P/1F/0skip（Fail）**，**Codec481P/0F/0skip（Pass）**，不同轮不相加。812输入源在两套前后及分析后全部相同，执行后仍固定HEAD/dirty0。正常编译的Release DLL清单各330项：Core前后45项、Codec前后8项变化分别保留，不声称二进制相同或全部执行。

唯一失败为既有InstallationApplicationControlTests.CloseRetainsTheOriginalBlockedCancellationWorkAndReaderUntilBothActuallySettle：225.530ms，Peer.Send42/Ping44→正文183写入Broken pipe；清理stdout记录实际child exit0/原IO已结算，原TRX没有Read/READY/callback阶段快照。旧测试文件未改。本轮无图片请求，不能据此认定图片补丁丢失join。候选为READY已外发但原Send await尚未完成，Close取消可令其OCE并跳过command Read；仅“取消早于注册”不够，因为已进入Read时Register会同步执行已取消token的回调。候选未证，不追认当次或历史唯一根因。未循环重跑/放宽断言或预算/改源码。

先封失败，下一最小诊断是fixture-only的原可取消Read注册后readArmed屏障，排除Admit None token，并记录READY返回/原回调/原Task状态；不改生产门。随后warm-instance最小下一步是先独审原常驻controller的受界一次性请求lease：双方真实OS连接进程句柄/创建身份与同用户会话/原完整性策略、选中安装generation及live owner/launch/租约；新连接challenge+request+有效期+UI epoch一次消费，保留原cold IMAGE one-shot，不直接放宽第二次IMAGE或浏览器角色白名单。指纹和Show只关联/通知，不授予权限；原Off、预览/OCR/逐次发送确认与清理门保持。此项仅建议，未实现新入口/权限。

[完整原件/命令/日志/TRX/逐字失败XML/812源码与DLL摘要](test-evidence/2026-10-21-p7-fixed-signoff.json)。本次后续提交只证据/报告，P7-01仍InProgress，已有实例认证转交、真实Windows现代菜单→WPF→文件Worker、安装/来源身份、OLE/OCR/画质/250600/600MiB及完整P6P7等未验不变。无新功能/依赖许可/凭据/权限/费用、用户电脑/真实模型、CI/main/强推/部署。


## 2026-10-21 · Installation取消持有夹具红门定位与修复

接固定2a4bed2a5d9c1a386368f32ce4e4ca286c95ab4d，原e2全Core2090P1F/Broken pipe原件保留。两个确定性夹具屏障证明旧期望可错：实际READY写出但Send未返/Read未入；Read已注册但后注册同token回调持有期间旧Read先Dispose，原held回调未进入。两者实际child均exit0/原IO结算，r01旧期望0P2F于Ping；不能追认历史唯一根因。

仅测试和夹具修复：原Fact先等同一次可取消inner Read创建后的Armed；normal port取消时保留原registration至本Read局部callbackStarted，再Dispose join回调返回。原474字节断言块、5s/6s/20ms/40s预算不改，body/cleanup双异常保全，生产src/250600/清理退出门不改。r02原Fact+两个明确旧期望反例3P；r03共享Installation163P（旧161全保留+2），均0F0skip，不累计。812输入中810不变；各轮执行中源不变，原日志/TRX/红源/DLL摘要保全。独立只读源码/原件ACK，尚未本修复后完整Core；将固定修复提交只跑一次，失败先分析。见[机制与边界](validation/cancellation-readiness-root-cause.md)/[直接原件](test-evidence/2026-10-21-cancellation-readiness-root-cause.json)。

P7-01/已有实例认证转交/真实Windows菜单-WPF/OLE/OCR/身份/画质/250600/600MiB/完整P6P7仍未验。PackageHold、生产复用Off、WebP odd拒绝及原安全门保持；无新功能/生产改动/依赖许可权限费用凭据/模型用户电脑/CI/main强推部署。


## 2026-10-21 · 固定9877e5e3一次完整Core签收通过

完成取消夹具最终源码与定向原件独审后，在clean固定 `9877e5e3bcc9750bb0faf60ad040e9af7fca07f0` 严格一次完整Release Core，实际 **2093P/0F/0skip**、exit0，UTC04:28:10启动、总耗时94.890s。旧2091案例全名多重集均保留，新增两个明确旧期望反例；原持有取消Fact也通过。r01旧期望受控0P2F、r02定向3P、r03 Installation163P各自保留，不累计；旧e2完整2090P1F不回填，历史唯一根因仍未知。

812输入源前后相同、执行后HEAD不变/dirty0。Release DLL前后各330项，正常编译45项变化，完整前后摘要/差异表、绑定固定SHA的AssemblyInfo、命令/日志/TRX和准确源码保全。本次追加仅证据与报告，生产源码/测试/SDK/锁均不再变化；无必要重复Codec/Provider/App。见[签收证据与原件](test-evidence/2026-10-21-cancellation-fixed-signoff.json)及[定位合同](validation/cancellation-readiness-root-cause.md)。

未接续warm转交功能；指纹/Show不构成授权。P7-01、已有实例认证、真实Windows菜单-WPF-fileWorker/来源身份/OLE/COM/OCR/画质/250600/600MiB及完整P6P7/106/Beta未验保持。无新依赖许可权限费用凭据/模型用户电脑/CI/main强推部署；DefaultOff、PackageHold/生产复用Off、WebP odd拒绝与原安全门不变。


## 2026-10-21 · P7-01已有实例认证转交最小设计（未实施）

父侧关闭2093P Core红门后，固定71f69225accb8978160f59481414621ea312c466 clean/远端0/0、唯一writer接续，仅形成[可审最小设计](design/p7-warm-image-admission.md)。外部请求仅到原normal controller，真实pipe双向held process/token/logon/session/IL/选中generation校验，权限根来自原Role/Start child/stdio/admission/execution的内部live capability；Show/指纹/锁冲突不能授权。单slot零队列，server request/ordinal/双方nonce/10秒grant/UI stamp一次消费，独立warm CTS与原受控通道，不重用cold IMAGE第二次。

明确唯一stdio reader/所有writer短串行、warm不等会话总serial锁；原任务以有界registry持有到settled，取消/断线与AppOwned在同UI提交wrapper仲裁，ACK丢失Unknown不重放，清理未确认不放slot/owner。prepare保留原Worker15秒，grant与清理不混同。拟先A纯Core/真实合成child屏障，再B Windows双向adapter与原Shell/file链，A不启用synthetic生产身份。列明必要生产修改与错误peer/旧generation/重放/过期/断线/held callback与Worker/STOP等定向测试，均尚未执行。

本轮只读核812输入与基线git字节/current完全一致，并查Microsoft官方API；未改src/tests/工具/SPEC/SDK/依赖锁，不重跑产品测试，不把旧2093P转记新功能。真实Windows身份/菜单-WPF/画质/250600及完整P7仍未验，Off/不自动OCR发送/原来源清理门保持，无费用权限凭据/用户电脑/模型/main强推部署。设计静态证据见[索引](test-evidence/2026-10-21-warm-image-design-review.json)。

设计候选经最终只读复审限定ACK：早期WARM_QUIESCE明确各端独立join避免关闭等待环，wrapper外最后有界记录复核/锁内volatile current及间接IO实施审查点明确。仅DesignReviewed候选交父侧独审，不是实施/测试/Windows验收ACK。


## 2026-10-21 · P7-01 A片 Core 与原 stdio（内部、未启用）

从已审96a0b3b设计实现内部单slot/零队列/一次请求、高水位与严格受界消息、真实TimeProvider10秒grant及15秒prepare，原取消/任务/timer/注册/finalize/write-flush实际结算后才退休。唯一reader demux与所有writer短串行，取消先撤销不等writer；warm不重用cold IMAGE/OFFER，不自动重放或fallback。仅合成fixture AttachWarm，App无外部入口或Windows/UI接线，默认Off不自动OCR/发送。

两处独审P2的确定性旧逻辑反例r05为1P3F；修复后同组加父write/flush共r06 6P。完整Installation r07实际200P/0F/0skip，旧163案例全部保留+37，821输入/准确源码执行中不变；App r08交叉构建0W/0E。不同轮次不累计，r01 16P1F、r02编译失败未执行、r03 21P12F、r04 10P2F原件/可见失败均保留，被finally掩盖的原异常不追认唯一根因。独立只读final source/红绿/r07原件限定ACK；将固定实现提交再做一次完整Core，尚不预测结果。

[实现合同与边界](validation/warm-image-core-a.md)、[完整命令/TRX/源码/DLL摘要及原件ZIP](test-evidence/2026-10-21-warm-image-core-a.json)。B的真实双向peer/live capability/安装身份/Accept调用链、WarmSync→Gate和异常candidate所有权仍未实施，必须父侧独审A后才进入。P7-01/完整warm、真实Windows/OLE/COM/WPF/OCR/画质/250600/600MiB/完整P6P7/106/Beta未验不变；PackageHold、生产复用Off、WebP odd拒绝保持，无新许可权限费用凭据/用户电脑/真实模型/CI/main强推部署。


## 2026-10-21 · 固定15d65bc一次完整Core Fail，原件与机制保全

固定 clean `15d65bca30ba32386c895a6cc2fd4e218a51196a` 仅一次完整Release Core：实际2129P/1F/0skip、exit1、114.964s；原2093全保留+37，Installation200均通过，821输入及HEAD/dirty前后不变。唯一SelectionReuseHostTests crash原case期望Crash、实际HardTimeout、624.2773ms；原TRX无阶段trace，不能据总耗时倒推阶段或唯一根因，也不能因相关源未改排除间接调度影响。

不重跑全套、不改旧测试或600ms。独立artifacts探针同原Host/DLL：同一实际inner read获EOF、child61.613ms真实exit23且Run未结束，持有原读取返回越600ms，Summary空/owner保持；释放后原marker全清、HardTimeout+NaturalNonZero、实际exit确认后释放owner。exit0，探针两条编译警告如实保留。独审限定ACK仅证明机制可发生，不回填历史唯一根因，不把Core Fail转绿。A片实现和相关200P已交，整体签收尚未完成；本轮只追加证据，下一步需先审定可控截止/夹具最小方案，默认真实250/600、原watchdog/退出门不动，暂不B。

[准确失败/完整日志TRX/固定源码产物摘要/探针原件](test-evidence/2026-10-21-warm-image-core-a-full-core.json)、[完整边界说明](validation/warm-image-core-a.md)。P7-01/完整warm/Windows身份菜单WPF/真实Worker接线与画质250600等仍未验，不改main/权限费用凭据/模型用户电脑/依赖协议/CI强推部署。


## 2026-10-21 · 退出原异常保全与诊断故障夹具修复

父侧确认warm错被finally CancelAsync覆盖P2：r10双屏障及stop/image双错0P3F。现分别join原warm/cancel/image/loop，递归原leaf按reference去重，单错EDI/多错Aggregate，不EXIT_READY/放slot。保留旧loop-only协议错允许Shell关闭但controllerUnconfirmed/owner fence不放行的原断言，并存清理错则全部原错一起保全。

诊断Host仅内部可控provider统一origin/模型/绝对Check/timer；默认真实600ms/原watchdog与lateIO/start/exit、idle120/清理2s/生产250600不动。7故障语义仍精确原reason，200字节断言未改；599/600未投timer及600投timer屏障、同原IO清零、native拒绝覆盖。r12 15P，r13相关255P/0F/0skip，旧245全保留+10，各轮不累计，823输入执行中不变。待固定实现提交一次完整Core，不预判；旧r09原Fail/历史唯一根因未知不回填。A片仍核心合成，B/真实Accept/WarmSync→Gate/candidate所有权/Windows/P7未验。

[实现合同](validation/exit-errors-and-controlled-deadline.md)、[完整命令TRX/源码产物摘要/原件ZIP](test-evidence/2026-10-21-exit-errors-deadline.json)。无新功能许可费用权限凭据/真实模型用户电脑/CI/main强推部署。


## 2026-10-21 · 固定0dbfa74一次完整Core2140P签收

完成退出原异常保全P2/诊断统一可控截止的源码与255P相关原件独审后，在clean `0dbfa74a5075e750d58efff9def5d0f86b645243` 严格一次完整Core：r14实际2140P/0F/0skip、exit0、91.922s。旧2130案例多重集均保留+10；故障语义7项、默认真实watchdog/迟到IO/退出及错误保全反例均通过，原200字节故障断言保留。823执行输入不变，前后HEAD相同/dirty0，330产物前后摘要/45差异、AssemblyInfo、命令/完整日志/TRX/源码封存。旧r09 2129P1F原Fail与历史阶段/唯一根因未知记录不回填，不累计不同轮次。

[固定完整Core原件](test-evidence/2026-10-21-exit-errors-deadline-full-core.json)、[修复合同与边界](validation/exit-errors-and-controlled-deadline.md)。后续仅证据报告，不改固定代码/测试、不重复整套；未重跑App/Codec/Provider（相关源/API未改）。本片Core签收通过，A仍核心合成；B/真实双向Windows身份/AcceptFileImage、WarmSync→Gate及candidate所有权/真实Worker-WPF/菜单来源身份/OLE/COM/OCR/画质/生产250600/600MiB/完整P6P7/106/Beta未验仍保持。Off/PackageHold/生产复用Off/WebP odd拒绝不变，无新许可费用权限凭据/真实模型用户电脑/CI/main强推部署。

## 2026-10-21 · P7-01 B正常会话单图接线与固定树签收

在74aed500指定环境唯一writer接续：真实双向Windows peer与normal original-role live capability、受界单次外部连接→原stdio→原文件Worker→AcceptFileImage warm纯内存提交→实际版本GUID/可见预览结果已接线，Off不自动OCR/启用/发送；冷one-shot、拒绝/丢收据不重放/fallback和原任务/清理/发送门保留。[实现与准确边界](validation/warm-image-b.md)。真实Windows尚未执行，P7-01仍InProgress。

独审指出并修复exact launch绑定、普通连接错误污染会话、WPF重入与旧位图擦除失败owner丢失。b03原33P8F逐字保留，b05同范围42P0F含原位图断言；b08 Core相关53P0F，b09 Provider相关142P0F；不同轮次不能累计。最终兼容修复：精确QUIESCE无授权消费并禁止迟到AttachWarm，idle actor仅合法handoff封闭，避免对关闭通道补写；b11原137P1F为新夹具在Dispose后查询已释放Process，改原Settle退出实录/Healthy屏障后做最小定向验证。b12精确两条兼容回归2P0F，完整最小B树独立源码限定ACK，无剩余确定P1/P2。随后在clean固定实现92e3548845bd07ee7717b53df9588c412d128f9d各执行一次完整Core2164P0F0skip、Provider2463P0F0skip、Codec481P0F0skip，最终App交叉构建0W/E。四轮836输入=current=git92e、前后HEAD同/dirty0；旧Core2140+24、Provider2449+14、Codec481全部保留。各330 DLL摘要正常变化45/10/8/13，不累计或泛称全执行；当轮目标AssemblyInfo核92e，额外generated可含未构建旧产物。原件与源码独立只读限定ACK，不借记旧通过。[完整原件索引](test-evidence/2026-10-21-warm-image-b.json)，Library已确认libfile_11ab83f8701c8191bca25470a98977e9（7577354字节/version0）。

默认Off/PackageHold/生产复用Off、WebP odd拒绝保持；无新依赖许可、SDK协议、权限凭据费用、真实模型、用户电脑、CI/main/强推/部署。真实Windows peer/ACL/菜单到同WPF/Worker、来源身份/OLE/COM/OCR/画质/250600/600MiB/完整P6P7未验。端点初始化/bind故障仍保守保留原fence，不冒充已实现可选降级恢复。


## 2026-10-22：P7 浏览器原图五格式接入（独立产品缺口）

从b82c377fba33f72b307cb4cc18598425cf85bc89继续，单writer/开发分支不动main。普通权限测试路径调查未建立可用入口：已知Windows托管runner为High，runner清单只读403，不重试/改权限；继续原SPEC实际未实现项。浏览器由PNG扩展至既有受界JPEG/BMP/静态WebP/GIF，双端Commit MIME↔sniff/rawcanvas，原Worker/Browser owner/EXIF/首帧notice与取消清理门；不增加权限/依赖/模型调用。独审设计及源码限定ACK，详见[实现、真实验证和边界](validation/browser-original-formats.md)。

本片最终Node104P、Core接收定向141P、完整Codec488P、Provider浏览器准备定向10P，各0F/0skip；正常App Release0W0E，轮次不相加。中间4F反例、Core新测试误判5F与两次Provider编译缺链接原件保留。未借记完整Core/Provider、真实浏览器或WPF验收。WebP奇数仍拒、PackageHold/生产复用Off；Windows medium/warm/Explorer、来源/完整性/250600ms及画质仍未验，P7整体InProgress。原件与最终摘要见[证据索引](test-evidence/2026-10-22-browser-original-formats.json)。


## 2026-10-22：冻结浏览器原图产品的完整Core/Provider离线签收

产品固定b554541f6cbcc8dad7f37c81a88c7fe56aeb1b6d，本轮未改产品或测试。顺序一次完整Core2182P/0F/0skip、一次完整Provider2464P/0F/0skip，均无filter/exit0；839输入前后同字节，21/22个DLL摘要保全，完整TRX与逐项结果一致。独立只读审查限定ACK。前141/10定向与旧Node/Codec轮不相加，未重复Codec、访问已拒API、新CI或模型。详见[固定产品签收](validation/browser-original-formats-full-signoff.md)与[证据索引](test-evidence/2026-10-22-browser-full-signoff.json)。证据only正常dev提交；真实Windows/Chrome/Edge/medium/250600ms/画质未验及P6/P7边界保持。


## 2026-10-22：P7受限data原始字节路径

从1de5fea42d9d16a165d9373417b24bdd54f3c060继续，补SPEC13.2 imageContext接受data但original必拒的真实缺口。仅五MIME明确canonical base64，编码/预计大小预限、两遍64KiB可取消解码、唯一输出异常清零、原MIME/Ready/来源/分片/双端Commit/Off发送门。原C#/manifest/协议/权限不改，blob/跨域/可见裁切不扩。独立设计、源码、原件限定ACK。

最终Node154P/0F/0取消/0跳过（842输入0漂移），含真实合成ServiceWorker/stdio/生产Receiver/Inbox九正例七负例及字节边界；首轮151P1F因新comment夹具仍为87a，最小87/89真实对照保全，仅修夹具不改生产门。原5F、target47P/边界3P不累计，未重复Core/Provider/Codec/CI或模型。详见[实现/原件/未验边界](validation/browser-data-originals.md)及[证据索引](test-evidence/2026-10-22-data-original.json)。Windows/Chrome/Edge/medium/250600/画质仍未验，P7整体不标Pass。


## 2026-10-22：单张 CF_DIB 位图投放最小切片

补 SPEC6.1 的真实位图缺口，独立默认关闭选项；仅40字节头/24bit BI_RGB/HGLOBAL及转换BMP各≤1MiB。隔离STA有界复制/行padding清零、typed IPC、同peer实际退出后Worker、共享图片槽和明确Bitmap预览来源，Off不自动OCR/启用/发送。独立设计及源码限定ACK。错误保全P2以r06旧helper0P1F准确复现，修复同时保留transport/cleanup原对象；r05编译失败不计红。r07 Core41P/Provider8P/Codec5P，850输入无漂移，轮次不累计。固定树完整签收随后归档，不预借旧通过。详见[位图切片实现与边界](validation/bitmap-drop.md)。真实Windows/OLE/WPF/来源/medium/250600/画质仍未验，P6/P7不标完整通过；安全与费用边界保持。


位图固定产品a4305e完整Core r08保全2222P1F（Cleanup:73），停止全套。确定性同Cleanup held-read反例r09/r10各0P1F，后者body/cleanup双栈保全；仅测试helper补原LateReadObserver的既有4秒join，生产src不改。r11相关75P0F0skip，独审后固定新测试树一次完整签收；不追认历史唯一根因。r07与a4305e为849输入同、README后续说明单差，前后850无漂移仍成立。


最终固定测试树72f61076f1701f6e35314c2be7b3dcfbe5cf7493 的r12完整Core2224P/Provider2469P/Codec493P，各0F0skip/exit0；App/OleReceiver/Worker三交叉构建各0W/E。六轮850输入=current=Git、HEAD前后相同/dirty0，目标AssemblyInfo同SHA；独立原件限定ACK，旧红门不回填、轮次不相加。功能源码仍a4305e，后续仅测试helper及报告。原件Library确认libfile_dc94a6dcf4a88191aa03af1cc3083a4d/v0（2914516B/168成员，SHA256 2a777e6bb523d4aee296a628976e6c9ac811480ec761fd6a635785a958b1430d）。Windows/OLE/WPF/真实来源/medium/250600/画质仍未验。


## 2026-10-22：Bitmap 本地 OCR 明确来源闭环

从6f6f348单writer接续，补既有Bitmap显式本地OCR入口；Core互斥来源枚举贯通Approve文字来源、主结果和版本菜单，未知值及假首帧拒绝。复用OwnedFileImage无desktop/proof映射、原On/语言/尺寸/源版本/Worker结算/预览与独立发送门，Off不自动OCR发送；DIB/OLE/IPC/依赖权限不扩。独立设计、源码及原件限定ACK。r01 Provider编译别名错误不计测试，r01 Codec4P、r02 Provider196P（851输入无漂移），轮次不相加；随后固定完整签收。详见[接线与未验边界](validation/bitmap-ocr.md)。Windows OCR/WPF/身份/画质250600仍未验，P6/P7不整体标Pass。


Bitmap OCR产品02fa638固定r03完整Core2223P1F，保全原249.999→HardTimeout而不继续其他完整签收。仅测试分离wire语义与真实600ms截止，同一受控clock/原CTS加恰600Startup屏障；r04相关61P2F继续定位原protocol语义计时混用，r05相关63P0F。正文/原任务清理错误均保全，生产src与250/600ms、原默认真实watchdog测试不改；后续冻结新测试树完整签收，不回填历史唯一根因。


r06最终正文/原任务清理保全相关63P0F；独审发现新Startup回调超时可能使child未返回owner，仅取消该回调内部超时，保留外5秒guard/finally release与原任务join；r07单屏障1P0F。生产源仍02fa638不变，旧失败不改绿，冻结测试树后一次完整签收。


r08冻结7260f66完整Core2225P0F、Provider2492P1F，851输入Git一致；失败后停余项。r09受控peer观察hold复现旧late释放断言0P1F，准确显示Reply未交给生产，原r08具体断言/唯一路径未知。仅Provider夹具加DecodedEntered屏障再Clear，保留原断言并保全正文/cleanup双错；r10相关63P0F，生产及Core测试不变。下一轮仅完整Provider和首次Codec/三构建，按套固定SHA分别记录，不重复已绿Core。


独审补夹具原Clear清理P2：保存同Task<bool>并在finally join，已有Clear不再用新调用替代；r11 Bitmap准备4P0F，保留r10及旧红原件。最终测试仅该Provider文件变化，Core及生产src不变，分别按固定HEAD签收。


最终r08完整Core2225P0F0skip固定7260f663e0269d4e5900872797b6874f7401e0a0；r12完整Provider2493P、Codec494P及App/OleReceiver/Worker各0W/E固定231325dcb73de1b664efe9f12537534571ee7ad2，全部exit0/输入851前后=各固定Git。两树仅Provider夹具不同，生产/Core不变，未重复Core，不相加轮次。339DLL是库存而非执行计数。短暂exec断连原环境已恢复且实际命令完成，未建新writer。产品02fa638不变，等待最终原件独审/Library确认后正常开发分支推送。


最终原件/报告独立限定ACK；Library确认libfile_6211a62342788191bc0625fb35215224/v0，ZIP3628492B/157成员/SHA256 5cc8fa4a96970ee34b5b88b8ebebc399290074c571a51009bf983136f91d8b33，逐成员/CRC与上传后xattr/hash确认。只证据报告3文件后续提交，未重跑旧绿或改产品。真实Windows/medium/OLE/WPF/原生OCR效果/画质250600仍未验，不称P6/P7完成。


## 2026-10-22：受界 SOF2 渐进 JPEG 实质兼容切片

从ccc5d9c原环境单writer继续SPEC7.1：新独立progressive validator/完整refinement与熵终止，复用原Worker/EXIF/来源/发送门，旧baseline字节及长前缀接受合同保留，不碰WebPodd或生产复用Off。独立设计/源码限定ACK。r02链接编译错无用例，r03旧Worker真非零4F，r04新5P，r05相关37P，r06相关50P，r07长前缀兼容3P，各轮不相加；定向冻结签收随后单记，不重跑完整Core/Provider/Codec。详见[产品子集、实际证据与未验边界](validation/progressive-jpeg.md)。真实Windows/medium/WPF/OCR效果/画质250600仍未验，不称P6/P7完成。


冻结产品/测试30b010f181a22b64b5e3288963c1953dff1f0268的r09四类Codec175P0F0skip/exit0，App/Worker各0W/E；858输入前后=Git、4目标版本同SHA，339DLL为库存非用例数。原选择124例（原伪渐进仅准确改名/期望）+新增51例；未重跑完整Core/Provider/Codec，轮次不累计。r08仅私有manifest执行器中文路径预SDK错误，0执行，原件保留。最后独审/证据归档与正常分支推送接续，未验边界保持。


原件/三报告与99成员ZIP逐项独立限定ACK；Library确认libfile_643983e0b1f4819199b88f4dd44b9cdb/v0，590992B，SHA256 c6eeef6ab674dd30137fdf7a008381e3d2d9a06ff8d959e86aa95db378984a1e，上传后xattr/hash不变。旧binary本体仅本地保全、摘要入包；后续仅这3份报告提交，不改固定产品/测试，不借记Windows或照片画质通过。


## 2026-10-22：SPEC7.4 智能图片联动本地 OCR

原环境单writer从5f0a0c3继续正常主路径：明确语言→原Worker结算→本地预览批准→独立发送确认；协调器强制Automatic一次批准，text_only+clear正式仅批准文字，同vision端点/风格；其他图文原图、非法分类停止，旧OCR-only独立文字不变。3项独审P2以r03同8例全红→r04全绿闭合；r05 Smart37P。r06相关轮7个stdout失败后自有执行取消，完整计数Unknown/无TRX/exit1，不用runner错误0字段；旧视觉夹具改显式模式保原断言、挂起仅有Gate同步桥候选不追认唯一根因。r07/08新夹具构造编译失败0执行，r09对应与来源18P。轮次不相加，冻结后只跑必要相关选择与App/Worker，不重刷完整套件。详见[实现、原件与边界](validation/smart-image-ocr.md)。生产复用Off/PackageHold/来源清理发送门保持，Windows/真实OCR质量/250600仍未验，不称P6/P7完成。


冻结产品/测试bef61b5f2935a4d44851b461c48e844c13f75250的r10相关12类Provider458P0F0skip/exit0，原418全保留+新40；App/Worker各0W/E。864输入前后=Git、4目标版本同SHA，339DLL仅库存。未跑完整Core/Provider/Codec，轮次不累加；随后只证据归档，不扩本片。Windows/原生OCR效果/250600/整体P6P7仍未验。


最终原件/三报告及337成员ZIP独立限定ACK；Library确认libfile_0ed7d88d3e08819182e4962783ea2391/v0，2225210B/SHA256 5013147b489f670ec8e23fbae64c1df2f6abce985ae44c508cbaf7b499f31000，上传后xattr/hash核验。后续只这3报告提交，产品/测试仍bef61b5，真实Windows/OCR质量性能与整体阶段未验保持。

## 2026-10-22 智能图片显式视觉继续

核实 SPEC7.4/7.5 主路径缺口：原 Automatic 在无语言/空 OCR 时分类前结束。按独审最小设计实现当前良性 OCR outcome→独立“使用视觉继续”→原 BYOK 发送确认；共用一次性 SmartImageApproval/原任务结算，视觉 final 即使 text_onlyclear 也保留原图，不伪造 OCR 文字批准。身份/协议/取消/超时/清理失败仍拒绝，不自动 fallback。r01 新视觉39P、r02 App0W/E、r03相关399P、r04真实pending屏障1P，各865输入无漂移；均dirty候选，不相加。冻结后相关范围及 SHA 见 [本片接线与证据](validation/smart-image-vision.md)。未做安装器或扩大权限依赖，Windows实际OCR/WPF/medium/画质性能仍未验。

固定产品/测试 `e00d8e15dbc1daf7a887bf403939728eb38dbc07` 的一次相关12类签收501P0F0skip（原458全部保留+43），正常App/Worker各0W/E；865输入前后=Git、4目标完整SHA一致，339DLL仅库存。r05清理三分支3P；不累计轮次，不代签完整Core/Provider/Codec或真实Windows。随后仅归档三报告和原件。

固定轮原件独审限定ACK：501条全Passed、旧458全保留、源码与产物摘要吻合；无新增确定P1/P2。

原件Library确认 `libfile_4e21f75a874c8191a1025bf286ddd1f9/v0`；177成员/1,751,547B，ZIP SHA256 eeea23f06790a8add475c63f1d5f4fc899aee9cfeced3c5f527e992e53480110，上传后xattr/摘要复核成功。产品/测试仍e00d8e15，仅这三报告补最终回执。

ZIP CRC/全部176项manifest及三报告独审最终限定ACK，未发现事实错误，未扩大验收。

## 2026-10-22 功能线：图片分类独立30秒截止

与父级独立视觉线分工，主线仅改ProductionScreenshotCoordinator和ProviderRequestBudget业务，不改主题/窗口/通用展示。核SPEC10.7真实缺口，独审设计后实现同clock子预算、保原profile身份及总预算，覆盖凭据/HTTP/队列/分类提交；超时不发正式生成、不自动fallback，生成不继承已完成分类的30秒。r01同源码反例0P4F(HTTP期望0实际1)→r02 6P；r03 14P2F原件保留，仅修新持续片段消费屏障r04 2P，轮次不相加。独审限定ACK后冻结并做必要相关签收，见[实际路径和原件](validation/classification-deadline.md)。Windows实机/像素/性能未代签，安装器未实施。

冻结07dcfe0dab4a117a0c039c0beefa03f4bf4767c7后，一次相关17类665P0F0skip（原501全保留+本片16+既有额外回归148），正常App0W/E；867输入前后=Git、3相关目标完整SHA一致，339DLL只库存。未跑完整Core/Provider/Codec或未变Worker。只接续原件独审/三报告与正常开发分支推送，不触视觉文件。

固定原件独审限定ACK，501+16+148拆分及输入/产物一致；无新增确定问题，未扩大为完整套件或Windows验收。

原件/三报告及ZIP独审最终限定ACK；Library确认 `libfile_1973521c2fa48191ba6a6bc661a159ca/v0`，117成员/1,291,053B，SHA256 f786fdf2ec541974b5d298c6edb461640f02e1fbfead7db84437b4fdba625108。保存后本地元数据/摘要复核成功；随后仅这3报告提交，产品/测试仍07dcfe0不变。

## 2026-10-22 分类按时提交的截止边界修复

父级独审发现确定P2：canonical29.999秒已提交分类，caller30.001秒才恢复，phase.Check误将其改为超时。暂停并暂存UI改动，最小修复只把phase最终检查放到canonical commit内；提交后保留原总预算，仍拒绝迟到提交和来源失效。r02同测试源码四例0P4F→r03相关24P，r04成功commit后Off/总预算到期4P；r01漏using编译失败零用例保留，不累计。设计/差异只读限定ACK，冻结后做必要相关签收，见[完整边界与原件](validation/classification-commit-boundary.md)。没有放宽时间或发送门，Windows未验保持。

固定94ffa5e的相关整类165P0F0skip（原153完整保留+新12），App0W/E；868输入=Git、三目标完整SHA一致，独审限定ACK。原件Library `libfile_b0c365efc690819195b2c183284e1ff5/v0`，719619B/92成员，SHA256 72e582976c505b88d3401d3e607d3ae2bb72b7919d28dd5a5c23f1aecbaad15e。零完整套件/Windows代签，随后仅3报告归档并恢复隔离UI工作。

## 2026-10-22 桌面界面精修接入

独立视觉任务failed且零写入，本环境唯一写入者完成分类P2独立提交后接手真实C#界面：设置导航/标题/表单卡片/保存区、浅深主题与枚举标签、浮窗矢量图标。原控件/事件/Content/Tag/几何与全部安全门保留；动态高对比glyph独审P2已纯外观修复，未调用业务Refresh。r03最终候选App0W/E、r04相关259P（新增Palette2+既有257），872输入无漂移，不计WPF运行。独审限定ACK，详见[接线、证据及真实边界](validation/desktop-polish.md)。离线HTML设计对照的Chromium截图被现有sandbox helper配置阻断；未关sandbox/改权限，真实像素/焦点/高对比/DPI/Windows均未验。不重复后端完整套件，不增加安装/权限/费用。

UI源码b03541c468180e84de33731dafba7b7fd78c50e4已核872测试输入逐Git一致；原candidate b473 dirty执行/产物版本如实分列，不无变化重复测试。独审核259全Passed、原257+新2，13真类；一处参数化testName按小数点误分类摘要已改为TRX testId→className，原错误摘要保留，无产品重测。原件Library libfile_7811cbea5c5881919759c41e1c92b8ed/v1，649806B/94成员，SHA256 af3ce2ba145fb6137e3bb30c602af45fc66c30f9bf5c28039967add25b295b38；返回元数据/摘要复核。截图环境失败/原生Windows未验保持，随后仅三报告推送。

## 2026-10-22 更正设置全局发送提示

父级独审P2：全局“按次确认”误称普通选区均需额外确认。仅更正SettingsChrome同TextBlock为“默认 Off（未启用）／启用后可自动发送选区／保存设置本身不发送内容”，业务与来源/隐私/清理门完全不改。其余345个src文件逐Git与84116e一致；定向文字/单字面量差异核验Pass，正常App候选交叉构建0W/0E。独立只读限定ACK，无新增/执行测试用例或全套重刷。原UI归档旧文案是历史快照、以本片纠正为准；真实WPF像素/DPI/焦点/高对比仍未验。见[文案更正及原日志](validation/desktop-polish.md)。

## 2026-10-22 安装链宿主注册与精确撤销内核

继续SPEC13.3/13.4，复用原版本完整性/role admission与集成metadata校验，新增固定两HKCU物理槽的内部注册/归属撤销事务和Windows适配器源码，不接外部入口、不执行注册。未知不领养、原receipt/selected/pending逐写复核、提交点前补偿/后失败保留、原句柄原Task关闭未确认保持pending与admission，原准入门新增registration pending拒绝。独审四项P2已确定红r03 4F/r05 2F并修；r06新事务35P/875输入无漂移，r01/r04编译失败零测试保留，不累计。当前浏览器直启NativeHost未纳管，全静止未证；自启仍false、真实安装/卸载/Windows未验，不把此纯协议片当可运行安装器。见[接线、原件与边界](validation/host-registration-transaction.md)。

固定b3d6e3057190605b7928a0ff4461cdfad3266fd1后，r07相关360P0F0skip（14类、新35+既有325，非完整Core），r08App0W/E；875输入=Git，四目标完整版本一致，其余344原src/旧测试及锁/工作流等未改。原件/固定源码独审最终限定ACK；Library libfile_ca561ccb56d481918c02db7700b78747/v0，1094683B/146成员，SHA256 1127c21cf40e27ddc38e108ba6134aea35b6a08d5a137e31d0589abc01213ec8，CRC/145manifest通过。随后只三报告推送。下一接线前须闭合浏览器直启NativeHost入场/静止门，不能收养未知进程；内部事务不代签真正安装、注册、自启或Windows验收。

## 2026-10-22 浏览器直启NativeHost入场与静止门

接续SPEC13.4/P7-04/05：正常受控App READY后发布exact同代session；浏览器Host在原信任校验后、BrowserSession前加入共享活动门，并保留至自身OS实际退出。真实变更持有独占门，role-only不能冒用；不收养/杀未知Host。取消/退役/旧session sticky失效、原App IO与退出结算、未知记录保留；旧owned目录缺门拒绝自动迁移，原Off/源/发送门和250/600不变。独审Submit等待后仍传图P2已用r05两屏障确定红后修持续guard；r07/r08新17P，编译与fixture误期望/已Dispose查询失败原件均保留且不累计。见[实际接线与边界](validation/browser-host-lifecycle.md)。未接真实注册/安装入口，Windows/浏览器桌面与全P7未代签；下一步固定树必要相关回归及独审。

第一次固定55959b3相关545例为514P31F，未签收；逐原件定位27新store手造夹具缺门、3旧注入早于新publication及1退役setup争用。r10确定红：setup与原cleanup IO屏障下取消后新Host仍ENTERED。修复为原launch closing先撤销、原role lease下完整prepared刷盘关闭后不覆盖Move发布/精确退役，写/关闭多错保留，首次已有closing不领养；通用owner/变更门不放宽，foreign owner及失败持久fence保留。r11定向62P，3旧注入仅改同步时点、原断言保持；原件与设计限定独审后再冻结必要签收，不循环刷绿。

修复冻结722270d46cd5ea6bcb7d0375ab652854b85506ae后，r12同范围一次549P0F0skip（19真类、本片21+原528，r09原545结果/544唯一显示名称全保留（两长origin显示截断重名）），r13正常App0W/E。880输入两轮前后逐Git一致，四目标完整SHA及摘要匹配，340/348原src不变；原3旧测试只补准确同步/新fixture门，正文断言保留。无完整套件或Windows代签，所有失败原件留存。只接续独审/原件保存和三报告正常分支推送。

最终原件独审限定ACK，549条、880输入、四目标及ZIP逐项一致；Library确认libfile_919bb999dad48191be09ed3585c278ef版本0，2261208B/308成员/307manifest，SHA256390f56f1e648ff5144eee6eb66dcdb3a91570e597938ae443e2412e293a6b3c2。原ZIP/产品不改，只三报告追加回执正常推送；真实Windows/安装/完整P7边界保留。


## 2026-10-22 完整角色源包的路径派生物化

SPEC13.3/13.4继续真实Apply缺口：v2固定source原SHA与完整角色/声明依赖，从随机generation实际生成manifest/trust/provenance和独立v3安装摘要；真实NativeHost共享配置校验及内部注册事务要求物化资格，generic v1不获资格。旧35注册fixture改由source真实Apply建立，不手造installed目录。r05身份4反例确定红→r06相关65P、r07边界16P，轮次不累计；编译失败和所有原件保留。设计/源码/原件独审限定ACK，冻结后做一次相关签收，见[实际物化与边界](validation/payload-materialization.md)。本环境实际managed产物加明确structural runtime/native/第三方fixture，不是可运行Windows包；三项原生DLL/完整自包含runtime输入、真实身份/注册/安装/Windows与250600仍未验。不扩权限许可/工具/费用，不触用户电脑或403 runner。

第一次冻结1c2225的r09相关585结果584P1F，唯一旧role信任集合计数23→47不符；原件保留，不签收。独审确认仅需测试合同迁移，新增一个独立完整47项集合/逐项拒绝检查，不改生产、不降断言；定向后再固定一次必要相关签收。r08六正常构建0W/E、883输入稳定/锁无漂移。

固定4b502f0a5aa75d4b6e66c1b80f20cc115e4680ad的r12一次相关586P0F0skip（20类、旧549+物化36+合同1），r11六个正常构建0W/E覆盖七角色含App；883输入逐Git一致，14自身managed完整SHA/DLL摘要吻合。旧结果按类/方法/显示名称重数完整保留，4个enum参数ID序数变化的audit错误已保全/更正，无产品重跑。公开样本source104→installed108实际Apply生成，仅metadata与摘要保全，structural第三方/runtime/native仍非可运行包；未代签完整套件/Windows或安装。

最终原件独审限定ACK，586结果/旧549重数/883输入/14目标及两份真实Apply样本自洽；未重跑产品，不代签Windows可运行包。仅接续归档和三报告正常分支推送。

原件ZIP/244manifest独审限定ACK；Library确认libfile_a42832a798ec8191a459bc114df913aa版本0，2,374,653B/245成员，SHA256 ac92e4ec753f82860acb950fa3ba66a4cd3a4ba1fccc092ef914858e050619e6。保存后身份/摘要核验成功，随后只三报告补回执正常推送；4b502f0产品/测试不改，真实Windows与完整可运行安装包未代签。

## 2026-10-22 修复安装源扩展清单资格P2

父级独审确认optional_permissions/content_scripts在重新固定合法source SHA后仍可通过，原资格门不完整而非摘要突破。原586/ZIP不改；唯一写入者核Git后，按独审封闭字段/本地路径/CSP方案补真实Apply重新固定SHA反例，见[修复证据](validation/extension-payload-contract.md)。本片仅定向红绿和必要构建，不重刷无关全套，不改扩展或系统权限，Windows/完整包仍未验。

r02同源重新固定SHA反例16结果4P12F（12项未抛异常）→r03同测试16P0F0skip，884输入稳定且红绿仅InstallationPayload.cs改变。源码独审限定ACK；冻结后只四相关类必要签收，不回填或累加旧586。

固定897cbb61c1e3d35ffc1e861c53ef00cdb4c85cfd定向四类105P0F0skip（新16+旧选中89），r04六普通构建0W/E，884输入逐Git一致、14目标完整SHA和摘要吻合。唯一生产差异InstallationPayload.cs，其余350原src及旧测试/锁/扩展不改；原586 TRX/ZIP前后SHA保持，未重跑全套，Windows/可运行包仍未验。仅接续原件独审及正常开发分支推送。

P2修复固定原件独审限定ACK，105结果/89既有/884输入/14目标与红绿/旧586保全自洽；仅归档与三报告推送，不借记完整套件或Windows验收。

修复原件ZIP/99manifest独审限定ACK；Library确认libfile_40f2a863803c819188e9a79663dd4439版本0，990356B/100成员，SHA256 3586b35d09f4dc3b8a8aef69917d2db62cba661aca6c0f2f0ab9e7a8dbfc2682。保存后身份/摘要核验成功，仅三报告补回执正常推送；897cbb6产品/测试不改，原586保全及Windows边界保持。

## 2026-10-22 稳定受控启动入口

沿原同代App controller/READY/Host/warm链新增独立Launcher和继承stdio生命周期桥；短门捕获current、门外验证、原admission复核，取消只关原stdin不kill controller，未确认不重放或清owner。独审三项生命周期P2由r05四屏障0P4F→r06同测试4P闭合候选，全部失败/编译零用例保留；r03七普通构建0W/E，无新包/许可。新主路径定向验证与独审接续，见[接线和原件](validation/stable-controlled-launcher.md)。稳定文件真实部署/更新及完整Windows包仍下一安装入口范围，不代签真实Windows/medium/WPF/250600，不重刷586/105。

稳定启动候选r07新Peer未连接AutoFlush导致3F原件保留；修正夹具并保留原取消合同，r08 19P，r09含双原异常保全20P0F。独审三项生命周期修复限定ACK；待冻结源码后一次相关签收与正常managed构建。

稳定启动源码21269b1336cd2b0188b771ccab331f0392716125固定签收：7正常managed构建命令0W/E；一次相关310P/0F/0skip（290既有+20新，13类），892输入与16目标版本核对通过。旧586/105和样本保留；audit绝对/相对样本索引错误原件保留并修正，未重测产品。稳定部署/统一安装/真实Windows与自包含缺件仍未签，P7整体InProgress。

稳定入口本片独审限定ACK与原件ZIP核对完成；Library libfile_82335144dbf88191b6931893a0b690a3（v0，1,945,681B/294members/293manifest，SHA256 f5a435a7946a6a48869a9371a653c82b0e3773af158d0d0e4bc41ee19e8c42a5）。本次证据提交仅三报告，产品21269b1不变，实际Windows/完整可运行安装包仍未签。

稳定bootstrap不可变部署/替换/撤销内部基元候选：精确五运行文件+catalog、持久root/data归属、atomic active、exact回滚与pending失败关闭；实际Program immutable入口与controller内绑定重核已接。候选r07 60P/0F/0skip（新40+原20），Entry原句柄/metadata界限独审问题已修，待固定树签收。FDD明确无runtime，固定locator/跨资源统一安装和真实Windows/P7未完成。

bootstrap内部基元固定签收aab208ab716f5c1b36c0392d464ff35d8fc1dd05：7正常managed构建0W/E；一次同范围60P/0F/0skip（新40+原20，4类），899输入/17目标版本与4实际Core副本核对一致。旧310/586未重跑，原断言与既有锁/打包/扩展/权限均保持；跨资源统一安装、fixed locator和真实Windows/P7仍未完成。

bootstrap部署基元独审及原件归档限定ACK；Library libfile_20fae98a783481918afd40bc2229d393（v0，1,475,759B/223members/222manifest；SHA256 664be5b73467cfd7994fbe0956cfbabd1ffcb219befbfa9b0212fb61c230ad4a）。本次证据提交仅三报告，产品aab208a不变；后续统一协调需共享原admission并明确generation/Bootstrap/Host三方commit与补偿，不能简单套用会重复取锁的现接口。真实Windows/完整安装仍未签。

统一generation/bootstrap/Host事务进入DesignReview：已提交一份原scope、已知upgrade-pending阻断、分阶段有界计划/Terminal、唯一最终归档点与逆序补偿方案及待实施验证矩阵。内部独审限定ACK，修补早期Abort无Prepared合法形态及最后Move不明历史分支；仅设计待根侧独审，产品aab208a及源码/依赖不变，没有新测试/构建通过数。fixed locator、真实Windows与完整安装/P7仍未完成。

统一部署scope/journal第一片：先持有setup/Host/upgrade owner再获取，原正文与关闭双错保全；scope持有setup至Ticket完整关闭，预发布finalizer拥有Closing后新文件/registry只读资源，仅AbortBeforeSnapshot且最后Move后无清理。独审settlement并发P2由r04同测试1F→r05 1P闭合，r06新25+原文件事务45=70P/0F/0skip/902稳定输入为dirty候选，待固定树签收。G/B/H统一发布、Committed/恢复/fixed locator/真实Windows与完整安装仍未交付，不借旧数字。见[范围和原件](validation/deployment-scope-journal.md)。

统一scope/journal固定产品a808310e0199df92ac10194c5bb25cd99c73ada0签收：3普通managed构建0W/E；一次111P/0F/0skip（新25+原86，4类），902输入匹配产品Git、12目标版本/4实际Core副本一致。独审并发清理P2已定向红绿闭合；旧测试全字节保留。仅existing-only/Ticket/AbortBeforeSnapshot，不是三方发布或Committed；真实Windows/native/runtime/fixed locator/P7继续未签。

原件已保存Library libfile_4f1fc1e8156c8191979ee2485d5b968e（v0，1,435,271B，162members/161manifest；SHA256 a0cda127c991fb2e0f6b1acb1f51e1970bb70d531e8113798abd7ed6c32b9d5d），本地Library身份已核。ZIP包含完整TRX/命令/日志/红例/输入摘要与固定Git源码；无DLL本体。证据提交只更新三报告，产品a808310e0199df92ac10194c5bb25cd99c73ada0不变。

统一事务Before快照/AbortBeforePrepared候选：同scope预发布原snapshot任务与finish封闭仲裁，四有界文件和Chrome/Edge原读取结算后才写实际≤128KiB Before；attempt先于CreateNew，partial/Flush/close/foreign永久Unconfirmed；Closing复核原观察并携BeforeSHA/Preparednull归档。三组真实最小红绿已独审，r07 45P/r08 2P分列不相加，待冻结48范围签收。尚未准备任何候选，无prepare/publish API，不冒称三方事务或Windows/P7。见[实现与原件](validation/deployment-before-snapshot.md)。

Before/AbortBeforePrepared固定产品a909c20981dd88fb31ecb42bf943071dbfec32f1签收：3普通managed构建0W/E；一次48P/0F/0skip（新23+原scope25），904 Git输入/12版本/4实际Core副本核一致，三组同完整testfile真实1F→1P保留，编译零用例分列。旧tests及权限依赖均未变；尚未准备候选，无Prepared/发布/Committed，真实Windows/P7未签。

固定树与原件独审限定ACK。原件Library libfile_348b60f9b7e08191833b0e3df952c14c（v0，1,877,796B，206members/205manifest，SHA256 ed0943a022ef9d92840a68f0ce603a2fdd61c63c43e198bc4c2bfc2ee2d512f1）；本地Library身份已核。ZIP CRC、205摘要及8份Git源码已独立核验，无DLL本体。证据提交仅三报告，产品a909c20981dd88fb31ecb42bf943071dbfec32f1不变；完整统一事务与Windows仍未签。

共享scope原IO的G只准备候选：唯一原Task/IO账本贯穿源校验、复制、Flush/Close及派生v3双复验；private凭据绑定原scope/原取消，不发布current/B active/registry。source原句柄错1F→1P、交付/交付后取消2F→2P同完整测试文件保留；r07 G24P及r08新4P分列，待固定签收。metadata partial/Move未知/foreign保留pending，payload partial仅结算后可记Inactive Abort；Closing新读取也由原finalizer结算。B/Prepared/三方提交和Windows/native/runtime仍未完成。见[实现与实际边界](validation/generation-preparation.md)。

G只准备固定产品eaec97468fe2d47c89fde042f528ba002a6f3e65：7普通managed构建0W/E；一次128P/0F/0skip（G28+原100，五类），907Git输入、17目标版本及4实际Core副本一致。source句柄和取消双例红绿保留，旧tests与既有安全/依赖合同未变。本片只产生scope绑定Inactive候选；B/Prepared/统一提交及真实Windows/native/runtime/P7仍未签。

G固定树与ZIP原件独审限定ACK。Library libfile_21975d63d5188191ae33535dc086bc94 v0，1,626,307B/197成员/196manifest，SHA256139cc38060cdaefc9882e44d326059618e3ba1f167c31afaa3bd26331c53a52c；CRC/11份Git源码/raw128/两红绿/17版本均独立核验，无DLL本体。证据提交只三报告，产品eaec974不变，B/Prepared仍待下一片。

同scope B只准备候选：原G有效后登记唯一原B Task，G/B共享同Ledger且Close/Closing按原channel结算；源/staging/final重验实际Core/revision与完整五文件合同，独立intent/随机Bundle/inactive retention，不改canonical owner/bundles/active/current/registry。原B正文保全1F→1P；r05混合Core产物12P18F保留，同源码测试仅同步managed后r07 30P，r08精确资格原因4P分列。待独审固定签收，B/Prepared/Windows不冒签。见[实现与原件](validation/bootstrap-preparation.md)。

B只准备固定产品f23bdcf387cfacb7d9c187c8c27739f1f7a97eb0：七普通managed构建0W/E，一次141P/0F/0skip＝B30+G28+Scope25+Before23+旧BootstrapDeployment35。909Git输入/17目标版本/4实际Core副本一致，335原测试文件未变；同文件原B错误保全红绿与混合产物失败诊断保留，不借旧数字。仅inactive B，Prepared/三方提交/canonical入口/Windows仍未签。

B固定树与ZIP原件独审限定ACK；Library libfile_d4d833ce83fc8191b658d54379a20274 v0，1,546,947B/179成员/178manifest，SHA25677c02c171d5341a48a9d5006bac48a81e5fc737f2ed08de9ed407296674a249e。CRC/10份Git源码/raw141/红绿/混代Core诊断/17版本均核，无DLL本体；证据只三报告，产品f23bdcf不变，下一片仍需Prepared与canonical发布设计，不当安装成功。

Prepared候选：同scope exact G/B＋原registry owner，原IO核旧current v3/Host与bootstrap真实归属，编码有界current/owner/receipt与B canonical映射，计划完整Flush/Close/exact后inner Task同锁完成资格；取消前后胜序与AbortAfterPrepared/SHA分开，partial/foreign/unknown保pending，零发布。registry挂起期间目标被改误签真实1F→1P同file，r05 26P/r07 28P分列，候选独审ACK，待固定相关签收。无前向/补偿/Windows假验。见[实现和原件](validation/prepared-plan.md)。

Prepared固定产品9c2256774d70c970b4e1cd24aec7db0f2a2f12af：七普通managed构建0W/E；一次相关204P/0F/0skip＝Prepared28+B30+G28+Scope25+Before23+BootstrapDeployment35+Host事务35，非完整Core。911Git输入/17目标版本/14产品DLL摘要及4实际Core副本一致，336旧测试不变；原registry挂起改G的同file1F→1P保留。计划有界封存与AbortAfterPrepared已闭合，零current/B active/registry发布；前向/补偿/Committed、固定入口/真实Windows/native/runtime/完整P7仍未签。

Prepared固定树与ZIP原件独审限定ACK。Library libfile_44b5928d8e1c8191901877454a513f17 v0，1,551,923B/191成员/190manifest，SHA256 a32f7e9251fed889c8fb7862aef81d5b98ae36d355d9ff9436dfc231400618d2；CRC/13项Git原件/raw204/同file红绿/911inputs/17版本记录均核，无DLL本体。本次证据仅三报告，产品9c2256774d70c970b4e1cd24aec7db0f2a2f12af不变；无前向/补偿/真实注册或Windows验收借记。

统一事务前向/逆序补偿候选：同原scope绑定Prepared/Task/原IO，B canonical归属准备失败保pending；Gcurrent→Bactive→Chrome→Edge→Hreceipt全provisional，Closing前取消逆向恢复且foreign/关闭unknown立即停止，Closing后仅结算原操作，最后committed archive Move才成功。原foreign观测被新读覆盖1F→1P、旧G/B恢复目标损坏2F→2P均同完整testfile；r10旧候选52P，随后crash原wait/错误保全及旧恢复目标已修，待固定新54+原相关204签收，不相加旧轮。仅隔离根/fake registry/受控真实child自退出，不接真实注册安装或P7。见[范围与原件](validation/deployment-publication.md)。

统一前向/补偿固定产品2ccc70074c1d586cd50acd853cc35f526918c711：七普通managed构建0W/E，一次相关258P/0F/0skip＝Publication54+原204（8类、258唯一名），包含七实际合成child退出和同原IO结算。915Git输入/17版本/14产品DLL摘要/4Core副本一致，337旧测试不变；两组同file真实红绿保留。最后committed archive Move才内核Committed，foreign/损坏旧恢复目标/原清理unknown保pending；非完整Core/Windows/真实注册安装/P7，无native/runtime可运行包签收。

统一前向/补偿固定树及ZIP独审限定ACK。原件Library libfile_f64fba2fd0e881919089b06caae265af v0，2,353,493B/287成员/286manifest，SHA2568177f07a91d61180f0ea6f93f3988d907431d989fa8fb8d79a08dbcc62164ffe；CRC/13项Git原件/raw258/7child/两红绿/915inputs/17版本记录均核，旧失败保留，无DLL本体。证据仅三报告，产品2ccc70074c1d586cd50acd853cc35f526918c711不变，不代签真实Windows或完整安装/P7。

根侧后续独审指出前片Write compare foreign异常未锁存P2，原258P/ZIP证据只保留实际范围，不等于产品根侧ACK。现原Snapshot按明确typed conflict/聚合原leaf永久锁存，Core拒新读/更早补偿/归档，close与flag独立；普通非foreign写后异常仍可exact补偿，不扩入口。独立新4case最终r06基线4F→r08同完整testfile4P；新夹具异常收尾独审问题已修，原run/finalization/settlement逐一join及多错保全，旧258测试源未改，待固定相关262签收。见[修复与原件](validation/registry-write-conflict.md)。

Write compare conflict固定修复cd2bab6c726f75f9a095b62879c647492cc3d50f：七普通managed构建0W/E，一次相关262P/0F/0skip＝新4+原258，旧集合多重集及338旧测试字节完整保留；两条普通非foreign写后异常补偿均Passed，未一律unknown。916Git输入/17版本/14产品DLL摘要/4Core副本一致，r06/r08同完整file4F→4P保留。仅原owner明确typed conflict永久锁存与新读/归档拒入；非完整Core/真实Windows/READY健康或完整安装P7。

Write compare修复固定树和ZIP独审限定ACK。Library libfile_7c3e6187e2688191835094263b92b889 v0，1,515,469B/162成员/161manifest，SHA256e5ccaafc927aceb3a046d51b91620a61e39a181948c0766c8a0ac525ff5926bf；CRC/9项Git原件/raw262/旧258集合/两普通IO/七child/同file红绿/916inputs/17版本均核，r02/r05旧失败保留，无DLL本体。证据仅三报告，产品cd2bab6c726f75f9a095b62879c647492cc3d50f不变；不将部署Committed当READY健康，不签真实Windows或完整P7。

已提交部署→原受控session健康候选：确定Committed原Scope一次绑定operation/Prepared/G/B/H及原controller/session，正常admission不绕pending；原StartAndCheck＋Host.Publish＋原READY callback后才仲裁READY，取消先赢/实际已退出拒迟到READY。部署Move后无追加部署步骤。失败原execution/角色/任务保留，Dispose不得丢租约，RollbackRequired明确未执行三方补偿；Unconfirmed保阻断无自动恢复。独审两P2由同完整file r06 2F→r08 2P闭合，r09 21P与r11 3P仅分列，不借记为固定全量；新22待签收。详见[合同与原件](validation/deployment-session-health.md)。真实Windows/native/runtime/完整P7继续未签。


已提交部署→会话固定产品b036357d998289f9e6192aeb23e0b484c170035f：七普通managed0W/E，一次相关191P/0F/0skip＝新22＋旧169（七类，非完整Core）；918Git输入/17完整版本/14产品DLL建测摘要/4Core副本一致，339旧测试文件字节保留。原两P2同file2F→2P，最终退出/held原Settle/取消/正常会话与稳定Launcher均覆盖；不同轮数不相加。仅2旧src＋1new，tools/依赖权限不动。RollbackRequired仍持原recovery租约且未完成三方回滚，SessionWorkSettled不称全资源释放，Unconfirmed无自动提升恢复。

固定树与ZIP原件独审限定ACK。Library libfile_9df832f4936481919ad31636349a170a v0，2,155,069B/227成员/226manifest，SHA2568a999dba70b089616c9776e8e146cb6a44d3bccd4d34e4965df65cf04b54b8e4；CRC/9项Git源码/raw191/旧169union/红绿/918inputs/17版本库存均核，无DLL本体；本地Library身份已核。证据仅三报告，产品b036357不变；三方postcommit新Ticket补偿、真实Windows正常入口/注册安装/native完整runtime/P7继续未交付，不将Committed=健康或保留原owner=回滚成功。

原失败session→新补偿Ticket候选：同controller/原execution持续持有，独立Task/IO/journal，复核exact committed targets及旧G/B后H→Edge→Chrome→Bactive→Gcurrent逆序恢复；foreign/取消/partial/关闭未知停依赖保pending。Closing封闭原execution退役、所有fresh读取与terminal IO逐原Task/句柄结算，lastMove才Compensated；不复活旧Prepared/current-only rollback。独审取消三屏障同file r05 3F→r07 3P、cached关闭Task同file r11 1F→r13 1P；r03混代未到断言，r08旧39P不借为最终43。本片待固定新43+前191签收，旧未跑204明示；真实Windows/native/runtime/完整P7仍未签。见[合同和原件](validation/deployment-compensation.md)。

原失败session补偿固定产品49cf34cf0492e14728c49c7bceff07565791d2e2：七普通managed0W/E，一次相关234P/0F/0skip＝新43+前片191，旧191多重集完整保留；旧204未跑不借记。920Git输入/17版本/14产品DLL建测摘要/4Core副本一致，340旧tests仅helper改变且原22方法正文byte一致。原execution始终原owner持有，H/Edge/Chrome/B/G补偿、原退役和全部关闭后lastMove才Compensated；pending/foreign/unknown无重放提升。真实Windows/native完整runtime/正常安装入口及旧会话健康/P7仍未签。

固定树及ZIP原件独审限定ACK；Library libfile_9d3712aa554c81918a0b12161c49658a v0，2,423,467B/285成员/284manifest，SHA256869e2a07c46c6f73d4465b70fd62c46a74f80d059393bcc99dc6af1cba04849d。CRC/12Git原件/raw234/两组红绿/920输入与17版本摘要实核，无DLL本体。证据仅三报告，产品49cf不变；不签旧204/fullCore/真Windows或完整安装P7。

补偿完成→旧generation受控会话候选：原Compensated一次内存权威/新controller+execution+launch，首次恢复为空不造session；原ledger核terminal/旧G/B/H和registry，最后异步registry关闭后重验闭包。新execution与prepared/Host IO先owner后Acquire，partial/Move/close未知保原Task及租约，不复活失败launch或Prepared。三反例r06同file3F→r08 3P，r10新32P分列；旧前置失败保留，不混数字。源码/fixture独审限定ACK，待冻结266相关签收，旧204/fullCore/真Windows/native/runtime/完整P7未签。见[合同和原件](validation/compensated-session-health.md)。

05c712bd固定首构建r11因probe空哨兵的泛型nullable标注CS8603失败，未执行任何固定测试，其余构建未跑；原件保留，不记通过。仅修显式null sentinel返回标注后重冻结，32候选不借记最终签收。

补偿后旧generation健康固定产品52b1130a76507f8dcc77b0fce8c6d8013643b81b：七普通managed0W/E；一次相关266P/0F/0skip＝新32＋前234，旧234与候选32多重集完整保留，旧204未跑不借记。922Git输入/17版本/14产品DLL建测摘要/4Core副本相符；原22方法suffix与补偿43完整testfile保留。首次安装空恢复无session，新launch原READY和最终退出分别签；取消/foreign/关闭或Move未知保原owner，旧Compensated历史不代表旧版可用。真Windows/native完整runtime/正常安装入口/完整P7仍未签。

固定52与ZIP原件独审限定ACK。Library libfile_f789dd28f0f481919a76dfe852bba0d3 v0，2,267,361B/256成员/255manifest，SHA2567bdc2445f412356c1f405edd2dce84fc86c35238d974319a75ae7cf3ef9d50e1；CRC/15Git原件/raw266/旧234+新32/红绿/922四快照及17版本库存实核，无DLL本体。证据仅三报告，产品52不变；旧204/fullCore/真Windows/native/runtime/P7未签。

单一安装操作编排候选：一个原owner串Scope准备/三资源提交/新版canonical READY、原失败授权补偿及旧版新READY；阶段结果与后续原生命周期分开，firstempty零旧factory，取消/外部owner不制造补偿资格，未知保原owner无新恢复入口。工厂原Task预登记，迟到取消fresh-only封闭，无generic Dispose其他工作。r05新15P，r01仅测试重载编译F零用例保留不算产品红；仅定向19（新15+旧4代表）待冻结，不重刷/借旧266及204。见[合同/原件边界](validation/installation-operation.md)。

单一安装操作固定产品6d04d197683f8841c906c5d33fed0e1800fe51ed：七普通managed0W/E，一次19P/0F/0skip＝新15+原4完整链代表，新15同候选/原4取前266指定三方法多重集相符，不混历史轮。924Git输入/17版本/14产品DLL建测库存/4Core匹配；原22方法suffix、Comp43/Restored32全文件字节保留，不等于全量重跑。仅单原owner编排，不新增恢复算法或真实安装入口；阶段READY与原生命周期/unknown分开，不签完整安装成功。

固定树与ZIP原件独审限定ACK。Library libfile_a500dcbf7ac881919ccd26aa18571e5c v0，1,193,473B/129成员/128manifest，SHA2560c95a452d71f64b92ce9d05fa700b10b43e2b5bcb36199e19c7701cabca2e213；CRC/10Git原件/raw19/924四输入及17版本库存均核，无DLL本体。证据仅三报告，产品6d04不变。旧266只重跑4代表，其他未跑，Prepared204/fullCore/真Windows/native/runtime/完整P7仍未签。

独立安装入口候选：固定内嵌source pins（现Unavailable）与两阶段独立Installer产物，首次根在同原Scope原IO ledger中排他创建/完整Ticket交接，未知保partial/pending，不领养空目录。单Operation七阶段结果与原生命周期分离，补只读原Start/协议/stop/exit观察退出fence，Ctrl+C原CancelAsync封producer/join期间仍抑制默认退出；不新增恢复/重试/Dispose外部owner。候选新16P，r08同方法有效fence1F→r11 1P；编译和前置失败完整保留不混数字。r12 Installer0W/E，源码独审限定ACK，待固定必要回归。生产缺完整包/可信pins拒绝，Installer健康会话不启warm；不签真实注册/Windows/原生完整runtime或P7。见[实际差异及原件](validation/installer-entry.md)。

独立安装入口固定产品d55be5563a6cde314975b58356b0fd7cb95c9524：八普通managed0W/E，一次58P/0F/0skip＝新16+原Operation15+Scope25+Host门代表2；956四输入/Git、18版本、15产品DLL建测库存/5Core相符。r08同方法fence1F→r14 1P，旧22方法及其余既有test文件保留。生产pins仍Unavailable，Installer不启warm；原任务/actual-exit和Ctrl+C原取消任务完全join才允许返回，未知不提升不新恢复。未重跑旧266/204/fullCore全量，不借记未执行项，Windows/native完整runtime/真实安装注册/完整P7仍未签。

固定树及ZIP原件独审限定ACK；Library libfile_9f283f3eb7b88191b050095bf97037bc v0，2,453,373B/389成员388manifest，SHA256ec60502473b264c437b8fe51e81664c86f5ce3b5fd4f8c7024832a052b02fbc3。CRC/25Git原件/raw58/同method红绿/956四输入/18版本及库存核过，无DLL本体不称归档二进制重验；后续证据只改三报告，产品d55be55不变，所有历史失败保留。

Windows payload封装接续：发现Imaging实际使用OcrNative却未纳入源资格门，最小补入原NativeFiles并增强独立48项合同；重算新source SHA的缺失/截断/x86反例同完整file4F→4P，另原receipt18P＝候选22P。仅已授权runtime10.0.13两归档原字节恢复，SHA512/CRC/MIT核对不是在线签名或Windows执行。新增受界本地输入清单，真实资源/归档来源与普通build目录分列，后者版本归属明确Unverified；Blocked且G/B摘要null、pins仍Unavailable。缺四真实native、七角色self-contained及有效签名/真实extension origin/B闭包，不用fixture代签；待冻结必要验证，不借旧完整套数。见[实际封装边界与原件](validation/windows-payload-assembly.md)。


Windows payload固定产品2182f8df5ba5d6e5a0e36f137e6b082101c6f7a8：八普通managed0W/E，一次109P/0F/0skip＝新OCR4+原receipt18+payload36+Host35+Installer16；959前后/Git输入一致，18obj版本声明/15产品DLL文件摘要/5Core副本相符，不称独立DLL metadata认证。Python新5/原19分别通过。实际466项只读输入清单含runtime409+notice2/Skia4/Git12/观察G34+B5，缺15，exit2 Blocked、G/B摘要null且pins不变；同完整file4F→4P保留，不混候选22P。Windows/native/selfcontained/真实注册安装/签名/真实extension origin/P7仍未签，缺合法已有Windows工具链与真实产物，不新增许可/权限/CI。

固定树及原件ZIP独审限定ACK：CRC/123manifest/17份Git源码/同file4F→4P/raw109/959四输入/8构建/新5与原19Python均核。Library libfile_3fa869febf848191aa932f2076b02f66 v0，965,715B/124成员，SHA256 df597040bd5c6ca78b2775e53db158a5684b655f464eb9b4f686327acc66bca8；本地Library身份已确认。ZIP不含DLL/nupkg本体，不称独立二进制重验或签名。证据只更新三报告，产品2182f8df5ba5d6e5a0e36f137e6b082101c6f7a8不变。

自包含B接续：首次真实Launcher no-restore/self-contained win-x64/10.0.13 publish实际NETSDK1112，缺已验证可消费runtime pack，原档恢复不等于cache安装。未重复七次同失败，未切offline/注入缓存/放宽验签。独审设计ACK后接原资格链：SC精确194文件+catalog/128MiB，开发5/64MiB保持；实际mode贯穿candidate/Prepared/receipt/Entry，固定RuntimeList字节/配置/deps封闭合同。仅组件元数据和原开发链可测，完整SC Read/publish/运行与Windows仍NotRun；pins不变。见[具体缺失与合同](design/selfcontained-bootstrap-contract.md)。

候选SC元数据20P，r03唯一集成失败在Apply前置混合revision、未到Mode断言，不算产品红；原件保留。源码独审限定ACK，现冻结后统一八managed并执行一次相关回归，不借旧测试成绩或扩大真实Windows口径。

固定产品583a8c31162599883f59423b3d9e44dd23f91e1c：八普通managed构建0W/E，一次相关153P/0F/0skip＝新组件21+B准备30+B部署35+Prepared28+StableLauncher19+Settlement4+Installer16，非完整Core。961四输入/Git一致，345旧testfile字节全部保留；18obj声明/15产品DLL文件摘要/5Core副本一致，不称独立DLLmetadata读取。新21与r03完整testfile和集合一致，r03混代前置失败不算产品红，不累加轮数。SC完整Read正例/真实publish仍NotRun，r01实际NETSDK1112及无输出目录保留，未重复七次/注入缓存或绕验签。

固定树及原件ZIP独审限定ACK：CRC/89manifest/16份Git原件/raw153/961四输入/8构建/候选同21及345旧testfile均核。Library libfile_60b3f1cb75408191ab26a59f3f5a67b7 v0，721,728B/90成员，SHA256 a846c0874a917f615bfe5996ca0bc26080fbbaba0272fae743d4a9c284514624；本地Library身份已确认，无DLL/nupkg本体不称独立二进制重验。证据仅三报告，产品583a8c31162599883f59423b3d9e44dd23f91e1c不变；本轮native工具条件追加本地记录未入ZIP，真实SC Read/publish/验签/Windows/P7仍未签。

2026-10-23指定环境恢复，单写者核5af远端/dirty0。两runtime10.0.13正常在线严格验签、官方locked restore与恰两包实际漏洞查询通过，锁未改；旧失败保留。r03全局-r传播Core触发NU1004，改用项目已有RID后Launcher原锁还原/实际包查询及自包含publish通过，不需要修改Core锁。用户电脑缺开发工具且要求下载即用，不在其电脑补SDK/MSVC；完整应用仍缺四native和完整七角色实际包、扩展origin/可信pins，Unavailable保持。

为今天有限窗口，固定5af另发布真实ShellWindow公开内容自包含WPF工具（非成品）。GrowthHarness原locked restore通过，但package-list CLI实际Sequence错误保留Fail；独审限定ACK后仅本artifact用实际assets两target/8project libraries+根项目/0package及原锁证明，不改生产gate。Core/Desktop两包独立验签审计和409 runtime逐字节绑定、14 SDK未复制的WindowsFormsIntegration原档条目补齐、实际全文件manifest；真实Windows尚未运行。旧153没有重跑。见[交付边界](validation/windows-afternoon-native-plan.md)和[工具受限交接](validation/windows-public-overlay-handoff.md)。

2026-10-23成品优先接续：复用既有public windows-2022/contentsread/attempt1/15分钟入口准备四native真实Release构建及两原CTest，预装14.44/SDK26100/CMake实际门，缺件不安装/不换工具；原bounded日志分片回收真实DLL，不用用户电脑或artifact/cache收费存储。只读repo metadata成功，未查询已拒Actions/runner API；实际投递/运行结果另记，不将准备当通过。七角色自包含云端发布可独立继续；原pins/完整成品/Windows未验边界不抬。GrowthHarness不是主要交付，其目录外OS缓存风险需专项只读审清，未核不让用户电脑启动。

真实147ab七角色+Launcher自包含发布均exit0，ImageWorker3包实际清单/官方漏洞查询及Skia三包严格在线verify通过，原锁不变；并非已封成品。实际ClipboardHost/CaptureHost均Core+Desktop，原payload精确合同误只允许Core。两实际配置原字节封存，经新testfile+原生产门真实2F，最小补两个明确role及夹具后整类46P=新10+旧36；其他role/版本/重复额外框架门未放宽。候选源码独审/冻结回归另记。GrowthHarness专项只读确认不能保证OS字体缓存/驱动/条件WER/启动日志绝无Etemp外写，当前严格临时目录合同不放行用户电脑启动，交接顶部已明确。

SC B实际194文件+新catalog→原Read在旧合同真实拒绝，独审设计后仅固定实际Linux SDK402生成的精确deps形状（16元数据差异），189runtime/notices长度/hash一项不改；graph/额外native版本四反例及实际deps正例加入，候选26组件通过。额外Read原例由拒转接受；Generation仅实际Core字节绑定，未签G安装/Windows。候选首次25P1F因嵌入资源改变Core而FD bin仍旧Core，正常重建FD Launcher后26P，原件保留；固定新SHA再重发八产物及必要签收，不复用旧Core/source digest。

固定产品a32854dde197f38fbef1e4672c171bc726633819：两实产物缺口独审闭合；940输入/Git一致，八SC发布全0、8Core+testCore字节同；一次必要三类102P=Payload46+BPrep30+SC26。真实B194+catalog原Read Pass，新sourceSHA30894F310FE35A518B24B3369F2AF84AAA93807038478C0A72AFF99BC591C729，两个实改且重算pin负源分别被runtime/graph门拒。原19F/31P/候选26分列不相加。G473/B194实际输入Library libfile_ba7a2fa674648191bbb09eebf6a6d0f5 v0，不可安装/DoNotRun；原件libfile_f56348701a6c81919a89c366718c8d3f v0，217成员/216manifest/16Git源码，无DLL本体。四native源147ab的既有免费CI请求已投递，实际结果/二进制回收Unknown，不读被拒API；四C++源到a328无diff。完整G/origin/审定pins/身份可信签名/Windows安装READY及250600仍未验，生产Unavailable。Growth不能保证OS缓存全Etemp，不在用户电脑启动。见[实际源与原件边界](validation/payload-desktop-host-runtime.md)。

2026-10-23 WPF窄窗布局：原PreferenceRow固定180改实际字体/template测量与单列；原ListBox窄窗顶部滚动，正文/底部各有界滚动，实际滚条动态资源参与极窄横向extent。两个原按钮行Wrap，原事件/Mask/保存事务与全部安全门不变。新纯布局7P；首轮6P1F是238与237.99999999999997精确浮点夹具差异，原件保留，不称产品红绿。初始候选App0W/E，冻结后的最终构建待单列。只读源码限定ACK；未重试拒绝的浏览器，不借HTML代签WPF，真实窄窗/200%字体/DPI/焦点/滚动/像素仍NotRun。见[准确范围](validation/settings-adaptive-layout-2026-10-23.md)。

固定产品b2ac823ed7b1b45aee2e6dda7001080a1ddf91e9最终App交叉构建0W/E、25.27s，487个测试后输入与固定源码逐项一致；7P为同字节候选测试，没有重复。原6P1F日志/完整TRX与初始测试文件保留。实际WPF像素/键盘/200%和旧平台门仍NotRun。

固定树与原件独审限定ACK：26唯一ZIP成员/25manifest/11源码、487捕获输入与Git、两个DLL本体及原6P1F/最终7P完整TRX均核；App0W/E25.27秒。Library libfile_a3d05462cb7c81919c6f37fba2d86cda v0，1,107,931B，SHA2569829e1f99424c8feeeea6260ca01b9ad9294cb79c6b10c860358ebd5b2e406dd。证据不称同文件产品红绿、WPF原生渲染或可运行成品。

2026-10-23四DLL原件阻塞实际关闭：父级正常登录job正文回收后，原环境Library材料化libfile_832f9b13205c81919c08be209f93db84 v0，345446B/3JSON，ZIP SHA2566587b905d2ca37f13bc92ae306d03fdb495dd7b73046507ec6f5ee5143c5cc62。消费者独立核20FILE/71CHUNK全部序号、base64/gzip/raw长度与摘要、summary合计及native-products四DLL源147ab；仅PE头只读，不执行DLL。四native源码到bf479无diff；原2CTest为历史记录不重跑。正常正文回收通路已证明，不再泛称证据不可取回；旧被拒API不重试。未启新CI，完整origin/G/B/签名/安装/READY/WPF UI与250600未验，pins不变。见[native回收收据](validation/native-payload-build.md)。

2026-10-23 Settings真实栅格准备：仅GrowthHarness新增模式及有界workflow，直接生产SettingsWindow，四种合成宽度/字号/恢复状态；45秒driver、60秒原进程、15分钟job。源码/包签名与runtime审计门保留，未知清理不Shutdown假通过。尚未投递或原生运行，PNG不能代签DWM/系统DPI/动画，pins仍Unavailable。见[范围与退出合同](validation/settings-raster-windows.md)。

Settings栅格准备定向签收：最终离线10P（不相加），工具最终交叉build0W/E3.56秒。三处夹具/owner审查问题已在投递前闭合，原失败日志保留；只读独审限定允许一次标准Windows CI。实际WPF运行和正文原件待回收，未提前签PNG通过。

Settings栅格唯一CI实际投递并普通页面确认运行中：源00a0e2a327fc35ef91738999d0400ca9a460a459，run39945729009/job120387796111（2026-10-23 11:23:28 UTC启动）。本环境日志正文要求登录，未登录/绕路/API；父级既有正常登录页面回收待续。准备原件Library libfile_970349fcb3a48191b1538ebd2e1a1fdd v0，279822B/23成员，SHA25642b44fb34ccb99b228514b0421887c0ddf027d7ea98a0ede442374a8718b7250；8个Git源、398输入、原失败日志及最终10P/0W0E证据。没有PNG或Windows通过声明，不再次投递。

Settings唯一Windows运行父级已核成功：run39945729009/job120387796111/source00a0e2a，Pass_LimitedRaster、TimedOut=false、CleanupConfirmed=true、OriginalHarnessExited、Errors=[]；17命令全exit0不当17测试。父级已回收四PNG30/30块并核raw/gzip摘要；非图附件未全取，不能签全包。消费者Library标识尚待，不假设父/tmp共享；像素审查进行中，不改产品/重投CI、不称DWM或真实DPI验收，pins不变。

Settings四张实际PNG已消费者Library本地材料化、读取/哈希并view确认：窄窗当前导航项右裁，Font26导航约150px及图文错位。生产修复保原事件/Mask/事务：当前项在load/selection/size/font/generator后合并post-layout自动露出，epoch拒卸载迟到；不抢焦点/追逐手动滚动。单行图标文字居中、rail按有限实际测量高度给正文让位。定向9P旧7保留，源码独审限定ACK；Windows修后像素尚未执行。本轮不改workflow、不追加CI，等待差异独审再定一次复核。

Settings导航修复产品76956cc5eabf24f6e765d8f22c2e60dc49fba227已正常push；原件Library libfile_740e74e3630881919188d59b3e32fed4 v0，1518621B，SHA256 6f001c0fd13400bbae23f6ca2f3f40188730d4a8b6d0289201664b5bb900237e，28成员/27摘要/9Git源码，含实际9P TRX/最终0W0E3.62秒交叉构建/四张修前PNG。源码独审ACK，修后原生selected-auto-reveal/像素仍NotRun，无新CI，不借记旧10P或DWM/DPI/生长流畅度。

父级原件和源码限定ACK后授权一次必要导航Windows复核：固定直接父9d917d8d6072e0a642b74db62a2af86e4a87678e（产品/工具等同76956cc），原workflow仅显示名变化、脚本仅父/scope固定、两文档记范围；保原depth2、15min/45s/60s、签名审计及退出清理门。先观察生产auto-reveal后才fixture滚动；四图及原退出摘要沿普通日志正文回收，未回收不签视觉通过，失败不自动重投。本条仅准备，真实run另报。

导航修复唯一原生复核已正常投递源b4e642230b1fda315e34404eeb0fa134e14a5b05：run39948580823/job120397194532，普通页面确认2026-10-23T11:47:36Z启动，初始InProgress。投递前真实Git来源门通过及相关scope1P，不重复旧套件。本环境普通job正文需登录，未登录/绕raw下载API/重投；父级现有登录页面需回收四PNG与原状态/退出摘要。实际结果和修后像素尚待，不签DWM或真实DPI。

唯一导航复核run39948580823在11:49:29UTC普通工作流页面已确认Completed/Success，耗时1m41s。仍未取得本轮日志正文、四PNG、原geometry/cleanup字段；交父级既有登录job120397194532回收，不把公开绿标代签像素/退出摘要。没有重投或绕路，产品仍冻结76956cc，投递源b4e6422。

设置导航修复限定收口：父级独核run39948580823四PNG长度/SHA、settings-result与summary.Actual一致，窄/Font26当前标签完整且ObservedProductionAutoReveal_NoFixtureScroll；误差0.5px、nav72/90px，宽图不变、短窗恢复/保存取消完整；17命令exit0、清理确认、无超时错误。SourceHead=b4e642230b1fda315e34404eeb0fa134e14a5b05，FixedProductSource=9d917d8d6072e0a642b74db62a2af86e4a87678e分列。消费者本地实际材料化/哈希/看过新窄窗libfile_bdeaf5233fec819197e55e28aa477bb6与大字libfile_e988791a20648191ab5644755aa55988两图；其他原件核验归父级，不冒称消费者全包。仅记录3文档，不改产品、不新测试/CI。生长动画原生产链存在，下一最小条件已只读列入validation：需原driver/退出所有权、去重真实帧时序、阶段/反向/流式长文及有界真实帧图，不能用旧总帧数签流畅。DWM/系统DPI/物理键盘/生长/安装仍未验。

生长动画采样工具准备：新增GrowthHarness --ci-overlay-stages，真实Shell绑定事件/原收起回看按钮，不另驱动Tick或窗口几何；生产仅两只读观察。中心9阶段各Timing/Raster一次，必须实际中间帧/正文可见/当前marker，保重复重入原回调、单调QPC/RenderingTime及有界真实WPF栅格能力；未在Linux造图。原driver发布先于执行，原close task与Closed分别join，未知保owner/目录，45秒取消不假清理；外部60秒原进程包装尚待下一授权片。只读独审限定ACK，最终定向12P/0F/0skip（不叠初始10），最终工具+App交叉构建0W/E3.56秒，508输入前后一致。r02新工具参数编译失败和r04修标记前构建保留，不当产品红绿。无workflow/新CI、用户电脑或模型动作；未签Windows动画、DWM/60Hz性能、50次、四角/系统DPI。详见[准确差异与后续最小条件](validation/overlay-stages-preparation.md)。

生长工具冻结b334c846a6b81fbf276adbd54491e367696abd08原件已入Library libfile_64719769a6a08191bc73a892be17eea1 v0：1856591B、29成员/28manifest/9Git变更源码、SHA2565767671aca0bdfdd83b89207e9df18070d6fc4eb2f5699999f89bfeb0efbe82e；含完整12P TRX、最终0W/E3.56秒和原编译失败日志、508输入/Git匹配及四本地DLL原件。未包含Windows图像，不是自包含产品包或动画验收；本跟进仅两文档记收据，无产品/测试/workflow变化。

生长采样原进程wrapper准备：仅新增两个Python脚本，生产/WPF/harness/workflow不变，无CI。三原观察线程先准备再唯一Popen，精确原引用/原wait/双EOF与日志关闭分别结算；60秒判定后仅原Popen单次救援，未知非daemon保owner并立即报告，不把45秒内部取消或deadline当退出，未结算可留到既有外层job边界。独占输出根不删除任何目录，原source/close结果和overlay-state残留门保持。独审发现thread-start夹具P2，同一方法三失败子项→通过已保原源码/日志；最终15P（2真实Linux子进程，其余确定屏障/规则）不叠旧10P。源码与原日志限定ACK；Windows运行、workflow接线及证据总门仍待独审，不签动画/像素/FPS。见[wrapper准确合同](validation/overlay-stages-preparation.md#original-process-wrapper-preparation-no-ci)。

原进程wrapper冻结41be15b26a7e1a1d351bf19dd90b6a8e7f3da77c及原件独审限定ACK：Library libfile_a51e3ab91bb08191b38d7562915cfec7 v0，189381B/15成员14manifest/4Git文件，SHA2563874efa4e0a0a6447c6e0a71c6b30988ef1d31fd9b96980c7a2a254032c5381e。原件机械摘要、同方法三子项红绿及最终15P核过；证据后续仅两文档，不新跑测试/CI、不改产品，不把Linux退出合同代签Windows/WPF。

生长采样最小集成：原job Python直接调用run_overlay并持有owner至证据报告结束，未知即时收据、原wait/双EOF不丢弃；不再外套60秒subprocess。完整原result/raw/全部PNG、stdout/stderr、紧凑收据、source/bin绑定及summary/manifest先冻结核32MiB raw/4MiB gzip总额才发FILE/CHUNK；已登记缺失/变更判Incomplete，不截断丢帧，不重复嵌Actual，不自动重跑。仅原workflow/入口/test和两报告共五文件，产品/工具/wrapper冻结294ce3f不变。真实本地16 unittest通过（新6+旧10，0.030秒，非TRX/WPF）；独审通过后按父级授权仅一次既有Windows2022/attempt1/15min采样，run/job及原生结果另列，不预签动画。原安全门、pinsUnavailable、无用户电脑/模型/新权限费用保持。详见[最小集成与原生边界](validation/overlay-stages-preparation.md#minimal-integration-and-authorized-single-native-sample)。

集成4996577718483c13ea952973a8b7d8fab5e08b86独审限定ACK及真实Git五命令来源门通过，唯一正常push已请求run39957744943/job120427702394，13:00:57UTC创建，普通页初始InProgress。准备原件Library libfile_18ba6eac86748191a51f9fd49d48d300 v0，187022B/10成员9manifest5Git源，SHA256d837f7da5681d56992bc198eed252bec05b1225424370edd4846caf08c945678，16P/sourcegate原件已独核，不含Windows帧。普通job HTTP200但Sign in to view logs，需父级既有登录正文回收，消费者未登录/调用API/raw/download/重投；页面快照与dispatch保全。原生阶段/帧/退出清理未取回前不代签，产品与wrapper不改。

同一生长采样13:02:46UTC普通页确认Completed/Failure；总1m28s/job1m24s，公开annotation仅Process completed with exit code1。原失败阶段/异常/FILE及退出清理尚不可见，不据此归因生产动画或环境；原页面/final-public-status已保全。停止本目标投递，无第二CI、无猜测补丁；需父级既有登录job120427702394正文取回实际原错后定位。此状态更新只改报告，16P仅准备检查，不掩盖原生失败。

父级失败原件已本地Library物化并独核libfile_6a8342c8e00c81919d9f025008d4fee2 v0，8643B/SHAfd8a4c6d100e31fc2678876f283c2becbbdd3d560bc899490d257502b2d18bd9，8FILE/8CHUNK原raw4503/gzip1862全符。确定前5Git成功后desktop-preflight 45秒TimeoutExpired/0B日志/Exitnull/子树清理未知；未启动SDK或动画。最小修复DesktopOnly只查桌面、Native/SDK明确NotRun并拒native资格，默认库存/桌面门原字节保留；固定阶段stderr即时flush及StageDesktopPreflight，无新owner框架/预算/权限。仅证明去掉无关dotnet/CMake/CTest/vswhere耦合，不追认历史唯一卡点。实际1个Linux原child超时/kill/wait/file-close实验和静态差异核过，非Windows；本机无pwsh，PS执行与实际解除runner阻塞仍NotRun。未改workflow/投新CI，原失败不回填；[精确定位与边界](validation/overlay-stages-preparation.md#original-preflight-failure-recovered-minimum-correction-no-new-ci)。

预检修复0cc15c69a0a5d3796520f0c112f66ebacf31b916独审限定ACK并正常push；去掉非必要诊断catch以保原异常。原件Library libfile_61bb32831c888191b448c9a00167521d v0，200753B/13成员12manifest5Git源码，SHA256d6cfdff970c1d0a0820a44a78643272e9099cdc0b4868513616e7e68ce882d35，包含父失败ZIP原字节及消费者核验/本地实验/输入摘要。本跟进只两文档，无新CI/重复实验。旧workflow来源门仍冻结，下次采样需另审源绑定与父级决策；PS和真实预检恢复未验。

父级限定ACK预检修复后明确授权一次新Windows复核。先精确更新PARENT/FIXED=d52199a及原5文件范围，depth2/父唯一/event SHA/clean/受保护输入门保持；9个Git对象证明产品/工具/wrapper到原294ce3f无漂移，预检源码同已审0cc15c6。只跑原source method1P（含正例和10反例），不叠旧16或Linux实验；真实commit来源门待单列，独审后唯一push。阶段stderr写原命令文件，非实时job控制台；完整证据后才可回收。旧失败与cleanup unknown不回填，工作流只显示名变化，无新权限/预算/模型或用户电脑；实际run/帧/预检退出另报。

新复核源1d28dde842f7fcad1e5410476eafe47bdcc383a9通过真实clean Git五命令来源门后唯一push，run39960404039/job120436667676于13:20:38UTC创建，普通页初始InProgress。本环境正文HTTP200需登录，消费者原生文件0，交父级已有登录普通页回收；不登录/绕API/raw/download/重投。准备Library libfile_dd23e61872b881919bf25508a9f34107 v0，193455B/11成员10manifest5Git源，SHA2562b9881802597b60e57784dcd044db20eee9865f2ac3a95d34d4cc80b77ba06de，1P及实际来源门/基线证据已独核，不含本次帧或Windows通过。源、固定父d521与旧失败分别记录。

2026-10-23 SeedOpen失败原件已由本消费者材料化核验：Library libfile_be3f096ed3f88191b6cbb2ba1db2c132，27文件/42块raw及gzip一致；预检5.313秒完成，原进程exit3与双EOF已确认、无救援，内层close/cleanuptrue但外层目录原来未检查，整体仍Fail。2790回调没有合格变化中间Opening、零PNG，不签流畅度。已修开始前测量耗时计入首Tick的确定缺陷并记录实际dt/epoch；历史run未记录dt，不追认唯一根因。同源clock6反例3P3F→6P，相关Provider18P、wrapper17P、既有Core几何4P分列；App/GrowthHarness交叉0W0E。独审限定ACK，未新CI，原workflow/sourcegate不放宽。详见docs/validation/overlay-seed-clock.md，真实Windows修复效果仍未验。

修复固定3a47f9b1a5048231c1e602bb55c4090e477b810f，封存Library libfile_b20c34e09174819185ea4737367a3795 v0，4482187B/48成员47manifest，SHA256 ae65fc64f09ba95ce6888cf187d445eff468c5aea5fcf08ffac7c1730f338d67。含旧失败原件、同文件红绿、实际7个DLL本体及来源摘要；此条仅证据回执，不借记Windows通过、不新CI。

父级已独审clock修复并授权一次既有Windows采样。新交付来源门仅绑定PARENT=FIXED=2ce69f410a6a6ca926eef5ca9a73d9271f15c069及原5文件范围；产品仍3a47f9b。只跑原来源方法正例/10拒绝子例并核真实clean Git门，独审后单次正常push；未重刷原18/4/17或构建。原进程/双EOF/目录观测和完整证据预算门保留，实际run/Tick/PNG/退出待回收，不自动重跑，不借记Windows或DWM通过。

交付a8c84ae1aa34cefc0d539a3874e6c81a31dc105b已独审并通过真实clean来源门后唯一push；run39964336709/job120450151713创建13:48:58UTC，13:50:19普通页仍InProgress。本消费者正文需登录、原生文件0，交父级既有正常登录页回收，无新登录/被拒API/raw/download重试。准备原件Library libfile_7f89efbdb9ec81918b72ec321b176be6 v0，301702B/17成员16manifest，SHA25654eee03fc549d8d0893efbadf7b49cbc7bd52e0eaa18b4a860cdf200954ec086；仅1方法及投递证据，不能签原生效果或退出清理。

最新39964336709原件已在本消费者环境Library物化、独核29文件49块及431源输入。Timing9阶段与RasterSeed完成，但第二轮同短文错误占420×376（第一轮420×113）；phase11长文确已更新，却被正确安全底边716限制而无可增长空间，45.237秒失败保留。原exit3/双EOF/目录Absent已核，inner cleanup true与outer false分列，不回填旧未知。尚无DesiredSize/extent记录，不能凭推测修WPF缓存。本片只补原生产测量的可空内部观测，工具512条上限取消原driver并保原close；不刷新布局/放宽中间态/改workflow或新CI。实际22P=新4+旧18端口/证据方法，交叉App+工具0W0E14.51秒；855输入一致，源码独审限定ACK。不是原生红绿或动画修复验收。见[新故障定位及最小剩余条件](validation/overlay-seed-clock.md#recovered-clock-fix-run-new-short-text-measurement-failure)。

诊断片2a04c83ae9ff7e76e14b5d0f072d34d5c307d884及原件已独审并正常推送；Library libfile_eb6ac6aaa30c8191808a5eddfd317790 v0，2035191B，SHA256934e4a266b397b11f4d1981701c64c720392d83427d5f842377d2c0848d57a3c。29唯一成员28manifest、10Git文件、855输入、22P完整TRX与4DLL本体及嵌套原失败ZIP摘要独核一致；创建和本地Library身份保存成功。仅追加两文档收据，不新测/新CI，不称缓存修复完成。

父级独审测量观测片后授权一次原Sequence诊断采样。本次仅精确重绑PARENT/FIXED=320615c59c72a119a0975d9f82c5e38d748678a0，固定产品/工具2a04c83；原五文件范围及来源/原进程/时间/完整证据预算门不变。原source-contract方法1P含正例与10拒绝子例，不叠旧22P；9受保护Git对象与2a04、2预检对象与0cc15c6一致。独审后真实clean提交门通过才唯一正常push，实际run/测量原件另报；Before在RefreshMarkdown后、CalculatedClampedHeight非Core实际target，必须结合阶段几何解释。不猜缓存修复、不自动重跑，原生未验边界保持。

测量诊断唯一交付42f614a9c658281e5ad8d14b17894ddf87d0b025已独审、真实clean五命令source gate全exit0后正常push；run39968606273/job120464971553于14:19:12UTC创建，14:20:38普通页仍InProgress。consumer普通job要求Sign in to view logs，未登录/绕API/raw/download；原测量记录待父级已有登录页面回收。准备原件Library libfile_92e707969fec8191a0be715e101ceb3b v0，301653B/SHA25602954a6ae950a74a91b929ca792fe97f910c4290d6759168865c875e2b17f110，16成员15manifest5Git源及真实来源门独核。仅1方法/投递证据，不含原生测量结果或PNG；无第二次CI。

有限诊断继续片已完成候选：原run39968606273的27文件/43块/431输入已消费者独核，321.2715ms真实停顿、113px短文测量及整体失败保留。工具到稳定终态却无合格中间态时锁存Failed，Completed仍null并在原预算内继续独立采集；反向前提失效NotRun零Click。源/Gate与动画epoch、最新完整文本测量匹配、原close/cleanup和最终exit3门保持，生产/主题/workflow未改、无新CI。原probe同321ms新方法1F→1P；独审工具epoch P2同方法2F→2P，最终27P=旧19+新8，交叉App/工具0W0E15.64秒，856输入无漂移。中途增量构建误复用原红DLL的19P1F已保全并明确不计候选验证；后强制重建。暖橙色全页面、功能分区优先及快译无宠物作为下一独立UI片，不混提交。详见validation/overlay-stages-preparation.md末节。

有限诊断工具冻结4762f45ca2ae8683956f69832d50bfccfff63a0a及原件已独审正常push；Library libfile_73caa283cec88191bf7c8a14d7e0f42e v0，5351718B/SHA256e4e4f73612b7dcd138f2ed53b93e4b64161e1dcd2978127201de3e0af549e816。62成员61manifest、6Git源856输入、旧失败ZIP及四轮原TRX/日志/DLL机械独核通过。包标DoNotRun和precommit BuildParent，不冒称Windows成品/新原生通过。此收据只两文档，不另测/构建/CI。

PNG同epoch独审P2已修候选：按当前moving样本与phase/epoch取图，最终IssuedAnimationEpoch区分合格/保留无效原图；真正issuance期间同epoch中间态保留。双端验证before/after epoch，完整16格合格集及两列表共用原预算，不以无效图补位。仅源码反例，未声称历史原生发生。旧逻辑提取同6例2P4F→修复后相关27P（新6+保留21）；Python同方法6失败子例→最终9方法通过；859输入无漂移，App/工具交叉build0W0E16.58秒。生产动画/时钟/主题/workflow/pins均不动，无新CI/来源重绑。原生效果和旧长文问题未验，暖橙UI另片。完整原件与范围见validation/overlay-stages-preparation.md末节。

修复冻结99f8e8f30e5020a352ccb9df7fc41323195d0e77已独审正常push；原件Library libfile_e1b82d814e908191956931933bbaa881 v0，2210862B/SHA2567547dfe08f6424bb677aab8f4765ac7a34a6f90bee74bb2ff4b27a74cc96b258。42成员41manifest/7Git文件/859输入/红绿原件均机械独核，含测试与工具DLL，App仅摘要已明示，DoNotRun/precommit BuildParent保留。创建与本地身份保存成功，本收据仅两文档，无追加测试/CI/来源重绑。

2026-10-24独立暖橙UI候选：基于040553d，仅5生产UI文件＋原2报告，七设置页共用导航/功能分区/反馈/主保存层级，浮窗正文与辅助工具区分；背景和方向分隔均非hit-test，不改正文测量/几何/动画/事件/红绿和高对比。既有离线11P＝配色2＋布局9，最终App/工具交叉build0W0E8.42秒；测试时857输入及原Canvas版build分列保留，最终背景层修正只影响未被该定向测试编入的OverlayPresentation，未借记原构建。独审限定ACK。无push/网络投递/CI/原生/模型/安装；像素/窄大字/设备和动效性能未验，旧图不代签。见validation/desktop-refinement-2026-10-23.md末节。

父级独审暖橙7d519cf后单独授权Git发布：实际远端040553d、exact7文件/.github0确认后普通push一次，读回7d519cf1cd2e0c697ee29d9bdc0d09ff5ca3b0e9；无workflow匹配改动，不投递被拒Windows诊断。随后ShowSource仅本地候选：默认false草稿保存/取消、精确所选文字版本immutable偏好、人工折叠优先、源切换不携人工展开、可读拒绝/重入installed身份退役；图片/OCR不自动展开，不唤醒/采集/发送。r02两P2同方法2F→最终2P；r03旧回滚断言1F保留未改，r04同89集合89P＝新14＋旧75；最终交叉0W0E16.72秒、859输入一致。不叠暖橙11P，不push下一片/CI/原生或模型，实际像素及250/600ms仍未验。原件/范围见validation/desktop-refinement-2026-10-23.md末节。

2026-10-24：ShowSource候选8f21f74b8ae007f1b30076fdf49db5e6b34ea14b已按父级限定授权正常推送并读回。随后仅本地推进SPEC8.2/13.4启动项事务A片：由原Installer固定pins解析、原Operation/Scope引用链及实际G/B验证取得非序列化资格，已有精确自有启动项随升级迁移；首次安装不自动启用。原五步保留，合格SC分支扩为G/B/Chrome/Edge/H/命名启动值/独立owner文件七步，含逐前缀逆序、READY前复核、失败READY的新ticket七步补偿和旧版READY核验；历史关闭Prepared不授写权，Entry退出后恢复会话也拒绝。活Prepared只可生成只读卸载归属计划，不执行卸载。测试仅合成registry和原Linux受控子进程；SC的189个runtime输入从现有缓存逐文件核固定大小/摘要，G仍是结构fixture，不签可运行Windows包。设置/stdio participant、v3设置journal、真实Run适配器与生产启用留后片；普通Launcher及Unavailable/null pins、LaunchAtSignIn=false准入未改。本地候选与固定树实际验证分列记录于validation/startup-qualified-transaction.md；未push/CI/真实注册或Windows运行。

A片源码冻结7a59ad9a66aedfe5ab539422c01849a82de6aceb，仅本地1提交，源/测试独审限定ACK、八角色固定树交叉0W0E；尚未签收。r10同209集合151P58F，58原失败全含/tmp ENOSPC（8.8GiB已满）；r11保原DLL与--no-build改子进程TMPDIR后52P157F，尾部确为Linux socket路径超过108字符，完整分类未再读。两轮原会话均实际exit1，未相加/冒充产品通过。默认sandbox启动随后也因/tmp ENOSPC失败；未删搬残留、未升级执行写入、无push/CI/系统注册。仅apply_patch保全两文档及artifact检查点；本轮报告修改尚未提交。下一最小条件为恢复正常sandbox启动、使用短的本轮专属工作区测试TMPDIR后核路径长度，不绕权限/不重启旧writer。真实Windows/生产pins/普通Launcher信任及设置stdio B仍未完成，见validation/startup-qualified-transaction.md。

随后按父级一次补验授权完成有界恢复：默认pwd/Git可成功但另两启动仍ENOSPC，成功保留原权限shell28678后顺序读取全部原件，确认r10全58为ENOSPC、r11全157为Unix socket路径超长。源码/测试零差异、DLL不变；独占短TMPDIR /workspace/k23530c9a的相关socket79/84/86字节，工作区约20.4GiB余量。r12同209唯一测试ID、--no-build补验209P/0F/0skip（新33＋原176），原进程exit0/406.639秒；退休入口相同测试源码红1F→本轮Passed，旧两失败不回填、不相加。Core和测试DLL与r10摘要完全一致，r12原TRX411342B/SHA256 e8a3cae79fd69abbad0d86beb78f4075a1ec50d85c98dc42a854823c0cc14738。仅本地相关回归通过，不签自启动或Windows；/tmp仍满、旧残留未删搬，shell空闲保留可接续，未再升级权限执行，未push/CI/B片。两报告继续未提交，详见validation/startup-qualified-transaction.md。

2026-10-24：父级完成A片Library原件机械独审，209identity/原TRX/摘要限定ACK；普通推送3b9240cbbad27820259087c70a01e0c97cab1d3e（父7a59ad9），包含A片及两报告，远端读回并同步0/0，.github零差异且现workflow无匹配，不投递被拒Windows诊断。随后仅本地B1闭合原Installer同次合格READY会话的已有启动项撤销：原admission借用、Settings锁前Reserve、原stdio异步路由、双效果及逆向补偿、原IO/应答结算、独立v3意图不进旧SecretRecovery。foreign观察不可洗白、保存主错与暂存密钥清理错保全分别有确切断言红→绿。r11相关237例232P/5F（新16全P，A片33全P）；5旧升级/handoff失败是新夹具误附startup participant，保原资格拒绝并仅修夹具分支，r12原5失败+3对照8P/0F。不同源码/轮次数不相加，不称最终237P；八角色及test项目0W0E，Core/tests DLL两轮相同。仅源码候选待父审，不push下一片、不CI/真实Run注册；生产Unavailable/false门、普通Launcher信任及启用/往返自启动仍未完成。旧session28678已不可用，新普通权限shell28535保全，/tmp仍满、残留未删。证据与准确边界见validation/startup-settings-removal.md。

2026-10-24：父级对B1 3f0b0fa最终可见应答窗口确认P2并暂不ACK。r13同一真实协调器/客户端合成交付反例0P/1F：COMMIT_OK可见、原send未结算时本地v3已删，原件Library独核。修复候选改为持久决定收据＋显式待原控制器结算；App保v3并一次无回复LOCAL_SETTLED绑定原Request/WriteId/规范摘要/固定data/config/本端清理结果。原controller预登记退休任务，实际原进程退出＋原stdio/响应全部join＋原句柄结算后，原owner核精确配置/意图/秘密清理事实，先退休v3再最终归档；未确认及Move未知保全，不从磁盘授资格。r17定向24P/0F，含COMMIT/ABORT原响应先可见后失败、成功释放后真实合成App exit0自动收敛、缺/错通知及本端清理失败；r18补两例丢收据有界观察但原IO仍持有，2P。下一固定树原237＋新增10例仅一次完整相关验证，尚未计通过；不push/CI/真实注册，普通Launcher及Unavailable/false门不启。


2026-10-24：B1原会话结算修复冻结2b9fd009197794953ea72d9f4e62b0a478f96f75，r19固定树相关集合仅一次247P/0F/0skip（旧237测试ID及名称完整保留＋新增10），原test命令exit0、405.313秒，所有观察预算未过期；八角色和test项目九构建均0W0E/exit0。12,290个tracked输入及22个产物DLL清单测试前后完全一致，HEAD固定、工作树clean；原TRX419433B/SHA256 1d016b47177ecb04b243a12520d3924194b1ea4e9492b823b3411c88258d68dd。COMMIT原反例已在本轮通过；ABORT是修复后受控回归，不伪称旧树ABORT红件。应答可见只确认持久决定，本地v3等待同次原App真实退出和原IO/应答结算后自动退休；失败与缺通知保持未知。r13红件和旧r11/r12分列保留，不相加。当前只更新两报告并保全候选供父独审，不push/新CI/真实注册；本轮不签全Core/Provider/Codec、Windows、250/600ms、自启动启用或普通Launcher资格，pinsUnavailable/false门保持。


2026-10-24：父级发现Reserve后尚未Bind/Apply即取消或陈旧草稿失败的空reservation不能退休。r20原生产源码真实stdio两例0P2F（App exit0/originalIO已结算、pending仍在），原件保留。受限补丁831dc588仅两生产＋两测试/夹具文件：仅原owner证明从未尝试Bind/Apply、ABORTED/null-settings、零prefix/attempt且原进程/原IO/响应/句柄全结算，核安装及原pending/decision/缺binding后单次归档根reservation；不造v3/notice、不删本地恢复，不放松已绑定或未知门。r21实际19P2F/21：原15及新正常收敛/背压4例全P，两个新增拒绝测试错期望单异常，实际保全双错；只修断言为精确双原错，生产零变化，冻结201628a60706fa28e9bb335d5554a0e0cf4a2280。r22仅原2失败＋2正常对照4P0F/44.355秒，九构建0W0E，全tracked输入及22DLL前后不变；不拼成21P、不借旧2b9的247P。不push/CI/真实注册，完整差异/三原TRX供父独审。未知根未删，原Unavailable/false及普通Launcher门保持。详见validation/startup-settings-removal.md新节。
