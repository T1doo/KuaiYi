# 图片窗口迟到状态与启动线程修复

基线 `2c99f9ed76b99443dff9159d6b2627f0af0e6548`。监督方确认其244项（Core167/Provider77）、8项真实Linux Worker证据及17工程托管构建；另指出下面两项。此次不标记P0或任何Windows整体验收通过。

## 已确认的原问题及实现

原窗口成功分支比较本地long version和checkbox，但catch的错误/取消直接改status；finally/退出轮询也有未受同等身份检查的状态写入。原窗口调用的RunAsync在第一次await前Process.Start，所以原“startup off dispatcher”注释不成立。两个问题属实；Windows桌面原现象未运行，不制造Windows失败/通过记录。

将已有ProviderEventReceiver的实际队列逻辑提取为Core.IdentityEventReceiver<T>，Provider门面也调用同一实现。图片ImageUiWorkflow的开始、完成、错误、取消、退出未确认都经过它，在目标队列执行时调用RuntimeGate.TryApply，原子复核session/version/request/epoch和activation。成功不再另设long/checkbox资格判定；错误/取消没有直写分支。显式本地checkbox使用Gate.TryEnableLocalOperation，不伪造HTTP origin或授予任何endpoint consent；采集、网络门控未放宽。

请求执行通过Gate.TryLinkRequestCancellation自动关联当前请求。生产窗口真实调用该workflow；字节读取/Worker操作异常统一转换为事件，状态和内容只在Dispatcher接收回调写入。用户Off/关闭/主动启用是显式本地动作。资源清理与事件资格分开：具体操作持有自己的worker/identity，ReferenceEquals只用于资源所有权，不用于结果资格。迟到finally/退出轮询只释放本操作资源和更新可用性，不写新任务或Off的状态/结果。未确认退出的恢复消息同样过当前请求Gate。

Process.Start实际放入Task.Run；15秒总截止从启动前开始。启动等待被取消并不保证系统尚未创建进程：客户端保留startTask和启动观察任务，未知时返回ExitUnconfirmed，不允许Dispose冒充退出。真实启动迟返时，把实际Start返回的Process交给共用OwnedHostTermination，仅终止该子进程并等待最多2秒；晚失败则记录有限异常类型并确认没有子进程。启动/停止尚未终结时不释放所有权，窗口保留并禁止关闭/重启；没有打开外部PID或终止未知后代。系统启动调用若始终不返回，保持未确认而非宣称取消完成。

## 实际验证

本轮新增67项：

- 40项，5种图片事件 × Off/B替换/Stop/Lock × 入队前或入队后失效，真实共用接收器不执行旧回调。
- 4项完整身份字段伪造、5项当前事件仅目标队列应用一次。
- 12项真实ImageUiWorkflow确定性任务屏障：A开始后，结果/失败/取消已排队或尚未到达；Off或B完成后，A再终结，当前状态与B结果/写入计数不变。受控任务只用于测试，没有OCR/model调用。
- 2项本地启用配置/锁屏/网络origin仍未授权；1项目标回调异常不吞。
- 3项启动边界。独立模拟UI线程在受控启动阻塞期间能继续处理队列，实际启动线程不同；启动中取消保留未知状态/拒绝Dispose，迟建真实Linux Worker被终止并确认；迟启动失败被观察且不捏造子进程。两项创建真实子进程，一项真实启动尝试失败，没有COM/WinRT。

完整回归Core234 + Provider77 = **311/311**，0失败/0跳过；原Provider77项在共用接收器后重新通过。17托管Release交叉构建0警告/0错误。首次67项也通过；未制造修复前TRX或把代码确认写成实机重现。

本轮审阅ZIP另存 `artifacts/KuaiYi-P0-identity-ui-fix-win-x64.zip`，20个文件摘要和两个EXE实际RT_MANIFEST/PE静态检查通过。它仍缺两个原生DLL、依赖.NET10 Windows Desktop，不是安装包或可OCR的完整交付。旧报告记录的通用ZIP输出路径已被重建，不能拿当前文件验证旧be68摘要；历史源码/原始TRX和摘要保留在相应提交。

原始证据、源摘要与包摘要见 test-evidence/2026-10-11-image-ui-summary.json。本轮待监督独审。Windows WPF调度/界面、真实Process.Start/Kill、OCR/原生DLL/现代菜单/安装均NotRun；全部106整体用例仍NotRun，原E06缺口不变。监督方指定下一可独立项为P0-06浏览器→NativeHost真实Begin/Chunk/Commit接线，待此次两项收尾独审后推进，不安装浏览器扩展或改NativeHost注册。
