# SPEC3.6：纯显示保存继续已启动的文字生成

从父有限准入的 `528b2ba8175b1014b51301eb653cc84d06dfd9b8` 正常建立 `dev/display-save-stream-continuation`。实际 HEAD、默认远端与 main 祖先、单 worktree 和唯一 root 写入者已核；既有 high_water_review 只读独审。仅修用户明确授权的已启动请求在保存显示/位置设置后被废弃的问题，不推进 P7 或其它功能。

## 实际缺陷与最小修复

实际 `SettingsCoordinator.SaveAsync` 每次提交生成新 WriteId/Version，`ShellLifecycle.SavePositionAsync` 也走这条保存链。原 `RunAsync.SendEligible` 对普通请求要求整个设置 Version 等于冻结版本；首段 SSE 后即使仅保存字号、宽度或位置，下一事件也会 Abandon，原请求不完成。

生产仅改 `TextTranslationCoordinator.cs`：仍保留原不可变请求快照；每个活动请求另保留一个已核保存配置基线。仅在提交的设置变化时用既有有界 SettingsJson 比较，归一化字段只有 Overlay 与真实事务 WriteId；要求新非空 WriteId、可写/无恢复状态及当前版本。两个比较缓冲均清零。已核版本复用，逐 token 不重复序列化，不新增存储/日志/诊断或一般状态组件。

初版经独审发现同一安全版本的双校验 CAS 败者会误取消；现在仅承认仍属同 identity、同冻结 Snapshot 引用、已核同目标版本的幂等成功，陈旧目标不得倒退缓存。被下一提交超越的检查重读当前设置；终结按同 identity 重试清除，不能清新请求或复活旧归属。Clear/取消仍释放归属。终审发现新增非profile不安全保存若立即Stop会跳过原Abandon回退，实证后改为给同owner永久SettingsRejected：先拒新HTTP与交付，下一destination仍走原中性Abandon，恢复旧文本/usage/身份并保留B已停止版本。恢复字段或版本不能清除否决，旧owner不能否决新identity；原profile/auth/key分支Stop保持。

OCR 本次风格可合成 Snapshot.Settings，故在风格投影前用 Offer 内部只读引用保存真实配置基线；不把 Preset 加入归一化白名单。请求仍用原风格/提示词/正文及确认。重生成原来绕过全局 Version 的冻结行为保留，新增普通版本限制不适用于它；配置删除/密钥/binding/profile 否决不放宽。旧未提交与待长度/OCR确认候选仍执行原精确 Version 门。来源、授权、HTTP、秘密、取消、identity/epoch 与实际目标交付门保持。

## 红绿、回归与证据边界

| 原件 | 实际结果与含义 |
|---|---|
| r01 → r02 | 父生产原字节下，两协议 × 字号/宽度/位置首段后实际保存 **6F→6P**，同测试源字节与六名；测试 DLL 不同，Core DLL 相同。 |
| r03 → r04 | 修补候选的同旧 owner 双校验，真实私有生产原语 **2F→2P**；同测试源/两名/测试 DLL，仅 Provider DLL 改。反射持有两个校验者可捕获的同旧参数，不能称真实 HTTP/通知同点线程屏障。 |
| r11 → r12 | OCR本次风格不同于保存默认，两协议 × 字号/位置真实 HTTP **4F→4P**，同测试源/四名；测试 DLL 不同，Core DLL 相同。这是候选残留红测，不冒称父版第二次红测。 |
| r05 / r07 | 前候选专项41P、完整 Provider2198P，全部原件保留。 |
| r13 → r18 | 新增批准撤销负例初为49P2F：夹具把识别前来源标记当成已批准文本的撤销。仅动作改为真实 Local.Clear()，全部断言及生产源不改；该候选专项 **51P**。此组不是产品红绿。 |
| r06 / r14 | 完整 Core 首轮 **1687P1F**，旧 SelectionReuseHostTests.Dispose_during_active_request_retires_and_settles_actual_child 报 Owned child or IO settlement unconfirmed；后续完整 **1688P**。Core.Tests/Core/NativeHost 三 DLL 相同，r06诊断 DLL 后读摘要亦同r14。失败源码/log/TRX/绑定保全，原因仍 Unknown，不称已修。 |
| r15 / r16 / r17 | 前候选完整 Provider2208P、App/Worker各0警告/0错误，全部原件保留。 |
| r23 / r24 / r25 / r26 | r23 OAI实际未回退；Anthropic夹具把usage放在未停止block内导致A先失败，不计双协议产品红。修正合法事件位置后，r24候选两协议 **2F**；r25父生产原字节同测试 **2P**，证实是新增回归；r26修后同测试 **2P**。A成功→不同来源B部分→上下文保存→排队尾段，核A/B各自usage、身份与中性Abandon。父对照仅暂换工作树一个文件，HEAD/index/分支不改，随后恢复候选。 |
| r27 / r28 / r29 / r30 | 最终专项 **55P**，完整 Provider **2212P**＝父2157全名多重集＋新增55；Core1688全名多重集同父。App/Worker Release --no-restore 各 **0警告/0错误**。 |

