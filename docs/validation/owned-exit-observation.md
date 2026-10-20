# 原退出等待结算前保留实际进程句柄

在继续核验完整Windows Core日志与当前生产调用链后，确认一个独立的共享生命周期P2：`OwnedHostTermination.StopCore`只等待`owned.WaitForExitAsync(token).WaitAsync(budget)`的外层预算，超时后丢失原等待任务；随后`ObserveExit`仅凭`HasExited`授权`OwnedProcessSession.TryFinalizeAsync`释放实际Process。原等待仍可能使用这个句柄。该类被Capture、OLE、Selection及安装会话等生产调用方共享。

最小修复缓存唯一`WaitForExitAsync(None)`，仅对调用者等待保留原预算。原任务未结算则不确认退出、不释放、不重复Kill或建立另一个等待；迟到fault／cancel先观察异常并保留固定FailureCode，再结合真实HasExited。先于外部Kill／Wait发布待结算门并序列化观察，覆盖重入窗口。已退出无需Kill、Kill失败后晚到退出、等待完成但HasExited=false等原合同保持。同步Kill／HasExited及永不完成的原观察不被声称有界；后者保留owner是有意拒绝释放。

| 实际既有Linux SDK10.0.402／host10.0.13执行 | 结果与边界 |
| --- | --- |
| r01红 | 新增3个原wait迟到success／fault／cancel及1真实child路径，实测4F；生产与父1462682原字节一致 |
| r02绿 | 整个相同测试文件字节10P，含6旧合同；真实child退出137／originalWait仍pending时finalized由True变False |
| r03受影响八类 | 205P1F／206，新增重入测试通过；唯一失败是Capture旧测试端口无限Delay(None)，finally虽杀死同一实际child，合成原任务永不结束，新门正确拒绝释放 |
| r04最终定向 | HostTermination＋Capture共34P／0skip；531项最终输入逐字节匹配；相对r03仅改Capture测试端口为同一真实child.WaitForExitAsync，原主体拒入与cleanup断言保持 |

没有重复完整1919套件，也不把205P1F改写成最终206P；全部失败原件保留。真实Process反例只证明实际Start返回的子进程和句柄生命周期，不代表真实桌面或模型验收。

完整Windows日志65376B／SHA256 `cdccd22476d8a7cff884b794e4e57cce29703af51c39362351e9f02d7b9d615f`已保全。本轮定位不把这个新证实P2归因为历史45次失败调用／25方法或其中25次ReuseCrash：此前日志缺实际childstderr／启动路径／底层异常，Windows旧CI仍Fail／EvidenceIncomplete／cleanupNotConfirmed。图片间歇根因也仍Unknown。

父级已转达用户对同范围免费CI的明确批准（Sentinel_5c59c687f4348191a5698c044f52c1a7），接受原WindowsSDK10.0.402与原隔离方式；不补签旧结果。先独审并保存本生产修复，再以确切source SHA作为CIrunner的精确父提交，单独独审四文件CI增量并运行一次原public windows-2022／job15min／step14min的验证。失败先读完整日志，不盲重跑；后续同范围必要免费修复CI已获授权。

374保护输入、九workflow／runner／SDK安装器／锁和其他生产源码保持；本source提交不改YAML，不投递CI。生产复用Off、250/600ms、来源／密码／完整性／取消／清理门保持，包Hold。无新SDK／新许可／凭据／安全权限／费用扩张、无用户个人电脑或真实模型操作；真实App／WPF／键盘／完整P6P7仍未验收。

[红绿、全部原件与源码绑定索引](../test-evidence/2026-10-20-owned-exit-observation.json)。
