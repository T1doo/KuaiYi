# P7-01 warm 单图：A 片 Core 与原 stdio

实现基线为 `96a0b3b9b42d48da49b650126062322a9304e619`，分支 `dev/p7-fixed-signoff`。本片只实现内部 opt-in 的状态与原受控 stdio；生产 App 没有调用 AttachWarm，没有外部监听、live-session capability、Windows peer 认证或 UI 接纳接线。合成 peer/owner 仅在测试工具使用，不能当作授权。

## 实际修改与安全边界

Core 新增严格 canonical JSON/Base64 有界消息、单 slot/零队列、连接一次消费和 ordinal 高水位。两端使用各自 TimeProvider 创建并保留原 timer；grant 为10秒，commit 后 prepare 为独立15秒，比较单调时间的恰到边界，旧 grant timer 不撤销 Preparing。已消费连接、旧 grant、替换请求、过期均拒绝；没有自动重放或 cold fallback。

Reserved/Preparing 的撤销在状态锁内先标 Revoked，锁外启动同一 CancelAsync。Accept 同锁先赢才记录 AppOwned；之后旧取消不清归 App 来源。原 work、取消注册/回调、timer DisposeAsync、finalize 和终态 write/flush 全部实际结算前不清 slot；finalize=false 只可显式重试同一 owner，不能重跑 prepare。正文与清理错误同时保全，故障收据不冒充成功清理。原 registry 有固定上限，历史不累积路径或任务。

InstallationApplicationControl 保留唯一 Output reader，同步 demux WARM 回包；START/STOP/IMAGE/OFFER/WARM 共用只跨单次 write/flush 的写门。原取消不等写锁；STOP、EOF、fault、WARM_QUIESCE 先封入场，随后各端独立 join，child 不等 server，server reader 不等 child 清理。所有正常/失败 IO 结算均观察原 warm 任务。原 cold IMAGE/OFFER one-shot 不放宽，首次内部 warm reserve 后永久拒绝后到 cold IMAGE/OFFER。公共 Run 不附加 warm actor 的回归保留。

App/Windows、coordinator、原安全文件打开、Worker、Codec、OCR、发送门、依赖锁均未改。812个基线输入中809个字节不变；现有生产文件只改 InstallationApplicationControl，另外两个现有改动为测试 Port 与合成 fixture。新增9个文件，执行输入合计821个。旧163个 Installation 案例全名多重集全部保留，原 Fact/Theory 正文未改，新增37个有区分力的案例。详见原件内 final-protection.json。

## 原始失败与定向红绿

各轮不相加、不覆盖失败：

| 轮次 | 实际结果 | 证据含义 |
| --- | --- | --- |
| r00 | Core build 0W/0E | 初始构建，原命令/DLL目录未捕获，不补造 |
| r01 | 16P/1F | flush 屏障观察到 FinalWrite 尚未发布；改为调用写工厂前发布原 Task，原 NotNull 断言保留。不能说原 retirement 未持有 await |
| r02 | 编译失败，未执行测试 | xUnit2013 将 collection-size Equal 改为 Single，不创造测试计数 |
| r03 | 21P/12F | 17状态+4actor通过；12个stdio失败包含 finally 查询已 Dispose 的 Process，可能掩盖正文。保留全部失败XML，不把12项均归为同一原因 |
| r04 | 10P/2F | 原件保存 finally Broken pipe、CLOSED=null、实际 child137。准确源码支持错误等待 lifetime.Cancel→RequestExit 的夹具假设；被 finally 掩盖的原异常没有直接原件，不追认唯一原因 |
| r05 | 1P/3F | 只投影恢复两处独审 P2 的旧逻辑：held prepare 新请求误抛 InvalidData；本地15000/15001ms未投递timer仍报 PreviewReady。14999ms通过 |
| r06 | 6P/0F/0skip | 同4个反例修复后通过，加父端 write/flush 背压：直接持有同原 RequestExitAsync，取消不等写锁，最后显式 join 原 STOP，保留正文+清理双异常 |
| r07 | 200P/0F/0skip | 完整 Installation 相关回归，旧163全保留；exit0，40.525s，执行821输入不变 |
| r08 | App 交叉构建0W/0E | exit0，16.773s；不是运行 Windows/WPF |

r05 原 TRX SHA256 `106ebba4c3639b7359c414c1bfe2ebf79f823b4cdbcee495822307187ce01dab`；r06 `1b78fec3f70c4d9a0ae648691f2a39a44407454acab5918abaf295cec49d5a3f`；r07 `c0f94e106ae1fdeb8135dded5dc874a7102f294db936563b1f1482424b940c57`。r03起每轮完整命令/日志/TRX/准确源码/输入与DLL摘要保全；r00–02原日志、可用TRX和源码照存，缺少的原命令/DLL目录明确列 early-run-limitations.json。

测试覆盖10/15秒刚前/恰到/刚后、手动延迟timer、两种 cancel/accept 胜序、旧 timer 跨 slot、跨连接 grant、Busy无队列、原work/callback/timer销毁持有、同owner false finalize、双方原 write/flush 背压、丢回执不重放。真实 child 与原 stdio 确实运行；prepare/source/owner 信号为合成，不能称真实 Worker 或可见预览。丢回执用例要求 child exit0 后 controller 仍报告原协议不确定并保留 lease/fence；不吞成 clean success。

## 独审与后续门

独立只读审查发现并修复 Quiesce 原子撤销、原 write 提前发布、同 owner 清理重试、child timer 自主结算、Busy pending 和 client 本地 deadline 六处问题。最终源码及 r05/r06 原件限定 ACK，r07 案例保留与原件另行复核。reviewer 未执行测试/构建或写入。

A 片完成后仍须先交父侧独审，才能进入 B。B 必须补真实双向 held process/token/logon/session/IL、选中 generation/live owner/launch/两把持有租约及认证连接一次消费，不能由 Show/指纹授权；实际 AcceptFileImage 逐调用链、WarmSync→Gate 锁序、异常 candidate 的图/来源归属尚未实施审查。完整设计约束见[已审候选](../design/p7-warm-image-admission.md)。

P7-01 与完整 warm 仍 InProgress；真实 Windows 菜单→WPF/安装来源身份/OLE/COM/OCR/画质/250/600ms/600MiB、完整 P6/P7/106/Beta 未验不变。默认Off，不自动OCR/发送，PackageHold、生产复用Off、WebP奇数payload拒绝保持。不操作用户电脑、不调用真实模型，不新增许可/凭据/权限/费用/CI，不改main、不强推、不部署。

直接原件索引：[A片命令、摘要、TRX与ZIP](../test-evidence/2026-10-21-warm-image-core-a.json)。本检查点尚未执行本A修复后的完整Core；将在源码与原件独审后固定提交仅执行一次，失败先定位，不循环凑绿。
