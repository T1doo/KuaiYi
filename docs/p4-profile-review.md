# P4-01 配置档、协议映射与固定公开内容测试

监督已限定签收基线 `81bd6707091eb1e01cd327623d1e08ec16c08364`；本轮按原 P4-01 补实际缺口，不把两协议已有实现重新包装为新任务。P0/E06 仍 Blocked，Windows/整体验收尚未执行，完整 P4 文字 Alpha 未签收。

新增统一 ApiProfilePolicy / ApiProfileMapper：预览、保存校验、正式发送共享保留代理路径前缀的解析器。BasePrefix 仅追加明确端点路径，ExactEndpoint 不追加；已填完整请求地址但仍选前缀模式明确拒绝，不猜多个地址、不重复 v1。默认 HTTPS；本机 HTTP 与远端/局域网 HTTP 分别需要明确风险设置，普通 HTTP 结果持续标明非加密连接，TLS 正常校验、重定向仍禁止。

正式配置和 WPF 表单可编辑端点路径、四种输出参数策略、温度、上下文上限、图像字节上限、五项三态能力、超时、stream、store:false、非敏感头及加密自定义认证头。两种官方预设提供正确协议/地址/认证默认，模型手输、不枚举猜测支持；预设不沿用旧密钥。能力属于 profile+模型+端点，界面修改身份字段重置为 Unknown，手动确认支持才发送温度和 OpenAI stream_options。明确不支持流式则发送 stream:false、JSON 一次展示；结构化 JSON/视觉未知不谎称已支持，视觉输入还受能力和字节限额检查。上下文上限为配置元数据，不伪装成精确 tokenizer 计数，也不发不支持的 max_context_tokens 参数。

OpenAI 输出上限可选 max_completion_tokens / max_tokens / 省略，绝不同时发两个；Anthropic 必须 max_tokens，不支持省略或 max_completion_tokens。温度 OpenAI 0—2、Anthropic 0—1，未知/不支持时省略并显示提示；store:false 只发到明确配置的 OpenAI，默认无供应商 beta/工具/思考扩展。含凭据的自定义认证值使用既有 SecretRef/SecretStore，只在单次请求加入；非敏感头禁止传输控制/常见凭据头、重复大小写和 CRLF。支持一个加密主认证头与至多八个非敏感附加头；多个独立加密附加头仍不是本轮提供的界面能力，不能把其值放进非敏感表单。旧开发 profile 的 OpenAI-Beta 现在明确报不支持，不静默忽略。默认文字总时限修为规格的120秒；原保存的有限超时仍按原值使用。

固定连接测试是正式设置入口：仅测试已保存配置，未保存参数/新密钥提示先保存重开；明确显示完整脱敏目标、模型和固定公开样本，确认后才发送。它与生成共用 RuntimeGate 的单请求资格，正在生成时拒绝测试，测试期间不开启 On、不会取词/截图/读剪贴板。Off 可执行这一明确授权的固定测试，但传输层拒绝测试身份携带任意正文、不同 system 或图片。每次请求单独密钥租约、最新设置和绑定检查；切换/编辑/关闭设置、取消、Off/锁屏或提交配置取消原测试，目的队列复核原身份，原任务结算后关闭。输出/错误来自真实 Provider，不硬编码成功，不把无 usage 伪成零费用。

正式文字调度的配置切换取消放在 TextTranslationCoordinator，取消其正在生成的旧 profile/参数快照；缓存回看与普通重新生成保留所示版本快照。通用 SettingsRuntimeLink 的独立消费者/旧快照保证不被破坏。父签收测试模式仍由命令原 URL 的不可变边界、无凭据固定模型和 client 固定回环 socket 兜底；普通参数映射、风险 HTTP 开关、固定测试入口都不能扩展该边界，旧39专项保留。

83 个新增用例（Core40/Provider43），含真实临时设置保存、AES 合成密文→门控租约→两协议实际 loopback HTTP/SSE/JSON、参数/认证/固定样本/能力/图像限额/取消/流内错误、配置消费者切换和不允许 Off 测试上传用户内容。原通用快照保证一项曾被全局取消破坏，已移到正式消费层；最初13个 Anthropic默认枚举及1个隐式测试入口断言失败，修协议默认和保留受限 fixture 标识，未降低断言。xUnit分析器编译错误也保留，改用等价过滤重载，未删除测试。

最终 Core1256 + Provider213 = 1469/1469，Node63/63，27 locked Release 零警告/错误；原 Core1216/Provider170 名称全部保留，27锁不变。配置 schema 按真实模型生成并通过原 schema 一致性断言，原 SPEC/中文产品书不改。最终运行源码/测试冻结后复核；本轮独立包 `artifacts/KuaiYi-P4-profile-managed-win-x64.zip` 不覆盖任何旧包，逐文件 manifest /十个PE32+x64及无生产Mock引用核验，21旧包与17精确历史摘要最终核对。旧误覆盖后恢复事故仍保留，不声称历史全过程未改。

证据 `docs/test-evidence/2026-10-11-p4-profile-*`，Windows步骤见 [清单](p4-profile-windows-checklist.md)。包仍框架依赖、无安装器/原生DLL。实际 Windows API表单/能力控件/关闭等待/焦点/取词/浮窗动效/DPI/DPAPI/ACL、安装卸载与远端CI NotRun。没有读取真实密钥、调用真实或付费模型、安装依赖、部署或接受新协议；1469便携与真实测试HTTP通过不代表Windows或模型质量验收。
