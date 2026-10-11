# 开发进度

日期：2026-10-11。分支：`dev/p0-foundations`；初始完整 HEAD：`51d077481cf6303602fcf5d2718213da9cf05178`。

P0 整体：**InProgress，真实 Windows 退出门槛 Blocked**。后续整阶段均未验收。

原始产品书与 SPEC 均为 1826 行、146706 字节，SHA256 `ea3aa6529405bcc703986988fd089f476ee8f991d867e2220b3b20c439126166`；保留原文不覆盖，176 个追踪条目完整。

| 任务 | 当前实际工作 | 实现状态 | 验证状态 |
|---|---|---|---|
| P0-01 | 开发分支、SPEC/AGENTS、精确 SDK、集中最小测试依赖、锁文件、检测/构建/测试脚本 | InProgress | Linux 部分 Pass；完整 Windows 工具链 Blocked |
| P0-02 | 实际 UIA MTA Broker、限定窗口搜索、父进程硬终止、公开文件选区比对探针 | InProgress | 交叉编译 Pass；实际应用选区运行 Blocked |
| P0-03 | 实际非激活/主动阅读/拖动 HWND 生长探针；OLE 未实现 | InProgress | 交叉编译 Pass；Windows HWND/OLE 实测 Blocked |
| P0-04 | 强类型布局、合法方向/平衡、帧级边界、单调高度跟随、实际 HWND 帧采样代码 | InProgress | 10000 组纯逻辑 Pass；混合 DPI HWND 测量 Blocked |
| P0-05 | 包身份、OCR、现代菜单与安装态验证合同 | NotStarted | Windows、签名/信任与安装环境 Blocked |
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
- 证据见 ole-cleanup-review.md 与 test-evidence/2026-10-11-ole-cleanup-summary.json；新提交等待独审。下一可推进项为原P0-05身份包/ImageWorker OCR真实工程与安装态脚本准备；实际安装、证书信任和真实API未授权、未执行。

## 当前边界与继续目标

当前没有可安装的完整主产品、原生 Shell 或浏览器扩展；没有 DPAPI、OCR、真实跨应用与动画帧时间通过证据。真实 API 和质量人工评审未运行，未配置真实凭据。

本轮形成可交付审阅的 Windows 探针和离线协议切片。探针运行说明、实际限制和下一步真实矩阵见 windows-probes.md。P0 尚缺真实 UIA/焦点/DPI/OLE、截图、身份/OCR/菜单及原生工具链门槛；后续生产阶段不越过这些退出门槛。还需生产 coordinator/配置/DPAPI、broker ACL、授权复制、OLE、图片 Worker、浏览器/Explorer 和安装卸载链路。未实现项保持独立缺口，不能以探针 ZIP 代替用户要求的完整产品。
