# ADR0019 冻结阶段配置与有效进展读取

状态：源码实施、Windows及真实服务未验。依据SPEC10.7/P4-04，父限定基线646c991。

schema1新增可选不可变RequestTimeouts，旧10/30/30/20默认；有界范围1—60/180/300/180秒。传输DTO/每版保存原记录，不动态引用当前设置。已有总预算在凭据前冻结并贯穿所有阶段，阶段token链接total，不能累计放宽；headers从send起包含建立过程，first从成功headers正文开始，idle从最近有效进展。

HttpClient按连接秒数有限池缓存，ConnectTimeout涵盖.NET建立过程；ConnectCallback包装真实socket/原固定回环边界，PlaintextStreamFilter仅观察可用状态，保留代理/TLS/认证/重定向规则。控clock的延迟dial是明确测试seam；实际回环TLS停滞使用系统ConnectTimeout，不能声称虚拟clock控制了.NET内部TLS时钟。

读取以意义而非字节计时。SSE的底层ReadAsync直接用当前阶段token，未知/keepalive/空正文/相同usage不重置；JSON增量只计已知协议/选中正文可解码scalar，完整验证/HTTP结算后一次展示。字段重排可能保守提前截断，界面可有限增时；未知/重复/坏字节不延时，内存1MiB/深度32保持。timer安装后查原始origin截止，配置/暂停/旧身份取消仍走canonical gate。

phaseClock与既有total clock分开注入以保持原观测合同，正式均System，可控测试可同一实例。所有新错误固定短码，原文/错误体/秘密不进日志。只明确信号429/503且无正文重试一次，总/阶段预算不授权重传未知网络结果。Windows/代理/DNS/人工质量的证据独立于托管协议回归。
