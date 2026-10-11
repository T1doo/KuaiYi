# P3 单调绝对截止独审

基线 `b07f2b556f7bf1aa214d81daaae92e76edff81ad`。修复父审指出的最终交付截止缺口，不推进下一阶段。正常开发分支，仅保存云Linux环境，无安装/真实凭据/模型调用/新协议。

## 原缺口与实际修复

原600ms相对定时器没有交付时复核；已完成Task的WaitAsync可沿完成路径返回，解析与校验跨界也会成功。旧5个确定性用例中4失败/1通过：同步完成read遇预先取消、最终校验600/601ms、最终校验时取消均失败；599ms成功边界通过。原实现只加internal最终校验屏障和结果局部变量后执行，未改原交付行为。旧TRX/log保留，不能把此前timer测试称为绝对交付证明。

CaptureAsync入口只记录一次TimeProvider.GetTimestamp，所有启动/读取/退出/校验共享该起点。剩余600ms预算设置取消唤醒；真正资格由GetElapsedTime(started)与token决定，>=600ms拒绝。启动await后、三次读取结果交付前、退出后、最终UTF8/标量/矩形/RuntimeId校验完成后立即复核。用户取消优先返回Cancelled，否则绝对超时HardTimeout，无原文。绝对时间不依赖墙钟或timer回调，三次read不重置预算。

ReadAsync在CheckDelivery之后才设delivered=true，因此已完成的成功帧在取消/超时时仍交唯一放弃观察者清零。捕获在解析之后失效时由原finally清payload；异常仍被观察。未确认启动/退出时持有所有权并阻止第二writer；原冻结输入身份和双队列准入保留。600ms表示结果交付资格，不宣称Windows/CLR调度或阻塞COM必在精确600ms归还线程。

官方约束核对：[.NET10 Task源码](https://raw.githubusercontent.com/dotnet/runtime/v10.0.0/src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/Task.cs)、[WaitAsync](https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.waitasync?view=net-10.0)、[GetTimestamp](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider.gettimestamp?view=net-10.0)、[GetElapsedTime](https://learn.microsoft.com/en-us/dotnet/api/system.timeprovider.getelapsedtime?view=net-10.0)。完成路径及本机同步已取消read反例共同证明必须独立后置复核。

## 已执行与可复核证据

新增15例：1同步已取消read、4真实公开child最终交付边界/取消、4读取边界/取消、2真实child启动越界、1预先取消不启动child、3连续读取共享起点。全部走生产reader/client，时钟及受控stream仅测试替身；公开child为Linux实际自有进程，不声称WindowsBroker实跑。

定向87/87、完整Core770+Provider77=**847/847**，Node**63/63**，25托管locked Release零警告/错误。初扩87中2失败是清理后查询已Dispose的Process；保留退出断言，改用独立只读观察对象后全部通过。旧5及初扩失败log/TRX全部保留，不删测试或弱化断言。原16个真实子进程用例及前轮输入/读取断言保持，以完整TRX为准。

新staging：`scripts/stage-p3-absolute-deadline.ps1`；框架依赖包 `artifacts/KuaiYi-P3-absolute-deadline-managed-win-x64.zip`，73文件，app44/probe25，9个PE32+x64入口。实际CaptureBroker包含，公开GrowthHarness仍禁用真实选区平台，无fixtures/密钥/原生DLL/安装器。精确大小/hash、逐文件manifest、13旧包、25锁文件、294源冻结及历史commit核对见[summary](test-evidence/2026-10-11-p3-absolute-deadline-summary.json)。

重跑：`scripts/ci-offline.ps1`；只读证据核对：`python3 docs/test-evidence/2026-10-11-p3-absolute-deadline-verify-evidence.py`。GitHub CI未运行。

## 真实平台缺口

[Windows清单](p3-absolute-deadline-windows-checklist.md)全部NotRun。Windowshook/UIA/跨应用兼容、焦点/字体/多DPI、DPAPI/ACL、原生P0/E06、安装卸载仍未执行。P0/E06 Blocked、P1/P2/P3 InProgress，176追踪/106整体NotRun不变。完整OLE文本Drop/复制授权/全热键/P4调度未交付，不称完整P3或可日常使用翻译软件。
