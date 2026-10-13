# SDK 仅编译取证与修复

起点为干净 `6f26b02be9c21fe90b4f0144a3fe6907a3e5fcc0`，分支 `dev/p0-com-selection-diagnostic`。上次 run38385630442/job115256586368 的固定状态为 SdkBuildFailed，SDK COM 六场景 NotRun；没有足够证据推断是签名、编译警告、链接、manifest 或其他构建问题。

监督方现明确授权公开有限官方错误码、固定仓库源码类别及有界行号，并允许同一免费范围内仅编译取证；仍不得公开任意路径、命令行、原文、环境或私有日志。没有新的权限、凭据、安装、费用或生产默认切换。

## 实现与纯测试

`sdk-compile-evidence.ps1` 只接受有限的官方 MSVC/链接/MSBuild/manifest 代码白名单；未列出的匹配代码归 Unknown。每项只含 Code、Source、Line：源码类别 Probe/Policy/Unknown，位置只识别 ComCaptureProbe.cpp 与 SelectionPolicy.h 且仅解析错误分隔符之前的定位信息，行号 1—100000。未知文件或坏/越界位置为 Unknown/null。输出去重、至多16项；输入至多4Mi字符，每行至多8192字符。丢弃整条消息、路径、命令、环境、指针、文件名与私有日志，不将空证据当成构建成功。

合成秘密测试覆盖 private path/token/环境/指针/消息、重复错误、未知代码/文件、消息中伪造第二诊断、0/负/大数/字符串行号、文件名后缀与前缀、MSBuild前缀、官方 command-line warning、manifest错误、空/超长输入和16项上限。**37/37 Pass**。环境检查调用点隔离2—6输出流，新增含合成秘密的全流测试确认成功返回值保留而其余流不可见。独审发现此缺口后修正，仅修改新runner调用点。仅编译 runner 的 PowerShell 语法解析 Pass；该纯测不是 Windows SDK 编译证明。

## Windows 门槛

新 workflow 仅配置/编译 `tools/KuaiYi.ComCaptureProbe`：现有 windows-2022、VS2022/v143精确补丁、SDK26100，read-only，15分钟上限。保留 `/W4 /WX`，不改变原生源码以猜测规避错误。依赖及SDK缺失标为 Blocked；configure/build各自记录实际进程退出码，摘要 ExitCode 独立表示门槛结论。raw输出仅本地忽略文件；公开仅白名单结构。只有构建返回0且预期exe确实存在才 BuildStatus Pass，未知/空错误码仍保持真实失败。

入口不启动生成的exe、浏览器、用户应用、UIA、OCR、剪贴板或模型，不执行六场景、120项、GetData；因此 Uia 始终 NotRun。先独审精确冻结，再执行一次仅编译 CI；取得实际错误后做针对源码/构建配置的最小修复，保留所有安全/时间门与 `/WX`，不凭猜测改接口。是否已有 Windows 修复后验证必须分开记录，不将离线修改或纯测试记为 SDK 编译通过。

原 GetData CODE 与 Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 保留独立，不重跑。SDK COM 实际取词、原两个超时、Win11 物理划词、PDF/Markdown、多DPI/安装与P0整体无新增通过结论。


## 本次仅编译实测

