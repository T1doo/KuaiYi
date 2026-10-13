# 默认关闭的官方 SDK COM 取词差分

起点为干净 HEAD `05d5330885b46ffdefcd1ef62a21d67826902f6b`。独立开发分支 `dev/p0-com-selection-diagnostic`；不改 main，不切生产默认 Broker，不加入安装包或 staging，不安装新 SDK/组件，不新增权限、凭据或付费调用。

## 依据与差分范围

[Microsoft TextRange_GetText 官方页](https://learn.microsoft.com/en-us/windows/win32/api/uiautomationcoreapi/nf-uiautomationcoreapi-textrange_gettext)标记旧导出为弃用并建议 UIA COM。`GetText(100002)` 参数和旧 BSTR 声明合法；此前四次 GetText/Enter/18 + 0xC0000005 尚未证明故障指令或模块，不据此换 STA、改旧 IntPtr 签名、强制 ReleaseCOM、降低文本上限或恢复访问违规进程。

新的诊断可执行文件 `tools/KuaiYi.ComCaptureProbe` 直接包含 SDK `UIAutomation.h`，使用 SDK IUIAutomation、IUIAutomationTextPattern、IUIAutomationTextRange 的完整声明和 WRL ComPtr。CoInitializeEx 固定 COINIT_MULTITHREADED；正常 RAII 释放自身持有的 COM 引用，再配对 CoUninitialize，不释放他人引用或改变 apartment。SAFEARRAY 由 SDK读取/解锁/销毁；BSTR 以 SysStringLen 保留嵌入 NUL、复制后清零并正常 SysFreeString，不手写接口 vtable 或把旧导出签名改成 IntPtr。

这是一个原生 COM 后端差分，不声称只改一个调用即可证明因果。沿用原控制视图焦点/命中祖先各八层、绑定至原 HWND 根后加入候选、PID 筛选的 BFS、64 候选、GetSelection→bounds→GetText、64 ranges、聚合原始 256 rectangles、100002 UTF16 哨兵和 50000 Unicode 标量限额。原始 bounds 先按 WPF 非正尺寸→Empty 规则处理，原始个数仍先计入上限；无可用几何时自动模式仍不得成功。正数文本上限不缩短，第一次单 range GetText 为 100002。

默认排除 KuaiYi 与终端进程、密码、较高完整性源，保持前台 HWND/PID/start 身份、成功前复核 held source start/active 与 HWND、自动控制/选区几何关联、250ms capture 和父原有 600ms 绝对上限、三次失败暂停及原清理。新增 native SID 有效性及 SAFEARRAY shape/type 检查为 fail-closed；不读 DOM/ValuePattern/整篇文本、控件 Name、标题或剪贴板。copy metadata 兼容证据只记录，不触发复制回退。

仍用原 stdout 双帧、请求 GUID、有限元数据和原父 SelectionBrokerClient 的协议、期限、失败暂停、读任务及真实子进程清理。仅诊断开关选择新 exe，默认脚本与 App 保持原 .NET Broker。现有非持久 marker 与固定 enum/序号继续用于非零原退出；SDK bounds 数据复制及释放在 GetText 前完成，GetText Returned 在 BSTR 复制/释放后发布。不得由 marker 认定故障指令。

## 有界运行及验收规则

`run-windows-selection-ci.ps1 -DiagnosticOnly -ComDiagnostic` 仅现有六个公开合成视图各调用一次 SDK COM；第一次读取按原参数 100002。内部按 ordinal 精确比较 expected text，公开只投影封闭状态、match、HRESULT/exit、已有 marker 和受限 backend 标签；无原文、路径、指针、dump、异常消息。原始 stdio/编译日志仅留 runner，外层只输出白名单摘要。既有安装签名/path、独占 profile/Job/start/HWND、清理规则不变。历史 .NET 六场景日志为比较基线，不在同一 job 重跑旧崩溃或凑十二个调用；跨 runner 时间差不能当因果证明。

workflow 只限新 dev 分支、read-only 权限、现有 windows-2022/VS2022/MSVC14.44/SDK26100、15 分钟；不修改安装依赖/网络安全、不跑120或GetData。SDK 编译或环境缺失为 Fail/Blocked，而不是取词 Pass。纯测试和精确独审后，仅一次新的六场景差分。

如果 SDK COM 精确取词并且在原预算内，只作为候选修复进一步进行定向安全回归；不立即切生产。若同样崩溃，停止猜测；超时或来源/精确文本不一致判未确定。两个先前 Chrome HTML/只读超时独立保留缺口，不能由其他四视图成功覆盖。

本地 portable 策略纯测 40/40，验证限额、Unicode、源身份/时间、排除及完整性、自动几何关联、坏几何和聚合上限的原子失败。PS 摘要 62/62，新增 backend 必须与显式 COM DiagnosticOnly 一致、禁止套入兼容 Pass。实际托管 probe 类型检查 0 warning / 0 error。Windows SDK 原生编译、COM 及真实跨进程 SDK 差分尚未运行；Linux 纯策略不是 Windows SDK 编译或 UIA 通过证明。

原 GetData CODE 与 Actions SHA 独立保留为 `220060f813a3640487d593920a1b1824aa779a00`（run38347142627/job115131066822），不重跑。P0、Win11 物理划词、PDF、Markdown、多屏 DPI、安装卸载均无新增通过结论。

## 本轮 Windows 构建阻塞（尚未执行 COM 场景）

源码/Actions 完整 SHA `98dfa217725fae30e2d9c0a3a37e3d18002cbcbe`，tree `ab869388b46158972fcef9a440a0086fb46f9ab0`，唯一 parent `05d5330885b46ffdefcd1ef62a21d67826902f6b`。精确独审冻结 `934eb3612281b87e2535f7450f39582f98bb09b6` 为同树。[run 38385630442](https://github.com/T1doo/KuaiYi/actions/runs/38385630442)，[job 115256586368](https://github.com/T1doo/KuaiYi/actions/runs/38385630442/job/115256586368)，attempt 1，结论 failure。

62 项摘要检查在 Windows 通过，真实 WPF GeometryContracts Pass / Checks 4。固定摘要为 **Fail / SdkBuildFailed / exit 3 / ModelCalls 0**。SDK 配置已完成，但 `/W4 /WX` 构建返回非零；此阶段位于 probe child 启动之前，因此六个 SDK COM 取词场景 **NotRun**。不能将本结果写成 SDK GetText 崩溃、0/6 取词失败、SDK 编译通过或候选修复成功。旧的四个访问违规与两个超时仍仅属于前次 .NET 路径证据。

原日志本地忽略保存为 `artifacts/windows-selection/windows-com-38385630442.local.log`：17,974 bytes，SHA256 `fe4c91f7acf597d5e5b4c81eff50ea849491a40202347c2f3339f947800f3958`。按照现有隐私边界，编译 stdout/stderr 重定向至 ephemeral runner 的 build.local.log，公开日志中没有 MSVC/LNK 错误码或源行；本环境无法读取已结束 runner 的该文件。未依据猜测改 SDK 接口、关 /WX、降限额或重复 CI。

本轮仅启动这一个有界 job；下一步最小取证缺口是原生编译失败的错误码及公开源码行位置，而非继续运行 UIA。可审阅方案：仅编译原生诊断 exe，纯测试验证报告器从本地编译日志中提取有限 MSVC/LNK 错误码枚举、固定源码类别（ComCaptureProbe/SelectionPolicy/SDK/Link）和有限行号，丢弃完整消息、路径、命令、源码内容与任意文本；独审后进行一次编译门槛检查，再决定是否存在具体可修复编译问题。该方案暂未实现或执行；因当前委托明确限定公开字段为封闭状态、match、HRESULT/exit 和已有标记，新增编译定位字段须由监督方审阅放行，不能自行扩大公开日志范围。没有新的系统权限、凭据或组件安装缺口。
