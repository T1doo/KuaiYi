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
