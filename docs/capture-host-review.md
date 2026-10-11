# P0 截图宿主隔离与 GDI 同步复核

本轮基线 `64cba6584a85a011d1cbe87c78b009f949449f2b`。监督对该基线仅限定签收物理几何、内存 PNG 和本地身份接线；指出截图生命周期另有真实缺口：父 App Dispatcher 上的 DwmFlush、进程内驱动调用以及 Closing 的无界等待。这个缺口与 **E06 同步 OLE 浮窗冻结独立**，本轮没有修复或重新命名 E06，也没有推进下一个产品阶段。

## 真实源码变化

1. 新增实际 `KuaiYi.CaptureHost` Windows x64 控制台入口，asInvoker/PMv2，不注册新的 MSIX Application。父 App 不再调用 DwmFlush 或捕获 DLL；二者在自己启动并持有的 CaptureHost 内执行。生产入口只接受私有继承 stdio，不提供外部命名管道/任意 PID 终止端点。
2. 复用并抽取原 ImageWorker 的启动、实际进程所有权与退出终结机制。实际 Process.Start 在后台任务中；取消等待不丢失原启动任务。必须同时确认启动任务已终结、持有的子进程已实际退出，才能释放 owner。已发布但启动未终结的 Process，即使已经退出也不能复用。失败的启动任务始终被观察。只终止自己 Start 返回的 Process，不按输入 PID 查找/终止，不杀未经确认的后代。
3. 截图客户端默认全流程5秒，成功的快速异步操作也复核单调截止；退出等待预算2秒。nonce/operation/epoch/App PID/HWND/backend 必须一致。64KiB 控制帧、1MiB 分片、逐屏次序/长度/SHA256、32MP/16屏/16384边及虚拟跨度先验证，再分配。完整 Commit、真实正常退出和当前截图租约缺一不可。任何部分帧、超时、取消或未确认退出都不交付截图。
4. 不可取消的读仍由原读取任务观察，迟到 payload 清零。部分像素、快照和临时分片在失败/取消时清理。这里只证明我们拥有的用户态数组，不能保证操作系统/GPU/DWM 的所有副本被绝对擦除。
5. App 取消立即投递恢复窗口，不等原生返回；投递执行时复核同一个 captureStop，旧回调不能恢复新截图窗口。重复已注册热键是隐藏期间的取消入口；没有注册热键时，按钮启动后的捕获错误恢复依赖全流程预算（界面此时隐藏）。真实热键与界面响应需 Windows 实测。Closing 最多等待捕获任务3秒，未结束或未确认退出时保留窗口，不再无界 await。
6. 退出未确认保留 owner，阻止新截图、OCR 和本地浏览器确认；可显式重试终止同一个 held child。当前租约对应的严重失败原子转 Off 并撤销旧请求；陈旧租约失败不能关闭替换后的身份。定时器只终结资源，不提交迟到像素、改写旧状态/结果或重新启用。
7. 安装角色完整性由13增至17项，CaptureHost 的 exe/dll/deps/runtimeconfig 全部要求摘要匹配，新增实际临时文件篡改负例。旧完整清单明确拒绝；没有配置/receipt 时浏览器接收继续拒绝。新增工程仅有内部引用，无新增第三方包。

## 自身隐藏与排除契约仍需真实像素验证

先枚举本 App 当前可见顶层窗口，拒绝主窗口之外的可见本进程窗口；设置并确认 `WDA_EXCLUDEFROMCAPTURE` 成功后 Hide，在 Dispatcher 空闲点之后检查实际 HWND 的 owner、不可见、layered 样式和可读的 affinity 值。CaptureHost 用 Toolhelp 核实际 OS 父进程，保持父进程 handle，核父 App 的相同目录角色路径、创建顺序及会话；在子进程准备前、DwmFlush 后和捕获返回后再次核 HWND，且要求父进程没有可见顶层窗口。无法核实就返回 `HiddenWindowNotVerified`，不回退到“尽力捕获”。PID/HWND 是比较数据，不是调用者自行声明的权限。

