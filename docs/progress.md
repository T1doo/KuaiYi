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
