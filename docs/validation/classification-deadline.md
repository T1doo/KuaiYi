# 图片分类独立截止

从 `772436003dbe295a55249d3f5c4d47c712031f7f` 在原环境功能线接续，修复 SPEC 10.7 图片分类默认30秒的实际缺口。此前分类与正式生成仅共用配置总预算，持续有效片段可令分类越过30秒。默认配置总限额120秒；既有更短配置仍优先。本片不把整链上限自动扩大到180秒，不自动降级、重试或多发请求。

## 产品范围与设计独审

生产仅改 `KuaiYi.Providers/ProviderRequestBudget.cs` 与 `KuaiYi.App/ProductionScreenshotCoordinator.cs`。没有修改Shell、Settings窗口、主题、通用样式或纯展示组件，与独立视觉线分开。

`LimitPhase(30)` 从分类开始冻结同一TimeProvider的阶段起点，Remaining取原总预算和阶段剩余时间较小值；Seconds保持原冻结profile的总限额，不通过更改已确认传输配置绕过预算/profile一致性门。子预算、Check、真实timer和事件准入均使用同一剩余值，不因凭据晚返、片段活动或排队重新计时。

分类if内、State之前只创建一次阶段预算与deadline，覆盖Classifying、凭据获取、HTTP读取、每个片段和最终分类提交。deadline token在等待凭据和网络时取消；await前后及每个事件均校验单调时间。目标队列仍使用原lifetime及阶段budget复核，过期事件不能提交分类；超时终态通过原lifetime和原总预算投递。来源取消优先，不将用户Off/清除/换源误作新失败或开始生成。原offer Task、HTTP枚举、lease和图片字节清理仍沿原任务结算。

分类scope结束后dispose该deadline；正式生成恢复原总预算，按时分类不会因正式生成越过分类30秒而误取消。原整体预算不重置、费用确认/来源/密码/完整性/原SHA/取消清理与逐次发送门不变。非法或不完整分类仍停止，不启用自动fallback；图片复用/重生成仍Off。

设计和生产增量已获独立只读限定ACK。测试Rig仅新增可选phaseClock参数；旧调用默认null与原行为一致。没有新依赖、许可、权限、凭据、真实模型、用户电脑操作、安装/部署、main修改或runner403重探。

## 实际红绿和原件

原件根 `artifacts/2026-10-22-classification-deadline/`；各轮保留原命令、日志、完整TRX、失败XML、源码与产物摘要，不累计用例数。

- r01旧生产+新反例：四例0P4F，双协议凭据在30000/30001ms返回、timer不dispatch时，实际分类HTTP=1而期望0；不是测试超时。866输入无漂移。
- r02修复：相同反例文件字节完全一致，6P0F0skip，含29999ms按时分类后30001ms正式生成成功两例；866输入无漂移。
- r03扩展：16例14P2F，867输入无漂移。两个持续SSE用例分别在服务端Write等待和WaitDeadline等待失败；原件保留，不称绿。原WaitDeadline(20)只能证明某timer存在，不能证明指定片段已canonical接收，且fake10秒可匹配分类30秒timer。此机制不能追认历史唯一原因。
- r04仅修新持续流夹具：逐一消费State、实际HTTP Started和每个独立delta-only有效片段后才advance；29.999秒仍在运行、恰30秒终止，两协议2P0F0skip/867输入无漂移。没有放宽时间、sleep、删断言或加生产hook，生产相对r03不变。queued Completed恰30、held凭据的Clear/Off/换源结算、父子预算等其余14例在r03通过，最终固定树已统一签收，见下文。

固定产品/测试后已一次执行原十二类图片/OCR选择及本片阶段、已有总预算/排队准入相关测试与正常App交叉构建。Worker不引用Providers且未改，不重复其构建；完整Core/Provider/Codec均NotRun。

## 验收边界

仅Linux合成内容、真实offline loopback双协议、受控时钟与交叉构建。不是Windows实际像素或桌面体验证明；WPF、UIA、OLE、medium warm、Explorer、浏览器、实际OCR/模型质量、250/600ms与整体P6/P7仍未验。本片未改前端视觉，实际像素与演示由独立视觉线验收，不能借用本片测试替代。

固定产品/测试 `07dcfe0dab4a117a0c039c0beefa03f4bf4767c7` 的r05相关17类665P/0F/0skip/exit0；原图片/OCR501完整保留，本片新增16，另148是既有预算/准入回归，不能称新增164。r06正常App Release交叉构建0W/0E/exit0。867输入各阶段前后及最终=逐Git blob，Providers/ProviderTests/App三份目标版本均含完整SHA；339个DLL摘要当前一致，仅为库存。未重复完整Core/Provider/Codec或未变Worker构建。

固定原件逐条TRX、501旧集合+16本片+148既有回归、867输入/Git、3版本及339项DLL摘要已获独立只读限定ACK；未重跑产品，后续仅三报告与封包核验。

最终封包独审限定ACK：117唯一成员、CRC及116项manifest摘要全部通过。Library工具确认 `libfile_1973521c2fa48191ba6a6bc661a159ca/v0` 保存成功；ZIP1,291,053B，SHA256 `f786fdf2ec541974b5d298c6edb461640f02e1fbfead7db84437b4fdba625108`，当前技能helper保存本地身份后摘要不变。包内报告是保存前快照，之后只追加保存回执，不循环改包。
