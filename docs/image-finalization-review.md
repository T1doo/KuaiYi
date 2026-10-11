# 图片子进程清理竞态修复

基线 `2609bf86c86f1466f24f30f25898c81811580b69`。监督方已核上一轮311项及主要修复，另发现子进程已发布、已退出但启动任务尚未终结时，ObserveExit可能提前返回true，导致窗口finally或DispatcherTimer调用DisposeAsync抛出ExitUnconfirmed。本轮只收尾这一竞态，P0-06接线等待独审。

通过internal测试屏障，暂停在实际Process.Start返回并发布owned/termination之后、StartOwned返回之前。等待真实Linux Worker的公开diagnose子进程退出，启动任务仍被屏障保持。旧ObserveExit谓词未改，仅加测试插桩，修复前1/1失败（Expected false / Actual true）。精确编译源、原谓词一致性说明和失败TRX已保存；不是Windows/WPF失败实测。

ObserveExit和ExitState现在同时检查启动和停止任务已终结，再确认所持有子进程退出；RunAsync的finally也不再因原始退出状态Confirmed而跳过完整检查。新增TryFinalizeAsync原子复核并释放，未知时返回false、保留所有权；实际窗口的finally与200ms轮询均使用此边界，取消了ObserveExit→DisposeAsync分离检查。新任务/关闭仍被所持有客户端阻止。严格DisposeAsync仍在未知时抛出ExitUnconfirmed。清理异常记录有限类型码，不清空资源或伪造成功。

新增19项：1项直接验证发布/退出/启动未终结时ObserveExit=false、TryFinalize=false、严格Dispose拒绝；18项成功/失败/取消 × 当前/Off/B替换 × finally/轮询路径，验证真实子进程所有权保持、排队事件身份隔离、清理不抛出到模拟UI回调、旧事件不写Off/B状态结果。轮询路径另执行一次重试tick。释放屏障并等待实际启动任务完成后，才断言观察退出和幂等释放成功。19项均创建真实Linux诊断子进程；UI队列与结果是明确的测试控制，不是OCR成功或WPF桌面证据。

首轮修复后测试19项中16失败、3通过，原因是测试finally错误假设“释放屏障”等于“启动任务已完成”。保留该次TRX及精确测试源；补充等待实际启动Task，保留全部用例和原有断言。修正后专项19/19及完整Core253 + Provider77 = **330/330**，0失败/0跳过；17托管Release工程0警告/0错误。

另存审阅ZIP `artifacts/KuaiYi-P0-identity-finalization-win-x64.zip`，SHA256 `b05cec5ea92006f8f23d5a941e46f7c698ae70b60b9f995a16d2ea7194cbc3e9`，283431字节，20文件摘要及两个实际EXE身份资源静态检查通过。仍为框架依赖、缺原生DLL的部分产物，不是安装包；上一轮独立UI-fix ZIP保留。原始TRX、构建日志、139项源摘要及包清单见 [证据汇总](test-evidence/2026-10-11-image-finalization-summary.json)。

真实Windows启动/终止/WPF调度、原生OCR/Shell编译、OCR/菜单/安装仍NotRun；106整体用例全部NotRun，P0整体及E06同步浮钮冻结缺口不变。未安装、注册、信任证书或调用真实API。
