# 文字失败终态与版本恢复

父 `c5e4f695f4d9570e4c8221f9cf9f0454e3ba6d48`、tree `abfe7acfae44b46aac4106ad577cf19524976836`。沿唯一原环境核实际文件、SPEC/AGENTS、clean Git、唯一 worktree/root 写入者、默认网络远端和原250祖先，建立 `dev/text-terminal-failures`。`terminal_failure_review` 全程只读独审，不跑 SDK/网络，不写文件；旧实例不恢复。

SPEC §10.8/F06/F08 的缺口是截断、拒答等仅在瞬时 tooltip 出现，没有保存在版本内；新来源失败回退旧正文时，失败版本也被丢弃。现在 Core 把服务码严格映射到有限 `TranslationFailureReason` 枚举，不保存服务错误正文、拒答原话、异常 message 或任意字符串。`CachedOverlayVersion.Failure` 与正文、实际 usage、完整 session/version/request/epoch 同寿命。截断明确说明输出限制与可能不完整；明确 refusal/content_filter 中性呈现“模型未提供翻译”；未知停止原因不猜拒答。OpenAI JSON 的合法message.content和usage在拒答终态前仍接纳（refusal字段本身永不作正文），其明确拒答和 Anthropic 两类响应的明确 refusal 统一对应安全原因；工具响应保持 unsupported，未放宽协议生命周期或缓冲上限。

同源失败仍回到原选择的旧版本。跨来源失败保留失败尝试与所选旧正文，最多另留一个旧版本，终态总缓存仍三版。旧正文的完成状态、原因、usage 和 source 不改；失败尝试通过“另一来源”菜单标识与自己的来源种类/版本号区分。按旧来源继续生成保持原序号；选中失败来源后可显式重新生成其来源与下一序号，不借用回退正文。新 Begin 从无原因开始，清 latest-failure 提示；清空和淘汰同时清对应 snapshot/元数据。没有正文的拒答也可选择、回看，复制仍取原始正文，不把状态文字拼进结果。

正式浮窗增加一个32DIP槽内的原生28DIP Button，持续显示精简状态；Focusable、标准 Click（键盘 Enter/Space）与完整 AutomationName/ToolTip 接原生控件。点击只打开现有“更多操作及版本”，复用原重新生成、旧版本和设置动作，没有独立错误窗口或自动重发。回退时按钮明确“新尝试未完成 · 正文为旧结果”，所选旧版本标签不带新失败。完整菜单状态说明具体安全原因、是否有部分正文及恢复动作。无正文首个失败也经原 Gate、手动收起、隐藏、身份和拖动门排队展开有限状态区；新 Begin/收起/隐藏/清空撤销旧展开。32DIP同时从正文约束与测量扣除，不改变按钮锚点。真实 WPF 显示、Tab 顺序、键盘和辅助技术路由仍 NotRun；Linux 端口只执行实际 linked handler/formatter、布局几何状态及 action/data，不假称实际可见或可达性通过。

所有 provider 事件现在在 destination 入口统一复验来源、冻结/当前设置规则、配置 binding 和 canonical identity；外部 proof 回调后的 gate 再验不能被 Clear/C 绕过。来源失效只在原身份下中性 Abandon，保留停止状态并恢复最后可读 fallback，不给旧版本贴一条失效服务错误。已取消/被替代的身份完全不改缓存。未接纳终态的任务 finally 只 MarkFinished/Stop 本 identity，不清后继 C；正常或异常 FailAsync 只有确实 `Results.Fail` 成功才返回 terminal=true，不能把 receiver accepted 当完成，否则 finally 会错误挡住空正文异常的展开。通知后 Clear/C、迟到 failure/delta/complete 都受原身份门隔离。实际 OCR 确认后的文字失败不再清回本地图片而丢掉失败版本；保留必要图片 owner 到清空/替换，原清理和旧正文回退释放规则保持。

本批新状态、选择、回看、Prepare 和菜单动作都不发送 HTTP、不增加 max tokens；手动重新生成沿原明确操作。原 §10.7 对无正文429/503最多一次的既有 transport 策略保持，不把本批“不自动恢复重发”误写成所有 HTTP 一律一次。断流、拒答、截断、格式/认证错误及未知结果不因新 UI 自动重试或增限；实际 wire 验证固定4096并无图像字段。

