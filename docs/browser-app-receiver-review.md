# P0-06 App 单次本地图片接收审阅

基线 `7d2da9b693ccf00f0d6773931193ae5f4de58db3` 已获截图/读取局部独审放行。本轮实现生产接收角色代码与合成离线验证，不注册宿主、不安装扩展、不生成实际ID/信任receipt、不执行真实授权或模型调用。截图fallback仍完整拒绝，不构成图片功能整体验收。

## 配置与系统身份

InstalledHostPolicy.LoadForApp验证Windows平台、App实际执行路径、相邻宿主清单及HKCU注册匹配、精确allowed_origins和11个安装角色文件摘要。缺任一项不创建监听管道；此次Linux实际执行的是UnsupportedPlatform负路径。配置只读，来源和角色配置来自同一份清单解析。

InstalledBrowserReceiver仅接收NativeHost角色，不代替Shell/Broker通用网关。CreateNamedPipeW使用当前用户SID保护DACL、不可继承句柄、首实例、字节模式、overlapped和显式PIPE_REJECT_REMOTE_CLIENTS。WindowsNativePeer从管道取得实际PID/session，持有QUERY_LIMITED_INFORMATION+SYNCHRONIZE进程句柄，复查存活、创建时间、映像路径、进程token用户SID、ProcessIdToSessionId和所有固定模块摘要。Offer之前、Begin、Commit分别重查，不接受客户端自报身份。失败拒绝、不提权或放宽ACL；安全/绑定失败停止监听，P0界面暂无监听恢复设置流程。

系统查询和文件摘要I/O不保证同步可取消；接收状态机的30秒截止不能证明已终止系统核验调用，App保留任务所有权，实际耗时/关闭须Windows证据。会话事件线程立即关闭门控，再调度UI清空，避免旧队列先于锁屏UI处理；失败本地Off会同步checkbox显示。

这些Windows原生调用只有源码和交叉编译证据，**NotRun**。Linux测试采用自己的临时实际管道与内部合成peer输入/可变复核结果，不能证明Windows DACL、远程拒绝、SID/PID/session或安装角色核验已通过。

