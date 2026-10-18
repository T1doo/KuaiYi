# 恢复独审：正常会话关闭收据与原任务结算

2026-10-18 用户明确恢复后，只使用原保存环境。实际 HEAD 和远端 `dev/installed-session-launch` 均为 `e8350adce77ac42f8f05da0ea67215a57257751e`，直接父为 `369796e2fed19d433e66a7174930af5c2bfcb69d`，工作区 clean、单 worktree；本任务是唯一写入者，既有 reviewer 只读。重新核对已提交729原件/14,228,145B和67源码哈希，无缺失或改写。从 e835 正常建立 `dev/session-close-receipt`，不恢复旧实例写入，不改 main。

## 对 e835 结论的追加修正

原“无剩余 P1/P2”结论不覆盖这次新增的两个调度路径。本轮独审重新打开并实际复现两项 P2；原报告、索引、729原件及全部更早证据原字节保留，不回写历史。

第一项是正常关闭误拒入：`SettleForExitAsync` 先 `CancelAsync`，再用 `loop.IsCompleted` 决定发送 CLOSING。如果原循环因本次取消先完成，会跳过收据。子进程实际 Exit0、原 IO 可结算，但父正常会话因缺收据永久保留 owner。原24例使用的真实 Console stdin 不能覆盖可取消 read 的这一调度。

第二项是异常会话误归档：原控制循环遇无效命令先请求同源关闭，再抛协议故障。关闭任务可在 requestClose dispatch 返回之前进入，发送 CLOSING；随后原循环 fault 被观察并记录，却仍允许 Shell Exit0。父只验证 CLOSING、实际退出与原 IO，看不到子端失败，错误清除 owner。本轮通过真实 stdin 无效命令、原 stdout 与明确调度屏障实际复现。

r03 实际8例为3F5P。上述两项均有实际 Exit0 和原 IO 日志：第一项应成功却 Unconfirmed；第二项应 Unconfirmed 却成功。第三个失败 TERMINAL_MISSING 是新最终收据门槛的前向合同用例，不另算第三个独立 P2，且最终 Theory 方法修改过清理期望，不能称同方法字节红绿。两个主要独立反例的测试方法从红到最终绿逐字节相同，精确 SHA/长度见机器索引与独审原文。

## 最小生产修复

沿用既有 App 入口、实际 Start 返回的 Process、原 client/coordinator/Shell，不新增产品角色、服务、扫描、自动重启或守护进程，不使用 COMMIT 或 ReleaseToReceiver 分支。

在唯一缓存的原 settlement Task 内，先快照本次取消之前是否需通知，再取消。CLOSING 只请求精确父 client 结束该 child 的 stdin，不再代表结算成功。必须在原取消任务和原控制循环都 join 后，非 committed、未记录失败，才能经原串行输出发送精确 launch/fingerprint 的 `EXIT_READY`。整个 Task 被复用，8个并发 Settle 调用及后续重复调用不另建成功收据。

非预期 EOF 在请求原 Close 之前记录 ControlEof；即使它与关闭 dispatch 重叠、或循环尚未完成，也不能变成健康终态。仍允许失去观察方的子窗口沿原 Close 退出，不伪造可恢复证明。已 committed 分支及其持久拒入边界保留。

父正常会话 client 另外核验 CLOSING→EXIT_READY 顺序、两者唯一且精确、无额外控制行；继续要求实际 Exit0、原 stdout/stderr 自然结束、原任务成功与精确 owner/current/pending/package。缺失、错误、提前、重复收据、协议故障、非预期 EOF、取消、清理或最终归档失败都保持拒入；不以 PID、EOF、lease 释放或关闭消息替代退出。

## 测试与证据口径

