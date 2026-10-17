# 既有 App 入口内的一次正常安装会话

父 `369796e2fed19d433e66a7174930af5c2bfcb69d` 经独立复核关闭真实 Shell pending STOP P2 后，从该已推送祖先建立正常分支 `dev/installed-session-launch`。只推进用户授权的普通启动→退出→再次启动闭环。未改 main、注册/签名/扩展 ID、安装器或稳定快捷方式路径。

## 生产路径

无参数 App 从规范 `versions/<GuidN>` 布局运行时，在创建 WPF Application、Shell 和取得 UI mutex 之前，进入同源 `InstallationSessionEntry`。该对象由 Program 静态持根，复用 `ReadActive`、`InstallationApplicationClient` 和协调器，只为本次用户会话创建一个受控 App 子进程。受控分支不会递归父入口，只有子 App 取得 UI mutex、创建原 Shell。开发布局仍沿原入口；旧或非规范 generation 拒入，不自动修路径或回退开发运行。显式诊断/平台 review/probe 生命周期保留原边界。

协调器必须全新；新会话在任何 session latch 或 owner 变更前拒绝已有角色/租约/升级状态。持 admission 与 execution 原租约，读取并验证实际选定 generation，写入一次 launch 证明后启动固定 App role。握手、健康、原启动任务和 IO 都仍属于该次实际 Start 返回的 Process；不按 PID 开进程，不收养锁记录，不将 EOF、关闭收据、lease 释放当作退出。

正常使用时保留原 `WaitForExitAsync(None)`，会话没有 10 秒运行总限。startup/cleanup 的 budget 只是分阶段等待限。用户成功关闭必须收到精确 launch/fingerprint 的单个 CLOSING，实际子进程 Exit0，原 stdout/stderr 自然结束且无多余控制行，全部保留原任务和启动观察任务成功完成，再核精确 owner/current/pending 及 package。最后才归档本次 launch、结算 Process handle、精确归档自己 owner 并放租约，允许下一次新会话。

新会话保留已完成的 fault task，不套用旧 rollback 只核完成的门。失败/取消锁存；即便随后自己的 child 与原任务可以收完，后续 Dispose 也不会清持久 owner。迟到 Start、超时原 adapter task 或未确认退出仍由原对象持有，不能当作已清理。并发 Dispose 立即拒绝忙碌会话，不在活 App 后无限排队，也不偷释放。失败清理只可结束该调用方实际 Start 返回的子进程。

重复用户调用拒入时仍发送原 Show 广播并 return3；只用纯 `ScopeFor` 计算 SID/session scope，父进程不创建/获取 UI mutex，不将广播当 owner 证明。它不会产生第二 Shell。Windows 消息广播/真实 mutex/WPF 时序仍未实测。

本路径没有调用 COMMIT 或 ReleaseToReceiver，也不复用 committed 释放句柄分支。既有 committed 自退仍保留原拒入边界。没有服务、扫描、自动重启、stale 恢复或新后台产品角色。

## 实际验证与独审修正

portable fixture 直接编译生产 `InstallationSessionEntry.cs`、既有 ShellCloseFlow/Requests；私有端口只替代启动可执行格式、WPF/Dispatcher/内容组件，不另写入口/关闭算法。测试实际启动父 fixture 与选定 generation 子 fixture。此证据不等于实际 Windows App 二进制执行。

新增24例覆盖：两次正常独立会话、每次重复启动与只一个 Shell；用户清理首次失败保留同一会话，显式再次关闭后成功；收到绑定 CLOSING 但 child 仍活时不释放；无 CLOSING 的 Exit0/非零退出；健康后取消；Start 返回 Process 迟到；忽略取消的原 health task；已实际退出但原 SettleSession adapter task 未结算；已完成 fault IO；owner/current/pending 改字节；未知 owner/pending/handoff/lock 字节；丢失父观察方；非当前 generation；混用旧角色；纯 scope 不抢实际 child 的 mutex；多余行、错误和重复 CLOSING。

HoldIo 先完成真实 client 双流 join，再扣住原 adapter Task，验证的是原 Task 超时后仍被保留，不宣称真实 stream reader 在该例中阻塞。父观察方丢失用例只 kill 测试自己实际 Start 返回的父 Process；确认该父实际退出和子端私有 CLOSED 收据，明确 `childExit=Unobserved`，不将收据/EOF当子进程 OS 退出。该公开临时 fixture 目录保留，不按 PID 清理子进程；fixture 自身有40秒测试保护。这不是生产定时器/服务。

独审发现并关闭两条 P2：最初候选混用旧 StartActiveRole 后，会因自己的 owner 拒绝新会话，却错误 Dispose 旧执行租约并置 disposed；现先检查全新协调器，不影响旧角色实际收尾。r04 实际运行该初始候选得到1F（预期 InvalidOperationException，实际 UpgradeUnconfirmed）；相同测试方法最终绿。红测在断型之前已显式关闭该实际 child、等待 OS 退出并调用 Dispose，不留下未观察的测试 child。第二条是拒入父曾短暂创建 UI mutex，可能抢在合法受控 child 前；现为纯 scope＋Show，源码终审关闭，portable 实际 mutex child 测试通过，真实 Windows 广播时序不据此标绿。

r01 6P12F 是测试 Peer 在 pipe 连接前设置 AutoFlush 导致的 harness 错误，原件完整保留，不作为产品红绿。r02 18P、r03 127P 是两条独审 P2 修正前中间结果；r03 编译后运行期间曾编辑后续修复，原 source 快照是执行前候选，不能拿它作最终绑定。r05 名称虽含 final，129P 后又加3个输出负例，因此也仅中间结果。正式五轮为 r06—r10。

r06 相关132P，r07 完整 Core1834P（父1810全名多重集＋新24），r08 完整 Provider2332P（父全名多重集不变）；无 skip、旧测试文件和断言未修改。r09/r10 App/Worker 实际交叉构建各0 warnings/0 errors。五轮各67源共335绑定，23目标 DLL；实际 child/fixture Core SHA 另由 TRX 收据绑定，不把构建哈希本身当执行证据。独立源码终审无剩余 P1/P2，封存另审。完整原件及文件保全见[机器索引](../test-evidence/2026-10-17-installed-session-launch.json)。

## 保留范围

Windows/WPF/真实 Dispatcher/STA、窗口可见健康、键盘、Job、原生 OCR、截图、DPAPI/ACL、物理250/600ms和模型仍 NotRun。没有验证真实注册、签名、扩展ID、原生身份绝对路径再生成、完整 payload、生产安装器/Inno、自启卸载/K组或完整P7。当前须从选定规范 generation 启动；稳定入口/路径变更不在本片。

历史图片/idle/Core/WindowsJob及父r10/r17/r20各自根因仍 Unknown，不报告图片间歇失败解决。生产图片/分类复用Off，来源/密码/完整性/取消/清理及250600门保持。无强推/部署/费用/真实模型/凭据/安全权限改变/新依赖许可；Skia4.153.1四包待批，源码403及拒绝Actions不查询或换路；未知锁/数据/日志/进程保全。

最终封存独审已通过：审计727原件/743暂存路径，无剩余P1/P2；仅依约追加原审查回执与既有stage-audit两份原件，最终729份/14,228,145字节，源码/测试及已审727份不改。父9786路径除明确11处外9775原字节，其中8869份旧证据全保。
