# 连续失败后的版本序号修复

父 `8666a62f93aeefa6b6cc96488c33bddb63a89709`，tree `34b44c4376c9ef76115f8d6ed00e4e1a60f196e3`。沿原环境核实际文件、SPEC/AGENTS、clean Git、唯一 worktree/root 写入者、默认网络远端及原250祖先后建立 `dev/failure-version-sequence`。`failure_sequence_review` 只读独审，无 SDK/网络/文件写入；不恢复旧实例。

父级发现的 P2 已实际复现：A1 成功，新来源 B1 失败回退 A1，手动选 B1 重新生成 B2，B2 再失败回退 B1。`FinishUnsuccessful` 原来只取旧 fallback 中 B 的最大号1，漏掉刚保留的 B2，因此 preview、offer 和后续 Begin 又编号2。这是模型序号缺陷，不是菜单显示问题。

Core 只改一处：对 `fallback.Append(failed)` 按回退 session 取最大号。原 fallback 的同源高号和刚失败的同源号都参与，其他来源的高号不混入；不只看裁剪后三版，避免被本次淘汰的原高号丢失。现有 NextVersionNumber/Begin 消费该序号，B2失败后下一版为3，继续失败依次4、5、6。三版上限、选择、完整 identity、原 source/usage/failure、取消/清理和晚到门不改；Fail 与中性 Abandon 共用修复。

补充验收也在同一反例中修复：自动回退到旧失败 B1 时，notice 默认公告新失败 B2/B3，并明确“新尝试未完成 · 正文为旧结果”；旧正文、标签和用量仍属于 B1。用户明确通过原菜单选 B1 后，才优先阅读 B1 自己的截断原因；再有新失败会撤销这次检查意图。用一个匹配 SelectedVersionId 的 GUID 表达手动检查，不复制正文、不改门、不增加 HTTP。无匹配手选意图时优先 LastFailure；无状态/清空会清意图。32DIP、按钮、菜单和测量接线不变，真实 WPF 可见性/键盘路由仍 NotRun。

| 本批原始执行 | 实际结果 |
|---|---|
| r01 | 新测试少 using KuaiYi.Providers，CS0103；0执行、无TRX，精确源码/log保全；没有把编译失败称为产品红测 |
| r02 父版生产红测 | **10执行、0P/10F**。6项序号 Expected3/Actual2，4项 notice 旧截断原因遮住最新失败。3个生产文件与父blob原字节相等，红绑定JSON记录源码与实际Core/Provider执行DLL摘要 |
| r03 同测试绿测 | **10/10**。测试源码与r02完全同SHA，不改断言；仅上述2生产文件修复 |
| r04 完整 | **1958/1958 = 父1948完整 + 新10**，0失败/跳过 |
| r05 完整 | 1957P/1F：旧离线图片 PreparationReentryC…（OpenAI/不换图）断言时 C 正文为空。原旧测试源码/log/TRX保全。此间歇失败根因未实证，不能称已证夹具或新产品修复 |
| r06 原样隔离 | 原生产/原旧测试未改、同执行DLL `--no-build` 隔离四例 **4/4**；仅证明可间歇通过，不证明根因消除 |
| r07 | 1956P/2F：补强旧测试时过宽替换，在两个尚未给C响应的场景加了过早Complete断言，是本轮测试编辑错误。保精确失败源码/log/TRX，从父blob恢复其它所有方法，只修改原失败的单一方法 |
| r08 最终完整 | **1958/1958**，0失败/跳过。旧失败方法保留所有原await/正文/PNG/映射/usage/身份/旧A迟到/Stopped断言；额外等待明确C Completed事件且核身份，再断C.Complete与Completed。该方法以外源码逐字节等父；图片生产代码完全未改 |
| r09/r10 | 最终 App、Worker Release交叉构建各0警告/0错误 |

新10例包括两协议×流/JSON的连续 B2—B5 失败，逐轮核 NextGeneration、Prepare snapshot、实际 Begin Number、精确请求identity、source B/拖放、各版usage/Failure、保留B1正文、同session序号唯一、三版与snapshot同步、拒绝晚到append/usage/fail、显式Prepare不发请求、Clear释放；两协议×流/JSON的自动B2提示→手选B1截断→手选B2拒答→手选B1再B3失败重置→手选B1，原正文/原因/用量各归其版；另两项纯Core分别走Fail/Abandon并断伪造ID、取消后的拒绝及Clear重置。r04后只对原notice四例追加更强B3检查，未删减r02红测断言或旧测试。

r05的等待/调度可能性来自静态观察：测试的 Immediate destination 可在后台同步回调，旧代码共享 Running 属性不一定能证明已等待重入 C；HTTP计数和server flush也不证明客户端消费。最终测试等待身份明确的C完成事件，原等待仍保留；没有调度时序重现证据，Running覆盖仍是推测，不宣称图片产品问题已修复。生产离线复用持续关闭。此限制单列保留给父级后续审查。

历史不能混称：**父批 text-terminal-failures 的 r05 两失败，一项是来源失效丢最后可读正文的真实生产回归（中性Abandon修复），另一项是视觉 HTTP计数先于事件入队的竞态**；原log/TRX/源码和固定JSON保持原字节。本批 r05 旧离线例与 r07 编辑错误另按上表记录。

[固定证据](../test-evidence/2026-10-16-failure-version-sequence.json) 保存红/绿绑定、源码manifest、实际执行DLL、逐测试名连续性和原件。28新原件含5父源码/项目、4测试候选/失败源码、红绑定JSON/preflight、10 SDK log及7 TRX；父388累计原件、1980旧证据和未改父文件原字节保持。旧测试仅一个方法同步/终态断言补强，不删测试、不降断言；项目、依赖、43锁/8workflow/8prompt不改。P4-04/F06/F08仅追加缺陷与证据，整体验收和P6不抬状态。

