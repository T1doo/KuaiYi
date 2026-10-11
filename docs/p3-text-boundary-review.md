# P3-06 显式本地测试模式边界父审修复

基线 `88d4d59d97a12c562a83e2e05221c409597330fb` 尚未获本轮监督签收。父审发现真实缺口：启动时检查 fixture URL 后，完整设置可把测试 profile 改成任意 HTTPS，旧 `ProfileTransport(...,true)` 接受它，却仅按 HTTP 开关显示“本地测试输出”。本轮只修该阻断，不进入下一阶段；其他父审仍待结论，Windows/整体验收状态不变。

旧生产路径的 25 个新反例实际运行，22 失败/3 通过；外部 `.invalid` URL 只送入配置和校验函数，从未调用外部传输。两个旧底层 HTTP 反例只发送至测试专用 loopback 服务，证明确实能绕过原边界。认证样本只用已有公开合成 fixture 字符串，无真实密钥。原失败 TRX、源码七份精确字节、原摘要/包继续保全。

现在 Program 从显式命令的原 URL 生成不可变 DevelopmentTranslationBoundary，固定回环主机/IP、端口、run/scenario 前缀与两协议的完整 endpoint。同一对象传入 Shell、TextTranslationCoordinator 和 ProviderClient。后续 settings、草稿、恢复文件及克隆的 DTO 都无法重新授权地址。每次冻结、确认、重新生成、密钥访问前及实际 HTTP 尝试都要求模型 `TEST_ONLY_FIXTURE`、Authentication.None、SecretRef=null、无自定义 headers、相同授权 fixture。配置越界立即使 Gate Off，当前任务与排队 UI 事件取消；启用前也拒绝越界配置，缓存回看不发送。

ProviderClient 自身的只读边界独立兜底，删除或替换请求记录的授权标记不能绕过。带任何 SecretLease 的测试请求在构造 header 前拒绝；底层 DTO endpoint 再校验授权标记。实际连接通过 ConnectCallback 固定 loopback IP（localhost 固定 127.0.0.1），禁用代理与 cookies，不做 DNS 回落；仅允许原端口，禁止重定向。标识改为 `IsDevelopmentFixture` 授权证明，不再从 `AllowLoopbackHttp` 推断“本地测试输出”。普通真实 Provider 不受该测试模式边界替代。

39 个专项全部通过：旧25不删除，另加固定 client 反克隆/认证/秘密租约四项、双协议 BasePrefix/ExactEndpoint 四项、标签证明一项、真实设置原子提交/备份恢复两项、旧真实 HTTP 排队后提交越界配置两项、localhost 固定回环连接一项。正式 WPF 入口已交叉编译；上述目的队列/标签属性为托管生产逻辑验证，不是 Windows 图形或焦点实测。原 P3-06 54 个协议/调度用例保留名称与断言，其合成认证/故障服务改用测试文件中的显式传输适配器，不冒充应用受限测试模式。

完整回归 Core1216 + Provider170 = 1386/1386，Node63/63；27 工程 locked Release 零警告/错误，27 锁和原规格字节不变，没有安装新依赖。最终回归后运行源码与测试保持原字节。独立新 staging 写入 `artifacts/KuaiYi-P3-text-boundary-managed-win-x64.zip`；20 份旧包最终摘要、16 个精确历史 commit 摘要核对，旧误覆盖后恢复事故继续保留，不声称历史全过程未改。新 manifest 与十个 PE32+ x64 入口检查，正式 app 无 Mock 引用。

证据见 `docs/test-evidence/2026-10-11-text-boundary-*`。新包仍为框架依赖审查构建，非安装器且不含原生 DLL。P0/E06 Blocked，P1/P2/P3 InProgress，106 整体 NotRun；Windows 原生、UIA/COM、焦点/动画/DPI、DPAPI/ACL、安装卸载、远端 CI、真实模型均未验。这里只提交已具体修复的测试模式边界，完整 P3/P4 和其他父审结论不提前签收。
