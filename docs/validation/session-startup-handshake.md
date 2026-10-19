# 启动握手：保留原异常与实际结算状态

仅修改既有 `InstallationSessionTests` 夹具。此前 `ChangedExactRecordsAreRetainedEvenAfterCleanActualExit(current.json)` 的8秒Healthy等待发生在记录改写之前；原会话若早已失败，未完成的Healthy信号会遮住更早异常。现在同时观察原 `RunActiveSessionAsync` 与Healthy，原run失败时传播同一异常对象，并记录实际启动阶段、client admission／health receipt原任务、owned child退出／原IO结算状态和公开fixture哈希。客户端收据任务与coordinator预算包装任务分别解释，生产1秒协议预算不变。

独审指出哈希／序列化／日志本身也可能覆盖原异常。本轮隔离所有诊断采样与写入失败，单独保留 `DiagnosticFailure` 后裸throw原异常。只对 `PUBLIC_SESSION_STARTUP` 抛错的测试输出端口验证这个路径；没有新增独立探针框架、改生产协议或降低原记录／归档断言。

| 实际Linux SDK402／host13执行 | 结果与证据边界 |
| --- | --- |
| r01 | patch断言在保存前中止，过滤器执行0例；Exit0不能算红或成功 |
| r02→r03 | 原held-ready后端，同反例方法字节 **1F→1P**；原run为Health阶段Timeout，Healthy未完成，实际child退出137、原IO结算；传播同一原异常 |
| r04唯一完整诊断 | **1919P／0F／0skip**，保留旧1918全名并增加1例；自然Healthy故障未复现，未再反复全量跑绿 |
| r05 | CS0649编译失败，无TRX／未执行测试；不算红测 |
| r06 | 会话类 **35P**，含日志隔离与第二反例，但为字段形式的中间字节 |
| r07→r08 | 同日志失败反例方法字节，实际 **1F→1P**；r08两新例合计 **2P**，531输入与最终源码全部逐字节绑定 |

r04先于日志隔离和第二Fact，不能称最终字节完整回归；r06先于字段改自动属性。最终字节只做相应定向验证，不以更早绿代最终完整。受控held-ready只证明等待／诊断遮蔽机制，**不能证明旧自然失败的原因**。旧r05完整1917P1F和原件保持；根因仍Unknown。App源未变，沿用前轮r07实际隔离App交叉构建Exit0／0W／0E，未新跑专门构建或WindowsApp。

父级正常登录桥接的[一次Windows CI39226078160/job117972581384](https://github.com/T1doo/KuaiYi/actions/runs/39226078160/job/117972581384#step:3:43)完整Core日志现已精确回收：八片ASCII哈希均Pass，gzip5167B／SHA256 `1c2306060c1b02a074296cd8c9e5028bf5eec8fbe2d37608853c1a89c5ef1a0c`，原文65376B／SHA256 `cdccd22476d8a7cff884b794e4e57cce29703af51c39362351e9f02d7b9d615f`；CRLF原字节保留。直接重核45次失败调用／25个方法名，仍非最终suite计数。约5.22秒即有xUnit失败，Core随后180秒超时；CoreTRX缺失、EvidenceIncomplete／cleanupNotConfirmed。日志没有足以归因25次Reuse Crash的具体childstderr／launcher／底层异常；OLE最终cleanup断言也可能遮住此前异常，不能归因某一个结算门。Windows根因仍Unknown。

补充前轮路径测试的证据精度：四个 `alias/../...` 反例中的 `..` 同时触发EndsWith-dot原组件检查，4F→布局49P不能单独隔离别名分支覆盖。原四个无 `..` 的别名测试保持，Windows精确PUBLIC.用例尚未重验。

2026-10-19 09:44:54 UTC同一实例最小只读命令实际成功，HEAD为c4dfc445，修复与日志都在；没有重启或终止任何未知进程。分支未配置upstream，按明确远端分支核对。369保护输入／九workflow／runner／全部锁／生产源码保持。不查询被拒Actions，不投递第二次WindowsSDK CI；本次tests／docs变更不命中该workflow自身YAML过滤。包Hold，图片间歇根因Unknown，生产复用Off、250/600ms／来源／密码／完整性／取消／清理门不变。真实WPF／键盘／WindowsApp／模型／完整P6P7未验收。

[全部红绿、无效尝试、源码快照、回收Windows日志及完整性索引](../test-evidence/2026-10-19-session-startup-handshake.json)。

独立只读源码及封存审查Pass，无剩余P1/P2；审查时53精确路径／47原件12,356,734B均匹配。审查消息另作第48份原件保留，审查未执行SDK／网络／CI。
