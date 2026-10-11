# P0 主动区域截图审阅

历史基线64cba：监督仅限定签收几何/PNG/本地身份。下文是该轮原始证据，进程内捕获/DwmFlush/无界关闭缺口未获签收；后续隔离修复与新的待审边界见[capture-host-review](capture-host-review.md)。

限定范围：监督已签收 `8d11a811c1813303d64bd9085b4b60e0f50ccd0c` 的确认等待修复，本轮接通用户明确启动的 Windows 框选截图到现有本地 ImageWorker。完整核对 SPEC 3.4、4.3、7.1—7.6、8.4、P0-06/P6-01/P6-02、I01/I02/I08/I10/I11。浏览器截图 fallback 保持全拒绝；没有模型发送、安装、真实授权或 Windows 实测。

## 生产代码闭环

App 截图按钮或显式注册的截图热键 → RuntimeGate 原子截图租约 → 暂停被动采集、隐藏 App、DwmFlush → 按显示器取得静态 BGRA → 每屏独立 HWND 遮罩 → GetPhysicalCursorPos 原始物理矩形 → 原像素拼接裁剪、立即释放全屏帧和遮罩位图 → 无元数据的内存 PNG → 本地确认/预览 → 单次本地 request → 现有 ImageUiWorkflow / ImageWorkerClient / 私有 stdio ImageWorker。

- Win32 物理矩形采用右/下边界不包含约定。负原点和每屏 DPI 独立；遮罩绘制才转换到实际 HWND 的 DIP 范围，裁剪、拼接与 PNG 均不缩放。显示器间的物理空隙输出中性白色，不从其他位置扩展像素。重叠/镜像拓扑明确拒绝，未假定每种拓扑都已支持。
- 首选 C++/WinRT WGC：`CreateForMonitor`、D3D11 BGRA 设备、`CreateFreeThreaded` 单缓冲帧池，等待首帧、严格尺寸校验、staging texture 逐行复制。事件使用共享拥有的 HANDLE，帧、session、pool、纹理、映射和 COM apartment 均有清理路径；关闭失败返回有限失败码并清零输出。
- GDI 是用户从列表明确选择的兼容后端，top-down DIB + BitBlt 原始坐标，释放 DC、位图及临时像素；不在 WGC 黑帧/失败后自动重试。全黑整屏明确报 `ProtectedOrBlackFrame`，也可能拒绝合法全黑桌面；不能判断黑色究竟来自保护还是内容。局部黑色原样保留，不绕过保护。
- 任何边小于8物理像素须重选；裁剪上限24MP、16384边、PNG20MiB；快照总量上限32MP、16屏、虚拟跨度32768像素。超限提示缩小范围，无无限重编码。每次主动获取，没有持续屏幕录制。逐屏首帧顺序获取，未宣称跨显示器同时曝光或 HDR 色彩正确。
- Esc、右键、意外丢失鼠标捕获、遮罩失去应用激活、锁屏、显示/DPI变化、Off、来源替换/epoch变化和30秒框选截止撤销。正常松开先终止共享手势再释放鼠标捕获，避免被误判为取消。取消选区不创建 request、不替换旧会话或结果。
- Off 可本地准备，按钮明确为“启用并本地识别”；默认焦点/默认动作是取消，未自动回答。On 默认松开进入本地 OCR；可选择总是本地预览。预览30秒截止，失去激活、取消或停用都不启用；提交前再次核拓扑和租约。该 P0 行为不冒充完整“启用并翻译”网络链路。
- 成功提交转为 localOnly，已有 endpoint consent 也不能发送模型或启动被动取词。完整身份进入共享 UI 交付门控；截图取消源在提交后绑定 `StopRequest(current)`，旧截图不能取消新请求或覆盖新结果。实际 Worker 退出未确认时保留原有所有权和重启/关闭禁止条件。
- PNG 压缩工作内存、扩容前旧缓冲、最终 PNG、裁剪 BGRA、全屏 BGRA、可写 WPF 遮罩/预览副本均有显式清理。捕获原生调用没返回时保留任务与缓冲所有权，晚到结果先检查租约/取消，失效即清零。系统/GPU/DWM 副本无法保证绝对擦除。

截图热键只在用户点击注册后调用 RegisterHotKey，P0 提供 Ctrl/Alt + A—Z（可加 Shift）子集，建议 Ctrl+Alt+S；占用失败保留旧组合，先注册新组合再撤旧，MOD_NOREPEAT，无常驻键盘记录。清除/退出撤销。完整设置持久化、全部动作/交换事务/AltGr等仍属于后续 P3，未签收。

App 新依赖 Windows.dll/CaptureNative.dll 已加入安装角色完整性清单（由11增至13），两项真实文件篡改负例补入测试；不会让旧 receipt 静默接受新模块。Windows DllImport 搜索限定自身目录/System32。当前无配置/完整原生模块，浏览器接收继续默认拒绝。

## 已执行证据

