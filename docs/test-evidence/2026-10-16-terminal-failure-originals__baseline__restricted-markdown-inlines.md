# SPEC §11.5：原生行内代码、粗体、斜体

从远端 `dev/overlay-reading-input` 的 `250548896d4f1d61669f20e9b575ed9d85c23bde`（父 `dd9c0796b3708acea92cb420128c035b6947bfe7`）建立正常开发分支 `dev/overlay-inline-markdown`。新容器实际仓库为 `/workspace/KuaiYi`；初始 HEAD/work 与远端 main 均为 `51d077481cf6303602fcf5d2718213da9cf05178`，工作树干净。先检查进程、worktree、文件和远端，未恢复旧实例；本任务单一写入者，独审代理仅只读。保留发现的未知 `.git/codex-index-refresh.lock`。默认网络下 `git ls-remote` 成功，默认 fetch refspec 仅取 main，第一次默认 fetch 未取得开发 ref，随后显式获取开发 ref 并核祖先后开分支。没有假设旧容器 artifacts/obj 已共享；本容器起初两者均不存在。

已读根 AGENTS、SPEC 的交互、测量、安全、§11.5 与执行边界；仓库未找到其他 AGENTS/SKILL。旧[块级切片记录](restricted-markdown.md)及原证据保留为历史。本文接续行内格式，P2-04 仍 InProgress，完整 §11.5 和 P2/B 组没有变为 Pass。

## 生产行为

`RestrictedMarkdown` 直接调用新增的 `RestrictedMarkdownInlines`。正式 `MarkdownFlowView` 对普通文本构造 Run，对行内格式构造包含 Run 的原生 Span；没有另建展示窗口。原 `OverlayMarkdown.RefreshMarkdown`、50ms FlushResult、MeasureBody、CopyResult、阅读守卫及取消/清空入口原字节保持。

| 支持范围 | 行为 |
|---|---|
| `**粗体**` / `__粗体__` | Span.FontWeight = Bold |
| `*斜体*` / `_斜体_` | Span.FontStyle = Italic |
| 1—32 个等宽、匹配的反引号 | 单行 Code Span；Consolas，显式 Normal weight/style，避免标题粗体影响代码 |
| 同一段落/标题/顶层单行列表内相邻格式 | 平面序列；普通片段合并为 Run |
| 反斜杠转义 ASCII 标点 | 展示被转义标点；未知转义和尾部反斜杠保留字面；代码内不解码转义 |
| 代码内 `*`、`_`、反斜杠、HTML、图片/链接文本 | 全部惰性字符，不解析、加载或执行 |
| 未闭标记、三重强调、嵌套/交叉、跨行行内段、歧义分隔符、词中下划线 | 整个所在块按原始 Source 字面显示，包括标题/列表前缀；已解析的局部格式不会漏掉后面的失败部分 |
| 未转义的链接/HTML/表格标记 | 整块纯文本；不创建 Hyperlink/Image/资源 URI/命令 |

这不是完整 CommonMark。代码内容的空格不折叠或规范化；复杂/跨行语法保留字面。星号允许词内强调，下划线保守要求外边界为空白或标点。既有围栏代码仍为单个等宽原始 Run。

每块 Source 完整保留原来的 CRLF/Unicode/块前缀；拼接所有块 Source 始终等于输入。行内片段另保留包含定界符/转义的 Source，其拼接等于传给行内解析器的正文；块级标题/列表前缀及行尾仍在块 Source。Text 为展示文本。工具栏复制仍取 `overlayResults.Text`，不会从渲染树重建 Markdown；测试核对的是这一原始数据，实际系统剪贴板及原生局部选择复制未运行。

流式时只重新解析变动尾段；完整标题/列表行、段落分隔与已闭围栏等稳定边界仍保留块及其行内对象，FlowView 因相同引用保留原生元素。非追加替换、版本切换和整份容量回退允许重建。CR 尾等待可能迟到的 LF；未闭强调随尾段更新，闭合后才变为格式。没有递归、正则、外部解析器或新依赖；单次解析受有限输入约束，不宣称恒定时间或 WPF 帧时间达标。

继续使用 120000 UTF-16 / 2048 块限制，新增文档累计 4096 行内片段上限（Plain/Code/空段等无片段块计 1）。单块解析也以 4096 为界，稳定前缀的片段计数被保留；越任一上限整份有限结果退为一个完整纯文本块，不截断。超过原上游 1048576 单元仍拒绝且不改既有模型。清空/重置时同时清稳定片段计数。

## 实际验证

本环境 Linux x86_64，SDK 锁定 10.0.401。每条 dotnet 命令前都显式 `DOTNET_GENERATE_ASPNET_CERTIFICATE=false`，父环境也 export，以便测试子进程继承；复用既有 activate.sh 提供的 SDK/CLI home。原证书存储/信任仍 Unknown，不读取证书库或证书配置，不 trust/delete；本轮日志没有新的证书安装提示，不据此推断存储状态。

