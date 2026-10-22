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

实际候选：r03 编译失败零用例；r05 两红含旧夹具关闭错，历史保留；修正夹具后的 r06 两个确定性反例均 Failed，r08 同完整 testfile 两例均 Passed（未知 admission 误报 ActivationBlocked、迟到原 publication fault 丢失）。随后只加强夹具原 read/write/flush owner 并追加两条边界，r09 21P、r11 3P 分列，不能相加为一轮。实际子进程是既有合成 stdio 应用；held Settle 屏障在 client 底层 IO 已结算后，不能称 raw IO 被 hold。原产物/命令/日志/TRX/源码与输入摘要保留在 `artifacts/2026-10-22-deployment-session`，待固定树签收。

未实现后续三方 postcommit 补偿：仍需新 Ticket、原 registry owner 重入、exact committed targets/旧 G/B 闭包复核及原 execution 的精确退役合同；不能复活旧 Scope、重用旧 Prepared 或调用 current-only Rollback 冒充 G/B/H 回滚。Unconfirmed 没有同 owner 重试提升接口，本片不承诺自动恢复。真实 Windows/medium/浏览器/Explorer/WPF/OLE/OCR/画质及250/600ms未验，3 native DLL/完整自包含 runtime 缺件仍不以结构夹具签可运行包/P7。无真实用户注册/模型调用/新许可费用权限。
