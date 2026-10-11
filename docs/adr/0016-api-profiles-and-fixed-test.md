# ADR0016：配置到请求的统一映射与固定样本测试

规格10.1—10.8/P4-01要求的URI、可选参数、能力与固定测试缺口通过Core ApiProfilePolicy和Providers ApiProfileMapper接线；设置预览/校验/正式生产运输共享解析规则，保留代理前缀，完整端点不追加，不按模型名猜能力。温度只在显式支持时发送，stream_options只用于OpenAI流式/已支持用量；Anthropic保留顶层system、必需max_tokens与独立事件生命周期。参考官方 [OpenAI Chat](https://developers.openai.com/api/reference/resources/chat)、[Anthropic Messages](https://platform.claude.com/docs/en/api/messages/create)；兼容服务能力仍须手动修正，文档不代表实际服务已验。

固定连接测试必须显式确认，并只用已保存profile/加密引用。RuntimeGate增加独立的connectionTest目的：Off仅允许其固定公开样本，而非把主开关偷偷On；Provider最后拒绝任意source/system/image。测试不与运行中生成争抢，使用同一请求资格与原身份目的队列，不读取当前选择或截图。未保存密钥/草稿不做隐式合并、解密回填或悄悄保存，界面明确要求保存重开。

曾在通用设置链接内取消所有model/default修改，会破坏已验证独立消费者与旧快照保证；保持原通用语义，在正式TextTranslationCoordinator的不可变liveRequest引用上取消其自有生成。缓存与普通重生成仍可沿用旧参数；测试窗口自身的编辑/切换/提交取消其自有测试。每次秘密打开和实际发送继续检查原绑定。

自定义主认证头与普通非敏感头分开，凭据仅用SecretStore；不提供传输控制头、CRLF或非敏感认证头入口，不默认beta/工具/思考/存储。主认证只支持一个加密值，不伪称多个独立凭据头已交付。已签收的本地fixture边界保持只读且client固定回环，普通风险HTTP设置不能放宽测试模式。

## 父审补正：测试目的与普通上传授权分开

7fecdf曾在固定测试Start中调用ConsentTo，把一次样本许可变成普通内容许可；该声明不能签收。临时目标现在在身份创建锁内绑定，只供同一connectionTest identity发送，失败/退出不写普通consent，Shell真实enable仍独立确认。另删除transport120硬上限，使用Consumer在凭据前冻结的同一单调ProviderRequestBudget，传输使用剩余值，独立阶段截止保留。表单所有相关事件统一推进draft revision并取消，最终UI队列重检。详情见p4-consent-review与原始红绿证据，实际Windows事件/图形仍NotRun。