新增9例，原24例及文件原有全部内容作为精确前缀保留，仅追加方法。其余父测试文件未改、无断言降低。新例覆盖：取消先结束循环后仍可连续两次正常真实会话；原控制循环故障与关闭重叠；EOF先完成与EOF/close重叠；最终成功收据缺失/错误/重复/提前；最后 owner 归档 File.Move 目标冲突。归档冲突中实际 Process/IO 已结算，原 owner、指针、公开冲突文件全保留，fresh 拒入。

取消竞争用例在实际选定 child 内使用明确的可取消命令读取端口，并在本次取消后/收据决策前等待原循环完成；原 START admission 仍走真实 stdin，没有启动后遗弃的 underlying read。仅测试调度 hook 在生产 App 永不配置。该例不是 Windows Console stdin 取消实测。协议故障反例走真实 stdin/stdout，只扣住原 Close callback 与 CLOSING 写入的相对顺序，不另写关闭算法。既有 App helper 与 Shell partial 的真实合成 parent/child 两次会话仍在原24例中执行。

EARLY/DUPLICATE 会使原 reader 抛 InvalidData；JoinSession 观察原双流后传播该故障，原 Dispose 正确保留 Unconfirmed。测试进一步核实际 Exit0、错误类型、owner 原字节和 fresh admission 拒入，只关闭自己 Start 返回且已退出的 Process，公开 fixture 根保留，不删除失败锁。这不是把 IO fault 称为成功清理。既有丢父用例也继续保留公开目录，只证父实退及子私有 CLOSED 收据，子 OS 退出仍 Unobserved。HoldIo 仍仅扣 adapter Task，未声称原 stream reader 阻塞。

全部尝试保留：r01 是 fixture nullable/捕获局部变量编译错误，r02 是 xUnit1031 阻塞 Result 分析器错误，均无 TRX，不算产品红；修正为显式 null contract/await，没有关闭分析器。r03 为实际3F5P。r04 为139P2F：新增 reader-fault 两例错误期待成功 Dispose；随后改成严格核 Unconfirmed 和故障来源，没有改生产 finalization 或吞错。该 Theory 最终与 r03 不是同字节。两主要反例方法保持不变。

正式最终五轮是 r05相关141P（132+9）、r06完整Core1843P（父1834全名多重集+9）、r07完整Provider2332P（父名单完全相同）、r08/r09 App/Worker交叉构建各0 warnings/0 errors，无 skip。五轮各67源、335绑定、23目标DLL；实际选定 child 的 driver/Core 收据另与执行元数据核对，不把构建哈希本身当运行证据。独立源码终审无剩余P1/P2，封存另审。见[机器索引与全部原件](../test-evidence/2026-10-18-session-close-receipt.json)。

## 未验与未改变的边界

Windows/WPF/真实 Dispatcher/STA、可见健康/键盘、实际 Console stdin 取消、Job/OCR、截图、DPAPI/ACL、物理250/600ms与真实模型仍 NotRun；同源便携端口不能充当 Windows 现场验收。真实注册/签名/扩展ID、稳定入口/绝对路径再生成、完整 payload、生产安装器/Inno、自启/卸载/K组与完整P7未推进或未验。

历史图片/idle/Core/Job及旧 r10/r17/r20 根因各仍 Unknown，本轮实证协议缺陷不证明那些间歇失败的根因。生产图片/分类复用 Off，250/600ms、来源/密码/完整性/取消/清理门保持。无 main/强推/部署/费用/真实模型/新增凭据/安全权限/新依赖许可操作；Skia4.153.1条款及源码403未解决，不绕过、不降低隔离；拒绝的 Actions 不查询或换路，未知锁/数据/日志/进程保全。

原r04失败TRX含xUnit输出的两行尾随空格（1530/1539）；保留其原始字节，仅该单一机器TRX路径设置-whitespace，源码/测试/其余路径检查未放宽。

最终封存独审Pass（653原件/660路径），依约仅追加回执和既有stage-audit后共655原件/13,668,794B；代码、测试和653已审原件未再改。父10515路径及全部9599旧证据原字节保留。
