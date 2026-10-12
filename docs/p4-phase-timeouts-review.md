# P4-04 可配置冻结阶段预算与有效内容读取

基线 `646c9915b71f561345310fdfa1d0f818f2430e27` 获父限定签收。核SPEC10.7/8.2/P4-04/F08/F12，补原连接10、响应头30、首有效响应30、无有效活动20固定，以及JSON仅总预算缺口；不跳P5/P6，不声称Windows或真实服务验收。

每profile新增不可变RequestTimeouts：connect1—60、headers1—180、firstResponse1—300、idle1—180秒；原默认10/30/30/20保持，旧schema1缺字段以内存默认补齐。总TimeoutSeconds仍1—600、默认120，与原秘密访问前冻结的同一单调ProviderRequestBudget贯穿凭据/连接/请求/读流/重试，不因阶段或有效活动另起总预算。高级四字段有范围提示，保存统一验证，编辑进入原ApiFormEdited取消固定测试，mapper实际传输DTO与每版快照携带完整record；参数不进模型body。普通旧版重生沿旧预算，当前设置动作取新保存预算；已发生成的profile阶段修改由原消费者自有取消规则撤销。

连接HttpClient按有限连接秒数缓存（最多60档、锁内创建/释放），handler在创建时固定ConnectTimeout，保留连接池与原代理/TLS规则。新的ConnectCallback包装真实DNS/TCP与阶段clock取消；正式normal为真实Socket/NetworkStream，显式开发边界仍优先走原固定回环IP，禁proxy/cookie，不允许测试seam扩大边界。PlaintextStreamFilter只观察建立后的明文协议可用状态，不改变流/证书，可区分真实TLS握手停滞与响应头等待。连接DOTNET内部截止涵盖建立过程，TCP包装也用可控clock；TLS握手测试是实际回环不响应ServerHello，系统计时1秒，未跳证书验证。慢dial专项在测试专用内部connector入口暂停，再走同一生产期限/取消；该等待不是OS SYN/DNS实测，不能把它签作所有网络行为已验。

headers从每次Send入场起计（包含连接/排队/头），first从成功headers进入正文计，idle从最近有效进展计。有效预算不重置总期限；重试仍仅明确429/503且无正文最多一次、Retry-After上限15秒。原clock参数只注入既有总预算，新增phaseClock注入阶段预算，生产都用TimeProvider.System；专项两个参数可取同一受控clock验证总预算优先，不改变原总预算观测断言。读取阶段origin保持单调，并在安装取消timer后复核原截止，防时钟推进/安装交错另起完整时限。

SSE framing、UTF8及1MiB事件限制保持。adapter额外报告有效活动，不更改原事件返回合同：首可识别协议/lifecycle、非空正文、变化的真实usage、一次结束进展/终止有效；ping、注释、未知事件、空delta、重复finish/相同usage、thinking/signature数据与未完成/无效字节不延展内容期限。后续空block生命周期也不无限买时间。每次实际底层ReadAsync使用当前阶段token，不能只Dispose流或WaitAsync假称已终止；阶段超时保留已输出正文，给CONNECT/HEADERS/FIRST/IDLE独立原因，总超时仍REQUEST_TIMEOUT，关闭/取消/旧身份依旧不接迟到事件、不重传未知结果。

JSON以ResponseHeadersRead增量读真实socket并保持整体1MiB上限、深度32。Utf8JsonReader保存state/BytesConsumed；只识别OpenAI choices/所选index0.message.content和Anthropic message/content text等已知结构，未知值和空白不刷新。已识别正文字符串的完整UTF8 scalar/合法escape可推进，半UTF8/半Unicode surrogate不算有效；重复属性拒绝。保守规则要求已知selected index/type才能计文本，若服务把这些字段放在巨大慢字符串之后，可能提前截止，用户可有限调大阶段值；最终仍完整协议验证后一次展示，未把JSON假拆成模型流式。完整对象后仍等待合法HTTP正文结算，不丢原中断/截断错误合同；不断尾随空白也受idle/total限制。固定公开测试/密钥/配置/权限最终门控保持。

原阶段设置4反例全失败保留。最初HTTP夹具未flush空headers，4项等Started超时属于夹具问题，另2项真实headers期限反例失败，原6/0 TRX保留但不冒称6个期限反例全部成立；修flush后原保存传输只改类名test-only重放6项全在精确期限断言失败。最终相同deadline/reason目标驱动生产代码；原重放体/原源字节保留。首生产12中2 OpenAI comment-only失败暴露Dispose不足，改真实ReadAsync token后全过。后续46中2：JSON reader派生异常精确类型断言改为JsonException族、虚拟时钟安装交错预算加固和真实timer屏障，未改秒数/目标原因/总上限，原失败均保留。

新61专项（Core15/Provider46）通过：范围/默认/记录冻结，慢dial、实际TLS、headers、首响应、keepalive/空内容/碎片停滞、JSON慢正文/UTF8/escape/未知数据、total优先、取消/Off/替换、消费者配置切换及旧快照、JSON重复/坏escape。真实Kestrel loopback脚本在响应flush后ack，clock控制只用于预算，未以MockHandler替代生产HTTP；测试-only老传输不在正式依赖。完整Core1315+Provider353=1668/1668、Node63/63，27locked Release零警告/错误；原1607逐名保留、27锁/规格/旧提示词字节保持。最终CI后未改运行/测试，文档/staging随后冻结。

再次断连后2026-10-12 01:44:46Z真实时间/HEAD/status/proc命令成功，HEAD646c、改动保全，上一组33专项已结束，当前无活动测试；无重复启动健康测试、扩权或第二写入者。独立新包及26旧包/22历史精确commit摘要、当前App/Provider发布字节与十个PE核验见summary；旧误覆盖后恢复事故继续记录。框架依赖包无原生DLL/安装器；P0/E06 Blocked，P1—P4 InProgress，176追踪/106整体NotRun，WPF/代理环境/真实DNS/多DPI/跨应用/DPAPI/安装/真实模型/远端CI仍未验，无真实key/模型/付费/依赖安装/扩权/发布。

技术依据：[Microsoft ConnectCallback](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.connectcallback?view=net-10.0)、[InitialRequestMessage](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttpconnectioncontext.initialrequestmessage?view=net-10.0)、[PlaintextStreamFilter](https://learn.microsoft.com/en-us/dotnet/api/system.net.http.socketshttphandler.plaintextstreamfilter?view=net-10.0)、[Utf8JsonReader增量读取](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/use-utf8jsonreader)。SDK编译/实际socket证据与这些接口依据分开，不用文档代替执行。
