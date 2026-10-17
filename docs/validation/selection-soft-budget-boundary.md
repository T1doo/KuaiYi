# UIA 父客户端 250ms 软预算边界

从已获父有限接受的 `4f47990d4db8b773ba67df3ac6bc4606e38f0824` 正常接续。初始本地/远端 `dev/local-diagnostic-report` 同 SHA、clean、单 worktree；root 唯一观测写入者，high_water_review 只读。新分支 `dev/selection-soft-budget-boundary`，main 未变，不恢复旧实例或重复历史交付。

## 契约与实证

SPEC 5.3 要求总软预算250ms、硬截止600ms。现有 `CaptureBroker/Program.cs` 在 `ElapsedMilliseconds>=250` 返回 SoftTimeout，且只在 `<250` 时附复制凭据；便携复用原型既有测试也在250ms拒绝Success。父 `SelectionBrokerClient.cs` 两处却仅检查 `ElapsedMs>250`。以前这只是源码候选，本次先在父生产原字节上实跑，之后才认定误接纳。

复用既有 `KuaiYi.CaptureControlFixture --selection-fixture`，只增加测试模式的数值耗时元数据，不新增探针或协议。每例启动真实自有.NET子进程、真实stdout帧IPC，通过生产Client及 `SelectionCaptureCoordinator`；仅桌面当前来源和剪贴板OS端口明确替代。每例记录实际状态、来源接纳、替代复制事务/输入次数、子进程次数及清理结算。没有真实Ctrl+C、用户剪贴板、UIA/COM、WPF或模型调用。

| 响应元数据 | 父版实际 | 修后实际 |
|---|---|---|
| Success / 249.999ms | Success，接纳来源 | 保持 |
| Success / 250ms | **Success，错误接纳来源** | SoftTimeout，不接纳 |
| Success / 250.001ms | SoftTimeout，不接纳 | 保持 |
| EmptyOrUnsupported+Copy / 249.999ms | 可信凭据，替代复制事务/输入各1次，接纳Clipboard来源 | 保持 |
| EmptyOrUnsupported+Copy / 250ms | **接纳凭据，替代复制事务/输入各1次，接纳来源** | ProtocolFailure，凭据为空，事务/输入均0，不接纳 |
| EmptyOrUnsupported+Copy / 250.001ms | ProtocolFailure，事务/输入均0，不接纳 | 保持 |

r01父红6执行、4P/2F；r02相同六测试/同夹具字节6P。红绿测试DLL、fixture DLL、Core/Contracts等均相同，仅实际执行的NativeHost DLL改变。原件分别绑定，不混称同生产二进制。全部十二次子进程启动各1、最终清理已结算；原始观察保存在TRX StdOut及提取JSON中。

## 最小修复

生产仅 `SelectionBrokerClient.cs` 两个 `>250` 改为 `>=250`。文本沿用SoftTimeout；过期copy证据沿用原先 `>250` 的不可信协议分类ProtocolFailure，不改变空结果无证据时的其他状态语义。未修改Broker或复用实现，不放宽软/硬预算，不改来源/密码/完整性/身份/取消/缓冲清理/重启门。测试显式使用249.999/250/250.001，不使用模糊的近似等待来推断边界。

本次证明的是**父客户端对精确IPC耗时元数据的准入策略**，不是Windows上真实250ms性能或UIA provider实测。既有600ms启动/读取/最终交付及取消/来源/迟到测试随完整Core回归继续执行，不把600ms改为更长。

最终r04完整Core **1593/1593**（父1587全保留，恰新增6）；r05完整Provider **2157/2157**（父结果多重集完全相同），均0失败/跳过；r06 App与r07 Worker Release交叉构建各0警告/0错误。独立只读代码审查通过。最终完整回归、构建、来源/DLL绑定与原件SHA见 [证据索引](../test-evidence/2026-10-17-selection-soft-budget-boundary.json)。旧诊断、图片、版本/usage/失败提示代码与全部旧测试原字节保留。P1-06仍InProgress；本轮仅追加P3-02/P3-04证据，不提升任何整体实现/验证状态。

真实WPF/Windows/文件语义/密码控件/完整性级别/模型/质量验收仍未执行。图片复用Off、旧图片间歇失败根因未知；Skia四包许可和源码403门保持，不安装运行或换路，GitHub Actions先前拒绝不查不调不绕，run数Unknown。没有main/强推/部署/费用/凭据/安全权限/新许可/新依赖/环境诊断；未知锁、数据、日志、进程保留。所有dotnet用已有SDK、`--no-restore`，activate前后显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`。