代码与 Actions 执行 SHA 均为 `ac0d7f421059a5267a69e1be82950ff7a7846944`，tree `3be0583d5aaea242db546f06cde3cd9160450f13`，父提交 `6f26b02be9c21fe90b4f0144a3fe6907a3e5fcc0`。独审本地冻结 `6e10b2a841bd7273550ac85d743c64c17a9cc8a3` 与其树完全一致。仅一次 [run38389154626](https://github.com/T1doo/KuaiYi/actions/runs/38389154626)，job115268311522，attempt1，结论 failure。

Windows 脱敏合约 **37/37 Pass**。ConfigureStatus Pass / ConfigureExit **0**；BuildStatus Fail / BuildExit **1**；摘要 Status Fail / Reason SdkBuildFailed / ExitCode **3**。实际公开编译证据仅两项：`Unknown / Unknown / null` 与 `C2146 / Unknown / null`。Unknown 不代表具体错误码、SDK文件或仓库行号；不得从它推断签名、包含顺序、警告或链接根因。

Uia **NotRun**，ModelCalls **0**；未启动生成程序、Broker、浏览器或六场景。job日志私下存入忽略的 `artifacts/windows-selection/windows-sdkcompile-38389154626.local.log`：17460 bytes，SHA256 `cc5eec8b8190571557c1ff712d173405e60c4658d87e3c808ed418ca93813aa7`。runner内部原始编译输出未上传，任务结束后不可回收；此哈希是公共job日志的本地留证，不是内部原始编译log。

已核对微软 [C2146说明](https://learn.microsoft.com/en-us/cpp/error-messages/compiler-errors-1/compiler-error-c2146?view=msvc-170) 与 [win32metadata官方UIAutomation头文件](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/UIAutomation.h)：C2146具有多种可能成因，官方头文件内容不能替代当前SDK26100的具体出错位置。当前证据尚不足以确定普通源码修复；原生源码保持未修改，`/W4 /WX` 保留，SDK构建仍 **Fail**，修复后SDK构建 **NotRun**。一次仅编译授权已消费，不自动制造多轮CI。需要监督方据这些实际字段决定下一步最小取证范围，禁止直接继续六场景或声明P0通过。


## 定位投影补正（第二次授权范围）

监督方明确允许在相同安全/费用边界增加固定SDK头文件类别，并至多一次必要的免费仅编译CI。只读核查确认旧parser可识别官方 `filename(line,column): error code:` 格式（包括完整Windows路径），此前 Unknown/null 不证明通用正则失效：只允许两个仓库basename，所以SDK头文件被归Unknown；C4430未在码白名单，不能据此反推上一实际Unknown即C4430。

新增官方 C2059/C4430及预定义SDK/OLE/RPC头、WRL client头映射：公开类别仅 SdkUiaCore、SdkUiaClient、SdkUiaApi、SdkOle、SdkRpc、WrlClient（另保留仓库Probe/Policy）。16个固定SDK basename，WRL必须额外匹配 wrl/client.h 路径后缀；其他client.h不被接受。所有来源仍按固定basename分类，分类不是文件真实身份校验。任意其他文件归Unknown/null；位置仅解析首个诊断分隔符之前，兼容有界MSBuild前缀、成对路径引号、列前空格；不返回路径、原basename、列号或正文。行号仍1—100000，16条/输入上限/去重/输出三字段未变。新增格式正反例与全部17映射、同码不同文件/行、未知client头、合成秘密测试，**91/91 Pass**。

逐项静态核对微软官方 win32metadata：UIAutomation.h包含Core/Client/CoreApi；Core导入oaidl/oleacc，二者引windows/ole2，oaidl引objidl；Client引Core。生产源参数对应声明：ElementFromHandle(UIA_HWND, Element**)，ElementFromPoint(POINT, Element**)，GetCurrentPatternAs(PATTERNID, REFIID, void**)，TextPattern.GetSelection(TextRangeArray**)，TextRange.GetBoundingRectangles(SAFEARRAY**)，TextRange.GetText(int, BSTR*)，CurrentNativeWindowHandle(UIA_HWND*)；WRL ComPtr输出地址与对应指针类型相符。未发现可从现有证据离线确定的接口/包含修复，因此不修改接口与所有权逻辑，/WX保留。依据监督方追加指导和微软UIAutomationDocumentClient示例，在windows.h后、UIAutomation.h前显式include ole2.h作为单行候选修复；聚合头可能已有间接包含，不宣称已确定根因。这些上游main声明核对不是对实际SDK26100编译成功的替代。

参考：[微软同类示例包含顺序](https://github.com/microsoft/Windows-classic-samples/blob/main/Samples/UIAutomationDocumentClient/cpp/UiaDocumentClient.cpp)、[MSVC定位格式](https://learn.microsoft.com/en-us/cpp/build/formatting-the-output-of-a-custom-build-step-or-build-event?view=msvc-170)、[官方C4430文档](https://github.com/MicrosoftDocs/cpp-docs/blob/main/docs/error-messages/compiler-warnings/compiler-warning-c4430.md)、[官方UIAutomationClient声明](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/UIAutomationClient.h)。新一次CI仍只编译、不启动UIA，需精确冻结独审后执行；新结果待测。


## 本轮唯一仅编译 CI：Pass

代码与 Actions SHA 均为 `1e69b5d075952e82c43cba616e460d793fa64130`，tree `f300fa85df40b5c3bdccee910a51342054037c56`，父提交 `acd67c4a81e8908ba7d589231a308246bd84b5e5`。精确独审本地冻结 `694ca4a84db216adabd40091815c5712f4e7bf0f` 与其树一致。仅一次 [run38390705858](https://github.com/T1doo/KuaiYi/actions/runs/38390705858)，job115273500883，attempt1，结论 **success**。

Windows 脱敏合约 **91/91 Pass**。实际 ConfigureExit **0** / ConfigureStatus Pass；实际 BuildExit **0** / BuildStatus Pass；预期 exe 存在（runner成功门已核），总 Status Pass / Reason Completed / ExitCode **0**，CompilerFaults为空。CMake原有 `/W4 /WX /EHsc`、SDK/VS版本选择与所有接口代码未改，唯一原生差异为显式 `#include <ole2.h>`。

这证明该版本SDK原生编译/链接与exe存在门通过；此前C2146在当前版本已不再导致构建失败。由于上一原始诊断位置未保留，不声称已确定具体出错头/符号或原始Unknown码；不将无报错误写为旧位置已成功提取。固定SDK类别的格式解析已通过合成正反例，当前真实成功日志没有错误可供验证位置投影。

公开字段 Uia **NotRun** / ModelCalls **0**。严格未运行生成exe、Broker、浏览器、六场景、120项或GetData。SDK真实取词、原GetText边界四次0xC0000005和两个超时是否消失、Win11物理划词/PDF/Markdown/DPI/安装卸载及P0整体仍无新增通过结论。下一步是另行授权的有界SDK COM六场景；编译Pass不能替代这项实测。

公共job日志本地忽略留证 `artifacts/windows-selection/windows-sdkcompile-38390705858.local.log`，**17283 bytes**，SHA256 `3857f8ca5c5a144f37ab6fa394ba79fff3492c76bf88ce2efd86b4f98e31c6e8`。这仍不是runner内部原始编译log。原GetData代码与Actions SHA `220060f813a3640487d593920a1b1824aa779a00` 独立保留、未重跑。