| 原始执行 | 结果与失败保全 |
|---|---|
| r01 旧生命周期专项 | 127项，125P/2F。旧 Usage 的跨来源断言要求失败 snapshot 被丢弃，与本批明确保留失败版本冲突；保父精确源码/log/TRX，改为旧 usage/正文不变且新失败原因、snapshot、usage 都保留的严格双向断言 |
| r02 新旧专项 | 225项，224P/1F。旧图片回退测试要求失败后清掉版本且 late receiver 返回false；新合同保版本、terminal禁止内容修改但 receiver 可接纳空操作。保精确失败源码/log/TRX，改为 exact body/reason 不变断言，未过滤事件或删除测试 |
| r03/r04 | 231/231、241/241：空失败展开、手动收起/清空/新版本抑制，排队来源/设置门与 actual EOF/HTTP错误/通知后C补强 |
| r05 首次完整 | 1942项，1940P/2F。一项真实回归：来源失效后 admission 拒绝也跳过原最后成功正文回退；修中性 Abandon 走原 cache/identity，不接纳迟到失败原因。另一项旧视觉夹具竞态：HTTP计数先于 Started入队，First()为空；保精确源码与回执，仅增加等待事件入队，原断言全保留 |
| r06 完整 | 1942/1942；随后独审发现未覆盖的发送前异常展开路径，修实际 terminal 返回值并加四项0连接异常测试 |
| r07 完整 | 1946/1946 = 父1857 + 新89，0失败/跳过；随后代码复核发现 OpenAI JSON content_filter 早退丢合法content/usage，保候选源码，改为保合法正文/用量而不展示refusal原话，强化48组合的拒答部分正文并加2例精确refusal/content/usage分离 |
| r10 最终完整 | **1948/1948 = 父1857全保留 + 新91（正常/隔离/展示87、发送前异常4）**，0失败/跳过；逐testName/重数无父项缺失 |
| r08/r09、r11/r12 | 前后两组 App、Worker Release交叉构建各0警告/0错误，最终代码为r11/r12 |

新48组合为选区、拖放、实际 OcrOnlyCoordinator 的合成 native 输出→本地预览批准→单次文字确认，跨两协议与流/非流、截断/明确拒答/中断/无效响应。走实际本机 Kestrel、生产 serializer/parser、receiver、Core cache 和链接 Shell；不使用真实模型或 native OCR 成功。其余覆盖原始错误/refusal/exception sentinel 不泄露、无正文失败、401/403/400/502、实际 EOF部分正文、版本/来源回退与手动恢复、三版淘汰/原序号、终态锁存/Clear、取消/Off/锁屏/来源/设置/新版本排队、手动收起/旧展开失效、通知后Clear/C，以及持实际Shell本地图片的OCR确认失败清理。新四项使用公开 synthetic 不可用引用和抛错 secret callback，严格0 dials、无 SecretStore/凭据或实际 HTTP。

[固定 JSON](../test-evidence/2026-10-16-text-terminal-failures.json) 保存 manifest、执行 DLL、计数和原始回执路径。父累计346原件及1937旧证据保持原字节；新42原件含17父源码/项目/文档、4候选/失败源码、12 SDK log、8 TRX和preflight。首次 cached diff-check 如实检出 r01 原 TRX 内8行 SDK/xUnit 诊断空白的原生行尾空格；仅该精确原件路径另设 `-whitespace`，不裁原字节，其它路径检查不变。只改三个旧测试文件上述具体断言/等待，不删除用例、降低断言或改测试过滤；所有1857原名保留。无新增项目/包/锁；Provider测试项目只多链接1实际 notice partial。43锁/8workflow/8prompt保持，P4-04/F06/F08和P2-04只加说明证据，整体验收与全部P6不抬状态。

当前状态更正：基础表格、代码无折行局部横滚和表格局部横滚早已在生产 MarkdownFlowView/MarkdownCodeRegion/MarkdownTableRegion 与便携测试接线；旧 restricted-markdown/inlines/tables 文档“未实现”仅为当时切片，已标历史与后续链接。父 vision 文档误沿用“仍缺失”的句子已修正；父 JSON `NotRun/BasicMarkdownTablesAndCodeHorizontalScrollRemain` 保原字节为历史记录，以本条更正为当前状态。真实 WPF 横滚/布局/选择/阅读锚点、B12和完整 §11.5仍 NotRun，本批没有重验或宣称验收通过。

所有 dotnet 激活前后及命令显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；证书存储/信任 Unknown，不读改库/配置、不trust/删除。生产图片复用 Off，250/600ms、Windows/WPF/OCR/COM/nativeJob/质量及完整阶段仍 NotRun。Skia待许可/源码403不绕过、不restore/安装/加载/执行；Actions不查调、run数Unknown。无真实模型、费用、凭据、安全权限、新协议接受、main修改、强推、部署、Windows/CMake探针或新CI。
