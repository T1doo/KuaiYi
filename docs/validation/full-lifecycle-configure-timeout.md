# 完整生命周期唯一 Windows 尝试及本地补正

审定准备源码 `16d3ff3799d3704ec29a22954d6a4f99d935a2b5` 经正常非 force 交付为 `1f78ac3cf2073895dab8c6a3ab3e936ce064d143`；两者完整 tree 同为 `381bd52c7dcab8d807b56af4c884c856443cb84b`，唯一 parent 为 `b39a9076e69bec3c277e5664428ff142eaaef9f5`。八文件逐字节核验后交付，原审定提交及 bundle 保留。

实际 [run 38595908121](https://github.com/T1doo/KuaiYi/actions/runs/38595908121)、[job 115962449373](https://github.com/T1doo/KuaiYi/actions/runs/38595908121/job/115962449373)、attempt 1 为 completed/failure。固定脱敏 [receipt](../test-evidence/2026-10-14-full-lifecycle-38595908121.json) SHA256 为 `26fed411c2c1d26f64c090e455a988a9049a8339902699732c700a5bd0118ba1`。原 connector job 文本仅保存在 ignored 私有目录，不上传任意原始路径、异常或编译输出。

## 实际结果

SourceHead、SourceParent、SourceAncestry、SourceLockedTree、SourceStatus、SourceCompare、CMakeVersion、DotnetVersion、VisualStudioInventory 九操作均 Completed/Exit0，held root 退出、Job 空及 cleanup 确认，无 rescue。ParentVerified、AncestorVerified、LockedTreeVerified、SourceEquivalent 均 true。

NativeConfigure 为 Timeout/Stage WaitJobEmpty/Exit0；ProcessCreated、Resumed、RootExitConfirmed 均 true，RescueUsed true，最终 JobEmpty 和 CleanupConfirmed true。最终 JobEmpty 是救援后的观察，不能改写为原操作成功；整体 Reason OperationTimeout、ExitCode3。CMake 根进程已正常退出，但等待 Job 成功结算超过预算。receipt 没有救援前子进程身份或完整操作原始日志，不能确定具体滞留子进程；也不能排除临界预算耗尽与 Job 变空的时序。具体 Windows 根因仍 unknown。

NativeBuild、ManagedRestore、ManagedBuild、LifecycleRuntime 及 Normal、BoundParentDeath、BeforeBindParentDeath、Watchdog 全部 NotRun。CompilerFaults[] 不证明编译无错误；configure 内部编译器探测不等于生命周期目标已构建。Runtime/UIA 原文/Match/Performance NotRun，ProductionReuse Off，ModelCalls0。旧预启动失败原因仍 unknown，旧失败证据不改。P0/E06 Blocked、整体106用例及普通 Windows11、实体输入、多屏 DPI、安装卸载没有因此通过。

## 仅本地准备的补正

受控 ChildEnvironment 原白名单未传入 MSBUILDDISABLENODEREUSE；仅工作流环境设置不能确保作用于子进程。现固定写入 `MSBUILDDISABLENODEREUSE=1`，覆盖宿主值并传给 configure 及其后代。官方 [MSBuild BuildParameters 源码](https://raw.githubusercontent.com/dotnet/msbuild/main/src/Build/BackEnd/BuildManager/BuildParameters.cs) 的 EnableNodeReuse setter/初始化尊重该开关；本地实际 SDK10.0.401 setter 在宿主0时 true、1时 false。该证据验证开关语义，不证明 Windows VS2022 的进程行为或本次 timeout 根因。构建节点保留仅是待检验假设。

新增5条环境及真实 SDK setter 合同，原86条保留，总91/91通过；不创建 BuildManager、不执行 build、无 Win32 调用。未来 runner 的唯一审定 parent 改为此次实际交付1f78，链长要求 depth6；真实 local 浅克隆 depth5 缺 locked 祖先、depth6 可核 parent/祖先/tree/runtime diff。原72/72 runner 合同及45/45 evidence 合同通过，缺失 cmake 明确拒绝，不安装依赖。

Job Empty 条件、救援判定、每操作30s及既有清理观察、step8min/job10min、生产250/600/120/2000ms守卫均未放宽；唯一 helper 功能变化为固定环境变量。未来 workflow 修改仅本地冻结，不上传、不触发新的 Windows 尝试。没有重跑、模型调用、安全网络改变或原始日志公开。
