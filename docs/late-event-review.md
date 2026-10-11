# 迟到错误专项修复与审阅入口

监督方静态复核发现 ProviderClient 的发送失败、非成功 HTTP、非流式读取失败未在发出 Failed 前检查身份。无 UI 阶段只能证明存在协议事件隔离缺口，不能宣称真实界面已发生串流。

## 实际复现

测试工具添加受控 HTTP 屏障；服务收到真实 TCP 请求后保持响应头或 JSON body，测试确认屏障 ready 后换文/关闭，再释放旧响应。没有用固定等待来假定服务进度。两个协议各测试等待响应头时换文/Off、JSON body 断开、迟到 401/503，旧事件必须为空或在换文后没有新事件，同时 B 正常完成一次、A 不重试。未修 ProviderClient 下 **10 项全部失败**，分别出现 CANCELLED、AUTH_FAILED、CANCELLED_OR_UNCONSENTED、STREAM_INTERRUPTED。

原始失败证据：`test-evidence/2026-10-11-late-events-before-fix.trx`，原始源码/测试摘要：同名前缀 json。该历史失败不覆盖当前回归结果。

## 修复与接收边界

所有直接失败出口已逐条检查，包括无效配置、发送失败、非成功 HTTP、body 打开失败、非流式读取失败、未知 MIME、SSE 读取/EOF、输出限额。响应头完成后检查身份，旧 503 不等待或进入再次发送。流式输出达到限额时，部分 delta 与紧邻 Failed 是两个异步出口，后者必须再次检查。

单靠生产端 yield 前检查仍不能保证已入 UI 队列的事件不会晚到。GenerateEnvelopesAsync 保留完整 RequestIdentity；ProviderEventReceiver 在宿主提供的目标上下文执行，通过 RuntimeGate.TryApply 原子复核 session/version/request/epoch。所有事件类型，包括 Started、Usage、Failed，都使用同一边界。回调异常显式传播，取消/失效排队事件不执行回调。接收端回调须同步短小，不等待/再 dispatch。

修复后测试新增五种事件排队换文、每个身份字段伪造、Off/Stop/Lock/取消、当前事件只应用一次、回调异常，以及真实双协议 HTTP 信封排队 A→B 的最终接收。当前请求的限额错误仍返回，确保没有以吞掉所有错误来制造通过。

Off 的原断言改为“无任何事件”，同时保留 HTTP 零请求断言；未授权但仍当前的身份仍返回 CANCELLED_OR_UNCONSENTED。原 Started 后关闭测试增强为只允许唯一 Started，排除迟到 Failed。没有删除测试或弱化隔离断言。

## 当前证据与限制

本地离线全量 **118/118**（Core45、Provider73）；源文件摘要、TRX、跨构建日志和探针 ZIP 摘要见 `test-evidence/2026-10-11-late-events-summary.json`。新增限额用例曾误把 Anthropic 正常 Usage 漏出预期序列，保留 `2026-10-11-limit-test-assumption-failure.trx`，现断言精确序列及 usage 值。

这是开发者修复与运行记录，供监督方独立复核；不冒充已完成的外部独审或 GitHub CI。正式 WPF App 尚未实现，接收合同还需宿主接入与 Windows UI 验收。P0 实际平台门槛仍 Blocked，A—L 106 条整体验收均 NotRun。
