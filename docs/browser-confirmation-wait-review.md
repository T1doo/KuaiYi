# 浏览器确认等待：跨模块预算修复

基线 `d25092acc907a16a7636dd95656fdac5a071da51` 的扩展 Offer→Ready 统一等待五秒，生产 App 接收端允许确认三十秒。用户正常犹豫超过五秒就断线。本轮只修复此监督发现并提交独审，没有越过 P0 平台退出门槛。

## 预算和协议

| 阶段 | 上限 | 转换条件 |
| --- | --- | --- |
| 握手 / Offer 首次回复 | 5 秒 | 只有一次、匹配操作与版本的 `ConfirmationPending` 或有效 `Ready` |
| 等待明确桌面确认 | 30 秒 | 接收端实际进入确认阶段；Pending 不含 grant，不允许读取图像 |
| 取图及传输 | 30 秒 | 有效 Ready 后开始，包括浏览器取原图与最终桌面交付 |
| 普通 Native Messaging 帧确认 | 5 秒 | Begin、每片 Chunk、Commit 各自等待，受传输/总上限约束 |
| 整次操作 | 65 秒 | 从连接操作开始，任何阶段或通知不能续期 |

`BrowserTiming` 和扩展 `TIMING` 定义相同常量。接收端发送内部 IPC `ConfirmationPending`，`DesktopPipeChannel` 验证连接、角色、操作 ID、固定预算后通知 `BrowserSession`；宿主才发送外部 Native Messaging 同名进度帧，包含协议版本。扩展严格验证唯一字段和唯一阶段，重复或改预算通知立即拒绝。普通确认仍为五秒，没有统一延长所有等待。

扩展使用 `performance.now()`，C# 使用 `TimeProvider.GetTimestamp/GetElapsedTime`；计时器取消之外，阶段转换前及交付迟到回复前再次检查单调时间。墙钟回拨、截止但回调尚未调度均不能接收 Ready。Ready 后独立传输计时器涵盖无帧等待的取图阶段，断线/截止取消所属 acquisition signal。

宿主在桌面确认等待期间并行监视唯一下一次 stdin 读取。匹配 Cancel、EOF 和提前数据撤销确认、关闭真实桌面连接，并观察迟到读取/授权结果；成功后复用该读取，不另起第二次读取。接收端仍检查 epoch、完整身份、nonce、连接、peer、截止和队列资格。只有 Ready 后服务 Worker 调用取图。截图 fallback 全拒绝、同源无凭据 PNG、默认缺配置拒绝、本地模式不发送模型等边界保持。

## 验证与原始失败

- 基线：新增测试 fixture 与 friend 可见性，生产协议/宿主/接收端行为保持原样；真实扩展发送器→独立宿主进程→实际 Linux 临时管道→生产接收状态机，合成回答等待实际六秒，**1/1 失败**。11 份当时源码字节、摘要、原始日志和 JUnit 都保存。
- 修复后：相同六秒等待成功，随后只交付一份公开测试 PNG；实际队列副本清零，无重复授权/传图。
- 新增 **20/20**：上述真实六秒案例，9 项实际 Service Worker→宿主→接收端链路（允许超过五秒、无回答、拒绝、工具栏取消、Off、实际管道失联、过期允许、无计时回调的过期允许、迟到 Ready），另10项扩展阶段/预算/非法字段案例。链路负例均零图像读取、零 Begin/Chunk/Commit，确认只调用一次，端口只关闭一次，至多一次 Cancel，队列无保留。Ready 已发但被延迟投递时，宿主可正常接收 Cancel 并以0退出；仍未提交图像。
- 中间9项中1失败：测试错误期望上述正常取消退出码3，实际为0；原始结果保留，改为该场景精确0、其他场景精确3，保留所有零采集/零提交/清零断言。没有删除测试或接受宽泛退出码集合。
- 全回归：Core **376/376**、Provider **77/77**，扩展 **63/63**，共516个独立子项，零失败/跳过。原65接收端和7读取竞态断言保留，只补严格 Pending 元数据与初始五秒预算断言。19个托管工程 locked Release 构建零警告/错误，无新增外部依赖。

真实执行的是生产协议、Native Messaging 字节流、独立进程、本地管道、接收端、门控和内存清理；Chrome API、图像 fetch、Windows peer/角色核验、回答及加速时间明确为测试控制输入。六秒案例使用实际时间，其他截止使用两端受控时钟；不包含 Windows 图形确认、Chrome/Edge 安装态或真实授权。fixture 只在测试工具入口存在，不进入正式宿主或审阅 ZIP。

原始证据和当前源摘要见 [summary](test-evidence/2026-10-11-browser-confirmation-wait-summary.json)，可用 `scripts/test.ps1 -Suite Offline` 复跑。测试 API 使用当前环境 Node 24.19.0 内置模块，不需要 npm 或真实模型。

## 审阅产物与未验证边界

`artifacts/KuaiYi-P0-browser-confirmation-wait-win-x64.zip`：554318 字节，SHA256 `01c1476aa1eb8157e0403dc2d62d15d167c636d06d8b85e1758c2cd196ba1358`。34 个运行/扩展文件和一份 manifest 全部逐文件校验。只用于代码审阅，框架依赖，不是安装包；没有 fixture、安装配置、实际扩展 ID、信任 receipt、OCR/Shell 原生 DLL。旧接收端 ZIP 及其摘要保持原样。

来源 argv、allowed_origins、注册配置及模块摘要不能认证浏览器调用者；逐次桌面确认的有限威胁模型未变。生产缺实际安装配置继续拒绝。同步 Windows 身份/文件校验不因协议计时器而宣称已取消或有界退出；实际耗时/关闭仍需 Windows 验证。Ready 后在浏览器取图期间，App 停用/失联会拒绝后续提交，但即时传播到浏览器 acquisition 仍未通过真实平台验证；不能把本轮等待确认阶段负例扩大为所有平台断线证明。

Windows SID/PID/session/DACL、WPF确认焦点、真实浏览器取图、锁屏/退出、安装卸载均 **NotRun**，106项整体 **NotRun**，P0 **Blocked**，E06同步OLE浮钮冻结缺口不变。无安装/注册/实际回答/凭据/模型调用/系统安全变更/发布。本专项提交监督独审，后续阶段等待该审阅结论。
