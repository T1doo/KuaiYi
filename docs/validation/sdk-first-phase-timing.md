# 首个 Chrome HTML 请求：固定九段观察

起点 `587e42d9a20fac7e62067f8527bed46b79abc423`。旧5/6与FocusAncestors首次软超时仅指出检测位置，不能归因；本轮停止改祖先和重复六项，只按监督授权记录一次首个Chrome HTML请求。

固定18个整数/空槽按索引组成九段：Preflight（capture入口的source/排除/完整性）、ComInitialize、AutomationCreate、Root、Focus、Hit、Password、Walker、FocusAncestors。起止均来自原origin的steady_clock累计整数毫秒，差值为End-Start；scope观察只读clock、不调用provider、不检查/重置期限、不改原拒绝/早退/异常处理、不开新线程/等待、不重试/预热/缓存。诊断关闭时没有clock读且不序列化timing。正常返回/异常离开scope结束记录；进程被Kill没有scope补填或旁路共享记录。

仅显式TimingDiagnostic+DiagnosticOnly+ComDiagnostic启动此观察；SDK child参数phase开关由该诊断路径传入，生产默认关闭。原250/600ms、来源/密码/完整性、几何/Unicode/额度、暂停与清理不变。父端只有真实exit0、nonce/Ping+BinaryChunk双帧、EOF、原600ms和取消检查及原结果校验通过后发布；迟到/取消/dispose/协议失败/非零退出/Crash均清空，不利用marker或Killed子进程填记录。

投影仅固定Stage、State、StartMs、EndMs、DurationMs。整数0—600，未执行NotRun三个值null；超界及其后执行段OverLimit三个值null，不将异常值当成因果证据。长度固定9，顺序固定，连续执行前缀，拒绝NotRun后再执行；无任意phase字符串、文本、路径、对象ID、指针或异常正文。PS回执要求有限正常Outcome、对应完整非Unknown BrokerObservation及BrokerSettled，拒绝Paused/CleanupPending/SourceNotCurrent等伪计时。首次累计跨250的位置与最大Duration可能不同，二者均不是已证根因。

实际本地验证：native recorder **27/27 Pass**（g++C++20/Werror+ASAN/UBSAN；此前LSANptrace限制仍Blocked）；原/新Selection xUnit原断言与真实控制子进程 **152/152 Pass**，包含18槽形状/累计差值/首跨≠最长/越界/空/倒序/前缀、真实nonce/extra/nonzero/hang响应和600ms/取消/dispose迟到不发布，旧135不删除。托管定向构建和probe实际源类型检查均0warning/0error。PS固定摘要 **86/86 Pass**、脱敏 **91/91 Pass**，runner AST与diff Pass。无端口runner用于当前VSTest socket限制；不是Windows UIA/提供者安全实测证明，WindowsSDK构建与定向请求待测，NuGet在线审计NotRun。

workflow仍现有免费windows-2022/read-only/15min，仅启动Chrome Html的第一个Word合成请求一次，不启动Edge或Chrome其它视图，不先做其它UIA，不补旧backend/六项/120/GetData、不切生产、不调用模型、不安装或扩大权限。已有WPF纯几何四检查不调用UIA。若未复现如实记录，不自动追加；若进入祖先前已过期按对应段排查，若祖先自身耗时才审请求内BuildCache；若固定初始化耗尽预算记录架构约束，不移出计时或放宽期限。

精确独审后仅一次定向CI，结果待测。所有旧失败、P0/真实Windows安全与物理桌面NotRun保留。GetData代码与Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 继续独立保留、不重跑。


## 唯一一次定向实测：复现 SoftTimeout / RangeText

代码与Actions执行SHA均为 `9398edf5c3499463455571e1b3b28a74b8815240`，tree `27db370549d7bda666cc37111774fc0abcf647e8`，唯一父提交 `587e42d9a20fac7e62067f8527bed46b79abc423`。精确独审冻结 `09b8bcb1ba44cacf9e54bc12cb3ce1c08f6dbed9` 为同树。[run38398925910](https://github.com/T1doo/KuaiYi/actions/runs/38398925910)，job115301220503，attempt1，**success**（有界诊断/清理完成，并非取词通过），无追加或重跑。

真实摘要 **Observed / TimingDiagnosticOnly / SdkCom / Planned1 / Correct0 / ExitCode0**。只Chrome154.0.8037.98 Html第一个Word请求：**SoftTimeout / RangeText / None / HRESULT null**，Mismatches0。SequenceCompleted、BrokerCleanup、ApplicationCleanup、ProfileDeleted和RunnerSettled全true；marker setup Ready，BrokerExitCode与ReachedBoundary均null；无Crash/HardTimeout。该timing由正常完整exit0、nonce/双帧/EOF/600ms校验后发布，未使用Kill后的标记。

| 固定阶段 | 起始累计ms | 结束累计ms | 差值ms |
|---|---:|---:|---:|
| Preflight | 0 | 0 | 0 |
| ComInitialize | 0 | 0 | 0 |
| AutomationCreate | 0 | 5 | 5 |
| Root | 5 | 110 | 105 |
| Focus | 110 | 156 | 46 |
| Hit | 156 | 163 | 7 |
| Password | 163 | 171 | 8 |
| Walker | 171 | 175 | 4 |
| FocusAncestors | 175 | 184 | 9 |

九段均Measured，顺序连续、无OverLimit/NotRun；数值是整毫秒截断，0不代表真实绝对零耗时。本轮SDK原生构建/预期exe门已通过（其后才启动请求），保留/WX；Windows摘要86/86、脱敏91/91、WPF纯几何4/4。

### 本证据支持的下一步判断

本次进入焦点祖先为175ms，结束184ms，均小于250ms；**未复现“进入焦点祖先前已耗尽软预算”**。焦点祖先差值9ms，不能据此认为其为主要耗时，也不据此改用BuildCache。Root105ms与Focus46ms是这九段中较大的观测值，不能把“较大”当成已证根因或不可避免的固定成本；目前不能宣称初始化本身固定耗尽250ms或存在已证架构约束。

首次软超时检测为RangeText，说明剩余未计时流程在184ms以后耗尽余下预算。九段中没有累计跨250的段，不能给Root贴“首次跨线”标签，更不能给未观察的GetText赋造耗时。焦点祖先之后还有命中祖先、候选密码/pattern/几何、GetSelection、范围bounds/text与后续判断；RangeText是检测位置，不是单独GetText耗时/签名根因证明。下一最小证据缺口应在184ms之后的这些既有调用区间，继续同一origin/只读固定观察，不增加provider调用或预热；本轮 **未扩展这些计时段、未继续CI**，保持实际已测代码。

原两个5/6、四AV与硬超时历史失败不覆盖，生产后端仍default-off，ModelCalls0、ClipboardFallback Disabled。其它Chrome视图/Edge/UIA、旧managed/六场景/120/GetData均NotRun；Windows定向安全/物理划词/PDF/Markdown/DPI/安装/P0整体验收无新增通过结论。

公共job日志本地忽略文件 `artifacts/windows-selection/windows-phase-38398925910.local.log`：**20233 bytes**，SHA256 `6bd2c86f0b942876184f9429f83e83be9b6cd7971aece1100b78484c4c866598`，没有上传原始stdio/receipt。原GetData代码与Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 独立保留、未重跑。