- 新增 **38** 截图测试：五种 DPI 组合的负坐标跨屏逐像素验证；实际 PNG 解压、CRC、尺寸和仅三种必要 chunk；1000组固定种子物理组合；8像素边界、超区域/24MP/快照限制；空隙、中途取消和拓扑变化；PNG实际20MiB上限、全黑拒绝/alpha；真实已分配缓冲清零；On/Off/已有网络同意；同时争用截图租约；不可取消的晚到捕获；正常/异常鼠标捕获释放。合成输入是公开坐标栅格和受控任务，不是真实屏幕。
- 其中一项把生产裁剪生成的 PNG 送入实际独立 ImageWorker 进程的私有二进制协议及实际 UI workflow。Linux准确返回 `UnsupportedPlatform` 并确认退出；**没有 OCR 成功或 Windows 截图成功声明**。新增2项安装角色篡改测试，原断言保留，只将有效完整清单数量精确改为13。
- 全回归 **Core416 + Provider77 =493/493**，Node **63/63**，556个独立子项，零失败/跳过。19工程 locked Release 零警告/错误。最后只补 App 的可见 owned-dialog 拒绝、滚动容器和固定两项热键 ID/按实际失败原因提示；再次全托管构建/打包，共享代码/已测断言未变，不无谓重复全部测试。
- 身份源静态脚本6项通过；不包含实际注册/安装。锁文件仅跟随新增内部 ProjectReference 传播，无新增第三方包。
- 初次扩展测试构建因测试把 EndpointOrigin 的 Uri 参数误写为 string，CS1503；修正输入类型，未改断言。原始编译失败保存。专项先32、再34截图+9角色=43通过，最后追加4边界后完整回归38截图全部通过。
- 原生尝试为 `cmake -S src/KuaiYi.CaptureNative ...`，Linux实际退出127（cmake不存在）。MSVC/Windows SDK/桌面也不存在，**NativeBuild Blocked，WindowsRuntime NotRun**，不能称为原生编译通过。

精确源摘要、原始 TRX/JUnit/失败日志及包清单见 [summary](test-evidence/2026-10-11-screenshot-summary.json)。复跑 `scripts/build.ps1 -Scope Managed`、`scripts/test.ps1 -Suite Offline`、`scripts/test-identity-source.ps1`；Windows原生源码纳入 `build.ps1 -Scope All` 和安装态组件存在检查。

## 原生与桌面验收缺口

截图总捕获取消源5秒、WGC首帧等待每屏2秒。**这些计时器不证明 D3D11 Map/驱动、BitBlt 或其他同步 OS 调用在截止内终止**；当前在后台线程调用，保留实际任务直到返回，尚未提供可强制终止的独立 CaptureHost。真实卡顿/退出时间需 Windows 测试，不能把 Task.Run 当进程隔离。全屏帧最多保留到框选截止/实际后台返回，真实峰值与释放时延未测。

需要后续授权的本项目 Windows 桌面按自建无隐私栅格执行：WGC设备/首帧/释放；明确GDI；自身隐藏+排除、截框附近内容；双屏100/150/200%负坐标、反向拖拽和边界；8像素重选；Esc/右键/锁屏/Off/拔屏/分辨率或DPI变化；预览失焦及超时；快捷键占用/更换/撤销；缺DLL/包身份/语言；正常Worker与卡住捕获的退出；重复30次截图和内存/GDI/D3D句柄采样；受保护内容和黑帧不绕过。当前这些均 **NotRun**，特别是 PMv2 下 HWND 与框选画面吻合/跨屏鼠标捕获及 WDA 实际效果不能以云端几何断言替代。

审阅 ZIP：`artifacts/KuaiYi-P0-screenshot-managed-win-x64.zip`，465400字节，SHA256 `b9af727d4ccb06ec1b46dfc74015b7e912afbd9c27afb50142e5f2acecdbd718`；27个 App/Worker/共享托管与资产文件、2个 CaptureNative 源文件、一份manifest。缺Capture/OCR/Shell原生DLL、安装配置/receipt，框架依赖，**不是安装包**。原确认等待审阅ZIP保持原字节。

106项整体验收仍 **NotRun**，P0 **Blocked**，E06同步OLE浮钮冻结未解决，完整翻译浮窗/取词、截图后处理/视觉翻译、设置/安装都没有整体签收。本轮提交监督独审，未自行推进下一产品阶段。

## 官方核实依据

WGC监视器互操作核对 [CreateForMonitor](https://learn.microsoft.com/en-us/windows/win32/api/windows.graphics.capture.interop/nf-windows-graphics-capture-interop-igraphicscaptureiteminterop-createformonitor)，D3D接口布局核对微软 [interop SDK header](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/windows.graphics.directx.direct3d11.interop.h)；现有微软 [Win32捕获示例](https://github.com/microsoft/Windows.UI.Composition-Win32-Samples/blob/master/cpp/ScreenCaptureforHWND/ScreenCaptureforHWND/SimpleCapture.cpp)仅作为初始化/释放参考，不作为本实现已运行证据。

物理输入采用 [GetPhysicalCursorPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getphysicalcursorpos)，兼容复制采用 [BitBlt](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-bitblt)；自身排除依据 [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)，该机制不提供绝对内容保护，仍先隐藏。热键依据 [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)。WPF分行清零核对 [WriteableBitmap实现](https://github.com/dotnet/wpf/blob/main/src/Microsoft.DotNet.Wpf/src/PresentationCore/System/Windows/Media/Imaging/WriteableBitmap.cs)的4参数历史重载：名为sourceRect的参数实际指定目标矩形，源从offset起取，避免清零时误偏移源缓冲。