依据：[CreateNamedPipe标志](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-createnamedpipea)、[实际客户端PID](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientprocessid)、[客户端session](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-getnamedpipeclientsessionid)、[进程token](https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-openprocesstoken)、[包装原生管道句柄](https://learn.microsoft.com/en-us/dotnet/api/system.io.pipes.namedpipeserverstream.-ctor?view=net-10.0)。没有接受法律协议或申请账户权限。

## 逐次确认和有限威胁模型

argv origin、allowed_origins、文件摘要只是声明/配置/文件匹配。已核验NativeHost也可被同账户其他程序手工启动，不能证明Chrome/Edge来源或用户手势；本轮不以父进程链猜测来源。当前账户完全受控、进程注入、替换自己的安装配置或伪造桌面UI不能被完全防御。逐次确认是有限意图许可，不是已认证浏览器来源。

生产DesktopLocalConfirmation每次显示已核验宿主PID及**声明**origin，明确仅在刚主动操作时允许、只接收本次最多20MiB PNG到内存、不识别或发送模型。拒绝为默认按钮且取得焦点；允许须显式点击，没有自动回答、持久同意或浏览器传入consent的路径。关闭/取消/截止撤销prompt，迟到decision只被观察，不发凭证；已On也必须逐次确认。有运行或退出未确认的OCR Worker时拒绝。

App目前仍是P0 OCR实验窗口，新增“关闭并清空”控件在初始Off/prompt/传输期间都可撤销，不依赖提交后checkbox变On。会话锁定/解锁都取消并清空，解锁不恢复On。关闭等待接收任务实际结束后再释放窗口。真实WPF焦点、键盘、锁屏、退出与确认操作未运行；合成Decision不能证明已有人在Windows上同意。

## 单次凭证、资源与网络

Offer只有operationId/origin，无图片/路径/URL/激活状态或身份。确认等待时并行读取以检测失联，Ready前任何额外输入拒绝。回答后复查原epoch和单调截止，在RuntimeGate锁内建立**本地**完整session/version/request/epoch，生成随机nonce。服务端grant绑定本连接ID、operationId、身份、nonce、30秒总截止与单次消费；任何字段变化、跨连接重放或重复Offer/Begin均拒绝。

Begin/每个Chunk/Commit复查身份、取消、单调时间，Begin/Commit另复查安装角色。输入上限20MiB、190KiB分片、索引、SHA-256和声明/实际PNG头尺寸、24MP/16384边界均验证。这里只验证PNG头与协议完整性，不是完整解码、P6规范化或OCR质量验收。中途IPC读取失败清零实际分配缓冲；控制/二进制帧、未完成重组和未接纳图片按所有权清理。

Commit在目标SynchronizationContext实际执行时重查身份、取消和**单调时间**，避免定时器回调延迟放行。排队时继续监测EOF；Off/替换/锁定/截止/失联撤销排队并清零。失败清理只停止自己的身份，不取消较新请求，失败本地grant恢复Off。一次成功Commit关闭连接，grant不能再用；正常完成后只保留当前一张有界内存图片，Off/下一来源/窗口关闭清零。提交或ack失败不保留图片；正常提交后的宿主退出区别于提交前失联。

App只通过TryUseCurrent在身份门控→inbox锁内短同步读取；原始Current引用仅friend测试可见，后台Clear不能在界面读取中途释放缓冲。LocalImageInbox没有文件持久化、OCR调用或Provider引用。RuntimeGate本地模式禁止被动采集、CanSend及TryBeginSend，已有端点同意也不继承网络资格；只有独立明确端点启用才能恢复网络门控。接收链没有该启用、配置密钥或模型调用。HTTP适配器仍独立检查实际origin/完整身份，本轮未接到接收图片上。

## 证据与剩余边界

新增65项在Linux使用**实际IpcFrameCodec和临时NamedPipe**运行实际DesktopReceiverSession；正路径使用实际DesktopPipeChannel交付相同字节。只有系统peer复核、逐次Decision、目标队列和时钟为明确测试控制。覆盖拒绝/缺配置/角色变化、恶意字段、完整grant各字段在Begin/Commit变化、跨连接重放、单次终态、Off/lock/替换/截止、图像尺寸/摘要/总量/重复/错序/短传输、排队失联和超时，以及实际IPC缓冲/已接纳图片清零和并发读取保护。TCS/手动时间控制，无Sleep；原stdin/JSON/IPC测试与断言保留。

完整Core376+Provider77=**453/453**，另Node**43/43**，共496项，零失败/跳过。19托管工程locked Release零警告/错误。无新增外部NuGet依赖，App增加生产NativeHost项目引用及锁文件；生产不引用fixture。专项首轮53、第二轮58、第三轮64均通过；完整回归后补失败本地grant恢复Off，再次执行完整构建/回归/打包，最后自审补充界面读取与后台Clear并发的所有权锁保护，增加第65项确定性测试并重新构建/回归/打包，最终源摘要对应最新产物。

最新独立包 `artifacts/KuaiYi-P0-browser-app-receiver-win-x64.zip` 包含App/NativeHost/MV3共34文件及清单，542962字节，SHA-256 `015ee12e88124f76e3044abfbd8bb0125c0d514638a5614d5c0266740dec03e9`。无fixture/凭据/实际ID/清单配置/receipt/注册，缺原生Shell/OCR DLL，框架依赖，**不是安装包**。完整源/原始TRX/JUnit/构建与打包日志见 [证据汇总](test-evidence/2026-10-11-browser-app-receiver-summary.json)。此前各独立ZIP保留历史用途。

Windows管道/角色/真实确认/浏览器图片/锁屏/安装均NotRun。截图缺口、E06同步OLE冻结假设、106条整体NotRun及P0整体Blocked不变。先独审本轮边界再推进真实Windows矩阵；本轮没有实际开放用户Windows来源或网络权限。