r14 Core 在 Provider-only 夹具动作及后续Providers永久否决修正前执行，其源记录准确保留前候选。已独审核14个项目的 Core 依赖闭包不含 Providers/Provider.Tests；整个 Core 依赖源码/项目/锁原字节不变，因此r14仍适用，无需为这些非依赖改动重复Core。不能说r14记录的Providers或新测试摘要等最终源码；最终Provider/App则按新冻结源重新全量/构建。

实际测试复用原 Kestrel SSE fixture、SettingsStore/SettingsCoordinator、ShellLifecycle 位置保存、ProviderClient/协议解析/门控/结果；首段必须真正进入结果后才保存。另以实际提交发布后、Observe 通知前交付排队 delta 覆盖 publication gap。55例包含多次保存/恢复显示、重复输入仅回看、旧候选、模型/endpoint/认证/key-reference/header/profile/删除/风格/上下文/模板/模式/隐私/图片设置否决、不安全 ABA、来源/停止/Off/锁屏/Clear/新 identity、晚到原语、冻结重生成与 OCR批准撤销、原Abandon跨来源回退/逐版本usage，以及显式settings-provider端口模拟精确旧原Version/已核Version再可见时的永久否决快速路径。全部为公开合成样本、AuthNone 回环 HTTP，秘密读取0。

OS来源有效端口明确为合成替代；OCR 使用真实 OcrOnlyCoordinator 批准，但本地 OCR worker 为显式合成替代，不执行新编解码包或 Windows OCR。真实 WPF 设置可见性、键盘/焦点与前台来源行为、Windows/DPAPI/ACL、模型、物理250/600ms及完整SPEC仍 NotRun。

初次130原件/137路径封存经字节审计后因上述回退回归暂停，未提交/推送；旧130原件全保留，修后重新终审。修后封存199份原件，独立终审另追加1份回执（最终200份）；具体索引如下。精确源码、命令、log/TRX、执行/构建 DLL、父版保全、多重集核对及独审见[固定索引](../test-evidence/2026-10-17-display-save-stream-continuation.json)。r08/r09仅预备命令，OCR修订后未执行，明确单列；真实最终构建为r29/r30。旧测试/SPEC/原产品书/AGENTS/依赖/锁/工作流/提示词/全部旧原件原字节保持；追踪仅给P1-03/A10追加本子项证据，所有状态不抬。

旧图片间歇根因Unknown、生产图片复用Off；250/600ms及来源/密码/身份/完整性/取消/清理门保持。仅已有SDK，激活前后禁ASP.NET证书生成，全部--no-restore，无新包/许可/环境安装。Skia4.153.1许可、源码403和Actions先前拒绝不查不绕。无main/强推/部署/费用/凭据安全权限或真实模型；未知锁、数据、日志与进程保全。
