# 初始 suspended 自有 Job：本地修复与单次候选计划

原实测run [38699679076](https://github.com/T1doo/KuaiYi/actions/runs/38699679076) / job116304547116 / attempt1仍为Fail/Exit3；来源通过，Created9，OwnedJobCounts条件false，清理确认，两个诊断NotRun。实际A/B数量及原因没有导出，旧表达式的&&可能跳过B查询。完整原日志及bb0fae011046daaff2f0cdfe9b7a7f00de7d1919归档原字节保持；本修复不证明旧失败唯一根因，更不证明原NativeConfigure超时根因。

## 修复与官方依据

旧循环每创建绑定一个pwsh就Resume，最后才断言A8/B1，已运行脚本的退出或派生会改变集合。现在移除整个Resume接口、调用和PInvoke，九个root从CreateProcessW的CREATE_SUSPENDED建立到自有Job清理全程不主动恢复主线程，保留固定完整pwsh路径/公开sleep参数及环境白名单，但不执行子脚本。不增加延时、扫描、API权限或后台服务。

[CREATE_SUSPENDED官方契约](https://learn.microsoft.com/en-us/windows/win32/procthread/process-creation-flags)保证主线程直至ResumeThread前不运行；[QueryFullProcessImageName](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew)只要求查询权利，没有要求先Resume；[TerminateJobObject](https://learn.microsoft.com/en-us/windows/win32/api/jobapi2/nf-jobapi2-terminatejobobject)用于终止Job进程，无需先恢复主线程。这支持本设计选择，不能替代实际Windows主机上的ABI、映像查询和终止验证。

A/B前后数量分别在PrimaryJobAccounting/OtherJobAccounting与PreservedPrimaryJobAccounting/PreservedOtherJobAccounting阶段查询，调用间检查同一预算；两值都取得后才比较，不以A数量错误短路B查询。若预算已到期或A查询抛异常立即结束，不为查询B越过停止门。数量只在内部比较，不导出实际值或身份。

清理算法不变：统一finally先计划终止两个自有Job；对绑定未确认或Job终止失败的已创建root仅使用自有句柄兜底；观察全部已创建root退出及两个Job AccountingEmpty，再逐一关闭所有process/thread/job句柄并最终核对清理截止。不先Resume，不以最后JobClose的自动终止冒充已观察清理。协作取消/注入OperationCanceledException走同一finally；进程强杀、宿主取消或同步API挂起时不承诺finally执行或清理完成，不能把job/step timeout认作清理证据。

## 精确来源、范围和一次运行边界

本地分支dev/job-member-suspended-fixture，唯一parent bb0fae011046daaff2f0cdfe9b7a7f00de7d1919，parent tree 062ac230125b8da99c95a88b0cde75f658094470。现有helper blob227378ffa3a59e80c65da7fc43c0cffcd21d70f7保持。候选完整SHA/tree与逐文件blob由本批交付回执冻结；只改七文件：

1. .github/workflows/windows-job-member-fixture.yml
2. docs/progress.md
3. docs/test-evidence/2026-10-15-job-member-suspended-offline.json
4. docs/validation/job-member-suspended-plan.md
5. tests/scripts/JobMemberWindowsFixture.cs
6. tests/scripts/job-member-fixture-contracts.test.ps1
7. tests/scripts/job-member-fixture.test.py

来源合同使用真实owned本地Git浅克隆：depth1拒绝，depth2验证完整唯一parent、parent tree、helper blob与七文件；篡改helper pin拒绝。原entry、helper、runner、五旧workflow以及旧失败结果文档不改。原失败workflow候选2d7366远端保留，新触发仅push到dev/job-member-suspended-fixture且变化命中该workflow自身路径，无dispatch/PR/schedule/matrix/retry。五个旧workflow均不匹配该新分支。

本批不push、不触发或重跑Windows。父任务先核精确freeze、独审及授权，才可另行执行正常非force推送一次；不向旧分支推送。使用public仓库标准windows-2022单job，contents:read；job5分钟、来源1分钟、fixture2分钟，checkout固定action提交、depth2、无持久凭据。已有pwsh/Add-Type编译独立fixture，不安装、不构建原目标、不使用artifact/cache/额外服务。执行前复核public/标准免费范围、远端祖先、未有同SHA运行；监测仅一条attempt1至终态，失败/超时/取消不自动重跑。

同一30秒Stopwatch在创建资源前建立，余量250ms停止；随后单独5秒清理观察。同步API/编译/调度不保证硬上限。两Job均非继承KILL_ON_JOB_CLOSE，A8/B1共九个自有process/thread。十一成功检查保留，第二项改为NineBoundInitiallySuspended：两个Job、九root创建绑定且未恢复、A8/B1、精确Job限制、前root live、真实A8固定72字节ABI及8个Other、受控B候选非成员拒绝、单候选零Wait/零Image、借用Job数量限制不变、后root live、所有自有资源清理确认。

受控B替换不声称真实PID复用。原helper保持OpenProcess 0x101000、前后成员/Wait0检查、固定映像buffer，最多8成员；对初始suspended进程的实际映像读取结果仍待Windows验证。通过Exit0，平台Blocked2，其余Fail3；custom pwsh -File保留退出码。失败/预算/清理未知全部隐藏观察与反例计数，只发布闭合固定状态，禁止PID/路径/命令/句柄/异常导出。来源、创建/绑定/数量/成员/预算/清理任一失败不重试不绕过。

## 本地证据与未完成项

本地注入2056断言/96场景通过，包括旧执行后退出/派生模拟与未执行新路径、A错仍查询B、查询异常固定阶段、创建后/查询间/观察后/反例后预算、九个创建/绑定/成员边界协作取消、清理取消、root/Job未空、Close失败或越期、全部已创建句柄独立关闭尝试、失败事实null与敏感输出拒绝。原helper411断言/63场景与旧文件fixture16合同通过。来源合同首次因准备时progress尚无差异而按七文件门拒绝，原输出保留；完成真实进度记录后再核验，不弱化七文件断言。完整输出hash和最终来源结果见本批固定离线证据。

本地通过只验证控制流与注入反例。修复候选Windows NotRun，真实A8 ABI和B拒绝仍未验收；原configure根因Unknown，ProductionReuse Off、ModelCalls0，原30秒/救援/清理和250/600ms、来源/密码/完整性/取消/清理安全门、P0/E06状态不变。不运行CMake、原runner、四生命周期或真实模型。
