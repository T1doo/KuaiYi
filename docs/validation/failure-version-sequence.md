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
