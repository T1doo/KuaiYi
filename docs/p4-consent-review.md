# P4-01 固定样本授权、总预算与表单编辑父审修复

基线 `7fecdf7995b8389457dbf52f279f771a1a715f4a` 尚未获监督签收。父审指出固定样本 Start 写入普通会话 consent，后续真实选区启用可跳过确认；传输还硬限制120秒，保存较大值不生效。本轮合并修复这两项及所有相关表单编辑取消，不进入下一阶段，其他父审结论待返回。

固定样本目标授权现在仅存于 RuntimeGate 的当前测试身份：创建身份在同一锁内核原 epoch/binding/资格并保存该次 origin，不读写普通内容 consent。每次发送只匹配该测试 origin；即使另一个 origin 原来有普通授权，测试身份也不能使用它。失败开始不留授权，取消/Off/锁屏/替换/绑定撤销清除目标，身份失效后无法沿用。已有真实内容授权保持原字节语义；成功测试后 ShellLifecycle 的真实内容 enable 仍先检查自己的 consent，拒绝确认保持 Off，明确同意后才添加普通会话授权。测试只发送原固定 source/system，不允许用户正文、图片；SecretStore 的请求绑定与最后HTTP检查仍生效。

ProviderRequestBudget 以一次 TimeProvider.GetTimestamp 冻结1—600秒总预算，正式调度和固定测试在秘密访问前创建，并把同一对象传到 ProviderClient；运输只使用剩余时间，不重新从120秒起算。连接10秒、响应头/首事件30秒、idle20秒的独立阶段截止保留，仍用系统阶段计时；可控时钟测试只替代总预算。60/120/180秒经过真实生产预算与loopback挂起HTTP，截止前一毫秒仍等待、截止时取消；共同凭据/传输期限与过期零发送也验证。总预算超时不冒充用户主动取消。

所有请求相关表单字段已统一接 TextChanged / PasswordChanged / SelectionChanged / Checked / Unchecked：密码/新输入揭示框、认证头名、温度、token方式、限额、超时、能力、模型/URL/端点、headers、HTTP风险、stream/store以及profile选择。编辑通过 DraftEdited 推进独立 revision，既取消运行中的测试，也使尚未确认的 intent 无效。最终UI派发还复核该revision；旧完成不能标新草稿成功。Windows 实际控件事件未运行，本轮真实生产 workflow/目的队列与交叉编译只作为对应证据。

旧授权反例17项11失败/6通过（Provider11中8失败，Core6中3失败），原TRX/源码7份保存。旧总计时观察重放以7fecdf ProviderClient源码为测试专用fixture，只改类名、观察CancelAfter参数及桥接非公开ConnectAsync；行为不换，实际loopback运行60/120/180断言2失败/1通过。最终相同三组值和精确相等断言观察修复后的生产Timer；原测试体与旧fixture保全，不改期望值或放宽容差。首轮fixture访问非公开API编译错误、实时时钟亚毫秒观察失败保留；改测试时钟为固定原点，不改生产时间或断言。

新30项最终全部通过：Core6、Provider24（授权14、生产预算7、原三时限3）。完整 Core1262+Provider237=1499/1499，Node63/63，27 locked Release零警告/错误；原1256/213逐项名称和27锁/原规格字节保留。整套首试一个旧捕获bounds用例在额外并行负载下超过600ms，原deadline与断言未动，保存失败后无额外测试负载重跑通过；第二套旧测试专用transportFactory未映射新总预算，补夹具参数映射后原1秒超时断言恢复通过。新测试初始化顺序错误（Shell构造早于fixture保存）也保存后按真实启动顺序修正。

独立包 `artifacts/KuaiYi-P4-consent-managed-win-x64.zip` 不覆盖旧包；逐文件manifest、十个PE32+x64与无生产Mock依赖核验，22旧包与18精确历史commit摘要最终比对。此前覆盖旧包后恢复事故继续保留，不称历史全过程未改。证据 `docs/test-evidence/2026-10-11-p4-consent-*`。其他父审未签收，P0/E06 Blocked，P1/P2/P3/P4 InProgress，106整体验收NotRun；Windows UI/焦点/DPI/动画/DPAPI/原生/安装、远端CI和真实模型未验，无真实key读取/付费调用/新依赖/扩权/部署。
