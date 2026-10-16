# 文字结果逐版本实际 Usage

父 `49980109cc178292769f305eee5080dfe20b5dd4`、tree `febff178e1ca26d739d2a93c812c02dd073509ef`。核实际文件、clean Git、单 worktree、唯一 root 写入者、默认网络远端与原基线祖先后建 `dev/text-version-usage`。`text_usage_review` 全程只读独审，无 SDK、网络或写入。

SPEC §12.1 TranslationVersion、§14.4 与 F04 的缺口是正常文字 Coordinator 丢弃原双协议解析器已经产生的 Usage，正常结果标签也未展示。现在 `CachedOverlayVersion` 与正文/终态同属完整 session/version/request/epoch 身份，保存不可变 `TranslationUsage`；新版本一律从两字段 Unknown 开始，不从 `RegenerationSnapshot with` 复制旧用量。选区、拖放文字及本地 OCR 预览显式确认后的文字路径统一经过现有 TextTranslationCoordinator；缺失/非法字段保持此前已知，原来未知仍 Unknown，合法 0 和向下修正覆盖，重复报告不累计、不取最大值、不估算。只显示现有协议实际报告的 input/output token，不造总额、价格、费用或完整计费凭证。

Usage 经原 ProviderEventReceiver 的原预算、取消及 destination gate；新消费分支再检查原来源、设置/再生成冻结规则、配置绑定和 canonical identity。更新后通知只刷新现有结果标签；不改正文、原始复制、完成状态、等待环、展开、阅读锚点或帧/flush。原标签可能单行截断且不可 hit，故现有“本次会话版本”菜单也调用同一生产 formatter，以完整独立第二行显示该版本两项计数和 Unknown；没有增加独立窗口。当前生成的 Usage 更新自己的版本，浏览旧版本仍显示旧版本计数。菜单真实布局/键盘可达性未在 WPF 实机验证，Linux 只执行其生产 formatter 与调用/数据端口，App 实际编译证明 MenuItem.Header 接线。

同源 SaveVersion、三版淘汰、失败 rollback、跨来源 fallback 与 Clear 使用同一缓存生命周期；被淘汰/被丢弃的版本不另建历史。失败或停止保留已接纳元数据，迟到与终态以后不再改写；Clear 不留 usage 引用。重开不发新请求，再生成新 identity 从 Unknown 独立接纳，四风格只影响各自冻结版本。原模式/暂停改变只失效未来采集，不取消已发请求的规则保持，不能误把这种改变当作晚到结果失效条件。新 Usage 通知中 Clear/启动 C 后，旧任务没有后续字典写回或清 C 动作。

| 原始执行 | 结果及失败记录 |
|---|---|
| r01 | 新夹具 `TriggerMode.DragDrop` 不存在，CS0117；0 执行、无 TRX。精确源码与 log 保全后修为原有 `Drop` |
| r02 | 97 项，75 通过 / 22 失败。21 项来自新 Anthropic 夹具在 active content block 内错误发送 message_delta，原严格 parser 正确拒绝；1 项错误预期 SetMode 会取消旧请求。原 log/TRX/精确测试源码保全，修夹具顺序并把 mode 移到明确正向保留测试，不改 parser/生产取消规则 |
| r03 | 95/95：修正后的双协议/三来源/四风格、缺失/重复/字段覆盖、三版/取消/迟到/重入合同 |
| r04 | 119/119：加 F04 finish 后空 choices usage 等 DONE、停止/中断/截断保存、旧版本阅读和菜单 formatter、Core 身份终态 |
| r05 首次完整 Provider | 1690 通过 / 4 失败，共 1694。旧 QueuedConsumerDeadlineTests 字面预期只 Begin/Delta/Complete，与本批新增 Usage 通知冲突；精确原测试和项目文件、log/TRX 保全。预期扩为 Begin/Delta/Usage/Complete，不过滤或降低断言，并补实际计数及截止前后已接纳/Unknown 断言 |
| r06 最终完整 Provider | **1737/1737 = 父 1575 全保留 + 新 Usage 119 + 原 Core 两文件 43**，0 失败/跳过。独审逐项比较 testName 多重集，无父项缺失 |
| r07/r08 | 最终 App/Worker Release 交叉构建各 0 警告 / 0 错误 |

新 119 为 TextVersionUsageTests 95、TextUsagePresentationTests 24。48 个三来源×双协议×流/非流×四风格用例走实际 Provider/Kestrel、原 parser、receiver、Core 缓存和链接的生产 Shell 标签；OCR 是实际 OcrOnlyCoordinator→合成 Worker 结果→本地预览批准→再次显式文字发送确认，成功原生识别仍未验。真实请求验证无图片及只含批准文字。其余覆盖 Anthropic message_start input+output0 后 output-only、OpenAI finish/空 delta/空 choices usage/DONE、重复/部分/负值/合法 0/向下修正/Int64 最大值、unknown、不因 usage 完成、旧版选择、取消/清空/配置来源/迟到、A/C 重入及三版淘汰。

为回归实际修改的 Core 缓存，Provider 项目仅新增两个 Compile Link，原 `OverlayPresentationTests.cs` 35 项及 `SessionVersionTests.cs` 8 项源码一字不改。它们运行纯托管状态/几何/临时设置文件与显式测试替身；不跑 Windows/原生/CMake 目标，不宣称完整 Core.Tests 已执行。不新增项目引用、PackageReference、依赖或锁。旧 Provider 测试名/参数全保留，只强化上述通知预期与计数断言；所有其它父测试原字节不改。

固定 [JSON](../test-evidence/2026-10-16-text-version-usage.json) 保存实际计数、source manifest、执行二进制和失败/原件摘要。24 份新原件为 13 SDK log/TRX、8 父源码/项目原件、2 失败测试源码及 1 preflight，精确路径 `-text` 保全；父累计 294 份原件、1883 旧证据及未改父文件逐字节核验。r02 原 TRX 内 8 行 SDK/xUnit 诊断空白带原生行末空格，首次 cached diff-check 如实失败；仅该精确回执另标 `-whitespace` 保原字节，源码及其它路径检查不变。43 锁、8 workflow、8 模板不变，Provider/parser/预算/序列化与 Imaging/OCR/图片生产门除明确文字 Coordinator 外不动。

F04 仅推进为 InProgress/NotRun，P4-04/05 保持 InProgress/NotRun；P6 各状态不改。真实 Windows/WPF 标签/菜单/测量/选择/阅读位置、实际 OCR/包身份/语言/质量、采集/COM/原生 Job/250/600ms 和完整阶段验收仍 NotRun。生产图片复用 Off，无图片生产发送、模型调用或新依赖。所有 dotnet 激活前后及命令显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；证书库/配置/信任 Unknown，不读改/trust/删除。Skia 许可待答与源码 403、Actions 拒绝门不绕过，不安装/加载/执行 Skia，不查调 Actions，run 数 Unknown。无 main/强推/部署/新费用/凭据/权限/协议接受或 Windows/CMake/新 CI 实验。
