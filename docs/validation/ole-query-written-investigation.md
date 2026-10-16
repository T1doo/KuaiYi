# OLE QueryWritten 间歇回归调查

从已独审 `9bf335c1a64c634fb2cfaa88caaef1721cbcdbb9` 接续，先核clean、单worktree/root唯一写入者、实际HEAD/默认远端及main祖先，正常建 `dev/ole-query-written-investigation`。本次调查父任务r08的 `RealParentReadDistinguishesStaleQueryFromAcceptedProductionFeedback`：收到正文后清理子进程，最终集合只有QueryRead/QueryStaleEpoch/QueryAccepted/DropRead/DropAccepted，缺QueryWritten。

**实证结论：原测试存在跨通道同步假设缺陷；历史r08那一次的具体调度仍未知。** 未修改生产 `src`、协议、取消/终止、诊断留存/解析或任何期限。只让原测试在清理前有界等待父端实际解析的QueryWritten收据，保留全部原断言；新增显式测试夹具屏障与正反对照。没有把Written提前到stdout flush前，没有合成观察、加长生产宽限、单纯sleep后宣称成功或放宽断言。

## 同步、进程和日志链

1. 实际Server只有一个串行WriteLoop：写stdout帧并Flush，随后对QueryEnd/DropBegin调用可选诊断Observe，再写后续帧。
2. Observe去重并TryWrite到32项有界队列；另一个Task.Run负责写入/Flush完整 `KYOT1|<固定枚举>\n` 到stderr。当前formal只会有QueryWritten/DropWritten，不支持“队列已挤满”假说。
3. 父端stdout读循环独立产生QueryRead/StaleEpoch/Accepted并交付DropEnd正文。由于writer串行，正文已交付时前面QueryEnd的诊断调用应已走过；不能仍解释为停在QueryEnd的flush→Observe之间，除可选诊断异常分支。
4. 父端另一个原始任务drain stderr，只有完整且规范的换行记录才保留；缺失或截断始终未知。stdout交付不保证诊断线程已写出或父端已解析。
5. 测试Cleanup先Cancel，必要时直接Kill其Starter返回的持有子进程；生产StopAsync也直接终止持有子进程。随后等待drain结算无法恢复子进程尚未写出的队列。没有操作未知进程。

原r08/r13的log/TRX/binding逐字节另存，本次不改旧原件。原r08缺每阶段时间、子stderr队列/写入状态和可选诊断异常的观测，不能倒推唯一原因；r13绿色也不能关闭根因。当前受控实验的父端单调ticks、持有子进程PID、退出态、原drain任务状态、固定错误码及有限观察集合写入TRX；测试夹具独立status仅含固定公开metadata。父ticks不冒充跨进程统一时钟或真实Windows/COM记录。

## 有界实验

| 运行 | 实际结果 |
|---|---|
| r00 | 保存旧r08/r13精确原件、9项父源码Git blob/SHA与初始父/子实际DLL路径和哈希 |
| r01 | 先不编辑/重编译，用上次r13已验输出（不是旧r08二进制） `--no-build --no-restore` 运行原目标测试20次：**20P/0F**。未自然复现，不据此关闭旧根因；未采逐次事后DLL哈希，绑定r00初始实际输出及无build命令 |
| r02 | fixture-only stderr首字节屏障，真实owned子进程/原生产server/client：**2执行1P/1F**。负对照证明正文已交付而目标写入停在首字节前，清理后ExitConfirmed、drain RanToCompletion、Failure=None，记录仍缺失；复制旧清理顺序的强断言实际失败，集合与r08相同 |
| r03 | 相同完整名称/相同原断言，正例改为显式释放既有epoch3禁用控制、等真实父解析收据后再清理：**2P**。生产及fixture DLL与r02完全相同，仅Core.Tests DLL改变；**不是同测试源码字节红绿，也不是产品生产修复红绿** |
| r04 | 保存实际DLL映射及当前生成AssemblyInfo/SourceLink。初始输出和新编译SHA不同；当前生成元数据标记9bf335c，不能仅因src未改就称所有二进制相同，也不声称已重构历史r08二进制或差异仅为元数据 |
| r05/r08/r10 | Core/Provider/App locked-mode恢复；无依赖/锁变化 |
| r06 | OLE相关专项 **52P/0F/0跳过** |
| r07 | 完整Core **1556P/0F/0跳过**，父1554项完整名称多重集保留，仅新增2项调查case |
| r09 | 完整Provider **2157P/0F/0跳过**，父完整名称多重集原样保留 |
| r11/r12 | App、ImageWorker Release交叉构建各 **0警告/0错误** |
| r13 | 修后原目标测试固定20次 **20P/0F**；不是根因关闭证据 |

屏障只在显式 `formal-stale-query-held-diagnostics` 测试模式包裹stderr，在生产writer已取到 `QueryWritten` 的完整固定记录、实际写任何字节前停住；不阻塞stdout或改Server。只在hello.Epoch+2且disabled的明确控制释放，初始disabled和Cancel的long.MaxValue都不会释放。负对照在未释放时终止；正例先证收据任务仍pending，明确release并见实际written status，然后仍必须等父TargetObservations里的QueryWritten。status不是收据替代品。helper最长4秒，每5ms重查实际谓词；若drain已EOF而无记录立即失败。测试不会为通过而从QueryRead反推或补写QueryWritten。

## 源码与实际DLL

r00逐路径保存相关父源码的Git blob/SHA256；r02/r03与最终运行各保存真正加载目录的父Core.Tests/Contracts/Core/OleTransfer及子fixture/Contracts/Core/OleTransfer SHA。父子核心依赖逐一核同，红绿生产和fixture DLL逐一核同，测试修复导致Core.Tests变更明确记录。初始自然基线使用上轮实际输出，重编译后的程序集不能用初始哈希代指。旧r08实际二进制未留存，本次只保全当时原binding中的摘要；不重造旧二进制冒充原件。所有生产源码保持9bf335c原字节。

封存独审通过：141原件与工作区/暂存区大小、SHA及字节相同，生产源码全保留。全量`diff --check`退出2，仅三份必须原字节保存的generated AssemblyInfo原件含EOF空行；9个非原件源码/文档路径检查退出0，没有改原件或加whitespace覆盖。

[固定证据、原件与源/DLL逐项映射](../test-evidence/2026-10-16-ole-query-written-investigation.json)记录所有运行、屏障正反对照、历史原件、最终完整名多重集与独立审查。这里只修已证的测试同步缺陷；缺日志的历史r08具体执行原因仍Unknown，真实Windows OLE/WPF/COM及完整E06/P5仍NotRun，旧图片间歇根因亦未实证、生产图片复用Off。

Skia4.153.1四包许可待批、源码403不绕，不安装/运行；Actions先前拒绝，不查询调度或换路，run数Unknown。原250/600ms及来源/密码/完整性/取消/清理门、全部P6状态保持。所有dotnet激活前后显式DOTNET_GENERATE_ASPNET_CERTIFICATE=false，证书库/信任Unknown且不读改；无main/强推/部署/费用/凭据/安全权限/新法律协议/真实模型，未知锁/数据/日志/进程保全。
