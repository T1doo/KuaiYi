# 首个 Chrome HTML 请求：固定九段观察

起点 `587e42d9a20fac7e62067f8527bed46b79abc423`。旧5/6与FocusAncestors首次软超时仅指出检测位置，不能归因；本轮停止改祖先和重复六项，只按监督授权记录一次首个Chrome HTML请求。

固定18个整数/空槽按索引组成九段：Preflight（capture入口的source/排除/完整性）、ComInitialize、AutomationCreate、Root、Focus、Hit、Password、Walker、FocusAncestors。起止均来自原origin的steady_clock累计整数毫秒，差值为End-Start；scope观察只读clock、不调用provider、不检查/重置期限、不改原拒绝/早退/异常处理、不开新线程/等待、不重试/预热/缓存。诊断关闭时没有clock读且不序列化timing。正常返回/异常离开scope结束记录；进程被Kill没有scope补填或旁路共享记录。

仅显式TimingDiagnostic+DiagnosticOnly+ComDiagnostic启动此观察；SDK child参数phase开关由该诊断路径传入，生产默认关闭。原250/600ms、来源/密码/完整性、几何/Unicode/额度、暂停与清理不变。父端只有真实exit0、nonce/Ping+BinaryChunk双帧、EOF、原600ms和取消检查及原结果校验通过后发布；迟到/取消/dispose/协议失败/非零退出/Crash均清空，不利用marker或Killed子进程填记录。

投影仅固定Stage、State、StartMs、EndMs、DurationMs。整数0—600，未执行NotRun三个值null；超界及其后执行段OverLimit三个值null，不将异常值当成因果证据。长度固定9，顺序固定，连续执行前缀，拒绝NotRun后再执行；无任意phase字符串、文本、路径、对象ID、指针或异常正文。PS回执要求有限正常Outcome、对应完整非Unknown BrokerObservation及BrokerSettled，拒绝Paused/CleanupPending/SourceNotCurrent等伪计时。首次累计跨250的位置与最大Duration可能不同，二者均不是已证根因。

实际本地验证：native recorder **27/27 Pass**（g++C++20/Werror+ASAN/UBSAN；此前LSANptrace限制仍Blocked）；原/新Selection xUnit原断言与真实控制子进程 **152/152 Pass**，包含18槽形状/累计差值/首跨≠最长/越界/空/倒序/前缀、真实nonce/extra/nonzero/hang响应和600ms/取消/dispose迟到不发布，旧135不删除。托管定向构建和probe实际源类型检查均0warning/0error。PS固定摘要 **86/86 Pass**、脱敏 **91/91 Pass**，runner AST与diff Pass。无端口runner用于当前VSTest socket限制；不是Windows UIA/提供者安全实测证明，WindowsSDK构建与定向请求待测，NuGet在线审计NotRun。

workflow仍现有免费windows-2022/read-only/15min，仅启动Chrome Html的第一个Word合成请求一次，不启动Edge或Chrome其它视图，不先做其它UIA，不补旧backend/六项/120/GetData、不切生产、不调用模型、不安装或扩大权限。已有WPF纯几何四检查不调用UIA。若未复现如实记录，不自动追加；若进入祖先前已过期按对应段排查，若祖先自身耗时才审请求内BuildCache；若固定初始化耗尽预算记录架构约束，不移出计时或放宽期限。

精确独审后仅一次定向CI，结果待测。所有旧失败、P0/真实Windows安全与物理桌面NotRun保留。GetData代码与Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 继续独立保留、不重跑。