所有 dotnet 激活前后与命令显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；证书存储/信任Unknown，不读改库/配置、不trust/删除。无真实模型/费用/凭据/安全权限/待授权协议接受；Skia许可/403、Actions拒绝门保持，不查询调度Actions，run数Unknown。无main/强推/部署/Windows/CMake探针/新CI。真实WPF显示/键盘、Windows/OCR/COM/nativeJob、250/600ms和完整阶段仍NotRun。

## 2026-10-16 续修：跨来源淘汰后的独立高水位

接续父提交 `d45ba9d4ab1af3675cb7d0a36cfe0f77618965d0`，仍在 `dev/failure-version-sequence`。新实例初始 HEAD 为 main 早期快照 `51d077481cf6303602fcf5d2718213da9cf05178`；默认网络 ls-remote 确认远端开发分支为指定父提交，正常 fetch 后建立本地同名分支，核其 main 祖先，未恢复旧任务实例写入。当前 root 是唯一写入者，`high_water_review` 只读独审。没有依赖跨实例文件共享或原 bin/obj；以锁文件重新 restore 已有依赖。

上一批局部修复未覆盖这个残留：A1/A2/A3 成功；B1 失败回 A3；手选 B1 生成 B2 再失败回 B1；C1 失败回 B1 并淘汰 B2；D1 失败回 B1。原 `FinishUnsuccessful` 再次从已经失去 B2 的列表重算 `sessionSequence=1`，下一 B 的 NextGeneration/Prepare/Begin 重用 2，实际应为 3。缓存里没有两个 B2 也不能证明编号正确。上文上一批 10F→10P 与 1958P 原件不改，不把它们当本路径已覆盖。

生产仅修改 `OverlayResults.cs`：用来源 session GUID 到最大已用编号的独立字典替代单一 `sessionSequence`。只有接受有效身份的 Begin 消耗新号，预览/Prepare/拒绝/晚到不消耗；回退只恢复该版正文、身份、用量与状态，不再重算高水位。Begin 淘汰后、Complete、Fail/Abandon 都按 `versions + fallback + identity` 的来源集合清理，Clear 清空；仍有任一可回看版或隐藏 fallback 的来源保留高水位，来源完全释放后归零。这个集合最多是三版 fallback 加当前新来源，即最多四项 GUID/int 元数据，不保存额外正文、快照或无限历史。三版结果淘汰规则、请求门、Coordinator/notice/图片生产代码均未改。

| 本续修实际执行 | 结果 |
|---|---|
| r00/r06/r08 | Provider、Core、App locked-mode restore 成功；没有新依赖/Skia 包 |
| r01 父红 | 8 执行、0P/8F；6 项应3实2，2 项隐藏 fallback 应3实1；源码/DLL绑定原件保存 |
| r02 初修 | 6P/2F；两项新增生命周期夹具错误地假设没有 A3 时 B2 会被淘汰，原规则实际会保留 B2。精确候选源码/log/TRX保全，没有把测试编辑错误算产品回归 |
| r03 最终候选父红 | 8 执行、0P/8F；生命周期夹具补齐 A1/A2/A3，保留原淘汰及后续生命周期断言；临时用精确父生产 blob 实跑后恢复修复 |
| r04 最终绿 | 同 r03 测试字节/8 个完整测试名，8P/0F/0跳过 |
| r05 完整 Provider | **1966/1966**、0失败/跳过；父1958全部测试名保留，恰好新增8 |
| r07 完整 Core | **1551/1551**、0失败/跳过；未改原测试或项目；这是 Linux 便携与 owned managed fixtures，平台不支持分支不算 Windows 实机通过 |
| r09/r10 | 最终 App、ImageWorker Release 交叉构建，各0警告/0错误 |

新增四项真实本地 Kestrel HTTP（两协议 × 流/JSON）严格复现 A/B/C/D 淘汰反例，逐段核 NextGeneration、Prepare 不发送、offer 编号/来源、实际 Begin 编号、完整身份、B1原文/拖放来源/usage/截断原因、各次新失败身份及自动新失败公告、手选 B1 自身失败、三版与 snapshot 同步、B2 snapshot释放、晚到拒绝和 Clear。另四项 Core 分别覆盖 Fail/中性 Abandon 的同一路径及80次来源替换清理、隐藏 fallback、快速新来源替换、成功释放和生成中 Clear。没有弱化原断言、修改旧测试或只断缓存唯一。

[本续修固定证据](../test-evidence/2026-10-16-retained-source-high-water.json) 另存红绿来源/DLL绑定、原件清单、全测试名连续性及保全核对；旧 JSON 和旧原件原字节保持。最终 App/Worker 构建各0警告/0错误；独立只读审查未发现需要修复的问题，最终原件与保全另核。

历史图片间歇失败根因仍未实证：本次全回归通过不证明已消除，也不声称图片产品修复；生产图片复用保持 Off。真实 WPF 可见性/键盘、Windows/OCR/COM/nativeJob、250/600ms/完整 SPEC、真实模型验收仍 NotRun，原 P4-04/F06/F08 整体状态和全部 P6 不抬。所有 dotnet 显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；不读改证书/凭据/安全权限。Skia4.153.1四包许可待批及源码403门保持，不安装运行、不绕过；GitHub Actions访问先前拒绝，未查询/调度/换路，运行数 Unknown。未改 main/强推/部署/付费/真实模型，未删除未知锁、数据、日志或未知进程。
