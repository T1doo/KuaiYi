# SelectionBroker 599ms 测试的双计时机制

从 `269dd3720c1f0288012509c1abc9f8fa7ea582a0`、clean/远端0/0/单worktree接续，唯一root writer，正常分支 `dev/core-selection-deadline`。本轮仅定位并修复原599ms红门，没有重复完整Core或扩展新功能。设计、精确差异及最终源码均经独立只读审查。

## 原失败与实际复现

[前轮固定00f80840的完整Core](../test-evidence/2026-10-21-core-post-fix-signoff.json)仍保留 **2070P/1F/0skip**；唯一失败为 `FinalValidationUsesAbsoluteMonotonicDeadlineWithoutTimerCallback(599,Success)`，首个Equal实际HardTimeout，duration0.7353269s。原TRX没有hook/starter阶段记录，后续held/清零断言未执行，不能从735ms总耗时倒推具体阶段或历史唯一根因。

测试的 `MonotonicClock`只控制Timestamp/UTC；客户端remaining取此时钟，但linked CTS的 `CancelAfter(remaining)`仍是实际计时器。即使假时间未到600ms，真实600ms也可先在Start/读帧/退出/交付阶段取消。仅覆写Clock.CreateTimer不会改变这个CTS。

r01/r02复用原theory(599,Success)与现有DelayedStarter，把同一原Start保持在已进入未返回的屏障；不加sleep、不改生产、不提前创建child。Capture返回时先记录phase/fake/real时间，再释放原Start，随后实际child由原owner接管、终止、结算。r02实测 **1P**，准确捕获原错误期望的EqualException，并断言HardTimeout/hook=false/fake=0/Starts=0/Entered=true/Returned=false/ExitUnconfirmed；当次Capture返回真实时间629.7335ms。独立witness在同一实际child交给生产owner前取得，owner结算后核实际退出。这个通过的反例验证不能记成“599自然通过”或产品绿；它证明原测试没有隔离真实timer，受控复现的实际阶段是startup，不回填历史未记录阶段。

r01最终为 **0P/1F**，错误是诊断末尾查询已Dispose的Process，另有日志准确保留上述旧EqualException。改用独立witness后r02通过；r01日志/源码/TRX未删除，不冒充产品红绿。

## 最小注入与合同

唯一产品差异是init-only `internal ArmDeadlineForTest(CTS,remaining)`及arm处条件分支；**null默认仍原 `deadline.CancelAfter(remaining)`**。public构造、remaining/<=0、HardBudget600、CheckDelivery的>=600、meta250边界、来源/密码/完整性、取消与实际清理路径均不改。还原这两处测试seam即逐字回到原整个产品文件；App没有配置入口或引用。

原三个599/600/601用例只接入手动受控deadline：核arm恰一次、due精确600ms、不发timer回调，仍按原假时间检查原Success/HardTimeout、原文本、hook、真实payload清零与owner结算断言。没有把599期望改为允许HardTimeout。默认真实计时器的屏障反例明确传null并继续保留；原真实startup/IO超时、取消和清理用例原字节保留。

新增startup/final两个受控fire分支取消arm收到的同一CTS；假时间分别0/599（均<600），必须HardTimeout/空文本/无Observation，final实际payload清零，startup原Start/owner到结算才释放。手动controller是同步one-shot，没有后台fire；Capture返回或清理入口即清自己的引用，不在已结束的CTS上迟到Cancel。

三个边界用例与两个fire例另设5秒**测试防挂外部取消**；触发只会Cancelled并使原期望断言失败，不能令600ms错误通过，不是生产预算。失败路径先取消guard、无条件释放同一Start、等待原Capture Task，再Dispose原owner，保全body/cleanup双错误，单错误EDI保栈。既有生产600ms真实timer仍独立验证。

## 最终验证及未验

- r03中间定向 **6P/0F/0skip**，之后按独审补齐guard和原Task收尾，故不与最终累计。
- r04最终相关 **128P/0F/0skip**，为原完整Core中按FullyQualifiedName选出的125案例全名多重集，加3个新案例，无旧案例遗漏；覆盖SelectionBroker/BoundaryMarker/PhaseTiming/Progress/SoftBudget、HostTermination与CaptureHost，含默认真实600ms启动/IO及取消。
- r05 App Release Windows目标交叉构建 **0W/0E**。820项其他源码/测试/锁/项目/许可/扩展/安装数据/SPEC原字节保护全真；旧599/600/601断言块保留，产品seam还原比对通过。仅writer运行测试，reviewer只读。

[命令/日志/TRX/准确源/保护与产物摘要索引](../test-evidence/2026-10-21-selection-deadline-root-cause.json)。未重跑完整Core、Provider、Codec，不把128P当完整Core或P6/P7签收。历史CaptureHost与599失败仍按各自原件保留，不能追认它们的所有未记录轨迹。Windows真实取词/250/600ms、WPF/OLE/OCR/安装身份、画质与600MiB仍未验；PackageHold、生产图片复用Off和WebP odd拒绝保持。无新依赖/许可/费用/凭据/安全权限、CI、用户电脑或内容、真实模型、main/强推/部署。
