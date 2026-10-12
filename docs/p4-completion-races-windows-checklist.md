# 截止与交付竞争 Windows 补验（全部 NotRun）

仅公开本地 HTTP fixture 和独立临时配置；不读取真实 key、不调用真实模型。

1. 在实际 Windows .NET 请求路径用可控本地响应，验证连接/响应头完成正好落在截止点时无迟到 Started、正文或成功；测试线程池忙时 timer 通知延后也由原单调期限拒绝。
2. 暂停 WPF 事件消费至原总期限/idle 已过，再恢复 JSON/SSE；已有正文保留，新增 Usage/Completed 不作为成功交付；原因标签分别匹配 REQUEST_TIMEOUT/READ_IDLE_TIMEOUT。
3. Started 后暂停至原 first 已过，恢复不能另起首响应预算；请求仍使用冻结的总起点和 profile。
4. 用户取消、Off、锁屏、替换身份与完成竞争：取消可报告当前请求的 CANCELLED，失效身份丢弃所有迟到事件，不能错误恢复 On、重发请求或串版。
5. 连接池复用、真实 Windows TLS/代理/DNS、源应用焦点与多屏 DPI、动画、DPAPI、原生组件、安装卸载与远端 CI 继续各自补验。

本地真实 socket 和受控时钟验证不替代这些平台验收；审查 ZIP 仍无安装器和原生 DLL。
