# 已提交部署与原受控会话健康（限定切片）

原部署最后 committed archive Move 仍是唯一部署成功点。本片在它已经返回后另开一个原 Task：只有原 Scope 的确定 `Committed` 才能一次绑定原 coordinator；磁盘 archive、SHA、`CommittedReceiptUncertain` 都不能重建这个私有授权。没有新增 App/安装器/注册入口。

绑定原 operation、Prepared SHA、G/current、B owner/active、H receipt/两槽及原 controller/session launch。原正常 admission 不绕 pending；完整核已提交记录和 G/B 后才 Start，沿原 `StartAndCheck → BrowserHostSession.Publish → 原 READY callback`，全部成功后再按取消/实际退出/一次仲裁接受 READY。READY 是已就绪时点的历史事实，后续用户关闭不自动授权回滚。

| 会话结果 | 含义 |
|---|---|
| CleanExit | 曾接受原 READY，并且原会话真实退出及原工作结算 |
| ActivationBlocked | 本次未取得自己的 execution，且本次原资源结算确认；不能清理别人的 owner |
| RollbackRequired | 就绪前失败，原实际角色/协议/任务/Host 退役已结算，原 execution 对象和租约继续持有；补偿仍未执行 |
| Unconfirmed | 原资源、任务、归属或关闭未确认；原对象保留并继续阻断 |

`SessionWorkSettled` 不包含故意保持的 recovery execution lease。`DisposeAsync` 不能丢该租约或凭记录字节重新认领。取消后原任务迟到 fault 与 body/cleanup 错误分别收集，单错保 EDI、多错 Aggregate；只针对新的 deployment-bound 分支，普通会话合同保持。

实际候选：r03 编译失败零用例；r05 两红含旧夹具关闭错，历史保留；修正夹具后的 r06 两个确定性反例均 Failed，r08 同完整 testfile 两例均 Passed（未知 admission 误报 ActivationBlocked、迟到原 publication fault 丢失）。随后只加强夹具原 read/write/flush owner 并追加两条边界，r09 21P、r11 3P 分列，不能相加为一轮。实际子进程是既有合成 stdio 应用；held Settle 屏障在 client 底层 IO 已结算后，不能称 raw IO 被 hold。原产物/命令/日志/TRX/源码与输入摘要保留在 `artifacts/2026-10-22-deployment-session`。候选轮数不借记为固定结果。

未实现后续三方 postcommit 补偿：仍需新 Ticket、原 registry owner 重入、exact committed targets/旧 G/B 闭包复核及原 execution 的精确退役合同；不能复活旧 Scope、重用旧 Prepared 或调用 current-only Rollback 冒充 G/B/H 回滚。Unconfirmed 没有同 owner 重试提升接口，本片不承诺自动恢复。真实 Windows/medium/浏览器/Explorer/WPF/OLE/OCR/画质及250/600ms未验，3 native DLL/完整自包含 runtime 缺件仍不以结构夹具签可运行包/P7。无真实用户注册/模型调用/新许可费用权限。


固定产品 **b036357d998289f9e6192aeb23e0b484c170035f**：r12 七普通 managed 构建各0W/E；r13 一次 **191P/0F/0skip**＝新22＋旧169，七类为 Session82、StableLauncher19、Publication54、RegistryConflict4、ExitSettlement6、LauncherSettlement4、新DeploymentSession22。不是完整Core，未重跑旧262/310全量。旧三个原TRX的选定集合按multiset union核169完整保留；其余339旧testfiles逐字节未变。新22中11例有真实合成子退出/原IO记录，包含原pub callback内诊断等实际退出0后仍拒签READY；诊断wait不代替原Lifetime fence。

固定轮从clean树开始；918输入前后相同并逐项等产品Git，17目标版本含完整产品SHA；14产品DLL建测摘要相同，Launcher/Tests/两个fixture实际Core副本相同。相对基线仅2旧src＋1新src、1新test和3报告；所有tools、依赖锁、workflow、packaging、extension、项目、脚本、SPEC/AGENTS均未改。r06/r08红绿完整testfile同SHA fc1356198c0cde5a85625dc4fce9054bcd87609214400c51c0b6e2376722a124；最终testfile另加强原IO夹具、追加held/exit边界和API命名，未冒称最终文件与该旧pair相同，两原反例在r11/r13重新通过。

独立只读审查限定ACK：实际核ZIP CRC/226项manifest、9项Git源码、raw191和旧169集合、同file红绿及固定输入/版本库存；没有独审重跑产品，也没有包内DLL本体可另行重hash。原件 **Library libfile_9df832f4936481919ad31636349a170a**（v0，2,155,069B，227成员/226manifest，SHA256 `8a999dba70b089616c9776e8e146cb6a44d3bccd4d34e4965df65cf04b54b8e4`），本地Library身份已核。包中product-source三报告是产品提交时的候选快照；本次后续证据提交仅更新三报告，不重写该原件历史。
