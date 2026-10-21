# 固定默认 Program／真实 Shell 的 Windows 分段验证

准备中，尚未执行 Windows。本次产品 src/tests/packaging/依赖锁与固定 `92e3548845bd07ee7717b53df9588c412d128f9d` 无差异。仅给既有 GrowthHarness 加显式 CI 模式，引用默认生产 App；不编译 isolated/平台实验开关，不走 `--platform-review`，不放宽 `--prepare-image`。

驱动明确为 **GrowthHarness EXE 内同进程调用默认 `Program.Main([])`**，因此有真实默认 Program、KnownFolder Settings、默认 Off 与原 Shell/Hotkey/Selection/Browser/清理，却不是独立 App.exe、安装布局、外部 CLI、warm 或 Explorer 验收。真实 KnownFolder 的 KuaiYi 目录须 Win32 CreateDirectory 独占新建成功；已有目录、reparse、未知文件均拒绝。仅此临时 runner 的新目录，保留 marker 与 fixture 数据到 job 销毁，不改 LOCALAPPDATA，不清未知目录。

管道组件直接调用固定生产 ReadToken/Current/CreateServer：真实自身 High/elevated 必须被原策略拒绝；唯一随机 fixture 名使用原 first-instance handle，第二 bind 与请求附加 create-instance 权的连接须精确 Win32 5；仅读原 DACL protected/single exact logon ACE/0x12019b。允许的 fixture 连接与双向 PID/session 明确 same-process；实际构造器仍 High 拒绝，不授予正向认证。不输出 SID/SDDL/token，不造、降权或借用其他 token，不改 UAC 或桌面权限。

UI 驱动只绕过选择文件对话框，将三个中性输入交给原 StartOpenImage：公开 PNG、运行时生成的双帧/偏移/透明 GIF、仓库字典增长/交错 GIF。factory 仅记录后返回原 CreateFileImageOperation 的真实对象。原 LocalImageFile→真实 ImageWorker/Skia→原 finalize→原 AcceptFileImage→WPF 预览全部保留。在首次实际可见 Image 的 Rendering 边界记录原 Worker SettlementSnapshot，再结合固定源码的 finalize-before-accept 合同；不把单一后验 ExitState 当独立时序证明。两帧渲染、首帧可见标签、完整逻辑画布/透明白底和独立 palette oracle 逐项检查，仍不代签真人画质。

持有同一原 OwnedFileImage 的 pixels/png、显示的 WriteableBitmap 与原文件 Task。原 ClearCurrentContent 后核原内存及位图逐字节归零、来源/owner 清空、原 Worker 退出和全部 IO 结算、Off/无生成。错误保留正文与清理两轨。harness 只把 Application 的测试消息泵设为 OnExplicitShutdown，待**原 Shell.Close、closeRequests.Current、startup/positionSave**全部成功结算才 Shutdown；失败不强清 owner，不 Environment.Exit 假报成功。外围 120秒超时仅说明该原自有树清理未确认，不能签收。

CI 仅一次 public 标准 windows-2022、原固定 checkout、contents:read、attempt1，15分钟 job／14分钟 step／13分钟内部总预算；绑定唯一父 `6c61f3d45716a7fb1e41603b35d3e0486fc9aae4`、精确八文件范围及固定生产源码。复用原 SDK402 精确 ZIP/许可/hash/安全解包、双 runtime 正常在线验签和 all/low/warnings-errors 审计、locked restore；不改旧 runner 的来源门。显式合并默认 App exe/runtimeconfig 与真实 Worker build closure，所有既有碰撞必须字节相等，不能盲覆盖。只构建必要目标和运行此 harness，不重刷 Linux 全套、不执行 Linux-only Codec 用例、不新增许可/费用/凭据/安全权限或模型请求。

正向 warm 仍缺既有 medium 入口而 Blocked；独立 App.exe、安装包、Explorer 来源、OLE/OCR、250/600ms、600MiB、真人画质和完整 P7 均不由此片签收。

源码独审限定 ACK；错误 image 使用的仍是 High peer，只证明组合输入被拒，不能独立签 image-path 门的区分力。新 runner 的离线来源/公共首轮/碰撞保护 3个方法通过；harness 必要交叉构建 0 warnings/0 errors，Windows 尚未执行。准备中的首次缺 assets 和 JsonSerializerOptions 编译歧义失败日志保留，不回填为成功。