官方 [DwmFlush](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmflush) 只等待调用应用的排队 DirectX 更新，**不能当作父 App 已隐藏绘制的 fence**。本实现明确没有这种证明。官方 [GetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowdisplayaffinity) 要求 layered HWND 和 DWM 合成，也不保证绝对保护。P0 评审窗口因此在创建前设置 AllowsTransparency/WindowStyle.None，并有拖动标题与关闭按钮；仍核实际 WS_EX_LAYERED。WPF [AllowsTransparency 契约](https://learn.microsoft.com/en-us/dotnet/api/system.windows.window.allowstransparency?view=windowsdesktop-10.0) 要求 WindowStyle.None。该最小评审窗口不是正式翻译浮窗。

**元数据检查成功不等于父窗口像素确实已从捕获画面消失。** Layered WPF 与 WDA 的实际兼容性、DWM 首帧时序、WGC/GDI 自身排除和画面中主窗口原位置必须在 Windows 用公开栅格验证，当前全部 NotRun。不能用子进程 DwmFlush、源码 Get/SetAffinity 或 Linux 进程测试签成这个门槛通过。

## GDI 同步修复

实际 BitBlt 后、任何 DIB bits 的 CPU memcpy 前，在同一绘制线程调用并检查 GdiFlush。失败绘制/取消路径也先同步；flush 失败不读取像素。析构路径在清零 DIB 前再次同线程 flush，失败就禁止 CPU 清零、标记清理失败、拒绝输出；SelectObject/位图/DC 清理失败同样不报告成功。取消前已有效的调用也清零 caller 输出。WGC 黑帧失败仍不自动改用 GDI。

依据 [CreateDIBSection](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-createdibsection) 对直接 bits 访问的同步要求和 [GdiFlush](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-gdiflush) 的调用线程批次语义。生产 C++ 使用 `GdiReadContract.hpp`，可移植测试编译同一 helper，断言 CPU 读/清零不能遇到未完成绘制且回调同线程。7项实际 g++ 执行通过；断言不受 NDEBUG 禁用。故意把 flush 移到消费像素之后的**合成变体**退出3、断言失败；这不是旧 Windows 原生 DLL 的运行证据。原始 native 源码单独保留。Windows 真实 DIB/BitBlt/GdiFlush 及原生清理仍 NotRun；同步失败时也不声称 DIB/GPU 绝对擦除。

## 已执行与未执行

- 新23项 CaptureHost 测试使用真实独立 .NET 子进程、继承 stdio、生产 CaptureHostSession/客户端/生命周期与实际 OS 退出。受控工厂用公开栅格；阻塞“compositor/native”是明确合成阻塞，**没有调用 Windows DwmFlush、WGC、GDI**。测试含四项取消/默认5秒截止、实际晚启动/失败启动/已发布退出但启动未终结、Kill/Wait 的合成失败、不可取消真实管道迟到 payload 清零、七种身份/角色/大小/摘要/分片/Commit 错误、明确隐藏失败、预取消、单调截止、当前/旧租约失败。
- Kill/Wait 故障适配只在测试中包住实际 child；清理只终止那个真实 child。模拟 UI finally 是可移植控制流验证，不能签成真实 Dispatcher/窗口恢复通过。原 ImageWorker 22项启动/终结严格测试先通过，然后保留在全回归。
- 首次 Windows 托管编译缺 System.IO；首次 fixture 编译缺 Core 引用；专项31中30通过/1失败因测试清理重复访问已释放 Process。修正清理先核已确认 exit，保留所有原断言；之后31/31。扩展3项首次测试编译写错三个已有 API 名称，按真实接口修正，最后23/23。另有一次合成 C++ 变体编译命令后误附空命令，退出127；已单独实际运行变体，退出3。原始错误记录均保留。
- 全回归 **Core441 + Provider77 =518/518**；Node **63/63**；另有 C++共享顺序契约 **7/7**。21个托管工程 locked Release 零警告/错误，身份源静态6项通过。无跳过，无真实模型调用。原截图38项、先前所有协议/身份/扩展用例均保留。
- Windows实际 CaptureHost.exe/角色/SID/可见性/WDA、驱动卡顿后的真实退出时延、取消/Off/关闭窗口、多屏DPI、截图内容、GDI/D3D资源、OCR、安装卸载等均 **NotRun**。本云没有 MSVC/Windows SDK/交互桌面，原生完整构建仍 Blocked。进程隔离提供可监督的边界，不能保证损坏系统驱动在 Windows 总能被 kill。

新框架依赖审阅包 `artifacts/KuaiYi-P0-capture-host-managed-win-x64.zip`（571372字节，SHA256 `fbfae658f27fcacb3b3a01ea17ee93e156624932803bece0b8f2eb4300eac347`），36个文件，含实际 App/NativeHost/ImageWorker/CaptureHost x64 PE apphost、PMv2/asInvoker CaptureHost 与3个原生源文件。无测试 fixture、真实秘密、配置/receipt、原生 Capture/OCR/Shell DLL；**不是安装包、不能据此宣称截图可用**。完整 native 构建仍从源码仓库执行，审阅包中的原生源码不构成独立可编译工程。原截图 ZIP 与确认等待 ZIP 的 SHA256 原样保留。

复跑：`build.ps1 -Scope Managed`、`test.ps1 -Suite Offline`、`test-gdi-contract.ps1`、`test-identity-source.ps1`、`stage-screenshot-probes.ps1`。Windows原生构建入口 `build.ps1 -Scope All` 增加共享C++契约 ctest，不含任何安装/注册操作。精确源摘要与全部原始证据见 [summary](test-evidence/2026-10-11-capture-host-summary.json)。**176追踪项保持完整，106整体验收仍 NotRun，P0 Blocked；本专项待监督独审。**
