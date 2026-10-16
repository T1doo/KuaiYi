# P6 分类与生成独立 usage 观察回执

基线 `a3b01123d452a9eb11c1f27d66b98a13a220e878`，正常分支 `dev/image-stage-usage`。实际核干净工作树、单 worktree/唯一写入者、HEAD 与默认网络远端祖先，main 保持 `51d077481cf6303602fcf5d2718213da9cf05178`。root 唯一写入，独审只读，未知锁/进程不动，不恢复旧实例。已读 AGENTS 与 SPEC7.6、10.5/10.6、TranslationVersion 数据模型、14.4、阶段依赖/退出门；本批只接上批暂停的离线截图 usage 元数据。

## 回执合同

`OfflineScreenshotCoordinator` 直接消费既有双协议的 `ModelEvent.Usage`，分别保存 `ClassificationUsage`、`GenerationUsage`。每份不可变回执含本地 `InvocationId`、明确 stage、完整 canonical RequestIdentity（session/version/request/epoch）、原图片 SourceVersion/SourceId、实际协议及两个可空 token 字段。usage 独立事件只更新元数据，不拼正文、不改变 Completed/Failed/Stopped，也不从正文、字符数、终态或模型名猜 token、费用。

- 阶段尚未开始时该阶段回执为 null；阶段枚举开始时建立两个字段都 Unknown、Reported=false 的观察项。本地 InvocationId 只关联一次 Provider 枚举尝试，不能证明已发 HTTP、服务端请求 ID、调用总次数或计费成功；原 Provider 的有限重试未改，也没有宣称覆盖服务端未知消耗。
- 只保存实际接纳的非负 Int64 值，0 是有效报告。每字段以最后一次有效服务端报告替换，允许向下修正；缺失/非法字段不清掉以前已知值，从未报告的字段继续 Unknown。相同报告不重复发 Usage 事件，不累加、不取最大值、不生成总价。Reported 表示至少接纳过一个有效字段，不表示用量已完整或任务已完成。
- 专用 usage 解析将缺失、null、非对象、非数字、负数、小数、越界、重复 usage 或重复 count 字段保 Unknown，其他合法字段照常保留。仍复用既有两个协议；choice/block index 的严格解析、JSON/协议结构、完整分类与完成门均不变。合法正文缺 usage 仍能完成；只有 usage 而无协议终态不能完成。
- 分类 JSON 合法且协议正常终止后，analysis 与分类回执在同一 canonical 目标提交缓存。再生成或同图新生成复用分类时，引用原不可变回执并标 ClassificationUsageReused，保持原请求/版本/协议/InvocationId，不新造分类消耗。即使原版本已被三版上限淘汰，只有这份有限原元数据随有效分类保留，不恢复旧正文/版本。显式重判同时清 analysis/原回执、创建新的分类尝试。
- 每版自己的生成回执绑定 job.Identity，不跟随用户当前浏览的旧版本；跨协议的新显式生成保留被复用分类的原协议，同时使用新协议生成回执。失败、截断、中断、停止之前已经接纳的字段保留在所属版本，不能把未知费用说成零。
- 每次 usage 都经过既有 receiver、budget、完整 identity/epoch/source/owner/settings/binding/lifecycle 门。先更新快照再通知；通知中 Clear/Adopt/Stop/Lock/Hide/后继 C 重入后，Receive 再核资格，不能续写旧缓存或启动旧阶段。模式只改下一次显式生成；Clear/换图/锁屏/隐藏清掉当前 owner、快照和分类引用，既有 jobs/finally 释放合同不变。

## 真实离线运行

| 运行 | 实际结果 | 范围 |
|---|---|---|
| r01-usage-red | 0 通过、4 失败，exit 1 | 双协议流/非流：实际 HTTP 已返回不同阶段 usage，旧消费逻辑未保存，回执为空 |
| r02-usage-fixed | 73/73 | 上批 69 个实际编排用例加最初 4 个 usage 用例 |
| r03-usage-expanded | 155/155 | 实际编排加 usage parser 扩展 |
| r04-provider | 1228/1228 | 保留父 1142，新增首批 86 |
| r05-usage-ownership | 4/4 | 浏览旧版本、两协议互换后的缓存归属 |
| r06-provider-final | 1232/1232 | 保留父 1142 测试名多重集合，最终新增 90：58 编排、32 parser |
| r07-app | 0 警告、0 错误 | 最终 Release/no-restore/EnableWindowsTargeting 交叉构建 |
| r08-provider-notification-fence | 1232/1232 | 仅收紧异步测试等待，生产源码与 r07 不变；等待第3次 Usage 已通知再检查快照，避免检查抢先 |

直接 Link 生产 App 组件，真实 Core Gate/Provider/PNG 与既有 Kestrel loopback socket；没有新验收工具或真实模型。覆盖两阶段、双协议流/非流、缺失/不完整/非法报告、有效0/大整数/重复/向下修正、不以usage完成、两阶段失败/中断、三版与重判、取消/迟到/清除/换图/锁屏/隐藏、模式与 C 重入、阅读旧版和跨协议归属。所有像素、正文、token 数字与地址均为合成 fixture 数据，不能作为真实服务质量或计费证据。

15 份 SDK log/TRX 与 3 份红版源码原字节唯一命名并精确 `-text`，父 124 份原件和 1705 份既有证据保持。红版 Coordinator 已增加最终数据契约表面、消费逻辑尚旧，因此不是父提交原字节；红版 tests 是最初 4 个真实 HTTP 用例，adapters 与父提交原字节一致。最终保留这些用例并扩展，没有弱化断言。具体 manifest、原件 SHA256、增量计数、独审与失败记录见[固定证据](../test-evidence/2026-10-16-image-stage-usage.json)。

## 保持的阶段与安全边界

普通 Shell/截图/菜单没有接入本组件，On 截图仍本地预览、未翻译未发送；生产离线复用 Off。250/600ms 和来源/密码/完整性/取消/清理全部门保持；Core、ProviderClient/receiver/budget/请求序列化、8 个提示模板、8 workflow、43 锁不变，无新依赖。P6-04/05 继续 InProgress/validation NotRun，不把局部元数据切片当阶段通过。

Linux 无 WindowsDesktop 或交互桌面；真实 WPF 测量/排版/选择/阅读/焦点、Windows/COM 所有权与时限、截图/OCR、模型与视觉质量、完整 P2/P4/P6 仍 NotRun。没有 Windows/CMake 探针、兼容层、权限/隔离绕过或新增 CI。

所有 dotnet 在激活前后 export 且命令前显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`；旧证书存储/信任和历史覆盖前字节 Unknown，不读库/配置/trust/删除/重造。Actions 不查询、不调度、不绕既有 Forbidden，远端运行数 Unknown。无 main 修改、强推、部署、新费用/凭据/权限/协议或真实调用。
