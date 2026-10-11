# 开发进度

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