实际命令（均在仓库根；原始输出见 `artifacts/inline-markdown/`）：

```bash
export DOTNET_GENERATE_ASPNET_CERTIFICATE=false
source /workspace/.kuaiyi-setup/activate.sh
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet --version
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet restore tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --locked-mode
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet restore src/KuaiYi.App/KuaiYi.App.csproj --locked-mode -p:EnableWindowsTargeting=true
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --filter 'FullyQualifiedName~RestrictedMarkdown|FullyQualifiedName~MarkdownProductionBridge' --logger 'trx;LogFileName=inline-focused-fixed.trx' --results-directory artifacts/inline-markdown
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet test tests/KuaiYi.Provider.Tests/KuaiYi.Provider.Tests.csproj --no-restore -c Release --logger 'trx;LogFileName=provider-full.trx' --results-directory artifacts/inline-markdown
DOTNET_GENERATE_ASPNET_CERTIFICATE=false dotnet build src/KuaiYi.App/KuaiYi.App.csproj --no-restore -c Release -p:EnableWindowsTargeting=true
```

- 最终专项 **72/72 Pass，0 Fail/Skip**；完整 Provider **610/610 Pass，0 Fail/Skip** = 前批 566 + 本批 44（行内模型 38、生产桥接 6）。Core.Tests 完整工程本轮 NotRun。
- 行内模型直接执行真实 Core：9 份新样例的全部两段切点/逐 UTF-16 单元；30 轮固定种子随机分片；星号/下划线/反引号及转义拆开、Unicode/CRLF、代码惰性、未闭/复杂整块回退、稳定引用、4096 精确两侧与稳定前缀累计、32/33 代码分隔符及 120000 未闭长尾。原 12 样例等 28 项块级回归保留；原粗体“不支持”样例改为仍不支持的三重嵌套，流式相等断言增加逐片段比较，没有删测试或削弱断言。
- Provider 工程直接链接实际 `MarkdownFlowView.cs` 和 `OverlayMarkdown.cs`，用明确标注的数据端口执行生产构造及 Refresh/Clear：两份文档、Run/Span 样式属性、稳定元素引用、字号更新、仅测量后正文刷新、实际 OverlayResults/RuntimeGate 的三版本淘汰/选中/失败回退、原始结果文本、取消/锁屏/清空及迟到拒绝。数据端口不模拟 WPF 布局、字体继承、选择、路由输入或 dispatcher，不是实际 WPF 运行证据。
- App Release 真实 Windows 目标交叉构建 **0 Warning / 0 Error**；不是启动 App 或桌面验收。
- 独审代理按用户“独审”要求只读核生产代码、测试及回执，未运行 SDK/改文件；最终审查见固定证据中的范围和结论。

失败历史不抹除：首轮专项 72 项中 **71 Pass / 1 Fail**，稳定前缀容量夹具把 `*y*` 替换成 `*z*`，属于非追加更新，按生产契约应重建；修正为追加 `*y* grows` 后检查复用，再追加触发整份回退。生产代码未因此改动；原 `focused-first.log` / `inline-focused.trx` 与修后回执分别保存且记录 SHA256。两次 locked restore 均成功，App 首次交叉构建成功。

另一次只读 `gh api repos/T1doo/KuaiYi/actions/runs` 查询（基线 SHA 过滤）返回 **Forbidden/Exit1**，已停止，不换通道绕过拒绝。八份 workflow 原字节不变，新分支不匹配其 push 分支；本批没有发起 workflow、Windows、CMake 或真实模型。远端 Actions 实际运行数 **Unknown（查询被拒）**，不能将静态触发条件审查写成服务端 0-run 证明。

## 保持与剩余

250/600ms、来源/密码/完整性/取消/清理门、三版本策略、离线生产复用关闭均保持，相关生产文件无差异；不碰原 CMake 未知根因、Windows 探针和 CI。八份 workflow、43 份依赖锁与全部既有证据文件原字节保持。无 main 修改、强推、部署、新费用/凭据/权限/协议或真实模型调用。

基础表格和代码块无折行的横向滚动仍未实现；嵌套/多行列表与复杂行内语法仍字面回退；不加入图片/链接打开。WPF 真正字体度量、布局/DesiredSize、运行时原生元素复用、选择/复制、阅读锚点和输入刷新交错、B06/B07/完整 B 组均 **NotRun**。停止/容量回退和尾段重排会改变视觉树，真实阅读位置需 Windows 单独验证，不用数据端口或编译成功冒充。

精确源码与运行回执 hash、失败记录及保持范围见[固定证据](../test-evidence/2026-10-15-restricted-markdown-inlines.json)。推送后的远端 SHA、main/旧开发分支及未提交差异另附最终交接回执。
