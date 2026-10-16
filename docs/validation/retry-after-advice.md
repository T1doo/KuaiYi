# SPEC10.8：失败版本的 Retry-After 等待建议

从已独审并推送的 `8b902b1da0d253c9f43a0bc50e535a5703d6311b` 正常接续，工作分支 `dev/retry-after-advice`。当前指定环境/root唯一写入；先核文件、clean Git、默认网络远端开发分支及 main SHA/祖先，不恢复旧实例。`high_water_review` 只读独审，不运行SDK/网络/写入。范围仅原SPEC10.8中RATE_LIMITED的可用等待建议；不扩功能或推进阶段验收。

最终429/503失败响应可附加一个规范化 UTC 建议时间。每响应先用 NonValidated 读取单一 Retry-After 值，至多128字符，接受delta秒或HTTP-date；只保留0至86400秒内的未来/当前建议，负值、过期、非法、超长、多值、过大/溢出均不保留。上界是等待建议元数据的边界，**不把长等待截成短等待，也不改变自动重试上限**。只存UTC instant，不存原始头、错误体或任意服务端文字；每个响应包括无有效值都会覆盖临时建议，只有最终失败才发布到ModelEvent。规范化在原typed RetryAfter访问之前进行，不依赖其后头值表示。

ModelEvent经原完整身份/截止门进入TextTranslationCoordinator；OverlayResults只给该失败版本绑定建议，保存/回退精确携带原身份、来源、正文、usage及建议。Begin清当前建议；非限流/服务失败、Abandon或timeout转换不继承传入建议；旧失败版本自己的建议可正常回看，Clear与淘汰释放。GenerateEnvelopes和ProviderEventReceiver把事件改判timeout/cancel时也清建议。上一批独立来源序号高水位逻辑保持。

现有状态按钮的Tooltip/Automation与版本菜单复用FailureSummary，显示“服务端建议不早于 yyyy-MM-dd HH:mm:ss UTC 手动重试；这是收到最终响应时的建议，不是倒计时”。短按钮标识“暂时限流”。自动回退公告最新失败，手选旧失败读它自身建议；正文和usage仍属于被选版本。没有新入口、后台计时、自动恢复请求或模型参数改变。真实WPF可见性/键盘仍NotRun。

原SPEC10.7的429/503条件、最多一次重试、15秒阈值、缺省1秒及Task.Delay/重新发送门保留原字节。每响应仅插入元数据读取；没有把建议的24小时上界用于重试调度。真实HTTP用例证明首0秒允许原有一次重试，最终429/503不再重试；16秒/120秒直接终结并显示建议。非法超长数字被.NET typed解析视为无有效值，原策略仍可按缺省1秒重试一次，这不是本片新增策略。

| 原始执行 | 实际结果 |
|---|---|
| r01 父生产红 | 4执行、0P/4F；两协议×delta/date，现有状态入口缺少建议；生产与父blob相等，红源码/执行DLL绑定保全 |
| r02 同测试绿 | 4/4；与r01同测试源码SHA和相同4个完整名称 |
| r03 | 新增超长数字用例错误假设原策略会解析成120秒、仅发送1次；实际typed解析为无有效值，发送2次，2项请求数断言失败。精确source/log/TRX保留，**不是产品红测或已证规范化绕界** |
| r04 | 新边界61执行57P/4F；新增归属测试把“另一来源尝试”上下文前缀误认为恢复B身份后仍不变。原版本与建议正确，精确失败源码/日志/TRX/DLL绑定保留；修正完整字符串的上下文预期，未降低版本数据断言 |
| r05 | 最终81/81、0失败/跳过；最终候选源码与执行DLL绑定 |
| r06/r08/r10 | Provider/Core/App locked-mode restore，无依赖/锁变更 |
| r07 完整Provider | **2047/2047**、0失败/跳过，父1966全部名称保留，恰好新增81 |
| r09 完整Core | **1551/1551**、0失败/跳过；Linux便携与本项目owned托管夹具，不冒充Windows/native实机 |
| r11/r12 | App、ImageWorker Release交叉构建各0警告/0错误 |

81例包括：4个父红/绿入口例（最终追加菜单断言）和2个超长实际HTTP例；28项有界解析/安全值构造；28项真实HTTP最终覆盖（两协议×429/503×delta/date/过期/缺失/非法/超长/超界）；4项两协议×流/JSON的A1/B1/B2/B3正文/来源/usage/建议/菜单/自动与手选归属，20次刷新及恢复入口不发送；8项两协议×Off/Stop/Clear/替换的目标队列迟到建议拒绝；4项16秒不重试；1项截止改判清建议；2项Core全身份/中性Abandon/不匹配失败类型/旧版建议保留与Clear。只改原PhaseHttpFixture夹具以提供逐响应状态与Retry-After；其它旧测试文件和断言不改。

[固定证据与原件清单](../test-evidence/2026-10-16-retry-after-advice.json) 保存红绿、错误测试候选、最终源/DLL绑定及全回归原件。独立代码/测试审查未发现P1/P2/P3，最终证据另核。旧图片间歇失败根因仍未实证，本次通过不代表已消除；图片流程生产源码未改、复用Off。250/600ms、来源/密码/完整性/取消/清理门保持，实际Windows/WPF/OCR/COM/nativeJob、模型及完整SPEC验收仍NotRun；P4-04/F06/F08和全部P6整体状态不抬。

所有dotnet显式DOTNET_GENERATE_ASPNET_CERTIFICATE=false；证书库/信任Unknown且不读改。Skia4.153.1四包许可待批、源码403不绕，不安装/运行；GitHub Actions先前拒绝，不查询/调度或换路，run数Unknown。无main/强推/部署/新费用/真实模型/凭据/安全权限/法律协议接受，保全未知锁、数据、日志和进程。
